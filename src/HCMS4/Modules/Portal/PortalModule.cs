using HCMS4.Modules.Portal.Services;

namespace HCMS4.Modules.Portal
{
    public static class PortalModule
    {
        public static IServiceCollection AddPortalModule(this IServiceCollection services)
        {
            services.AddScoped<IDashboardService, DashboardService>();
            return services;
        }
    }
}
