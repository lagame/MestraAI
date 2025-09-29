using RPGSessionManager.Models;

namespace RPGSessionManager.Services;

/// <summary>
/// Interface para comunicação com o Context Service (microserviço de memória)
/// </summary>
public interface IContextService
{
    /// <summary>
    /// Verifica se o Context Service está disponível
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adiciona uma conversa à memória de uma Mesa
    /// </summary>
    /// <param name="gameTabletopId">ID da Mesa</param>
    /// <param name="sessionId">ID da Sessão (opcional)</param>
    /// <param name="speakerName">Nome do falante</param>
    /// <param name="speakerType">Tipo do falante (Player, NPC, Narrator, System)</param>
    /// <param name="content">Conteúdo da mensagem</param>
    /// <param name="context">Contexto adicional (opcional)</param>
    /// <param name="importance">Importância da memória (1-10)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>ID do documento criado ou null se falhou</returns>
    Task<string?> AddConversationAsync(
        int gameTabletopId,
        int? sessionId,
        string speakerName,
        string speakerType,
        string content,
        string? context = null,
        int importance = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca conversas relevantes na memória de uma Mesa
    /// </summary>
    /// <param name="gameTabletopId">ID da Mesa</param>
    /// <param name="query">Texto de busca</param>
    /// <param name="limit">Número máximo de resultados</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de conversas relevantes</returns>
    Task<List<ConversationMemory>> SearchConversationsAsync(
        int gameTabletopId,
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca conversas relevantes com contexto de NPC para fallback de IA
    /// </summary>
    /// <param name="gameTabletopId">ID da Mesa</param>
    /// <param name="query">Texto de busca</param>
    /// <param name="npcName">Nome do NPC</param>
    /// <param name="npcRole">Papel do NPC</param>
    /// <param name="npcAppearance">Aparência do NPC</param>
    /// <param name="npcPersonality">Traços de personalidade</param>
    /// <param name="npcMannerisms">Maneirismos</param>
    /// <param name="npcDialogues">Trechos de diálogos</param>
    /// <param name="npcImprovExamples">Exemplos de improviso</param>
    /// <param name="limit">Número máximo de resultados</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de conversas relevantes ou resposta de IA</returns>
    Task<List<ConversationMemory>> SearchConversationsWithNpcContextAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Limpa toda a memória de uma Mesa
    /// </summary>
    /// <param name="gameTabletopId">ID da Mesa</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se limpou com sucesso</returns>
    Task<bool> ClearGameTabletopMemoryAsync(
        int gameTabletopId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera embedding para um texto
    /// </summary>
    /// <param name="text">Texto para gerar embedding</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Embedding como array de floats ou null se falhou</returns>
    Task<float[]?> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém estatísticas da memória de uma Mesa
    /// </summary>
    /// <param name="gameTabletopId">ID da Mesa</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas da memória</returns>
    Task<MemoryStats?> GetMemoryStatsAsync(
        int gameTabletopId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Estatísticas de memória de uma Mesa
/// </summary>
public class MemoryStats
{
    public int TotalConversations { get; set; }
    public int ActiveConversations { get; set; }
    public DateTime? OldestConversation { get; set; }
    public DateTime? NewestConversation { get; set; }
    public Dictionary<string, int> SpeakerTypeCounts { get; set; } = new();
    public double AverageImportance { get; set; }
}

