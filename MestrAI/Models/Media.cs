using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RPGSessionManager.Models
{
    public class Media : ISoftDeletable
    {
        [Key]
        public int Id { get; set; }

        [Required, StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required, StringLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string MediaType { get; set; } = string.Empty;

        // [Required] em tipos-valor é redundante, mas pode ficar
        [Required]
        public long FileSize { get; set; }

        [Required, StringLength(512)]
        public string FilePath { get; set; } = string.Empty;

        [Required]
        public int SessionId { get; set; }

        [Required, StringLength(450)]
        public string UploadedBy { get; set; } = string.Empty;

        [Required]
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        [StringLength(1000)]
        public string? Description { get; set; }

        public string? Metadata { get; set; }

        // Navegação: o EF vai popular; usamos default! para silenciar o nullability
        [ForeignKey(nameof(SessionId))]
        public virtual Session Session { get; set; } = default!;

        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public string? DeletedByUserId { get; set; }
    }
}
