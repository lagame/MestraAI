using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using System.ComponentModel.DataAnnotations;

namespace RPGSessionManager.Pages.Admin.Members
{
    [Authorize(Roles = "Admin")]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EditModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public GameTabletop? GameTabletop { get; set; }

        public IList<TabletopMember> ActiveMembers { get; set; } = new List<TabletopMember>();

        // Removed NewMemberEmail property because member creation is handled on a dedicated page

        public async Task<IActionResult> OnGetAsync(int id)
        {
            GameTabletop = await _context.GameTabletops
                .Include(g => g.Narrator)
                .Include(g => g.Members).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted);

            if (GameTabletop == null)
            {
                return NotFound();
            }

            ActiveMembers = GameTabletop.Members.Where(m => m.IsActive).ToList();
            return Page();
        }

        // Removed OnPostAddMemberAsync: member creation is handled via the Create page

        public async Task<IActionResult> OnPostRemoveMemberAsync(int MemberId, int id)
        {
            var member = await _context.TabletopMembers
                .Include(m => m.GameTabletop)
                .FirstOrDefaultAsync(m => m.Id == MemberId && m.IsActive);

            if (member == null)
            {
                return NotFound();
            }

            // Do not allow removing narrator via this method
            if (member.UserId == member.GameTabletop.NarratorId)
            {
                return BadRequest();
            }

            member.IsActive = false;
            member.LeftAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToPage(new { id = id });
        }
    }
}