using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    /// <summary>
    /// A meal the client ACTUALLY ate (logged by the client).
    /// </summary>
    [Table("MealLogs")]
    public class MealLog
    {
        [Key]
        public int MealLogID { get; set; }

        public int ClientID { get; set; }

        [Column(TypeName = "date")]
        public DateTime LogDate { get; set; }

        [Required, MaxLength(20)]
        public string MealType { get; set; } = null!;

        [Required, MaxLength(100)]
        public string FoodName { get; set; } = null!;

        public int? Calories { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal? ProteinG { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal? CarbsG { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal? FatG { get; set; }

        // Navigation property
        [ForeignKey(nameof(ClientID))]
        public ClientProfile? Client { get; set; }
    }
}
