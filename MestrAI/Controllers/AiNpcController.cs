using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using RPGSessionManager.Data;
using RPGSessionManager.Dtos;
using RPGSessionManager.Models;
using RPGSessionManager.Services;

namespace RPGSessionManager.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiNpcController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly INpcStateCache _npcCache;
    private readonly INpcAuditService _auditService;
    private readonly IPermissionService _permissionService;
    private readonly ISignalRNotificationService _signalRService;
    private readonly ILogger<AiNpcController> _logger;
    private readonly INpcTelemetryService _telemetryService;

    public AiNpcController(
        ApplicationDbContext context,
        INpcStateCache npcCache,
        INpcAuditService auditService,
        IPermissionService permissionService,
        ISignalRNotificationService signalRService,
        ILogger<AiNpcController> logger,
        INpcTelemetryService telemetryService)
    {
        _context = context;
        _npcCache = npcCache;
        _auditService = auditService;
        _permissionService = permissionService;
        _signalRService = signalRService;
        _logger = logger;
        _telemetryService = telemetryService;
    }

    [HttpPost("set-status")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<NpcStatusResponse>> SetNpcStatus([FromBody] SetNpcStatusRequest request)
    {
        try
        {
            // Validação de entrada
            if (!ModelState.IsValid)
            {
                return BadRequest(new NpcStatusResponse
                {
                    Success = false,
                    Message = "Dados de entrada inválidos",
                    Errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList()
                });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new NpcStatusResponse
                {
                    Success = false,
                    Message = "Usuário não autenticado"
                });
            }

            // Verificar permissões
            if (!await _permissionService.CanManageSessionAsync(userId, request.SessionId))
            {
                return Forbid();
            }

            // Verificar se o personagem existe e tem IA habilitada
            var character = await _context.CharacterSheets
                .FirstOrDefaultAsync(cs => cs.Id == request.CharacterId && 
                                         cs.SessionId == request.SessionId && 
                                         cs.AiEnabled);

            if (character == null)
            {
                return NotFound(new NpcStatusResponse
                {
                    Success = false,
                    Message = "Personagem não encontrado ou não tem IA habilitada"
                });
            }

            // Verificar rate limiting (não mais de 10 mudanças por minuto por NPC)
            if (await _auditService.HasRecentChangesAsync(request.SessionId, request.CharacterId, TimeSpan.FromMinutes(1)))
            {
                var recentChanges = await _context.AiNpcStateChanges
                    .Where(log => log.SessionId == request.SessionId && 
                                log.CharacterId == request.CharacterId && 
                                log.ChangedAt > DateTime.UtcNow.AddMinutes(-1))
                    .CountAsync();

                if (recentChanges >= 10)
                {
                    return StatusCode(429, new NpcStatusResponse
                    {
                        Success = false,
                        Message = "Muitas mudanças recentes. Aguarde antes de fazer novas alterações."
                    });
                }
            }

            // Buscar ou criar estado do NPC
            var npcState = await _npcCache.GetNpcStateAsync(request.SessionId, request.CharacterId);
            
            if (npcState == null)
            {
                // Criar novo estado
                npcState = new SessionAiCharacter
                {
                    SessionId = request.SessionId,
                    AiCharacterId = request.CharacterId,
                    IsActive = request.IsActive ?? false,
                    IsVisible = request.IsVisible ?? true,
                    InteractionFrequency = request.InteractionFrequency ?? 50,
                    PersonalitySettings = request.PersonalitySettings ?? new PersonalitySettings().ToJson(),
                    LastModifiedBy = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                _context.SessionAiCharacters.Add(npcState);
                await _context.SaveChangesAsync();

                // Log da criação
                await _auditService.LogStateChangeAsync(new SessionAiCharacter(), request, userId, "Creation");
            }
            else
            {
                // Registrar mudanças para auditoria ANTES de alterar
                await _auditService.LogStateChangeAsync(npcState, request, userId);
                
                // Atualizar estado existente
                if (request.IsActive.HasValue)
                {
                    npcState.IsActive = request.IsActive.Value;
                }
                if (request.IsVisible.HasValue)
                {
                    npcState.IsVisible = request.IsVisible.Value;
                }
                if (request.InteractionFrequency.HasValue)
                {
                    npcState.InteractionFrequency = request.InteractionFrequency.Value;
                }
                if (!string.IsNullOrEmpty(request.PersonalitySettings))
                {
                    npcState.PersonalitySettings = request.PersonalitySettings;
                }
                npcState.UpdatedAt = DateTime.UtcNow;
                npcState.LastModifiedBy = userId;

                _context.SessionAiCharacters.Update(npcState);
                await _context.SaveChangesAsync();
            }

            // Atualizar cache
            await _npcCache.SetNpcStateAsync(npcState);

            var npcStateDto = new NpcStateDto
            {
                Id = npcState.Id,
                SessionId = npcState.SessionId,
                CharacterId = npcState.AiCharacterId,
                CharacterName = character.Name,
                IsActive = npcState.IsActive,
                IsVisible = npcState.IsVisible,
                InteractionFrequency = npcState.InteractionFrequency,
                PersonalitySettings = PersonalitySettings.FromJson(string.IsNullOrWhiteSpace(npcState.PersonalitySettings) ? "{}" : npcState.PersonalitySettings),

                UpdatedAt = npcState.UpdatedAt,
                LastModifiedBy = npcState.LastModifiedBy
            };

            return Ok(new NpcStatusResponse
            {
                Success = true,
                Message = "Status do NPC atualizado com sucesso",
                Data = npcStateDto
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao definir status do NPC para SessionId: {SessionId}, CharacterId: {CharacterId}", request.SessionId, request.CharacterId);
            return StatusCode(500, new NpcStatusResponse
            {
                Success = false,
                Message = "Ocorreu um erro interno ao processar a solicitação."
            });
        }
    }

    [HttpGet("session/{sessionId}/npcs")]
    public async Task<ActionResult<SessionNpcsResponse>> GetSessionNpcs(int sessionId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new SessionNpcsResponse
                {
                    Success = false,
                    Message = "Usuário não autenticado"
                });
            }

            if (!await _permissionService.CanViewSessionAsync(userId, sessionId))
            {
                return Forbid();
            }

            var sessionNpcs = await _context.CharacterSheets
                .Where(cs => cs.SessionId == sessionId && cs.AiEnabled)
                .Select(cs => new SessionNpcDto
                {
                    CharacterId = cs.Id,
                    CharacterName = cs.Name,
                    AiEnabled = cs.AiEnabled,
                    CreatedAt = cs.CreatedAt
                })
                .ToListAsync();

            foreach (var npcDto in sessionNpcs)
            {
                var npcState = await _npcCache.GetNpcStateAsync(sessionId, npcDto.CharacterId);
                if (npcState != null)
                {
                    npcDto.State = new NpcStateDto
                    {
                        Id = npcState.Id,
                        SessionId = npcState.SessionId,
                        CharacterId = npcState.AiCharacterId,
                        CharacterName = npcDto.CharacterName,
                        IsActive = npcState.IsActive,
                        IsVisible = npcState.IsVisible,
                        InteractionFrequency = npcState.InteractionFrequency,
                        PersonalitySettings = PersonalitySettings.FromJson(string.IsNullOrWhiteSpace(npcState.PersonalitySettings) ? "{}" : npcState.PersonalitySettings),
                        UpdatedAt = npcState.UpdatedAt,
                        LastModifiedBy = npcState.LastModifiedBy
                    };
                }
            }

            return Ok(new SessionNpcsResponse
            {
                Success = true,
                Message = "NPCs da sessão recuperados com sucesso",
                Npcs = sessionNpcs,
                TotalCount = sessionNpcs.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao recuperar NPCs para SessionId: {SessionId}", sessionId);
            return StatusCode(500, new SessionNpcsResponse
            {
                Success = false,
                Message = "Ocorreu um erro interno ao processar a solicitação."
            });
        }
    }

    [HttpGet("session/{sessionId}/audit")]
    public async Task<ActionResult<List<AuditLogDto>>> GetSessionAuditLog(int sessionId, [FromQuery] int pageSize = 50, [FromQuery] int pageNumber = 1)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (!await _permissionService.CanViewSessionAsync(userId, sessionId))
            {
                return Forbid();
            }

            var auditLogs = await _auditService.GetSessionAuditLogAsync(sessionId, pageSize, pageNumber);
            return Ok(auditLogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao recuperar log de auditoria da sessão para SessionId: {SessionId}", sessionId);
            return StatusCode(500, "Ocorreu um erro interno ao processar a solicitação.");
        }
    }

    [HttpGet("character/{characterId}/audit")]
    public async Task<ActionResult<List<AuditLogDto>>> GetCharacterAuditLog(int characterId, [FromQuery] int pageSize = 50, [FromQuery] int pageNumber = 1)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            // TODO: Implementar verificação de permissão para CharacterId
            // Por enquanto, apenas verifica se o usuário pode ver alguma sessão que contenha este personagem.
            // Idealmente, deveria verificar se o usuário tem permissão para o personagem específico.
            var character = await _context.CharacterSheets.FindAsync(characterId);
            if (character == null || !await _permissionService.CanViewSessionAsync(userId, character.SessionId))
            {
                return Forbid();
            }

            var auditLogs = await _auditService.GetCharacterAuditLogAsync(characterId, pageSize, pageNumber);
            return Ok(auditLogs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao recuperar log de auditoria do personagem para CharacterId: {CharacterId}", characterId);
            return StatusCode(500, "Ocorreu um erro interno ao processar a solicitação.");
        }
    }

    [HttpGet("session/{sessionId}/metrics")]
    public async Task<ActionResult<NpcMetricsDto>> GetSessionMetrics(int sessionId)
    {
        try
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (!await _permissionService.CanViewSessionAsync(userId, sessionId))
            {
                return Forbid();
            }

            var metrics = await _telemetryService.GetSessionMetricsAsync(sessionId);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao recuperar métricas para SessionId: {SessionId}", sessionId);
            return StatusCode(500, "Ocorreu um erro interno ao processar a solicitação.");
        }
    }
}
