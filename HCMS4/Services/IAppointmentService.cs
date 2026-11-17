using HCMS4.Models;
using HCMS4.Models.Common;

namespace HCMS4.Services
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
    }
}
