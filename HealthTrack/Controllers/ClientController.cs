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
    /// Client module: everything a logged-in client can see and do.
    ///
    /// Security rules applied in every action:
    ///  - Only users in the "Client" role can reach this controller ([Authorize]).
    ///  - Every query is filtered by the logged-in client's own ClientID,
    ///    so a client can never see another client's data, even by
    ///    changing an ID in the URL.
    ///
    /// Connection to the Trainer module:
    ///  - READS what trainers write: WorkoutPlans, DietPlans, TrainerFeedback,
    ///    appointment status/notes, and the TrainerClientAssignments link.
    ///  - WRITES what trainers read: WorkoutLogs, MealLogs, HealthMetrics,
    ///    and appointment requests.
    /// </summary>
    [Authorize(Roles = "Client")]
    [Route("client")]
    public class ClientController : Controller
    {
        private readonly HealthTrackContext _context;
        private readonly ILogger<ClientController> _logger;
        private readonly INotificationService _notifications;

        public ClientController(HealthTrackContext context, ILogger<ClientController> logger,
                                INotificationService notifications)
        {
            _context = context;
            _logger = logger;
            _notifications = notifications;
        }

        #region Helpers

        /// <summary>
        /// Returns the UserID of the logged-in user, read from the login cookie.
        /// Returns 0 if it cannot be read.
        /// </summary>
        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        /// <summary>
        /// Loads the ClientProfile that belongs to the logged-in user.
        /// All other client data is looked up using this profile's ClientID,
        /// which is what the trainer side stores when it creates plans.
        /// </summary>
        private async Task<ClientProfile?> GetCurrentClientAsync()
        {
            var userId = GetCurrentUserId();
            return await _context.ClientProfiles
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserID == userId || c.ClientProfileID == userId);
        }

        #endregion

        #region Dashboard (Task 2)

        /// <summary>
        /// GET /client - the Client Dashboard.
        /// Shows health stats, today's workout, diet plan, upcoming
        /// appointments, assigned trainer and recent trainer feedback.
        /// </summary>
        [HttpGet("")]
        [HttpGet("dashboard")]
        public async Task<IActionResult> Index()
        {
            var model = new ClientDashboardViewModel();

            try
            {
                var client = await GetCurrentClientAsync();
                if (client == null)
                {
                    model.ErrorMessage = "Your client profile could not be found. Please contact the administrator.";
                    return View(model);
                }

                int clientId = client.ClientProfileID;
                var today = DateTime.Today;
                var now = DateTime.Now;

                // Start of this week (Monday)
                int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                var weekStart = today.AddDays(-daysSinceMonday);

                // Read the name directly from the logged-in user's account
                var currentUser = await _context.Users.FindAsync(GetCurrentUserId());
                model.FirstName = currentUser?.FirstName ?? "there";

                // --- Health stats: latest two measurements, to show the trend ---
                var lastTwoMetrics = await _context.HealthMetrics
                    .Where(m => m.ClientID == clientId)
                    .OrderByDescending(m => m.MeasuredOn)
                    .ThenByDescending(m => m.MetricID)
                    .Take(2)
                    .ToListAsync();

                model.LatestMetric = lastTwoMetrics.FirstOrDefault();
                if (lastTwoMetrics.Count == 2 &&
                    lastTwoMetrics[0].WeightKg.HasValue &&
                    lastTwoMetrics[1].WeightKg.HasValue)
                {
                    model.WeightChangeKg = lastTwoMetrics[0].WeightKg - lastTwoMetrics[1].WeightKg;
                }

                // --- Assigned trainer (the client-trainer link) ---
                var assignment = await _context.TrainerClientAssignments
                    .Include(a => a.Trainer).ThenInclude(t => t!.User)
                    .Where(a => a.ClientID == clientId && a.IsActive)
                    .OrderByDescending(a => a.AssignedAt)
                    .FirstOrDefaultAsync();

                if (assignment?.Trainer?.User != null)
                {
                    model.TrainerName = $"{assignment.Trainer.User.FirstName} {assignment.Trainer.User.LastName}";
                }

                // --- Active workout plan (written by the trainer) and today's exercises ---
                model.ActiveWorkoutPlan = await _context.WorkoutPlans
                    .Include(p => p.Items).ThenInclude(i => i.Exercise)
                    .Where(p => p.ClientID == clientId && p.IsActive
                                && p.StartDate <= today
                                && (p.EndDate == null || p.EndDate >= today))
                    .OrderByDescending(p => p.StartDate)
                    .FirstOrDefaultAsync();

                if (model.ActiveWorkoutPlan != null)
                {
                    byte todayNumber = (byte)today.DayOfWeek; // 0 = Sunday ... 6 = Saturday
                    model.TodaysExercises = model.ActiveWorkoutPlan.Items
                        .Where(i => i.DayOfWeek == todayNumber)
                        .OrderBy(i => i.ReminderTime)
                        .ToList();
                }

                // --- Active diet plan (written by the trainer) ---
                model.ActiveDietPlan = await _context.DietPlans
                    .Include(p => p.Meals)
                    .Where(p => p.ClientID == clientId && p.IsActive
                                && p.StartDate <= today
                                && (p.EndDate == null || p.EndDate >= today))
                    .OrderByDescending(p => p.StartDate)
                    .FirstOrDefaultAsync();

                // --- Activity summary from the client's own logs ---
                model.CaloriesEatenToday = await _context.MealLogs
                    .Where(m => m.ClientID == clientId && m.LogDate == today)
                    .SumAsync(m => m.Calories ?? 0);

                var weekWorkouts = _context.WorkoutLogs
                    .Where(w => w.ClientID == clientId && w.LogDate >= weekStart && w.LogDate <= today);

                model.WorkoutsThisWeek = await weekWorkouts.CountAsync();
                model.CaloriesBurnedThisWeek = await weekWorkouts.SumAsync(w => w.CaloriesBurned ?? 0);

                // --- Upcoming appointments (status set by the trainer) ---
                model.UpcomingAppointments = await _context.Appointments
                    .Include(a => a.Trainer).ThenInclude(t => t!.User)
                    .Where(a => a.ClientID == clientId
                                && a.StartTime >= now
                                && a.Status != "Cancelled")
                    .OrderBy(a => a.StartTime)
                    .Take(3)
                    .ToListAsync();

                // --- Latest trainer feedback ---
                model.RecentFeedback = await _context.TrainerFeedback
                    .Include(f => f.Trainer).ThenInclude(t => t!.User)
                    .Where(f => f.ClientID == clientId)
                    .OrderByDescending(f => f.CreatedAt)
                    .Take(3)
                    .ToListAsync();

                // --- Unread notifications (notifications are stored per UserID) ---
                var userId = GetCurrentUserId();
                model.UnreadNotifications = await _context.Notifications
                    .CountAsync(n => n.UserID == userId && !n.IsRead);
            }
            catch (Exception ex)
            {
                // Log the full error for developers, but show a friendly message to the user
                _logger.LogError(ex, "Error loading client dashboard");
                model.ErrorMessage = "Sorry, the dashboard could not be loaded. Please try again later.";
            }

            return View(model);
        }

        #endregion

        #region Progress & Health Metrics (Task 5 logging + Task 7 trends)

        /// <summary>
        /// GET /client/progress - shows the add-measurement form,
        /// weight/BMI charts, calories chart and measurement history.
        /// </summary>
        [HttpGet("progress")]
        public async Task<IActionResult> Progress()
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            var model = await BuildProgressViewModelAsync(client);
            model.NewMetric.HeightCm = client.HeightCm; // pre-fill saved height
            return View(model);
        }

        /// <summary>
        /// POST /client/progress - saves a new health measurement.
        /// BMI is calculated on the server from weight and height,
        /// so the client cannot type in a wrong BMI.
        /// </summary>
        [HttpPost("progress")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMetric([Bind(Prefix = "NewMetric")] HealthMetricInput input)
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            // Invalid input: show the page again with the error messages
            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildProgressViewModelAsync(client);
                invalidModel.NewMetric = input;
                return View(nameof(Progress), invalidModel);
            }

            try
            {
                // Save a new height to the profile if the client entered one
                if (input.HeightCm.HasValue)
                {
                    client.HeightCm = input.HeightCm;
                }

                var metric = new HealthMetric
                {
                    ClientID = client.ClientProfileID,   // always the logged-in client
                    MeasuredOn = input.MeasuredOn.Date,
                    WeightKg = input.WeightKg,
                    BMI = CalculateBmi(input.WeightKg, client.HeightCm),
                    BloodPressure = string.IsNullOrWhiteSpace(input.BloodPressure) ? null : input.BloodPressure.Trim(),
                    HeartRate = input.HeartRate,
                    BloodSugar = input.BloodSugar,
                    BodyFatPercent = input.BodyFatPercent
                };

                _context.HealthMetrics.Add(metric);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Measurement saved.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving health metric");
                TempData["ErrorMessage"] = "Sorry, the measurement could not be saved. Please try again.";
            }

            // Redirect after POST, so refreshing the page does not save it twice
            return RedirectToAction(nameof(Progress));
        }

        /// <summary>
        /// POST /client/progress/delete/{id} - deletes one of the client's own measurements.
        /// The ownership check stops a client deleting someone else's record
        /// by changing the id.
        /// </summary>
        [HttpPost("progress/delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMetric(int id)
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            var metric = await _context.HealthMetrics
                .FirstOrDefaultAsync(m => m.MetricID == id && m.ClientID == client.ClientProfileID);

            if (metric == null)
            {
                // Either it doesn't exist or it belongs to another client
                TempData["ErrorMessage"] = "Measurement not found.";
                return RedirectToAction(nameof(Progress));
            }

            _context.HealthMetrics.Remove(metric);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Measurement deleted.";
            return RedirectToAction(nameof(Progress));
        }

        /// <summary>
        /// BMI = weight (kg) / (height (m) x height (m)), rounded to 1 decimal place.
        /// Returns null if weight or height is missing.
        /// </summary>
        private static decimal? CalculateBmi(decimal? weightKg, decimal? heightCm)
        {
            if (!weightKg.HasValue || !heightCm.HasValue || heightCm.Value <= 0)
                return null;

            decimal heightM = heightCm.Value / 100m;
            return Math.Round(weightKg.Value / (heightM * heightM), 1);
        }

        /// <summary>
        /// Loads the history and builds the chart data for the Progress page.
        /// Shared by the GET action and the POST action (when validation fails).
        /// </summary>
        private async Task<ClientProgressViewModel> BuildProgressViewModelAsync(ClientProfile client)
        {
            int clientId = client.ClientProfileID;
            var model = new ClientProgressViewModel { HeightCm = client.HeightCm };

            model.History = await _context.HealthMetrics
                .Where(m => m.ClientID == clientId)
                .OrderByDescending(m => m.MeasuredOn)
                .ThenByDescending(m => m.MetricID)
                .ToListAsync();

            // Weight / BMI chart: oldest to newest
            foreach (var m in model.History.AsEnumerable().Reverse())
            {
                model.ChartDates.Add(m.MeasuredOn.ToString("dd MMM"));
                model.ChartWeights.Add(m.WeightKg);
                model.ChartBmis.Add(m.BMI);
            }

            // Calories chart: totals per day for the last 14 days
            var firstDay = DateTime.Today.AddDays(-13);

            var burnedByDay = await _context.WorkoutLogs
                .Where(w => w.ClientID == clientId && w.LogDate >= firstDay)
                .GroupBy(w => w.LogDate)
                .Select(g => new { Day = g.Key, Total = g.Sum(w => w.CaloriesBurned ?? 0) })
                .ToDictionaryAsync(x => x.Day, x => x.Total);

            var eatenByDay = await _context.MealLogs
                .Where(m => m.ClientID == clientId && m.LogDate >= firstDay)
                .GroupBy(m => m.LogDate)
                .Select(g => new { Day = g.Key, Total = g.Sum(m => m.Calories ?? 0) })
                .ToDictionaryAsync(x => x.Day, x => x.Total);

            for (var day = firstDay; day <= DateTime.Today; day = day.AddDays(1))
            {
                model.CalorieDates.Add(day.ToString("dd MMM"));
                model.CaloriesBurned.Add(burnedByDay.TryGetValue(day, out var burned) ? burned : 0);
                model.CaloriesEaten.Add(eatenByDay.TryGetValue(day, out var eaten) ? eaten : 0);
            }

            return model;
        }

        #endregion

        #region My Workouts (Task 5 - view trainer's plan + log workouts)

        /// <summary>
        /// GET /client/workouts - shows the trainer's active plan as a weekly
        /// schedule, the log-a-workout form and recent workouts.
        /// Optional ?planItemId=5 pre-fills the form from a plan item
        /// (used by the "Log it" buttons on today's exercises).
        /// </summary>
        [HttpGet("workouts")]
        public async Task<IActionResult> Workouts(int? planItemId)
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            var model = await BuildWorkoutsViewModelAsync(client);

            // Pre-fill the form from the chosen plan item, but only if it is in THIS client's plan
            if (planItemId.HasValue && model.ActivePlan != null)
            {
                var item = model.ActivePlan.Items.FirstOrDefault(i => i.PlanItemID == planItemId.Value);
                if (item != null)
                {
                    model.NewLog.PlanItemID = item.PlanItemID;
                    model.NewLog.ExerciseID = item.ExerciseID;
                    model.NewLog.Sets = item.Sets;
                    model.NewLog.Reps = item.Reps;
                    model.NewLog.DurationMin = item.DurationMin;
                }
            }

            return View(model);
        }

        /// <summary>
        /// POST /client/workouts - saves a workout the client has done.
        /// If calories are left blank they are estimated as
        /// duration (min) x the exercise's CaloriesPerMinute.
        /// </summary>
        [HttpPost("workouts")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogWorkout([Bind(Prefix = "NewLog")] WorkoutLogInput input)
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            int clientId = client.ClientProfileID;

            // The chosen exercise must exist
            var exercise = await _context.Exercises.FindAsync(input.ExerciseID ?? 0);
            if (exercise == null)
                ModelState.AddModelError("NewLog.ExerciseID", "Please choose a valid exercise.");

            // A linked plan item must belong to one of THIS client's plans
            if (input.PlanItemID.HasValue)
            {
                bool ownsItem = await _context.WorkoutPlanItems
                    .AnyAsync(i => i.PlanItemID == input.PlanItemID.Value
                                   && i.WorkoutPlan!.ClientID == clientId);
                if (!ownsItem)
                    input.PlanItemID = null; // ignore a tampered value
            }

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildWorkoutsViewModelAsync(client);
                invalidModel.NewLog = input;
                return View(nameof(Workouts), invalidModel);
            }

            try
            {
                // Estimate calories when the client didn't enter them
                decimal? calories = input.CaloriesBurned;
                if (calories == null && input.DurationMin.HasValue && exercise!.CaloriesPerMinute.HasValue)
                {
                    calories = Math.Round(input.DurationMin.Value * exercise.CaloriesPerMinute.Value, 0);
                }

                var log = new WorkoutLog
                {
                    ClientID = clientId,              // always the logged-in client
                    ExerciseID = exercise!.ExerciseID,
                    PlanItemID = input.PlanItemID,
                    LogDate = input.LogDate.Date,
                    Sets = input.Sets,
                    Reps = input.Reps,
                    DurationMin = input.DurationMin,
                    CaloriesBurned = calories,
                    Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()
                };

                _context.WorkoutLogs.Add(log);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Workout logged: {exercise.ExerciseName}"
                    + (calories.HasValue ? $" ({calories:0} kcal)." : ".");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving workout log");
                TempData["ErrorMessage"] = "Sorry, the workout could not be saved. Please try again.";
            }

            return RedirectToAction(nameof(Workouts));
        }

        /// <summary>
        /// POST /client/workouts/delete/{id} - deletes one of the client's own workout logs.
        /// </summary>
        [HttpPost("workouts/delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWorkoutLog(int id)
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            var log = await _context.WorkoutLogs
                .FirstOrDefaultAsync(w => w.WorkoutLogID == id && w.ClientID == client.ClientProfileID);

            if (log == null)
            {
                TempData["ErrorMessage"] = "Workout not found.";
                return RedirectToAction(nameof(Workouts));
            }

            _context.WorkoutLogs.Remove(log);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Workout deleted.";
            return RedirectToAction(nameof(Workouts));
        }

        /// <summary>
        /// Loads the active plan, exercise list and recent logs for the My Workouts page.
        /// </summary>
        private async Task<ClientWorkoutsViewModel> BuildWorkoutsViewModelAsync(ClientProfile client)
        {
            int clientId = client.ClientProfileID;
            var today = DateTime.Today;
            var model = new ClientWorkoutsViewModel();

            model.ActivePlan = await _context.WorkoutPlans
                .Include(p => p.Items).ThenInclude(i => i.Exercise)
                .Include(p => p.Trainer).ThenInclude(t => t!.User)
                .Where(p => p.ClientID == clientId && p.IsActive
                            && p.StartDate <= today
                            && (p.EndDate == null || p.EndDate >= today))
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();

            if (model.ActivePlan?.Trainer?.User != null)
            {
                var u = model.ActivePlan.Trainer.User;
                model.PlanTrainerName = $"{u.FirstName} {u.LastName}";
            }

            // Which plan items has the client already logged today?
            var doneIds = await _context.WorkoutLogs
                .Where(w => w.ClientID == clientId && w.LogDate == today && w.PlanItemID != null)
                .Select(w => w.PlanItemID!.Value)
                .ToListAsync();
            model.PlanItemsDoneToday = doneIds.ToHashSet();

            model.Exercises = await _context.Exercises
                .OrderBy(e => e.Category).ThenBy(e => e.ExerciseName)
                .ToListAsync();

            model.RecentLogs = await _context.WorkoutLogs
                .Include(w => w.Exercise)
                .Where(w => w.ClientID == clientId)
                .OrderByDescending(w => w.LogDate)
                .ThenByDescending(w => w.WorkoutLogID)
                .Take(20)
                .ToListAsync();

            return model;
        }

        #endregion

        #region Diet Plans (Task 5 - view trainer's diet plan + log meals)

        /// <summary>
        /// GET /client/diet-plans - shows the trainer's diet plan, today's
        /// intake against the calorie target, the log-a-meal form and history.
        /// Optional ?mealId=5 pre-fills the form from a planned meal.
        /// </summary>
        [HttpGet("diet-plans")]
        public async Task<IActionResult> DietPlans(int? mealId)
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            var model = await BuildDietViewModelAsync(client);

            // Pre-fill from a planned meal, but only if it is in THIS client's plan
            if (mealId.HasValue && model.ActivePlan != null)
            {
                var meal = model.ActivePlan.Meals.FirstOrDefault(m => m.DietMealID == mealId.Value);
                if (meal != null)
                {
                    model.NewMeal.MealType = meal.MealType;
                    model.NewMeal.FoodName = meal.MealName;
                    model.NewMeal.Calories = meal.Calories;
                }
            }

            return View(model);
        }

        /// <summary>
        /// POST /client/diet-plans - saves a meal the client has eaten.
        /// </summary>
        [HttpPost("diet-plans")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogMeal([Bind(Prefix = "NewMeal")] MealLogInput input)
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            if (!ModelState.IsValid)
            {
                var invalidModel = await BuildDietViewModelAsync(client);
                invalidModel.NewMeal = input;
                return View(nameof(DietPlans), invalidModel);
            }

            try
            {
                var log = new MealLog
                {
                    ClientID = client.ClientProfileID,   // always the logged-in client
                    LogDate = input.LogDate.Date,
                    MealType = input.MealType!,
                    FoodName = input.FoodName!.Trim(),
                    Calories = input.Calories,
                    ProteinG = input.ProteinG,
                    CarbsG = input.CarbsG,
                    FatG = input.FatG
                };

                _context.MealLogs.Add(log);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"{log.MealType} logged: {log.FoodName}"
                    + (log.Calories.HasValue ? $" ({log.Calories} kcal)." : ".");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving meal log");
                TempData["ErrorMessage"] = "Sorry, the meal could not be saved. Please try again.";
            }

            return RedirectToAction(nameof(DietPlans));
        }

        /// <summary>
        /// POST /client/diet-plans/delete/{id} - deletes one of the client's own meal logs.
        /// </summary>
        [HttpPost("diet-plans/delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMealLog(int id)
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            var log = await _context.MealLogs
                .FirstOrDefaultAsync(m => m.MealLogID == id && m.ClientID == client.ClientProfileID);

            if (log == null)
            {
                TempData["ErrorMessage"] = "Meal not found.";
                return RedirectToAction(nameof(DietPlans));
            }

            _context.MealLogs.Remove(log);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Meal deleted.";
            return RedirectToAction(nameof(DietPlans));
        }

        /// <summary>
        /// Loads the active diet plan, today's meals with totals and earlier meals.
        /// </summary>
        private async Task<ClientDietViewModel> BuildDietViewModelAsync(ClientProfile client)
        {
            int clientId = client.ClientProfileID;
            var today = DateTime.Today;
            var model = new ClientDietViewModel();

            model.ActivePlan = await _context.DietPlans
                .Include(p => p.Meals)
                .Include(p => p.Trainer).ThenInclude(t => t!.User)
                .Where(p => p.ClientID == clientId && p.IsActive
                            && p.StartDate <= today
                            && (p.EndDate == null || p.EndDate >= today))
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();

            if (model.ActivePlan?.Trainer?.User != null)
            {
                var u = model.ActivePlan.Trainer.User;
                model.PlanTrainerName = $"{u.FirstName} {u.LastName}";
            }

            model.TodaysMeals = await _context.MealLogs
                .Where(m => m.ClientID == clientId && m.LogDate == today)
                .OrderBy(m => m.MealLogID)
                .ToListAsync();

            // Add up today's intake
            model.TodayCalories = model.TodaysMeals.Sum(m => m.Calories ?? 0);
            model.TodayProtein = model.TodaysMeals.Sum(m => m.ProteinG ?? 0);
            model.TodayCarbs = model.TodaysMeals.Sum(m => m.CarbsG ?? 0);
            model.TodayFat = model.TodaysMeals.Sum(m => m.FatG ?? 0);

            model.RecentMeals = await _context.MealLogs
                .Where(m => m.ClientID == clientId && m.LogDate < today)
                .OrderByDescending(m => m.LogDate)
                .ThenByDescending(m => m.MealLogID)
                .Take(20)
                .ToListAsync();

            return model;
        }

        #endregion

        #region Appointments (Task 6 - book, view and cancel)

        /// <summary>
        /// GET /client/appointments - booking form plus upcoming and past appointments.
        /// The status of each appointment (Pending / Approved / Cancelled ...) is set by the trainer.
        /// </summary>
        [HttpGet("appointments")]
        public async Task<IActionResult> Appointments()
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            var model = await BuildAppointmentsViewModelAsync(client);
            model.NewAppointment.TrainerID = model.AssignedTrainerID; // pre-select own trainer
            return View(model);
        }

        /// <summary>
        /// POST /client/appointments - books a new appointment with status "Pending".
        /// Checks the trainer exists and is active, and that neither the client
        /// nor the trainer already has an appointment at that time.
        /// The trainer is notified so they can approve it.
        /// </summary>
        [HttpPost("appointments")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookAppointment([Bind(Prefix = "NewAppointment")] AppointmentInput input)
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            int clientId = client.ClientProfileID;
            var model = await BuildAppointmentsViewModelAsync(client);

            // The trainer must be one of the active trainers shown in the list
            var trainer = model.Trainers.FirstOrDefault(t => t.TrainerID == input.TrainerID);
            if (input.TrainerID.HasValue && trainer == null)
                ModelState.AddModelError("NewAppointment.TrainerID", "Please choose a valid trainer.");

            if (ModelState.IsValid)
            {
                var start = input.StartDateTime!.Value;
                var end = start.AddMinutes(input.DurationMin);

                // Two time slots overlap if each one starts before the other ends
                bool clientBusy = await _context.Appointments.AnyAsync(a =>
                    a.ClientID == clientId
                    && a.Status != "Cancelled" && a.Status != "Rejected"
                    && a.StartTime < end && start < a.EndTime);
                if (clientBusy)
                    ModelState.AddModelError("", "You already have an appointment at that time.");

                bool trainerBusy = await _context.Appointments.AnyAsync(a =>
                    a.TrainerID == input.TrainerID
                    && a.Status != "Cancelled" && a.Status != "Rejected"
                    && a.StartTime < end && start < a.EndTime);
                if (trainerBusy)
                    ModelState.AddModelError("", "That trainer is already booked at that time. Please choose another time.");
            }

            if (!ModelState.IsValid)
            {
                model.NewAppointment = input;
                return View(nameof(Appointments), model);
            }

            try
            {
                var startTime = input.StartDateTime!.Value;
                var appointment = new Appointment
                {
                    ClientID = clientId,                   // always the logged-in client
                    TrainerID = input.TrainerID!.Value,
                    StartTime = startTime,
                    EndTime = startTime.AddMinutes(input.DurationMin),
                    Reason = input.Reason!.Trim(),
                    Status = "Pending",                    // the trainer approves or rejects it
                    CreatedAt = DateTime.UtcNow
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                // Tell the trainer there is a new request
                await NotifyTrainerAsync(appointment.TrainerID,
                    "New appointment request",
                    $"A client requested a session on {startTime:ddd dd MMM, hh:mm tt}: {appointment.Reason}");

                TempData["SuccessMessage"] =
                    $"Appointment requested for {startTime:dddd dd MMM, hh:mm tt}. Your trainer will confirm it soon.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error booking appointment");
                TempData["ErrorMessage"] = "Sorry, the appointment could not be booked. Please try again.";
            }

            return RedirectToAction(nameof(Appointments));
        }

        /// <summary>
        /// POST /client/appointments/cancel/{id} - the client cancels one of their own
        /// future appointments (Pending or Approved). The trainer is notified.
        /// </summary>
        [HttpPost("appointments/cancel/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            var appointment = await _context.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentID == id && a.ClientID == client.ClientProfileID);

            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return RedirectToAction(nameof(Appointments));
            }

            if ((appointment.Status != "Pending" && appointment.Status != "Approved")
                || appointment.StartTime <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "Only upcoming pending or approved appointments can be cancelled.";
                return RedirectToAction(nameof(Appointments));
            }

            try
            {
                appointment.Status = "Cancelled";
                await _context.SaveChangesAsync();

                await NotifyTrainerAsync(appointment.TrainerID,
                    "Appointment cancelled",
                    $"A client cancelled the session on {appointment.StartTime:ddd dd MMM, hh:mm tt}.");

                TempData["SuccessMessage"] = "Appointment cancelled.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling appointment {Id}", id);
                TempData["ErrorMessage"] = "Sorry, the appointment could not be cancelled. Please try again.";
            }

            return RedirectToAction(nameof(Appointments));
        }

        /// <summary>
        /// Sends an in-app notification to a trainer. TrainerID is the trainer's UserID
        /// (TrainerProfiles.TrainerID is a foreign key to Users). A failed notification
        /// is logged but does not stop the booking.
        /// </summary>
        private async Task NotifyTrainerAsync(int trainerId, string title, string message)
        {
            try
            {
                await _notifications.CreateNotificationAsync(trainerId, "Appointment", title, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not notify trainer {TrainerId}", trainerId);
            }
        }

        /// <summary>
        /// Loads the trainer list, the assigned trainer and the client's appointments.
        /// </summary>
        private async Task<ClientAppointmentsViewModel> BuildAppointmentsViewModelAsync(ClientProfile client)
        {
            int clientId = client.ClientProfileID;
            var now = DateTime.Now;
            var model = new ClientAppointmentsViewModel();

            // All trainer profiles with their user account. TrainerID = the trainer's UserID.
            var allTrainers = await (
                from t in _context.TrainerProfiles
                join u in _context.Users on t.TrainerProfileID equals u.UserID
                orderby u.FirstName
                select new { t.TrainerProfileID, u.FirstName, u.LastName, u.IsActive, t.Specialization }
            ).ToListAsync();

            model.TrainerNames = allTrainers.ToDictionary(
                t => t.TrainerProfileID, t => $"{t.FirstName} {t.LastName}");

            model.Trainers = allTrainers
                .Where(t => t.IsActive)
                .Select(t => new TrainerOption
                {
                    TrainerID = t.TrainerProfileID,
                    Name = $"{t.FirstName} {t.LastName}",
                    Specialization = t.Specialization
                })
                .ToList();

            model.AssignedTrainerID = await _context.TrainerClientAssignments
                .Where(a => a.ClientID == clientId && a.IsActive)
                .OrderByDescending(a => a.AssignedAt)
                .Select(a => (int?)a.TrainerID)
                .FirstOrDefaultAsync();

            var appointments = await _context.Appointments
                .Where(a => a.ClientID == clientId)
                .ToListAsync();

            // Upcoming = still in the future and not cancelled/rejected/completed
            model.Upcoming = appointments
                .Where(a => a.EndTime >= now && (a.Status == "Pending" || a.Status == "Approved"))
                .OrderBy(a => a.StartTime)
                .ToList();

            model.Past = appointments
                .Except(model.Upcoming)
                .OrderByDescending(a => a.StartTime)
                .Take(20)
                .ToList();

            return model;
        }

        #endregion

        #region Profile (personal details + change password)

        /// <summary>
        /// GET /client/profile - the client's personal details and change-password form.
        /// </summary>
        [HttpGet("profile")]
        public async Task<IActionResult> Profile()
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            return View(await BuildProfileViewModelAsync(client));
        }

        /// <summary>
        /// POST /client/profile - saves the client's personal details
        /// (name on the Users table, everything else on ClientProfiles).
        /// </summary>
        [HttpPost("profile")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Details")] ProfileDetailsInput input)
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
                return RedirectToAction(nameof(Index));

            // Only validate the details form (the password form is separate)
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Password.")).ToList())
                ModelState.Remove(key);

            if (!ModelState.IsValid)
            {
                var invalid = await BuildProfileViewModelAsync(client);
                invalid.Details = input;
                return View(nameof(Profile), invalid);
            }

            try
            {
                var user = await _context.Users.FindAsync(GetCurrentUserId());
                if (user != null)
                {
                    user.FirstName = input.FirstName!.Trim();
                    user.LastName = input.LastName!.Trim();
                }

                client.Phone = Clean(input.Phone);
                client.DateOfBirth = input.DateOfBirth?.Date;
                client.Gender = Clean(input.Gender);
                client.HeightCm = input.HeightCm;
                client.AddressLine1 = Clean(input.AddressLine1);
                client.AddressLine2 = Clean(input.AddressLine2);
                client.City = Clean(input.City);
                client.StateProvince = Clean(input.StateProvince);
                client.PostalCode = Clean(input.PostalCode);
                client.GoalDescription = Clean(input.GoalDescription);
                client.MedicalNotes = Clean(input.MedicalNotes);

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Your profile has been updated.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating client profile");
                TempData["ErrorMessage"] = "Sorry, your profile could not be saved. Please try again.";
            }

            return RedirectToAction(nameof(Profile));
        }

        /// <summary>
        /// POST /client/profile/password - changes the password.
        /// The current password must be correct; the new one is stored as a
        /// salted hash using the project's PasswordHasher (never as plain text).
        /// </summary>
        [HttpPost("profile/password")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordInput input)
        {
            var client = await GetCurrentClientAsync();
            var user = await _context.Users.FindAsync(GetCurrentUserId());
            if (client == null || user == null)
                return RedirectToAction(nameof(Index));

            // Only validate the password form
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Details.")).ToList())
                ModelState.Remove(key);

            if (ModelState.IsValid
                && !PasswordHasher.VerifyPassword(input.CurrentPassword!, user.PasswordHash, user.PasswordSalt))
            {
                ModelState.AddModelError("Password.CurrentPassword", "Your current password is incorrect.");
            }

            if (ModelState.IsValid && input.NewPassword == input.CurrentPassword)
            {
                ModelState.AddModelError("Password.NewPassword", "The new password must be different from the current one.");
            }

            if (!ModelState.IsValid)
            {
                var invalid = await BuildProfileViewModelAsync(client);
                // Never send passwords back to the browser
                return View(nameof(Profile), invalid);
            }

            try
            {
                PasswordHasher.CreatePasswordHash(input.NewPassword!, out var hash, out var salt);
                user.PasswordHash = hash;
                user.PasswordSalt = salt;
                await _context.SaveChangesAsync();

                await _notifications.CreateNotificationAsync(user.UserID, "System",
                    "Password changed", "Your password was changed. If this wasn't you, contact the administrator.");

                TempData["SuccessMessage"] = "Your password has been changed.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing client password");
                TempData["ErrorMessage"] = "Sorry, your password could not be changed. Please try again.";
            }

            return RedirectToAction(nameof(Profile));
        }

        /// <summary>Trims text and turns empty strings into null before saving.</summary>
        private static string? Clean(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        /// <summary>Loads the profile page data from the Users and ClientProfiles tables.</summary>
        private async Task<ClientProfilePageViewModel> BuildProfileViewModelAsync(ClientProfile client)
        {
            var user = await _context.Users.FindAsync(GetCurrentUserId());

            var trainerName = await (
                from a in _context.TrainerClientAssignments
                join u in _context.Users on a.TrainerID equals u.UserID
                where a.ClientID == client.ClientProfileID && a.IsActive
                orderby a.AssignedAt descending
                select u.FirstName + " " + u.LastName
            ).FirstOrDefaultAsync();

            return new ClientProfilePageViewModel
            {
                Email = user?.Email ?? "",
                MemberSince = user?.CreatedAt ?? DateTime.Today,
                TrainerName = trainerName,
                Details = new ProfileDetailsInput
                {
                    FirstName = user?.FirstName,
                    LastName = user?.LastName,
                    Phone = client.Phone,
                    DateOfBirth = client.DateOfBirth,
                    Gender = client.Gender,
                    HeightCm = client.HeightCm,
                    AddressLine1 = client.AddressLine1,
                    AddressLine2 = client.AddressLine2,
                    City = client.City,
                    StateProvince = client.StateProvince,
                    PostalCode = client.PostalCode,
                    GoalDescription = client.GoalDescription,
                    MedicalNotes = client.MedicalNotes
                }
            };
        }

        #endregion
    }
}
