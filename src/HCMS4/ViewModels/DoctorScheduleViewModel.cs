using HCMS4.Models;

namespace HCMS4.ViewModels
{
    public class DoctorScheduleViewModel
    {

        public int DoctorId { get; set; }
        public string DoctorName { get; set; }
        public string Specialization { get; set; }
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

        public bool HasAppointments => AllAppointments.Any();
        public bool IsFilteredView => SelectedStatus != "all";

     
        public DateTime Today => DateTime.Today;
    }
}
