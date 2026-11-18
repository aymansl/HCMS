using HCMS4.Modules.Feedback.Services;

namespace HCMS4.Modules.Feedback
{
    public static class FeedbackModule
    {
        public static IServiceCollection AddFeedbackModule(this IServiceCollection services)
        {
            services.AddScoped<IComplaintAttachmentService, ComplaintAttachmentService>();
            return services;
        }
    }
}
