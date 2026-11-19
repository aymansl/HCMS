using HCMS4.Modules.Feedback.Services;

namespace HCMS4.Modules.Feedback
{
    public static class FeedbackModule
    {
        public static IServiceCollection AddFeedbackModule(this IServiceCollection services)
        {
            services.AddScoped<IComplaintAttachmentService, ComplaintAttachmentService>();
            services.AddScoped<IVisitRatingService, VisitRatingService>();
            services.AddScoped<IComplaintService, ComplaintService>();
            services.AddScoped<ISurveyService, SurveyService>();
            services.AddScoped<IMedicalArticleService, MedicalArticleService>();
            return services;
        }
    }
}
