using RPGSessionManager.Models;

namespace RPGSessionManager.Services;

public interface IAiResponseQueue
{
    Task QueueAiResponseAsync(AiResponseRequest request);
    Task<int> GetQueueSizeAsync();
    Task<bool> IsHealthyAsync();
}

public class AiResponseRequest
{
    public int SessionId { get; set; }
    public int CharacterId { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public string TriggerMessage { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public SessionAiCharacter NpcState { get; set; } = null!;
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public int Priority { get; set; } = 5; // 1-10, maior = mais prioritário
}

