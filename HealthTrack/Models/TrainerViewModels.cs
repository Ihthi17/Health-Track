namespace HealthTrack.Models
{
    /// <summary>
    /// View model for Trainer Dashboard
    /// </summary>
    public class TrainerDashboardViewModel
    {
        public int AssignedClientsCount { get; set; }
        public int TotalAppointments { get; set; }
        public List<Appointment> UpcomingAppointments { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// View model for Client Details
    /// </summary>
    public class ClientDetailViewModel
    {
        public ClientProfile? ClientProfile { get; set; }
        public List<Appointment> Appointments { get; set; } = new();
    }

    /// <summary>
    /// View model for Trainer Profile Management
    /// </summary>
    public class TrainerProfileViewModel
    {
        public User? User { get; set; }
        public TrainerProfile? TrainerProfile { get; set; }
    }
}
