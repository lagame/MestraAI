using System.ComponentModel.DataAnnotations;

namespace RPGSessionManager.Models;

public class AiNpcStateChange
{
    public int Id { get; set; }
    
    [Required]
    public int SessionId { get; set; }
    
    [Required]
    public int CharacterId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string PropertyChanged { get; set; } = string.Empty; // "IsActive", "IsVisible", "PersonalitySettings"
    
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    
    [Required]
    [MaxLength(450)]
    public string ChangedBy { get; set; } = string.Empty;
    
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(500)]
    public string? Reason { get; set; }
    
    [MaxLength(100)]
    public string? ChangeSource { get; set; } // "Manual", "Automatic", "System"
  public DateTime Timestamp { get; set; }
  public string? Description { get; set; }
  public string? ChangeType { get; set; }
}

