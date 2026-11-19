using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Billing.Services;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Modules.Billing.Controllers
{
    // Part of the former PatientController; [ControllerName] keeps the /Patient/... URLs.
    [Authorize(Roles = "Patient")]
    [ControllerName("Patient")]
    public class PatientBillingController : HcmsControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly IInvoicePaymentService _paymentService;
        private readonly ILogger<PatientBillingController> _logger;

        public PatientBillingController(
            IInvoiceService invoiceService,
            IInvoicePaymentService paymentService,
            ILogger<PatientBillingController> logger)
        {
            _invoiceService = invoiceService;
            _paymentService = paymentService;
            _logger = logger;
        }

        public async Task<IActionResult> InvoiceHistory()
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var invoices = await _invoiceService.GetForPatientAsync(patientId.Value);
                return View(invoices.Select(ToViewModel).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice history for user");
                TempData["ErrorMessage"] = "An error occurred while loading your invoice history.";
                return RedirectToAction("Dashboard", "Patient");
            }
        }

        public async Task<IActionResult> InvoiceDetails(int id)
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var invoice = await _invoiceService.GetForPatientAsync(patientId.Value, id);
                if (invoice == null)
                {
                    TempData["ErrorMessage"] = "Invoice not found or you don't have permission to view it.";
                    return RedirectToAction(nameof(InvoiceHistory));
                }

                ViewBag.Invoice = invoice;
                return View(ToViewModel(invoice));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice details {InvoiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading invoice details.";
                return RedirectToAction(nameof(InvoiceHistory));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment(int invoiceId, decimal amount, PaymentMethod paymentMethod, string? notes)
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var result = await _paymentService.PayAsync(invoiceId, patientId.Value, amount, paymentMethod, notes);
                TempData[result.Success ? "SuccessMessage" : "ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(InvoiceHistory));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment for invoice {InvoiceId}", invoiceId);
                TempData["ErrorMessage"] = "An error occurred while processing your payment.";
                return RedirectToAction(nameof(InvoiceHistory));
            }
        }

        private static InvoiceViewModel ToViewModel(Invoice invoice) => new()
        {
            Id = invoice.Id,
            InvoiceDate = invoice.InvoiceDate,
            ConsultationFee = invoice.ConsultationFee,
            MedicationTotal = invoice.MedicationTotal,
            TotalAmount = invoice.TotalAmount,
            PaymentStatus = invoice.PaymentStatus.ToString(),
            AmountPaid = invoice.AmountPaid,
            PaymentDate = invoice.PaymentDate,
            Notes = invoice.Notes,
            CreatedAt = invoice.UpdatedAt,
            DoctorName = invoice.Appointment?.Doctor?.User?.FullName ??
                         invoice.Prescription?.Doctor?.User?.FullName ?? "N/A",
            AppointmentDate = invoice.Appointment?.AppointmentDateTime
        };
    }
}
