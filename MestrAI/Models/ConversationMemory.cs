using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGSessionManager.Models;

/// <summary>
/// Representa uma entrada de memória de conversa para personagens AI em uma Mesa
/// </summary>
public class ConversationMemory
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// ID da Mesa (GameTabletop) à qual esta memória pertence
    /// </summary>
    [Required]
    public int GameTabletopId { get; set; }

    /// <summary>
    /// Referência à Mesa
    /// </summary>
    [ForeignKey(nameof(GameTabletopId))]
    public virtual GameTabletop GameTabletop { get; set; } = null!;

    /// <summary>
    /// ID da Sessão onde a conversa ocorreu (para rastreamento)
    /// </summary>
    public int? SessionId { get; set; }

    /// <summary>
    /// Referência à Sessão (opcional, para histórico)
    /// </summary>
    [ForeignKey(nameof(SessionId))]
    public virtual Session? Session { get; set; }

    /// <summary>
    /// Nome do personagem que falou (pode ser jogador ou NPC)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string SpeakerName { get; set; } = string.Empty;

    /// <summary>
    /// Tipo do falante (Player, NPC, Narrator, System)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string SpeakerType { get; set; } = string.Empty;

    /// <summary>
    /// Conteúdo da mensagem/conversa
    /// </summary>
    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Contexto adicional da conversa (situação, local, etc.)
    /// </summary>
    [MaxLength(500)]
    public string? Context { get; set; }

    /// <summary>
    /// Importância da memória (1-10, sendo 10 mais importante)
    /// </summary>
    [Range(1, 10)]
    public int Importance { get; set; } = 5;

    /// <summary>
    /// Embedding vetorial da conversa (para busca semântica)
    /// Armazenado como JSON array de floats
    /// </summary>
    [MaxLength(8000)]
    public string? EmbeddingVector { get; set; }

    /// <summary>
    /// Hash do conteúdo para evitar duplicatas
    /// </summary>
    [MaxLength(64)]
    public string? ContentHash { get; set; }

    /// <summary>
    /// Data e hora quando a conversa ocorreu
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Data da última atualização
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Indica se esta memória está ativa (não foi arquivada)
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Tags para categorização da memória
    /// </summary>
    [MaxLength(200)]
    public string? Tags { get; set; }

    /// <summary>
    /// Metadados adicionais em formato JSON
    /// </summary>
    [MaxLength(1000)]
    public string? Metadata { get; set; }

    // ============================
    // Campos não mapeados (transientes)
    // ============================

    /// <summary>
    /// Score de ranking/relevância retornado pela busca semântica.
    /// Não é persistido no banco.
    /// </summary>
    [NotMapped]
    public float? Score { get; set; }

    /// <summary>
    /// Proxy para compatibilidade: usa CreatedAt como "Timestamp".
    /// Não é persistido separadamente. Escrever aqui atualiza CreatedAt.
    /// </summary>
    [NotMapped]
    public DateTime Timestamp
    {
        get => CreatedAt;
        set => CreatedAt = value;
    }
}
