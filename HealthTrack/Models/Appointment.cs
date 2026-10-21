using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    [Table("Appointments")]
    public class Appointment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AppointmentID { get; set; }

        [Required]
        [ForeignKey(nameof(Client))]
        public int ClientID { get; set; }

        [Required]
        [ForeignKey(nameof(Trainer))]
        public int TrainerID { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        [MaxLength(500)]
        public string? TrainerNotes { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Completed, Cancelled

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey(nameof(ClientID))]
        public ClientProfile? Client { get; set; }

        [ForeignKey(nameof(TrainerID))]
        public TrainerProfile? Trainer { get; set; }
    }
}
