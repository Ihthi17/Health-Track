using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    /// <summary>
    /// A workout the client ACTUALLY did (logged by the client).
    /// Trainers read these to compare against the prescribed plan.
    /// </summary>
    [Table("WorkoutLogs")]
    public class WorkoutLog
    {
        [Key]
        public int WorkoutLogID { get; set; }

        public int ClientID { get; set; }

        public int ExerciseID { get; set; }

        /// <summary>Optional link to the plan item this workout fulfils.</summary>
        public int? PlanItemID { get; set; }

        [Column(TypeName = "date")]
        public DateTime LogDate { get; set; }

        public int? Sets { get; set; }
        public int? Reps { get; set; }
        public int? DurationMin { get; set; }

        [Column(TypeName = "decimal(7,2)")]
        public decimal? CaloriesBurned { get; set; }

        [MaxLength(255)]
        public string? Notes { get; set; }

        // Navigation properties
        [ForeignKey(nameof(ClientID))]
        public ClientProfile? Client { get; set; }

        [ForeignKey(nameof(ExerciseID))]
        public Exercise? Exercise { get; set; }

        [ForeignKey(nameof(PlanItemID))]
        public WorkoutPlanItem? PlanItem { get; set; }
    }
}
