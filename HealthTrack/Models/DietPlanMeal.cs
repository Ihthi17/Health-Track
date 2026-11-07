using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    /// <summary>
    /// One scheduled meal inside a diet plan, e.g. "Breakfast - Oats with fruit - 350 kcal - 08:00".
    /// </summary>
    [Table("DietPlanMeals")]
    public class DietPlanMeal
    {
        [Key]
        public int DietMealID { get; set; }

        public int DietPlanID { get; set; }

        /// <summary>Breakfast, Lunch, Dinner or Snack.</summary>
        [Required, MaxLength(20)]
        public string MealType { get; set; } = null!;

        [Required, MaxLength(100)]
        public string MealName { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        public int? Calories { get; set; }

        /// <summary>Time of day the meal is scheduled (used for meal reminders).</summary>
        public TimeSpan? MealTime { get; set; }

        // Navigation property
        [ForeignKey(nameof(DietPlanID))]
        public DietPlan? DietPlan { get; set; }
    }
}
