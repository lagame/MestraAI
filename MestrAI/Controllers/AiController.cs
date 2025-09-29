using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RPGSessionManager.Services;

namespace RPGSessionManager.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Narrator,Admin")]
public class AiController : ControllerBase
{
    private readonly AiOrchestrator _aiOrchestrator;
    private readonly ILogger<AiController> _logger;
    private readonly IChatService _chatService;

    public AiController(AiOrchestrator aiOrchestrator, ILogger<AiController> logger, IChatService chatService)
    {
        _aiOrchestrator = aiOrchestrator;
        _logger = logger;
        _chatService = chatService;
    }

    [HttpPost("character/act")]
    public async Task<IActionResult> CharacterAct([FromBody] CharacterActRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // (NOVO) Busca o histórico de mensagens para dar contexto à IA.
            var conversationHistory = await _chatService.GetMessagesSinceLastAiTurnAsync(
                request.SessionId,
                request.CharacterId,
                fallbackMessageCount: 15,
                maxWindowCount: 60);


            // Chama o orquestrador com a LISTA de mensagens, como ele agora espera.
            var response = await _aiOrchestrator.GenerateCharacterReplyAsync(
                request.SessionId,
                request.CharacterId,
                conversationHistory); // CORRIGIDO!
            return Ok(new { response });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating character response");
            return StatusCode(500, "Internal server error");
        }
    }
}

public class CharacterActRequest
{
    public int SessionId { get; set; }
    public int CharacterId { get; set; }
    public string PlayerCommand { get; set; } = string.Empty;
}

