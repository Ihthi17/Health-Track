using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    [Table("BackupLogs")]
    public class BackupLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int BackupLogID { get; set; }

        [Required]
        [MaxLength(255)]
        public string BackupFileName { get; set; } = null!;

        [Required]
        public DateTime BackupDate { get; set; } = DateTime.UtcNow;

        public long? Size { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Success"; // Success, Failed, InProgress

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }

        [MaxLength(255)]
        public string? BackupPath { get; set; }

        [MaxLength(50)]
        public string BackupType { get; set; } = "Full"; // Full, Differential, Incremental

        public DateTime? RestoredDate { get; set; }
    }
}
