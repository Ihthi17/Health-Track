using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    /// <summary>
    /// One row of the trainer's "Client Progress" table - a summary of what
    /// one client has logged during the chosen period.
    /// </summary>
    public class ClientProgressRow
    {
        public int ClientID { get; set; }
        public string Name { get; set; } = "";

        public decimal? CurrentWeightKg { get; set; }

        /// <summary>Latest weight minus the first weight in the period (negative = lost weight).</summary>
        public decimal? WeightChangeKg { get; set; }

        public decimal? CurrentBmi { get; set; }

        /// <summary>All workouts logged in the period.</summary>
        public int WorkoutsLogged { get; set; }

        /// <summary>Plan exercises done / plan exercises scheduled in the period.</summary>
        public int PlanItemsDone { get; set; }
        public int PlanItemsScheduled { get; set; }

        /// <summary>0-100, or null if the client has no active workout plan.</summary>
        public int? CompletionPercent =>
            PlanItemsScheduled > 0 ? (int)Math.Round(PlanItemsDone * 100.0 / PlanItemsScheduled) : null;

        public decimal CaloriesBurned { get; set; }

        /// <summary>Most recent day the client logged anything.</summary>
        public DateTime? LastActivity { get; set; }
    }

    /// <summary>Data for the trainer's Client Progress overview page (Task 7).</summary>
    public class TrainerProgressViewModel
    {
        /// <summary>Length of the period in days (7, 30 or 90).</summary>
        public int Days { get; set; } = 30;

        public List<ClientProgressRow> Rows { get; set; } = new();

        // Summary cards across all the trainer's clients
        public int ActiveClients => Rows.Count;
        public decimal? AverageWeightChange =>
            Rows.Any(r => r.WeightChangeKg.HasValue)
                ? Math.Round(Rows.Where(r => r.WeightChangeKg.HasValue).Average(r => r.WeightChangeKg!.Value), 1)
                : null;
        public int? AverageCompletion =>
            Rows.Any(r => r.CompletionPercent.HasValue)
                ? (int)Math.Round(Rows.Where(r => r.CompletionPercent.HasValue).Average(r => r.CompletionPercent!.Value))
                : null;
        public int TotalWorkouts => Rows.Sum(r => r.WorkoutsLogged);
    }

    /// <summary>Data for one client's progress page (charts, logs and feedback).</summary>
    public class TrainerClientProgressViewModel
    {
        public int Days { get; set; } = 30;
        public ClientProgressRow Summary { get; set; } = new();

        // Chart data (oldest first)
        public List<string> ChartDates { get; set; } = new();
        public List<decimal?> ChartWeights { get; set; } = new();
        public List<decimal?> ChartBmis { get; set; } = new();

        public List<WorkoutLog> RecentWorkouts { get; set; } = new();
        public List<MealLog> RecentMeals { get; set; } = new();
        public List<TrainerFeedback> Feedback { get; set; } = new();

        public FeedbackInput NewFeedback { get; set; } = new();
    }

    /// <summary>The trainer's feedback form.</summary>
    public class FeedbackInput
    {
        [Required(ErrorMessage = "Please write your feedback.")]
        [MinLength(3, ErrorMessage = "Feedback is too short.")]
        [MaxLength(1000, ErrorMessage = "Feedback can be at most 1000 characters.")]
        [Display(Name = "Feedback for the client")]
        public string? Message { get; set; }
    }
}
