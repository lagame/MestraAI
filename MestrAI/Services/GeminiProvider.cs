using Google.Apis.Auth.OAuth2;
using RPGSessionManager.Models;
using System.Text;
using System.Text.Json;

namespace RPGSessionManager.Services;

/// <summary>
/// Provedor de IA usando Google Gemini (Vertex AI)
/// Implementa:
/// - GenerateCharacterReplyAsync / GenerateNpcReplyAsync via :generateContent
/// - GetEmbeddingAsync via :embedContent (text-embedding-004 por padrão)
/// - MergeSummariesAsync via :generateContent (prompt específico p/ mesclagem curta)
/// </summary>
public class GeminiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiProvider> _logger;
    private readonly string _projectId;
    private readonly string _location;
    private readonly string _model;
    private readonly string _embeddingModel;
    private readonly string _credentialsPath;

    // Configs opcionais para mesclagem
    private readonly double _mergeTemperature;
    private readonly int _mergeMaxTokens;

    public string ProviderName => "Gemini";

    public GeminiProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _projectId = configuration["AiSettings:GeminiProject"] ?? "your-project-id";
        _location = configuration["AiSettings:GeminiLocation"] ?? "southamerica-east1";
        _model = configuration["AiSettings:GeminiModel"] ?? "gemini-2.5-flash-lite";
        _embeddingModel = configuration["AiSettings:GeminiEmbeddingModel"] ?? "text-embedding-004";
        _credentialsPath = Path.Combine(Directory.GetCurrentDirectory(), "secrets", "google-credentials.json");

        _mergeTemperature = TryParseDouble(configuration["AiSettings:GeminiMergeTemperature"], 0.3);
        _mergeMaxTokens = TryParseInt(configuration["AiSettings:GeminiMergeMaxTokens"], 256);

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri($"https://{_location}-aiplatform.googleapis.com");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MestrAI/0.4.5");
    }

    public async Task<string> GenerateCharacterReplyAsync(CharacterSheet character, string context, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildCharacterPrompt(character, context);
            return await GenerateResponseAsync(prompt, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar resposta do personagem {CharacterName} via Gemini", character.Name);
            throw;
        }
    }

    public async Task<string> GenerateNpcReplyAsync(string npcName, string npcContext, string conversationContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildNpcPrompt(npcName, npcContext, conversationContext);
            return await GenerateResponseAsync(prompt, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar resposta do NPC {NpcName} via Gemini", npcName);
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var testPrompt = "Responda apenas 'OK' para testar a conexão.";
            var response = await GenerateResponseAsync(testPrompt, cancellationToken: cancellationToken);
            return !string.IsNullOrEmpty(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar conexão com Gemini");
            return false;
        }
    }

    /// <summary>
    /// Retorna embedding (vetor float) do texto informado usando Vertex AI :embedContent.
    /// </summary>
    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();

            var requestBody = new
            {
                // Alguns formatos aceitam "content" com parts; deixando no formato mais aceito pelo Vertex
                content = new
                {
                    parts = new[]
                    {
                        new { text = text ?? string.Empty }
                    }
                },
                // Opcional: taskType pode ajudar a calibrar (RETRIEVAL_DOCUMENT/RETRIEVAL_QUERY/etc.)
                taskType = "RETRIEVAL_DOCUMENT"
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var endpoint =
                $"v1/projects/{_projectId}/locations/{_location}/publishers/google/models/{_embeddingModel}:embedContent";

            using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Erro no embedContent do Gemini: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new HttpRequestException($"Gemini Embedding API error: {response.StatusCode} | Content: {responseContent}");
            }

            return ParseEmbeddingResponse(responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter embeddings do Gemini");
            // Fallback seguro: vetor pequeno com base em hash (não ideal, mas evita quebrar o fluxo)
            return HashEmbeddingFallback(text ?? string.Empty, 128);
        }
    }

    /// <summary>
    /// Mescla dois resumos (mesmo fato/evento) em 1–3 frases coesas, no passado.
    /// Usa :generateContent com prompt curto e limites conservadores.
    /// </summary>
    public async Task<string> MergeSummariesAsync(string existingSummary, string incomingSummary, CancellationToken cancellationToken = default)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();

            var prompt = BuildMergePrompt(existingSummary ?? string.Empty, incomingSummary ?? string.Empty);

            var request = new
            {
                contents = new[]
                {
                    new {
                        role = "user",
                        parts = new[] { new { text = prompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = _mergeTemperature,
                    maxOutputTokens = _mergeMaxTokens,
                    topP = 0.95,
                    topK = 40
                }
            };

            var json = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var endpoint =
                $"v1/projects/{_projectId}/locations/{_location}/publishers/google/models/{_model}:generateContent";

            using var response = await _httpClient.PostAsync(endpoint, httpContent, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var merged = ParseGeminiResponse(responseContent);
                return string.IsNullOrWhiteSpace(merged)
                    ? MergeConcatenateFallback(existingSummary, incomingSummary)
                    : merged.Trim();
            }
            else
            {
                _logger.LogError("Erro no MergeSummaries via Gemini: {Code} - {Body}", response.StatusCode, responseContent);
                return MergeConcatenateFallback(existingSummary, incomingSummary);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha no MergeSummariesAsync, usando fallback concat");
            return MergeConcatenateFallback(existingSummary, incomingSummary);
        }
    }

    // ------------------------- Core calls -------------------------

    private async Task<string> GenerateResponseAsync(string prompt, double? temperature = null, int? maxTokens = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();

            var request = new
            {
                contents = new[]
                {
                    new {
                        role = "user",
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = temperature ?? 0.8,
                    maxOutputTokens = maxTokens ?? 1024,
                    topP = 0.95,
                    topK = 40
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var endpoint =
                $"v1/projects/{_projectId}/locations/{_location}/publishers/google/models/{_model}:generateContent";

            using var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return ParseGeminiResponse(responseContent);
            }
            else
            {
                _logger.LogError("Erro na resposta do Gemini: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new HttpRequestException($"Gemini API error: {response.StatusCode} | Content: {responseContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar API do Gemini");
            throw;
        }
    }

    private async Task<string> GetAccessTokenAsync()
    {
        try
        {
            if (!File.Exists(_credentialsPath))
            {
                throw new FileNotFoundException($"Arquivo de credenciais não encontrado: {_credentialsPath}");
            }

            var credential = GoogleCredential.FromFile(_credentialsPath)
                .CreateScoped("https://www.googleapis.com/auth/cloud-platform");

            var token = await credential.UnderlyingCredential.GetAccessTokenForRequestAsync(
                "https://www.googleapis.com/auth/cloud-platform");

            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter token de acesso do Google");
            throw;
        }
    }

    // ------------------------- Parsers & helpers -------------------------

    private string ParseGeminiResponse(string responseContent)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(responseContent);

            // Vertex :generateContent típico
            if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.ValueKind == JsonValueKind.Array &&
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];

                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.ValueKind == JsonValueKind.Array &&
                    parts.GetArrayLength() > 0)
                {
                    var firstPart = parts[0];
                    if (firstPart.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? "";
                    }
                }
            }

            _logger.LogWarning("Formato de resposta inesperado do Gemini (:generateContent): {Response}", responseContent);

            if (jsonDoc.RootElement.TryGetProperty("promptFeedback", out var feedback) &&
                feedback.TryGetProperty("blockReason", out var reason))
            {
                return $"[CONTEÚDO BLOQUEADO PELO GEMINI: {reason.GetString()}]";
            }

            return "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parsear resposta do Gemini");
            return "";
        }
    }

    private float[] ParseEmbeddingResponse(string responseContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            // Padrão 1 (frequente no Vertex): { "embedding": { "values": [ ... ] } }
            if (root.TryGetProperty("embedding", out var embedding) &&
                embedding.TryGetProperty("values", out var values1) &&
                values1.ValueKind == JsonValueKind.Array)
            {
                return ValuesToFloatArray(values1);
            }

            // Padrão 2 (algumas variantes): { "predictions": [ { "embeddings": [ { "values": [ ... ] } ] } ] }
            if (root.TryGetProperty("predictions", out var predictions) &&
                predictions.ValueKind == JsonValueKind.Array &&
                predictions.GetArrayLength() > 0)
            {
                var firstPred = predictions[0];

                if (firstPred.TryGetProperty("embeddings", out var embeddings) &&
                    embeddings.ValueKind == JsonValueKind.Array &&
                    embeddings.GetArrayLength() > 0)
                {
                    var emb0 = embeddings[0];
                    if (emb0.TryGetProperty("values", out var values2) &&
                        values2.ValueKind == JsonValueKind.Array)
                    {
                        return ValuesToFloatArray(values2);
                    }
                }

                // Algumas respostas tem "embedding" direto no prediction
                if (firstPred.TryGetProperty("embedding", out var embSingle) &&
                    embSingle.TryGetProperty("values", out var values3) &&
                    values3.ValueKind == JsonValueKind.Array)
                {
                    return ValuesToFloatArray(values3);
                }
            }

            // fallback: tentar data[0].embedding
            if (root.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Array &&
                data.GetArrayLength() > 0)
            {
                var d0 = data[0];
                if (d0.TryGetProperty("embedding", out var embD) &&
                    embD.ValueKind == JsonValueKind.Array)
                {
                    return ValuesToFloatArray(embD);
                }
            }

            _logger.LogWarning("Não foi possível localizar vetor de embedding no corpo: {Body}", Truncate(responseContent, 512));
            return HashEmbeddingFallback(responseContent, 128);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parsear embedding do Gemini");
            return HashEmbeddingFallback(responseContent, 128);
        }
    }

    private static float[] ValuesToFloatArray(JsonElement array)
    {
        var list = new List<float>(array.GetArrayLength());
        foreach (var v in array.EnumerateArray())
        {
            if (v.ValueKind == JsonValueKind.Number)
            {
                if (v.TryGetDouble(out var d))
                    list.Add((float)d);
            }
            else if (v.ValueKind == JsonValueKind.String)
            {
                if (float.TryParse(v.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f))
                    list.Add(f);
            }
        }
        return list.ToArray();
    }

    private static float[] HashEmbeddingFallback(string text, int dim)
    {
        // Fallback determinístico: NÃO usar em produção como substituto,
        // serve apenas para não quebrar o fluxo em caso de falha temporária.
        var v = new float[dim];
        unchecked
        {
            int h1 = text?.GetHashCode() ?? 0;
            int h2 = (text?.Length ?? 0) * 397;
            var seed = h1 ^ h2;

            var rnd = new Random(seed);
            for (int i = 0; i < dim; i++)
            {
                v[i] = (float)(rnd.NextDouble() * 2.0 - 1.0); // [-1,1]
            }
        }
        return v;
    }

    private static string MergeConcatenateFallback(string existingSummary, string incomingSummary)
    {
        existingSummary = (existingSummary ?? string.Empty).Trim();
        incomingSummary = (incomingSummary ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(existingSummary)) return incomingSummary;
        if (string.IsNullOrEmpty(incomingSummary)) return existingSummary;

        var sb = new StringBuilder();
        sb.Append(existingSummary);
        if (!existingSummary.EndsWith(".")) sb.Append('.');
        sb.Append(' ');
        sb.Append(incomingSummary);
        if (!incomingSummary.EndsWith(".")) sb.Append('.');

        var text = sb.ToString();
        const int maxLen = 600;
        if (text.Length > maxLen)
            text = text[..maxLen].TrimEnd() + "...";
        return text;
    }

    private static string BuildCharacterPrompt(CharacterSheet character, string context)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine($"Você é {character.Name}, um personagem de RPG.");

        if (!string.IsNullOrEmpty(character.DataJson) && character.DataJson != "{}")
        {
            prompt.AppendLine($"Dados do personagem: {character.DataJson}");
        }

        prompt.AppendLine("\nContexto da conversa:");
        prompt.AppendLine(context);

        prompt.AppendLine("\nResponda como este personagem, mantendo sua personalidade e estilo. Seja conciso (máximo 2-3 frases):");

        return prompt.ToString();
    }

    private static string BuildNpcPrompt(string npcName, string npcContext, string conversationContext)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine($"Você é {npcName}, um NPC (personagem não-jogador) em um RPG.");
        prompt.AppendLine($"Contexto do NPC: {npcContext}");

        prompt.AppendLine("\nContexto da conversa:");
        prompt.AppendLine(conversationContext);

        prompt.AppendLine("\nResponda como este NPC, mantendo sua personalidade e papel na história. Seja conciso (máximo 2-3 frases):");

        return prompt.ToString();
    }

    private static string BuildMergePrompt(string existingSummary, string incomingSummary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Mescle as duas entradas em um único resumo coeso, na voz neutra e no passado.");
        sb.AppendLine("Regra: 1 a 3 frases; sem redundâncias; preserve fatos e relações importantes; retorno APENAS o texto final.");
        sb.AppendLine();
        sb.AppendLine("Entrada A:");
        sb.AppendLine(existingSummary?.Trim() ?? string.Empty);
        sb.AppendLine();
        sb.AppendLine("Entrada B:");
        sb.AppendLine(incomingSummary?.Trim() ?? string.Empty);
        sb.AppendLine();
        sb.AppendLine("Resumo mesclado:");
        return sb.ToString();
    }

    private static double TryParseDouble(string? s, double def)
        => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;

    private static int TryParseInt(string? s, int def)
        => int.TryParse(s, out var v) ? v : def;

    private static string Truncate(string s, int len) => (s?.Length ?? 0) <= len ? s : s.Substring(0, len) + "…";
}
