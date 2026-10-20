using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace HealthTrack.Models
{
    public class SystemSettings
    {
        [Key]
        public int SystemSettingsID { get; set; }

        [MaxLength(100)]
        public string? SystemName { get; set; }

        [MaxLength(500)]
        public string? SystemLogo { get; set; }

        public bool MaintenanceMode { get; set; } = false;

        // Backup Scheduling (Feature A)
        public bool AutoBackupEnabled { get; set; } = false;
        
        public TimeSpan? BackupTime { get; set; } = new TimeSpan(2, 0, 0); // Default 2:00 AM
        
        [MaxLength(50)]
        public string BackupFrequency { get; set; } = "Daily"; // Daily, Weekly, Monthly
        
        public int? BackupDayOfWeek { get; set; } = null; // 0=Sunday, 6=Saturday (for Weekly)
        
        public int BackupRetentionCount { get; set; } = 10; // Keep last N backups
        
        public DateTime? LastAutoBackupAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Not mapped - used only for file upload form
        [NotMapped]
        public IFormFile? LogoFile { get; set; }
    }

    public class AuditLog
    {
        [Key]
        public int AuditID { get; set; }

        public int? UserID { get; set; }

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } = null!;

        [MaxLength(500)]
        public string? Details { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? IPAddress { get; set; }

        // For compatibility with views expecting PerformedBy and Timestamp
        [NotMapped]
        public string PerformedBy => "Admin";

        [NotMapped]
        public DateTime Timestamp => CreatedAt;
    }

    public class Permission
    {
        [Key]
        public int PermissionID { get; set; }

        [Required]
        [MaxLength(100)]
        public string PermissionName { get; set; } = null!;

        [MaxLength(200)]
        public string? Description { get; set; }

        [MaxLength(100)]
        public string? Module { get; set; } // e.g., "Dashboard", "Appointments", "Backups", "Users", "Reports"

        // Navigation property
        [InverseProperty("Permissions")]
        public ICollection<Role> Roles { get; set; } = new List<Role>();
    }

    public class RolePermission
    {
        public int PermissionID { get; set; }
        public int RoleID { get; set; }
    }

    public class EmailSettings
    {
        [Key]
        public int EmailSettingsID { get; set; }

        [Required]
        [MaxLength(200)]
        public string SMTPServer { get; set; } = null!;

        [Required]
        public int SMTPPort { get; set; } = 587;

        [MaxLength(200)]
        public string? SMTPUsername { get; set; }

        [MaxLength(200)]
        public string? SMTPPassword { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(200)]
        public string FromEmail { get; set; } = null!;

        [MaxLength(200)]
        public string? FromName { get; set; }

        public bool EnableSSL { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class BackupLog
    {
        [Key]
        public int BackupLogID { get; set; }

        [Required]
        [MaxLength(200)]
        public string BackupFileName { get; set; } = null!;

        [MaxLength(500)]
        public string? BackupPath { get; set; }

        public DateTime BackupDate { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string BackupType { get; set; } = "Manual"; // Manual, Scheduled

        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Success, Failed

        public long Size { get; set; }

        public DateTime? RestoredDate { get; set; }

        [MaxLength(500)]
        public string? ErrorMessage { get; set; }
    }

    public class Appointment
    {
        [Key]
        public int AppointmentID { get; set; }

        [Required]
        public int ClientID { get; set; }

        [Required]
        public int TrainerID { get; set; } = 0; // Required by DB, default 0 if not assigned

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Completed, Cancelled

        [MaxLength(500)]
        public string? Reason { get; set; }

        [MaxLength(500)]
        public string? TrainerNotes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey(nameof(ClientID))]
        public ClientProfile? Client { get; set; }

        [ForeignKey(nameof(TrainerID))]
        public TrainerProfile? Trainer { get; set; }
    }

    public class Notification
    {
        [Key]
        public int NotificationID { get; set; }

        [Required]
        public int UserID { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = null!; // Registration, Appointment, Update, System

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = null!;

        [MaxLength(500)]
        public string? Message { get; set; }

        public DateTime? RemindAt { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey(nameof(UserID))]
        public User? User { get; set; }
    }
}
