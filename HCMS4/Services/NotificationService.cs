using HCMS4.Data;
using HCMS4.Models;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Services
{
    public interface INotificationService
    {
        Task CreateForUserAsync(string userId, string title, string message, NotificationType type,
            string? linkUrl = null, string? relatedEntityType = null, int? relatedEntityId = null);
        Task CreateForRoleAsync(string roleName, string title, string message, NotificationType type,
            string? linkUrl = null, string? relatedEntityType = null, int? relatedEntityId = null);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;

        public NotificationService(ApplicationDbContext context)
        {
            _context = context;
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
                CreatedAt = DateTime.UtcNow
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
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }
    }
}
