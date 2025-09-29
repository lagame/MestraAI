using RPGSessionManager.Models;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace RPGSessionManager.Services;

/// <summary>
/// Cliente para comunicação com o Context Service via HTTP
/// </summary>
public class ContextServiceClient : IContextService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ContextServiceClient> _logger;
    private readonly string? _apiKey;
    private readonly JsonSerializerOptions _jsonOptions;

    public ContextServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<ContextServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration.GetValue<string>("ContextService:ApiKey");
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Configurar headers padrão
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Context Service não está disponível");
            return false;
        }
    }

    public async Task<string?> AddConversationAsync(
        int gameTabletopId,
        int? sessionId,
        string speakerName,
        string speakerType,
        string content,
        string? context = null,
        int importance = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                sessionId = $"gametabletop_{gameTabletopId}",
                content = content,
                metadata = new
                {
                    gameTabletopId = gameTabletopId,
                    sessionId = sessionId,
                    speakerName = speakerName,
                    speakerType = speakerType,
                    context = context,
                    importance = importance,
                    timestamp = DateTime.UtcNow
                }
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/documents", httpContent, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent, _jsonOptions);
                
                if (result.TryGetProperty("documentId", out var documentIdElement))
                {
                    return documentIdElement.GetString();
                }
            }
            else
            {
                _logger.LogWarning("Falha ao adicionar conversa ao Context Service. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar conversa ao Context Service");
        }

        return null;
    }

    public async Task<List<ConversationMemory>> SearchConversationsAsync(
        int gameTabletopId,
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                gameTabletopId = gameTabletopId,
                query = query,
                topK = limit,
                minScore = 0.2f // Score mínimo para considerar relevante
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/search", httpContent, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent, _jsonOptions);
                
                // Verifica se é uma resposta de fallback do LLM
                if (result.TryGetProperty("source", out var sourceElement) && 
                    sourceElement.GetString() == "llm-fallback")
                {
                    if (result.TryGetProperty("llmResponse", out var llmResponseElement))
                    {
                        var llmResponse = llmResponseElement.GetString();
                        if (!string.IsNullOrEmpty(llmResponse))
                        {
                            // Cria uma ConversationMemory especial para resposta do LLM
                            var llmConversation = new ConversationMemory
                            {                                
                                GameTabletopId = gameTabletopId,
                                SpeakerName = "NPC (IA)",
                                SpeakerType = "NPC",
                                Content = llmResponse,
                                Context = "Resposta gerada por IA local",
                                Importance = 8,
                                Timestamp = DateTime.UtcNow,
                                Score = 1.0f // Score máximo para resposta do LLM
                            };
                            
                            return new List<ConversationMemory> { llmConversation };
                        }
                    }
                }
                
                // Processamento normal para resultados do Qdrant
                if (result.TryGetProperty("results", out var resultsElement))
                {
                    var conversations = new List<ConversationMemory>();
                    
                    foreach (var item in resultsElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("metadata", out var metadataElement))
                        {
                            var conversation = ParseConversationFromMetadata(metadataElement, item);
                            if (conversation != null)
                            {
                                conversations.Add(conversation);
                            }
                        }
                    }
                    
                    return conversations;
                }
            }
            else
            {
                _logger.LogWarning("Falha ao buscar conversas no Context Service. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar conversas no Context Service");
        }

        return new List<ConversationMemory>();
    }

    public async Task<List<ConversationMemory>> SearchConversationsWithNpcContextAsync(
        int gameTabletopId,
        string query,
        string npcName,
        string npcRole,
        string npcAppearance,
        string npcPersonality,
        string npcMannerisms,
        string npcDialogues,
        string npcImprovExamples,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var npcContext = new
            {
                name = npcName,
                role = npcRole,
                appearance = npcAppearance,
                personalityTraits = npcPersonality,
                mannerisms = npcMannerisms,
                dialogueExcerpts = npcDialogues,
                improvExamples = npcImprovExamples
            };

            var request = new
            {
                gameTabletopId = gameTabletopId,
                query = query,
                topK = limit,
                minScore = 0.2f,
                npcContext = npcContext
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/search", httpContent, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent, _jsonOptions);
                
                // Verifica se é uma resposta de fallback do LLM
                if (result.TryGetProperty("source", out var sourceElement) && 
                    sourceElement.GetString() == "llm-fallback")
                {
                    if (result.TryGetProperty("llmResponse", out var llmResponseElement))
                    {
                        var llmResponse = llmResponseElement.GetString();
                        if (!string.IsNullOrEmpty(llmResponse))
                        {
                            // Cria uma ConversationMemory especial para resposta do LLM
                            var llmConversation = new ConversationMemory
                            {
                                GameTabletopId = gameTabletopId,
                                SpeakerName = npcName,
                                SpeakerType = "NPC",
                                Content = llmResponse,
                                Context = "Resposta gerada por IA local",
                                Importance = 8,
                                Timestamp = DateTime.UtcNow,
                                Score = 1.0f // Score máximo para resposta do LLM
                            };
                            
                            return new List<ConversationMemory> { llmConversation };
                        }
                    }
                }
                
                // Processamento normal para resultados do Qdrant
                if (result.TryGetProperty("results", out var resultsElement))
                {
                    var conversations = new List<ConversationMemory>();
                    
                    foreach (var item in resultsElement.EnumerateArray())
                    {
                        if (item.TryGetProperty("metadata", out var metadataElement))
                        {
                            var conversation = ParseConversationFromMetadata(metadataElement, item);
                            if (conversation != null)
                            {
                                conversations.Add(conversation);
                            }
                        }
                    }
                    
                    return conversations;
                }
            }
            else
            {
                _logger.LogWarning("Falha ao buscar conversas no Context Service. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar conversas no Context Service com contexto de NPC");
        }

        return new List<ConversationMemory>();
    }
    public async Task<bool> ClearGameTabletopMemoryAsync(int gameTabletopId, CancellationToken cancellationToken = default)
    {
        try
        {
            var sessionId = $"gametabletop_{gameTabletopId}";
            var response = await _httpClient.DeleteAsync($"/api/session/{sessionId}", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Memória da Mesa {GameTabletopId} limpa com sucesso", gameTabletopId);
                return true;
            }
            else
            {
                _logger.LogWarning("Falha ao limpar memória da Mesa {GameTabletopId}. Status: {StatusCode}", 
                    gameTabletopId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar memória da Mesa {GameTabletopId}", gameTabletopId);
        }

        return false;
    }

    public async Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { text = text };
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("/api/embeddings", httpContent, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent, _jsonOptions);
                
                if (result.TryGetProperty("embedding", out var embeddingElement))
                {
                    var embedding = new List<float>();
                    foreach (var value in embeddingElement.EnumerateArray())
                    {
                        embedding.Add(value.GetSingle());
                    }
                    return embedding.ToArray();
                }
            }
            else
            {
                _logger.LogWarning("Falha ao gerar embedding. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar embedding");
        }

        return null;
    }

    public async Task<MemoryStats?> GetMemoryStatsAsync(int gameTabletopId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Busca todas as conversas da Mesa para calcular estatísticas
            var conversations = await SearchConversationsAsync(gameTabletopId, "", 1000, cancellationToken);
            
            if (conversations.Any())
            {
                var stats = new MemoryStats
                {
                    TotalConversations = conversations.Count,
                    ActiveConversations = conversations.Count(c => c.IsActive),
                    OldestConversation = conversations.Min(c => c.CreatedAt),
                    NewestConversation = conversations.Max(c => c.CreatedAt),
                    AverageImportance = conversations.Average(c => c.Importance),
                    SpeakerTypeCounts = conversations
                        .GroupBy(c => c.SpeakerType)
                        .ToDictionary(g => g.Key, g => g.Count())
                };
                
                return stats;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter estatísticas de memória da Mesa {GameTabletopId}", gameTabletopId);
        }

        return null;
    }

    private ConversationMemory? ParseConversationFromMetadata(JsonElement metadata, JsonElement item)
    {
        try
        {
            var conversation = new ConversationMemory();
            
            if (metadata.TryGetProperty("gameTabletopId", out var gameTabletopIdElement))
                conversation.GameTabletopId = gameTabletopIdElement.GetInt32();
            
            if (metadata.TryGetProperty("sessionId", out var sessionIdElement) && sessionIdElement.ValueKind != JsonValueKind.Null)
                conversation.SessionId = sessionIdElement.GetInt32();
            
            if (metadata.TryGetProperty("speakerName", out var speakerNameElement))
                conversation.SpeakerName = speakerNameElement.GetString() ?? "";
            
            if (metadata.TryGetProperty("speakerType", out var speakerTypeElement))
                conversation.SpeakerType = speakerTypeElement.GetString() ?? "";
            
            if (item.TryGetProperty("content", out var contentElement))
                conversation.Content = contentElement.GetString() ?? "";
            
            if (metadata.TryGetProperty("context", out var contextElement) && contextElement.ValueKind != JsonValueKind.Null)
                conversation.Context = contextElement.GetString();
            
            if (metadata.TryGetProperty("importance", out var importanceElement))
                conversation.Importance = importanceElement.GetInt32();
            
            if (metadata.TryGetProperty("timestamp", out var timestampElement))
                conversation.CreatedAt = timestampElement.GetDateTime();
            
            // Gerar hash do conteúdo
            conversation.ContentHash = GenerateContentHash(conversation.Content);
            conversation.IsActive = true;
            
            return conversation;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao parsear conversa dos metadados");
            return null;
        }
    }

    private string GenerateContentHash(string content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

