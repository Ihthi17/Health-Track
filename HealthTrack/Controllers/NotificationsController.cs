using System.Security.Claims;
using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Controllers
{
    /// <summary>
    /// Notifications page for clients and trainers (Task 8).
    /// Shows the logged-in user's notifications and reminders, lets them open
    /// one (marks it read and goes to the related page) or mark all as read.
    ///
    /// Security: a user can only see and change their OWN notifications.
    /// </summary>
    [Authorize(Roles = "Client,Trainer")]
    [Route("notifications")]
    public class NotificationsController : Controller
    {
        private readonly HealthTrackContext _context;
        private readonly ILogger<NotificationsController> _logger;

        public NotificationsController(HealthTrackContext context, ILogger<NotificationsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claim, out var id) ? id : 0;
        }

        /// <summary>
        /// Works out which page a notification should open, based on its type
        /// and whether the user is a client or a trainer.
        /// </summary>
        private string GetTargetUrl(Notification n)
        {
            bool isTrainer = User.IsInRole("Trainer");

            return n.Type switch
            {
                "Appointment" => isTrainer ? "/trainer/appointments" : "/client/appointments",
                "Workout"     => "/client/workouts",
                "Diet"        => "/client/diet-plans",
                "Feedback"    => "/client",
                "Assignment"  => isTrainer ? "/trainer/clients" : "/client",
                "Reminder"    => isTrainer ? "/trainer/appointments"
                                 : n.Title.StartsWith("Workout") ? "/client/workouts"
                                 : n.Title.StartsWith("Upcoming") ? "/client/appointments"
                                 : "/client/diet-plans",
                _             => isTrainer ? "/trainer" : "/client"
            };
        }

        /// <summary>GET /notifications - the user's latest notifications, unread first.</summary>
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            int userId = GetCurrentUserId();

            var notifications = await _context.Notifications
                .Where(n => n.UserID == userId)
                .OrderBy(n => n.IsRead)                   // unread first
                .ThenByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            ViewData["IsTrainer"] = User.IsInRole("Trainer");
            return View(notifications);
        }

        /// <summary>
        /// POST /notifications/{id}/open - marks one notification as read and
        /// redirects to the page it is about.
        /// </summary>
        [HttpPost("{id:int}/open")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Open(int id)
        {
            int userId = GetCurrentUserId();
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationID == id && n.UserID == userId);

            if (notification == null)
                return RedirectToAction(nameof(Index));

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return Redirect(GetTargetUrl(notification));
        }

        /// <summary>POST /notifications/{id}/read - marks one notification as read and stays on the page.</summary>
        [HttpPost("{id:int}/read")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            int userId = GetCurrentUserId();
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationID == id && n.UserID == userId);

            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>POST /notifications/read-all - marks all of the user's notifications as read.</summary>
        [HttpPost("read-all")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            int userId = GetCurrentUserId();
            try
            {
                var unread = await _context.Notifications
                    .Where(n => n.UserID == userId && !n.IsRead)
                    .ToListAsync();

                foreach (var n in unread)
                    n.IsRead = true;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = unread.Count > 0
                    ? $"{unread.Count} notification(s) marked as read."
                    : "You have no unread notifications.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notifications as read for user {UserId}", userId);
                TempData["ErrorMessage"] = "Sorry, something went wrong. Please try again.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
