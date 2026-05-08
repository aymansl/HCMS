using HCMS4.Data;
using HCMS4.Models;

namespace HCMS4.Services
{
    public interface IActivityLogService
    {
        Task LogAsync(string action, string entityType, string description, string? entityId = null,
            string? userId = null, string? userName = null);
    }

    public class ActivityLogService : IActivityLogService
    {
        private readonly ApplicationDbContext _context;

        public ActivityLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string action, string entityType, string description, string? entityId = null,
            string? userId = null, string? userName = null)
        {
            _context.SystemActivityLogs.Add(new SystemActivityLog
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Description = description,
                UserId = userId,
                UserName = userName,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }
    }
}
