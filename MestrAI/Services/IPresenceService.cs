using System.Collections.Generic;
using System.Threading.Tasks;

namespace RPGSessionManager.Services
{
    public interface IPresenceService
    {
        Task UserJoinedAsync(int sessionId, string userId, string displayName, string connectionId);
        Task<IEnumerable<int>> UserDisconnectedAsync(string userId);
        Task UserLeftAsync(string connectionId);
        Task<List<object>> GetParticipantsAsync(int sessionId, string? requestingUserId = null);
        Task RemoveConnectionAsync(string connectionId);
    }
}
