using HCMS4.Data;
using HCMS4.Models;
using HCMS4.Models.Common;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AppointmentService> _logger;

        public AppointmentService(ApplicationDbContext context, ILogger<AppointmentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Appointment> GetByIdAsync(int id)
        {
            return await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<Appointment> GetByIdWithDetailsAsync(int id)
        {
            return await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Specialization)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<PaginatedResult<Appointment>> GetAllAsync(PaginationParams pagination, string status = "all")
        {
            var query = _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .AsNoTracking()
                .AsQueryable();

            if (status != "all" && Enum.TryParse<AppointmentStatus>(status, out var statusEnum))
            {
                query = query.Where(a => a.Status == statusEnum);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.AppointmentDateTime)
                .Skip((pagination.PageNumber - 1) * pagination.PageSize)
                .Take(pagination.PageSize)
                .ToListAsync();

            return new PaginatedResult<Appointment>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pagination.PageNumber,
                PageSize = pagination.PageSize
            };
        }

        public async Task<List<Appointment>> GetByDoctorIdAsync(int doctorId)
        {
            return await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Where(a => a.DoctorId == doctorId)
                .OrderByDescending(a => a.AppointmentDateTime)
                .ToListAsync();
        }

        public async Task<List<Appointment>> GetByPatientIdAsync(int patientId)
        {
            return await _context.Appointments
                .AsNoTracking()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Specialization)
                .Where(a => a.PatientId == patientId)
                .OrderByDescending(a => a.AppointmentDateTime)
                .ToListAsync();
        }

        public async Task<List<Appointment>> GetTodayAppointmentsAsync(int? doctorId = null, int? patientId = null)
        {
            var today = DateTime.Today;
            var query = _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .AsNoTracking()
                .Where(a => a.AppointmentDateTime.Date == today && a.Status == AppointmentStatus.Scheduled);

            if (doctorId.HasValue)
                query = query.Where(a => a.DoctorId == doctorId.Value);
            if (patientId.HasValue)
                query = query.Where(a => a.PatientId == patientId.Value);

            return await query.OrderBy(a => a.AppointmentDateTime).ToListAsync();
        }

        public async Task<List<Appointment>> GetUpcomingAppointmentsAsync(int? doctorId = null, int days = BusinessRules.UpcomingAppointmentsWindowDays)
        {
            var today = DateTime.Today;
            var query = _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .AsNoTracking()
                .Where(a => a.AppointmentDateTime.Date > today && a.Status == AppointmentStatus.Scheduled);

            if (doctorId.HasValue)
                query = query.Where(a => a.DoctorId == doctorId.Value);

            return await query
                .OrderBy(a => a.AppointmentDateTime)
                .Take(BusinessRules.MaxUpcomingAppointments)
                .ToListAsync();
        }

        public async Task<List<Appointment>> GetCanceledAppointmentsAsync(int? doctorId = null)
        {
            var today = DateTime.Today;
            var query = _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .AsNoTracking()
                .Where(a => a.Status == AppointmentStatus.Canceled && a.AppointmentDateTime.Date >= today);

            if (doctorId.HasValue)
                query = query.Where(a => a.DoctorId == doctorId.Value);

            return await query
                .OrderByDescending(a => a.AppointmentDateTime)
                .Take(20)
                .ToListAsync();
        }

        public async Task<ServiceResult> CreateAsync(Appointment appointment)
        {
            try
            {
                if (await IsTimeSlotAvailableAsync(appointment.DoctorId, appointment.AppointmentDateTime))
                {
                    appointment.CreatedAt = DateTime.UtcNow;
                    appointment.UpdatedAt = DateTime.UtcNow;
                    appointment.Status = AppointmentStatus.Scheduled;

                    _context.Appointments.Add(appointment);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Appointment created successfully for patient {PatientId} with doctor {DoctorId}",
                        appointment.PatientId, appointment.DoctorId);

                    return ServiceResult.Ok("Appointment booked successfully.");
                }

                return ServiceResult.Fail("This time slot is not available for the selected doctor.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating appointment");
                return ServiceResult.Fail("An error occurred while booking the appointment.");
            }
        }

        public async Task<ServiceResult> RescheduleAsync(int id, DateTime newDateTime)
        {
            try
            {
                var appointment = await _context.Appointments.FindAsync(id);
                if (appointment == null)
                {
                    return ServiceResult.Fail("Appointment not found.");
                }

                if (appointment.Status != AppointmentStatus.Scheduled)
                {
                    return ServiceResult.Fail("Only scheduled appointments can be rescheduled.");
                }

                if (!await IsTimeSlotAvailableAsync(appointment.DoctorId, newDateTime, id))
                {
                    return ServiceResult.Fail("This time slot is already booked. Please choose another time.");
                }

                // Preserve the original ConsultationFee when rescheduling
                var originalFee = appointment.ConsultationFee;
                appointment.AppointmentDateTime = newDateTime;
                appointment.UpdatedAt = DateTime.UtcNow;
                appointment.ConsultationFee = originalFee;

                _context.Update(appointment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Appointment {AppointmentId} rescheduled to {NewDateTime}", id, newDateTime);
                return ServiceResult.Ok("Appointment rescheduled successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rescheduling appointment {AppointmentId}", id);
                return ServiceResult.Fail("An error occurred while rescheduling the appointment.");
            }
        }

        public async Task<ServiceResult> CancelAsync(int id, string reason)
        {
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.Patient)
                        .ThenInclude(p => p.User)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (appointment == null)
                {
                    return ServiceResult.Fail("Appointment not found.");
                }

                if (appointment.Status != AppointmentStatus.Scheduled)
                {
                    return ServiceResult.Fail($"Only scheduled appointments can be canceled. Current status: {appointment.Status}");
                }

                if (appointment.AppointmentDateTime <= DateTime.UtcNow.AddHours(BusinessRules.AppointmentCancellationWindowHours))
                {
                    return ServiceResult.Fail($"Appointments cannot be canceled less than {BusinessRules.AppointmentCancellationWindowHours} hours before the scheduled time.");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    return ServiceResult.Fail("Cancellation reason is required.");
                }

                appointment.Status = AppointmentStatus.Canceled;
                appointment.CancellationReason = reason.Trim();
                appointment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Appointment {AppointmentId} canceled. Patient: {PatientName}",
                    id, appointment.Patient?.User?.FullName);

                return ServiceResult.Ok($"Appointment for {appointment.Patient?.User?.FullName} canceled successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling appointment {AppointmentId}", id);
                return ServiceResult.Fail("An error occurred while canceling the appointment.");
            }
        }

        public async Task UpdatePastScheduledToCompletedAsync()
        {
            var now = DateTime.UtcNow;
            var pastScheduledAppointments = await _context.Appointments
                .Where(a => a.AppointmentDateTime < now && a.Status == AppointmentStatus.Scheduled)
                .ToListAsync();

            if (pastScheduledAppointments.Any())
            {
                foreach (var appointment in pastScheduledAppointments)
                {
                    appointment.Status = AppointmentStatus.Completed;
                    appointment.UpdatedAt = now;
                }
                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated {Count} past scheduled appointments to Completed",
                    pastScheduledAppointments.Count);
            }
        }

        public async Task<bool> IsTimeSlotAvailableAsync(int doctorId, DateTime dateTime, int? excludeAppointmentId = null)
        {
            var query = _context.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId &&
                           a.AppointmentDateTime >= dateTime.AddMinutes(-BusinessRules.AppointmentSlotBufferMinutes) &&
                           a.AppointmentDateTime <= dateTime.AddMinutes(BusinessRules.AppointmentSlotBufferMinutes) &&
                           a.Status != AppointmentStatus.Canceled);

            if (excludeAppointmentId.HasValue)
            {
                query = query.Where(a => a.Id != excludeAppointmentId.Value);
            }

            return !await query.AnyAsync();
        }
    }
}
