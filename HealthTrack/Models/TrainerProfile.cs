using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    public class TrainerProfile
    {
        [Key]
        [Column("TrainerID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TrainerProfileID { get; set; }

        [ForeignKey(nameof(User))]
        public int? UserID { get; set; }

        public string? PhoneNumber { get; set; }
        public string? Specialization { get; set; }
        public string? Qualifications { get; set; }
        public bool IsActivated { get; set; }

        // Navigation property to User
        public User? User { get; set; }
    }
}
