using HCMS4.Data;
using HCMS4.Models;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Services
{
    public class DailyReportService : IDailyReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DailyReportService> _logger;

        public DailyReportService(ApplicationDbContext context, ILogger<DailyReportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<DailyPharmacyReportViewModel> GetDailyReportAsync(DateTime date)
        {
            try
            {
                var startOfDay = date.Date;
                var endOfDay = startOfDay.AddDays(1);

                var pharmacyInvoices = await _context.Invoices
                    .AsNoTracking()
                    .Include(i => i.Patient)
                        .ThenInclude(p => p.User)
                    .Include(i => i.Pharmacist)
                        .ThenInclude(p => p.User)
                    .Where(i => i.InvoiceDate >= startOfDay &&
                                i.InvoiceDate < endOfDay &&
                                i.PrescriptionId != null)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                var invoiceItems = pharmacyInvoices.Select(i => new DailyInvoiceItem
                {
                    InvoiceId = i.Id,
                    PatientName = i.Patient?.User?.FullName ?? "Unknown",
                    PharmacistName = i.Pharmacist?.User?.FullName ?? "System",
                    TotalAmount = i.TotalAmount,
                    InvoiceTime = i.InvoiceDate,
                    PaymentStatus = i.PaymentStatus,
                    PrescriptionId = i.PrescriptionId?.ToString()
                }).ToList();

                return new DailyPharmacyReportViewModel
                {
                    ReportDate = date,
                    Invoices = invoiceItems,
                    TotalAmount = invoiceItems.Sum(i => i.TotalAmount),
                    TotalInvoices = invoiceItems.Count,
                    HasInvoices = invoiceItems.Any()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting daily report for {Date}", date);
                return new DailyPharmacyReportViewModel
                {
                    ReportDate = date,
                    HasInvoices = false,
                    ErrorMessage = "Failed to load report data."
                };
            }
        }

        public async Task<DailyPharmacyReportViewModel> GetFilteredReportAsync(
            DateTime? startDate, DateTime? endDate, PaymentStatus? status, string? searchTerm)
        {
            try
            {
                var query = _context.Invoices
                    .Include(i => i.Patient)
                        .ThenInclude(p => p.User)
                    .Include(i => i.Pharmacist)
                        .ThenInclude(p => p.User)
                    .Where(i => i.PrescriptionId != null);

                if (startDate.HasValue)
                {
                    var start = startDate.Value.Date;
                    query = query.Where(i => i.InvoiceDate >= start);
                }

                if (endDate.HasValue)
                {
                    var end = endDate.Value.Date.AddDays(1);
                    query = query.Where(i => i.InvoiceDate < end);
                }

                if (status.HasValue)
                {
                    query = query.Where(i => i.PaymentStatus == status.Value);
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    // Note: Contains() with null checks for safety
                    query = query.Where(i =>
                        (i.Patient != null && i.Patient.User != null && i.Patient.User.FullName.Contains(searchTerm)) ||
                        i.Id.ToString().Contains(searchTerm) ||
                        (i.Pharmacist != null && i.Pharmacist.User != null && i.Pharmacist.User.FullName.Contains(searchTerm)));
                }

                var invoices = await query
                    .AsNoTracking()
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                var invoiceItems = invoices.Select(i => new DailyInvoiceItem
                {
                    InvoiceId = i.Id,
                    PatientName = i.Patient?.User?.FullName ?? "Unknown",
                    PharmacistName = i.Pharmacist?.User?.FullName ?? "System",
                    TotalAmount = i.TotalAmount,
                    InvoiceTime = i.InvoiceDate,
                    PaymentStatus = i.PaymentStatus,
                    PrescriptionId = i.PrescriptionId?.ToString()
                }).ToList();

                return new DailyPharmacyReportViewModel
                {
                    ReportDate = DateTime.UtcNow,
                    Invoices = invoiceItems,
                    TotalAmount = invoiceItems.Sum(i => i.TotalAmount),
                    TotalInvoices = invoiceItems.Count,
                    HasInvoices = invoiceItems.Any()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filtered report");
                return new DailyPharmacyReportViewModel
                {
                    HasInvoices = false,
                    ErrorMessage = "Failed to load report data."
                };
            }
        }

        public async Task<bool> GenerateDailyReportAsync(DateTime date)
        {
            try
            {
                var startOfDay = date.Date;
                var endOfDay = startOfDay.AddDays(1);

                var existingReport = await _context.DailyReports
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ReportDate.Date == startOfDay);

                if (existingReport != null)
                {
                    _logger.LogInformation("Daily report for {Date} already exists, skipping generation", startOfDay);
                    return true;
                }

                var pharmacyInvoices = await _context.Invoices
                    .AsNoTracking()
                    .Include(i => i.Patient)
                    .Where(i => i.InvoiceDate >= startOfDay &&
                                i.InvoiceDate < endOfDay &&
                                i.PrescriptionId != null)
                    .ToListAsync();

                var totalAmount = pharmacyInvoices.Sum(i => i.TotalAmount);

                var dailyReport = new DailyReport
                {
                    ReportDate = startOfDay,
                    TotalInvoices = pharmacyInvoices.Count,
                    TotalAmount = totalAmount,
                    GeneratedAt = DateTime.UtcNow,
                    ReportData = System.Text.Json.JsonSerializer.Serialize(pharmacyInvoices.Select(i => new
                    {
                        i.Id,
                        i.PatientId,
                        i.TotalAmount,
                        i.InvoiceDate,
                        i.PaymentStatus,
                        i.PrescriptionId
                    }))
                };

                _context.DailyReports.Add(dailyReport);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Daily report generated for {Date}: {Count} invoices, total {Total:C2}",
                    startOfDay, pharmacyInvoices.Count, totalAmount);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating daily report for {Date}", date);
                return false;
            }
        }

        public async Task<List<DateTime>> GetAvailableReportDatesAsync()
        {
            try
            {
                var dates = await _context.Invoices
                    .AsNoTracking()
                    .Where(i => i.PrescriptionId != null)
                    .Select(i => i.InvoiceDate.Date)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .Take(30)
                    .ToListAsync();

                return dates;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available report dates");
                return new List<DateTime>();
            }
        }
    }
}
