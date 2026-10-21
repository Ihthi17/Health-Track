using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    [Table("Notifications")]
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NotificationID { get; set; }

        [Required]
        [ForeignKey(nameof(User))]
        public int UserID { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = null!; // Appointment, System, Alert, etc.

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = null!;

        [MaxLength(500)]
        public string? Message { get; set; }

        public DateTime? RemindAt { get; set; }

        [Required]
        public bool IsRead { get; set; } = false;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public User? User { get; set; }
    }
}
