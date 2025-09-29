using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGSessionManager.Models;

public class ScenarioDefinition
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string World { get; set; } = string.Empty;
    
    [Column(TypeName = "TEXT")]
    public string Notes { get; set; } = string.Empty;
    
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}

