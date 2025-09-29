using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RPGSessionManager.Services;

namespace RPGSessionManager.Controllers;

[ApiController]
[Route("api/chat/admin")]
[Authorize(Roles = "Admin")]
public class ChatAdminController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly IChatStreamManager _streamManager;
    private readonly ILogger<ChatAdminController> _logger;

    public ChatAdminController(
        ChatService chatService,
        IChatStreamManager streamManager,
        ILogger<ChatAdminController> logger)
    {
        _chatService = chatService;
        _streamManager = streamManager;
        _logger = logger;
    }

    [HttpGet("stats")]
    public IActionResult GetChatStats()
    {
        try
        {
            var chatStats = _chatService.GetStats();
            var connectionCounts = _streamManager.GetSessionConnectionCounts();

            var stats = new
            {
                ChatService = new
                {
                    ActiveRateLimitEntries = chatStats.ActiveRateLimitEntries,
                    ActiveDuplicateHashes = chatStats.ActiveDuplicateHashes,
                    RateLimitInterval = chatStats.RateLimitInterval.TotalSeconds,
                    DuplicateCheckWindow = chatStats.DuplicateCheckWindow.TotalMinutes
                },
                Connections = new
                {
                    TotalSessions = connectionCounts.Count,
                    TotalConnections = connectionCounts.Values.Sum(),
                    SessionDetails = connectionCounts.Select(kvp => new
                    {
                        SessionId = kvp.Key,
                        ConnectionCount = kvp.Value
                    }).ToList()
                },
                Timestamp = DateTime.UtcNow
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat statistics");
            return StatusCode(500, new { error = "Failed to retrieve chat statistics" });
        }
    }

    [HttpGet("sessions/{sessionId:int}/connections")]
    public IActionResult GetSessionConnections(int sessionId)
    {
        try
        {
            var connectionCount = _streamManager.GetActiveConnectionCount(sessionId);
            
            return Ok(new
            {
                SessionId = sessionId,
                ActiveConnections = connectionCount,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session {SessionId} connections", sessionId);
            return StatusCode(500, new { error = "Failed to retrieve session connections" });
        }
    }

    [HttpPost("sessions/{sessionId:int}/broadcast")]
    public async Task<IActionResult> BroadcastSystemMessage(int sessionId, [FromBody] BroadcastMessageRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message cannot be empty" });
            }

            await _chatService.AddSystemMessageAsync(sessionId, request.Message);
            
            _logger.LogInformation("Admin broadcast message sent to session {SessionId}: {Message}", 
                sessionId, request.Message);

            return Ok(new { success = true, message = "System message broadcasted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting system message to session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to broadcast system message" });
        }
    }

    [HttpGet("health")]
    public IActionResult GetHealthStatus()
    {
        try
        {
            var connectionCounts = _streamManager.GetSessionConnectionCounts();
            var chatStats = _chatService.GetStats();

            var health = new
            {
                Status = "Healthy",
                Services = new
                {
                    ChatService = new
                    {
                        Status = "Running",
                        ActiveEntries = chatStats.ActiveRateLimitEntries + chatStats.ActiveDuplicateHashes
                    },
                    StreamManager = new
                    {
                        Status = "Running",
                        ActiveSessions = connectionCounts.Count,
                        TotalConnections = connectionCounts.Values.Sum()
                    }
                },
                Timestamp = DateTime.UtcNow
            };

            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health status");
            return StatusCode(500, new { 
                Status = "Unhealthy", 
                Error = ex.Message,
                Timestamp = DateTime.UtcNow 
            });
        }
    }
}

public class BroadcastMessageRequest
{
    public string Message { get; set; } = string.Empty;
}

