using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    public class Role
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RoleID { get; set; }

        [Required]
        [MaxLength(100)]
        public string RoleName { get; set; } = null!;

        public string? Description { get; set; }

        // Navigation property for many-to-many relationship with Permissions
        [InverseProperty("Roles")]
        public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
    }
}
