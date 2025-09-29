using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using RPGSessionManager.Services;

namespace RPGSessionManager.Hubs;

public class ChatHub : Hub
{
    private readonly IPresenceService _presenceService;
    private readonly IChatService _chatService;
    private readonly ILogger<ChatHub> _logger;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _context;
    public ChatHub(ApplicationDbContext context, IPresenceService presenceService, IChatService chatService, ILogger<ChatHub> logger,
                 UserManager<ApplicationUser> users)
      {
          _presenceService = presenceService;
          _chatService = chatService;
          _logger = logger;
          _users = users;
        _context = context;
    }
    private static string Group(int sessionId) => $"Session_{sessionId}";
    public async Task JoinSession(int sessionId)
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("UserIdentifier is null or empty for connection {ConnectionId}", Context.ConnectionId);
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(sessionId));
        //await Groups.AddToGroupAsync(Context.ConnectionId, sessionId.ToString());

        var displayName =
        Context.User?.FindFirst("displayName")?.Value ??
        Context.User?.FindFirst(ClaimTypes.Name)?.Value ??
        (await _users.FindByIdAsync(userId))?.DisplayName ??
        (await _users.FindByIdAsync(userId))?.UserName ??
        userId;
    // Adiciona o parâmetro displayName (pode ser igual ao userId se não houver outro disponível)
    await _presenceService.UserJoinedAsync(sessionId, userId, displayName, Context.ConnectionId);
        _logger.LogInformation("User {UserId} joined session {SessionId} with connection {ConnectionId}", userId, sessionId, Context.ConnectionId);

        // Notificar todos na sessão sobre a atualização de presença
        var participants = await _presenceService.GetParticipantsAsync(sessionId);
        await Clients.Group(sessionId.ToString()).SendAsync("PresenceUpdated", participants);
    }
    public async Task LeaveSession(int sessionId)
    {
      await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(sessionId));
    }
    // Em ChatService.cs
    //public async Task<List<ChatMessage>> GetMessagesSinceLastAiTurnAsync(int sessionId, int characterId, int fallbackMessageCount = 15)
    //{
    //    // 1. Encontrar a data da última mensagem enviada por este personagem de IA.
    //    var lastAiMessage = await _context.ChatMessages
    //        .Where(m => m.SessionId == sessionId && m.CharacterId == characterId && m.IsAiGenerated)
    //        .OrderByDescending(m => m.CreatedAt)
    //        .FirstOrDefaultAsync();

    //    // 2. Buscar as mensagens a partir dessa data.
    //    IQueryable<ChatMessage> query = _context.ChatMessages
    //        .Include(m => m.SenderUser) // Incluir dados do remetente
    //        .Where(m => m.SessionId == sessionId);

    //    if (lastAiMessage != null)
    //    {
    //        // Se a IA já falou, pegue tudo o que aconteceu DEPOIS da última fala dela.
    //        query = query.Where(m => m.CreatedAt > lastAiMessage.CreatedAt);
    //    }
    //    else
    //    {
    //        // Se a IA nunca falou, use um fallback para dar um contexto inicial.
    //        // Pega as últimas 'fallbackMessageCount' mensagens.
    //        query = query.OrderByDescending(m => m.CreatedAt).Take(fallbackMessageCount);
    //    }

    //    // 3. Ordenar o resultado final cronologicamente.
    //    var messages = await query.OrderBy(m => m.CreatedAt).ToListAsync();

    //    // Se a IA nunca falou e usamos o fallback, a lista virá em ordem descendente.
    //    // Precisamos garantir que está ascendente no final.
    //    if (lastAiMessage == null)
    //    {
    //        return messages.OrderBy(m => m.CreatedAt).ToList();
    //    }

    //    return messages;
    //}

    public override async Task OnDisconnectedAsync(Exception? ex)
  {
    await _presenceService.RemoveConnectionAsync(Context.ConnectionId);
    // se quiser, também remova do grupo preservado, se você armazenar (opcional)
    await base.OnDisconnectedAsync(ex);
  }

  //public override async Task OnDisconnectedAsync(Exception? exception)
  //  {
  //      var userId = Context.UserIdentifier;
  //      if (string.IsNullOrEmpty(userId))
  //      {
  //          _logger.LogWarning("UserIdentifier is null or empty for disconnected connection {ConnectionId}", Context.ConnectionId);
  //          await base.OnDisconnectedAsync(exception);
  //          return;
  //      }

  //      // Corrigido: Remover a conexão e notificar as sessões afetadas manualmente
  //      await _presenceService.RemoveConnectionAsync(Context.ConnectionId);

  //      // Se necessário, obtenha as sessões afetadas de outra forma (por exemplo, armazenando-as no PresenceService)
  //      // Aqui, como não temos UserDisconnectedAsync, não temos como saber as sessões afetadas diretamente.
  //      // Se você precisa notificar as sessões, pode ser necessário obter os IDs das sessões do usuário de outra forma.

  //      _logger.LogInformation("User {UserId} disconnected with connection {ConnectionId}.", userId, Context.ConnectionId);
  //      await base.OnDisconnectedAsync(exception);
  //  }

  public async Task SendMessage(int sessionId, string messageContent, string senderName, int? characterId = null)
    {
        try
        {
            var userId = Context.UserIdentifier;
            var chatMessage = await _chatService.SendMessageAsync(sessionId, messageContent, senderName, userId, characterId: characterId);
            
            // O SendMessageAsync já publica via SSE, mas o SignalR pode ser usado para notificação imediata
            // await Clients.Group(sessionId.ToString()).SendAsync("ReceiveMessage", chatMessage);

            // Disparar respostas de IA se for uma mensagem de jogador
            if (chatMessage.Type == Models.MessageType.PlayerMessage)
            {
                await _chatService.TriggerAiResponsesAsync(sessionId, chatMessage.Content);
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Failed to send message: {Message}", ex.Message);
            await Clients.Caller.SendAsync("ReceiveSystemMessage", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to session {SessionId}", sessionId);
            await Clients.Caller.SendAsync("ReceiveSystemMessage", "Erro ao enviar mensagem.");
        }
    }

    // Removido GetOnlineForSessionAsync e qualquer estado estático
}

