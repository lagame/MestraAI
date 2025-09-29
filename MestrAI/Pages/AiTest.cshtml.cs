using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using RPGSessionManager.Services;
using System.ComponentModel.DataAnnotations;

namespace RPGSessionManager.Pages;

[Authorize(Roles = "Narrator,Admin")]
public class AiTestModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AiOrchestrator _aiOrchestrator;
    private readonly IChatService _chatService;

    public AiTestModel(
        ApplicationDbContext context, 
        UserManager<ApplicationUser> userManager,
        AiOrchestrator aiOrchestrator, IChatService chatService)
    {
        _context = context;
        _userManager = userManager;
        _aiOrchestrator = aiOrchestrator;
        _chatService = chatService;
    }

    [BindProperty]
    [Required]
    public int SessionId { get; set; }

    [BindProperty]
    [Required]
    public int CharacterId { get; set; }

    [BindProperty]
    [Required]
    public string PlayerCommand { get; set; } = string.Empty;

    public string? AiResponse { get; set; }
    public string? ErrorMessage { get; set; }

    public SelectList SessionOptions { get; set; } = new SelectList(new List<object>(), "Value", "Text");
    public SelectList CharacterOptions { get; set; } = new SelectList(new List<object>(), "Value", "Text");

    public async Task OnGetAsync()
    {
        await LoadOptionsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadOptionsAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            // 1. (NOVO) Busca o histórico da conversa para dar contexto à IA.
            var conversationHistory = await _chatService.GetMessagesSinceLastAiTurnAsync(
                SessionId,
                CharacterId,
                fallbackMessageCount: 15,
                maxWindowCount: 60);

            // 2. Passa a lista `conversationHistory` em vez da string `PlayerCommand`.
            AiResponse = await _aiOrchestrator.GenerateCharacterReplyAsync(
                SessionId,
                CharacterId,
                conversationHistory); // CORRIGIDO!
        }        
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        return Page();
    }

    private async Task LoadOptionsAsync()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return;

        // Load sessions
        var sessionsQuery = _context.Sessions.AsQueryable();
        
        if (!User.IsInRole("Admin"))
        {
            sessionsQuery = sessionsQuery.Where(s => s.NarratorId == currentUser.Id || 
                                                   s.Participants.Contains(currentUser.Id));
        }

        var sessions = await sessionsQuery
            .Select(s => new { s.Id, s.Name })
            .ToListAsync();

        SessionOptions = new SelectList(sessions, "Id", "Name");

        // Load AI-enabled characters
        var characters = await _context.CharacterSheets
            .Where(cs => cs.AiEnabled)
            .Include(cs => cs.Session)
            .Where(cs => User.IsInRole("Admin") || 
                        cs.Session.NarratorId == currentUser.Id || 
                        cs.Session.Participants.Contains(currentUser.Id))
            .Select(cs => new { cs.Id, Name = $"{cs.Name} (Sessão: {cs.Session.Name})" })
            .ToListAsync();

        CharacterOptions = new SelectList(characters, "Id", "Name");
    }
}

