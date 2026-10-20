using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    [Table("ClientProfiles")]
    public class ClientProfile
    {
        [Key]
        [Column("ClientID")]
        public int ClientProfileID { get; set; }

        [ForeignKey(nameof(User))]
        public int? UserID { get; set; }

        public string? ProfileImagePath { get; set; }
        public string? Phone { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? StateProvince { get; set; }
        public string? PostalCode { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public decimal? HeightCm { get; set; }
        public string? GoalDescription { get; set; }
        public string? MedicalNotes { get; set; }

        // Navigation property to User
        public User? User { get; set; }
    }
}
