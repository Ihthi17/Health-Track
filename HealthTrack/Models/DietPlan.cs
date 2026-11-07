using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    /// <summary>
    /// A diet plan PRESCRIBED by a trainer for one client.
    /// Created by the trainer side; read-only on the client side.
    /// </summary>
    [Table("DietPlans")]
    public class DietPlan
    {
        [Key]
        public int DietPlanID { get; set; }

        public int TrainerID { get; set; }

        public int ClientID { get; set; }

        [Required, MaxLength(100)]
        public string PlanName { get; set; } = null!;

        /// <summary>Target calories per day.</summary>
        public int? DailyCalories { get; set; }

        [Column(TypeName = "date")]
        public DateTime StartDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime? EndDate { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation properties
        [ForeignKey(nameof(TrainerID))]
        public TrainerProfile? Trainer { get; set; }

        [ForeignKey(nameof(ClientID))]
        public ClientProfile? Client { get; set; }

        /// <summary>The scheduled meals in this plan.</summary>
        public List<DietPlanMeal> Meals { get; set; } = new();
    }
}
