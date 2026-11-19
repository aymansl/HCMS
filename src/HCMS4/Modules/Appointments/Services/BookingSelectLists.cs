using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.ViewModels;

namespace HCMS4.Modules.Appointments.Services
{
    /// <summary>Maps doctors and patients to the drop-down items of the booking form.</summary>
    public static class BookingSelectLists
    {
        public static List<DoctorSelectDto> ToDoctorOptions(IEnumerable<Doctor> doctors) =>
            doctors.Select(d => new DoctorSelectDto
            {
                Id = d.Id,
                FullName = d.User != null ? d.User.FullName : "Unknown",
                Specialization = d.Specialization != null ? d.Specialization.Name : "Not set",
                ConsultationFee = d.Specialization != null ? d.Specialization.ConsultationFee : 0
            }).ToList();

        public static List<PatientSelectDto> ToPatientOptions(IEnumerable<Patient> patients) =>
            patients.Select(p => new PatientSelectDto
            {
                Id = p.Id,
                FullName = p.User != null ? p.User.FullName : "Unknown",
                Email = p.User?.Email ?? "N/A"
            }).ToList();
    }
}
