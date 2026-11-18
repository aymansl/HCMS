using HCMS4.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCMS4.Tests
{
    // Regression test for issue #25: the filtered report showed the generation time instead of the requested range.
    public class DailyReportRangeTests
    {
        [Fact]
        public async Task GetFilteredReportAsync_ReportsRequestedRange()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var service = new DailyReportService(context, NullLogger<DailyReportService>.Instance);
            var start = new DateTime(2026, 1, 5, 15, 30, 0);
            var end = new DateTime(2026, 1, 10);

            var report = await service.GetFilteredReportAsync(start, end, null, null);

            Assert.Equal(start.Date, report.ReportDate);
            Assert.Equal(start.Date, report.StartDate);
            Assert.Equal(end.Date, report.EndDate);
        }
    }
}
