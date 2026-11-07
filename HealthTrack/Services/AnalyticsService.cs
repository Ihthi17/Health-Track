using System.Text;
using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Builds the Task 7 analytics report (popular routines + engagement statistics)
    /// from what clients log (WorkoutLogs, MealLogs, HealthMetrics) and what trainers
    /// plan (WorkoutPlans / WorkoutPlanItems).
    ///
    /// The same code serves both reports:
    ///   - Trainer: clientIds = the trainer's assigned clients
    ///   - Admin:   clientIds = null (all clients)
    /// </summary>
    public static class AnalyticsService
    {
        public static readonly int[] AllowedPeriods = { 7, 30, 90 };

        public static int NormalisePeriod(int days) => AllowedPeriods.Contains(days) ? days : 30;

        public static async Task<AnalyticsReport> BuildReportAsync(HealthTrackContext context, List<int>? clientIds, int days)
        {
            days = NormalisePeriod(days);
            var today = DateTime.Today;
            var from = today.AddDays(-(days - 1));

            var report = new AnalyticsReport { Days = days, From = from, To = today };

            // Which clients are included? (null = everyone)
            var allClientIds = clientIds ?? await context.ClientProfiles.Select(c => c.ClientProfileID).ToListAsync();
            report.TotalClients = allClientIds.Count;

            // ----- Raw logs for the period (only the columns we need) -----
            var workouts = await context.WorkoutLogs
                .Where(w => allClientIds.Contains(w.ClientID) && w.LogDate >= from && w.LogDate <= today)
                .Select(w => new { w.ClientID, w.ExerciseID, w.PlanItemID, w.LogDate, w.DurationMin, w.CaloriesBurned })
                .ToListAsync();

            var meals = await context.MealLogs
                .Where(m => allClientIds.Contains(m.ClientID) && m.LogDate >= from && m.LogDate <= today)
                .Select(m => new { m.ClientID, m.LogDate })
                .ToListAsync();

            var metrics = await context.HealthMetrics
                .Where(m => allClientIds.Contains(m.ClientID) && m.MeasuredOn >= from && m.MeasuredOn <= today)
                .Select(m => new { m.ClientID, m.MeasuredOn })
                .ToListAsync();

            report.WorkoutsLogged = workouts.Count;
            report.MealsLogged = meals.Count;
            report.MeasurementsLogged = metrics.Count;
            report.CaloriesBurned = workouts.Sum(w => w.CaloriesBurned ?? 0);

            // ----- Engagement: who logged anything in the last 7 days? -----
            var weekAgo = today.AddDays(-6);
            var activeIds = await context.WorkoutLogs
                    .Where(w => allClientIds.Contains(w.ClientID) && w.LogDate >= weekAgo).Select(w => w.ClientID)
                .Union(context.MealLogs
                    .Where(m => allClientIds.Contains(m.ClientID) && m.LogDate >= weekAgo).Select(m => m.ClientID))
                .Union(context.HealthMetrics
                    .Where(m => allClientIds.Contains(m.ClientID) && m.MeasuredOn >= weekAgo).Select(m => m.ClientID))
                .Distinct()
                .ToListAsync();
            report.ActiveClients = activeIds.Count;

            // ----- Daily activity for the chart -----
            for (var day = from; day <= today; day = day.AddDays(1))
            {
                report.DailyActivity.Add(new DailyActivityRow
                {
                    Day = day,
                    Workouts = workouts.Count(w => w.LogDate == day),
                    Meals = meals.Count(m => m.LogDate == day),
                    Measurements = metrics.Count(m => m.MeasuredOn == day)
                });
            }

            // ----- Popular routines: exercises clients actually logged -----
            var exercises = await context.Exercises.ToDictionaryAsync(e => e.ExerciseID);

            report.PopularExercises = workouts
                .GroupBy(w => w.ExerciseID)
                .Select(g => new PopularExerciseRow
                {
                    ExerciseName = exercises.TryGetValue(g.Key, out var e) ? e.ExerciseName : "Exercise",
                    Category = exercises.TryGetValue(g.Key, out var e2) ? e2.Category : null,
                    TimesLogged = g.Count(),
                    TotalMinutes = g.Sum(w => w.DurationMin ?? 0),
                    TotalCalories = g.Sum(w => w.CaloriesBurned ?? 0),
                    ClientCount = g.Select(w => w.ClientID).Distinct().Count()
                })
                .OrderByDescending(r => r.TimesLogged)
                .ThenByDescending(r => r.TotalMinutes)
                .Take(10)
                .ToList();

            // ----- Popular routines: exercises trainers put into plans -----
            var plannedItems = await context.WorkoutPlanItems
                .Where(i => allClientIds.Contains(i.WorkoutPlan!.ClientID) && i.WorkoutPlan.IsActive)
                .Select(i => new { i.ExerciseID, i.WorkoutPlan!.ClientID })
                .ToListAsync();

            report.MostPlannedExercises = plannedItems
                .GroupBy(i => i.ExerciseID)
                .Select(g => new PopularExerciseRow
                {
                    ExerciseName = exercises.TryGetValue(g.Key, out var e) ? e.ExerciseName : "Exercise",
                    Category = exercises.TryGetValue(g.Key, out var e2) ? e2.Category : null,
                    TimesLogged = g.Count(),                              // times it appears in plans
                    ClientCount = g.Select(i => i.ClientID).Distinct().Count()
                })
                .OrderByDescending(r => r.TimesLogged)
                .Take(10)
                .ToList();

            // ----- Plan usage: completion of each plan in the period -----
            var plans = await context.WorkoutPlans
                .Include(p => p.Items)
                .Where(p => allClientIds.Contains(p.ClientID))
                .ToListAsync();

            var names = await context.Users
                .Select(u => new { u.UserID, Name = u.FirstName + " " + u.LastName })
                .ToDictionaryAsync(u => u.UserID, u => u.Name);

            foreach (var plan in plans)
            {
                var row = new PlanUsageRow
                {
                    PlanName = plan.PlanName,
                    ClientName = names.TryGetValue(plan.ClientID, out var c) ? c : "Client",
                    TrainerName = names.TryGetValue(plan.TrainerID, out var t) ? t : "Trainer",
                    IsActive = plan.IsActive
                };

                // Scheduled = plan exercises falling on days inside both the plan and the period
                var start = plan.StartDate > from ? plan.StartDate : from;
                var end = plan.EndDate.HasValue && plan.EndDate.Value < today ? plan.EndDate.Value : today;
                for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
                    row.Scheduled += plan.Items.Count(i => i.DayOfWeek == (byte)day.DayOfWeek);

                // Done = plan exercises logged with "Log it" (each counted once per day)
                var itemIds = plan.Items.Select(i => i.PlanItemID).ToHashSet();
                row.Done = Math.Min(row.Scheduled, workouts
                    .Where(w => w.PlanItemID.HasValue && itemIds.Contains(w.PlanItemID.Value) && w.LogDate >= start)
                    .Select(w => new { w.PlanItemID, w.LogDate })
                    .Distinct()
                    .Count());

                report.Plans.Add(row);
            }

            report.Plans = report.Plans
                .OrderByDescending(p => p.IsActive)
                .ThenByDescending(p => p.CompletionPercent ?? -1)
                .ToList();

            return report;
        }

        /// <summary>Turns a report into a CSV file (opens in Excel).</summary>
        public static byte[] ToCsv(AnalyticsReport r, string title)
        {
            var csv = new StringBuilder();
            csv.AppendLine(title);
            csv.AppendLine($"Period,{r.From:yyyy-MM-dd} to {r.To:yyyy-MM-dd} ({r.Days} days)");
            csv.AppendLine();

            csv.AppendLine("ENGAGEMENT");
            csv.AppendLine($"Total clients,{r.TotalClients}");
            csv.AppendLine($"Active clients (last 7 days),{r.ActiveClients}");
            csv.AppendLine($"Inactive clients,{r.InactiveClients}");
            csv.AppendLine($"Engagement %,{r.EngagementPercent}");
            csv.AppendLine($"Workouts logged,{r.WorkoutsLogged}");
            csv.AppendLine($"Meals logged,{r.MealsLogged}");
            csv.AppendLine($"Measurements logged,{r.MeasurementsLogged}");
            csv.AppendLine($"Calories burned,{r.CaloriesBurned:0}");
            csv.AppendLine($"Average workouts per client,{r.AverageWorkoutsPerClient}");
            csv.AppendLine($"Average plan completion %,{r.AveragePlanCompletion?.ToString() ?? "n/a"}");
            csv.AppendLine();

            csv.AppendLine("MOST POPULAR EXERCISES (logged by clients)");
            csv.AppendLine("Exercise,Category,Times logged,Total minutes,Calories,Clients");
            foreach (var e in r.PopularExercises)
                csv.AppendLine($"{Esc(e.ExerciseName)},{Esc(e.Category)},{e.TimesLogged},{e.TotalMinutes},{e.TotalCalories:0},{e.ClientCount}");
            csv.AppendLine();

            csv.AppendLine("MOST PLANNED EXERCISES (in active plans)");
            csv.AppendLine("Exercise,Category,Times in plans,Clients");
            foreach (var e in r.MostPlannedExercises)
                csv.AppendLine($"{Esc(e.ExerciseName)},{Esc(e.Category)},{e.TimesLogged},{e.ClientCount}");
            csv.AppendLine();

            csv.AppendLine("WORKOUT PLANS");
            csv.AppendLine("Plan,Client,Trainer,Status,Scheduled,Done,Completion %");
            foreach (var p in r.Plans)
                csv.AppendLine($"{Esc(p.PlanName)},{Esc(p.ClientName)},{Esc(p.TrainerName)},{(p.IsActive ? "Active" : "Inactive")},{p.Scheduled},{p.Done},{p.CompletionPercent?.ToString() ?? "n/a"}");
            csv.AppendLine();

            csv.AppendLine("DAILY ACTIVITY");
            csv.AppendLine("Date,Workouts,Meals,Measurements");
            foreach (var d in r.DailyActivity)
                csv.AppendLine($"{d.Day:yyyy-MM-dd},{d.Workouts},{d.Meals},{d.Measurements}");

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        }

        /// <summary>Quotes a CSV value if it contains a comma or quote.</summary>
        private static string Esc(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
        }
    }
}
