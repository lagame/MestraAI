using System.ComponentModel.DataAnnotations;

namespace RPGSessionManager.Models;

public class BattleMap : ISoftDeletable
{
    public int Id { get; set; }
    
    [Required]
    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;
    
    public string? BackgroundUrl { get; set; }
    
    [Range(10, 200)]
    public int GridSize { get; set; } = 50; // Tamanho em pixels

    [Range(0.1, 0.9)]
    public float ZoomMin { get; set; } = 0.5f;
    
    [Range(1.1, 5.0)]
    public float ZoomMax { get; set; } = 2.0f;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MapToken> Tokens { get; set; } = new List<MapToken>();    
    
    [Display(Name = "Valor da Unidade do Grid")]
    public float GridUnitValue { get; set; } = 1.5f; // O valor numérico (ex: 1.5)

    [MaxLength(10)]
    [Display(Name = "Unidade do Grid")]
    public required string GridUnit { get; set; } = "m"; // A unidade (ex: "m" ou "ft")
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
}

