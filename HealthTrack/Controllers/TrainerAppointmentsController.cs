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
    /// Trainer side of the appointment system (Task 6) - the part that connects
    /// the Trainer module to the Client module.
    ///
    /// Clients book appointments as "Pending" (ClientController.BookAppointment).
    /// Here the trainer can:
    ///   - Approve   : Pending            -> Approved
    ///   - Reschedule: Pending / Approved -> Approved at a new time
    ///   - Cancel    : Pending / Approved -> Cancelled
    ///   - Complete  : Approved (started) -> Completed
    /// Every action saves an optional note (TrainerNotes, shown to the client)
    /// and sends the client an in-app notification.
    ///
    /// Security: only trainers can use this controller, and every action checks
    /// the appointment belongs to the logged-in trainer.
    /// </summary>
    [Authorize(Roles = "Trainer")]
    [Route("trainer/appointments")]
    public class TrainerAppointmentsController : Controller
    {
        private readonly HealthTrackContext _context;
        private readonly ILogger<TrainerAppointmentsController> _logger;
        private readonly INotificationService _notifications;

        public TrainerAppointmentsController(HealthTrackContext context,
                                             ILogger<TrainerAppointmentsController> logger,
                                             INotificationService notifications)
        {
            _context = context;
            _logger = logger;
            _notifications = notifications;
        }

        #region Helpers

        /// <summary>UserID of the logged-in trainer (0 if it cannot be read).</summary>
        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        /// <summary>
        /// TrainerID of the logged-in trainer. TrainerProfiles.TrainerID is a
        /// foreign key to Users.UserID, so match on either column.
        /// </summary>
        private async Task<int?> GetCurrentTrainerIdAsync()
        {
            int userId = GetCurrentUserId();
            return await _context.TrainerProfiles
                .Where(t => t.UserID == userId || t.TrainerProfileID == userId)
                .Select(t => (int?)t.TrainerProfileID)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Loads an appointment ONLY if it belongs to the logged-in trainer.
        /// Returns null otherwise, so a trainer cannot manage another trainer's
        /// appointment by changing the id in the URL.
        /// </summary>
        private async Task<Appointment?> GetOwnAppointmentAsync(int id)
        {
            var trainerId = await GetCurrentTrainerIdAsync();
            if (trainerId == null)
                return null;

            return await _context.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentID == id && a.TrainerID == trainerId.Value);
        }

        /// <summary>
        /// Sends the client an in-app notification. ClientID is the client's UserID
        /// (ClientProfiles.ClientID is a foreign key to Users). Failures are logged only.
        /// </summary>
        private async Task NotifyClientAsync(int clientId, string title, string message)
        {
            try
            {
                await _notifications.CreateNotificationAsync(clientId, "Appointment", title, message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not notify client {ClientId}", clientId);
            }
        }

        /// <summary>Saves the trainer's note if one was typed (keeps the old note otherwise).</summary>
        private static void ApplyNote(Appointment appointment, string? note)
        {
            if (!string.IsNullOrWhiteSpace(note))
                appointment.TrainerNotes = note.Trim();
        }

        /// <summary>Builds the Manage page model, including which actions are allowed.</summary>
        private async Task<ManageAppointmentViewModel> BuildManageViewModelAsync(Appointment appointment)
        {
            var client = await _context.Users
                .Where(u => u.UserID == appointment.ClientID)
                .Select(u => new { u.FirstName, u.LastName, u.Email })
                .FirstOrDefaultAsync();

            bool isFuture = appointment.StartTime > DateTime.Now;
            bool isOpen = appointment.Status == "Pending" || appointment.Status == "Approved";

            return new ManageAppointmentViewModel
            {
                Appointment = appointment,
                ClientName = client != null ? $"{client.FirstName} {client.LastName}" : "Client",
                ClientEmail = client?.Email,
                Reschedule = new RescheduleInput
                {
                    Date = appointment.StartTime.Date,
                    StartTime = appointment.StartTime.TimeOfDay,
                    DurationMin = (int)(appointment.EndTime - appointment.StartTime).TotalMinutes,
                    Note = appointment.TrainerNotes
                },
                CanApprove = appointment.Status == "Pending" && isFuture,
                CanReschedule = isOpen,
                CanCancel = isOpen && isFuture,
                CanComplete = appointment.Status == "Approved" && !isFuture
            };
        }

        #endregion

        /// <summary>
        /// GET /trainer/appointments/{id} - the Manage page for one appointment.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> Manage(int id)
        {
            var appointment = await GetOwnAppointmentAsync(id);
            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return Redirect("/trainer/appointments");
            }

            return View(await BuildManageViewModelAsync(appointment));
        }

        /// <summary>POST /trainer/appointments/{id}/approve - Pending -> Approved.</summary>
        [HttpPost("{id:int}/approve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, TrainerNoteInput input)
        {
            var appointment = await GetOwnAppointmentAsync(id);
            if (appointment == null || appointment.Status != "Pending" || appointment.StartTime <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "Only upcoming pending appointments can be approved.";
                return RedirectToAction(nameof(Manage), new { id });
            }

            appointment.Status = "Approved";
            ApplyNote(appointment, input.Note);
            await _context.SaveChangesAsync();

            await NotifyClientAsync(appointment.ClientID, "Appointment approved",
                $"Your session on {appointment.StartTime:ddd dd MMM, hh:mm tt} has been approved.");

            TempData["SuccessMessage"] = "Appointment approved. The client has been notified.";
            return RedirectToAction(nameof(Manage), new { id });
        }

        /// <summary>
        /// POST /trainer/appointments/{id}/reschedule - moves the appointment to a new
        /// time (and approves it). Checks neither the trainer nor the client is busy then.
        /// </summary>
        [HttpPost("{id:int}/reschedule")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reschedule(int id, [Bind(Prefix = "Reschedule")] RescheduleInput input)
        {
            var appointment = await GetOwnAppointmentAsync(id);
            if (appointment == null)
            {
                TempData["ErrorMessage"] = "Appointment not found.";
                return Redirect("/trainer/appointments");
            }

            if (appointment.Status != "Pending" && appointment.Status != "Approved")
                ModelState.AddModelError("", "Only pending or approved appointments can be rescheduled.");

            if (ModelState.IsValid)
            {
                var start = input.StartDateTime!.Value;
                var end = start.AddMinutes(input.DurationMin);

                // Two slots overlap if each starts before the other ends (ignore this appointment)
                bool clash = await _context.Appointments.AnyAsync(a =>
                    a.AppointmentID != appointment.AppointmentID
                    && (a.TrainerID == appointment.TrainerID || a.ClientID == appointment.ClientID)
                    && a.Status != "Cancelled" && a.Status != "Rejected"
                    && a.StartTime < end && start < a.EndTime);

                if (clash)
                    ModelState.AddModelError("", "You or the client already have an appointment at that time.");
            }

            if (!ModelState.IsValid)
            {
                var model = await BuildManageViewModelAsync(appointment);
                model.Reschedule = input;
                return View(nameof(Manage), model);
            }

            var oldStart = appointment.StartTime;
            appointment.StartTime = input.StartDateTime!.Value;
            appointment.EndTime = appointment.StartTime.AddMinutes(input.DurationMin);
            appointment.Status = "Approved";
            ApplyNote(appointment, input.Note);
            await _context.SaveChangesAsync();

            await NotifyClientAsync(appointment.ClientID, "Appointment rescheduled",
                $"Your session on {oldStart:ddd dd MMM, hh:mm tt} has been moved to {appointment.StartTime:ddd dd MMM, hh:mm tt}.");

            TempData["SuccessMessage"] = "Appointment rescheduled and approved. The client has been notified.";
            return RedirectToAction(nameof(Manage), new { id });
        }

        /// <summary>POST /trainer/appointments/{id}/cancel - Pending/Approved -> Cancelled.</summary>
        [HttpPost("{id:int}/cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, TrainerNoteInput input)
        {
            var appointment = await GetOwnAppointmentAsync(id);
            if (appointment == null
                || (appointment.Status != "Pending" && appointment.Status != "Approved")
                || appointment.StartTime <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "Only upcoming pending or approved appointments can be cancelled.";
                return RedirectToAction(nameof(Manage), new { id });
            }

            appointment.Status = "Cancelled";
            ApplyNote(appointment, input.Note);
            await _context.SaveChangesAsync();

            await NotifyClientAsync(appointment.ClientID, "Appointment cancelled by trainer",
                $"Your session on {appointment.StartTime:ddd dd MMM, hh:mm tt} was cancelled."
                + (string.IsNullOrWhiteSpace(input.Note) ? "" : $" Note: {input.Note.Trim()}"));

            TempData["SuccessMessage"] = "Appointment cancelled. The client has been notified.";
            return RedirectToAction(nameof(Manage), new { id });
        }

        /// <summary>POST /trainer/appointments/{id}/complete - Approved (already started) -> Completed.</summary>
        [HttpPost("{id:int}/complete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, TrainerNoteInput input)
        {
            var appointment = await GetOwnAppointmentAsync(id);
            if (appointment == null || appointment.Status != "Approved" || appointment.StartTime > DateTime.Now)
            {
                TempData["ErrorMessage"] = "Only approved sessions that have started can be marked as completed.";
                return RedirectToAction(nameof(Manage), new { id });
            }

            appointment.Status = "Completed";
            ApplyNote(appointment, input.Note);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Session marked as completed.";
            return RedirectToAction(nameof(Manage), new { id });
        }
    }
}
