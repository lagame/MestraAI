using System.ComponentModel.DataAnnotations;

namespace RPGSessionManager.Models
{
    public class TabletopMember
    {
        public int Id { get; set; }

        [Required]
        public int GameTabletopId { get; set; }

        public GameTabletop GameTabletop { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        public TabletopRole Role { get; set; } = TabletopRole.Player;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        public DateTime? LeftAt { get; set; }
    }

    public enum TabletopRole
    {
        Player = 0,
        Narrator = 1,
        Admin = 2
    }
}

