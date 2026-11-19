using System.Globalization;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Common;
using HCMS4.Data;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Shared.Notifications.Services
{
    public interface INotificationService
    {
        Task CreateForUserAsync(string userId, string title, string message, NotificationType type,
            string? linkUrl = null, string? relatedEntityType = null, int? relatedEntityId = null);
        Task CreateForRoleAsync(string roleName, string title, string message, NotificationType type,
            string? linkUrl = null, string? relatedEntityType = null, int? relatedEntityId = null);

        /// <summary>The user's most recent notifications, newest first.</summary>
        Task<List<UserNotification>> GetRecentForUserAsync(string userId, int count = 5);

        Task<int> CountUnreadAsync(string userId);

        /// <summary>Marks the user's notification as read and returns it; null when it is not theirs.</summary>
        Task<UserNotification?> MarkAsReadAsync(string userId, int id);

        /// <summary>Marks all the user's notifications as read; returns how many changed.</summary>
        Task<int> MarkAllAsReadAsync(string userId);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserNotification>> GetRecentForUserAsync(string userId, int count = 5)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<UserNotification>();
            }

            return await _context.UserNotifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public Task<int> CountUnreadAsync(string userId) =>
            string.IsNullOrWhiteSpace(userId)
                ? Task.FromResult(0)
                : _context.UserNotifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        public async Task<UserNotification?> MarkAsReadAsync(string userId, int id)
        {
            var notification = await _context.UserNotifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (notification is { IsRead: false })
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return notification;
        }

        public async Task<int> MarkAllAsReadAsync(string userId)
        {
            var unread = await _context.UserNotifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
            unread.ForEach(n => n.IsRead = true);
            await _context.SaveChangesAsync();
            return unread.Count;
        }

        public async Task CreateForUserAsync(string userId, string title, string message, NotificationType type,
            string? linkUrl = null, string? relatedEntityType = null, int? relatedEntityId = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            _context.UserNotifications.Add(new UserNotification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                LinkUrl = linkUrl,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId?.ToString(CultureInfo.InvariantCulture),
                CreatedAt = ClinicTime.Now
            });

            await _context.SaveChangesAsync();
        }

        public async Task CreateForRoleAsync(string roleName, string title, string message, NotificationType type,
            string? linkUrl = null, string? relatedEntityType = null, int? relatedEntityId = null)
        {
            var userIds = await (
                from userRole in _context.UserRoles
                join role in _context.Roles on userRole.RoleId equals role.Id
                where role.Name == roleName
                select userRole.UserId
            ).Distinct().ToListAsync();

            if (userIds.Count == 0)
            {
                return;
            }

            foreach (var userId in userIds)
            {
                _context.UserNotifications.Add(new UserNotification
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    Type = type,
                    LinkUrl = linkUrl,
                    RelatedEntityType = relatedEntityType,
                    RelatedEntityId = relatedEntityId?.ToString(CultureInfo.InvariantCulture),
                    CreatedAt = ClinicTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }
    }
}
