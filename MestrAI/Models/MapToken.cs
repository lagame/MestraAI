using System.ComponentModel.DataAnnotations;

namespace RPGSessionManager.Models;

public class MapToken : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public int BattleMapId { get; set; }
    public BattleMap BattleMap { get; set; } = null!;

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = "";
    
    [StringLength(500)]
    public string? ImageUrl { get; set; }
    
    [Required]
    public string OwnerId { get; set; } = ""; // ApplicationUser.Id
    public ApplicationUser Owner { get; set; } = null!;

    public float X { get; set; }
    public float Y { get; set; }
    
    [Range(0.1, 5.0)]
    public float Scale { get; set; } = 1f;
    
    [Range(0, 360)]
    public float Rotation { get; set; } = 0f;
    
    public bool IsVisible { get; set; } = true;
    
    public int Z { get; set; } = 0;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
}

