using HCMS4.Data;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Feedback.Services
{
    public interface IVisitRatingService
    {
        /// <summary>The patient's visit ratings, keyed by appointment id (value = rating id).</summary>
        Task<Dictionary<int, int>> GetRatingIdsByAppointmentAsync(int patientId);
    }

    public class VisitRatingService : IVisitRatingService
    {
        private readonly ApplicationDbContext _context;

        public VisitRatingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<Dictionary<int, int>> GetRatingIdsByAppointmentAsync(int patientId) =>
            _context.VisitRatings.AsNoTracking()
                .Where(vr => vr.PatientId == patientId)
                .ToDictionaryAsync(vr => vr.AppointmentId, vr => vr.Id);
    }
}
