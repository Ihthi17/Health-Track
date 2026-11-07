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
    /// Trainer side of Diet Management (Task 5) - connects the Trainer module
    /// to the Client module.
    ///
    /// The trainer creates DietPlans + DietPlanMeals here for clients assigned to
    /// them. The client reads the same rows on /client/diet-plans and the client
    /// dashboard, and logs the meals they actually eat (MealLogs).
    ///
    /// Security: only trainers can use this controller, a trainer can only create
    /// plans for their own assigned clients, and can only open/change their own plans.
    /// </summary>
    [Authorize(Roles = "Trainer")]
    [Route("trainer/diets")]
    public class TrainerDietPlansController : Controller
    {
        private readonly HealthTrackContext _context;
        private readonly ILogger<TrainerDietPlansController> _logger;
        private readonly INotificationService _notifications;

        public TrainerDietPlansController(HealthTrackContext context,
                                          ILogger<TrainerDietPlansController> logger,
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

        /// <summary>Loads a diet plan with its meals ONLY if this trainer created it.</summary>
        private async Task<DietPlan?> GetOwnPlanAsync(int planId, int trainerId)
        {
            return await _context.DietPlans
                .Include(p => p.Meals)
                .FirstOrDefaultAsync(p => p.DietPlanID == planId && p.TrainerID == trainerId);
        }

        /// <summary>Keeps ONE active diet plan per client from this trainer.</summary>
        private async Task DeactivateOtherPlansAsync(int clientId, int trainerId, int keepPlanId)
        {
            var others = await _context.DietPlans
                .Where(p => p.ClientID == clientId && p.TrainerID == trainerId
                            && p.IsActive && p.DietPlanID != keepPlanId)
                .ToListAsync();

            foreach (var p in others)
                p.IsActive = false;
        }

        /// <summary>Sends the client an in-app notification (ClientID = the client's UserID).</summary>
        private async Task NotifyClientAsync(int clientId, string title, string message)
        {
            try
            {
                await _notifications.CreateNotificationAsync(clientId, "Diet", title, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not notify client {ClientId}", clientId);
            }
        }

        /// <summary>Builds the details page model for a diet plan.</summary>
        private async Task<DietPlanDetailsViewModel> BuildDetailsViewModelAsync(DietPlan plan)
        {
            var clientName = await _context.Users
                .Where(u => u.UserID == plan.ClientID)
                .Select(u => u.FirstName + " " + u.LastName)
                .FirstOrDefaultAsync() ?? "Client";

            return new DietPlanDetailsViewModel
            {
                Plan = plan,
                ClientName = clientName,
                Edit = new DietPlanInput
                {
                    ClientID = plan.ClientID,
                    PlanName = plan.PlanName,
                    DailyCalories = plan.DailyCalories,
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
        /// GET /trainer/diets - the trainer's real diet plans.
        /// Optional ?clientId= shows one client's plans only.
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> DietPlans(int? clientId)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var clients = await GetAssignedClientsAsync(trainerId.Value);
            var assignedIds = clients.Select(c => c.ClientID).ToList();

            var query = _context.DietPlans
                .Include(p => p.Meals)
                .Where(p => p.TrainerID == trainerId.Value && assignedIds.Contains(p.ClientID));

            if (clientId.HasValue)
                query = query.Where(p => p.ClientID == clientId.Value);

            var model = new TrainerDietPlansViewModel
            {
                Plans = await query.OrderByDescending(p => p.IsActive).ThenByDescending(p => p.StartDate).ToListAsync(),
                Clients = clients,
                SelectedClientId = clientId,
                ClientNames = clients.ToDictionary(c => c.ClientID, c => c.Name)
            };

            return View(model);
        }

        /// <summary>
        /// GET /trainer/clients/{clientId}/diets - used by the "Diet Plans" button
        /// on the My Clients page; shows that client's plans.
        /// </summary>
        [HttpGet("/trainer/clients/{clientId:int}/diets")]
        public IActionResult ForClient(int clientId)
        {
            return RedirectToAction(nameof(DietPlans), new { clientId });
        }

        #endregion

        #region Create

        /// <summary>GET /trainer/diets/create - new diet plan form (?clientId= pre-selects a client).</summary>
        [HttpGet("create")]
        public async Task<IActionResult> Create(int? clientId)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var model = new CreateDietPlanViewModel
            {
                Clients = await GetAssignedClientsAsync(trainerId.Value)
            };
            model.Input.ClientID = clientId;
            return View(model);
        }

        /// <summary>POST /trainer/diets/create - saves the plan, then opens it to add meals.</summary>
        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind(Prefix = "Input")] DietPlanInput input)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var clients = await GetAssignedClientsAsync(trainerId.Value);

            // Security: the client must be assigned to this trainer
            if (input.ClientID.HasValue && !clients.Any(c => c.ClientID == input.ClientID.Value))
                ModelState.AddModelError("Input.ClientID", "You can only create plans for your own clients.");

            if (!ModelState.IsValid)
                return View(new CreateDietPlanViewModel { Input = input, Clients = clients });

            try
            {
                var plan = new DietPlan
                {
                    TrainerID = trainerId.Value,
                    ClientID = input.ClientID!.Value,
                    PlanName = input.PlanName!.Trim(),
                    DailyCalories = input.DailyCalories,
                    StartDate = input.StartDate!.Value.Date,
                    EndDate = input.EndDate?.Date,
                    Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim(),
                    IsActive = input.IsActive
                };

                _context.DietPlans.Add(plan);
                await _context.SaveChangesAsync();

                if (plan.IsActive)
                {
                    await DeactivateOtherPlansAsync(plan.ClientID, plan.TrainerID, plan.DietPlanID);
                    await _context.SaveChangesAsync();

                    await NotifyClientAsync(plan.ClientID, "New diet plan",
                        $"Your trainer created a new diet plan: {plan.PlanName}.");
                }

                TempData["SuccessMessage"] = "Diet plan created. Now add the meals.";
                return RedirectToAction(nameof(Details), new { id = plan.DietPlanID });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating diet plan");
                ModelState.AddModelError("", "Sorry, the plan could not be saved. Please try again.");
                return View(new CreateDietPlanViewModel { Input = input, Clients = clients });
            }
        }

        #endregion

        #region Details, edit and meals

        /// <summary>GET /trainer/diets/{id} - edit the plan and manage its meals.</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var plan = await GetOwnPlanAsync(id, trainerId.Value);
            if (plan == null)
                return NotFound();

            return View(await BuildDetailsViewModelAsync(plan));
        }

        /// <summary>POST /trainer/diets/{id}/edit - updates the plan details.</summary>
        [HttpPost("{id:int}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind(Prefix = "Edit")] DietPlanInput input)
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
                var model = await BuildDetailsViewModelAsync(plan);
                model.Edit = input;
                return View(nameof(Details), model);
            }

            bool becameActive = input.IsActive && !plan.IsActive;

            plan.PlanName = input.PlanName!.Trim();
            plan.DailyCalories = input.DailyCalories;
            plan.StartDate = input.StartDate!.Value.Date;
            plan.EndDate = input.EndDate?.Date;
            plan.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
            plan.IsActive = input.IsActive;

            if (plan.IsActive)
                await DeactivateOtherPlansAsync(plan.ClientID, plan.TrainerID, plan.DietPlanID);

            await _context.SaveChangesAsync();

            if (becameActive)
            {
                await NotifyClientAsync(plan.ClientID, "Diet plan updated",
                    $"Your trainer activated the diet plan: {plan.PlanName}.");
            }

            TempData["SuccessMessage"] = "Plan details saved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>POST /trainer/diets/{id}/meals - adds a meal to the plan.</summary>
        [HttpPost("{id:int}/meals")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMeal(int id, [Bind(Prefix = "NewMeal")] PlanMealInput input)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var plan = await GetOwnPlanAsync(id, trainerId.Value);
            if (plan == null)
                return NotFound();

            // Same four meal types the client uses when logging meals
            if (input.MealType != null && !MealLogInput.MealTypes.Contains(input.MealType))
                ModelState.AddModelError("NewMeal.MealType", "Please choose a valid meal type.");

            if (!ModelState.IsValid)
            {
                var model = await BuildDetailsViewModelAsync(plan);
                model.NewMeal = input;
                return View(nameof(Details), model);
            }

            _context.DietPlanMeals.Add(new DietPlanMeal
            {
                DietPlanID = plan.DietPlanID,
                MealType = input.MealType!,
                MealName = input.MealName!.Trim(),
                Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
                Calories = input.Calories,
                MealTime = input.MealTime
            });
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Meal added to the plan.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>POST /trainer/diets/{id}/meals/{mealId}/delete - removes a meal from the plan.</summary>
        [HttpPost("{id:int}/meals/{mealId:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMeal(int id, int mealId)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return Unauthorized();

            var plan = await GetOwnPlanAsync(id, trainerId.Value);
            var meal = plan?.Meals.FirstOrDefault(m => m.DietMealID == mealId);
            if (meal == null)
                return NotFound();

            _context.DietPlanMeals.Remove(meal);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Meal removed from the plan.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// POST /trainer/diets/{id}/delete - deletes the whole plan and its meals.
        /// The client's meal logs are separate and are not affected.
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
                _context.DietPlanMeals.RemoveRange(plan.Meals);
                _context.DietPlans.Remove(plan);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Plan \"{plan.PlanName}\" deleted.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting diet plan {Id}", id);
                TempData["ErrorMessage"] = "Sorry, the plan could not be deleted.";
            }

            return RedirectToAction(nameof(DietPlans));
        }

        #endregion
    }
}
