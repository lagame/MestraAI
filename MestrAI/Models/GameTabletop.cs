using System.ComponentModel.DataAnnotations;

namespace RPGSessionManager.Models
{
    public class GameTabletop
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        [StringLength(100)]
        public string SystemName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ScenarioName { get; set; } = string.Empty;

        public int MaxPlayers { get; set; } = 4;

        public bool IsPublic { get; set; } = false;

        public bool AllowSpectators { get; set; } = false;

        [Required]
        public string NarratorId { get; set; } = string.Empty;

        public ApplicationUser Narrator { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        // Navigation properties
        public ICollection<TabletopMember> Members { get; set; } = new List<TabletopMember>();
        public ICollection<Session> Sessions { get; set; } = new List<Session>();
    }
}

