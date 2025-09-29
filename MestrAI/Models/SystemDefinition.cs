using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGSessionManager.Models;

public class SystemDefinition
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [Column(TypeName = "TEXT")]
    public string? JsonSchema { get; set; }
    
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}

