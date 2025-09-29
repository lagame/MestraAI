using System.Globalization;
using System.Linq.Expressions;
using System.Reflection.Emit;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RPGSessionManager.Models;
using RPGSessionManager.Services;
namespace RPGSessionManager.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
  private readonly IUserResolverService _userResolverService;
  public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IUserResolverService userResolverService)
        : base(options)
    {
      _userResolverService = userResolverService;
    }
    
    public DbSet<Session> Sessions { get; set; }
    public DbSet<CharacterSheet> CharacterSheets { get; set; }
    public DbSet<SystemDefinition> SystemDefinitions { get; set; }
    public DbSet<ScenarioDefinition> ScenarioDefinitions { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<GameTabletop> GameTabletops { get; set; }
    public DbSet<TabletopMember> TabletopMembers { get; set; }
    public DbSet<SessionCharacter> SessionCharacters { get; set; }
    public DbSet<BattleMap> BattleMaps { get; set; }
    public DbSet<MapToken> MapTokens { get; set; }
    public DbSet<ConversationMemory> ConversationMemories { get; set; }
    public DbSet<AiSettings> AiSettings { get; set; }
    public DbSet<Media> Media { get; set; }
    public DbSet<SessionAiCharacter> SessionAiCharacters { get; set; }
    public DbSet<AiNpcStateChange> AiNpcStateChanges { get; set; }
    public DbSet<NpcLongTermMemory> NpcLongTermMemories { get; set; }
    public DbSet<NpcInteraction> NpcInteractions { get; set; }

  // ATUALIZE O MÉTODO OnBeforeSaving
  private void OnBeforeSaving()
    {
      var userId = _userResolverService.GetUserId(); // Pega o ID do usuário atual

      foreach (var entry in ChangeTracker.Entries<ISoftDeletable>())
      {
        if (entry.State == EntityState.Deleted)
        {
          entry.State = EntityState.Modified;
          entry.Entity.IsDeleted = true;
          entry.Entity.DeletedAt = DateTime.UtcNow;
          entry.Entity.DeletedByUserId = userId; 
        }
      }
    }
    // Adicione estes métodos à sua classe ApplicationDbContext
    public override int SaveChanges()
    {
      OnBeforeSaving();
      return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      OnBeforeSaving();
      return base.SaveChangesAsync(cancellationToken);
    }

  private static DateTime ParseIsoOrLegacy(string v)
  {
    if (string.IsNullOrWhiteSpace(v))
      return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

    // ISO-8601 (round-trip)
    if (DateTime.TryParseExact(
            v,
            new[] { "O", "yyyy-MM-ddTHH:mm:ss.FFFFFFFK" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var iso))
      return iso;

    // Legado pt-BR "dd/MM/yyyy HH:mm:ss"
    if (DateTime.TryParse(
            v,
            new CultureInfo("pt-BR"),
            DateTimeStyles.AssumeLocal,
            out var br))
      return DateTime.SpecifyKind(br, DateTimeKind.Local).ToUniversalTime();

    // Último recurso
    return DateTime.SpecifyKind(DateTime.Parse(v, CultureInfo.InvariantCulture), DateTimeKind.Utc);
  }

  protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

    if (Database.IsSqlite())
    {
      // 🔁 DateTime <-> string (ISO-8601 UTC)
      var isoDateTimeConverter = new ValueConverter<DateTime, string>(
          v => (v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime())
                  .ToString("O", CultureInfo.InvariantCulture),
          v => ParseIsoOrLegacy(v)
      );

      builder.Entity<AiSettings>(b =>
      {
        b.Property(x => x.CreatedAt).HasConversion(isoDateTimeConverter);
        b.Property(x => x.UpdatedAt).HasConversion(isoDateTimeConverter);
      });
    }

    // Dentro de OnModelCreating em ApplicationDbContext.cs

    // Configuração do Filtro Global para Soft Delete
    foreach (var entityType in builder.Model.GetEntityTypes())
        {
          if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
          {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeletedProperty = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var filter = Expression.Lambda(
                Expression.Equal(isDeletedProperty, Expression.Constant(false)),
                parameter
            );
            entityType.SetQueryFilter(filter);
          }
        }

    // Configure relationships with Restrict delete behavior
    builder.Entity<Session>()
            .HasOne(s => s.Narrator)
            .WithMany()
            .HasForeignKey(s => s.NarratorId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.Entity<Session>()
            .HasOne(s => s.System)
            .WithMany(sys => sys.Sessions)
            .HasForeignKey(s => s.SystemId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.Entity<Session>()
            .HasOne(s => s.Scenario)
            .WithMany(sc => sc.Sessions)
            .HasForeignKey(s => s.ScenarioId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.Entity<Session>()
            .HasOne(s => s.GameTabletop)
            .WithMany(gt => gt.Sessions)
            .HasForeignKey(s => s.GameTabletopId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.Entity<CharacterSheet>()
            .HasOne(cs => cs.Session)
            .WithMany(s => s.CharacterSheets)
            .HasForeignKey(cs => cs.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.Entity<CharacterSheet>()
            .HasOne(cs => cs.Player)
            .WithMany()
            .HasForeignKey(cs => cs.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.Entity<ChatMessage>()
            .HasOne(cm => cm.Session)
            .WithMany(s => s.ChatMessages)
            .HasForeignKey(cm => cm.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.Entity<ChatMessage>()
            .HasOne(cm => cm.SenderUser)
            .WithMany()
            .HasForeignKey(cm => cm.SenderUserId)
            .OnDelete(DeleteBehavior.SetNull);
            
        builder.Entity<ChatMessage>()
            .HasOne(cm => cm.Character)
            .WithMany()
            .HasForeignKey(cm => cm.CharacterId)
            .OnDelete(DeleteBehavior.SetNull);
            
        // GameTabletop relationships
        builder.Entity<GameTabletop>()
            .HasOne(gt => gt.Narrator)
            .WithMany()
            .HasForeignKey(gt => gt.NarratorId)
            .OnDelete(DeleteBehavior.Restrict);
            
        // TabletopMember relationships
        builder.Entity<TabletopMember>()
            .HasOne(tm => tm.GameTabletop)
            .WithMany(gt => gt.Members)
            .HasForeignKey(tm => tm.GameTabletopId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.Entity<TabletopMember>()
            .HasOne(tm => tm.User)
            .WithMany()
            .HasForeignKey(tm => tm.UserId)
            .OnDelete(DeleteBehavior.Restrict);
            
        // SessionCharacter relationships
        builder.Entity<SessionCharacter>()
            .HasOne(sc => sc.Session)
            .WithMany()
            .HasForeignKey(sc => sc.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.Entity<SessionCharacter>()
            .HasOne(sc => sc.CharacterSheet)
            .WithMany()
            .HasForeignKey(sc => sc.CharacterSheetId)
            .OnDelete(DeleteBehavior.Restrict);
            
        // Create indexes
        builder.Entity<Session>()
            .HasIndex(s => s.NarratorId);
            
        builder.Entity<CharacterSheet>()
            .HasIndex(cs => cs.SessionId);
            
        builder.Entity<CharacterSheet>()
            .HasIndex(cs => cs.PlayerId);
            
        builder.Entity<ChatMessage>()
            .HasIndex(cm => cm.SessionId);
            
        builder.Entity<ChatMessage>()
            .HasIndex(cm => cm.CreatedAt);
            
        // GameTabletop indexes
        builder.Entity<GameTabletop>()
            .HasIndex(gt => gt.NarratorId);
            
        builder.Entity<GameTabletop>()
            .HasIndex(gt => gt.IsDeleted);
            
        // TabletopMember indexes
        builder.Entity<TabletopMember>()
            .HasIndex(tm => tm.GameTabletopId);
            
        builder.Entity<TabletopMember>()
            .HasIndex(tm => tm.UserId);
            
        builder.Entity<TabletopMember>()
            .HasIndex(tm => new { tm.GameTabletopId, tm.UserId })
            .IsUnique()
            .HasFilter("IsActive = 1");
            
        // SessionCharacter indexes
        builder.Entity<SessionCharacter>()
            .HasIndex(sc => sc.SessionId);
            
        builder.Entity<SessionCharacter>()
            .HasIndex(sc => sc.CharacterSheetId);
            
        builder.Entity<SessionCharacter>()
            .HasIndex(sc => new { sc.SessionId, sc.CharacterSheetId })
            .IsUnique()
            .HasFilter("IsActive = 1");
            
        // BattleMap relationships
        builder.Entity<BattleMap>()
            .HasOne(bm => bm.Session)
            .WithOne()
            .HasForeignKey<BattleMap>(bm => bm.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // MapToken relationships
        builder.Entity<MapToken>()
            .HasOne(mt => mt.BattleMap)
            .WithMany(bm => bm.Tokens)
            .HasForeignKey(mt => mt.BattleMapId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.Entity<MapToken>()
            .HasOne(mt => mt.Owner)
            .WithMany()
            .HasForeignKey(mt => mt.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
            
        // BattleMap indexes
        builder.Entity<BattleMap>()
            .HasIndex(bm => bm.SessionId)
            .IsUnique();
            
        // MapToken indexes
        builder.Entity<MapToken>()
            .HasIndex(mt => mt.BattleMapId);
            
        builder.Entity<MapToken>()
            .HasIndex(mt => mt.OwnerId);
            
        // ConversationMemory relationships
        builder.Entity<ConversationMemory>()
            .HasOne(cm => cm.GameTabletop)
            .WithMany()
            .HasForeignKey(cm => cm.GameTabletopId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.Entity<ConversationMemory>()
            .HasOne(cm => cm.Session)
            .WithMany()
            .HasForeignKey(cm => cm.SessionId)
            .OnDelete(DeleteBehavior.SetNull);
            
        // ConversationMemory indexes
        builder.Entity<ConversationMemory>()
            .HasIndex(cm => cm.GameTabletopId);
            
        builder.Entity<ConversationMemory>()
            .HasIndex(cm => cm.SessionId);
            
        builder.Entity<ConversationMemory>()
            .HasIndex(cm => cm.CreatedAt);
            
        builder.Entity<ConversationMemory>()
            .HasIndex(cm => cm.SpeakerName);
            
        builder.Entity<ConversationMemory>()
            .HasIndex(cm => cm.SpeakerType);
            
        builder.Entity<ConversationMemory>()
            .HasIndex(cm => cm.Importance);
            
        builder.Entity<ConversationMemory>()
            .HasIndex(cm => cm.IsActive);
            
        builder.Entity<ConversationMemory>()
            .HasIndex(cm => cm.ContentHash);
            
        builder.Entity<ConversationMemory>()
            .HasIndex(cm => new { cm.GameTabletopId, cm.IsActive, cm.Importance });
            
        // AiSettings configuration
        builder.Entity<AiSettings>()
            .HasIndex(ai => ai.IsActive);

        builder.Entity<Media>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.OriginalFileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.MediaType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FileSize).IsRequired();
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(512);
            entity.Property(e => e.SessionId).IsRequired();
            entity.Property(e => e.UploadedBy).IsRequired().HasMaxLength(450);
            entity.Property(e => e.UploadedAt).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Metadata);

            entity.HasOne(d => d.Session)
                  .WithMany()
                  .HasForeignKey(d => d.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // SessionAiCharacter configuration
        builder.Entity<SessionAiCharacter>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Índice único para evitar duplicatas
            entity.HasIndex(e => new { e.SessionId, e.AiCharacterId })
                  .IsUnique()
                  .HasDatabaseName("IX_SessionAiCharacter_Session_Character");
            
            // Índices para performance
            entity.HasIndex(e => e.SessionId)
                  .HasDatabaseName("IX_SessionAiCharacter_SessionId");
            
            entity.HasIndex(e => e.AiCharacterId)
                  .HasDatabaseName("IX_SessionAiCharacter_CharacterId");
            
            entity.HasIndex(e => new { e.SessionId, e.IsActive, e.IsVisible })
                  .HasDatabaseName("IX_SessionAiCharacter_Session_Active_Visible");
            
            // Relacionamentos
            entity.HasOne(e => e.Session)
                  .WithMany()
                  .HasForeignKey(e => e.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.AiCharacter)
                  .WithMany()
                  .HasForeignKey(e => e.AiCharacterId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Configurações de campo
            entity.Property(e => e.PersonalitySettings)
                  .HasColumnType("TEXT");
            
            entity.Property(e => e.LastModifiedBy)
                  .HasMaxLength(450);
        });

        // AiNpcStateChange configuration
        builder.Entity<AiNpcStateChange>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Índices para consultas de auditoria
            entity.HasIndex(e => e.SessionId)
                  .HasDatabaseName("IX_AiNpcStateChange_SessionId");
            
            entity.HasIndex(e => e.CharacterId)
                  .HasDatabaseName("IX_AiNpcStateChange_CharacterId");
            
            entity.HasIndex(e => e.ChangedAt)
                  .HasDatabaseName("IX_AiNpcStateChange_ChangedAt");
            
            entity.HasIndex(e => new { e.SessionId, e.CharacterId, e.ChangedAt })
                  .HasDatabaseName("IX_AiNpcStateChange_Session_Character_Date");
            
            entity.HasIndex(e => e.ChangedBy)
                  .HasDatabaseName("IX_AiNpcStateChange_ChangedBy");
            
            // Configurações de campo
            entity.Property(e => e.OldValue)
                  .HasColumnType("TEXT");
            
            entity.Property(e => e.NewValue)
                  .HasColumnType("TEXT");
        });

        // NpcLongTermMemory configuration
        builder.Entity<NpcLongTermMemory>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Índices para performance de consultas
            entity.HasIndex(e => e.CharacterId)
                  .HasDatabaseName("IX_NpcLongTermMemory_CharacterId");
            
            entity.HasIndex(e => e.SessionId)
                  .HasDatabaseName("IX_NpcLongTermMemory_SessionId");
            
            entity.HasIndex(e => new { e.CharacterId, e.MemoryType, e.IsActive })
                  .HasDatabaseName("IX_NpcLongTermMemory_Character_Type_Active");
            
            entity.HasIndex(e => new { e.CharacterId, e.Importance, e.IsActive })
                  .HasDatabaseName("IX_NpcLongTermMemory_Character_Importance_Active");
            
            entity.HasIndex(e => e.LastAccessedAt)
                  .HasDatabaseName("IX_NpcLongTermMemory_LastAccessed");
            
            // Relacionamentos
            entity.HasOne(e => e.Character)
                  .WithMany()
                  .HasForeignKey(e => e.CharacterId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Session)
                  .WithMany()
                  .HasForeignKey(e => e.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Configurações de campo
            entity.Property(e => e.Content)
                  .HasColumnType("TEXT")
                  .IsRequired();
            
            entity.Property(e => e.Tags)
                  .HasColumnType("TEXT");
            
            entity.Property(e => e.RelatedEntities)
                  .HasColumnType("TEXT");
        });
    }
}
