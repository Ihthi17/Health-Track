using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Services;

namespace HealthTrack.Controllers
{
    /// <summary>
    /// Trainer side of Progress Tracking (Task 7) and Feedback (Task 3).
    ///
    /// Reads what CLIENTS log (HealthMetrics, WorkoutLogs, MealLogs) and compares it
    /// with what the TRAINER planned (WorkoutPlanItems) to show each client's progress
    /// and plan completion. The trainer writes TrainerFeedback here, which the client
    /// sees on their dashboard.
    ///
    /// Security: trainers only, and only for clients actively assigned to them.
    /// </summary>
    [Authorize(Roles = "Trainer")]
    [Route("trainer/progress")]
    public class TrainerProgressController : Controller
    {
        /// <summary>Periods the trainer can choose from (days).</summary>
        private static readonly int[] AllowedPeriods = { 7, 30, 90 };

        private readonly HealthTrackContext _context;
        private readonly ILogger<TrainerProgressController> _logger;
        private readonly INotificationService _notifications;

        public TrainerProgressController(HealthTrackContext context,
                                         ILogger<TrainerProgressController> logger,
                                         INotificationService notifications)
        {
            _context = context;
            _logger = logger;
            _notifications = notifications;
        }

        #region Helpers

        /// <summary>TrainerID of the logged-in trainer (TrainerID = the trainer's UserID).</summary>
        private async Task<int?> GetCurrentTrainerIdAsync()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int userId = int.TryParse(claim, out var id) ? id : 0;

            return await _context.TrainerProfiles
                .Where(t => t.UserID == userId || t.TrainerProfileID == userId)
                .Select(t => (int?)t.TrainerProfileID)
                .FirstOrDefaultAsync();
        }

        /// <summary>The trainer's ACTIVE assigned clients (ClientID -> name).</summary>
        private async Task<Dictionary<int, string>> GetAssignedClientsAsync(int trainerId)
        {
            return await (
                from a in _context.TrainerClientAssignments
                join u in _context.Users on a.ClientID equals u.UserID
                where a.TrainerID == trainerId && a.IsActive
                select new { a.ClientID, Name = u.FirstName + " " + u.LastName }
            ).ToDictionaryAsync(x => x.ClientID, x => x.Name);
        }

        /// <summary>Only allow 7, 30 or 90 days (anything else becomes 30).</summary>
        private static int NormalisePeriod(int days) => AllowedPeriods.Contains(days) ? days : 30;

        /// <summary>
        /// Builds one client's summary for the period: weight change, workouts,
        /// calories burned, last activity and plan completion.
        ///
        /// Plan completion = plan exercises the client logged ("Log it")
        ///                   / plan exercises scheduled on the days in the period.
        /// </summary>
        private async Task<ClientProgressRow> BuildSummaryAsync(int clientId, string name, int trainerId, int days)
        {
            var today = DateTime.Today;
            var from = today.AddDays(-(days - 1));
            var row = new ClientProgressRow { ClientID = clientId, Name = name };

            // --- Weight and BMI: latest, and change since the first entry in the period ---
            var metricsInPeriod = await _context.HealthMetrics
                .Where(m => m.ClientID == clientId && m.MeasuredOn >= from && m.WeightKg != null)
                .OrderBy(m => m.MeasuredOn).ThenBy(m => m.MetricID)
                .ToListAsync();

            var latest = await _context.HealthMetrics
                .Where(m => m.ClientID == clientId)
                .OrderByDescending(m => m.MeasuredOn).ThenByDescending(m => m.MetricID)
                .FirstOrDefaultAsync();

            row.CurrentWeightKg = latest?.WeightKg;
            row.CurrentBmi = latest?.BMI;
            if (metricsInPeriod.Count >= 2)
                row.WeightChangeKg = metricsInPeriod.Last().WeightKg - metricsInPeriod.First().WeightKg;

            // --- Workouts in the period ---
            var workouts = await _context.WorkoutLogs
                .Where(w => w.ClientID == clientId && w.LogDate >= from && w.LogDate <= today)
                .Select(w => new { w.LogDate, w.PlanItemID, w.CaloriesBurned })
                .ToListAsync();

            row.WorkoutsLogged = workouts.Count;
            row.CaloriesBurned = workouts.Sum(w => w.CaloriesBurned ?? 0);

            // --- Last activity: newest log of any kind ---
            var lastWorkout = workouts.Select(w => (DateTime?)w.LogDate).Max();
            var lastMeal = await _context.MealLogs.Where(m => m.ClientID == clientId).MaxAsync(m => (DateTime?)m.LogDate);
            var lastMetric = latest?.MeasuredOn;
            row.LastActivity = new[] { lastWorkout, lastMeal, lastMetric }.Where(d => d.HasValue).Max();

            // --- Plan completion (this trainer's active plan) ---
            var plan = await _context.WorkoutPlans
                .Include(p => p.Items)
                .Where(p => p.ClientID == clientId && p.TrainerID == trainerId && p.IsActive)
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();

            if (plan != null && plan.Items.Count > 0)
            {
                var start = plan.StartDate > from ? plan.StartDate : from;
                var end = plan.EndDate.HasValue && plan.EndDate.Value < today ? plan.EndDate.Value : today;

                // Count how many plan exercises fell on the days in the period
                for (var day = start.Date; day <= end.Date; day = day.AddDays(1))
                {
                    row.PlanItemsScheduled += plan.Items.Count(i => i.DayOfWeek == (byte)day.DayOfWeek);
                }

                // Each plan exercise counts once per day, even if logged twice
                var itemIds = plan.Items.Select(i => i.PlanItemID).ToHashSet();
                row.PlanItemsDone = workouts
                    .Where(w => w.PlanItemID.HasValue && itemIds.Contains(w.PlanItemID.Value) && w.LogDate >= start)
                    .Select(w => new { w.PlanItemID, w.LogDate })
                    .Distinct()
                    .Count();

                row.PlanItemsDone = Math.Min(row.PlanItemsDone, row.PlanItemsScheduled);
            }

            return row;
        }

