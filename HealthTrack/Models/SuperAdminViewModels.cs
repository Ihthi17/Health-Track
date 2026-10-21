using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    /// <summary>
    /// ViewModels for SuperAdmin Reports and Analytics
    /// </summary>

    #region Trainer Assignment Management

    public class TrainerAssignmentViewModel
    {
        public int AssignmentId { get; set; }
        public int TrainerId { get; set; }
        public string TrainerName { get; set; } = "";
        public string TrainerEmail { get; set; } = "";
        public int ClientId { get; set; }
        public string ClientName { get; set; } = "";
        public string ClientEmail { get; set; } = "";
        public DateTime AssignedDate { get; set; }
        public string Status { get; set; } = "Active"; // Active, Inactive, Completed
        public string? Notes { get; set; }
    }

    public class ManageAssignmentsViewModel
    {
        public List<TrainerProfile> AvailableTrainers { get; set; } = new();
        public List<ClientProfile> AvailableClients { get; set; } = new();
        public List<TrainerAssignmentViewModel> CurrentAssignments { get; set; } = new();
        public int TotalActiveAssignments { get; set; }
        public int? SelectedTrainerId { get; set; }
        public int? SelectedClientId { get; set; }
    }

    #endregion

    #region Reports and Analytics

    public class ReportsAndAnalyticsViewModel
    {
        // Date filters
        public DateTime StartDate { get; set; } = DateTime.UtcNow.AddMonths(-1);
        public DateTime EndDate { get; set; } = DateTime.UtcNow;

        // User Registration Report
        public int NewRegistrationsThisPeriod { get; set; }
        public int NewClientsThisPeriod { get; set; }
        public int NewTrainersThisPeriod { get; set; }
        public int ActiveAccountsThisPeriod { get; set; }
        public List<UserRegistrationRecord> UserRegistrationDetails { get; set; } = new();

        // Client and Trainer Distribution
        public int TotalActiveClients { get; set; }
        public int TotalInactiveClients { get; set; }
        public int TotalActiveTrainers { get; set; }
        public int TotalInactiveTrainers { get; set; }
        public int TotalAssignedClients { get; set; }
        public int TotalUnassignedClients { get; set; }

        // Trainer Assignment Statistics
        public int TotalTrainerAssignments { get; set; }
        public double AverageClientsPerTrainer { get; set; }
        public List<TrainerAssignmentStatsRecord> TrainerAssignmentStats { get; set; } = new();

        // Appointment Statistics
        public int TotalAppointmentsThisPeriod { get; set; }
        public int PendingAppointmentsThisPeriod { get; set; }
        public int ApprovedAppointmentsThisPeriod { get; set; }
        public int CompletedAppointmentsThisPeriod { get; set; }
        public int CancelledAppointmentsThisPeriod { get; set; }
        public double CompletionRate { get; set; }
        public double CancellationRate { get; set; }
        public List<AppointmentTrendRecord> AppointmentTrends { get; set; } = new();
        public List<AppointmentDetailRecord> AppointmentDetails { get; set; } = new();

        // Workout and Diet Plan Usage
        public int TotalWorkoutPlansThisPeriod { get; set; }
        public int TotalDietPlansThisPeriod { get; set; }
        public int ActiveWorkoutPlans { get; set; }
        public int ActiveDietPlans { get; set; }

        // Engagement and System Usage
        public int TotalActiveSessions { get; set; }
        public int TotalAuditEventsThisPeriod { get; set; }
        public List<SystemActivityRecord> SystemActivityLog { get; set; } = new();

        // Chart Data (JSON strings)
        public string RegistrationTrendChartJson { get; set; } = "{}";
        public string ClientTrainerDistributionChartJson { get; set; } = "{}";
        public string AppointmentStatusChartJson { get; set; } = "{}";
        public string AppointmentCompletionChartJson { get; set; } = "{}";

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
    }

    public class UserRegistrationRecord
    {
        public int UserId { get; set; }
        public string Email { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Role { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
    }

    public class TrainerAssignmentStatsRecord
    {
        public int TrainerId { get; set; }
        public string TrainerName { get; set; } = "";
        public int ClientCount { get; set; }
        public int TotalAppointments { get; set; }
        public int CompletedAppointments { get; set; }
        public double AverageRating { get; set; }
    }

    public class AppointmentTrendRecord
    {
        public DateTime Date { get; set; }
        public int TotalAppointments { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public int Pending { get; set; }
    }

    public class AppointmentDetailRecord
    {
        public int AppointmentId { get; set; }
        public string ClientName { get; set; } = "";
        public string TrainerName { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = "";
        public string? Reason { get; set; }
    }

    public class SystemActivityRecord
    {
        public int AuditId { get; set; }
        public string Action { get; set; } = "";
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
        public string PerformedBy { get; set; } = "";
        public string? IPAddress { get; set; }
    }

    #endregion

    #region Activation and Security Management

    public class ActivationSecurityViewModel
    {
        // Pending Activations
        public List<PendingActivationRecord> PendingActivations { get; set; } = new();
        public int TotalPendingActivations { get; set; }
        public int TotalExpiredTokens { get; set; }

        // Account Status
        public List<AccountStatusRecord> AccountStatus { get; set; } = new();
        public int TotalActiveAccounts { get; set; }
        public int TotalInactiveAccounts { get; set; }
        public int TotalLockedAccounts { get; set; }

        // Failed Login Attempts
        public List<FailedLoginRecord> FailedLoginAttempts { get; set; } = new();
        public int TotalFailedAttemptsThisPeriod { get; set; }

        // Security Audit Events
        public List<SecurityAuditRecord> SecurityAuditEvents { get; set; } = new();

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);

        // Filters
        public string SelectedStatus { get; set; } = "all"; // all, pending, expired, active, inactive
        public DateTime StartDate { get; set; } = DateTime.UtcNow.AddMonths(-1);
        public DateTime EndDate { get; set; } = DateTime.UtcNow;
    }

    public class PendingActivationRecord
    {
        public int UserId { get; set; }
        public string Email { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Role { get; set; } = "";
        public DateTime RegistrationDate { get; set; }
        public DateTime TokenExpiryDate { get; set; }
        public bool IsExpired { get; set; }
        public int ActivationTokenId { get; set; }
    }

    public class AccountStatusRecord
    {
        public int UserId { get; set; }
        public string Email { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsActive { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class FailedLoginRecord
    {
        public int UserId { get; set; }
        public string Email { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public int FailedAttempts { get; set; }
        public DateTime LastFailedAttempt { get; set; }
        public bool IsCurrentlyLocked { get; set; }
        public DateTime? LockoutEnd { get; set; }
    }

    public class SecurityAuditRecord
    {
        public int AuditId { get; set; }
        public string Action { get; set; } = "";
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
        public string PerformedBy { get; set; } = "";
        public string? IPAddress { get; set; }
        public string Severity { get; set; } = "Info"; // Info, Warning, Error, Critical
    }

    #endregion

    #region User and Account Management

    public class UserAccountManagementViewModel
    {
        public List<User> Users { get; set; } = new();
        public List<Role> AvailableRoles { get; set; } = new();
        public List<ClientProfile> ClientProfiles { get; set; } = new();
        public List<TrainerProfile> TrainerProfiles { get; set; } = new();

        // Statistics
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int TotalPendingActivations { get; set; }

        // Filters
        public string SearchEmail { get; set; } = "";
        public int? FilterRoleId { get; set; }
        public string FilterStatus { get; set; } = "all"; // all, active, inactive

        // Pagination
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalRecords { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
    }

    #endregion
}
