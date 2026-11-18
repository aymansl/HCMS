using HCMS4.Shared.Notifications.Services;

namespace HCMS4.Shared.Notifications
{
    public static class NotificationsServiceCollectionExtensions
    {
        public static IServiceCollection AddNotifications(this IServiceCollection services)
        {
            services.AddScoped<INotificationService, NotificationService>();
            return services;
        }
    }
}
