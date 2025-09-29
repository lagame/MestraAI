using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using System.Security.Claims;

namespace RPGSessionManager.Pages.Mesas;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public IndexModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public IList<GameTabletop> GameTabletops { get; set; } = new List<GameTabletop>();

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            GameTabletops = new List<GameTabletop>();
            return;
        }

        // base query com includes necessários
        var query = _context.GameTabletops
            .Include(g => g.Narrator)
            .Include(g => g.Members).ThenInclude(m => m.User)
            .Include(g => g.Sessions)
            .AsQueryable();

        // admins veem tudo (opcional)
        if (User.IsInRole("Admin"))
        {
            GameTabletops = await query
                .OrderByDescending(g => g.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
            return;
        }

        // pega mesas onde o usuário é narrador OU membro
        GameTabletops = await query
            .Where(g => g.NarratorId == userId
                        || g.Members.Any(m => m.UserId == userId))
            .OrderByDescending(g => g.CreatedAt)
            .AsNoTracking() // leitura -> melhora perf.
            .ToListAsync();
    }

}

