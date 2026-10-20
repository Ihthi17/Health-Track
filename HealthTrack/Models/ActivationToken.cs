using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    public class ActivationToken
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ActivationTokenID { get; set; }

        [Required]
        public int UserID { get; set; }

        [Required]
        [MaxLength(256)]
        public string TokenHash { get; set; } = null!;

        [Required]
        public DateTime ExpiresAt { get; set; }

        public bool IsUsed { get; set; }
    }
}
