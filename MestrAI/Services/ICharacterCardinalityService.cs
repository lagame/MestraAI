using RPGSessionManager.Models;

namespace RPGSessionManager.Services
{
    public interface ICharacterCardinalityService
    {
        Task<bool> CanUserCreateCharacterAsync(string userId, int sessionId);
        Task<bool> CanUserHaveMultipleCharactersAsync(string userId, int sessionId);
        Task<int> GetMaxCharactersPerUserAsync(int sessionId);
        Task<int> GetUserCharacterCountAsync(string userId, int sessionId);
        Task<bool> ValidateCharacterLimitsAsync(string userId, int sessionId);
        Task<List<CharacterSheet>> GetUserCharactersInSessionAsync(string userId, int sessionId);
        Task<bool> CanActivateCharacterAsync(int characterId, int sessionId);
        Task<bool> CanDeactivateCharacterAsync(int characterId, int sessionId);
    }
}

