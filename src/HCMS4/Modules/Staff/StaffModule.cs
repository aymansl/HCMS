using HCMS4.Modules.Staff.Services;

namespace HCMS4.Modules.Staff
{
    public static class StaffModule
    {
        public static IServiceCollection AddStaffModule(this IServiceCollection services)
        {
            services.AddScoped<ILeaveService, LeaveService>();
            services.AddScoped<HCMS4.Shared.Identity.Services.IDoctorLookup, DoctorLookup>();
            return services;
        }
    }
}
