using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;

namespace RPGSessionManager.Pages.Admin.Members
{
    [Authorize(Roles = "Admin")]
    public class EditMemberModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EditMemberModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty] public int TabletopId { get; set; }
        [BindProperty] public int MemberRecordId { get; set; }   // TabletopMember.Id
        [BindProperty] public string DisplayName { get; set; } = string.Empty;
        [BindProperty] public TabletopRole Role { get; set; }
        [BindProperty] public bool IsActive { get; set; }

        public string? UserEmail { get; set; }
        public List<SelectListItem> RoleOptions { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int tabletopId, int memberId)
        {
            TabletopId = tabletopId;
            MemberRecordId = memberId;

            var memberRecord = await _context.TabletopMembers
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == memberId);

            if (memberRecord == null)
            {
                ErrorMessage = "Membro não encontrado.";
                return Page();
            }

            UserEmail = memberRecord.User?.Email ?? memberRecord.User?.UserName ?? "—";
            DisplayName = memberRecord.User?.DisplayName ?? string.Empty;
            Role = memberRecord.Role;
            IsActive = memberRecord.IsActive;

            RoleOptions = Enum.GetValues(typeof(TabletopRole))
                .Cast<TabletopRole>()
                .Select(r => new SelectListItem { Value = ((int)r).ToString(), Text = r.ToString() })
                .ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Carregar registro e usuário
            var memberRecord = await _context.TabletopMembers
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == MemberRecordId);

            if (memberRecord == null)
            {
                ModelState.AddModelError(string.Empty, "Registro de membro não encontrado.");
                return Page();
            }

            // Atualizar DisplayName no ApplicationUser (se informado)
            if (!string.IsNullOrWhiteSpace(DisplayName) && memberRecord.User != null)
            {
                if (memberRecord.User.DisplayName != DisplayName)
                {
                    memberRecord.User.DisplayName = DisplayName;
                    var updateRes = await _userManager.UpdateAsync(memberRecord.User);
                    if (!updateRes.Succeeded)
                    {
                        foreach (var e in updateRes.Errors) ModelState.AddModelError(string.Empty, e.Description);
                        return Page();
                    }
                }
            }

            // Atualizar associação (papel / ativo)
            memberRecord.Role = Role;
            memberRecord.IsActive = IsActive;

            _context.TabletopMembers.Update(memberRecord);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Membro atualizado: {(string.IsNullOrEmpty(DisplayName) ? (memberRecord.User?.UserName ?? "—") : DisplayName)}";
            return RedirectToPage("./Edit", new { id = TabletopId });
        }
    }
}
