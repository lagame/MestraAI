using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using System.Security.Claims;

namespace RPGSessionManager.Pages.Sessions;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DetailsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public Session Session { get; set; } = null!;
    public List<string> ParticipantNames { get; set; } = new();
    public List<CharacterSummaryViewModel> Characters { get; set; } = new();
    public string? ScenarioDescription { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var session = await _context.Sessions
            .Include(s => s.GameTabletop)
                .ThenInclude(g => g.Narrator)
            .Include(s => s.GameTabletop)
                .ThenInclude(g => g.Members)
                    .ThenInclude(m => m.User)
            .Include(s => s.Narrator)
            .Include(s => s.System)
            .Include(s => s.Scenario)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session == null)
        {
            return NotFound("Sessão não encontrada.");
        }

        // Verificar se o usuário tem acesso à sessão
        var hasAccess = session.GameTabletop.NarratorId == userId || 
                       session.GameTabletop.Members.Any(m => m.UserId == userId);

        if (!hasAccess)
        {
            return Forbid("Você não tem permissão para acessar esta sessão.");
        }

        Session = session;

        // Populate participant names
        var participantIds = System.Text.Json.JsonSerializer
            .Deserialize<string[]>(session.Participants ?? "[]")
            ?? Array.Empty<string>();

        var participants = await _context.Users
            .Where(u => participantIds.Contains(u.Id))
            .Select(u => u.DisplayName ?? u.UserName ?? u.Email ?? "Usuário")
            .ToListAsync();
        ParticipantNames = participants;

        // Initialize empty characters list for now
        Characters = new List<CharacterSummaryViewModel>();

        return Page();
    }
}

public class CharacterSummaryViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public bool AiEnabled { get; set; }
    public DateTime UpdatedAt { get; set; }
}

