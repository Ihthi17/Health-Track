using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    /// <summary>
    /// Creates automatic reminder notifications (Task 8):
    ///   Client  - today's planned workouts, today's planned meals, and
    ///             approved appointments in the next 24 hours.
    ///   Trainer - approved appointments in the next 24 hours.
    ///
    /// It is called every time the notification bell is shown, so reminders are
    /// created when the user is using the app (no background job needed).
    /// Each reminder is only created ONCE: before adding one we check whether a
    /// reminder with the same title and time already exists for that user.
    /// </summary>
    public static class ReminderGenerator
    {
        public const string ReminderType = "Reminder";

        /// <summary>Default reminder time for plan items that have no time set.</summary>
        private static readonly TimeSpan DefaultTime = new(8, 0, 0);

        /// <summary>Creates any missing reminders for this user. Errors are swallowed by the caller.</summary>
        public static async Task EnsureRemindersAsync(HealthTrackContext context, int userId, bool isClient, bool isTrainer)
        {
            var candidates = new List<Notification>();

            if (isClient)
                candidates.AddRange(await BuildClientRemindersAsync(context, userId));
            if (isTrainer)
                candidates.AddRange(await BuildTrainerRemindersAsync(context, userId));

            if (candidates.Count == 0)
                return;

            // Load today's/tomorrow's existing reminders once, then skip duplicates
            var from = DateTime.Today;
            var to = DateTime.Today.AddDays(2);
            var existing = await context.Notifications
                .Where(n => n.UserID == userId && n.Type == ReminderType
                            && n.RemindAt >= from && n.RemindAt < to)
                .Select(n => new { n.Title, n.RemindAt })
                .ToListAsync();

            var newOnes = candidates
                .Where(c => !existing.Any(e => e.Title == c.Title && e.RemindAt == c.RemindAt))
                .ToList();

            if (newOnes.Count > 0)
            {
                context.Notifications.AddRange(newOnes);
                await context.SaveChangesAsync();
            }
        }

        /// <summary>Workout, meal and appointment reminders for a client (ClientID = UserID).</summary>
        private static async Task<List<Notification>> BuildClientRemindersAsync(HealthTrackContext context, int clientId)
        {
            var list = new List<Notification>();
            var today = DateTime.Today;
            byte todayNumber = (byte)today.DayOfWeek;

            // --- Today's workouts from the active plan (skip ones already logged today) ---
            var workoutPlan = await context.WorkoutPlans
                .Include(p => p.Items).ThenInclude(i => i.Exercise)
                .Where(p => p.ClientID == clientId && p.IsActive && p.StartDate <= today
                            && (p.EndDate == null || p.EndDate >= today))
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();

            if (workoutPlan != null)
            {
                var doneToday = await context.WorkoutLogs
                    .Where(w => w.ClientID == clientId && w.LogDate == today && w.PlanItemID != null)
                    .Select(w => w.PlanItemID!.Value)
                    .ToListAsync();

                foreach (var item in workoutPlan.Items.Where(i => i.DayOfWeek == todayNumber && !doneToday.Contains(i.PlanItemID)))
                {
                    var time = item.ReminderTime ?? DefaultTime;
                    var amount = item.Sets.HasValue && item.Reps.HasValue ? $"{item.Sets} × {item.Reps}"
                               : item.DurationMin.HasValue ? $"{item.DurationMin} min" : "";

                    list.Add(NewReminder(clientId,
                        $"Workout: {item.Exercise?.ExerciseName ?? "Exercise"}",
                        $"Today at {FormatTime(time)}" + (amount != "" ? $" – {amount}" : "") + $" ({workoutPlan.PlanName})",
                        today + time));
                }
            }

            // --- Today's meals from the active diet plan ---
            var dietPlan = await context.DietPlans
                .Include(p => p.Meals)
                .Where(p => p.ClientID == clientId && p.IsActive && p.StartDate <= today
                            && (p.EndDate == null || p.EndDate >= today))
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();

            if (dietPlan != null)
            {
                foreach (var meal in dietPlan.Meals.Where(m => m.MealTime.HasValue))
                {
                    list.Add(NewReminder(clientId,
                        $"{meal.MealType}: {meal.MealName}",
                        $"Today at {FormatTime(meal.MealTime!.Value)}" + (meal.Calories.HasValue ? $" – {meal.Calories} kcal" : ""),
                        today + meal.MealTime.Value));
                }
            }

            // --- Approved appointments in the next 24 hours ---
            var now = DateTime.Now;
            var appointments = await context.Appointments
                .Where(a => a.ClientID == clientId && a.Status == "Approved"
                            && a.StartTime > now && a.StartTime <= now.AddHours(24))
                .ToListAsync();

            foreach (var a in appointments)
            {
                var trainerName = await context.Users.Where(u => u.UserID == a.TrainerID)
                    .Select(u => u.FirstName).FirstOrDefaultAsync() ?? "your trainer";
                list.Add(NewReminder(clientId,
                    "Upcoming appointment",
                    $"Session with {trainerName} on {a.StartTime:ddd dd MMM} at {a.StartTime:hh:mm tt}",
                    a.StartTime));
            }

            return list;
        }

        /// <summary>Appointment reminders for a trainer (TrainerID = UserID).</summary>
        private static async Task<List<Notification>> BuildTrainerRemindersAsync(HealthTrackContext context, int trainerId)
        {
            var list = new List<Notification>();
            var now = DateTime.Now;

            var appointments = await context.Appointments
                .Where(a => a.TrainerID == trainerId && a.Status == "Approved"
                            && a.StartTime > now && a.StartTime <= now.AddHours(24))
                .ToListAsync();

            foreach (var a in appointments)
            {
                var clientName = await context.Users.Where(u => u.UserID == a.ClientID)
                    .Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync() ?? "a client";
                list.Add(NewReminder(trainerId,
                    "Upcoming session",
                    $"Session with {clientName} on {a.StartTime:ddd dd MMM} at {a.StartTime:hh:mm tt}",
                    a.StartTime));
            }

            return list;
        }

        private static Notification NewReminder(int userId, string title, string message, DateTime remindAt) => new()
        {
            UserID = userId,
            Type = ReminderType,
            Title = title.Length > 200 ? title[..200] : title,
            Message = message.Length > 500 ? message[..500] : message,
            RemindAt = remindAt,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        private static string FormatTime(TimeSpan t) => DateTime.Today.Add(t).ToString("hh:mm tt");
    }
}
