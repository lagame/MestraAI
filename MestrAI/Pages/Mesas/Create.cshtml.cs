using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using System.Security.Claims;

namespace RPGSessionManager.Pages.Mesas;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public CreateModel(ApplicationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public GameTabletop GameTabletop { get; set; } = new();

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        GameTabletop.NarratorId = userId;
        GameTabletop.CreatedAt = DateTime.UtcNow;
        GameTabletop.UpdatedAt = DateTime.UtcNow;

        _context.GameTabletops.Add(GameTabletop);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Mesa criada com sucesso!";
        return RedirectToPage("./Details", new { id = GameTabletop.Id });
    }
}

