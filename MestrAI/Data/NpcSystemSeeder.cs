using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Models;

namespace RPGSessionManager.Data;

public static class NpcSystemSeeder
{
    public static async Task SeedNpcSystemDataAsync(ApplicationDbContext context)
    {
        // Criar estados iniciais para NPCs existentes que têm AiEnabled = true
        var existingNpcs = await context.CharacterSheets
            .Where(cs => cs.AiEnabled)
            .Include(cs => cs.Session)
            .ToListAsync();

        foreach (var npc in existingNpcs)
        {
            var existingState = await context.SessionAiCharacters
                .FirstOrDefaultAsync(sac => sac.SessionId == npc.SessionId && sac.AiCharacterId == npc.Id);

            if (existingState == null)
            {
                var defaultPersonality = new PersonalitySettings();
                
                var npcState = new SessionAiCharacter
                {
                    SessionId = npc.SessionId,
                    AiCharacterId = npc.Id,
                    IsActive = false, // Começar desativado por segurança
                    IsVisible = true, // Visível por padrão
                    PersonalitySettings = defaultPersonality.ToJson(),
                    InteractionFrequency = 50, // Frequência média
                    LastModifiedBy = "System",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.SessionAiCharacters.Add(npcState);
            }
        }

        await context.SaveChangesAsync();
    }
}

