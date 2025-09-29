using RPGSessionManager.Models;

namespace RPGSessionManager.Services;

public interface INpcStateCache
{
    Task<SessionAiCharacter?> GetNpcStateAsync(int sessionId, int characterId);
    Task SetNpcStateAsync(SessionAiCharacter state);
    Task InvalidateSessionCacheAsync(int sessionId);
    Task InvalidateNpcCacheAsync(int sessionId, int characterId);
    Task<List<SessionAiCharacter>> GetSessionActiveNpcsAsync(int sessionId);
    void TrackCacheHit(string operation);
    void TrackCacheMiss(string operation);
}

