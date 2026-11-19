using HCMS4.Data;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Feedback.Services
{
    public interface IComplaintService
    {
        Task<List<Complaint>> GetAllAsync();
        /// <summary>Opens a complaint for the admin; a submitted complaint moves to "under review".</summary>
        Task<Complaint?> OpenForReviewAsync(int id);
        /// <summary>The updated complaint includes its patient, for the notification.</summary>
        Task<ServiceResult<Complaint>> UpdateStatusAsync(int id, ComplaintStatus status, string? resolutionNotes);

        Task<List<Complaint>> GetForPatientAsync(int patientId);
        Task<Complaint> CreateAsync(int patientId, PatientComplaintFormViewModel model, string? attachmentFileName);
    }

    public class ComplaintService : IComplaintService
    {
        private readonly ApplicationDbContext _context;
        private readonly IClinicClock _clock;

        public ComplaintService(ApplicationDbContext context, IClinicClock clock)
        {
            _context = context;
            _clock = clock;
        }

        public Task<List<Complaint>> GetAllAsync() =>
            _context.Complaints.AsNoTracking()
                .Include(c => c.Patient)
                    .ThenInclude(p => p.User)
                .OrderByDescending(c => c.SubmissionDate)
                .ToListAsync();

        public async Task<Complaint?> OpenForReviewAsync(int id)
        {
            var complaint = await _context.Complaints
                .Include(c => c.Patient)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (complaint?.Status == ComplaintStatus.Submitted)
            {
                complaint.Status = ComplaintStatus.UnderReview;
                await _context.SaveChangesAsync();
            }

            return complaint;
        }

        public async Task<ServiceResult<Complaint>> UpdateStatusAsync(int id, ComplaintStatus status, string? resolutionNotes)
        {
            var complaint = await _context.Complaints
                .Include(c => c.Patient)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
                return ServiceResult<Complaint>.Fail("Complaint not found.");

            complaint.Status = status;
            complaint.ResolutionNotes = resolutionNotes;
            complaint.ResolvedAt = status == ComplaintStatus.Resolved || status == ComplaintStatus.Closed
                ? _clock.Now
                : null;
            await _context.SaveChangesAsync();

            return ServiceResult<Complaint>.Ok(complaint, "Complaint updated successfully.");
        }

        public Task<List<Complaint>> GetForPatientAsync(int patientId) =>
            _context.Complaints.AsNoTracking()
                .Where(c => c.PatientId == patientId)
                .OrderByDescending(c => c.SubmissionDate)
                .ToListAsync();

        public async Task<Complaint> CreateAsync(int patientId, PatientComplaintFormViewModel model, string? attachmentFileName)
        {
            var now = _clock.Now;
            var complaint = new Complaint
            {
                PatientId = patientId,
                Title = model.Title.Trim(),
                Type = model.Type,
                Description = model.Description.Trim(),
                AssociatedVisitDate = model.AssociatedVisitDate,
                AttachmentPath = attachmentFileName,
                TrackingNumber = $"CMP-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                SubmissionDate = now,
                Status = ComplaintStatus.Submitted
            };

            _context.Complaints.Add(complaint);
            await _context.SaveChangesAsync();
            return complaint;
        }
    }
}
