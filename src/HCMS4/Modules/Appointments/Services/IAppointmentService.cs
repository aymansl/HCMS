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
    }
}
