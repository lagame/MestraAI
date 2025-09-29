using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;

namespace RPGSessionManager.Pages.Admin.Members
{
    [Authorize(Roles = "Admin")]
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
            GameTabletops = await _context.GameTabletops
                .Include(g => g.Narrator)
                .Include(g => g.Members)
                .Where(g => !g.IsDeleted)
                .OrderByDescending(g => g.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}