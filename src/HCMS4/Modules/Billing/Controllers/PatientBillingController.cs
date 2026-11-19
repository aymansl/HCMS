using HCMS4.Controllers;
using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Billing.Services;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Feedback.Services;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Notifications.Services;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HCMS4.Modules.Billing.Controllers
{
    // Part of the former PatientController; [ControllerName] keeps the /Patient/... URLs.
    [Authorize(Roles = "Patient")]
    [ControllerName("Patient")]
    public class PatientBillingController : BaseController
    {
        private readonly IAppointmentService _appointmentService;
        private readonly ILeaveService _leaveService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;
        private readonly IComplaintAttachmentService _attachmentService;
        private readonly IInvoicePaymentService _paymentService;

        public PatientBillingController(
            ApplicationDbContext context,
            ILogger<PatientBillingController> logger,
            IAppointmentService appointmentService,
            ILeaveService leaveService,
            INotificationService notificationService,
            IActivityLogService activityLogService,
            IComplaintAttachmentService attachmentService,
            IInvoicePaymentService paymentService)
            : base(context, logger)
        {
            _paymentService = paymentService;
            _appointmentService = appointmentService;
            _leaveService = leaveService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
            _attachmentService = attachmentService;
        }

        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> InvoiceHistory()
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var invoices = await _context.Invoices
                    .Where(i => i.PatientId == patientId.Value)
                    .Include(i => i.Appointment)
                        .ThenInclude(a => a != null ? a.Doctor : null)
                            .ThenInclude(d => d != null ? d.User : null)
                    .Include(i => i.Prescription)
                        .ThenInclude(p => p != null ? p.Doctor : null)
                            .ThenInclude(d => d != null ? d.User : null)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                var viewModel = invoices.Select(i => new InvoiceViewModel
                {
                    Id = i.Id,
                    InvoiceDate = i.InvoiceDate,
                    ConsultationFee = i.ConsultationFee,
                    MedicationTotal = i.MedicationTotal,
                    TotalAmount = i.TotalAmount,
                    PaymentStatus = i.PaymentStatus.ToString(),
                    AmountPaid = i.AmountPaid,
                    PaymentDate = i.PaymentDate,
                    Notes = i.Notes,
                    CreatedAt = i.UpdatedAt,
                    DoctorName = i.Appointment?.Doctor?.User?.FullName ??
                                i.Prescription?.Doctor?.User?.FullName ?? "N/A",
                    AppointmentDate = i.Appointment?.AppointmentDateTime
                }).ToList();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice history for user");
                TempData["ErrorMessage"] = "An error occurred while loading your invoice history.";
                return RedirectToAction("Dashboard", "Patient");
            }
        }

        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> InvoiceDetails(int id)
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var invoice = await _context.Invoices
                    .Include(i => i.Patient)
                        .ThenInclude(p => p.User)
                    .Include(i => i.Appointment)
                        .ThenInclude(a => a != null ? a.Doctor : null)
                            .ThenInclude(d => d != null ? d.User : null)
                    .Include(i => i.Prescription)
                        .ThenInclude(p => p != null ? p.PrescriptionItems : null)
                            .ThenInclude(pi => pi != null ? pi.Drug : null)
                    .FirstOrDefaultAsync(i => i.Id == id && i.PatientId == patientId.Value);

                if (invoice == null)
                {
                    TempData["ErrorMessage"] = "Invoice not found or you don't have permission to view it.";
                    return RedirectToAction(nameof(InvoiceHistory));
                }

                var viewModel = new InvoiceViewModel
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

                ViewBag.Invoice = invoice;
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice details {InvoiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading invoice details.";
                return RedirectToAction(nameof(InvoiceHistory));
            }
        }

        [HttpPost]
        [Authorize(Roles = "Patient")]
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
    }
}
