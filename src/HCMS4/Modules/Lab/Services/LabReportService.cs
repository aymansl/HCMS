using HCMS4.Data;
using HCMS4.Modules.Lab.Models;
using HCMS4.Modules.Lab.ViewModels;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Lab.Services
{
    /// <summary>The daily lab activity report (UC-L11).</summary>
    public interface ILabReportService
    {
        /// <summary>The report for <paramref name="from"/>–<paramref name="to"/> (both days included).</summary>
        Task<ServiceResult<LabActivityReport>> BuildAsync(DateOnly from, DateOnly to);

        /// <summary>The page model for the given dates; both default to today.</summary>
        Task<LabActivityReportPageViewModel> BuildPageAsync(DateOnly? from, DateOnly? to);
    }

    public class LabReportService : ILabReportService
    {
        public const int MaxDays = 366;
        public const string RangeError = "The end date cannot be before the start date.";

        private static readonly LabRequestStatus[] StatusOrder =
        {
            LabRequestStatus.Requested, LabRequestStatus.Received, LabRequestStatus.InProgress,
            LabRequestStatus.ResultIssued, LabRequestStatus.ReturnedToDoctor, LabRequestStatus.Cancelled
        };

        private readonly ApplicationDbContext _context;
        private readonly IClinicClock _clock;

        public LabReportService(ApplicationDbContext context, IClinicClock clock)
        {
            _context = context;
            _clock = clock;
        }

        public async Task<ServiceResult<LabActivityReport>> BuildAsync(DateOnly from, DateOnly to)
        {
            if (to < from)
                return ServiceResult<LabActivityReport>.Fail(RangeError);
            if (to.DayNumber - from.DayNumber + 1 > MaxDays)
                return ServiceResult<LabActivityReport>.Fail($"Choose a period of {MaxDays} days at most.");

            var start = from.ToDateTime(TimeOnly.MinValue);
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

            var requests = await _context.LabRequests.AsNoTracking()
                .Include(r => r.Items).ThenInclude(i => i.LabTest)
                .Where(r => r.RequestedAt >= start && r.RequestedAt < end)
                .ToListAsync();

            var turnarounds = requests
                .Where(r => r.Status == LabRequestStatus.ResultIssued && r.ReceivedAt.HasValue && r.ResultIssuedAt.HasValue)
                .Select(r => r.ResultIssuedAt!.Value - r.ReceivedAt!.Value)
                .ToList();

            return ServiceResult<LabActivityReport>.Ok(new LabActivityReport
            {
                From = from,
                To = to,
                TotalRequests = requests.Count,
                ByStatus = StatusOrder.Select(s => (s, requests.Count(r => r.Status == s))).ToList(),
                ByTest = requests.SelectMany(r => r.Items)
                    .GroupBy(i => i.LabTest.Name)
                    .Select(g => (g.Key, g.Count()))
                    .OrderByDescending(x => x.Item2).ThenBy(x => x.Key)
                    .ToList(),
                MedianTurnaround = Median(turnarounds),
                TurnaroundSampleSize = turnarounds.Count
            });
        }

        /// <summary>The middle value; with an even count, the mean of the two middle values.</summary>
        public static TimeSpan? Median(IReadOnlyCollection<TimeSpan> values)
        {
            if (values.Count == 0)
                return null;

            var sorted = values.OrderBy(v => v).ToList();
            var middle = sorted.Count / 2;
            return sorted.Count % 2 == 1
                ? sorted[middle]
                : TimeSpan.FromTicks((sorted[middle - 1].Ticks + sorted[middle].Ticks) / 2);
        }

        public async Task<LabActivityReportPageViewModel> BuildPageAsync(DateOnly? from, DateOnly? to)
        {
            var today = DateOnly.FromDateTime(_clock.Today);
            var page = new LabActivityReportPageViewModel { From = from ?? to ?? today, To = to ?? from ?? today };

            var result = await BuildAsync(page.From, page.To);
            if (result.Success)
                page.Report = result.Data;
            else
                page.Error = result.Message;
            return page;
        }
    }
}
