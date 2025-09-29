using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using RPGSessionManager.Services;
using System.Text.Json;
using Microsoft.Net.Http.Headers;

namespace RPGSessionManager.Controllers;

[ApiController]
[Route("sessions/{sessionId:int}/chat")]
[Authorize]
public class ChatStreamController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IChatStreamManager _streamManager;
    private readonly ILogger<ChatStreamController> _logger;

    public ChatStreamController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IChatStreamManager streamManager,
        ILogger<ChatStreamController> logger)
    {
        _context = context;
        _userManager = userManager;
        _streamManager = streamManager;
        _logger = logger;
    }

    [HttpGet("stream")]
    public async Task<IActionResult> GetChatStream(int sessionId, [FromQuery] string? lastEventId = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) 
        {
            _logger.LogWarning("Unauthorized SSE connection attempt for session {SessionId}", sessionId);
            return Unauthorized();
        }

        var session = await _context.Sessions.FindAsync(sessionId);
        if (session == null) 
        {
            _logger.LogWarning("SSE connection attempt for non-existent session {SessionId}", sessionId);
            return NotFound();
        }

        // Check if user has access to this session
        var hasAccess = User.IsInRole("Admin") ||
                       session.NarratorId == currentUser.Id ||
                       session.Participants.Contains(currentUser.Id);

        if (!hasAccess) 
        {
            _logger.LogWarning("User {UserId} attempted to access unauthorized session {SessionId}", 
                currentUser.Id, sessionId);
            return Forbid();
        }

        _logger.LogInformation("Starting SSE connection for user {UserId} in session {SessionId}, lastEventId: {LastEventId}", 
            currentUser.Id, sessionId, lastEventId ?? "none");

        // Tipo correto para SSE
        Response.ContentType = "text/event-stream; charset=utf-8";

        // Propriedades tipadas → não usa strings mágicas e não lança por chave duplicada
        var h = Response.Headers;
        h.CacheControl = "no-cache";
        h.Connection = "keep-alive";

        // Não há propriedades tipadas para todos (CORS, X-Accel-Buffering), use indexador
        h["X-Accel-Buffering"] = "no";   // evita buffering no Nginx
        h["Access-Control-Allow-Origin"] = "*";
        h["Access-Control-Allow-Headers"] = "Cache-Control";

        try
        {
            var missedMessageCount = 0;
            
            // Send missed messages first if lastEventId is provided
            if (!string.IsNullOrEmpty(lastEventId) && int.TryParse(lastEventId, out var lastMessageId))
            {
                _logger.LogDebug("Retrieving missed messages after ID {LastMessageId} for session {SessionId}", 
                    lastMessageId, sessionId);

                var missedMessages = await _context.ChatMessages
                    .Where(m => m.SessionId == sessionId && m.Id > lastMessageId)
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => new ChatMessageEventData
                    {
                        Id = m.Id,
                        SessionId = m.SessionId,
                        AuthorName = m.SenderName,
                        Content = m.Content,
                        MessageType = m.Type.ToString(),
                        CreatedAt = m.CreatedAt,
                        IsAiGenerated = m.IsAiGenerated
                    })
                    .ToListAsync();

                missedMessageCount = missedMessages.Count;
                
                if (missedMessageCount > 0)
                {
                    _logger.LogInformation("Sending {Count} missed messages to user {UserId} in session {SessionId}", 
                        missedMessageCount, currentUser.Id, sessionId);
                }

                foreach (var message in missedMessages)
                {
                    var eventData = FormatSseEvent(message);
                    await Response.WriteAsync(eventData);
                    await Response.Body.FlushAsync();
                    
                    // Check for cancellation between messages
                    if (HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        _logger.LogDebug("SSE connection cancelled while sending missed messages for session {SessionId}", sessionId);
                        return new EmptyResult();
                    }
                }
            }
            else
            {
                // Send initial connection confirmation
                var confirmationEvent = $"id: 0\nevent: connected\ndata: {{\"type\":\"connected\",\"sessionId\":{sessionId},\"timestamp\":\"{DateTime.UtcNow:O}\"}}\n\n";
                await Response.WriteAsync(confirmationEvent);
                await Response.Body.FlushAsync();
            }

            var eventStream = await _streamManager.SubscribeToSessionAsync(sessionId, lastEventId);
            
            _logger.LogDebug("SSE stream established for user {UserId} in session {SessionId}, missed messages: {MissedCount}", 
                currentUser.Id, sessionId, missedMessageCount);
            
            await foreach (var eventData in eventStream)
            {
                await Response.WriteAsync(eventData);
                await Response.Body.FlushAsync();
                
                if (HttpContext.RequestAborted.IsCancellationRequested)
                {
                    _logger.LogDebug("SSE connection cancelled for user {UserId} in session {SessionId}", 
                        currentUser.Id, sessionId);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("SSE connection cancelled for user {UserId} in session {SessionId}", 
                currentUser.Id, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SSE stream for user {UserId} in session {SessionId}", 
                currentUser.Id, sessionId);
        }
        finally
        {
            _logger.LogDebug("SSE connection closed for user {UserId} in session {SessionId}", 
                currentUser.Id, sessionId);
        }

        return new EmptyResult();
    }

    private string FormatSseEvent(ChatMessageEventData messageData)
    {
        var json = JsonSerializer.Serialize(messageData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return $"id: {messageData.Id}\nevent: message\ndata: {json}\n\n";
    }
}

