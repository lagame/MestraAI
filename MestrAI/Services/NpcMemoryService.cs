using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using RPGSessionManager.Ai; // Adicione este using se AiConnectionPool estiver nesse namespace

namespace RPGSessionManager.Services;

public class NpcMemoryService : INpcMemoryService
{
  private readonly ApplicationDbContext _context;
  private readonly ILogger<NpcMemoryService> _logger;
  private readonly IAiConnectionPool _aiPool;

  public NpcMemoryService(ApplicationDbContext context, ILogger<NpcMemoryService> logger, IAiConnectionPool aiPool)
  {
    _context = context;
    _logger = logger;
    _aiPool = aiPool;
  }

  public async Task SaveMemoryAsync(int characterId, int sessionId, string memoryType, string content, int importance)
  {
    try
    {
      var memory = new NpcLongTermMemory
      {
        CharacterId = characterId,
        SessionId = sessionId,
        MemoryType = memoryType,
        Content = content,
        Importance = Math.Clamp(importance, 1, 10)
      };

      _context.NpcLongTermMemories.Add(memory);
      await _context.SaveChangesAsync();
      _logger.LogDebug("Memória salva para NPC {CharacterId}: {MemoryType}", characterId, memoryType);
      await CleanupOldMemoriesAsync(characterId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Erro ao salvar memória para NPC {CharacterId}", characterId);
    }
  }

  // ... (Implementação dos outros métodos da interface) ...
  // Para simplificar e fazer o código compilar, vamos adicionar implementações vazias por enquanto.
  // O código completo está no documento da Etapa 5.

  public async Task<List<NpcLongTermMemory>> GetRelevantMemoriesAsync(int characterId, string context, int maxMemories = 5)
  {
    // Lógica para buscar memórias relevantes com base no contexto.
    // Isso pode envolver:
    // 1. Filtrar memórias ativas para o personagem.
    // 2. Usar um modelo de IA para ranquear a relevância das memórias em relação ao contexto.
    // 3. Priorizar memórias com maior importância e acesso mais recente.

    var relevantMemories = await _context.NpcLongTermMemories
        .Where(m => m.CharacterId == characterId && m.IsActive)
        .OrderByDescending(m => m.Importance) // Prioriza memórias mais importantes
        .ThenByDescending(m => m.LastAccessedAt) // Prioriza memórias acessadas recentemente
        .Take(maxMemories)
        .ToListAsync();

    // Para cada memória relevante encontrada, atualiza o LastAccessedAt e AccessCount
    foreach (var memory in relevantMemories)
    {
        await UpdateMemoryAccessAsync(memory.Id);
    }

    return relevantMemories;
  }

  public async Task UpdateMemoryAccessAsync(int memoryId)
  {
    var memory = await _context.NpcLongTermMemories.FindAsync(memoryId);
    if (memory != null)
    {
      memory.LastAccessedAt = DateTime.UtcNow;
      memory.AccessCount++;
      await _context.SaveChangesAsync();
    }
  }

  public async Task EvolvePersonalityAsync(int characterId, string interaction)
  {
    _logger.LogInformation("Evolving personality for NPC {CharacterId} based on interaction: {Interaction}", characterId, interaction);

    // Placeholder para a lógica de evolução de personalidade.
    // Isso envolveria:
    // 1. Recuperar as PersonalitySettings do NPC.
    // 2. Usar um modelo de IA ou regras heurísticas para ajustar traços com base na interação.
    // 3. Salvar as PersonalitySettings atualizadas.
    // 4. Registrar um AiNpcStateChange para auditoria.

    // Exemplo de registro de auditoria (simplificado):
    var character = await _context.CharacterSheets.FindAsync(characterId);
    if (character != null)
    {
        var auditEntry = new AiNpcStateChange
        {
            CharacterId = characterId,
            SessionId = character.SessionId, // Assumindo que CharacterSheet tem SessionId
            ChangeType = "PersonalityEvolution",
            ChangeSource = "Auto",
            Description = $"Personalidade evoluída devido à interação: {interaction}",
            Timestamp = DateTime.UtcNow
        };
        _context.AiNpcStateChanges.Add(auditEntry);
        await _context.SaveChangesAsync();
    }
  }

  public async Task<List<NpcLongTermMemory>> GetCharacterMemoriesAsync(int characterId, string? memoryType = null)
  {
    var query = _context.NpcLongTermMemories.Where(m => m.CharacterId == characterId);
    if (!string.IsNullOrEmpty(memoryType))
    {
      query = query.Where(m => m.MemoryType == memoryType);
    }
    return await query.ToListAsync();
  }

  public async Task CleanupOldMemoriesAsync(int characterId, int maxMemories = 100)
  {
    var memoryCount = await _context.NpcLongTermMemories.CountAsync(m => m.CharacterId == characterId);
    if (memoryCount > maxMemories)
    {
      var memoriesToDeactivate = await _context.NpcLongTermMemories
          .Where(m => m.CharacterId == characterId)
          .OrderBy(m => m.Importance)
          .ThenBy(m => m.LastAccessedAt)
          .Take(memoryCount - maxMemories)
          .ToListAsync();

      foreach (var memory in memoriesToDeactivate)
      {
        memory.IsActive = false;
      }
      await _context.SaveChangesAsync();
    }
  }
}
