using System.ComponentModel.DataAnnotations;

namespace RPGSessionManager.Models;

/// <summary>
/// Configurações de IA para o sistema
/// </summary>
public class AiSettings
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Provedor de IA selecionado (Gemini ou Local)
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Provider { get; set; } = "Gemini";
    
    /// <summary>
    /// ID do projeto Google Cloud para Gemini
    /// </summary>
    [StringLength(200)]
    public string? GeminiProject { get; set; }

    /// <summary>
    /// Localização do Vertex AI (ex: southamerica-east1)
    /// </summary>
    [StringLength(100)]
    public string? GeminiLocation { get; set; }
    
    /// <summary>
    /// Modelo Gemini a ser usado (ex: gemini-2.5-flash-lite)
    /// </summary>
    [StringLength(100)]
    public string? GeminiModel { get; set; }
    
    /// <summary>
    /// Endpoint do LLM local
    /// </summary>
    [StringLength(500)]
    public string? LocalEndpoint { get; set; }
    
    /// <summary>
    /// Chave de API para o LLM local (opcional)
    /// </summary>
    [StringLength(500)]
    public string? LocalApiKey { get; set; }
    
    /// <summary>
    /// Data de criação
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Data da última atualização
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Indica se as configurações estão ativas
    /// </summary>
    public bool IsActive { get; set; } = true;
}

