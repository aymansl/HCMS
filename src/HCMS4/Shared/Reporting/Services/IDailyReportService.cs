using HCMS4.Modules.Billing.Models;
// Services/DailyReportService.cs
using HCMS4.ViewModels;
using Microsoft.Extensions.Logging;

namespace HCMS4.Shared.Reporting.Services
{
    public interface IDailyReportService
    {
        Task<DailyPharmacyReportViewModel> GetDailyReportAsync(DateTime date);
        Task<DailyPharmacyReportViewModel> GetFilteredReportAsync(DateTime? startDate, DateTime? endDate, PaymentStatus? status, string? searchTerm);
        Task<bool> GenerateDailyReportAsync(DateTime date);
        Task<List<DateTime>> GetAvailableReportDatesAsync();
    }
}