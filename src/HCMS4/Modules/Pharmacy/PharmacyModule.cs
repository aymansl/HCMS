using HCMS4.Modules.Pharmacy.Services;

namespace HCMS4.Modules.Pharmacy
{
    public static class PharmacyModule
    {
        public static IServiceCollection AddPharmacyModule(this IServiceCollection services)
        {
            services.AddScoped<IDrugService, DrugService>();
            services.AddScoped<IPrescriptionService, PrescriptionService>();
            services.AddScoped<IPharmacistService, PharmacistService>();
            return services;
        }
    }
}
