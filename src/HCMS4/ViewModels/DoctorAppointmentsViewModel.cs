using HCMS4.Models;

namespace HCMS4.ViewModels
{
    public class DoctorAppointmentsViewModel
    {
        public List<Appointment> TodayAppointments { get; set; } = new();
        public List<Appointment> UpcomingAppointments { get; set; } = new();
        public List<Appointment> PastAppointments { get; set; } = new();
        public DateTime? SelectedDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string ViewMode { get; set; } = "today"; // today, date, range
        public string SelectedRiskLevel { get; set; } = "all";
        public bool AnalyticsServiceAvailable { get; set; } = true;
        public bool IsUsingAI { get; set; }
        public Dictionary<int, double> AppointmentRiskScores { get; set; } = new();

        // Statistics
        public int TotalTodayAppointments => TodayAppointments.Count;
        public int TotalUpcomingAppointments => UpcomingAppointments.Count;
        public int TotalPastAppointments => PastAppointments.Count;
    }
}
