using HCMS4.Modules.Appointments.Services;

namespace HCMS4.Modules.Appointments
{
    public static class AppointmentsModule
    {
        public static IServiceCollection AddAppointmentsModule(this IServiceCollection services)
        {
            services.AddScoped<IAppointmentService, AppointmentService>();
            services.AddScoped<INoShowRiskService, NoShowRiskService>();
            services.AddHttpClient("NoShowAPI", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            return services;
        }
    }
}
