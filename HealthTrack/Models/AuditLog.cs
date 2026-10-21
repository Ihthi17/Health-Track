using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    [Table("AuditLogs")]
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AuditID { get; set; }

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = null!;

        [MaxLength(500)]
        public string? Details { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? IPAddress { get; set; }

        public int? UserID { get; set; }

        // Ignored properties for display purposes
        [NotMapped]
        public string PerformedBy { get; set; } = "System";

        [NotMapped]
        public DateTime Timestamp { get; set; }
    }
}
