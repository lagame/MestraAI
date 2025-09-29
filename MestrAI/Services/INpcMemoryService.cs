using RPGSessionManager.Models;

namespace RPGSessionManager.Services;

public interface INpcMemoryService
{
  Task SaveMemoryAsync(int characterId, int sessionId, string memoryType, string content, int importance);
  Task<List<NpcLongTermMemory>> GetRelevantMemoriesAsync(int characterId, string context, int maxMemories = 5);
  Task UpdateMemoryAccessAsync(int memoryId);
  Task EvolvePersonalityAsync(int characterId, string interaction);
  Task<List<NpcLongTermMemory>> GetCharacterMemoriesAsync(int characterId, string? memoryType = null);
  Task CleanupOldMemoriesAsync(int characterId, int maxMemories = 100);
}
