using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using System.ComponentModel.DataAnnotations;

namespace RPGSessionManager.Pages.Admin.Members
{
    [Authorize(Roles = "Admin")]
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
        [Required]
        public int GameTabletopId { get; set; }

        [BindProperty]
        [EmailAddress(ErrorMessage = "Email inválido.")]
        [Required(ErrorMessage = "Informe o email do usuário.")]

        public string Email { get; set; } = string.Empty;

        [BindProperty]
        [Required(ErrorMessage = "Informe o nome de exibição.")]
        [Display(Name = "Nome de exibição")]
        public string DisplayName { get; set; } = string.Empty;


        [BindProperty]
        public TabletopRole Role { get; set; } = TabletopRole.Player;

        public GameTabletop? GameTabletop { get; set; }

        // nova lista de mesas para preencher o select
        public List<SelectListItem> TabletopList { get; set; } = new List<SelectListItem>();

        public List<SelectListItem> RoleOptions { get; set; } = new List<SelectListItem>();

        public async Task<IActionResult> OnGetAsync(int id = 0)
        {
            // carregar todas as mesas (admins globais veem tudo)
            var tables = await _context.GameTabletops
                .Where(g => !g.IsDeleted)
                .Include(g => g.Narrator)
                .OrderBy(g => g.Name)
                .AsNoTracking()
                .ToListAsync();

            TabletopList = tables
                .Select(t => new SelectListItem(t.Name, t.Id.ToString()))
                .ToList();

            // preencher RoleOptions (mantendo seu código)
            RoleOptions = Enum.GetValues(typeof(TabletopRole))
                .Cast<TabletopRole>()
                .Select(r => new SelectListItem { Value = ((int)r).ToString(), Text = r.ToString() })
                .ToList();

            if (id > 0)
            {
                GameTabletopId = id;
                GameTabletop = tables.FirstOrDefault(g => g.Id == id);
                if (GameTabletop == null)
                {
                    return NotFound();
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // recarregar listas caso haja validação
            var tables = await _context.GameTabletops
                .Where(g => !g.IsDeleted)
                .Include(g => g.Narrator)
                .OrderBy(g => g.Name)
                .AsNoTracking()
                .ToListAsync();

            TabletopList = tables.Select(t => new SelectListItem(t.Name, t.Id.ToString())).ToList();

            RoleOptions = Enum.GetValues(typeof(TabletopRole))
                .Cast<TabletopRole>()
                .Select(r => new SelectListItem { Value = ((int)r).ToString(), Text = r.ToString() })
                .ToList();

            if (!ModelState.IsValid)
            {
                GameTabletop = tables.FirstOrDefault(g => g.Id == GameTabletopId);
                return Page();
            }

            GameTabletop = await _context.GameTabletops
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == GameTabletopId && !g.IsDeleted);

            if (GameTabletop == null)
            {
                ModelState.AddModelError(string.Empty, "Mesa não encontrada.");
                return Page();
            }

            var emailTrimmed = Email?.Trim() ?? string.Empty;
            var user = await _userManager.FindByEmailAsync(emailTrimmed);

            var newlyCreatedUser = false;

            if (user == null)
            {
                // Criar conta mínima (ApplicationUser) para o e-mail informado
                user = new ApplicationUser
                {
                    UserName = emailTrimmed,     // geralmente ok; se policy exigir, ajuste
                    Email = emailTrimmed,
                    EmailConfirmed = false       // recomendamos false: enviar link de reset/confirm
                                                 // se tiver DisplayName: DisplayName = ...
                };

                // gerar senha temporária forte
                var tempPassword = GenerateRandomPassword();

                var createResult = await _userManager.CreateAsync(user, tempPassword);
                if (!createResult.Succeeded)
                {
                    // adiciona erros ao ModelState para exibir ao admin
                    foreach (var err in createResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, err.Description);
                    }
                    return Page();
                }

                newlyCreatedUser = true;

                // gerar token de reset para o usuário definir senha (link)
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var tokenEncoded = System.Net.WebUtility.UrlEncode(resetToken);

                // montar link para ResetPassword (ajuste a rota /page conforme seu projeto)
                var resetLink = Url.Page("/Account/ResetPassword", null,
                    new { userId = user.Id, token = tokenEncoded }, Request.Scheme);

                // NÃO dependemos de um serviço de e-mail aqui para compilar.
                // Colocamos o link em TempData para o admin ver / repassar ou para logs.
                TempData["Info"] = $"Conta criada para {emailTrimmed}. Link para definir senha: {resetLink}";
                // depois de gerar resetLink:
                TempData["InfoMessage"] = $"Conta criada para {emailTrimmed}.";
                TempData["InfoLink"] = resetLink; // url segura (já gerada com Url.Page)

                // Opcional: se quiser marcar email confirmado automaticamente:
                // await _userManager.ConfirmEmailAsync(user, await _userManager.GenerateEmailConfirmationTokenAsync(user));
            }

            // agora temos user (novo ou existente)

            // não adicionar o narrador
            if (user.Id == GameTabletop.NarratorId)
            {
                ModelState.AddModelError(string.Empty, "O narrador já faz parte da mesa.");
                return Page();
            }

            // checar duplicidade (safety)
            if (GameTabletop.Members.Any(m => m.UserId == user.Id && m.IsActive))
            {
                ModelState.AddModelError(string.Empty, "Usuário já é membro desta mesa.");
                return Page();
            }

            // adicionar TabletopMember
            var member = new TabletopMember
            {
                GameTabletopId = GameTabletopId,
                UserId = user.Id,
                Role = Role,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.TabletopMembers.Add(member);
            await _context.SaveChangesAsync();

            TempData["Success"] = newlyCreatedUser
                ? $"Conta criada e Membro adicionado à mesa '{GameTabletop.Name}'. Verifique o link de definição de senha no Info."
                : $"Membro {user.UserName} adicionado à mesa '{GameTabletop.Name}' com sucesso.";

            return RedirectToPage("./Edit", new { id = GameTabletopId });

            // Local function: gera senha forte
            static string GenerateRandomPassword(int length = 24)
            {
                const string valid = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@$?_-";
                var res = new char[length];
                using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                {
                    var buffer = new byte[length];
                    rng.GetBytes(buffer);
                    for (int i = 0; i < length; i++)
                    {
                        var idx = buffer[i] % valid.Length;
                        res[i] = valid[idx];
                    }
                }
                return new string(res);
            }
        }

    }
}
