using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Lab.Services
{
    /// <summary>Search of issued results and the printable result report (UC-L08).</summary>
    public interface ILabResultSearchService
    {
        /// <summary>
        /// Issued requests matching the filter, newest first. With no criteria, today's issued results
        /// (UC-L08 alternative flow).
        /// </summary>
        Task<List<LabRequest>> SearchIssuedAsync(IssuedResultFilter filter);

        /// <summary>One issued request with everything its printed report shows, or null.</summary>
        Task<LabResultReportViewModel?> GetReportAsync(int requestId);
    }

    public class LabResultSearchService : ILabResultSearchService
    {
        private readonly ApplicationDbContext _context;
        private readonly IClinicClock _clock;

        public LabResultSearchService(ApplicationDbContext context, IClinicClock clock)
        {
            _context = context;
            _clock = clock;
        }

        private IQueryable<LabRequest> IssuedWithDetails() => _context.LabRequests.AsNoTracking()
            .Include(r => r.Patient).ThenInclude(p => p.User)
            .Include(r => r.Doctor).ThenInclude(d => d.User)
            .Include(r => r.Items).ThenInclude(i => i.LabTest)
            .Include(r => r.Items).ThenInclude(i => i.Result)
            .Where(r => r.Status == LabRequestStatus.ResultIssued);

        public async Task<List<LabRequest>> SearchIssuedAsync(IssuedResultFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);
            var query = IssuedWithDetails();

            // No criteria: today's results.
            var date = filter.IsEmpty ? DateOnly.FromDateTime(_clock.Today) : filter.Date;
            if (date is { } day)
            {
                var from = day.ToDateTime(TimeOnly.MinValue);
                var to = from.AddDays(1);
                query = query.Where(r => r.ResultIssuedAt >= from && r.ResultIssuedAt < to);
            }

            if (filter.RequestId is { } requestId)
                query = query.Where(r => r.Id == requestId);

            if (filter.TestId is { } testId)
                query = query.Where(r => r.Items.Any(i => i.LabTestId == testId));

            if (!string.IsNullOrWhiteSpace(filter.PatientName))
            {
                // Case-insensitive whatever the collation; EF translates ToUpper() to SQL UPPER().
                var name = filter.PatientName.Trim().ToUpperInvariant();
#pragma warning disable CA1304, CA1311, CA1862
                query = query.Where(r => (r.Patient.User.FirstName + " " + r.Patient.User.LastName).ToUpper().Contains(name));
#pragma warning restore CA1304, CA1311, CA1862
            }

            return await query.OrderByDescending(r => r.ResultIssuedAt).ThenByDescending(r => r.Id).ToListAsync();
        }

        public async Task<LabResultReportViewModel?> GetReportAsync(int requestId)
        {
            var request = await IssuedWithDetails().FirstOrDefaultAsync(r => r.Id == requestId);
            if (request == null)
                return null;

            var issuedBy = await _context.Users.AsNoTracking().Where(u => u.Id == request.ResultIssuedBy)
                .Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync();

            return new LabResultReportViewModel { Request = request, IssuedByName = issuedBy, PrintedAt = _clock.Now };
        }
    }
}
