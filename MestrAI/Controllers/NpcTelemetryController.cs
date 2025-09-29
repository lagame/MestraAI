using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RPGSessionManager.Dtos;
using RPGSessionManager.Services;

namespace RPGSessionManager.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Narrator")] // Apenas Narradores e Admins podem acessar
public class NpcTelemetryController : ControllerBase
{
    private readonly INpcTelemetryService _telemetryService;
    private readonly ILogger<NpcTelemetryController> _logger;

    public NpcTelemetryController(INpcTelemetryService telemetryService, ILogger<NpcTelemetryController> logger)
    {
        _telemetryService = telemetryService;
        _logger = logger;
    }

    [HttpGet("session/{sessionId}")]
    [ProducesResponseType(typeof(NpcMetricsDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<NpcMetricsDto>> GetSessionMetrics(int sessionId)
    {
        _logger.LogInformation("Request for session metrics for session {SessionId}", sessionId);
        var metrics = await _telemetryService.GetSessionMetricsAsync(sessionId);
        return Ok(metrics);
    }

    [HttpGet("system")]
    [ProducesResponseType(typeof(SystemMetricsDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<ActionResult<SystemMetricsDto>> GetSystemMetrics()
    {
        _logger.LogInformation("Request for system metrics");
        var metrics = await _telemetryService.GetSystemMetricsAsync();
        return Ok(metrics);
    }
}

