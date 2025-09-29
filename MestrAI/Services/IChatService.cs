// Em Services/IChatService.cs
using RPGSessionManager.Models;

namespace RPGSessionManager.Services;

public interface IChatService
{
    Task<ChatMessage> SendMessageAsync(
      int sessionId,
      string content,
      string senderName,
      string? senderUserId = null,
      MessageType messageType = MessageType.PlayerMessage,
      int? characterId = null);

    Task SendAiGeneratedMessageAsync(int sessionId, int characterId, string characterName, string content);
    Task<List<ChatMessage>> GetRecentMessagesAsync(int sessionId, int count = 50);
    Task<bool> TriggerAiResponsesAsync(int sessionId, string triggerMessage);
    Task<List<CharacterSheet>> GetSessionCharactersAsync(int sessionId);
    Task<List<string>> GetSessionParticipantsAsync(int sessionId);
    Task<List<ChatMessage>> GetMessagesSinceLastAiTurnAsync(
    int sessionId,
    int characterId,
    int fallbackMessageCount = 15,
    int maxWindowCount = 60);
    Task AddSystemMessageAsync(int sessionId, string content);    
    ChatServiceStats GetStats();
}
