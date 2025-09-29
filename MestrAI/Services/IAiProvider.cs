using RPGSessionManager.Models;
using System.Threading;

namespace RPGSessionManager.Services;

/// <summary>
/// Interface para provedores de IA
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Gera uma resposta de personagem baseada no contexto
    /// </summary>
    Task<string> GenerateCharacterReplyAsync(CharacterSheet character, string context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gera uma resposta de NPC baseada no contexto
    /// </summary>
    Task<string> GenerateNpcReplyAsync(string npcName, string npcContext, string conversationContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Testa se o provedor está funcionando corretamente
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna um embedding (vetor numérico) para o texto informado,
    /// usado para busca/deduplicação semântica.
    /// O tamanho do vetor pode variar conforme o provedor.
    /// </summary>
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mescla de forma coesa dois resumos relacionados ao mesmo fato/evento,
    /// retornando um novo resumo curto (1-3 frases).
    /// </summary>
    Task<string> MergeSummariesAsync(string existingSummary, string incomingSummary, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nome do provedor
    /// </summary>
    string ProviderName { get; }
}
