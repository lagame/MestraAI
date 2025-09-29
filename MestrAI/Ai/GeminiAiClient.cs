using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Options;

namespace RPGSessionManager.Ai
{
    /// <summary>
    /// Gemini client with secure auth order:
    /// 1) Service Account via ADC (GOOGLE_APPLICATION_CREDENTIALS / GCE-GKE metadata)
    /// 2) Service Account file path from appsettings (relative to ContentRoot)
    /// 3) API Key fallback (querystring) as last resort
    ///
    /// Default model: gemini-1.5-flash (fast). Fallbacks: flash-8b → pro.
    /// </summary>
    public class GeminiAiClient : IAiClient
    {
        private readonly HttpClient _http;
        private readonly GeminiConfig _cfg;
        private readonly ILogger<GeminiAiClient> _log;
        private readonly string _contentRoot;
        private readonly JsonSerializerOptions _jsonOptions;

        public GeminiAiClient(
            HttpClient http,
            IOptions<AiConfig> cfg,
            ILogger<GeminiAiClient> log,
            IHostEnvironment env)
        {
            _http = http;
            _cfg = cfg.Value.Gemini;
            _log = log;
            _contentRoot = env.ContentRootPath;
            _jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public Task<string> GenerateResponseAsync(string prompt)
            => GenerateResponseAsync(string.Empty, prompt);

        public async Task<string> GenerateResponseAsync(string systemPrompt, string userPrompt)
        {
            var started = DateTime.UtcNow;

            try
            {
                // Resolve auth first
                var auth = await GetAuthAsync();

                // Build request body (v1beta generateContent)
                var body = new
                {
                    system_instruction = string.IsNullOrWhiteSpace(systemPrompt) ? null : new
                    {
                        parts = new[] { new { text = systemPrompt } }
                    },
                    contents = new[]
                    {
                        new { parts = new[] { new { text = userPrompt } } }
                    },
                    generationConfig = new
                    {
                        maxOutputTokens = _cfg.MaxTokens,
                        temperature = 0.7
                    }
                };
                var serialized = JsonSerializer.Serialize(body, _jsonOptions);

                // Try preferred models in order
                var models = new[] { _cfg.Model, "gemini-1.5-flash-8b", "gemini-1.5-pro" }
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Distinct()
                    .ToArray();

                HttpResponseMessage? rsp = null;
                string? modelTried = null;
                string? lastErrorBody = null;

                // Timeout per call
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(5, _cfg.TimeoutSeconds)));

                foreach (var model in models)
                {
                    modelTried = model;
                    var endpoint = (_cfg.Endpoint ?? string.Empty).Replace("{model}", model);
                    var uri = auth.Kind == AuthKind.Bearer ? endpoint : $"{endpoint}?key={auth.ApiKey}";

                    rsp = await SendWithRetryAsync(
                        () => BuildRequestMessage(uri, serialized, auth),
                        Math.Max(0, _cfg.MaxRetries),
                        cts.Token);

                    if (rsp.IsSuccessStatusCode)
                        break;

                    lastErrorBody = await SafeReadAsync(rsp);
                    _log.LogWarning("Gemini model {Model} failed with {Status}. Body: {Body}",
                        model, rsp.StatusCode, Truncate(lastErrorBody, 500));
                }

                if (rsp is null || !rsp.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Gemini failed after trying: {string.Join(", ", models)}. " +
                        $"Last status: {rsp?.StatusCode}. Body: {Truncate(lastErrorBody, 1000)}");
                }

                var payload = await rsp.Content.ReadAsStringAsync(cts.Token);
                var text = ExtractTextOrNull(payload);

                _log.LogInformation("Gemini OK (model {Model}) in {Ms} ms",
                    modelTried, (DateTime.UtcNow - started).TotalMilliseconds);