        #endregion

        /// <summary>
        /// GET /trainer/progress?days=30 - progress overview of all the trainer's clients.
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> ClientProgress(int days = 30)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            days = NormalisePeriod(days);
            var clients = await GetAssignedClientsAsync(trainerId.Value);

            var model = new TrainerProgressViewModel { Days = days };
            foreach (var client in clients.OrderBy(c => c.Value))
            {
                model.Rows.Add(await BuildSummaryAsync(client.Key, client.Value, trainerId.Value, days));
            }

            return View(model);
        }

        /// <summary>
        /// GET /trainer/progress/{clientId}?days=30 - one client's charts, logs and feedback.
        /// </summary>
        [HttpGet("{clientId:int}")]
        public async Task<IActionResult> Client(int clientId, int days = 30)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var clients = await GetAssignedClientsAsync(trainerId.Value);
            if (!clients.TryGetValue(clientId, out var name))
                return NotFound();   // not this trainer's client

            return View(await BuildClientViewModelAsync(clientId, name, trainerId.Value, NormalisePeriod(days)));
        }

        /// <summary>
        /// POST /trainer/progress/{clientId}/feedback - saves feedback for the client
        /// and notifies them. It appears on the client's dashboard.
        /// </summary>
        [HttpPost("{clientId:int}/feedback")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFeedback(int clientId, [Bind(Prefix = "NewFeedback")] FeedbackInput input, int days = 30)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var clients = await GetAssignedClientsAsync(trainerId.Value);
            if (!clients.TryGetValue(clientId, out var name))
                return NotFound();

            if (!ModelState.IsValid)
            {
                var model = await BuildClientViewModelAsync(clientId, name, trainerId.Value, NormalisePeriod(days));
                model.NewFeedback = input;
                return View(nameof(Client), model);
            }

            try
            {
                _context.TrainerFeedback.Add(new TrainerFeedback
                {
                    TrainerID = trainerId.Value,
                    ClientID = clientId,
                    Message = input.Message!.Trim(),
                    CreatedAt = DateTime.Now
                });
                await _context.SaveChangesAsync();

                try
                {
                    await _notifications.CreateNotificationAsync(clientId, "Feedback",
                        "New feedback from your trainer", input.Message!.Trim());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not notify client {ClientId}", clientId);
                }

                TempData["SuccessMessage"] = $"Feedback sent to {name}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving feedback");
                TempData["ErrorMessage"] = "Sorry, the feedback could not be saved.";
            }

            return RedirectToAction(nameof(Client), new { clientId, days });
        }

        /// <summary>Loads charts, recent logs and feedback history for one client.</summary>
        private async Task<TrainerClientProgressViewModel> BuildClientViewModelAsync(int clientId, string name, int trainerId, int days)
        {
            var from = DateTime.Today.AddDays(-(days - 1));
            var model = new TrainerClientProgressViewModel
            {
                Days = days,
                Summary = await BuildSummaryAsync(clientId, name, trainerId, days)
            };

            var metrics = await _context.HealthMetrics
                .Where(m => m.ClientID == clientId && m.MeasuredOn >= from)
                .OrderBy(m => m.MeasuredOn).ThenBy(m => m.MetricID)
                .ToListAsync();

            foreach (var m in metrics)
            {
                model.ChartDates.Add(m.MeasuredOn.ToString("dd MMM"));
                model.ChartWeights.Add(m.WeightKg);
                model.ChartBmis.Add(m.BMI);
            }

            model.RecentWorkouts = await _context.WorkoutLogs
                .Include(w => w.Exercise)
                .Where(w => w.ClientID == clientId && w.LogDate >= from)
                .OrderByDescending(w => w.LogDate).ThenByDescending(w => w.WorkoutLogID)
                .Take(15)
                .ToListAsync();

            model.RecentMeals = await _context.MealLogs
                .Where(m => m.ClientID == clientId && m.LogDate >= from)
                .OrderByDescending(m => m.LogDate).ThenByDescending(m => m.MealLogID)
                .Take(15)
                .ToListAsync();

            model.Feedback = await _context.TrainerFeedback
                .Where(f => f.ClientID == clientId && f.TrainerID == trainerId)
                .OrderByDescending(f => f.CreatedAt)
                .Take(10)
                .ToListAsync();

            return model;
        }
    }
}
