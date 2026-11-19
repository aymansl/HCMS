using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Common;
namespace HCMS4.ViewModels
{
    public class DoctorScheduleViewModel
    {

        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public string SelectedStatus { get; set; } = "all";

        
        public List<Appointment> AllAppointments { get; set; } = new();
        public List<Appointment> TodayAppointments { get; set; } = new();
        public List<Appointment> UpcomingAppointments { get; set; } = new();
        public List<Appointment> CompletedAppointments { get; set; } = new();
        public List<Appointment> CanceledAppointments { get; set; } = new();
        public List<Appointment> FilteredAppointments { get; set; } = new();

        
        public int TotalAppointments { get; set; }
        public int ScheduledCount { get; set; }
        public int CompletedCount { get; set; }
        public int CanceledCount { get; set; }

        public bool HasAppointments => AllAppointments.Count > 0;
        public bool IsFilteredView => SelectedStatus != "all";

     
        public DateTime Today => ClinicTime.Today;
    }
}
