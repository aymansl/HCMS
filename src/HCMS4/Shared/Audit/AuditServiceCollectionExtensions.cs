using HCMS4.Shared.Audit.Services;

namespace HCMS4.Shared.Audit
{
    public static class AuditServiceCollectionExtensions
    {
        public static IServiceCollection AddAudit(this IServiceCollection services)
        {
            services.AddScoped<IActivityLogService, ActivityLogService>();
            return services;
        }
    }
}
