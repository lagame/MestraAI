using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Hubs;
using RPGSessionManager.Models;
using System.Text;

namespace RPGSessionManager.Services;

public class ChatMessageService : IChatMessageService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatMessageService> _logger;

    public ChatMessageService(
        ApplicationDbContext context,
        IHubContext<ChatHub> hubContext,
        ILogger<ChatMessageService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<ChatMessage> SendMessageAsync(
        int sessionId,
        string content,
        string senderName,
        string? senderUserId = null,
        MessageType messageType = MessageType.PlayerMessage,
        SenderType senderType = SenderType.User,
        int? characterId = null)
    {
        // Gerar ULID único
        var messageId = GenerateMessageId();

        // Criar mensagem
        var message = new ChatMessage
        {
            MessageId = messageId,
            SessionId = sessionId,
            Content = content.Trim(),
            SenderName = senderName,
            SenderUserId = senderUserId,
            Type = messageType,
            SenderType = senderType,
            CharacterId = characterId,
            IsAiGenerated = senderType == SenderType.AI,
            CreatedAt = DateTime.UtcNow
        };

        // Persistir no banco (fonte da verdade)
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Message {MessageId} saved to database for session {SessionId} by {SenderName}", 
            messageId, sessionId, senderName);

        // Broadcast via SignalR APENAS após persistir
        await BroadcastMessageAsync(message);

        return message;
    }

    public async Task<List<ChatMessage>> GetMessagesAfterAsync(int sessionId, string? afterMessageId = null, int limit = 50)
    {
        var query = _context.ChatMessages
            .Include(m => m.SenderUser)
            .Include(m => m.Character)
            .Where(m => m.SessionId == sessionId);

        if (!string.IsNullOrEmpty(afterMessageId))
        {
            query = query.Where(m => string.Compare(m.MessageId, afterMessageId) > 0);
        }

        return await query
            .OrderBy(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<ChatMessage>> GetRecentMessagesAsync(int sessionId, int count = 50)
    {
        return await _context.ChatMessages
            .Include(m => m.SenderUser)
            .Include(m => m.Character)
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(count)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<ChatMessage> AddSystemMessageAsync(int sessionId, string content)
    {
        return await SendMessageAsync(
            sessionId,
            content,
            "Sistema",
            senderUserId: null,
            MessageType.SystemMessage,
            SenderType.System);
    }

    public async Task<bool> MessageExistsAsync(string messageId)
    {
        return await _context.ChatMessages.AnyAsync(m => m.MessageId == messageId);
    }

    public string GenerateMessageId()
    {
        // Implementação simples de ULID usando timestamp + random
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var random = new Random();
        var randomBytes = new byte[10];
        random.NextBytes(randomBytes);
        
        // Converter para base32 (simplificado)
        var ulid = $"{timestamp:X13}{Convert.ToHexString(randomBytes)}";
        return ulid.PadRight(26, '0')[..26]; // Garantir 26 caracteres
    }

    private async Task BroadcastMessageAsync(ChatMessage message)
    {
        var groupName = $"Session_{message.SessionId}";
        
        var messageData = new
        {
            MessageId = message.MessageId,
            SessionId = message.SessionId,
            Content = message.Content,
            SenderName = message.SenderName,
            SenderType = message.SenderType.ToString(),
            MessageType = message.Type.ToString(),
            IsAiGenerated = message.IsAiGenerated,
            CreatedAt = message.CreatedAt,
            CharacterId = message.CharacterId
        };

        await _hubContext.Clients.Group(groupName).SendAsync("ReceiveMessage", messageData);
        
        _logger.LogDebug("Broadcasted message {MessageId} to group {GroupName}", message.MessageId, groupName);
    }
}

