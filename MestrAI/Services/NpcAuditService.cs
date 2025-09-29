using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Dtos;
using RPGSessionManager.Models;

namespace RPGSessionManager.Services;

public class NpcAuditService : INpcAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NpcAuditService> _logger;

    public NpcAuditService(ApplicationDbContext context, ILogger<NpcAuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogStateChangeAsync(SessionAiCharacter currentState, SetNpcStatusRequest request, string userId, string changeSource = "Manual")
    {
        var changes = new List<AiNpcStateChange>();

        // Log mudança de IsActive
        if (request.IsActive.HasValue && request.IsActive.Value != currentState.IsActive)
        {
            changes.Add(new AiNpcStateChange
            {
                SessionId = currentState.SessionId,
                CharacterId = currentState.AiCharacterId,
                PropertyChanged = nameof(SessionAiCharacter.IsActive),
                OldValue = currentState.IsActive.ToString(),
                NewValue = request.IsActive.Value.ToString(),
                ChangedBy = userId,
                Reason = request.Reason,
                ChangeSource = changeSource
            });
        }

        // Log mudança de IsVisible
        if (request.IsVisible.HasValue && request.IsVisible.Value != currentState.IsVisible)
        {
            changes.Add(new AiNpcStateChange
            {
                SessionId = currentState.SessionId,
                CharacterId = currentState.AiCharacterId,
                PropertyChanged = nameof(SessionAiCharacter.IsVisible),
                OldValue = currentState.IsVisible.ToString(),
                NewValue = request.IsVisible.Value.ToString(),
                ChangedBy = userId,
                Reason = request.Reason,
                ChangeSource = changeSource
            });
        }

        // Log mudança de InteractionFrequency
        if (request.InteractionFrequency.HasValue && request.InteractionFrequency.Value != currentState.InteractionFrequency)
        {
            changes.Add(new AiNpcStateChange
            {
                SessionId = currentState.SessionId,
                CharacterId = currentState.AiCharacterId,
                PropertyChanged = nameof(SessionAiCharacter.InteractionFrequency),
                OldValue = currentState.InteractionFrequency.ToString(),
                NewValue = request.InteractionFrequency.Value.ToString(),
                ChangedBy = userId,
                Reason = request.Reason,
                ChangeSource = changeSource
            });
        }

        // Log mudança de PersonalitySettings
        if (!string.IsNullOrEmpty(request.PersonalitySettings) && request.PersonalitySettings != currentState.PersonalitySettings)
        {
            changes.Add(new AiNpcStateChange
            {
                SessionId = currentState.SessionId,
                CharacterId = currentState.AiCharacterId,
                PropertyChanged = nameof(SessionAiCharacter.PersonalitySettings),
                OldValue = currentState.PersonalitySettings ?? "null",
                NewValue = request.PersonalitySettings,
                ChangedBy = userId,
                Reason = request.Reason,
                ChangeSource = changeSource
            });
        }

        if (changes.Any())
        {
            _context.AiNpcStateChanges.AddRange(changes);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Registradas {Count} mudanças de estado para NPC {CharacterId} por {UserId}", 
                changes.Count, currentState.AiCharacterId, userId);
        }
    }

    public async Task<List<AuditLogDto>> GetSessionAuditLogAsync(int sessionId, int pageSize = 50, int pageNumber = 1)
    {
        var skip = (pageNumber - 1) * pageSize;
        
        var logs = await _context.AiNpcStateChanges
            .Where(log => log.SessionId == sessionId)
            .Join(_context.CharacterSheets,
                  log => log.CharacterId,
                  cs => cs.Id,
                  (log, cs) => new AuditLogDto
                  {
                      Id = log.Id,
                      SessionId = log.SessionId,
                      CharacterId = log.CharacterId,
                      CharacterName = cs.Name,
                      PropertyChanged = log.PropertyChanged,
                      OldValue = log.OldValue,
                      NewValue = log.NewValue,
                      ChangedBy = log.ChangedBy,
                      ChangedAt = log.ChangedAt,
                      Reason = log.Reason,
                      ChangeSource = log.ChangeSource
                  })
            .OrderByDescending(log => log.ChangedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return logs;
    }

    public async Task<List<AuditLogDto>> GetCharacterAuditLogAsync(int characterId, int pageSize = 50, int pageNumber = 1)
    {
        var skip = (pageNumber - 1) * pageSize;
        
        var logs = await _context.AiNpcStateChanges
            .Where(log => log.CharacterId == characterId)
            .Join(_context.CharacterSheets,
                  log => log.CharacterId,
                  cs => cs.Id,
                  (log, cs) => new AuditLogDto
                  {
                      Id = log.Id,
                      SessionId = log.SessionId,
                      CharacterId = log.CharacterId,
                      CharacterName = cs.Name,
                      PropertyChanged = log.PropertyChanged,
                      OldValue = log.OldValue,
                      NewValue = log.NewValue,
                      ChangedBy = log.ChangedBy,
                      ChangedAt = log.ChangedAt,
                      Reason = log.Reason,
                      ChangeSource = log.ChangeSource
                  })
            .OrderByDescending(log => log.ChangedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return logs;
    }

    public async Task<bool> HasRecentChangesAsync(int sessionId, int characterId, TimeSpan timeWindow)
    {
        var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);
        
        return await _context.AiNpcStateChanges
            .AnyAsync(log => log.SessionId == sessionId && 
                           log.CharacterId == characterId && 
                           log.ChangedAt > cutoffTime);
    }
}
