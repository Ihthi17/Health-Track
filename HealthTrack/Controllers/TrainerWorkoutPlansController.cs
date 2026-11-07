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
    /// Trainer side of Workout Management (Task 5) - connects the Trainer module
    /// to the Client module.
    ///
    /// The trainer creates WorkoutPlans + WorkoutPlanItems here for clients assigned
    /// to them (TrainerClientAssignments). The client reads the same rows on
    /// /client/workouts and the client dashboard, and logs workouts against them.
    ///
    /// Security: only trainers can use this controller, a trainer can only create
    /// plans for their own assigned clients, and can only open/change their own plans.
    /// </summary>
    [Authorize(Roles = "Trainer")]
    [Route("trainer/workouts")]
    public class TrainerWorkoutPlansController : Controller
    {
        private readonly HealthTrackContext _context;
        private readonly ILogger<TrainerWorkoutPlansController> _logger;
        private readonly INotificationService _notifications;

        public TrainerWorkoutPlansController(HealthTrackContext context,
                                             ILogger<TrainerWorkoutPlansController> logger,
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

        /// <summary>The trainer's ACTIVE assigned clients, with names, for drop-downs.</summary>
        private async Task<List<ClientOption>> GetAssignedClientsAsync(int trainerId)
        {
            return await (
                from a in _context.TrainerClientAssignments
                join u in _context.Users on a.ClientID equals u.UserID
                where a.TrainerID == trainerId && a.IsActive
                orderby u.FirstName
                select new ClientOption { ClientID = a.ClientID, Name = u.FirstName + " " + u.LastName }
            ).ToListAsync();
        }

        /// <summary>Loads a plan with its items ONLY if this trainer created it.</summary>
        private async Task<WorkoutPlan?> GetOwnPlanAsync(int planId, int trainerId)
        {
            return await _context.WorkoutPlans
                .Include(p => p.Items).ThenInclude(i => i.Exercise)
                .FirstOrDefaultAsync(p => p.WorkoutPlanID == planId && p.TrainerID == trainerId);
        }

        /// <summary>
        /// Keeps ONE active plan per client: when a plan is made active,
        /// the client's other active plans from this trainer are switched off.
        /// </summary>
        private async Task DeactivateOtherPlansAsync(int clientId, int trainerId, int keepPlanId)
        {
            var others = await _context.WorkoutPlans
                .Where(p => p.ClientID == clientId && p.TrainerID == trainerId
                            && p.IsActive && p.WorkoutPlanID != keepPlanId)
                .ToListAsync();

            foreach (var p in others)
                p.IsActive = false;
        }

        /// <summary>Sends the client an in-app notification (ClientID = the client's UserID).</summary>
        private async Task NotifyClientAsync(int clientId, string title, string message)
        {
            try
            {
                await _notifications.CreateNotificationAsync(clientId, "Workout", title, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not notify client {ClientId}", clientId);
            }
        }

        /// <summary>Builds the details page model for a plan.</summary>
        private async Task<WorkoutPlanDetailsViewModel> BuildDetailsViewModelAsync(WorkoutPlan plan, int trainerId)
        {
            var clientName = await _context.Users
                .Where(u => u.UserID == plan.ClientID)
                .Select(u => u.FirstName + " " + u.LastName)
                .FirstOrDefaultAsync() ?? "Client";

            var itemIds = plan.Items.Select(i => i.PlanItemID).ToList();
            var timesLogged = await _context.WorkoutLogs
                .Where(w => w.PlanItemID != null && itemIds.Contains(w.PlanItemID.Value))
                .GroupBy(w => w.PlanItemID!.Value)
                .Select(g => new { ItemId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ItemId, x => x.Count);

            return new WorkoutPlanDetailsViewModel
            {
                Plan = plan,
                ClientName = clientName,
                Exercises = await _context.Exercises.OrderBy(e => e.Category).ThenBy(e => e.ExerciseName).ToListAsync(),
                Clients = await GetAssignedClientsAsync(trainerId),
                TimesLogged = timesLogged,
                Edit = new WorkoutPlanInput
                {
                    ClientID = plan.ClientID,
                    PlanName = plan.PlanName,
                    StartDate = plan.StartDate,
                    EndDate = plan.EndDate,
                    Notes = plan.Notes,
                    IsActive = plan.IsActive
                }
            };
        }

        #endregion

        #region List

        /// <summary>
        /// GET /trainer/workouts - the trainer's real workout plans.
        /// Optional ?clientId= shows one client's plans only.
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> WorkoutPlans(int? clientId)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var clients = await GetAssignedClientsAsync(trainerId.Value);
            var assignedIds = clients.Select(c => c.ClientID).ToList();

            var query = _context.WorkoutPlans
                .Include(p => p.Items).ThenInclude(i => i.Exercise)
                .Where(p => p.TrainerID == trainerId.Value && assignedIds.Contains(p.ClientID));

            if (clientId.HasValue)
                query = query.Where(p => p.ClientID == clientId.Value);

            var model = new TrainerWorkoutPlansViewModel
            {
                Plans = await query.OrderByDescending(p => p.IsActive).ThenByDescending(p => p.StartDate).ToListAsync(),
                Clients = clients,
                SelectedClientId = clientId,
                ClientNames = clients.ToDictionary(c => c.ClientID, c => c.Name)
            };

            return View(model);
        }

        /// <summary>
        /// GET /trainer/clients/{clientId}/workouts - used by the "Workout Plans"
        /// button on the My Clients page; shows that client's plans.
        /// </summary>
        [HttpGet("/trainer/clients/{clientId:int}/workouts")]
        public IActionResult ForClient(int clientId)
        {
            return RedirectToAction(nameof(WorkoutPlans), new { clientId });
        }

        #endregion

        #region Create

        /// <summary>GET /trainer/workouts/create - new plan form (?clientId= pre-selects a client).</summary>
        [HttpGet("create")]
        public async Task<IActionResult> Create(int? clientId)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var model = new CreateWorkoutPlanViewModel
            {
                Clients = await GetAssignedClientsAsync(trainerId.Value)
            };
            model.Input.ClientID = clientId;
            return View(model);
        }

        /// <summary>
        /// POST /trainer/workouts/create - saves the plan, then opens its details
        /// page so the trainer can add exercises.
        /// </summary>
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "Input")] WorkoutPlanInput input)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var clients = await GetAssignedClientsAsync(trainerId.Value);

            // Security: the client must be assigned to this trainer
            if (input.ClientID.HasValue && !clients.Any(c => c.ClientID == input.ClientID.Value))
                ModelState.AddModelError("Input.ClientID", "You can only create plans for your own clients.");

            if (!ModelState.IsValid)
                return View(new CreateWorkoutPlanViewModel { Input = input, Clients = clients });

            try
            {
                var plan = new WorkoutPlan
                {
                    TrainerID = trainerId.Value,
                    ClientID = input.ClientID!.Value,
                    PlanName = input.PlanName!.Trim(),
                    StartDate = input.StartDate!.Value.Date,
                    EndDate = input.EndDate?.Date,
                    Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
                    IsActive = input.IsActive,
                    CreatedAt = DateTime.Now
                };

                _context.WorkoutPlans.Add(plan);
                await _context.SaveChangesAsync();

                if (plan.IsActive)
                {
                    await DeactivateOtherPlansAsync(plan.ClientID, plan.TrainerID, plan.WorkoutPlanID);
                    await _context.SaveChangesAsync();

                    await NotifyClientAsync(plan.ClientID, "New workout plan",
                        $"Your trainer created a new workout plan: {plan.PlanName}.");
                }

                TempData["SuccessMessage"] = "Plan created. Now add exercises to it.";
                return RedirectToAction(nameof(Details), new { id = plan.WorkoutPlanID });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating workout plan");
                ModelState.AddModelError("", "Sorry, the plan could not be saved. Please try again.");
                return View(new CreateWorkoutPlanViewModel { Input = input, Clients = clients });
            }
        }

        #endregion

        #region Details, edit and exercises

        /// <summary>GET /trainer/workouts/{id} - edit the plan and manage its exercises.</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var plan = await GetOwnPlanAsync(id, trainerId.Value);
            if (plan == null)
                return NotFound();

            return View(await BuildDetailsViewModelAsync(plan, trainerId.Value));
        }

        /// <summary>POST /trainer/workouts/{id}/edit - updates the plan details.</summary>
        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Edit")] WorkoutPlanInput input)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var plan = await GetOwnPlanAsync(id, trainerId.Value);
            if (plan == null)
                return NotFound();

            // The client of an existing plan can't be changed
            input.ClientID = plan.ClientID;
            ModelState.Remove("Edit.ClientID");

            if (!ModelState.IsValid)
            {
                var model = await BuildDetailsViewModelAsync(plan, trainerId.Value);
                model.Edit = input;
                return View(nameof(Details), model);
            }

            bool becameActive = input.IsActive && !plan.IsActive;

            plan.PlanName = input.PlanName!.Trim();
            plan.StartDate = input.StartDate!.Value.Date;
            plan.EndDate = input.EndDate?.Date;
            plan.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
            plan.IsActive = input.IsActive;

            if (plan.IsActive)
                await DeactivateOtherPlansAsync(plan.ClientID, plan.TrainerID, plan.WorkoutPlanID);

            await _context.SaveChangesAsync();

            if (becameActive)
            {
                await NotifyClientAsync(plan.ClientID, "Workout plan updated",
                    $"Your trainer activated the workout plan: {plan.PlanName}.");
            }

            TempData["SuccessMessage"] = "Plan details saved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>POST /trainer/workouts/{id}/items - adds an exercise to the plan.</summary>
        [HttpPost("{id:int}/items")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItem(int id, [Bind(Prefix = "NewItem")] PlanItemInput input)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var plan = await GetOwnPlanAsync(id, trainerId.Value);
            if (plan == null)
                return NotFound();

            if (input.ExerciseID.HasValue && !await _context.Exercises.AnyAsync(e => e.ExerciseID == input.ExerciseID.Value))
                ModelState.AddModelError("NewItem.ExerciseID", "Please choose a valid exercise.");

            if (!ModelState.IsValid)
            {
                var model = await BuildDetailsViewModelAsync(plan, trainerId.Value);
                model.NewItem = input;
                return View(nameof(Details), model);
            }

            _context.WorkoutPlanItems.Add(new WorkoutPlanItem
            {
                WorkoutPlanID = plan.WorkoutPlanID,
                ExerciseID = input.ExerciseID!.Value,
                DayOfWeek = input.DayOfWeek,
                Sets = input.Sets,
                Reps = input.Reps,
                DurationMin = input.DurationMin,
                ReminderTime = input.ReminderTime
            });
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Exercise added to the plan.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// POST /trainer/workouts/{id}/items/{itemId}/delete - removes an exercise.
        /// Any workouts the client already logged against it are kept (just unlinked).
        /// </summary>
        [HttpPost("{id:int}/items/{itemId:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteItem(int id, int itemId)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var plan = await GetOwnPlanAsync(id, trainerId.Value);
            var item = plan?.Items.FirstOrDefault(i => i.PlanItemID == itemId);
            if (item == null)
                return NotFound();

            // Keep the client's history: unlink their logs instead of deleting them
            var logs = await _context.WorkoutLogs.Where(w => w.PlanItemID == itemId).ToListAsync();
            foreach (var log in logs)
                log.PlanItemID = null;

            _context.WorkoutPlanItems.Remove(item);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Exercise removed from the plan.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// POST /trainer/workouts/{id}/delete - deletes the whole plan.
        /// The client's logged workouts are kept (just unlinked from the plan).
        /// </summary>
        [HttpPost("{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var plan = await GetOwnPlanAsync(id, trainerId.Value);
            if (plan == null)
                return NotFound();

            try
            {
                var itemIds = plan.Items.Select(i => i.PlanItemID).ToList();
                var logs = await _context.WorkoutLogs
                    .Where(w => w.PlanItemID != null && itemIds.Contains(w.PlanItemID.Value))
                    .ToListAsync();
                foreach (var log in logs)
                    log.PlanItemID = null;

                _context.WorkoutPlanItems.RemoveRange(plan.Items);
                _context.WorkoutPlans.Remove(plan);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Plan \"{plan.PlanName}\" deleted.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting workout plan {Id}", id);
                TempData["ErrorMessage"] = "Sorry, the plan could not be deleted.";
            }

            return RedirectToAction(nameof(WorkoutPlans));
        }

        #endregion
    }
}
