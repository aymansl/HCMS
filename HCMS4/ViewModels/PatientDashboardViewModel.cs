using HCMS4.Models;

namespace HCMS4.ViewModels
{
    public class PatientDashboardViewModel
    {
        public int UpcomingAppointmentsCount { get; set; }
        public int CompletedVisitsCount { get; set; }
        public int ActiveSurveyCount { get; set; }
        public int OpenComplaintCount { get; set; }
        public List<AppointmentHistoryViewModel> UpcomingAppointments { get; set; } = new();
        public List<MedicalArticle> PublishedArticles { get; set; } = new();
        public List<UserNotification> RecentNotifications { get; set; } = new();
    }
}
