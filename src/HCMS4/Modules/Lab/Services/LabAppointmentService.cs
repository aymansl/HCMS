using System.Globalization;
using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Lab.Services
{
    /// <summary>Sample-collection bookings from patients (UC-L05).</summary>
    public interface ILabAppointmentService
    {
        /// <summary>The patient's lab requests, newest first, each with its active booking.</summary>
        Task<List<PatientLabRequestRow>> GetPatientRequestsAsync(string patientUserId);

        /// <summary>The booking page for one of the patient's requests, or null when it is not theirs.</summary>
        Task<BookLabAppointmentViewModel?> GetBookingFormAsync(string patientUserId, int requestId, DateOnly? date);

        /// <summary>The free future slots of a day.</summary>
        Task<List<DateTime>> GetFreeSlotsAsync(DateOnly date);

        /// <summary>Books a free slot for the request, or moves its booking there (alternative flow).</summary>
        Task<ServiceResult<LabAppointment>> BookAsync(string patientUserId, int requestId, DateTime slot);

        /// <summary>The lab's schedule for a day, by slot time.</summary>
        Task<List<LabAppointment>> GetScheduleAsync(DateOnly date);
    }

    public class LabAppointmentService : ILabAppointmentService
    {
        public const string SlotTakenError = "This time was just booked by another patient. Choose another time.";
        public const string InvalidSlotError = "Choose a time from the available slots.";
        public const string NotBookableError = "A sample-collection time can only be booked for a request that is waiting for its sample.";

        /// <summary>Requests whose sample is not collected yet.</summary>
        public static readonly IReadOnlyList<LabRequestStatus> BookableStatuses =
            new[] { LabRequestStatus.Requested, LabRequestStatus.Received };

        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;
        private readonly IClinicClock _clock;

        public LabAppointmentService(ApplicationDbContext context, INotificationService notifications, IClinicClock clock)
        {
            _context = context;
            _notifications = notifications;
            _clock = clock;
        }

        private static bool IsBookable(LabRequest request) => BookableStatuses.Contains(request.Status);

        private IQueryable<LabRequest> PatientRequests(string patientUserId) => _context.LabRequests.AsNoTracking()
            .Include(r => r.Doctor).ThenInclude(d => d.User)
            .Include(r => r.Items).ThenInclude(i => i.LabTest)
            .Where(r => r.Patient.UserId == patientUserId);

        private Task<LabAppointment?> ActiveAppointmentAsync(int requestId) =>
            _context.LabAppointments.FirstOrDefaultAsync(a => a.LabRequestId == requestId && a.Status == LabAppointmentStatus.Booked);

        public async Task<List<PatientLabRequestRow>> GetPatientRequestsAsync(string patientUserId)
        {
            var requests = await PatientRequests(patientUserId).OrderByDescending(r => r.RequestedAt).ToListAsync();
            var ids = requests.Select(r => r.Id).ToList();
            var appointments = await _context.LabAppointments.AsNoTracking()
                .Where(a => ids.Contains(a.LabRequestId) && a.Status == LabAppointmentStatus.Booked)
                .ToDictionaryAsync(a => a.LabRequestId);

            return requests.Select(r => new PatientLabRequestRow
            {
                Request = r,
                Appointment = appointments.GetValueOrDefault(r.Id),
                CanBook = IsBookable(r)
            }).ToList();
        }

        /// <summary>Today (if a slot is still ahead) and the next working days, up to the booking limit.</summary>
        private List<DateOnly> BookableDates()
        {
            var today = DateOnly.FromDateTime(_clock.Today);
            return Enumerable.Range(0, LabSchedule.BookingDays + 1)
                .Select(today.AddDays)
                .Where(LabSchedule.IsWorkingDay)
                .Where(d => LabSchedule.SlotsFor(d).Any(s => s > _clock.Now))
                .ToList();
        }

        public async Task<BookLabAppointmentViewModel?> GetBookingFormAsync(string patientUserId, int requestId, DateOnly? date)
        {
            var request = await PatientRequests(patientUserId).FirstOrDefaultAsync(r => r.Id == requestId);
            if (request == null)
                return null;

            var dates = BookableDates();
            var selected = date.HasValue && dates.Contains(date.Value) ? date.Value : dates.FirstOrDefault();

            return new BookLabAppointmentViewModel
            {
                Request = request,
                Appointment = await _context.LabAppointments.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.LabRequestId == requestId && a.Status == LabAppointmentStatus.Booked),
                BookableDates = dates,
                SelectedDate = selected,
                FreeSlots = dates.Count == 0 ? new List<DateTime>() : await GetFreeSlotsAsync(selected)
            };
        }

        public async Task<List<DateTime>> GetFreeSlotsAsync(DateOnly date)
        {
            var from = date.ToDateTime(TimeOnly.MinValue);
            var to = from.AddDays(1);
            var taken = await _context.LabAppointments.AsNoTracking()
                .Where(a => a.Status == LabAppointmentStatus.Booked && a.SlotStart >= from && a.SlotStart < to)
                .Select(a => a.SlotStart)
                .ToListAsync();

            return LabSchedule.SlotsFor(date).Where(s => s > _clock.Now && !taken.Contains(s)).ToList();
        }

        public async Task<ServiceResult<LabAppointment>> BookAsync(string patientUserId, int requestId, DateTime slot)
        {
            var request = await _context.LabRequests.Include(r => r.Patient)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.Patient.UserId == patientUserId);
            if (request == null)
                return ServiceResult<LabAppointment>.Fail("Lab request not found.");

            if (!IsBookable(request))
                return ServiceResult<LabAppointment>.Fail(NotBookableError);

            var lastDay = DateOnly.FromDateTime(_clock.Today).AddDays(LabSchedule.BookingDays);
            if (!LabSchedule.IsValidSlot(slot) || slot <= _clock.Now || DateOnly.FromDateTime(slot) > lastDay)
                return ServiceResult<LabAppointment>.Fail(InvalidSlotError);

            var appointment = await ActiveAppointmentAsync(requestId);
            if (appointment?.SlotStart == slot)
                return ServiceResult<LabAppointment>.Ok(appointment, "You already have this time.");

            var taken = await _context.LabAppointments.AnyAsync(a =>
                a.SlotStart == slot && a.Status == LabAppointmentStatus.Booked && a.LabRequestId != requestId);
            if (taken)
                return ServiceResult<LabAppointment>.Fail(SlotTakenError);

            var now = _clock.Now;
            var changed = appointment != null;
            if (appointment == null)
            {
                appointment = new LabAppointment { LabRequestId = requestId, BookedAt = now };
                _context.LabAppointments.Add(appointment);
            }

            appointment.SlotStart = slot;
            appointment.Status = LabAppointmentStatus.Booked;
            appointment.UpdatedAt = now;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Another patient took the slot between the check and the save, and the unique index refused
                // this booking. Any other database error is not hidden.
                _context.ChangeTracker.Clear();
                var takenNow = await _context.LabAppointments.AnyAsync(a =>
                    a.SlotStart == slot && a.Status == LabAppointmentStatus.Booked && a.LabRequestId != requestId);
                if (takenNow)
                    return ServiceResult<LabAppointment>.Fail(SlotTakenError);
                throw;
            }

            var when = slot.ToString("dddd yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            var scheduleLink = $"/Lab/Schedule?date={slot.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            await _notifications.CreateForRoleAsync(Roles.LabTechnician,
                changed ? "Lab appointment changed" : "New lab appointment",
                $"Lab request #{requestId}: sample collection {(changed ? "moved to" : "booked for")} {when}.",
                NotificationType.LabRequest, scheduleLink, nameof(LabAppointment), appointment.Id);
            await _notifications.CreateForUserAsync(patientUserId,
                changed ? "Lab appointment changed" : "Lab appointment confirmed",
                $"Your sample collection for lab request #{requestId} is on {when}.",
                NotificationType.LabRequest, "/Patient/LabRequests", nameof(LabAppointment), appointment.Id);

            return ServiceResult<LabAppointment>.Ok(appointment, changed
                ? $"Your lab appointment was changed to {when}."
                : $"Your lab appointment is booked for {when}.");
        }

        public Task<List<LabAppointment>> GetScheduleAsync(DateOnly date)
        {
            var from = date.ToDateTime(TimeOnly.MinValue);
            var to = from.AddDays(1);
            return _context.LabAppointments.AsNoTracking()
                .Include(a => a.LabRequest).ThenInclude(r => r.Patient).ThenInclude(p => p.User)
                .Include(a => a.LabRequest).ThenInclude(r => r.Items).ThenInclude(i => i.LabTest)
                .Where(a => a.Status == LabAppointmentStatus.Booked && a.SlotStart >= from && a.SlotStart < to)
                .OrderBy(a => a.SlotStart)
                .ToListAsync();
        }
    }
}
