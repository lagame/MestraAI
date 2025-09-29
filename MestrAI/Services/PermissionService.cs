using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;

namespace RPGSessionManager.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PermissionService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        
        {
            _context = context;
            _userManager = userManager;
        }


        public async Task<bool> CanUserEditSessionAsync(string userId, int sessionId)
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null) return false;

            // Only narrator can edit session
            return session.NarratorId == userId;
        }

        public async Task<bool> CanUserManageCharactersAsync(string userId, int sessionId)
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null) return false;

            // Narrator can always manage characters
            if (session.NarratorId == userId) return true;

            // Players can manage their own characters
            return await CanAccessSessionAsync(userId, sessionId);
        }

        public async Task<bool> CanUserAccessTabletopAsync(string userId, int tabletopId)
        {
            var tabletop = await _context.GameTabletops
                .Include(gt => gt.Members)
                .FirstOrDefaultAsync(gt => gt.Id == tabletopId && !gt.IsDeleted);

            if (tabletop == null) return false;

            // Narrator can always access
            if (tabletop.NarratorId == userId) return true;

            // Check if user is an active member
            return tabletop.Members.Any(m => m.UserId == userId && m.IsActive);
        }

        public async Task<bool> CanUserEditTabletopAsync(string userId, int tabletopId)
        {
            var tabletop = await _context.GameTabletops.FindAsync(tabletopId);
            if (tabletop == null || tabletop.IsDeleted) return false;

            // Only narrator can edit tabletop
            return tabletop.NarratorId == userId;
        }

        public async Task<bool> CanUserInviteMembersAsync(string userId, int tabletopId)
        {
            var tabletop = await _context.GameTabletops
                .Include(gt => gt.Members)
                .FirstOrDefaultAsync(gt => gt.Id == tabletopId && !gt.IsDeleted);

            if (tabletop == null) return false;

            // Narrator can always invite
            if (tabletop.NarratorId == userId) return true;

            // Admins can invite
            var member = tabletop.Members.FirstOrDefault(m => m.UserId == userId && m.IsActive);
            return member?.Role == TabletopRole.Admin;
        }

        public async Task<bool> IsUserTabletopOwnerAsync(string userId, int tabletopId)
        {
            var tabletop = await _context.GameTabletops.FindAsync(tabletopId);
            return tabletop != null && !tabletop.IsDeleted && tabletop.NarratorId == userId;
        }


        public async Task<string> GetUserRoleInTabletopAsync(string userId, int tabletopId)
        {
            var tabletop = await _context.GameTabletops
                .Include(gt => gt.Members)
                .FirstOrDefaultAsync(gt => gt.Id == tabletopId && !gt.IsDeleted);

            if (tabletop == null) return "None";

            // Check if narrator
            if (tabletop.NarratorId == userId) return "Narrator";

            // Check member role
            var member = tabletop.Members.FirstOrDefault(m => m.UserId == userId && m.IsActive);
            return member?.Role.ToString() ?? "None";
        }

        public async Task<string> GetUserRoleInSessionAsync(string userId, int sessionId)
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null) return "None";

            // Check if narrator
            if (await IsSessionNarratorAsync(userId, sessionId)) return "Narrator";

            // Check if participant
            if (await CanAccessSessionAsync(userId, sessionId)) return "Player";

            return "None";
        }

        public async Task<bool> CanManageSessionAsync(string? userId, int sessionId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null)
                return false;

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isNarrator = session.NarratorId == userId;

            return isAdmin || isNarrator;
        }

        public async Task<bool> CanAccessSessionAsync(string? userId, int sessionId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null)
                return false;

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isNarrator = session.NarratorId == userId;
            
            var isParticipant = false;
            if (!string.IsNullOrEmpty(session.Participants))
            {
                var participants = System.Text.Json.JsonSerializer.Deserialize<List<string>>(session.Participants);
                isParticipant = participants?.Contains(userId) == true;
            }

            return isAdmin || isNarrator || isParticipant;
        }

        public async Task<bool> IsSessionNarratorAsync(string? userId, int sessionId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            var session = await _context.Sessions.FindAsync(sessionId);
            return session?.NarratorId == userId;
        }

        public async Task<bool> CanViewSessionAsync(string? userId, int sessionId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            var session = await _context.Sessions
                .Include(s => s.GameTabletop)
                .ThenInclude(gt => gt!.Members)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null) return false;

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isNarrator = session.NarratorId == userId;

            var isParticipant = false;
            if (!string.IsNullOrEmpty(session.Participants))
            {
                var participants = System.Text.Json.JsonSerializer.Deserialize<List<string>>(session.Participants);
                isParticipant = participants?.Contains(userId) == true;
            }

            var isTabletopMember = false;
            if (session.GameTabletopId > 0)
            {
                isTabletopMember = session.GameTabletop.Members.Any(m => m.UserId == userId && m.IsActive);
            }

            return isAdmin || isNarrator || isParticipant || isTabletopMember;
        }
    }
}

