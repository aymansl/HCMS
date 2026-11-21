using HCMS4.Modules.Lab.Services;
using HCMS4.Shared.Identity.Services;

namespace HCMS4.Modules.Lab
{
    public static class LabModule
    {
        public static IServiceCollection AddLabModule(this IServiceCollection services)
        {
            services.AddScoped<ILabTechnicianService, LabTechnicianService>();
            services.AddScoped<IAccountStatusCheck, LabTechnicianAccountStatusCheck>();
            services.AddScoped<ILabTestService, LabTestService>();
            services.AddScoped<ILabTestUsage, LabRequestTestUsage>();
            services.AddScoped<ILabRequestService, LabRequestService>();
            services.AddScoped<ILabAppointmentService, LabAppointmentService>();
            services.AddScoped<ILabResultService, LabResultService>();
            services.AddScoped<ILabHistoryService, LabHistoryService>();
            services.AddScoped<ILabResultSearchService, LabResultSearchService>();
            return services;
        }
    }
}
