using HCMS4.Modules.Records.Services;

namespace HCMS4.Modules.Records
{
    public static class RecordsModule
    {
        public static IServiceCollection AddRecordsModule(this IServiceCollection services)
        {
            services.AddScoped<IPatientService, PatientService>();
            return services;
        }
    }
}
