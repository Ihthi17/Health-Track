using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    /// <summary>
    /// A health measurement recorded by the client on a given date
    /// (weight, BMI, blood pressure, etc.). Used for progress charts.
    /// </summary>
    [Table("HealthMetrics")]
    public class HealthMetric
    {
        [Key]
        public int MetricID { get; set; }

        public int ClientID { get; set; }

        [Column(TypeName = "date")]
        public DateTime MeasuredOn { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? WeightKg { get; set; }

        /// <summary>Body Mass Index = weight (kg) / height (m) squared.</summary>
        [Column(TypeName = "decimal(4,1)")]
        public decimal? BMI { get; set; }

        /// <summary>Stored as text, e.g. "120/80".</summary>
        [MaxLength(10)]
        public string? BloodPressure { get; set; }

        public int? HeartRate { get; set; }

        [Column(TypeName = "decimal(5,1)")]
        public decimal? BloodSugar { get; set; }

        [Column(TypeName = "decimal(4,1)")]
        public decimal? BodyFatPercent { get; set; }

        // Navigation property
        [ForeignKey(nameof(ClientID))]
        public ClientProfile? Client { get; set; }
    }
}
