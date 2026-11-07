using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    /// <summary>
    /// One exercise inside a workout plan, scheduled for a day of the week,
    /// e.g. "Monday - Squats - 3 sets x 12 reps - reminder at 07:00".
    /// </summary>
    [Table("WorkoutPlanItems")]
    public class WorkoutPlanItem
    {
        [Key]
        public int PlanItemID { get; set; }

        public int WorkoutPlanID { get; set; }

        public int ExerciseID { get; set; }

        /// <summary>Day of the week: 0 = Sunday ... 6 = Saturday (same as .NET DayOfWeek).</summary>
        public byte DayOfWeek { get; set; }

        public int? Sets { get; set; }
        public int? Reps { get; set; }
        public int? DurationMin { get; set; }

        /// <summary>Time of day to remind the client about this exercise.</summary>
        public TimeSpan? ReminderTime { get; set; }

        // Navigation properties
        [ForeignKey(nameof(WorkoutPlanID))]
        public WorkoutPlan? WorkoutPlan { get; set; }

        [ForeignKey(nameof(ExerciseID))]
        public Exercise? Exercise { get; set; }
    }
}
