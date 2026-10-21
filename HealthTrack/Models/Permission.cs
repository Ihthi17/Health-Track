using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    [Table("Permissions")]
    public class Permission
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PermissionID { get; set; }

        [Required]
        [MaxLength(100)]
        public string PermissionName { get; set; } = null!;

        public string? Description { get; set; }

        [MaxLength(50)]
        public string? Module { get; set; }

        // Navigation property for many-to-many relationship with Roles
        [InverseProperty("Permissions")]
        public ICollection<Role> Roles { get; set; } = new List<Role>();
    }
}
