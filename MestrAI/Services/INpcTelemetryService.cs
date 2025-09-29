using RPGSessionManager.Dtos;

namespace RPGSessionManager.Services;

public interface INpcTelemetryService
{
    void TrackNpcActivation(int sessionId, int characterId, bool isActive);
    void TrackNpcVisibilityChange(int sessionId, int characterId, bool isVisible);
    void TrackAiResponseTime(int sessionId, int characterId, TimeSpan elapsed);
    void TrackAiResponseGenerated(int sessionId, int characterId);
    void TrackCacheHit(int sessionId);
    void TrackCacheMiss(int sessionId);
    Task<NpcMetricsDto> GetSessionMetricsAsync(int sessionId);
    Task<SystemMetricsDto> GetSystemMetricsAsync();
    Task RecordInteractionAsync(int sessionId, int characterId, double responseTimeMs);
    Task<NpcMetricsDto> GetNpcMetricsAsync(int sessionId, int characterId);  

}

