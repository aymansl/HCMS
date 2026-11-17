using HCMS4.Models;

namespace HCMS4.ViewModels
{
    public class PatientStatsViewModel
    {
        public List<Patient> Patients { get; set; } = new();
        public int TotalPatients => Patients.Count;
        public int TotalScheduledAppointments { get; set; }
        public int TotalPrescriptions { get; set; }
        public int PendingInvoicesCount { get; set; }

    }
}
