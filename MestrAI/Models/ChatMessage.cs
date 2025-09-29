using System.ComponentModel.DataAnnotations;

namespace RPGSessionManager.Models;

public class ChatMessage: ISoftDeletable
{
    public int Id { get; set; }
    
    /// <summary>
    /// ULID único para ordenação e deduplicação
    /// </summary>
    [Required]
    [MaxLength(26)]
    public string MessageId { get; set; } = string.Empty;

    [Required]
    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;

    [Required]
    [MaxLength(2000)] // Aumentado para permitir mensagens mais longas
    public string Content { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string SenderName { get; set; } = string.Empty;

    [Required]
    public MessageType Type { get; set; } = MessageType.PlayerMessage;
    
    /// <summary>
    /// Tipo do remetente (User, Character, System, AI)
    /// </summary>
    [Required]
    public SenderType SenderType { get; set; } = SenderType.User;

    public string? SenderUserId { get; set; }
    public ApplicationUser? SenderUser { get; set; }

    public int? CharacterId { get; set; }
    public CharacterSheet? Character { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // For AI-generated messages
    public bool IsAiGenerated { get; set; } = false;
    public string? AiPromptUsed { get; set; }
    
    /// <summary>
    /// Metadados adicionais em JSON (opcional)
    /// </summary>
    public string? Metadata { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
}

public enum SenderType
{
    User = 0,
    Character = 1,
    System = 2,
    AI = 3
}

public enum MessageType
{
    PlayerMessage = 0,
    CharacterAction = 1,
    NarratorDescription = 2,
    SystemMessage = 3,
    DiceRoll = 4,
    AiCharacterResponse = 5
}

