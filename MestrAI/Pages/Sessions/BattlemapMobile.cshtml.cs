using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using RPGSessionManager.Services;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RPGSessionManager.Hubs;

namespace RPGSessionManager.Pages.Sessions;

[Authorize]
public class BattlemapMobileModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPermissionService _permissionService;
    private readonly IChatService _chatService;
    private readonly ILogger<BattlemapMobileModel> _logger;
    private readonly IRollService _rollService;

    public BattlemapMobileModel(
        ApplicationDbContext context,
        IRollService rollService,
        UserManager<ApplicationUser> userManager,
        IPermissionService permissionService,
        IChatService chatService,
        ILogger<BattlemapMobileModel> logger)
    {
        _context = context;
        _userManager = userManager;
        _permissionService = permissionService;
        _chatService = chatService;
        _logger = logger;
        _rollService = rollService;
    }

    public int SessionId { get; set; }

    public string CurrentUserId { get; set; } = string.Empty;
    public string SessionName { get; set; } = string.Empty;
    public BattleMap BattleMap { get; set; } = null!;
    public string TokensJson { get; set; } = "[]";
    public string BattleMapJson { get; set; } = "{}";
    public bool CanEditTokens { get; set; }
    public bool IsNarrator { get; set; }
    public List<ChatMessageViewModel> Messages { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();
        CurrentUserId = currentUser.Id;
        // Carregar sessão
        var session = await _context.Sessions
            .Include(s => s.Narrator)
            .Include(s => s.GameTabletop)
                .ThenInclude(gt => gt.Members)
                    .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session == null) return NotFound();

        // Verificar permissões
        var hasAccess = await _permissionService.CanAccessSessionAsync(currentUser.Id, session.Id);
        if (!hasAccess)
        {
            return Forbid("Você não tem permissão para acessar esta sessão.");
        }

        SessionId = session.Id;
        SessionName = session.Name;
        IsNarrator = session.NarratorId == currentUser.Id || User.IsInRole("Admin");
        CanEditTokens = IsNarrator; // Por enquanto, apenas narradores podem editar

        // Carregar ou criar BattleMap
        var battleMap = await _context.BattleMaps
            .Include(bm => bm.Tokens)
                .ThenInclude(t => t.Owner)
            .FirstOrDefaultAsync(bm => bm.SessionId == id);

        if (battleMap == null)
        {
          // Criar BattleMap padrão
          battleMap = new BattleMap
          {
            SessionId = id,
            GridSize = 50,
            ZoomMin = 0.5f,
            ZoomMax = 2.0f,
            GridUnitValue = 1.5f, // <-- Valor padrão para a medida
            GridUnit = "m",       // <-- Unidade padrão (metros)
            UpdatedAt = DateTime.UtcNow
          };

      _context.BattleMaps.Add(battleMap);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new BattleMap for session {SessionId}", id);
        }

        BattleMap = battleMap;
        // Prepare a lightweight JSON representation of the battle map to avoid circular references and send only what the client needs
        BattleMapJson = JsonSerializer.Serialize(new
        {
            id = battleMap.Id,
            sessionId = battleMap.SessionId,
            gridSize = battleMap.GridSize,
            zoomMin = battleMap.ZoomMin,
            zoomMax = battleMap.ZoomMax,
            backgroundUrl = battleMap.BackgroundUrl,
            gridUnitValue = battleMap.GridUnitValue,
            gridUnit = battleMap.GridUnit
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Serializar tokens para JSON
        var tokensData = battleMap.Tokens.Select(t => new
        {
            id = t.Id.ToString(),
            name = t.Name,
            imageUrl = t.ImageUrl,
            ownerId = t.OwnerId,
            ownerName = t.Owner?.DisplayName ?? t.Owner?.UserName ?? "Desconhecido",
            x = t.X,
            y = t.Y,
            scale = t.Scale,
            rotation = t.Rotation,
            isVisible = t.IsVisible,
            z = t.Z,
            updatedAt = t.UpdatedAt,
            canEdit = IsNarrator || t.OwnerId == currentUser.Id
        }).ToList();

        TokensJson = JsonSerializer.Serialize(tokensData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Carregar mensagens recentes do chat
        try
        {
            var messages = await _chatService.GetRecentMessagesAsync(id, 20);
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
            _logger.LogError(ex, "Erro ao carregar mensagens para session {SessionId}", id);
            Messages = new List<ChatMessageViewModel>();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSendMessageAsync(int sessionId, string content, MessageType messageType)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var session = await _context.Sessions.FindAsync(sessionId);
        if (session == null) return NotFound();

        // Check access
        var hasAccess = User.IsInRole("Admin") ||
                       session.NarratorId == currentUser.Id ||
                       session.Participants.Contains(currentUser.Id);

        if (!hasAccess) return Forbid();

        // Validate message type permissions
        if ((messageType == MessageType.NarratorDescription ) &&
            !User.IsInRole("Narrator") && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        try
        {
            // Send message using ChatService
            var chatMessage = await _chatService.SendMessageAsync(
                sessionId,
                content,
                currentUser.DisplayName,
                currentUser.Id,
                messageType);

            // Processar rolagens se for MessageType.DiceRoll ou se o conteúdo começar com '#'
            if (messageType == MessageType.DiceRoll || content.Trim().StartsWith("#"))
            {
                //try
                //{
                //    var rollResult = await _rollService.ProcessRollAsync(content, currentUser.DisplayName, sessionId);
                //    // Construir metadados enriquecidos com resumo e detalhes em PT-BR
                //    var summary = RPGSessionManager.Rolling.RollFormatter.FormatSummaryPtBr(rollResult);
                //    // Concatena detalhes em uma única string ou vazio se não houver
                //    var detailsList = RPGSessionManager.Rolling.RollFormatter.FormatDetailsPtBr(rollResult).ToList();
                //    var details = detailsList.Count > 0 ? string.Join(" | ", detailsList) : string.Empty;
                //    var metaObj = new
                //    {
                //        rollResult.User,
                //        rollResult.Expression,
                //        rollResult.Seed,
                //        rollResult.Blocks,
                //        rollResult.Total,
                //        rollResult.IsError,
                //        rollResult.ErrorCode,
                //        rollResult.ErrorMessage,
                //        Summary = summary,
                //        Details = details
                //    };
                //    chatMessage.Metadata = JsonSerializer.Serialize(metaObj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                //    _context.ChatMessages.Update(chatMessage);
                //    await _context.SaveChangesAsync();
                //}
                //catch (Exception ex)
                //{
                //    _logger.LogError(ex, "Erro ao processar rolagem: {Content} para usuário {UserId} na sessão {SessionId}",
                //        content, currentUser.Id, sessionId);
                //}
                try
                {
                    var rollResult = await _rollService.ProcessRollAsync(content, currentUser.DisplayName, sessionId);

                    // MUDANÇA: Agora serializamos o objeto 'rollResult' original e puro.
                    // Isso garante que todos os campos (incluindo 'Ruleset') sejam mantidos
                    // e nenhum campo extra (como 'summary' e 'details') seja adicionado.
                    chatMessage.Metadata = JsonSerializer.Serialize(rollResult, new JsonSerializerOptions
                    {
                        // Para manter o "PascalCase" (User, Expression), basta remover a política de nomes.
                        // O padrão irá preservar a capitalização original das propriedades.
                        PropertyNamingPolicy = null
                    });

                    _context.ChatMessages.Update(chatMessage);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar rolagem: {Content} para usuário {UserId} na sessão {SessionId}",
                        content, currentUser.Id, sessionId);
                }
            }

            // Implementar nova regra de ativação da IA:
            // A IA deve responder APENAS quando:
            // 1. O tipo da mensagem for explicitamente "PlayerMessage" (equivalente a "Mensagem")
            // 2. E a personagem da IA estiver com status "ativa" na sessão
            // Nota: Tanto Mestres quanto Jogadores podem interagir com a IA usando "Mensagem"

            var shouldTriggerAi = messageType == MessageType.PlayerMessage;

            if (shouldTriggerAi)
            {
                try
                {
                    var userRole = User.IsInRole("Narrator") ? "Mestre" : "Jogador";
                    _logger.LogInformation("IA acionada: {UserRole} postou tipo 'Mensagem' na sessão {SessionId}", userRole, sessionId);
                    await _chatService.TriggerAiResponsesAsync(sessionId, content);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error triggering AI responses for session {SessionId}", sessionId);
                    // Don't fail the request if AI responses fail
                }
            }
            else
            {
                var userRole = User.IsInRole("Narrator") ? "Mestre" : "Jogador";
                _logger.LogInformation("IA ignorou: {UserRole} postou tipo '{MessageType}' na sessão {SessionId}",
                    userRole, messageType, sessionId);
            }

            _logger.LogInformation("User {UserId} sent message to session {SessionId}", currentUser.Id, sessionId);

            // Enviar mensagem via SignalR para todos os clientes conectados
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

            // Retornar JSON para requisições AJAX
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { success = true });
            }

            return RedirectToPage(new { id = sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message from battlemap for session {SessionId}", sessionId);
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { success = false, error = "Erro ao enviar mensagem" });
            }

            return RedirectToPage(new { id = sessionId });
        }
    }
}

