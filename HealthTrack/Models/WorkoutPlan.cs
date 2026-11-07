using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    /// <summary>
    /// A workout plan PRESCRIBED by a trainer for one client.
    /// Created by the trainer side; read-only on the client side.
    /// </summary>
    [Table("WorkoutPlans")]
    public class WorkoutPlan
    {
        [Key]
        public int WorkoutPlanID { get; set; }

        /// <summary>Trainer who created the plan (TrainerProfiles.TrainerID).</summary>
        public int TrainerID { get; set; }

        /// <summary>Client the plan belongs to (ClientProfiles.ClientID = the client's UserID).</summary>
        public int ClientID { get; set; }

        [Required, MaxLength(100)]
        public string PlanName { get; set; } = null!;

        [Column(TypeName = "date")]
        public DateTime StartDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime? EndDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey(nameof(TrainerID))]
        public TrainerProfile? Trainer { get; set; }

        [ForeignKey(nameof(ClientID))]
        public ClientProfile? Client { get; set; }

        /// <summary>The individual exercises (per day) that make up this plan.</summary>
        public List<WorkoutPlanItem> Items { get; set; } = new();
    }
}
