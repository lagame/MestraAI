using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RPGSessionManager.Data;
using RPGSessionManager.Models;
using System.Text.Json;

namespace RPGSessionManager.Services;

public class DevSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<DevSeeder> _logger;

    public DevSeeder(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<DevSeeder> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            await SeedRolesAsync();
            await SeedUsersAsync();
            await SeedSystemDefinitionsAsync();
            await SeedScenarioDefinitionsAsync();
            await SeedSessionsAsync();
            
            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during database seeding");
            throw;
        }
    }

    private async Task SeedRolesAsync()
    {
        var roles = new[] { "Admin", "Narrator", "User" };
        
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
                _logger.LogInformation("Created role: {Role}", role);
            }
        }
    }

    private async Task SeedUsersAsync()
    {
        // Admin user
        var admin = await _userManager.FindByEmailAsync("admin@rpg.local");
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = "admin@rpg.local",
                Email = "admin@rpg.local",
                DisplayName = "Administrator",
                EmailConfirmed = true
            };
            
            await _userManager.CreateAsync(admin, "Admin123!");
            await _userManager.AddToRoleAsync(admin, "Admin");
            _logger.LogInformation("Created admin user: admin@rpg.local / Admin123!");
        }

        // Narrator user
        var narrator = await _userManager.FindByEmailAsync("narrator@rpg.local");
        if (narrator == null)
        {
            narrator = new ApplicationUser
            {
                UserName = "narrator@rpg.local",
                Email = "narrator@rpg.local",
                DisplayName = "Mestre Gurgel",
                EmailConfirmed = true
            };
            
            await _userManager.CreateAsync(narrator, "Narrator123!");
            await _userManager.AddToRoleAsync(narrator, "Narrator");
            _logger.LogInformation("Created narrator user: narrator@rpg.local / Narrator123!");
        }

        // Player user
        var player = await _userManager.FindByEmailAsync("player@rpg.local");
        if (player == null)
        {
            player = new ApplicationUser
            {
                UserName = "player@rpg.local",
                Email = "player@rpg.local",
                DisplayName = "Jogador Ronassic",
                EmailConfirmed = true
            };
            
            await _userManager.CreateAsync(player, "Player123!");
            await _userManager.AddToRoleAsync(player, "User");
            _logger.LogInformation("Created player user: player@rpg.local / Player123!");
        }
    }

    private async Task SeedSystemDefinitionsAsync()
    {
        if (!await _context.SystemDefinitions.AnyAsync())
        {
            var dndSystem = new SystemDefinition
            {
                Name = "D&D 5e",
                JsonSchema = @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""name"": { ""type"": ""string"" },
                        ""level"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 20 },
                        ""class"": { ""type"": ""string"" },
                        ""race"": { ""type"": ""string"" },
                        ""abilities"": {
                            ""type"": ""object"",
                            ""properties"": {
                                ""STR"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 30 },
                                ""DEX"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 30 },
                                ""CON"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 30 },
                                ""INT"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 30 },
                                ""WIS"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 30 },
                                ""CHA"": { ""type"": ""integer"", ""minimum"": 1, ""maximum"": 30 }
                            },
                            ""required"": [""STR"", ""DEX"", ""CON"", ""INT"", ""WIS"", ""CHA""]
                        },
                        ""hp"": { ""type"": ""integer"", ""minimum"": 1 },
                        ""ac"": { ""type"": ""integer"", ""minimum"": 1 }
                    },
                    ""required"": [""name"", ""level"", ""class"", ""race"", ""abilities"", ""hp"", ""ac""]
                }"
            };
            
            _context.SystemDefinitions.Add(dndSystem);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created D&D 5e system definition");
        }
    }

    private async Task SeedScenarioDefinitionsAsync()
    {
        if (!await _context.ScenarioDefinitions.AnyAsync())
        {
            var forgottenRealms = new ScenarioDefinition
            {
                Name = "Forgotten Realms",
                World = "Faerûn",
                Notes = "Cenário clássico de D&D com cidades como Waterdeep, Baldur's Gate e Neverwinter."
            };
            
            _context.ScenarioDefinitions.Add(forgottenRealms);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created Forgotten Realms scenario definition");
        }
    }

    private async Task SeedSessionsAsync()
    {
        var narrator = await _userManager.FindByEmailAsync("narrator@rpg.local");
        var player = await _userManager.FindByEmailAsync("player@rpg.local");
        if (narrator == null || player == null) return;

        // 1) Garante a GameTabletop
        var tabletop = await _context.GameTabletops
            .FirstOrDefaultAsync(g => g.Name == "Mesa Principal");
        if (tabletop == null)
        {
            tabletop = new GameTabletop
            {
                Name = "Mesa Principal",
                Description = "Mesa de jogo principal para testes",
                SystemName = "D&D 5e",
                ScenarioName = "Forgotten Realms",
                NarratorId = narrator.Id,
                MaxPlayers = 4,
                IsPublic = false
            };
            _context.GameTabletops.Add(tabletop);
            await _context.SaveChangesAsync();
        }

        // 2) Garante a Session
        var session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.Name == "A Taverna do Javali Dourado" &&
                                      s.GameTabletopId == tabletop.Id);
        if (session == null)
        {
            var system = await _context.SystemDefinitions.FirstAsync();
            var scenario = await _context.ScenarioDefinitions.FirstAsync();

            session = new Session
            {
                Name = "A Taverna do Javali Dourado",
                SystemId = system.Id,
                ScenarioId = scenario.Id,
                GameTabletopId = tabletop.Id,
                NarratorId = narrator.Id,
                Participants = JsonSerializer.Serialize(new[] { player.Id })
            };
            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();
        }

        // 3) Garante as fichas (player + 2 NPCs IA)
        var celithData = new
        {
            name = "Celith",
            level = 1,
            @class = "Rogue",
            race = "Half-Elf",
            abilities = new { STR = 10, DEX = 16, CON = 14, INT = 12, WIS = 13, CHA = 8 },
            hp = 12,
            ac = 15
        };
        await EnsureCharacterAsync(session.Id, player.Id, "Celith", celithData, ai: false);

        var npc1Data = new
        {
            name = "Gareth, o Taverneiro",
            level = 1,
            @class = "Commoner",
            race = "Human",
            abilities = new { STR = 11, DEX = 10, CON = 12, INT = 10, WIS = 11, CHA = 10 },
            hp = 8,
            ac = 11
        };
        var gareth = await EnsureCharacterAsync(session.Id, narrator.Id, "Gareth, o Taverneiro", npc1Data, ai: true);

        var npc2Data = new
        {
            name = "Mira, a Barda",
            level = 2,
            @class = "Bard",
            race = "Half-Elf",
            abilities = new { STR = 9, DEX = 14, CON = 10, INT = 12, WIS = 11, CHA = 16 },
            hp = 14,
            ac = 12
        };
        var mira = await EnsureCharacterAsync(session.Id, narrator.Id, "Mira, a Barda", npc2Data, ai: true);

        // 4) Vincula NPCs IA na Session (se faltarem)
        await EnsureSessionAiCharacterAsync(session.Id, gareth.Id,
            new { Aggressiveness = 10, Friendliness = 50 }, 50);

        await EnsureSessionAiCharacterAsync(session.Id, mira.Id,
            new { Friendliness = 70, Creativity = 80, Humor = 50 }, 30);

        // 5) (Opcional) Memórias iniciais (se faltarem)
        await EnsureNpcMemoryAsync(session.Id, gareth.Id,
            "Gareth é dono da taverna e conhece todos os mercadores locais.", "taverna,negocios", 0.6);

        await EnsureNpcMemoryAsync(session.Id, mira.Id,
            "Mira toca alaúde e coleciona rumores da cidade.", "bardo,rumores,musica", 0.5);

        // 6) Garante o vínculo do personagem do jogador à sessão
        var alreadyLinked = await _context.SessionCharacters
            .AnyAsync(sc => sc.SessionId == session.Id && sc.CharacterSheetId ==
                            _context.CharacterSheets.Where(c => c.SessionId == session.Id && c.Name == "Celith")
                                .Select(c => c.Id).FirstOrDefault());
        if (!alreadyLinked)
        {
            var celithId = await _context.CharacterSheets
                .Where(c => c.SessionId == session.Id && c.Name == "Celith")
                .Select(c => c.Id)
                .FirstAsync();

            _context.SessionCharacters.Add(new SessionCharacter
            {
                SessionId = session.Id,
                CharacterSheetId = celithId,
                Role = "Player",
                IsActive = true,
                JoinedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Seed aditivo concluído para a sessão {SessionId}", session.Id);
    }

    // ---------- HELPERS ----------

    private async Task<CharacterSheet> EnsureCharacterAsync(
        int sessionId, string ownerUserId, string name, object data, bool ai)
    {
        var existing = await _context.CharacterSheets
            .FirstOrDefaultAsync(c => c.SessionId == sessionId && c.Name == name);

        if (existing != null) return existing;

        var cs = new CharacterSheet
        {
            SessionId = sessionId,
            PlayerId = ownerUserId,
            Name = name,
            DataJson = JsonSerializer.Serialize(data),
            AiEnabled = ai
        };
        _context.CharacterSheets.Add(cs);
        await _context.SaveChangesAsync();
        return cs;
    }

    private async Task EnsureSessionAiCharacterAsync(
        int sessionId, int aiCharacterId, object personality, int frequency)
    {
        var exists = await _context.SessionAiCharacters
            .AnyAsync(x => x.SessionId == sessionId && x.AiCharacterId == aiCharacterId);
        if (exists) return;

        _context.SessionAiCharacters.Add(new SessionAiCharacter
        {
            SessionId = sessionId,
            AiCharacterId = aiCharacterId,
            IsActive = true,
            IsVisible = true,
            PersonalitySettings = JsonSerializer.Serialize(personality),
            InteractionFrequency = frequency,
            LastModifiedBy = "System"
        });
        await _context.SaveChangesAsync();
    }

    private async Task EnsureNpcMemoryAsync(
        int sessionId, int characterId, string content, string tags, double importance)
    {
        var exists = await _context.NpcLongTermMemories
            .AnyAsync(m => m.SessionId == sessionId && m.CharacterId == characterId && m.Content == content);
        if (exists) return;

        _context.NpcLongTermMemories.Add(new NpcLongTermMemory
        {
            SessionId = sessionId,
            CharacterId = characterId,
            MemoryType = "fact",
            Content = content,
            Importance = importance,
            IsActive = true,
            Tags = tags,
            AccessCount = 0,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
    }

}

