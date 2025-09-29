using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;

namespace RPGSessionManager.Services;

public class NpcStateCache : INpcStateCache
{
    private readonly IMemoryCache _cache;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NpcStateCache> _logger;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _sessionCacheExpiry = TimeSpan.FromMinutes(10);

    public NpcStateCache(IMemoryCache cache, ApplicationDbContext context, ILogger<NpcStateCache> logger)
    {
        _cache = cache;
        _context = context;
        _logger = logger;
    }

    public async Task<SessionAiCharacter?> GetNpcStateAsync(int sessionId, int characterId)
    {
        var cacheKey = GetNpcCacheKey(sessionId, characterId);
        
        if (_cache.TryGetValue(cacheKey, out SessionAiCharacter? cachedState))
        {
            TrackCacheHit("GetNpcState");
            return cachedState;
        }

        TrackCacheMiss("GetNpcState");
        
        var state = await _context.SessionAiCharacters
            .Include(sac => sac.AiCharacter)
            .Include(sac => sac.Session)
            .FirstOrDefaultAsync(sac => sac.SessionId == sessionId && sac.AiCharacterId == characterId);

        if (state != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheExpiry,
                SlidingExpiration = TimeSpan.FromMinutes(2),
                Priority = CacheItemPriority.Normal
            };

            _cache.Set(cacheKey, state, cacheOptions);
        }

        return state;
    }

    public async Task SetNpcStateAsync(SessionAiCharacter state)
    {
        var cacheKey = GetNpcCacheKey(state.SessionId, state.AiCharacterId);
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheExpiry,
            SlidingExpiration = TimeSpan.FromMinutes(2),
            Priority = CacheItemPriority.Normal
        };

        _cache.Set(cacheKey, state, cacheOptions);
        
        // Invalidar cache da sessão para forçar recarga da lista
        await InvalidateSessionCacheAsync(state.SessionId);
        
        _logger.LogDebug("Cache atualizado para NPC {CharacterId} na sessão {SessionId}", 
            state.AiCharacterId, state.SessionId);
    }

    public async Task<List<SessionAiCharacter>> GetSessionActiveNpcsAsync(int sessionId)
    {
        var cacheKey = GetSessionCacheKey(sessionId);
        
        if (_cache.TryGetValue(cacheKey, out List<SessionAiCharacter>? cachedNpcs))
        {
            TrackCacheHit("GetSessionActiveNpcs");
            return cachedNpcs ?? new List<SessionAiCharacter>();
        }

        TrackCacheMiss("GetSessionActiveNpcs");
        
        var npcs = await _context.SessionAiCharacters
            .Include(sac => sac.AiCharacter)
            .Where(sac => sac.SessionId == sessionId && sac.IsActive && sac.IsVisible)
            .ToListAsync();

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _sessionCacheExpiry,
            SlidingExpiration = TimeSpan.FromMinutes(3),
            Priority = CacheItemPriority.High
        };

        _cache.Set(cacheKey, npcs, cacheOptions);
        
        return npcs;
    }

    public Task InvalidateSessionCacheAsync(int sessionId)
    {
        var sessionCacheKey = GetSessionCacheKey(sessionId);
        _cache.Remove(sessionCacheKey);
        
        _logger.LogDebug("Cache da sessão {SessionId} invalidado", sessionId);
        return Task.CompletedTask;
    }

    public Task InvalidateNpcCacheAsync(int sessionId, int characterId)
    {
        var npcCacheKey = GetNpcCacheKey(sessionId, characterId);
        _cache.Remove(npcCacheKey);
        
        // Também invalidar cache da sessão
        InvalidateSessionCacheAsync(sessionId);
        
        _logger.LogDebug("Cache do NPC {CharacterId} na sessão {SessionId} invalidado", 
            characterId, sessionId);
        return Task.CompletedTask;
    }

    public void TrackCacheHit(string operation)
    {
        _logger.LogDebug("Cache HIT para operação: {Operation}", operation);
        // Aqui pode integrar com sistema de métricas
    }

    public void TrackCacheMiss(string operation)
    {
        _logger.LogDebug("Cache MISS para operação: {Operation}", operation);
        // Aqui pode integrar com sistema de métricas
    }

    private static string GetNpcCacheKey(int sessionId, int characterId) 
        => $"npc_state_{sessionId}_{characterId}";

    private static string GetSessionCacheKey(int sessionId) 
        => $"session_npcs_{sessionId}";
}

