using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGSessionManager.Models;

public class CharacterSheet : ISoftDeletable
{
    public int Id { get; set; }
    
    [Required]
    public int SessionId { get; set; }
    
    [Required]
    public string PlayerId { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Column(TypeName = "TEXT")]
    public string DataJson { get; set; } = "{}";
    
    public bool AiEnabled { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    [ForeignKey(nameof(SessionId))]
    public Session Session { get; set; } = null!;
    
    [ForeignKey(nameof(PlayerId))]
    public ApplicationUser Player { get; set; } = null!;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
}

