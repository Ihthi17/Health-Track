using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthTrack.Models
{
    /// <summary>
    /// Links a trainer to a client. This is the "connection" between the
    /// trainer module and the client module: a trainer may only see and
    /// manage clients that have an active assignment to them.
    /// Created by an administrator.
    /// </summary>
    [Table("TrainerClientAssignments")]
    public class TrainerClientAssignment
    {
        [Key]
        public int AssignmentID { get; set; }

        public int TrainerID { get; set; }

        public int ClientID { get; set; }

        /// <summary>UserID of the admin who made the assignment.</summary>
        public int? AssignedBy { get; set; }

        public DateTime AssignedAt { get; set; } = DateTime.Now;

        /// <summary>False when the assignment has ended (kept for history).</summary>
        public bool IsActive { get; set; } = true;

        // Navigation properties
        [ForeignKey(nameof(TrainerID))]
        public TrainerProfile? Trainer { get; set; }

        [ForeignKey(nameof(ClientID))]
        public ClientProfile? Client { get; set; }

        [ForeignKey(nameof(AssignedBy))]
        public User? AssignedByUser { get; set; }
    }
}
