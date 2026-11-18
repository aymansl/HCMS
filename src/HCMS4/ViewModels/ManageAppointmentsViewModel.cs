using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
namespace HCMS4.ViewModels
{
    public class ManageAppointmentsViewModel
    {
        public List<Appointment> TodayAppointments { get; set; } = new();
        public List<Appointment> UpcomingAppointments { get; set; } = new();
        public List<Appointment> CanceledAppointments { get; set; } = new();
        public List<Appointment> FilteredAppointments { get; set; } = new();
        public string SelectedStatus { get; set; } = "all";
        public List<Doctor> AvailableDoctors { get; set; } = new();
        public List<Patient> AvailablePatients { get; set; } = new();
        public bool IsFilteredView => SelectedStatus != "all";
        public bool AnalyticsServiceAvailable { get; set; } = true;
        public bool IsUsingAI { get; set; } = false;
        public Dictionary<int, double> AppointmentRiskScores { get; set; } = new();
        public string SelectedRiskLevel { get; set; } = "all";
    }
}
