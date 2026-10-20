using HealthTrack.Data;
using HealthTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(int userId, string type, string title, string message, DateTime? remindAt = null);
        Task<List<Notification>> GetUnreadNotificationsAsync(int userId, int count = 10);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAsReadAsync(int notificationId, int userId);
        Task MarkAllAsReadAsync(int userId);
    }

    public class NotificationService : INotificationService
    {
        private readonly HealthTrackContext _context;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(HealthTrackContext context, ILogger<NotificationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task CreateNotificationAsync(int userId, string type, string title, string message, DateTime? remindAt = null)
        {
            try
            {
                var notification = new Notification
                {
                    UserID = userId,
                    Type = type,
                    Title = title,
                    Message = message,
                    RemindAt = remindAt,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Notification created for user {userId}: {title}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating notification for user {userId}");
                throw;
            }
        }

        public async Task<List<Notification>> GetUnreadNotificationsAsync(int userId, int count = 10)
        {
            try
            {
                return await _context.Notifications
                    .Where(n => n.UserID == userId && !n.IsRead)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving notifications for user {userId}");
                return new List<Notification>();
            }
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            try
            {
                return await _context.Notifications
                    .CountAsync(n => n.UserID == userId && !n.IsRead);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting unread count for user {userId}");
                return 0;
            }
        }

        public async Task MarkAsReadAsync(int notificationId, int userId)
        {
            try
            {
                var notification = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationID == notificationId && n.UserID == userId);

                if (notification != null)
                {
                    notification.IsRead = true;
                    _context.Notifications.Update(notification);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking notification {notificationId} as read");
                throw;
            }
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            try
            {
                var notifications = await _context.Notifications
                    .Where(n => n.UserID == userId && !n.IsRead)
                    .ToListAsync();

                foreach (var notification in notifications)
                {
                    notification.IsRead = true;
                }

                _context.Notifications.UpdateRange(notifications);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Marked all notifications as read for user {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking all notifications as read for user {userId}");
                throw;
            }
        }
    }
}
