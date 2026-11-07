using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    /// <summary>
    /// An exercise in the shared exercise library (e.g. "Squats", "Running").
    /// Trainers pick exercises from here when building workout plans,
    /// and clients pick them when logging a workout.
    /// </summary>
    [Table("Exercises")]
    public class Exercise
    {
        [Key]
        public int ExerciseID { get; set; }

        [Required, MaxLength(100)]
        public string ExerciseName { get; set; } = null!;

        [MaxLength(50)]
        public string? Category { get; set; }

        /// <summary>Used to estimate calories burned: minutes x CaloriesPerMinute.</summary>
        [Column(TypeName = "decimal(5,2)")]
        public decimal? CaloriesPerMinute { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }
    }
}
