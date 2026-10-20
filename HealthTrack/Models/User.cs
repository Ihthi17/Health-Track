using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserID { get; set; }

        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = null!;

        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string Email { get; set; } = null!;

        [Required]
        [Column("PasswordHashNew")]
        [MaxLength(256)]
        public string PasswordHash { get; set; } = null!;

        [Required]
        [Column("PasswordSaltNew")]
        [MaxLength(256)]
        public string PasswordSalt { get; set; } = null!;

        public bool IsActive { get; set; } = true;

        public int FailedLoginCount { get; set; }

        public DateTime? LockoutEnd { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Role foreign key
        [ForeignKey("Role")]
        public int? RoleID { get; set; }
        public Role? Role { get; set; }
    }
}
