using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RPGSessionManager.Models;
using RPGSessionManager.Services;
using System.Security.Claims;

namespace RPGSessionManager.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatMessageService _chatMessageService;
    private readonly IPermissionService _permissionService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatMessageService chatMessageService,
        IPermissionService permissionService,
        ILogger<ChatController> logger)
    {
        _chatMessageService = chatMessageService;
        _permissionService = permissionService;
        _logger = logger;
    }

    /// <summary>
    /// Envia uma nova mensagem de chat
    /// </summary>
    [HttpPost("sessions/{sessionId}/messages")]
    public async Task<IActionResult> SendMessage(int sessionId, [FromBody] SendMessageRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name ?? "Unknown";

            // Verificar permissões
            if (!await _permissionService.CanAccessSessionAsync(userId, sessionId))
            {
                return Forbid("Acesso negado à sessão");
            }

            // Validar entrada
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest("Conteúdo da mensagem não pode estar vazio");
            }

            if (request.Content.Length > 2000)
            {
                return BadRequest("Mensagem muito longa (máximo 2000 caracteres)");
            }

            // Determinar tipo de remetente
            var senderType = request.CharacterId.HasValue ? SenderType.Character : SenderType.User;
            var senderName = request.SenderName ?? userName;

            // Enviar mensagem
            var message = await _chatMessageService.SendMessageAsync(
                sessionId,
                request.Content.Trim(),
                senderName,
                userId,
                request.MessageType,
                senderType,
                request.CharacterId);

            _logger.LogInformation("Message {MessageId} sent by user {UserId} to session {SessionId}", 
                message.MessageId, userId, sessionId);

            return Ok(new
            {
                MessageId = message.MessageId,
                Success = true,
                Timestamp = message.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to session {SessionId}", sessionId);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Busca mensagens para replay (após um MessageId específico)
    /// </summary>
    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<IActionResult> GetMessages(
        int sessionId,
        [FromQuery] string? afterId = null,
        [FromQuery] int limit = 50)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Verificar permissões
            if (!await _permissionService.CanAccessSessionAsync(userId, sessionId))
            {
                return Forbid("Acesso negado à sessão");
            }

            // Limitar o número de mensagens
            limit = Math.Min(limit, 100);

            var messages = await _chatMessageService.GetMessagesAfterAsync(sessionId, afterId, limit);

            var result = messages.Select(m => new
            {
                MessageId = m.MessageId,
                SessionId = m.SessionId,
                Content = m.Content,
                SenderName = m.SenderName,
                SenderType = m.SenderType.ToString(),
                MessageType = m.Type.ToString(),
                IsAiGenerated = m.IsAiGenerated,
                CreatedAt = m.CreatedAt,
                CharacterId = m.CharacterId
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting messages for session {SessionId}", sessionId);
            return StatusCode(500, "Erro interno do servidor");
        }
    }

    /// <summary>
    /// Busca mensagens recentes de uma sessão
    /// </summary>
    [HttpGet("sessions/{sessionId}/messages/recent")]
    public async Task<IActionResult> GetRecentMessages(int sessionId, [FromQuery] int count = 50)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Verificar permissões
            if (!await _permissionService.CanAccessSessionAsync(userId, sessionId))
            {
                return Forbid("Acesso negado à sessão");
            }

            // Limitar o número de mensagens
            count = Math.Min(count, 100);

            var messages = await _chatMessageService.GetRecentMessagesAsync(sessionId, count);

            var result = messages.Select(m => new
            {
                MessageId = m.MessageId,
                SessionId = m.SessionId,
                Content = m.Content,
                SenderName = m.SenderName,
                SenderType = m.SenderType.ToString(),
                MessageType = m.Type.ToString(),
                IsAiGenerated = m.IsAiGenerated,
                CreatedAt = m.CreatedAt,
                CharacterId = m.CharacterId
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent messages for session {SessionId}", sessionId);
            return StatusCode(500, "Erro interno do servidor");
        }
    }
}

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public string? SenderName { get; set; }
    public MessageType MessageType { get; set; } = MessageType.PlayerMessage;
    public int? CharacterId { get; set; }
}

