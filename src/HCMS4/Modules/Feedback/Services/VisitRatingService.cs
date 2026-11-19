using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Feedback.Services
{
    public interface IVisitRatingService
    {
        /// <summary>The patient's visit ratings, keyed by appointment id (value = rating id).</summary>
        Task<Dictionary<int, int>> GetRatingIdsByAppointmentAsync(int patientId);

        /// <summary>The rating form for one of the patient's completed, not yet rated visits.</summary>
        Task<ServiceResult<VisitRatingFormViewModel>> GetRatingFormAsync(int patientId, int appointmentId);

        /// <summary>Saves the rating and refreshes the doctor's average rating; returns the rating id.</summary>
        Task<ServiceResult<int>> RateAsync(int patientId, VisitRatingFormViewModel model);
    }

    public class VisitRatingService : IVisitRatingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IClinicClock _clock;

        public VisitRatingService(ApplicationDbContext context, IClinicClock clock)
        {
            _context = context;
            _clock = clock;
        }

        public Task<Dictionary<int, int>> GetRatingIdsByAppointmentAsync(int patientId) =>
            _context.VisitRatings.AsNoTracking()
                .Where(vr => vr.PatientId == patientId)
                .ToDictionaryAsync(vr => vr.AppointmentId, vr => vr.Id);

        public async Task<ServiceResult<VisitRatingFormViewModel>> GetRatingFormAsync(int patientId, int appointmentId)
        {
            var check = await CheckRateableAsync(patientId, appointmentId);
            if (!check.Success)
                return ServiceResult<VisitRatingFormViewModel>.Fail(check.Message);

            var appointment = check.Data;
            return ServiceResult<VisitRatingFormViewModel>.Ok(new VisitRatingFormViewModel
            {
                AppointmentId = appointment.Id,
                DoctorId = appointment.DoctorId,
                DoctorName = appointment.Doctor?.User?.FullName ?? "Doctor",
                AppointmentDateTime = appointment.AppointmentDateTime
            });
        }

        public async Task<ServiceResult<int>> RateAsync(int patientId, VisitRatingFormViewModel model)
        {
            var check = await CheckRateableAsync(patientId, model.AppointmentId);
            if (!check.Success)
                return ServiceResult<int>.Fail(check.Message);

            var appointment = check.Data;
            var rating = new VisitRating
            {
                AppointmentId = appointment.Id,
                PatientId = patientId,
                DoctorId = appointment.DoctorId,
                Rating = model.Rating,
                Comment = model.Comment,
                DoctorCooperative = model.DoctorCooperative,
                WaitingTimeReasonable = model.WaitingTimeReasonable,
                CreatedAt = _clock.Now
            };
            _context.VisitRatings.Add(rating);
            await _context.SaveChangesAsync();

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == appointment.DoctorId);
            if (doctor != null)
            {
                var ratings = _context.VisitRatings.Where(vr => vr.DoctorId == doctor.Id);
                doctor.RatingCount = await ratings.CountAsync();
                doctor.AverageRating = Math.Round((decimal)await ratings.AverageAsync(vr => vr.Rating), 2);
                await _context.SaveChangesAsync();
            }

            return ServiceResult<int>.Ok(rating.Id, "Thank you for your rating, your feedback has been submitted successfully.");
        }

        private async Task<ServiceResult<Appointment>> CheckRateableAsync(int patientId, int appointmentId)
        {
            var appointment = await _context.Appointments.AsNoTracking()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId);

            if (appointment == null || appointment.Status != AppointmentStatus.Completed)
                return ServiceResult<Appointment>.Fail("Only completed visits can be rated.");

            if (await _context.VisitRatings.AnyAsync(vr => vr.PatientId == patientId && vr.AppointmentId == appointmentId))
                return ServiceResult<Appointment>.Fail("You have already rated this visit.");

            return ServiceResult<Appointment>.Ok(appointment);
        }
    }
}
