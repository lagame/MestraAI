using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using RPGSessionManager.Services;
using Microsoft.AspNetCore.SignalR;
using RPGSessionManager.Hubs;

namespace RPGSessionManager.Pages.Sessions;

[Authorize]
public class ChatModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IChatService _chatService;
    private readonly AiOrchestrator _aiOrchestrator;
    private readonly IRollService _rollService;
    private readonly ILogger<ChatModel> _logger;

    public ChatModel(
        ApplicationDbContext context, 
        UserManager<ApplicationUser> userManager,
        IChatService chatService,
        AiOrchestrator aiOrchestrator,
        IRollService rollService,
        ILogger<ChatModel> logger)
    {
        _context = context;
        _userManager = userManager;
        _chatService = chatService;
        _aiOrchestrator = aiOrchestrator;
        _rollService = rollService;
        _logger = logger;
    }

    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public List<ChatMessageViewModel> Messages { get; set; } = new();
    public List<CharacterSummaryViewModel> Characters { get; set; } = new();
    public List<string> ParticipantNames { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Carregar sessão com narrador, opcionalmente gameTabletop e membros
        var session = await _context.Sessions
            .Include(s => s.Narrator)
            .Include(s => s.GameTabletop)
                .ThenInclude(gt => gt.Members) // se existir relacionamento GameTabletop.Members
                                               // .Include(s => s.Participants) // se Participants for uma entidade navegacional fortemente tipada
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session == null) return NotFound();

        var isAdmin = User.IsInRole("Admin");
        var isNarrator = session.NarratorId == currentUser.Id;
        var isParticipant = false;

        // Defensive: tratar vários formatos possíveis de "Participants"
        try
        {
            object? participantsObj = session.Participants as object;

            if (participantsObj != null)
            {
                // 1) Se for IEnumerable<string>
                if (participantsObj is IEnumerable<string> ids)
                {
                    isParticipant = ids.Contains(currentUser.Id);
                }
                // 2) Se for IEnumerable<object> (coleção de entidades com UserId)
                else if (participantsObj is IEnumerable<object> objs)
                {
                    foreach (var p in objs)
                    {
                        if (p == null) continue;
                        // tenta propriedade UserId
                        var userIdProp = p.GetType().GetProperty("UserId");
                        if (userIdProp != null)
                        {
                            var val = userIdProp.GetValue(p)?.ToString();
                            if (val == currentUser.Id) { isParticipant = true; break; }
                        }

                        // tenta propriedade Id
                        var idProp = p.GetType().GetProperty("Id");
                        if (idProp != null)
                        {
                            var val = idProp.GetValue(p)?.ToString();
                            if (val == currentUser.Id) { isParticipant = true; break; }
                        }

                        // fallback para string implícita (ToString)
                        var asString = p.ToString();
                        if (!string.IsNullOrEmpty(asString) && asString == currentUser.Id)
                        {
                            isParticipant = true;
                            break;
                        }
                    }
                }
                // 3) Se for string (possível CSV ou único id)
                else if (participantsObj is string csv)
                {
                    // tolera separadores comuns
                    var parts = csv.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(s => s.Trim());
                    if (parts.Contains(currentUser.Id))
                    {
                        isParticipant = true;
                    }
                    else
                    {
                        // talvez o campo seja um único id igual a currentUser.Id
                        if (csv.Trim() == currentUser.Id) isParticipant = true;
                    }
                }
                // 4) caso genérico: tenta um único objeto com propriedade UserId direta em session.Participants
                else
                {
                    var prop = participantsObj.GetType().GetProperty("UserId");
                    if (prop != null)
                    {
                        var val = prop.GetValue(participantsObj)?.ToString();
                        if (val == currentUser.Id) isParticipant = true;
                    }
                }
            }
        }
        catch
        {
            // Não falhar a rota por causa do formato inesperado; garantimos checagem via GameTabletop abaixo.
            isParticipant = false;
        }

        // Se ainda não for participante, verificar membros da GameTabletop (se houver)
        if (!isParticipant && session.GameTabletop != null && session.GameTabletop.Members != null)
        {
            try
            {
                isParticipant = session.GameTabletop.Members.Any(m => m.UserId == currentUser.Id);
            }
            catch
            {
                isParticipant = false;
            }
        }

        var hasAccess = isAdmin || isNarrator || isParticipant;
        if (!hasAccess) return Forbid();

        SessionId = id;
        SessionName = session.Name ?? string.Empty;

        // Carregar mensagens com segurança
        try
        {
            var messages = await _chatService.GetRecentMessagesAsync(id);
            Messages = messages.Select(m => new ChatMessageViewModel
            {
                Id = m.Id,
                Content = m.Content,
                SenderName = m.SenderName,
                Type = m.Type,
                CreatedAt = m.CreatedAt,
                IsAiGenerated = m.IsAiGenerated,
                Metadata = m.Metadata
            }).ToList();
        }
        catch (Exception ex)
        {
            // logue e siga com lista vazia
            _logger.LogError(ex, "Erro ao carregar mensagens para session {SessionId}", id);
            Messages = new List<ChatMessageViewModel>();
        }

        // Carregar personagens
        try
        {
            var characters = await _chatService.GetSessionCharactersAsync(id);
            Characters = characters.Select(c => new CharacterSummaryViewModel
            {
                Id = c.Id,
                Name = c.Name,
                OwnerName = c.Player?.DisplayName ?? c.Player?.UserName ?? "Sistema",
                AiEnabled = c.AiEnabled,
                UpdatedAt = c.UpdatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar personagens para session {SessionId}", id);
            Characters = new List<CharacterSummaryViewModel>();
        }

        // Carregar participantes (nomes) para inicial fallback no client
        try
        {
            ParticipantNames = await _chatService.GetSessionParticipantsAsync(id) ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar participantes para session {SessionId}", id);
            ParticipantNames = new List<string>();
        }

        return Page();
    }

    [HttpPost]
    public async Task<IActionResult> OnPostSendMessageAsync(int sessionId, string content, MessageType messageType)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var session = await _context.Sessions.FindAsync(sessionId);
        if (session == null) return NotFound();

        // Acesso
        var hasAccess = User.IsInRole("Admin") ||
                        session.NarratorId == currentUser.Id ||
                        session.Participants.Contains(currentUser.Id);

        if (!hasAccess) return Forbid();

        // Permissões por tipo
        if ((messageType == MessageType.NarratorDescription || messageType == MessageType.DiceRoll) &&
            !User.IsInRole("Narrator") && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        // Validação básica de conteúdo
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest("Conteúdo da mensagem não pode ser vazio.");

        try
        {
            // Enviar mensagem (ChatService faz: validação, rate limit, duplicate check,
            // normalização de rolagem (# -> DiceRoll), persistência e memória de conversa)
            var chatMessage = await _chatService.SendMessageAsync(
                sessionId,
                content,
                currentUser.DisplayName,
                currentUser.Id,
                messageType);

            // Se o ChatService classificou como rolagem, processa o resultado e salva metadados
            if (chatMessage.Type == MessageType.DiceRoll)
            {
                try
                {
                    var rollResult = await _rollService.ProcessRollAsync(
                        chatMessage.Content,
                        currentUser.DisplayName,
                        sessionId);

                    if (!rollResult.IsError)
                    {
                        chatMessage.Metadata = System.Text.Json.JsonSerializer.Serialize(rollResult);
                        _context.ChatMessages.Update(chatMessage);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Erro ao processar rolagem: {Content} para usuário {UserId} na sessão {SessionId}",
                        chatMessage.Content, currentUser.Id, sessionId);
                }
            }

            // Regra de disparo da IA:
            // Só dispara se o tipo FINAL da mensagem for PlayerMessage
            // (se for rolagem, o ChatService já terá mudado para DiceRoll e não dispara)
            if (chatMessage.Type == MessageType.PlayerMessage)
            {
                try
                {
                    var userRole = User.IsInRole("Narrator") ? "Mestre" : "Jogador";
                    _logger.LogInformation("IA acionada: {UserRole} postou tipo 'Mensagem' na sessão {SessionId}",
                        userRole, sessionId);

                    await _chatService.TriggerAiResponsesAsync(sessionId, chatMessage.Content);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error triggering AI responses for session {SessionId}", sessionId);
                    // Não falha a requisição por causa da IA
                }
            }
            else
            {
                var userRole = User.IsInRole("Narrator") ? "Mestre" : "Jogador";
                _logger.LogInformation("IA ignorou: {UserRole} postou tipo '{MessageType}' na sessão {SessionId}",
                    userRole, chatMessage.Type, sessionId);
            }

            _logger.LogInformation("User {UserId} sent message to session {SessionId}", currentUser.Id, sessionId);

            // Broadcast via SignalR (além do SSE interno do ChatService)
            var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<ChatHub>>();
            var messageData = new
            {
                id = chatMessage.Id,
                sessionId = chatMessage.SessionId,
                content = chatMessage.Content,
                senderName = chatMessage.SenderName,
                type = chatMessage.Type.ToString(),
                createdAt = chatMessage.CreatedAt,
                isAiGenerated = chatMessage.IsAiGenerated,
                metadata = chatMessage.Metadata
            };

            await hubContext.Clients.Group($"Session_{sessionId}").SendAsync("ReceiveMessage", messageData);

            // Resposta para AJAX
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return new JsonResult(new { success = true });

            return RedirectToPage(new { id = sessionId });
        }
        catch (ArgumentException ex)
        {
            // Erros de conteúdo (ex.: vazio)
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return new JsonResult(new { success = false, error = ex.Message });

            ModelState.AddModelError(string.Empty, ex.Message);
            return RedirectToPage(new { id = sessionId });
        }
        catch (InvalidOperationException ex)
        {
            // Possível rate-limit / mensagem duplicada
            _logger.LogWarning(ex, "Falha de regra de envio na sessão {SessionId}", sessionId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return new JsonResult(new { success = false, error = ex.Message });

            TempData["Error"] = ex.Message;
            return RedirectToPage(new { id = sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message for session {SessionId}", sessionId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return new JsonResult(new { success = false, error = "Erro ao enviar mensagem" });

            return RedirectToPage(new { id = sessionId });
        }
    }


    public async Task<IActionResult> OnPostTriggerAiResponseAsync(int sessionId, int characterId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Only narrators and admins can trigger AI responses
        if (!User.IsInRole("Narrator") && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var session = await _context.Sessions.FindAsync(sessionId);
        if (session == null) return NotFound();

        var character = await _context.CharacterSheets
            .Include(c => c.Player)
            .FirstOrDefaultAsync(c => c.Id == characterId && c.SessionId == sessionId);

        if (character == null || !character.AiEnabled) return NotFound();

        try
        {
            // Get recent messages for context
            //var recentMessages = await _chatService.GetRecentMessagesAsync(sessionId, 5);
            //var lastMessage = recentMessages.LastOrDefault()?.Content ?? "Interação manual solicitada";

            // NOVO: Busca o histórico de mensagens desde a última fala da IA.
            var conversationHistory = await _chatService.GetMessagesSinceLastAiTurnAsync(sessionId, characterId, 25, 80);

            // Generate AI response using AiOrchestrator directly for manual trigger
            var aiResponse = await _aiOrchestrator.GenerateCharacterReplyAsync(
                sessionId,
                characterId,
                conversationHistory
            );

            if (!string.IsNullOrEmpty(aiResponse))
            {
                await _chatService.SendMessageAsync(
                    sessionId,
                    aiResponse,
                    character.Name,
                    messageType: MessageType.AiCharacterResponse,
                    characterId: characterId);

                _logger.LogInformation("Manual AI response generated for character {CharacterId} in session {SessionId}", characterId, sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI response for character {CharacterId}", characterId);
            
            // Add error message to chat using ChatService
            await _chatService.AddSystemMessageAsync(sessionId, "Erro ao gerar resposta da IA. Tente novamente.");
        }

        return RedirectToPage(new { id = sessionId });
    }
}



