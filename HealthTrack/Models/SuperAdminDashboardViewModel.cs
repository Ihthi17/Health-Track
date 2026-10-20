using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    /// <summary>
    /// Strongly-typed ViewModel for SuperAdmin Dashboard
    /// Replaces ViewBag with proper type safety and allows passing chart data as JSON
    /// </summary>
    public class SuperAdminDashboardViewModel
    {
        // Summary Statistics
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalClients { get; set; }
        public int TotalTrainers { get; set; }
        public int TotalAdmins { get; set; }

        // Appointment Statistics
        public int TotalAppointments { get; set; }
        public int PendingAppointments { get; set; }
        public int ApprovedAppointments { get; set; }
        public int CompletedAppointments { get; set; }
        public int CancelledAppointments { get; set; }

        // Recent Data
        public List<AppointmentSummary> RecentAppointments { get; set; } = new();
        public List<NotificationSummary> RecentNotifications { get; set; } = new();
        public List<AuditLogSummary> RecentAuditLogs { get; set; } = new();

        // Workout, Diet, and Log Counts (for future expansion)
        public int TotalWorkoutPlans { get; set; }
        public int TotalDietPlans { get; set; }
        public int TotalWorkoutLogs { get; set; }
        public int TotalMealLogs { get; set; }

        // Chart Data (as JSON strings for Chart.js)
        public string AppointmentStatusChartJson { get; set; } = "{}";
        public string UsersByRoleChartJson { get; set; } = "{}";
        public string AuditActivityChartJson { get; set; } = "{}";
        public string AppointmentsPerMonthChartJson { get; set; } = "{}";

        // Error Handling
        public string? ErrorMessage { get; set; }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    }

    public class AppointmentSummary
    {
        public int AppointmentID { get; set; }
        public string ClientName { get; set; } = "Unknown";
        public string TrainerName { get; set; } = "Unknown";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = "Pending";
    }

    public class NotificationSummary
    {
        public int NotificationID { get; set; }
        public string Title { get; set; } = "";
        public string? Message { get; set; }
        public string Type { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }

    public class AuditLogSummary
    {
        public int AuditID { get; set; }
        public string Action { get; set; } = "";
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? IPAddress { get; set; }
        public string PerformedBy { get; set; } = "System";
    }
}
