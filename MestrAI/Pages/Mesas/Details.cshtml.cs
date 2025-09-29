using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using System.Security.Claims;

namespace RPGSessionManager.Pages.Mesas;

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

    public GameTabletop GameTabletop { get; set; } = null!;
    public IList<Session> Sessions { get; set; } = new List<Session>();

    public bool CanManageMembers { get; set; } = false;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Challenge();

        var gameTabletop = await _context.GameTabletops
            .Include(g => g.Narrator)
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .Include(g => g.Sessions)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (gameTabletop == null) return NotFound("Mesa não encontrada.");

        var hasAccess = gameTabletop.NarratorId == userId ||
                        gameTabletop.Members.Any(m => m.UserId == userId);

        if (!hasAccess) return Forbid("Você não tem permissão para acessar esta mesa.");

        GameTabletop = gameTabletop;
        Sessions = gameTabletop.Sessions
            .OrderByDescending(s => s.ScheduledDate ?? s.CreatedAt)
            .ToList();

        // <-- define aqui se o usuário pode gerenciar membros (narrador ou admin)
        CanManageMembers = gameTabletop.NarratorId == userId || User.IsInRole("Admin");

        return Page();
    }

    public async Task<IActionResult> OnPostAddPlayerAsync(int GameTabletopId, string PlayerEmail)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var gameTabletop = await _context.GameTabletops
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == GameTabletopId);

        if (gameTabletop == null)
        {
            TempData["Error"] = "Mesa não encontrada.";
            return RedirectToPage(new { id = GameTabletopId });
        }

        // Verificar se o usuário é o narrador
        if (gameTabletop.NarratorId != userId)
        {
            TempData["Error"] = "Apenas o narrador pode adicionar jogadores.";
            return RedirectToPage(new { id = GameTabletopId });
        }

        // Buscar usuário pelo email
        var playerUser = await _userManager.FindByEmailAsync(PlayerEmail);
        if (playerUser == null)
        {
            TempData["Error"] = "Usuário não encontrado com este email.";
            return RedirectToPage(new { id = GameTabletopId });
        }

        // Verificar se já é membro
        if (gameTabletop.Members.Any(m => m.UserId == playerUser.Id))
        {
            TempData["Error"] = "Este usuário já é membro da mesa.";
            return RedirectToPage(new { id = GameTabletopId });
        }

        // Verificar se não é o próprio narrador
        if (playerUser.Id == gameTabletop.NarratorId)
        {
            TempData["Error"] = "O narrador já faz parte da mesa.";
            return RedirectToPage(new { id = GameTabletopId });
        }

        // Adicionar como membro
        var member = new TabletopMember
        {
            GameTabletopId = GameTabletopId,
            UserId = playerUser.Id,
            Role = TabletopRole.Player,
            JoinedAt = DateTime.UtcNow
        };

        _context.TabletopMembers.Add(member);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Jogador {playerUser.UserName} adicionado com sucesso!";
        return RedirectToPage(new { id = GameTabletopId });
    }
}

