using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    /// <summary>
    /// Everything shown on the Client Dashboard (Task 2), gathered by
    /// ClientController.Index and passed to Views/Client/Index.cshtml.
    /// </summary>
    public class ClientDashboardViewModel
    {
        /// <summary>Client's first name, for the greeting.</summary>
        public string FirstName { get; set; } = "";

        /// <summary>Most recent health measurement, or null if none recorded yet.</summary>
        public HealthMetric? LatestMetric { get; set; }

        /// <summary>Weight change (kg) between the latest and the previous measurement.</summary>
        public decimal? WeightChangeKg { get; set; }

        /// <summary>Trainer currently assigned to this client (via TrainerClientAssignments).</summary>
        public string? TrainerName { get; set; }

        /// <summary>The client's current active workout plan (created by the trainer).</summary>
        public WorkoutPlan? ActiveWorkoutPlan { get; set; }

        /// <summary>Exercises from the active plan scheduled for today.</summary>
        public List<WorkoutPlanItem> TodaysExercises { get; set; } = new();

        /// <summary>The client's current active diet plan (created by the trainer).</summary>
        public DietPlan? ActiveDietPlan { get; set; }

        /// <summary>Calories the client has logged today (from MealLogs).</summary>
        public int CaloriesEatenToday { get; set; }

        /// <summary>Calories burned this week (from WorkoutLogs).</summary>
        public decimal CaloriesBurnedThisWeek { get; set; }

        /// <summary>Number of workouts logged this week.</summary>
        public int WorkoutsThisWeek { get; set; }

        /// <summary>Next few appointments that are not cancelled.</summary>
        public List<Appointment> UpcomingAppointments { get; set; } = new();

        /// <summary>Latest feedback messages written by trainers.</summary>
        public List<TrainerFeedback> RecentFeedback { get; set; } = new();

        /// <summary>Count of unread notifications for the bell badge.</summary>
        public int UnreadNotifications { get; set; }

        /// <summary>Shown instead of the dashboard content if loading fails.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Data for the Progress page (Task 5 logging + Task 7 trends):
    /// the add-measurement form, the history table and the chart data.
    /// </summary>
    public class ClientProgressViewModel
    {
        /// <summary>Values typed into the "Add measurement" form.</summary>
        public HealthMetricInput NewMetric { get; set; } = new();

        /// <summary>All of the client's measurements, newest first.</summary>
        public List<HealthMetric> History { get; set; } = new();

        /// <summary>Client's saved height (cm), used to calculate BMI.</summary>
        public decimal? HeightCm { get; set; }

        // ----- Chart data (oldest first, so the line goes left to right) -----
        public List<string> ChartDates { get; set; } = new();
        public List<decimal?> ChartWeights { get; set; } = new();
        public List<decimal?> ChartBmis { get; set; } = new();

        /// <summary>Last 14 days, one label per day, for the calories-burned chart.</summary>
        public List<string> CalorieDates { get; set; } = new();
        public List<decimal> CaloriesBurned { get; set; } = new();
        public List<int> CaloriesEaten { get; set; } = new();
    }

    /// <summary>
    /// The "Add measurement" form. Validation attributes reject impossible
    /// values on the server, so bad input never reaches the database.
    /// </summary>
    public class HealthMetricInput : IValidatableObject
    {
        [Required(ErrorMessage = "Please choose the date of the measurement.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime MeasuredOn { get; set; } = DateTime.Today;

        [Range(20, 400, ErrorMessage = "Weight must be between 20 and 400 kg.")]
        [Display(Name = "Weight (kg)")]
        public decimal? WeightKg { get; set; }

        [Range(50, 250, ErrorMessage = "Height must be between 50 and 250 cm.")]
        [Display(Name = "Height (cm)")]
        public decimal? HeightCm { get; set; }

        [RegularExpression(@"^\d{2,3}/\d{2,3}$", ErrorMessage = "Enter blood pressure like 120/80.")]
        [Display(Name = "Blood pressure")]
        public string? BloodPressure { get; set; }

        [Range(30, 220, ErrorMessage = "Heart rate must be between 30 and 220 bpm.")]
        [Display(Name = "Heart rate (bpm)")]
        public int? HeartRate { get; set; }

        [Range(1, 600, ErrorMessage = "Blood sugar must be between 1 and 600 mg/dL.")]
        [Display(Name = "Blood sugar (mg/dL)")]
        public decimal? BloodSugar { get; set; }

        [Range(2, 70, ErrorMessage = "Body fat must be between 2 and 70 %.")]
        [Display(Name = "Body fat (%)")]
        public decimal? BodyFatPercent { get; set; }

        /// <summary>Extra rules that involve more than one field.</summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (MeasuredOn.Date > DateTime.Today)
            {
                yield return new ValidationResult(
                    "The date cannot be in the future.", new[] { nameof(MeasuredOn) });
            }

            if (WeightKg == null && BloodPressure == null && HeartRate == null
                && BloodSugar == null && BodyFatPercent == null)
            {
                yield return new ValidationResult(
                    "Please enter at least one measurement.");
            }
        }
    }

    /// <summary>
    /// Data for the My Workouts page: the trainer's plan (read-only),
    /// the log-a-workout form and the client's recent workout history.
    /// </summary>
    public class ClientWorkoutsViewModel
    {
        /// <summary>Current active plan created by the trainer, or null.</summary>
        public WorkoutPlan? ActivePlan { get; set; }

        /// <summary>Name of the trainer who created the active plan.</summary>
        public string? PlanTrainerName { get; set; }

        /// <summary>Plan items that already have a log for today (shown as "Done").</summary>
        public HashSet<int> PlanItemsDoneToday { get; set; } = new();

        /// <summary>All exercises, for the drop-down list.</summary>
        public List<Exercise> Exercises { get; set; } = new();

        /// <summary>Values typed into the "Log a workout" form.</summary>
        public WorkoutLogInput NewLog { get; set; } = new();

        /// <summary>The client's most recent workout logs.</summary>
        public List<WorkoutLog> RecentLogs { get; set; } = new();
    }

    /// <summary>
    /// The "Log a workout" form, with server-side validation.
    /// </summary>
    public class WorkoutLogInput : IValidatableObject
    {
        [Required(ErrorMessage = "Please choose the date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime LogDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Please choose an exercise.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please choose an exercise.")]
        [Display(Name = "Exercise")]
        public int? ExerciseID { get; set; }

        /// <summary>Set when the client logs an exercise straight from their plan.</summary>
        public int? PlanItemID { get; set; }

        [Range(1, 50, ErrorMessage = "Sets must be between 1 and 50.")]
        public int? Sets { get; set; }

        [Range(1, 500, ErrorMessage = "Reps must be between 1 and 500.")]
        public int? Reps { get; set; }

        [Range(1, 600, ErrorMessage = "Duration must be between 1 and 600 minutes.")]
        [Display(Name = "Duration (min)")]
        public int? DurationMin { get; set; }

        [Range(0, 5000, ErrorMessage = "Calories must be between 0 and 5000.")]
        [Display(Name = "Calories burned")]
        public decimal? CaloriesBurned { get; set; }

        [MaxLength(255, ErrorMessage = "Notes can be at most 255 characters.")]
        public string? Notes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (LogDate.Date > DateTime.Today)
            {
                yield return new ValidationResult(
                    "You can't log a workout in the future.", new[] { nameof(LogDate) });
            }

            if (Sets == null && Reps == null && DurationMin == null)
            {
                yield return new ValidationResult(
                    "Please enter sets/reps or a duration.");
            }
        }
    }

    /// <summary>
    /// Data for the Diet Plans page: the trainer's diet plan (read-only),
    /// the log-a-meal form, today's totals and recent meal history.
    /// </summary>
    public class ClientDietViewModel
    {
        /// <summary>Current active diet plan created by the trainer, or null.</summary>
        public DietPlan? ActivePlan { get; set; }

        /// <summary>Name of the trainer who created the active plan.</summary>
        public string? PlanTrainerName { get; set; }

        /// <summary>Values typed into the "Log a meal" form.</summary>
        public MealLogInput NewMeal { get; set; } = new();

        /// <summary>Meals the client has logged today.</summary>
        public List<MealLog> TodaysMeals { get; set; } = new();

        // Today's totals, shown against the plan's daily calorie target
        public int TodayCalories { get; set; }
        public decimal TodayProtein { get; set; }
        public decimal TodayCarbs { get; set; }
        public decimal TodayFat { get; set; }

        /// <summary>Earlier meal logs (before today), newest first.</summary>
        public List<MealLog> RecentMeals { get; set; } = new();
    }

    /// <summary>
    /// The "Log a meal" form, with server-side validation.
    /// </summary>
    public class MealLogInput : IValidatableObject
    {
        /// <summary>Allowed meal types (also used to build the drop-down).</summary>
        public static readonly string[] MealTypes = { "Breakfast", "Lunch", "Dinner", "Snack" };

        [Required(ErrorMessage = "Please choose the date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime LogDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Please choose a meal type.")]
        [Display(Name = "Meal")]
        public string? MealType { get; set; }

        [Required(ErrorMessage = "Please enter what you ate.")]
        [MaxLength(100, ErrorMessage = "Food name can be at most 100 characters.")]
        [Display(Name = "Food")]
        public string? FoodName { get; set; }

        [Range(0, 5000, ErrorMessage = "Calories must be between 0 and 5000.")]
        [Display(Name = "Calories (kcal)")]
        public int? Calories { get; set; }

        [Range(0, 500, ErrorMessage = "Protein must be between 0 and 500 g.")]
        [Display(Name = "Protein (g)")]
        public decimal? ProteinG { get; set; }

        [Range(0, 1000, ErrorMessage = "Carbs must be between 0 and 1000 g.")]
        [Display(Name = "Carbs (g)")]
        public decimal? CarbsG { get; set; }

        [Range(0, 500, ErrorMessage = "Fat must be between 0 and 500 g.")]
        [Display(Name = "Fat (g)")]
        public decimal? FatG { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (LogDate.Date > DateTime.Today)
            {
                yield return new ValidationResult(
                    "You can't log a meal in the future.", new[] { nameof(LogDate) });
            }

            // Only accept the four known meal types (stops tampered form values)
            if (MealType != null && !MealTypes.Contains(MealType))
            {
                yield return new ValidationResult(
                    "Please choose a valid meal type.", new[] { nameof(MealType) });
            }
        }
    }

    /// <summary>A trainer the client can book with (for the drop-down list).</summary>
    public class TrainerOption
    {
        public int TrainerID { get; set; }
        public string Name { get; set; } = "";
        public string? Specialization { get; set; }
    }

    /// <summary>
    /// Data for the Appointments page (Task 6): the booking form,
    /// upcoming appointments and past appointments.
    /// </summary>
    public class ClientAppointmentsViewModel
    {
        public AppointmentInput NewAppointment { get; set; } = new();

        /// <summary>Active trainers the client can book with.</summary>
        public List<TrainerOption> Trainers { get; set; } = new();

        /// <summary>The client's assigned trainer, pre-selected in the form.</summary>
        public int? AssignedTrainerID { get; set; }

        /// <summary>TrainerID -> trainer name, for showing names in the lists.</summary>
        public Dictionary<int, string> TrainerNames { get; set; } = new();

        public List<Appointment> Upcoming { get; set; } = new();
        public List<Appointment> Past { get; set; } = new();
    }

    /// <summary>
    /// The "Book an appointment" form, with server-side validation.
    /// </summary>
    public class AppointmentInput : IValidatableObject
    {
        /// <summary>Allowed session lengths in minutes.</summary>
        public static readonly int[] Durations = { 30, 45, 60, 90 };

        /// <summary>Earliest and latest start times the trainers work.</summary>
        public static readonly TimeSpan OpeningTime = new(6, 0, 0);
        public static readonly TimeSpan ClosingTime = new(21, 0, 0);

        [Required(ErrorMessage = "Please choose a trainer.")]
        [Display(Name = "Trainer")]
        public int? TrainerID { get; set; }

        [Required(ErrorMessage = "Please choose a date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime? Date { get; set; }

        [Required(ErrorMessage = "Please choose a start time.")]
        [DataType(DataType.Time)]
        [Display(Name = "Start time")]
        public TimeSpan? StartTime { get; set; }

        [Required(ErrorMessage = "Please choose how long the session is.")]
        [Display(Name = "Length")]
        public int DurationMin { get; set; } = 60;

        [Required(ErrorMessage = "Please tell your trainer what the session is about.")]
        [MaxLength(500, ErrorMessage = "Reason can be at most 500 characters.")]
        [Display(Name = "Reason")]
        public string? Reason { get; set; }

        /// <summary>Combined start date and time.</summary>
        public DateTime? StartDateTime =>
            Date.HasValue && StartTime.HasValue ? Date.Value.Date + StartTime.Value : null;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!Durations.Contains(DurationMin))
            {
                yield return new ValidationResult(
                    "Please choose a valid session length.", new[] { nameof(DurationMin) });
            }

            if (StartDateTime.HasValue)
            {
                if (StartDateTime.Value <= DateTime.Now.AddHours(1))
                {
                    yield return new ValidationResult(
                        "Appointments must be booked at least 1 hour in advance.", new[] { nameof(StartTime) });
                }

                if (StartDateTime.Value > DateTime.Today.AddMonths(3))
                {
                    yield return new ValidationResult(
                        "Appointments can be booked up to 3 months ahead.", new[] { nameof(Date) });
                }

                var end = StartTime!.Value.Add(TimeSpan.FromMinutes(DurationMin));
                if (StartTime.Value < OpeningTime || end > ClosingTime)
                {
                    yield return new ValidationResult(
                        "Sessions must be between 06:00 and 21:00.", new[] { nameof(StartTime) });
                }
            }
        }
    }

    /// <summary>Data for the client's Profile page: personal details + change password.</summary>
    public class ClientProfilePageViewModel
    {
        public ProfileDetailsInput Details { get; set; } = new();
        public ChangePasswordInput Password { get; set; } = new();

        /// <summary>Login email (shown read-only - it identifies the account).</summary>
        public string Email { get; set; } = "";
        public DateTime MemberSince { get; set; }
        public string? TrainerName { get; set; }

        /// <summary>Age worked out from the date of birth.</summary>
        public int? Age
        {
            get
            {
                if (!Details.DateOfBirth.HasValue) return null;
                var dob = Details.DateOfBirth.Value.Date;
                int age = DateTime.Today.Year - dob.Year;
                if (dob > DateTime.Today.AddYears(-age)) age--;   // birthday not reached yet this year
                return age;
            }
        }
    }

    /// <summary>
    /// The personal details form. Maximum lengths match the database columns
    /// (Users and ClientProfiles) so a save can never fail because text is too long.
    /// </summary>
    public class ProfileDetailsInput : IValidatableObject
    {
        public static readonly string[] Genders = { "Male", "Female", "Other" };

        [Required(ErrorMessage = "Please enter your first name.")]
        [MaxLength(50)]
        [Display(Name = "First name")]
        public string? FirstName { get; set; }

        [Required(ErrorMessage = "Please enter your last name.")]
        [MaxLength(50)]
        [Display(Name = "Last name")]
        public string? LastName { get; set; }

        [MaxLength(20)]
        [RegularExpression(@"^[0-9+\-\s()]{7,20}$", ErrorMessage = "Enter a valid phone number (digits, spaces, + - ( ) only).")]
        public string? Phone { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        [MaxLength(10)]
        public string? Gender { get; set; }

        [Range(50, 250, ErrorMessage = "Height must be between 50 and 250 cm.")]
        [Display(Name = "Height (cm)")]
        public decimal? HeightCm { get; set; }

        [MaxLength(150)] [Display(Name = "Address line 1")] public string? AddressLine1 { get; set; }
        [MaxLength(150)] [Display(Name = "Address line 2")] public string? AddressLine2 { get; set; }
        [MaxLength(80)] public string? City { get; set; }
        [MaxLength(80)] [Display(Name = "State / Province")] public string? StateProvince { get; set; }
        [MaxLength(20)] [Display(Name = "Postal code")] public string? PostalCode { get; set; }

        [MaxLength(255, ErrorMessage = "Your goal can be at most 255 characters.")]
        [Display(Name = "My fitness goal")]
        public string? GoalDescription { get; set; }

        [MaxLength(500, ErrorMessage = "Medical notes can be at most 500 characters.")]
        [Display(Name = "Medical notes (shared with your trainer)")]
        public string? MedicalNotes { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (DateOfBirth.HasValue)
            {
                if (DateOfBirth.Value.Date > DateTime.Today)
                    yield return new ValidationResult("Date of birth can't be in the future.", new[] { nameof(DateOfBirth) });
                else if (DateOfBirth.Value.Date < DateTime.Today.AddYears(-120))
                    yield return new ValidationResult("Please check your date of birth.", new[] { nameof(DateOfBirth) });
            }

            if (!string.IsNullOrEmpty(Gender) && !Genders.Contains(Gender))
                yield return new ValidationResult("Please choose a valid option.", new[] { nameof(Gender) });
        }
    }

    /// <summary>The change password form (same 6-character minimum as registration).</summary>
    public class ChangePasswordInput
    {
        [Required(ErrorMessage = "Please enter your current password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string? CurrentPassword { get; set; }

        [Required(ErrorMessage = "Please enter a new password.")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "The new password must be at least 6 characters.")]
        [MaxLength(100)]
        [Display(Name = "New password")]
        public string? NewPassword { get; set; }

        [Required(ErrorMessage = "Please confirm the new password.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The new passwords do not match.")]
        [Display(Name = "Confirm new password")]
        public string? ConfirmPassword { get; set; }
    }
}
