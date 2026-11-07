using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    /// <summary>A client the trainer can create plans for (drop-down item).</summary>
    public class ClientOption
    {
        public int ClientID { get; set; }
        public string Name { get; set; } = "";
    }

    /// <summary>Data for the trainer's Workout Plans list page.</summary>
    public class TrainerWorkoutPlansViewModel
    {
        /// <summary>Plans for this trainer's clients (filtered by SelectedClientId if set).</summary>
        public List<WorkoutPlan> Plans { get; set; } = new();

        /// <summary>The trainer's assigned clients (for the filter and the create button).</summary>
        public List<ClientOption> Clients { get; set; } = new();

        public int? SelectedClientId { get; set; }

        /// <summary>ClientID -> client name, for showing names on the plan cards.</summary>
        public Dictionary<int, string> ClientNames { get; set; } = new();
    }

    /// <summary>
    /// The plan details form (used for create and edit).
    /// </summary>
    public class WorkoutPlanInput : IValidatableObject
    {
        [Required(ErrorMessage = "Please choose a client.")]
        [Display(Name = "Client")]
        public int? ClientID { get; set; }

        [Required(ErrorMessage = "Please give the plan a name.")]
        [MaxLength(100, ErrorMessage = "Plan name can be at most 100 characters.")]
        [Display(Name = "Plan name")]
        public string? PlanName { get; set; }

        [Required(ErrorMessage = "Please choose a start date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start date")]
        public DateTime? StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "End date (optional)")]
        public DateTime? EndDate { get; set; }

        [MaxLength(500, ErrorMessage = "Notes can be at most 500 characters.")]
        [Display(Name = "Notes for the client")]
        public string? Notes { get; set; }

        [Display(Name = "Active (the client sees this plan)")]
        public bool IsActive { get; set; } = true;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate.HasValue && EndDate.HasValue && EndDate.Value.Date < StartDate.Value.Date)
            {
                yield return new ValidationResult(
                    "The end date can't be before the start date.", new[] { nameof(EndDate) });
            }
        }
    }

    /// <summary>Data for the Create Workout Plan page.</summary>
    public class CreateWorkoutPlanViewModel
    {
        public WorkoutPlanInput Input { get; set; } = new();
        public List<ClientOption> Clients { get; set; } = new();
    }

    /// <summary>
    /// One exercise to add to a plan: which day, how much, and when to remind the client.
    /// </summary>
    public class PlanItemInput : IValidatableObject
    {
        [Required(ErrorMessage = "Please choose an exercise.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please choose an exercise.")]
        [Display(Name = "Exercise")]
        public int? ExerciseID { get; set; }

        /// <summary>0 = Sunday ... 6 = Saturday (same as .NET DayOfWeek).</summary>
        [Range(0, 6, ErrorMessage = "Please choose a day.")]
        [Display(Name = "Day")]
        public byte DayOfWeek { get; set; } = 1;

        [Range(1, 50, ErrorMessage = "Sets must be between 1 and 50.")]
        public int? Sets { get; set; }

        [Range(1, 500, ErrorMessage = "Reps must be between 1 and 500.")]
        public int? Reps { get; set; }

        [Range(1, 600, ErrorMessage = "Duration must be between 1 and 600 minutes.")]
        [Display(Name = "Minutes")]
        public int? DurationMin { get; set; }

        [DataType(DataType.Time)]
        [Display(Name = "Reminder time")]
        public TimeSpan? ReminderTime { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Sets == null && Reps == null && DurationMin == null)
            {
                yield return new ValidationResult("Please enter sets/reps or a duration.");
            }
        }
    }

    /// <summary>Data for the plan details page (edit plan + manage its exercises).</summary>
    public class WorkoutPlanDetailsViewModel
    {
        public WorkoutPlan Plan { get; set; } = null!;
        public string ClientName { get; set; } = "";
        public List<Exercise> Exercises { get; set; } = new();
        public List<ClientOption> Clients { get; set; } = new();

        public WorkoutPlanInput Edit { get; set; } = new();
        public PlanItemInput NewItem { get; set; } = new();

        /// <summary>How many times the client has logged each plan item (PlanItemID -> count).</summary>
        public Dictionary<int, int> TimesLogged { get; set; } = new();
    }

    // ===================== Diet plans =====================

    /// <summary>Data for the trainer's Diet Plans list page.</summary>
    public class TrainerDietPlansViewModel
    {
        public List<DietPlan> Plans { get; set; } = new();
        public List<ClientOption> Clients { get; set; } = new();
        public int? SelectedClientId { get; set; }
        public Dictionary<int, string> ClientNames { get; set; } = new();
    }

    /// <summary>The diet plan details form (used for create and edit).</summary>
    public class DietPlanInput : IValidatableObject
    {
        [Required(ErrorMessage = "Please choose a client.")]
        [Display(Name = "Client")]
        public int? ClientID { get; set; }

        [Required(ErrorMessage = "Please give the plan a name.")]
        [MaxLength(100, ErrorMessage = "Plan name can be at most 100 characters.")]
        [Display(Name = "Plan name")]
        public string? PlanName { get; set; }

        [Range(800, 6000, ErrorMessage = "Daily calories must be between 800 and 6000 kcal.")]
        [Display(Name = "Daily calorie target (kcal)")]
        public int? DailyCalories { get; set; }

        [Required(ErrorMessage = "Please choose a start date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start date")]
        public DateTime? StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "End date (optional)")]
        public DateTime? EndDate { get; set; }

        [MaxLength(500, ErrorMessage = "Notes can be at most 500 characters.")]
        [Display(Name = "Notes for the client")]
        public string? Notes { get; set; }

        [Display(Name = "Active (the client sees this plan)")]
        public bool IsActive { get; set; } = true;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartDate.HasValue && EndDate.HasValue && EndDate.Value.Date < StartDate.Value.Date)
            {
                yield return new ValidationResult(
                    "The end date can't be before the start date.", new[] { nameof(EndDate) });
            }
        }
    }

    /// <summary>Data for the Create Diet Plan page.</summary>
    public class CreateDietPlanViewModel
    {
        public DietPlanInput Input { get; set; } = new();
        public List<ClientOption> Clients { get; set; } = new();
    }

    /// <summary>One meal to add to a diet plan.</summary>
    public class PlanMealInput
    {
        [Required(ErrorMessage = "Please choose a meal type.")]
        [Display(Name = "Meal")]
        public string? MealType { get; set; }

        [Required(ErrorMessage = "Please enter the meal name.")]
        [MaxLength(100, ErrorMessage = "Meal name can be at most 100 characters.")]
        [Display(Name = "Meal name")]
        public string? MealName { get; set; }

        [MaxLength(500, ErrorMessage = "Description can be at most 500 characters.")]
        public string? Description { get; set; }

        [Range(0, 3000, ErrorMessage = "Calories must be between 0 and 3000.")]
        [Display(Name = "Calories (kcal)")]
        public int? Calories { get; set; }

        [DataType(DataType.Time)]
        [Display(Name = "Time")]
        public TimeSpan? MealTime { get; set; }
    }

    /// <summary>Data for the diet plan details page (edit plan + manage its meals).</summary>
    public class DietPlanDetailsViewModel
    {
        public DietPlan Plan { get; set; } = null!;
        public string ClientName { get; set; } = "";
        public DietPlanInput Edit { get; set; } = new();
        public PlanMealInput NewMeal { get; set; } = new();

        /// <summary>Total calories of all meals in the plan.</summary>
        public int PlannedCalories => Plan.Meals.Sum(m => m.Calories ?? 0);
    }
}
