using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Dtos;
using RPGSessionManager.Models;
using System;
using System.Threading.Tasks;
using static RPGSessionManager.Hubs.BattlemapHub;

namespace RPGSessionManager.Services
{
    public class BattlemapService : IBattlemapService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BattlemapService> _logger;

        public BattlemapService(ApplicationDbContext context, ILogger<BattlemapService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<MapToken?> AddTokenAsync(int sessionId, int battleMapId, string name, string? imageUrl, float x, float y, string ownerId)
        {
            try
            {
                var battleMap = await _context.BattleMaps
                    .Include(bm => bm.Session)
                    .FirstOrDefaultAsync(bm => bm.Id == battleMapId && bm.SessionId == sessionId);

                if (battleMap == null)
                {
                    _logger.LogWarning("Attempted to add token to non-existent or wrong session's battlemap. BattleMapId: {BattleMapId}, SessionId: {SessionId}", battleMapId, sessionId);
                    return null;
                }

                var session = battleMap.Session;
                var hasAccess = session.NarratorId == ownerId || (session.Participants?.Contains(ownerId) == true);

                if (!hasAccess)
                {
                     _logger.LogWarning("User {UserId} does not have access to add tokens in session {SessionId}", ownerId, sessionId);
                    return null;
                }

                var gridSize = battleMap.GridSize;
                var snappedX = (float)(Math.Round(x / gridSize) * gridSize);
                var snappedY = (float)(Math.Round(y / gridSize) * gridSize);

                var token = new MapToken
                {
                    BattleMapId = battleMapId,
                    Name = string.IsNullOrWhiteSpace(name) ? "New Token" : name.Trim(),
                    ImageUrl = imageUrl,
                    OwnerId = ownerId,
                    X = snappedX,
                    Y = snappedY,
                    Scale = 1f,
                    Rotation = 0f,
                    IsVisible = true,
                    Z = 0,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.MapTokens.Add(token);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Token {TokenId} '{TokenName}' added by user {UserId} in session {SessionId}", token.Id, token.Name, ownerId, sessionId);
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding token in session {SessionId} by user {UserId}", sessionId, ownerId);
                return null;
            }
        }

        public async Task<MapToken?> MoveTokenAsync(int sessionId, Guid tokenId, float x, float y, string currentUserId)
        {
            try
            {
                var token = await _context.MapTokens
                    .Include(t => t.BattleMap)
                    .ThenInclude(bm => bm.Session)
                    .FirstOrDefaultAsync(t => t.Id == tokenId);

                if (token == null || token.BattleMap.Session.Id != sessionId)
                {
                    _logger.LogWarning("Token not found or does not belong to session. TokenId: {TokenId}, SessionId: {SessionId}", tokenId, sessionId);
                    return null;
                }

                var session = token.BattleMap.Session;
                var isNarrator = session.NarratorId == currentUserId;
                var isOwner = token.OwnerId == currentUserId;

                if (!isNarrator && !isOwner)
                {
                    _logger.LogWarning("User {UserId} does not have permission to move token {TokenId}", currentUserId, tokenId);
                    return null;
                }

                var gridSize = token.BattleMap.GridSize;
                var snappedX = (float)(Math.Round(x / gridSize) * gridSize);
                var snappedY = (float)(Math.Round(y / gridSize) * gridSize);

                token.X = snappedX;
                token.Y = snappedY;
                token.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Token {TokenId} moved to ({X}, {Y}) by user {UserId} in session {SessionId}", tokenId, snappedX, snappedY, currentUserId, sessionId);
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error moving token {TokenId} in session {SessionId} by user {UserId}", tokenId, sessionId, currentUserId);
                return null;
            }
        }

        public async Task<bool> RemoveTokenAsync(int sessionId, Guid tokenId, string currentUserId)
        {
            try
            {
                var token = await _context.MapTokens
                    .Include(t => t.BattleMap)
                    .ThenInclude(bm => bm.Session)
                    .FirstOrDefaultAsync(t => t.Id == tokenId);

                if (token == null || token.BattleMap.Session.Id != sessionId)
                {
                    _logger.LogWarning("Token not found or does not belong to session for removal. TokenId: {TokenId}, SessionId: {SessionId}", tokenId, sessionId);
                    return false;
                }

                var session = token.BattleMap.Session;
                var isNarrator = session.NarratorId == currentUserId;
                var isOwner = token.OwnerId == currentUserId;

                if (!isNarrator && !isOwner)
                {
                    _logger.LogWarning("User {UserId} does not have permission to remove token {TokenId}", currentUserId, tokenId);
                    return false;
                }

                _context.MapTokens.Remove(token);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Token {TokenId} removed by user {UserId} in session {SessionId}", tokenId, currentUserId, sessionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing token {TokenId} in session {SessionId} by user {UserId}", tokenId, sessionId, currentUserId);
                return false;
            }
        }

        public async Task<BattleMap?> SetMapImageAsync(int sessionId, int battleMapId, string imageUrl, string currentUserId)
        {
            try
            {
                var battleMap = await _context.BattleMaps
                    .Include(bm => bm.Session)
                    .FirstOrDefaultAsync(bm => bm.Id == battleMapId && bm.SessionId == sessionId);

                if (battleMap == null)
                {
                    _logger.LogWarning("Battlemap not found for setting image. BattleMapId: {Id}", battleMapId);
                    return null;
                }

                // Apenas o Narrador pode mudar o mapa
                if (battleMap.Session.NarratorId != currentUserId)
                {
                    _logger.LogWarning("User {UserId} does não have permission to change map image for session {SessionId}", currentUserId, sessionId);
                    return null;
                }

                battleMap.BackgroundUrl = imageUrl;
                battleMap.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Background image for Battlemap {Id} updated by {UserId}", battleMapId, currentUserId);
                return battleMap;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting map image for Battlemap {Id}", battleMapId);
                return null;
            }
        }

        public async Task<BattleMap?> UpdateGridSettingsAsync(int sessionId, int battleMapId, GridSettingsDto settings, string currentUserId)
        {
            try
            {
                var battleMap = await _context.BattleMaps
                    .Include(bm => bm.Session)
                    .FirstOrDefaultAsync(bm => bm.Id == battleMapId && bm.SessionId == sessionId);

                if (battleMap == null)
                {
                    _logger.LogWarning("Battlemap not found for updating grid settings. BattleMapId: {Id}", battleMapId);
                    return null;
                }

                // Regra de permissão: Apenas o Narrador pode mudar o grid.
                if (battleMap.Session.NarratorId != currentUserId)
                {
                    _logger.LogWarning("User {UserId} does not have permission to change grid settings for session {SessionId}", currentUserId, sessionId);
                    return null;
                }

                // Atualiza as propriedades
                battleMap.GridSize = settings.GridSize;
                battleMap.GridUnitValue = settings.GridUnitValue;
                battleMap.GridUnit = settings.GridUnit ?? string.Empty;
                battleMap.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Grid settings for Battlemap {Id} updated by {UserId}", battleMapId, currentUserId);

                return battleMap;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating grid settings for Battlemap {Id}", battleMapId);
                return null;
            }
        }
    
    public async Task<MapToken?> UpdateTokenAsync(int sessionId, Guid tokenId, UpdateTokenDto updates, string currentUserId)
    {
      try
      {
        var token = await _context.MapTokens
            .Include(t => t.BattleMap)
            .ThenInclude(bm => bm.Session)
            .FirstOrDefaultAsync(t => t.Id == tokenId);

        if (token == null || token.BattleMap.Session.Id != sessionId)
        {
          _logger.LogWarning("Token not found or does not belong to session for update. TokenId: {TokenId}, SessionId: {SessionId}", tokenId, sessionId);
          return null;
        }

        var session = token.BattleMap.Session;
        var isNarrator = session.NarratorId == currentUserId;
        var isOwner = token.OwnerId == currentUserId;

        // Regra de permissão: Narrador pode editar tudo, dono pode mover, mas só narrador redimensiona/rotaciona
        if (!isNarrator && !isOwner)
        {
          _logger.LogWarning("User {UserId} does not have permission to update token {TokenId}", currentUserId, tokenId);
          return null;
        }

        // Se o usuário não for o narrador, ele só pode atualizar posição (X, Y)
        if (!isNarrator && (updates.Scale != default(double) || updates.Rotation != default(double) || updates.IsVisible != default(bool)))
        {
          _logger.LogWarning("User {UserId} attempted to update restricted properties of token {TokenId}", currentUserId, tokenId);
          return null; // Ou podemos simplesmente ignorar esses campos
        }

        // Aplica as atualizações
        if (updates.X != default(double) && updates.Y != default(double))
        {
          var gridSize = token.BattleMap.GridSize;
          token.X = (float)(Math.Round(updates.X / gridSize) * gridSize);
          token.Y = (float)(Math.Round(updates.Y / gridSize) * gridSize);
        }

        if (updates.Scale != default(double))
        {
          // Adiciona limites para evitar valores absurdos
          token.Scale = (float)Math.Clamp(updates.Scale, 0.1f, 5.0f);
        }

        if (updates.Rotation != default(double))
        {
          token.Rotation = (float)(updates.Rotation % 360);
        }
        
        token.IsVisible = updates.IsVisible;        

        token.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Token {TokenId} updated by user {UserId} in session {SessionId}", tokenId, currentUserId, sessionId);
        return token;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error updating token {TokenId} in session {SessionId} by user {UserId}", tokenId, sessionId, currentUserId);
        return null;
      }
    }
  }
}

