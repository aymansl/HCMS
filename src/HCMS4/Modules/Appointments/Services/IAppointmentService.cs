using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Common;
namespace HCMS4.Modules.Appointments.Services
{
    public interface IAppointmentService
    {
        Task<Appointment> GetByIdAsync(int id);
        Task<Appointment> GetByIdWithDetailsAsync(int id);
        Task<PaginatedResult<Appointment>> GetAllAsync(PaginationParams pagination, string status = "all");
        Task<List<Appointment>> GetByDoctorIdAsync(int doctorId);
        Task<List<Appointment>> GetByPatientIdAsync(int patientId);
        Task<List<Appointment>> GetTodayAppointmentsAsync(int? doctorId = null, int? patientId = null);
        Task<List<Appointment>> GetUpcomingAppointmentsAsync(int? doctorId = null, int days = 30);
        Task<List<Appointment>> GetCanceledAppointmentsAsync(int? doctorId = null);
        Task<ServiceResult> CreateAsync(Appointment appointment);
        Task<ServiceResult> RescheduleAsync(int id, DateTime newDateTime);
        Task<ServiceResult> CancelAsync(int id, string reason);
        Task UpdatePastScheduledToCompletedAsync();
        Task<bool> IsTimeSlotAvailableAsync(int doctorId, DateTime dateTime, int? excludeAppointmentId = null);

        /// <summary>Consultation fee of the doctor's specialization; 0 when the doctor or specialization is unknown.</summary>
        Task<decimal> GetConsultationFeeAsync(int doctorId);

        Task<int> CountByStatusAsync(AppointmentStatus status);

        /// <summary>A doctor's appointments whose date is between <paramref name="fromDate"/> and <paramref name="toDate"/> (inclusive), oldest first.</summary>
        Task<List<Appointment>> GetDoctorAppointmentsAsync(int doctorId, DateTime fromDate, DateTime toDate, bool scheduledOnly, int? take = null);

        /// <summary>Completed appointments of all doctors on the given date, with patient and user.</summary>
        Task<List<Appointment>> GetCompletedOnDateAsync(DateTime date);

        /// <summary>Number of completed appointments per patient.</summary>
        Task<Dictionary<int, int>> CountCompletedByPatientAsync(IReadOnlyCollection<int> patientIds);
    }
}