public class ChatMessageViewModel
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsAiGenerated { get; set; }
    public string? Metadata { get; set; }
}

// Funções auxiliares para exibição das mensagens
public static class BattlemapMobileHelpers
{
    public static string GetMessageCssClass(MessageType type)
    {
        return type switch
        {
            MessageType.NarratorDescription => "narrator-message",
            MessageType.CharacterAction => "character-action",
            MessageType.SystemMessage => "system-message",
            MessageType.DiceRoll => "dice-roll",
            MessageType.AiCharacterResponse => "ai-message",
            _ => ""
        };
    }

    public static string GetSenderCssClass(MessageType type)
    {
        return type switch
        {
            MessageType.NarratorDescription => "sender-narrator",
            MessageType.CharacterAction => "sender-character",
            MessageType.SystemMessage => "sender-system",
            MessageType.DiceRoll => "sender-dice",
            MessageType.AiCharacterResponse => "sender-ai",
            _ => ""
        };
    }

    public static string GetSenderDisplayName(ChatMessageViewModel message)
    {
        return message.Type switch
        {
            MessageType.NarratorDescription => $"🎭 {message.SenderName}",
            MessageType.CharacterAction => $"⚔️ {message.SenderName}",
            MessageType.SystemMessage => "🤖 Sistema",
            MessageType.DiceRoll => $"🎲 {message.SenderName}",
            MessageType.AiCharacterResponse => $"🤖 {message.SenderName}",
            _ => message.SenderName
        };
    }
}

