using RPGSessionManager.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace RPGSessionManager.Services;

/// <summary>
/// Provedor de IA usando endpoint HTTP local
/// </summary>
public class LocalProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocalProvider> _logger;
    private readonly string _endpoint;
    private readonly string? _apiKey;

    public string ProviderName => "Local";

    public LocalProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<LocalProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _endpoint = configuration["AiSettings:LocalEndpoint"] ?? "http://localhost:8080";
        _apiKey = configuration["AiSettings:LocalApiKey"];

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_endpoint);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "RPGSessionManager/0.4.5");

        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }
    }

    // ========= IAiProvider =========

    public async Task<string> GenerateCharacterReplyAsync(CharacterSheet character, string context, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildCharacterPrompt(character, context);
            return await GenerateResponseAsync(prompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar resposta do personagem {CharacterName} via Local Provider", character.Name);
            throw;
        }
    }

    public async Task<string> GenerateNpcReplyAsync(string npcName, string npcContext, string conversationContext, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildNpcPrompt(npcName, npcContext, conversationContext);
            return await GenerateResponseAsync(prompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar resposta do NPC {NpcName} via Local Provider", npcName);
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Tenta fazer um ping simples no endpoint
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // Se não tiver endpoint de health, tenta uma chamada simples
            var testPrompt = "Responda apenas 'OK' para testar a conexão.";
            var testResponse = await GenerateResponseAsync(testPrompt, cancellationToken);
            return !string.IsNullOrEmpty(testResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao testar conexão com Local Provider");
            return false;
        }
    }

    /// <summary>
    /// Embeddings (estilo OpenAI). Endpoint esperado: POST /v1/embeddings { input: "texto" }
    /// Formatos aceitos no retorno: data[0].embedding | embedding | vector.
    /// </summary>
    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var req = new
            {
                input = text ?? string.Empty
                // se o seu servidor exigir, inclua "model"
                // model = "local-embedding"
            };

            var json = JsonSerializer.Serialize(req);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _httpClient.PostAsync("/v1/embeddings", content, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Erro no /v1/embeddings: {Code} - {Body}", resp.StatusCode, body);
                return HashEmbeddingFallback(text ?? string.Empty, 128);
            }

            return ParseLocalEmbeddingResponse(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao obter embeddings no Local Provider, usando fallback hash");
            return HashEmbeddingFallback(text ?? string.Empty, 128);
        }
    }

    /// <summary>
    /// Mescla dois resumos em 1-3 frases coesas (usa completions local).
    /// </summary>
    public async Task<string> MergeSummariesAsync(string existingSummary, string incomingSummary, CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildMergePrompt(existingSummary ?? string.Empty, incomingSummary ?? string.Empty);
            // temperatura menor + menos tokens para ficar objetivo
            return await GenerateResponseAsync(prompt, temperature: 0.3, maxTokens: 256, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MergeSummariesAsync falhou no Local Provider, usando fallback concat");
            return MergeConcatenateFallback(existingSummary, incomingSummary);
        }
    }

    // ========= Core HTTP helpers =========

    private async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
        => await GenerateResponseAsync(prompt, temperature: 0.8, maxTokens: 1024, cancellationToken);

    private async Task<string> GenerateResponseAsync(string prompt, double temperature, int maxTokens, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                prompt = prompt,
                max_tokens = maxTokens,
                temperature = temperature,
                top_p = 0.95,
                stop = new[] { "\n\n", "Human:", "Assistant:" }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/v1/completions", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return ParseLocalResponse(responseContent);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Erro na resposta do Local Provider: {StatusCode} - {Content}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Local Provider API error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar API do Local Provider");
            throw;
        }
    }

    private string ParseLocalResponse(string responseContent)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(responseContent);

            if (jsonDoc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];

                if (firstChoice.TryGetProperty("text", out var text))
                {
                    return text.GetString()?.Trim() ?? "";
                }
            }

            // Fallback: se não conseguir parsear, tenta retornar o conteúdo como está
            if (jsonDoc.RootElement.TryGetProperty("response", out var response))
            {
                return response.GetString()?.Trim() ?? "";
            }

            _logger.LogWarning("Formato de resposta inesperado do Local Provider: {Response}", responseContent);
            return "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parsear resposta do Local Provider");
            return "";
        }
    }

    // ========= Embeddings helpers =========

    private float[] ParseLocalEmbeddingResponse(string responseContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            // OpenAI-like: { "data": [ { "embedding": [ ... ] } ] }
            if (root.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Array &&
                data.GetArrayLength() > 0)
            {
                var first = data[0];
                if (first.TryGetProperty("embedding", out var embArr) && embArr.ValueKind == JsonValueKind.Array)
                    return ValuesToFloatArray(embArr);
            }

            // Alternativos: { "embedding": [ ... ] } | { "vector": [ ... ] }
            if (root.TryGetProperty("embedding", out var emb) && emb.ValueKind == JsonValueKind.Array)
                return ValuesToFloatArray(emb);

            if (root.TryGetProperty("vector", out var vec) && vec.ValueKind == JsonValueKind.Array)
                return ValuesToFloatArray(vec);

            _logger.LogWarning("Resposta de embedding desconhecida do Local Provider: {Body}", Truncate(responseContent, 512));
            return HashEmbeddingFallback(responseContent, 128);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parsear embedding do Local Provider");
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
                list.Add((float)v.GetDouble());
            }
            else if (v.ValueKind == JsonValueKind.String)
            {
                if (float.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                    list.Add(f);
            }
        }
        return list.ToArray();
    }

    private static float[] HashEmbeddingFallback(string text, int dim)
    {
        // Fallback determinístico: NÃO é substituto de embeddings reais,
        // serve para não quebrar o fluxo enquanto o endpoint local não existir.
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

    // ========= Merge helpers =========

    private static string BuildMergePrompt(string existingSummary, string incomingSummary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Mescle as duas entradas em um único resumo coeso, na voz neutra e no passado.");
        sb.AppendLine("Regra: 1 a 3 frases; sem redundâncias; preserve fatos e relações importantes; retorne APENAS o texto final.");
        sb.AppendLine();
        sb.AppendLine("Entrada A:");
        sb.AppendLine((existingSummary ?? string.Empty).Trim());
        sb.AppendLine();
        sb.AppendLine("Entrada B:");
        sb.AppendLine((incomingSummary ?? string.Empty).Trim());
        sb.AppendLine();
        sb.AppendLine("Resumo mesclado:");
        return sb.ToString();
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

    private static string Truncate(string s, int len) => (s?.Length ?? 0) <= len ? s : s.Substring(0, len) + "…";

    // ========= Prompts =========

    private string BuildCharacterPrompt(CharacterSheet character, string context)
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

    private string BuildNpcPrompt(string npcName, string npcContext, string conversationContext)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine($"Você é {npcName}, um NPC (personagem não-jogador) em um RPG.");
        prompt.AppendLine($"Contexto do NPC: {npcContext}");

        prompt.AppendLine("\nContexto da conversa:");
        prompt.AppendLine(conversationContext);

        prompt.AppendLine("\nResponda como este NPC, mantendo sua personalidade e papel na história. Seja conciso (máximo 2-3 frases):");

        return prompt.ToString();
    }
}
