using System.ComponentModel.DataAnnotations;

namespace RPGSessionManager.Models
{
    public class SessionCharacter : ISoftDeletable
  {
        public int Id { get; set; }

        [Required]
        public int SessionId { get; set; }

        public Session Session { get; set; } = null!;

        [Required]
        public int CharacterSheetId { get; set; }

        public CharacterSheet CharacterSheet { get; set; } = null!;

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "PC"; // PC (Player Character), NPC (Non-Player Character)

        public bool IsActive { get; set; } = true;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LeftAt { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedByUserId { get; set; }
    }
}

