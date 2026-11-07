using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    /// <summary>
    /// Feedback written by a trainer for one of their clients.
    /// Written on the trainer side; shown on the client dashboard.
    /// </summary>
    [Table("TrainerFeedback")]
    public class TrainerFeedback
    {
        [Key]
        public int FeedbackID { get; set; }

        public int TrainerID { get; set; }

        public int ClientID { get; set; }

        [Required, MaxLength(1000)]
        public string Message { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey(nameof(TrainerID))]
        public TrainerProfile? Trainer { get; set; }

        [ForeignKey(nameof(ClientID))]
        public ClientProfile? Client { get; set; }
    }
}
