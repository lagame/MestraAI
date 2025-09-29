using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGSessionManager.Models
{ 
  public class SessionAiCharacter : ISoftDeletable
  {
      public int Id { get; set; }
    
      [Required]
      public int SessionId { get; set; }
    
      [Required]
      public int AiCharacterId { get; set; }
    
      public bool IsActive { get; set; } = false;
      public bool IsVisible { get; set; } = true;
    
      // Campos para auditoria
      public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
      public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
      public string? LastModifiedBy { get; set; }
    
      // Configurações avançadas de IA
      public string? PersonalitySettings { get; set; } = "{}"; // JSON com configurações
      public int InteractionFrequency { get; set; } = 50; // 0-100, chance de responder
    
      // Navigation properties
      [ForeignKey(nameof(SessionId))]
      public Session Session { get; set; } = null!;
    
      [ForeignKey(nameof(AiCharacterId))]
      public CharacterSheet AiCharacter { get; set; } = null!;
      public bool IsDeleted { get; set; } = false;
      public DateTime? DeletedAt { get; set; }
      public string? DeletedByUserId { get; set; }
  }

}
