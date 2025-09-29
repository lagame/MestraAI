using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace RPGSessionManager.Pages.Sessions;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new(); // inicializa para nunca ser nulo


    public GameTabletop GameTabletop { get; set; } = null!;

    public class InputModel
    {
        [Required(ErrorMessage = "O nome da sessão é obrigatório")]
        [StringLength(200, ErrorMessage = "O nome deve ter no máximo 200 caracteres")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "A descrição deve ter no máximo 1000 caracteres")]
        public string? Description { get; set; }

        public DateTime? ScheduledDate { get; set; }
        [Required]
        public int GameTabletopId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int? mesaId)
    {
        if (!mesaId.HasValue)
        {
            return BadRequest("ID da mesa é obrigatório.");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var gameTabletop = await _context.GameTabletops
            .Include(g => g.Narrator)
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == mesaId.Value);

        if (gameTabletop == null)
        {
            return NotFound("Mesa não encontrada.");
        }

        // Verificar se o usuário tem permissão (narrador ou membro)
        var hasAccess = gameTabletop.NarratorId == userId || 
                       gameTabletop.Members.Any(m => m.UserId == userId);

        if (!hasAccess)
        {
            return Forbid("Você não tem permissão para criar sessões nesta mesa.");
        }

        GameTabletop = gameTabletop;
        Input.GameTabletopId = mesaId.Value;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        // Recarregar GameTabletop para validação
        GameTabletop = await _context.GameTabletops!
            .Include(g => g.Narrator)
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == Input.GameTabletopId);

        if (GameTabletop == null)
        {
            ModelState.AddModelError("", "Mesa não encontrada.");
            return Page();
        }

        // Verificar permissão novamente
        var hasAccess = GameTabletop.NarratorId == userId || 
                       GameTabletop.Members.Any(m => m.UserId == userId);

        if (!hasAccess)
        {
            ModelState.AddModelError("", "Você não tem permissão para criar sessões nesta mesa.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var session = new Session
        {
            Name = Input.Name,
            Description = Input.Description,
            ScheduledDate = Input.ScheduledDate,
            GameTabletopId = Input.GameTabletopId,
            CreatedAt = DateTime.UtcNow,
            Status = SessionStatus.Planned
        };

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Sessão criada com sucesso!";
        return RedirectToPage("/Mesas/Details", new { id = Input.GameTabletopId });
    }
}

