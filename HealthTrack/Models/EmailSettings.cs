using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    [Table("EmailSettings")]
    public class EmailSettings
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EmailSettingsID { get; set; }

        [Required]
        [MaxLength(255)]
        public string SMTPServer { get; set; } = null!;

        [Required]
        public int SMTPPort { get; set; } = 587;

        [MaxLength(255)]
        public string? SMTPUsername { get; set; }

        [MaxLength(255)]
        public string? SMTPPassword { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(255)]
        public string FromEmail { get; set; } = null!;

        [MaxLength(255)]
        public string? FromName { get; set; }

        [Required]
        public bool EnableSSL { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
