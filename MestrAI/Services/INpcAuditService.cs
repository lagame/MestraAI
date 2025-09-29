using RPGSessionManager.Dtos;
using RPGSessionManager.Models;

namespace RPGSessionManager.Services;

public interface INpcAuditService
{
    Task LogStateChangeAsync(SessionAiCharacter currentState, SetNpcStatusRequest request, string userId, string changeSource = "Manual");
    Task<List<AuditLogDto>> GetSessionAuditLogAsync(int sessionId, int pageSize = 50, int pageNumber = 1);
    Task<List<AuditLogDto>> GetCharacterAuditLogAsync(int characterId, int pageSize = 50, int pageNumber = 1);
    Task<bool> HasRecentChangesAsync(int sessionId, int characterId, TimeSpan timeWindow);
}