                return text ?? string.Empty;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Gemini failed after {Ms} ms", (DateTime.UtcNow - started).TotalMilliseconds);
                throw;
            }
        }

        public string GenerateResponse(string prompt)
            => GenerateResponseAsync(prompt).GetAwaiter().GetResult();

        public string GenerateResponse(string systemPrompt, string userPrompt)
            => GenerateResponseAsync(systemPrompt, userPrompt).GetAwaiter().GetResult();

        // ---------- Helpers ----------

        private static string Truncate(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s[..max] + "...";
        }

        private static async Task<string?> SafeReadAsync(HttpResponseMessage rsp)
        {
            try { return await rsp.Content.ReadAsStringAsync(); }
            catch { 
                return null; 
                }
        }

        private HttpRequestMessage BuildRequestMessage(string uri, string serializedBody, AuthInfo auth)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, uri);
            req.Content = new StringContent(serializedBody, Encoding.UTF8, "application/json");
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (auth.Kind == AuthKind.Bearer)
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.BearerToken);

            return req;
        }

        private async Task<HttpResponseMessage> SendWithRetryAsync(
            Func<HttpRequestMessage> requestFactory,
            int maxRetries,
            CancellationToken ct)
        {
            var attempt = 0;
            HttpResponseMessage? rsp = null;

            while (true)
            {
                attempt++;
                using var req = requestFactory();
                rsp = await _http.SendAsync(req, ct);

                // Retry only on transient 5xx
                if ((int)rsp.StatusCode < 500 || attempt > Math.Max(1, maxRetries + 1))
                    return rsp;

                var delayMs = 200 * attempt;
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), ct);
            }
        }

        private enum AuthKind { Bearer, ApiKey }
        private record AuthInfo(AuthKind Kind, string? BearerToken = null, string? ApiKey = null);

        /// <summary>
        /// Resolve credentials in the order:
        /// 1) ADC (GOOGLE_APPLICATION_CREDENTIALS or platform metadata)
        /// 2) Service account JSON path from appsettings
        /// 3) API Key env fallback
        /// </summary>
        private async Task<AuthInfo> GetAuthAsync()
        {
            var scopes = new[] { "https://www.googleapis.com/auth/generative-language" };

            // 1) ADC
            try
            {
                var adc = await GoogleCredential.GetApplicationDefaultAsync();
                if (adc != null)
                {
                    var scoped = adc.CreateScoped(scopes);
                    var token = await scoped.UnderlyingCredential.GetAccessTokenForRequestAsync();
                    if (!string.IsNullOrWhiteSpace(token))
                        return new AuthInfo(AuthKind.Bearer, BearerToken: token);
                }
            }
            catch
            {
                // continue to next
            }

            // 2) CredentialsFile from appsettings (relative to ContentRoot if not rooted)
            if (!string.IsNullOrWhiteSpace(_cfg.CredentialsFile))
            {
                var path = _cfg.CredentialsFile!;
                if (!Path.IsPathRooted(path))
                    path = Path.Combine(_contentRoot, path);

                if (File.Exists(path))
                {
                    var cred = GoogleCredential.FromFile(path).CreateScoped(scopes);
                    var token = await cred.UnderlyingCredential.GetAccessTokenForRequestAsync();
                    if (!string.IsNullOrWhiteSpace(token))
                        return new AuthInfo(AuthKind.Bearer, BearerToken: token);
                }
            }

            // 3) API Key as last resort (dev emergency)
            var envName = string.IsNullOrWhiteSpace(_cfg.ApiKeyEnv) ? "GEMINI_API_KEY" : _cfg.ApiKeyEnv;
            var apiKey = Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrWhiteSpace(apiKey))
                return new AuthInfo(AuthKind.ApiKey, ApiKey: apiKey);

            throw new InvalidOperationException(
                "Missing credentials. Use GOOGLE_APPLICATION_CREDENTIALS or Ai:Gemini:CredentialsFile. " +
                "As a last resort, configure an API key in the environment variable.");
        }

        /// <summary>
        /// Extracts the primary text from Gemini v1beta responses.
        /// </summary>
        private static string? ExtractTextOrNull(string json)
        {
            using var doc = JsonDocument.Parse(json);

            // Typical path: candidates[0].content.parts[0].text
            if (doc.RootElement.TryGetProperty("candidates", out var candidates)
                && candidates.ValueKind == JsonValueKind.Array
                && candidates.GetArrayLength() > 0)
            {
                var first = candidates[0];
                if (first.TryGetProperty("content", out var contentEl)
                    && contentEl.ValueKind == JsonValueKind.Object
                    && contentEl.TryGetProperty("parts", out var parts)
                    && parts.ValueKind == JsonValueKind.Array
                    && parts.GetArrayLength() > 0)
                {
                    var p0 = parts[0];
                    if (p0.TryGetProperty("text", out var textEl))
                        return textEl.GetString();
                }
            }

            // Fallback: return the full JSON (useful for debugging / alternate shapes)
            return json;
        }
    }

    // ---------- Configuration classes (bind to appsettings: "Ai") ----------

    public class AiConfig
    {
        public string Provider { get; set; } = "Gemini";
        public GeminiConfig Gemini { get; set; } = new();
    }

    public class GeminiConfig
    {
        /// <summary>Default model, e.g., "gemini-1.5-flash".</summary>
        public string? Model { get; set; }

        /// <summary>Environment variable name for API key fallback.</summary>
        public string? ApiKeyEnv { get; set; }

        /// <summary>Relative or absolute path to a service account JSON file.</summary>
        public string? CredentialsFile { get; set; }

        /// <summary>v1beta generateContent endpoint template.</summary>
        public string? Endpoint { get; set; }

        /// <summary>Max output tokens (keep modest for latency/cost; raise on demand).</summary>
        public int MaxTokens { get; set; } = 4096;

        /// <summary>Per-call timeout in seconds.</summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>Max transient retries for 5xx.</summary>
        public int MaxRetries { get; set; } = 2;
    }
}
