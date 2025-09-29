using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using RPGSessionManager.Services;
using System.Text.Json;

namespace RPGSessionManager.Pages.Sessions
{
    [Authorize]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPermissionService _permissionService;

        public EditModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IPermissionService permissionService)
        {
            _context = context;
            _userManager = userManager;
            _permissionService = permissionService;
        }

        [BindProperty]
        public Session Session { get; set; } = default!;

        [BindProperty]
        public List<string> SelectedParticipants { get; set; } = new();

        public SelectList SystemOptions { get; set; } = default!;
        public SelectList ScenarioOptions { get; set; } = default!;
        public SelectList TabletopOptions { get; set; } = default!;
        public List<ApplicationUser> AvailableUsers { get; set; } = new();
        public string NarratorName { get; set; } = string.Empty;
        public string? TabletopName { get; set; }
        public int TabletopMemberCount { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var session = await _context.Sessions
                .Include(s => s.Narrator)
                .Include(s => s.System)
                .Include(s => s.Scenario)
                .Include(s => s.GameTabletop)
                .ThenInclude(gt => gt!.Members)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (session == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            // Check if user can edit this session
            if (!await _permissionService.CanUserEditSessionAsync(currentUser.Id, session.Id))
            {
                return Forbid();
            }

            Session = session;
            NarratorName = session.Narrator.DisplayName ?? session.Narrator.Email ?? "Unknown";

            // Load participants
            var participants = JsonSerializer.Deserialize<string[]>(session.Participants) ?? Array.Empty<string>();
            SelectedParticipants = participants.ToList();

            // Load tabletop info if applicable
            if (session.GameTabletop != null)
            {
                TabletopName = session.GameTabletop.Name;
                TabletopMemberCount = session.GameTabletop.Members.Count(m => m.IsActive);
            }

            await LoadSelectListsAsync(currentUser.Id);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    await LoadSelectListsAsync(currentUser.Id);
                }
                return Page();
            }

            var currentUser2 = await _userManager.GetUserAsync(User);
            if (currentUser2 == null)
            {
                return Challenge();
            }

            // Check if user can edit this session
            if (!await _permissionService.CanUserEditSessionAsync(currentUser2.Id, Session.Id))
            {
                return Forbid();
            }

            var sessionToUpdate = await _context.Sessions.FindAsync(Session.Id);
            if (sessionToUpdate == null)
            {
                return NotFound();
            }

            // Update session properties
            sessionToUpdate.Name = Session.Name;
            sessionToUpdate.SystemId = Session.SystemId;
            sessionToUpdate.ScenarioId = Session.ScenarioId;
            sessionToUpdate.GameTabletopId = Session.GameTabletopId;
            sessionToUpdate.UpdatedAt = DateTime.UtcNow;

            // Update participants
            var participantsJson = JsonSerializer.Serialize(SelectedParticipants.ToArray());
            sessionToUpdate.Participants = participantsJson;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SessionExists(Session.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Details", new { id = Session.Id });
        }

        private async Task LoadSelectListsAsync(string currentUserId)
        {
            // Load systems
            var systems = await _context.SystemDefinitions.ToListAsync();
            SystemOptions = new SelectList(systems, "Id", "Name");

            // Load scenarios
            var scenarios = await _context.ScenarioDefinitions.ToListAsync();
            ScenarioOptions = new SelectList(scenarios, "Id", "Name");

            // Load tabletops where user is owner or member
            var tabletops = await _context.GameTabletops
                .Include(gt => gt.Members)
                .Where(gt => !gt.IsDeleted && 
                            (gt.NarratorId == currentUserId || 
                             gt.Members.Any(m => m.UserId == currentUserId && m.IsActive)))
                .ToListAsync();
            TabletopOptions = new SelectList(tabletops, "Id", "Name");

            // Load available users (exclude current participants and narrator)
            var excludeUserIds = SelectedParticipants.ToList();
            excludeUserIds.Add(Session.NarratorId);

            AvailableUsers = await _context.Users
                .Where(u => !excludeUserIds.Contains(u.Id))
                .OrderBy(u => u.DisplayName ?? u.Email)
                .ToListAsync();
        }

        public string GetUserDisplayName(string userId)
        {
            var user = _context.Users.Find(userId);
            return user?.DisplayName ?? user?.Email ?? "Unknown User";
        }

        private bool SessionExists(int id)
        {
            return _context.Sessions.Any(e => e.Id == id);
        }
    }
}

