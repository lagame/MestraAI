using System.ComponentModel.DataAnnotations;
using RPGSessionManager.Models;

namespace RPGSessionManager.Dtos;

public class SetNpcStatusRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "SessionId deve ser maior que 0")]
    public int SessionId { get; set; }
    
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "CharacterId deve ser maior que 0")]
    public int CharacterId { get; set; }
    
    public bool? IsActive { get; set; }
    public bool? IsVisible { get; set; }
    
    [StringLength(2000, ErrorMessage = "PersonalitySettings não pode exceder 2000 caracteres")]
    public string? PersonalitySettings { get; set; }
    
    [Range(0, 100, ErrorMessage = "InteractionFrequency deve estar entre 0 e 100")]
    public int? InteractionFrequency { get; set; }
    
    [StringLength(500, ErrorMessage = "Reason não pode exceder 500 caracteres")]
    public string? Reason { get; set; }
}

public class NpcStatusResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public NpcStateDto? Data { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class NpcStateDto
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int CharacterId { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public int InteractionFrequency { get; set; }
    public PersonalitySettings? PersonalitySettings { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? LastModifiedBy { get; set; }
}

public class SessionNpcsResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<SessionNpcDto> Npcs { get; set; } = new();
    public int TotalCount { get; set; }
}

public class SessionNpcDto
{
    public int CharacterId { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public bool AiEnabled { get; set; }
    public NpcStateDto? State { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NpcMetricsDto
{    
    public int TotalNpcs { get; set; }
    public int ActiveNpcs { get; set; }
    public int VisibleNpcs { get; set; }    
    public int InteractionsLastHour { get; set; }
    public double AverageResponseTime { get; set; }
    public DateTime LastUpdate { get; set; }

    // Propriedades para métricas de um NPC específico
    public int TotalInteractions { get; set; }
    public double AverageResponseTimeMs { get; set; }

    // Propriedades para métricas de uma sessão inteira
    public int SessionId { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public double P95ResponseTimeMs { get; set; }
}

public class AuditLogDto
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int CharacterId { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public string PropertyChanged { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? Reason { get; set; }
    public string? ChangeSource { get; set; }
}

