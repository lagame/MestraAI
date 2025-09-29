using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGSessionManager.Models;

public class Session : ISoftDeletable
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime? ScheduledDate { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Planned;
    
    public int? SystemId { get; set; }
    public int? ScenarioId { get; set; }
    
    [Required]
    public int GameTabletopId { get; set; }
    
    [Required]
    public string NarratorId { get; set; } = string.Empty;
    
    [Column(TypeName = "TEXT")]
    public string Participants { get; set; } = "[]"; // JSON array of UserIds
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [ForeignKey(nameof(NarratorId))]
    public ApplicationUser Narrator { get; set; } = null!;
    
    [ForeignKey(nameof(SystemId))]
    public SystemDefinition? System { get; set; }
    
    [ForeignKey(nameof(ScenarioId))]
    public ScenarioDefinition? Scenario { get; set; }
    
    [ForeignKey(nameof(GameTabletopId))]
    public GameTabletop GameTabletop { get; set; } = null!;
    
    public ICollection<CharacterSheet> CharacterSheets { get; set; } = new List<CharacterSheet>();
    public ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
}

public enum SessionStatus
{
    Planned = 0,
    Active = 1,
    Completed = 2,
    Cancelled = 3
}

