using RPGSessionManager.Models;

namespace RPGSessionManager.Services;

public interface ISignalRNotificationService
{
  Task NotifyNpcStatusChangedAsync(int sessionId, int characterId, SessionAiCharacter npcState);
  Task NotifyNpcMessageAsync(int sessionId, ChatMessage message);
  Task UpdateSessionPresenceAsync(int sessionId);
  Task NotifyNpcActivationAsync(int sessionId, int characterId, bool isActive);
  Task NotifyNpcVisibilityAsync(int sessionId, int characterId, bool isVisible);
  Task BroadcastSystemMessageAsync(int sessionId, string message, string messageType = "info");
    Task NotifyNpcInteractionRecorded(int sessionId, int characterId, double responseTimeMs);
}
