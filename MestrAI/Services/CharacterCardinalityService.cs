using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;

namespace RPGSessionManager.Services
{
    public class CharacterCardinalityService : ICharacterCardinalityService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPermissionService _permissionService;

        // Default limits - can be made configurable later
        private const int DEFAULT_MAX_CHARACTERS_PER_USER = 3;
        private const int NARRATOR_MAX_CHARACTERS = 50; // Narrators can have many NPCs

        public CharacterCardinalityService(ApplicationDbContext context, IPermissionService permissionService)
        {
            _context = context;
            _permissionService = permissionService;
        }

        public async Task<bool> CanUserCreateCharacterAsync(string userId, int sessionId)
        {
            // Check if user has access to the session
            if (!await _permissionService.CanAccessSessionAsync(userId, sessionId))
                return false;

            // Check character limits
            return await ValidateCharacterLimitsAsync(userId, sessionId);
        }

        public async Task<bool> CanUserHaveMultipleCharactersAsync(string userId, int sessionId)
        {
            var maxCharacters = await GetMaxCharactersPerUserAsync(sessionId);
            return maxCharacters > 1;
        }

        public async Task<int> GetMaxCharactersPerUserAsync(int sessionId)
        {
            var session = await _context.Sessions.FindAsync(sessionId);
            if (session == null) return 0;

            // For now, return default limits
            // In the future, this could be configurable per session or tabletop
            return DEFAULT_MAX_CHARACTERS_PER_USER;
        }

        public async Task<int> GetUserCharacterCountAsync(string userId, int sessionId)
        {
            var activeCharacters = await _context.SessionCharacters
                .Where(sc => sc.SessionId == sessionId && 
                            sc.CharacterSheet.PlayerId == userId && 
                            sc.IsActive)
                .CountAsync();

            return activeCharacters;
        }

        public async Task<bool> ValidateCharacterLimitsAsync(string userId, int sessionId)
        {
            var currentCount = await GetUserCharacterCountAsync(userId, sessionId);
            var maxAllowed = await GetMaxCharactersPerUserAsync(sessionId);

            // Narrators have higher limits
            if (await _permissionService.IsSessionNarratorAsync(userId, sessionId))
            {
                return currentCount < NARRATOR_MAX_CHARACTERS;
            }

            return currentCount < maxAllowed;
        }

        public async Task<List<CharacterSheet>> GetUserCharactersInSessionAsync(string userId, int sessionId)
        {
            var characters = await _context.SessionCharacters
                .Include(sc => sc.CharacterSheet)
                .Where(sc => sc.SessionId == sessionId && 
                            sc.CharacterSheet.PlayerId == userId && 
                            sc.IsActive)
                .Select(sc => sc.CharacterSheet)
                .ToListAsync();

            return characters;
        }

        public async Task<bool> CanActivateCharacterAsync(int characterId, int sessionId)
        {
            var character = await _context.CharacterSheets.FindAsync(characterId);
            if (character == null) return false;

            // Check if user can manage characters in this session
            if (!await _permissionService.CanUserManageCharactersAsync(character.PlayerId!, sessionId))
                return false;

            // Check if adding this character would exceed limits
            var currentCount = await GetUserCharacterCountAsync(character.PlayerId!, sessionId);
            var maxAllowed = await GetMaxCharactersPerUserAsync(sessionId);

            // Narrators have higher limits
            if (await _permissionService.IsSessionNarratorAsync(character.PlayerId!, sessionId))
            {
                return currentCount < NARRATOR_MAX_CHARACTERS;
            }

            return currentCount < maxAllowed;
        }

        public async Task<bool> CanDeactivateCharacterAsync(int characterId, int sessionId)
        {
            var character = await _context.CharacterSheets.FindAsync(characterId);
            if (character == null) return false;

            // Check if user can manage characters in this session
            return await _permissionService.CanUserManageCharactersAsync(character.PlayerId!, sessionId);
        }
    }
}

