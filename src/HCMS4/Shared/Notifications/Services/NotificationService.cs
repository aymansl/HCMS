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
                RelatedEntityId = relatedEntityId?.ToString(),
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

            if (!userIds.Any())
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
                    RelatedEntityId = relatedEntityId?.ToString(),
                    CreatedAt = ClinicTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }
    }
}
