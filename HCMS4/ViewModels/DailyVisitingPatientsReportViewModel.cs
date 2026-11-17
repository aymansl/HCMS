namespace HCMS4.ViewModels
{
    public class DailyVisitingPatientsReportViewModel
    {
        public DateTime ReportDate { get; set; }
        public List<VisitingPatientItem> Patients { get; set; } = new();
        public int TotalPatients { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class VisitingPatientItem
    {
        public int AppointmentId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public DateTime AppointmentTime { get; set; }
        public string VisitType { get; set; } = string.Empty;
        public string? Diagnosis { get; set; }
        public bool HasPrescription { get; set; }
    }
}
