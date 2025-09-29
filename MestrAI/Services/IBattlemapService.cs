using RPGSessionManager.Dtos;
using RPGSessionManager.Models;
using System;
using System.Threading.Tasks;
using static RPGSessionManager.Hubs.BattlemapHub;

namespace RPGSessionManager.Services
{
    public interface IBattlemapService
    {
        Task<MapToken?> AddTokenAsync(int sessionId, int battleMapId, string name, string? imageUrl, float x, float y, string ownerId);
        Task<MapToken?> MoveTokenAsync(int sessionId, Guid tokenId, float x, float y, string currentUserId);
        Task<bool> RemoveTokenAsync(int sessionId, Guid tokenId, string currentUserId);
        Task<BattleMap?> SetMapImageAsync(int sessionId, int battleMapId, string imageUrl, string currentUserId);
        Task<BattleMap?> UpdateGridSettingsAsync(int sessionId, int battleMapId, GridSettingsDto settings, string currentUserId);
        Task<MapToken?> UpdateTokenAsync(int sessionId, Guid tokenId, UpdateTokenDto updates, string currentUserId);

  }
}
