using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGSessionManager.Models;

public class NpcLongTermMemory : ISoftDeletable
{
  public int Id { get; set; }

  [Required]
  public int CharacterId { get; set; }

  [Required]
  public int SessionId { get; set; }

  [Required]
  [MaxLength(50)]
  public string MemoryType { get; set; } = string.Empty; // "personality_trait", "relationship", "event", "preference"

  [Required]
  public string Content { get; set; } = string.Empty;

  [Range(1, 10)]
  public double Importance { get; set; } = 5.0; // 1-10, usado para priorização

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
  public int AccessCount { get; set; } = 0;
  public bool IsActive { get; set; } = true;

  // Metadados opcionais
  public string? Tags { get; set; } // JSON array de tags para busca
  public string? RelatedEntities { get; set; } // JSON com IDs de entidades relacionadas

  // Navigation properties
  [ForeignKey(nameof(CharacterId))]
  public CharacterSheet Character { get; set; } = null!;

  [ForeignKey(nameof(SessionId))]
  public Session Session { get; set; } = null!;
  public bool IsDeleted { get; set; } = false;
  public DateTime? DeletedAt { get; set; }
  public string? DeletedByUserId { get; set; }
}
