using HCMS4.Modules.Records.Services;

namespace HCMS4.Modules.Records
{
    public static class RecordsModule
    {
        public static IServiceCollection AddRecordsModule(this IServiceCollection services)
        {
            services.AddScoped<IPatientService, PatientService>();
            services.AddScoped<HCMS4.Shared.Identity.Services.IPatientLookup, PatientLookup>();
            return services;
        }
    }
}
