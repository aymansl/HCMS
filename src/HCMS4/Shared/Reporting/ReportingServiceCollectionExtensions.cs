using HCMS4.Shared.Reporting.Services;

namespace HCMS4.Shared.Reporting
{
    public static class ReportingServiceCollectionExtensions
    {
        public static IServiceCollection AddReporting(this IServiceCollection services)
        {
            services.AddScoped<IDailyReportService, DailyReportService>();
            // Nightly job: completes past appointments and stores the pharmacy report.
            services.AddHostedService<DailyReportBackgroundService>();
            return services;
        }
    }
}
