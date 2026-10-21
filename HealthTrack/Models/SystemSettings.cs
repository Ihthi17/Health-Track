using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    [Table("SystemSettings")]
    public class SystemSettings
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SettingID { get; set; }

        // For views
        [NotMapped]
        public int SystemSettingsID => SettingID;

        [Required]
        [MaxLength(100)]
        public string SettingKey { get; set; } = null!;

        public string? SettingValue { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string DataType { get; set; } = "String"; // String, Int, Bool, DateTime

        // Backup-related settings
        [NotMapped]
        public bool AutoBackupEnabled { get; set; }

        [NotMapped]
        public TimeSpan? BackupTime { get; set; }

        [NotMapped]
        public string? BackupFrequency { get; set; } // Daily, Weekly, Monthly

        [NotMapped]
        public int? BackupDayOfWeek { get; set; }

        [NotMapped]
        public DateTime? LastAutoBackupAt { get; set; }

        [NotMapped]
        public int? BackupRetentionCount { get; set; }

        // System branding and settings
        [NotMapped]
        public string? SystemName { get; set; }

        [NotMapped]
        public string? SystemLogo { get; set; }

        [NotMapped]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Maintenance mode
        [NotMapped]
        public bool MaintenanceMode { get; set; }
    }
}
