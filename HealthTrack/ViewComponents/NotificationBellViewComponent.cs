using System.Security.Claims;
using HealthTrack.Data;
using HealthTrack.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.ViewComponents
{
    /// <summary>
    /// The bell icon in the client and trainer top bars (Task 8).
    /// Creates any due reminders, then shows the number of unread notifications.
    /// Used in a layout with: @await Component.InvokeAsync("NotificationBell")
    /// </summary>
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly HealthTrackContext _context;
        private readonly ILogger<NotificationBellViewComponent> _logger;

        public NotificationBellViewComponent(HealthTrackContext context, ILogger<NotificationBellViewComponent> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            int unread = 0;
            var claim = UserClaimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (int.TryParse(claim, out var userId))
            {
                try
                {
                    await ReminderGenerator.EnsureRemindersAsync(_context, userId,
                        UserClaimsPrincipal.IsInRole("Client"), UserClaimsPrincipal.IsInRole("Trainer"));
                }
                catch (Exception ex)
                {
                    // A reminder problem must never break the page
                    _logger.LogWarning(ex, "Could not create reminders for user {UserId}", userId);
                }

                unread = await _context.Notifications.CountAsync(n => n.UserID == userId && !n.IsRead);
            }

            return View(unread);
        }
    }
}
