using HCMS4.Models;

namespace HCMS4.ViewModels
{
    public class PatientViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;
        public DateTime? DateOfBirth { get; set; }
        public string? Address { get; set; }
        public string? EmergencyContact { get; set; }
        public List<Appointment> Appointments { get; set; } = new();
        public List<Prescription> Prescriptions { get; set; } = new();
        public List<Invoice> Invoices { get; set; } = new();
    }
}
