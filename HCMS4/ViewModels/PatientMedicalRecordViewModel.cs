using HCMS4.Models;
using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class PatientMedicalRecordViewModel
    {
        public int PatientId { get; set; }
        [Display(Name = "Appointment")]
        public int? AppointmentId { get; set; }

        // Patient Information
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }

        [Display(Name = "Date of Birth")]
        [DisplayFormat(DataFormatString = "{0:MMMM dd, yyyy}")]
        public DateTime? DateOfBirth { get; set; }

        public int? Age => DateOfBirth.HasValue ?
            DateTime.Now.Year - DateOfBirth.Value.Year -
            (DateTime.Now.DayOfYear < DateOfBirth.Value.DayOfYear ? 1 : 0) : null;

        public string? Address { get; set; }

        [Display(Name = "Emergency Contact")]
        public string? EmergencyContact { get; set; }

        [Display(Name = "Chronic Conditions")]
        public string? ChronicConditions { get; set; }

        // Medical History
        public List<AppointmentHistoryViewModel> AppointmentHistory { get; set; } = new();
        public List<PrescriptionViewModel> Prescriptions { get; set; } = new();
        public List<ClinicalNoteViewModel> ClinicalNotes { get; set; } = new();
        public List<InvoiceViewModel> Invoices { get; set; } = new();

        // Full entities for recent activity
        public Appointment? LastAppointment { get; set; }
        public Prescription? LastPrescription { get; set; }

        // Statistics
        public int TotalAppointments => AppointmentHistory.Count;
        public int TotalPrescriptions => Prescriptions.Count;
        public int TotalClinicalNotes => ClinicalNotes.Count;

        public bool HasMedicalHistory => AppointmentHistory.Any() || Prescriptions.Any() || ClinicalNotes.Any();
    }


}