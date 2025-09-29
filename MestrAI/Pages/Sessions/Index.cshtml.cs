using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using System.Text.Json;

namespace RPGSessionManager.Pages.Sessions;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public IList<SessionViewModel> Sessions { get; set; } = new List<SessionViewModel>();

    public async Task OnGetAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return;

        var query = _context.Sessions
            .Include(s => s.Narrator)
            .Include(s => s.System)
            .Include(s => s.Scenario)
            .AsQueryable();

        if (User.IsInRole("Admin"))
        {
            // Admin can see all sessions
        }
        else if (User.IsInRole("Narrator"))
        {
            // Narrator can see sessions they created or participate in
            query = query.Where(s => s.NarratorId == currentUser.Id || 
                                   s.Participants.Contains(currentUser.Id));
        }
        else
        {
            // Regular users can only see sessions they participate in
            query = query.Where(s => s.Participants.Contains(currentUser.Id));
        }

        var sessions = await query.OrderByDescending(s => s.UpdatedAt).ToListAsync();

        Sessions = sessions.Select(s => new SessionViewModel
        {
            Id = s.Id,
            Name = s.Name,
            SystemName = s.System?.Name,
            ScenarioName = s.Scenario?.Name,
            NarratorName = s.Narrator.DisplayName,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();
    }
}

public class SessionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? SystemName { get; set; }
    public string? ScenarioName { get; set; }
    public string NarratorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

