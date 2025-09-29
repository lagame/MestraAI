using RPGSessionManager.Models;

namespace RPGSessionManager.Services
{
    public interface IPermissionService
    {

        Task<bool> CanUserEditSessionAsync(string userId, int sessionId);
        Task<bool> CanUserManageCharactersAsync(string userId, int sessionId);
        Task<bool> CanUserAccessTabletopAsync(string userId, int tabletopId);
        Task<bool> CanUserEditTabletopAsync(string userId, int tabletopId);
        Task<bool> CanUserInviteMembersAsync(string userId, int tabletopId);
        Task<bool> IsUserTabletopOwnerAsync(string userId, int tabletopId);
        Task<string> GetUserRoleInTabletopAsync(string userId, int tabletopId);
        Task<string> GetUserRoleInSessionAsync(string userId, int sessionId);
        Task<bool> CanManageSessionAsync(string? userId, int sessionId);
        Task<bool> CanAccessSessionAsync(string? userId, int sessionId);
        Task<bool> IsSessionNarratorAsync(string? userId, int sessionId);
        Task<bool> CanViewSessionAsync(string? userId, int sessionId);
    }
}
