using HCMS4.Modules.Billing.Services;

namespace HCMS4.Modules.Billing
{
    public static class BillingModule
    {
        public static IServiceCollection AddBillingModule(this IServiceCollection services)
        {
            services.AddScoped<IInvoicePaymentService, InvoicePaymentService>();
            return services;
        }
    }
}
