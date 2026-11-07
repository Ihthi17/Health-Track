namespace HealthTrack.Models
{
    /// <summary>One exercise in the "most popular" list.</summary>
    public class PopularExerciseRow
    {
        public string ExerciseName { get; set; } = "";
        public string? Category { get; set; }
        public int TimesLogged { get; set; }
        public int TotalMinutes { get; set; }
        public decimal TotalCalories { get; set; }

        /// <summary>How many different clients logged this exercise.</summary>
        public int ClientCount { get; set; }
    }

    /// <summary>One workout plan with how well clients are following it.</summary>
    public class PlanUsageRow
    {
        public string PlanName { get; set; } = "";
        public string ClientName { get; set; } = "";
        public string TrainerName { get; set; } = "";
        public bool IsActive { get; set; }
        public int Scheduled { get; set; }
        public int Done { get; set; }
        public int? CompletionPercent => Scheduled > 0 ? (int)Math.Round(Done * 100.0 / Scheduled) : null;
    }

    /// <summary>Activity for one day (for the engagement chart).</summary>
    public class DailyActivityRow
    {
        public DateTime Day { get; set; }
        public int Workouts { get; set; }
        public int Meals { get; set; }
        public int Measurements { get; set; }
    }

    /// <summary>
    /// Task 7 report: popular routines and engagement statistics, for a set of
    /// clients over a period. Used by the trainer (their clients) and the admin (everyone).
    /// </summary>
    public class AnalyticsReport
    {
        public int Days { get; set; } = 30;
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        // ----- Engagement -----
        public int TotalClients { get; set; }

        /// <summary>Clients who logged anything in the last 7 days.</summary>
        public int ActiveClients { get; set; }
        public int InactiveClients => TotalClients - ActiveClients;
        public int EngagementPercent => TotalClients > 0 ? (int)Math.Round(ActiveClients * 100.0 / TotalClients) : 0;

        public int WorkoutsLogged { get; set; }
        public int MealsLogged { get; set; }
        public int MeasurementsLogged { get; set; }
        public decimal CaloriesBurned { get; set; }
        public double AverageWorkoutsPerClient => TotalClients > 0 ? Math.Round((double)WorkoutsLogged / TotalClients, 1) : 0;

        /// <summary>Average plan completion across active plans (null if none).</summary>
        public int? AveragePlanCompletion =>
            Plans.Any(p => p.IsActive && p.CompletionPercent.HasValue)
                ? (int)Math.Round(Plans.Where(p => p.IsActive && p.CompletionPercent.HasValue).Average(p => p.CompletionPercent!.Value))
                : null;

        // ----- Popular routines -----
        public List<PopularExerciseRow> PopularExercises { get; set; } = new();

        /// <summary>Exercises trainers put into plans most often.</summary>
        public List<PopularExerciseRow> MostPlannedExercises { get; set; } = new();

        public List<PlanUsageRow> Plans { get; set; } = new();

        public List<DailyActivityRow> DailyActivity { get; set; } = new();
    }
}
