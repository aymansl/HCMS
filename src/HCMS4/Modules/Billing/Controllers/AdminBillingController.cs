using System.Globalization;
using HCMS4.Modules.Billing.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Reporting.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HCMS4.Shared.Identity.Authorization;

namespace HCMS4.Modules.Billing.Controllers
{
    // Part of the former AdminController; [ControllerName] keeps the /Admin/... URLs.
    [Authorize(Policy = Permissions.Billing.ManageInvoices)]
    [ControllerName("Admin")]
    public class AdminBillingController : HcmsControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly IDailyReportService _reportService;
        private readonly IClinicClock _clock;
        private readonly ILogger<AdminBillingController> _logger;

        public AdminBillingController(
            IInvoiceService invoiceService,
            IDailyReportService reportService,
            IClinicClock clock,
            ILogger<AdminBillingController> logger)
        {
            _invoiceService = invoiceService;
            _reportService = reportService;
            _clock = clock;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> ManageInvoices([FromQuery] InvoiceListFilter filter)
        {
            try
            {
                return View(await _invoiceService.GetListAsync(filter));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading the invoice list");
                TempData["ErrorMessage"] = "Error loading the invoice list. Please try again later.";
                return View(new AdminInvoiceListViewModel { Filter = filter });
            }
        }

        public async Task<IActionResult> GenerateInvoice(int patientId)
        {
            try
            {
                var viewModel = await _invoiceService.GetGenerateFormAsync(patientId);
                if (viewModel == null)
                {
                    _logger.LogWarning("Patient not found with ID: {PatientId}", patientId);
                    TempData["ErrorMessage"] = "Patient not found.";
                    return RedirectToAction("Patients");
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice generation page for patient {PatientId}", patientId);
                TempData["ErrorMessage"] = "An error occurred while loading invoice details.";
                return RedirectToAction("Patients");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateInvoice(GenerateInvoiceViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var result = await _invoiceService.CreateAsync(model);
                    if (result.Success)
                    {
                        _logger.LogInformation("Invoice {InvoiceId} created for patient {PatientId}", result.Data.Id, model.PatientId);
                        TempData["SuccessMessage"] = result.Message;
                        return RedirectToAction("Patients");
                    }

                    if (result.Message == InvoiceService.PatientNotFoundMessage)
                    {
                        TempData["ErrorMessage"] = result.Message;
                        return RedirectToAction("Patients");
                    }

                    foreach (var error in result.Errors.Count > 0 ? result.Errors : new List<string> { result.Message })
                        ModelState.AddModelError("", error);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating invoice for patient {PatientId}", model.PatientId);
                    ModelState.AddModelError("", "An error occurred while creating the invoice.");
                }

                await _invoiceService.FillGenerateFormOptionsAsync(model);
            }

            return View(model);
        }

        public async Task<IActionResult> ViewInvoice(int id)
        {
            try
            {
                var invoice = await _invoiceService.GetAsync(id);
                if (invoice == null)
                    return NotFound();

                return View(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error viewing invoice {InvoiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the invoice.";
                return RedirectToAction("Patients");
            }
        }

        public async Task<IActionResult> InvoiceDetails(int id)
        {
            try
            {
                var invoice = await _invoiceService.GetAsync(id);
                if (invoice == null)
                    return NotFound();

                return View(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice details {InvoiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading invoice details.";
                return RedirectToAction(nameof(ViewInvoice), new { id });
            }
        }

        [HttpGet]
        public async Task<IActionResult> UpdatePaymentStatus(int id)
        {
            try
            {
                var invoice = await _invoiceService.GetAsync(id);
                if (invoice == null)
                {
                    TempData["ErrorMessage"] = "Invoice not found.";
                    return RedirectToAction("ManageInvoices");
                }

                if (invoice.PaymentStatus == HCMS4.Modules.Billing.Models.PaymentStatus.Paid)
                {
                    TempData["WarningMessage"] = "This invoice is already marked as Paid and cannot be modified.";
                    return RedirectToAction("InvoiceDetails", new { id = invoice.Id });
                }

                return View(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice for payment update.");
                TempData["ErrorMessage"] = "An error occurred while loading the invoice.";
                return RedirectToAction("ManageInvoices");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("UpdatePaymentStatus")]
        public async Task<IActionResult> UpdatePaymentStatusPost(int id)
        {
            try
            {
                var result = await _invoiceService.UpdatePaymentStatusAsync(id, new PaymentUpdateRequest(
                    Request.Form["PaymentStatus"].ToString(),
                    Request.Form["AmountPaid"].ToString(),
                    Request.Form["PaymentDate"].ToString(),
                    Request.Form["Notes"].ToString()));

                switch (result.Outcome)
                {
                    case PaymentUpdateOutcome.NotFound:
                        TempData["ErrorMessage"] = result.Message;
                        return RedirectToAction("ManageInvoices");
                    case PaymentUpdateOutcome.AlreadyPaid:
                        TempData["WarningMessage"] = result.Message;
                        return RedirectToAction("InvoiceDetails", new { id });
                    case PaymentUpdateOutcome.Invalid:
                        TempData["ErrorMessage"] = result.Message;
                        return RedirectToAction("UpdatePaymentStatus", new { id });
                }

                _logger.LogInformation("Invoice {InvoiceId} payment status updated by {User}", id, User.Identity?.Name ?? "Unknown");
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction("InvoiceDetails", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating payment status for invoice {InvoiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while updating the payment status.";
                return RedirectToAction("ManageInvoices");
            }
        }

        public async Task<IActionResult> DailyPharmacyReport(DateTime? date = null)
        {
            try
            {
                var targetDate = date ?? _clock.Today;
                var report = await _reportService.GetDailyReportAsync(targetDate);
                var availableDates = await _reportService.GetAvailableReportDatesAsync();

                ViewBag.AvailableDates = availableDates;
                ViewBag.SelectedDate = targetDate;

                return View(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading daily pharmacy report");
                TempData["ErrorMessage"] = "An error occurred while loading the daily report.";
                return View(new DailyPharmacyReportViewModel { HasInvoices = false, ErrorMessage = "Failed to load report." });
            }
        }

        public async Task<IActionResult> FilteredPharmacyReport(DailyReportFilterViewModel filters)
        {
            try
            {
                var report = await _reportService.GetFilteredReportAsync(
                    filters.StartDate,
                    filters.EndDate,
                    filters.PaymentStatus,
                    filters.SearchTerm);

                ViewBag.Filters = filters;
                return View("DailyPharmacyReport", report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading filtered pharmacy report");
                TempData["ErrorMessage"] = "An error occurred while loading the filtered report.";
                return RedirectToAction(nameof(DailyPharmacyReport));
            }
        }

        public async Task<IActionResult> ExportPharmacyReport(DateTime? date)
        {
            try
            {
                var targetDate = date ?? _clock.Today;
                var report = await _reportService.GetDailyReportAsync(targetDate);

                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Invoice ID,Patient Name,Pharmacist,Total Amount,Time,Payment Status,Prescription ID");

                foreach (var invoice in report.Invoices)
                {
                    csv.AppendLine(CultureInfo.CurrentCulture, $"{invoice.InvoiceId},{invoice.PatientName},{invoice.PharmacistName},{invoice.TotalAmount:C2},{invoice.InvoiceTime:yyyy-MM-dd HH:mm},{invoice.PaymentStatus},{invoice.PrescriptionId}");
                }

                csv.AppendLine();
                csv.AppendLine(CultureInfo.CurrentCulture, $"Total Invoices,{report.TotalInvoices}");
                csv.AppendLine(CultureInfo.CurrentCulture, $"Total Amount,{report.TotalAmount:C2}");
                csv.AppendLine(CultureInfo.CurrentCulture, $"Report Date,{targetDate:yyyy-MM-dd}");

                var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
                var result = new FileContentResult(bytes, "text/csv")
                {
                    FileDownloadName = $"Pharmacy_Report_{targetDate:yyyy-MM-dd}.csv"
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting pharmacy report");
                TempData["ErrorMessage"] = "An error occurred while exporting the report.";
                return RedirectToAction(nameof(DailyPharmacyReport));
            }
        }
    }
}
