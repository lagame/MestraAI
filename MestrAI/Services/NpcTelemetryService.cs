using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RPGSessionManager.Data;
using RPGSessionManager.Dtos;
using RPGSessionManager.Models;

namespace RPGSessionManager.Services
{
    /// <summary>
    /// Implementação oficial baseada em EF Core (telemetria persistida).
    /// </summary>
    public class NpcTelemetryService : INpcTelemetryService
    {
        private readonly ILogger<NpcTelemetryService> _logger;
        private readonly ApplicationDbContext _db;
        private readonly ISignalRNotificationService _signalR;

        public NpcTelemetryService(
            ILogger<NpcTelemetryService> logger,
            ApplicationDbContext db,
            ISignalRNotificationService signalR)
        {
            _logger = logger;
            _db = db;
            _signalR = signalR;
        }

        /// <summary>
        /// Registra interação no banco e tenta notificar via SignalR (best-effort).
        /// </summary>
        public async Task RecordInteractionAsync(int sessionId, int characterId, double responseTimeMs)
        {
            var entity = new NpcInteraction
            {
                SessionId = sessionId,
                CharacterId = characterId,
                ResponseTimeMs = responseTimeMs,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.NpcInteractions.Add(entity);
            await _db.SaveChangesAsync();

            try
            {
                await _signalR.NotifyNpcInteractionRecorded(sessionId, characterId, responseTimeMs);
            }
            catch (Exception ex)
            {
                // Telemetria não deve derrubar o fluxo
                _logger.LogDebug(ex, "Falha ao notificar via SignalR (ignorada).");
            }
        }

        /// <summary>
        /// Métricas de um NPC específico na sessão (contagem + média).
        /// </summary>
        public async Task<NpcMetricsDto> GetNpcMetricsAsync(int sessionId, int characterId)
        {
            var query = _db.NpcInteractions
                .AsNoTracking()
                .Where(i => i.SessionId == sessionId && i.CharacterId == characterId);

            var total = await query.CountAsync();
            if (total == 0)
            {
                return new NpcMetricsDto
                {
                    TotalInteractions = 0,
                    AverageResponseTimeMs = 0
                };
            }

            var avg = await query.AverageAsync(i => i.ResponseTimeMs);

            return new NpcMetricsDto
            {
                TotalInteractions = total,
                AverageResponseTimeMs = avg
            };
        }

        /// <summary>
        /// Métricas agregadas da sessão (todos os NPCs da sessão).
        /// </summary>
        public async Task<NpcMetricsDto> GetSessionMetricsAsync(int sessionId)
        {
            var query = _db.NpcInteractions
                .AsNoTracking()
                .Where(i => i.SessionId == sessionId);

            var total = await query.CountAsync();
            if (total == 0)
            {
                return new NpcMetricsDto
                {
                    SessionId = sessionId,
                    TotalInteractions = 0,
                    AverageResponseTimeMs = 0
                };
            }

            var avg = await query.AverageAsync(i => i.ResponseTimeMs);

            return new NpcMetricsDto
            {
                SessionId = sessionId,
                TotalInteractions = total,
                AverageResponseTimeMs = avg
            };
        }

        /// <summary>
        /// Métrica global simples (média geral).
        /// </summary>
        public async Task<SystemMetricsDto> GetSystemMetricsAsync()
        {
            var query = _db.NpcInteractions.AsNoTracking();

            var any = await query.AnyAsync();
            var avg = any ? await query.AverageAsync(i => i.ResponseTimeMs) : 0d;

            return new SystemMetricsDto
            {
                OverallAverageResponseTimeMs = avg
            };
        }

        // --------- Métodos "Track*" mantidos para compatibilidade (no-op com log) ---------

        public void TrackNpcActivation(int sessionId, int characterId, bool isActive)
            => _logger.LogDebug("TrackNpcActivation: session={SessionId}, npc={CharacterId}, isActive={IsActive}",
                                sessionId, characterId, isActive);

        public void TrackNpcVisibilityChange(int sessionId, int characterId, bool isVisible)
            => _logger.LogDebug("TrackNpcVisibilityChange: session={SessionId}, npc={CharacterId}, isVisible={IsVisible}",
                                sessionId, characterId, isVisible);

        public void TrackAiResponseTime(int sessionId, int characterId, TimeSpan elapsed)
            => _logger.LogDebug("TrackAiResponseTime: session={SessionId}, npc={CharacterId}, elapsedMs={Elapsed}",
                                sessionId, characterId, elapsed.TotalMilliseconds);

        public void TrackAiResponseGenerated(int sessionId, int characterId)
            => _logger.LogDebug("TrackAiResponseGenerated: session={SessionId}, npc={CharacterId}", sessionId, characterId);

        public void TrackCacheHit(int sessionId)
            => _logger.LogDebug("TrackCacheHit: session={SessionId}", sessionId);

        public void TrackCacheMiss(int sessionId)
            => _logger.LogDebug("TrackCacheMiss: session={SessionId}", sessionId);
    }
}
