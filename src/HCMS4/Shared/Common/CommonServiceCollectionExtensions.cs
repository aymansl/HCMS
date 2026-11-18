namespace HCMS4.Shared.Common
{
    public static class CommonServiceCollectionExtensions
    {
        public static IServiceCollection AddSharedCommon(this IServiceCollection services)
        {
            // All dates and times are Damascus time (see ClinicTime).
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IClinicClock>(sp =>
                new ClinicClock(sp.GetRequiredService<TimeProvider>(), ClinicTime.TimeZone));
            return services;
        }
    }
}
