using HCMS4.Models;

namespace HCMS4.ViewModels
{
    public class DoctorDashboardViewModel
    {
        public int TodayAppointmentsCount { get; set; }
        public int PendingReissueRequestsCount { get; set; }
        public int PendingReviewRequestsCount { get; set; }
        public int PublishedArticlesCount { get; set; }
        public int DraftArticlesCount { get; set; }
        public decimal AverageRating { get; set; }
        public int RatingCount { get; set; }
        public List<VisitRating> RecentRatings { get; set; } = new();
        public List<UserNotification> RecentNotifications { get; set; } = new();
    }
}
