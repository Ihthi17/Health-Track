using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    /// <summary>
    /// Data for the trainer's "Manage appointment" page, where a trainer
    /// approves, reschedules, cancels or completes a client's booking.
    /// </summary>
    public class ManageAppointmentViewModel
    {
        public Appointment Appointment { get; set; } = null!;
        public string ClientName { get; set; } = "";
        public string? ClientEmail { get; set; }

        /// <summary>The reschedule form (pre-filled with the current time).</summary>
        public RescheduleInput Reschedule { get; set; } = new();

        // Which buttons to show, worked out in the controller
        public bool CanApprove { get; set; }
        public bool CanReschedule { get; set; }
        public bool CanCancel { get; set; }
        public bool CanComplete { get; set; }
    }

    /// <summary>A short note the trainer can attach when approving, cancelling or completing.</summary>
    public class TrainerNoteInput
    {
        [MaxLength(500, ErrorMessage = "The note can be at most 500 characters.")]
        public string? Note { get; set; }
    }

    /// <summary>
    /// The reschedule form. Uses the same rules as client booking
    /// (future time, 06:00-21:00, allowed lengths).
    /// </summary>
    public class RescheduleInput : IValidatableObject
    {
        [Required(ErrorMessage = "Please choose a date.")]
        [DataType(DataType.Date)]
        [Display(Name = "New date")]
        public DateTime? Date { get; set; }

        [Required(ErrorMessage = "Please choose a start time.")]
        [DataType(DataType.Time)]
        [Display(Name = "New start time")]
        public TimeSpan? StartTime { get; set; }

        [Display(Name = "Length")]
        public int DurationMin { get; set; } = 60;

        [MaxLength(500, ErrorMessage = "The note can be at most 500 characters.")]
        [Display(Name = "Note to client")]
        public string? Note { get; set; }

        public DateTime? StartDateTime =>
            Date.HasValue && StartTime.HasValue ? Date.Value.Date + StartTime.Value : null;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!AppointmentInput.Durations.Contains(DurationMin))
            {
                yield return new ValidationResult(
                    "Please choose a valid session length.", new[] { nameof(DurationMin) });
            }

            if (StartDateTime.HasValue)
            {
                if (StartDateTime.Value <= DateTime.Now)
                {
                    yield return new ValidationResult(
                        "The new time must be in the future.", new[] { nameof(StartTime) });
                }

                var end = StartTime!.Value.Add(TimeSpan.FromMinutes(DurationMin));
                if (StartTime.Value < AppointmentInput.OpeningTime || end > AppointmentInput.ClosingTime)
                {
                    yield return new ValidationResult(
                        "Sessions must be between 06:00 and 21:00.", new[] { nameof(StartTime) });
                }
            }
        }
    }
}
