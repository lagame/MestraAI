using RPGSessionManager.Models;

namespace RPGSessionManager.Services;

public interface IChatMessageService
{
    /// <summary>
    /// Envia uma nova mensagem e faz broadcast via SignalR
    /// </summary>
    Task<ChatMessage> SendMessageAsync(
        int sessionId,
        string content,
        string senderName,
        string? senderUserId = null,
        MessageType messageType = MessageType.PlayerMessage,
        SenderType senderType = SenderType.User,
        int? characterId = null);

    /// <summary>
    /// Busca mensagens após um MessageId específico (para replay)
    /// </summary>
    Task<List<ChatMessage>> GetMessagesAfterAsync(int sessionId, string? afterMessageId = null, int limit = 50);

    /// <summary>
    /// Busca mensagens recentes de uma sessão
    /// </summary>
    Task<List<ChatMessage>> GetRecentMessagesAsync(int sessionId, int count = 50);

    /// <summary>
    /// Adiciona mensagem do sistema
    /// </summary>
    Task<ChatMessage> AddSystemMessageAsync(int sessionId, string content);

    /// <summary>
    /// Verifica se uma mensagem já existe (deduplicação)
    /// </summary>
    Task<bool> MessageExistsAsync(string messageId);

    /// <summary>
    /// Gera um novo ULID para mensagem
    /// </summary>
    string GenerateMessageId();
}

