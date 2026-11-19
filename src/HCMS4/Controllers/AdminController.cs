using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Notifications.Services;
using HCMS4.Shared.Reporting.Services;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Identity.Models;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Common;
using HCMS4.Data;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static HCMS4.ViewModels.GenerateInvoiceViewModel;

namespace HCMS4.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : BaseController
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IDailyReportService _reportService;
        private readonly IPatientService _patientService;
        private readonly IAppointmentService _appointmentService;
        private readonly IPrescriptionService _prescriptionService;
        private readonly ApplicationDbContext _adminContext;
        private readonly INoShowRiskService _noShowRiskService;
        private readonly ILeaveService _leaveService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AdminController> logger,
            IDailyReportService reportService,
            IPatientService patientService,
            IAppointmentService appointmentService,
            IPrescriptionService prescriptionService,
            INoShowRiskService noShowRiskService,
            ILeaveService leaveService,
            INotificationService notificationService,
            IActivityLogService activityLogService)
            : base(context, logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _reportService = reportService;
            _patientService = patientService;
            _appointmentService = appointmentService;
            _prescriptionService = prescriptionService;
            _adminContext = context;
            _noShowRiskService = noShowRiskService;
            _leaveService = leaveService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
        }

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.DoctorCount = await _context.Doctors.CountAsync();
            ViewBag.PatientCount = await _context.Patients.CountAsync();
            ViewBag.AppointmentCount = await _context.Appointments.CountAsync();
            ViewBag.PendingComplaintsCount = await _context.Complaints.CountAsync(c => c.Status == ComplaintStatus.Submitted || c.Status == ComplaintStatus.UnderReview);
            ViewBag.ActiveSurveysCount = await _context.Surveys.CountAsync(s => s.Status == SurveyStatus.Active);
            var todayReport = await _reportService.GetDailyReportAsync(ClinicTime.Today);
            ViewBag.TodayReport = todayReport;

            // Admins receive leave requests and other alerts as notifications; show the latest ones.
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            ViewBag.RecentNotifications = await _notificationService.GetRecentForUserAsync(currentUserId, 8);

            return View();
        }



        public async Task<IActionResult> GenerateInvoice(int patientId)
        {
            try
            {
                _logger.LogInformation("Loading GenerateInvoice for patient {PatientId}", patientId);

                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == patientId);

                if (patient == null)
                {
                    _logger.LogWarning("Patient not found with ID: {PatientId}", patientId);
                    TempData["ErrorMessage"] = "Patient not found.";
                    return RedirectToAction("Patients");
                }

                var recentAppointments = await _context.Appointments
                    .Where(a => a.PatientId == patientId &&
                               a.Status == AppointmentStatus.Completed &&
                               a.AppointmentDateTime >= ClinicTime.Now.AddDays(-30))
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.User)
                    .OrderByDescending(a => a.AppointmentDateTime)
                    .ToListAsync();

                var recentPrescriptions = await _context.Prescriptions
                    .Where(p => p.PatientId == patientId &&
                               p.Status == PrescriptionStatus.Completed &&
                               p.PrescriptionDate >= ClinicTime.Now.AddDays(-30))
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.User)
                    .OrderByDescending(p => p.PrescriptionDate)
                    .ToListAsync();

                var existingInvoices = await _context.Invoices
                    .Where(i => i.PatientId == patientId)
                    .OrderByDescending(i => i.InvoiceDate)
                    .Take(5)
                    .ToListAsync();

                var consultationFee = recentAppointments
                    .Sum(a => a.ConsultationFee);

                var medicationTotal = recentPrescriptions
                    .Sum(p => p.MedicationTotal ?? 0);

                var totalAmount = consultationFee + medicationTotal;

                var viewModel = new GenerateInvoiceViewModel
                {
                    PatientId = patient.Id,
                    PatientName = patient.User?.FullName ?? "Unknown",
                    ConsultationFee = consultationFee,
                    MedicationTotal = medicationTotal,
                    TotalAmount = totalAmount,
                    RecentAppointments = recentAppointments.Select(a => new GenerateInvoiceViewModel.AppointmentSelectDto
                    {
                        Id = a.Id,
                        AppointmentDate = a.AppointmentDateTime,
                        DoctorName = a.Doctor?.User?.FullName ?? "Unknown",
                        ConsultationFee = a.ConsultationFee,
                        DisplayText = $"{a.AppointmentDateTime:yyyy-MM-dd} - {a.Doctor?.User?.FullName ?? "Unknown"} - {a.ConsultationFee:F2}"
                    }).ToList(),
                    RecentPrescriptions = recentPrescriptions.Select(p => new PrescriptionSelectDto
                    {
                        Id = p.Id,
                        PrescriptionDate = p.PrescriptionDate,
                        DoctorName = p.Doctor?.User?.FullName ?? "Unknown",
                        MedicationTotal = p.MedicationTotal ?? 0,
                        DisplayText = $"{p.PrescriptionDate:yyyy-MM-dd} - {p.Doctor?.User?.FullName ?? "Unknown"} - ${(p.MedicationTotal ?? 0):F2}"
                    }).ToList(),
                    ExistingInvoices = existingInvoices
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice generation page for patient {PatientId}", patientId);
                TempData["ErrorMessage"] = $"An error occurred while loading invoice details: {ex.Message}";
                return RedirectToAction("Patients");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateInvoice(GenerateInvoiceViewModel model)
        {
            var calculatedTotal = model.ConsultationFee + model.MedicationTotal;
            if (Math.Abs(model.TotalAmount - calculatedTotal) > 0.01m)
            {
                ModelState.AddModelError("TotalAmount",
                    $"Total amount (${model.TotalAmount:F2}) must equal Consultation Fee (${model.ConsultationFee:F2}) + Medication Total (${model.MedicationTotal:F2}) = ${calculatedTotal:F2}");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var patient = await _context.Patients
                        .Include(p => p.User)
                        .FirstOrDefaultAsync(p => p.Id == model.PatientId);

                    if (patient == null)
                    {
                        TempData["ErrorMessage"] = "Patient not found.";
                        return RedirectToAction("Patients");
                    }

                    if (model.AppointmentId.HasValue)
                    {
                        var appointment = await _context.Appointments
                            .FirstOrDefaultAsync(a => a.Id == model.AppointmentId.Value && a.PatientId == model.PatientId);
                        if (appointment == null)
                            ModelState.AddModelError("AppointmentId", "Appointment not found or doesn't belong to this patient.");
                    }

                    if (model.PrescriptionId.HasValue)
                    {
                        var prescription = await _context.Prescriptions
                            .FirstOrDefaultAsync(p => p.Id == model.PrescriptionId.Value && p.PatientId == model.PatientId);
                        if (prescription == null)
                            ModelState.AddModelError("PrescriptionId", "Prescription not found or doesn't belong to this patient.");
                    }

                    if (!ModelState.IsValid)
                    {
                        await ReloadInvoiceDropdowns(model);
                        return View(model);
                    }

                    var invoice = new Invoice
                    {
                        PatientId = model.PatientId,
                        AppointmentId = model.AppointmentId,
                        PrescriptionId = model.PrescriptionId,
                        InvoiceDate = ClinicTime.Now,
                        ConsultationFee = model.ConsultationFee,
                        MedicationTotal = model.MedicationTotal,
                        TotalAmount = model.TotalAmount,
                        PaymentStatus = PaymentStatus.Pending,
                        PaymentDate = null,
                        AmountPaid = null,
                        Notes = model.Notes,
                        UpdatedAt = ClinicTime.Now
                    };

                    _context.Invoices.Add(invoice);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Invoice {InvoiceId} created for patient {PatientId}", invoice.Id, model.PatientId);
                    TempData["SuccessMessage"] = $"Invoice #{invoice.Id} created successfully for {patient.User?.FullName}!";
                    return RedirectToAction("Patients");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating invoice for patient {PatientId}", model.PatientId);
                    ModelState.AddModelError("", "An error occurred while creating the invoice.");
                    await ReloadInvoiceDropdowns(model);
                }
            }

            return View(model);
        }

        public async Task<IActionResult> ViewInvoice(int id)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Patient)
                        .ThenInclude(p => p.User)
                    .Include(i => i.Appointment)
                        .ThenInclude(a => a.Doctor)
                            .ThenInclude(d => d.User)
                    .Include(i => i.Prescription)
                        .ThenInclude(p => p.Doctor)
                            .ThenInclude(d => d.User)
                    .FirstOrDefaultAsync(i => i.Id == id);

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
                var invoice = await _context.Invoices
                    .Include(i => i.Patient)
                        .ThenInclude(p => p.User)
                    .Include(i => i.Appointment)
                        .ThenInclude(a => a.Doctor)
                            .ThenInclude(d => d.User)
                    .Include(i => i.Prescription)
                        .ThenInclude(p => p.Doctor)
                            .ThenInclude(d => d.User)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (invoice == null)
                    return NotFound();

                if (invoice.PrescriptionId.HasValue)
                {
                    invoice.Prescription = await _context.Prescriptions
                        .Include(p => p.PrescriptionItems)
                            .ThenInclude(pi => pi.Drug)
                        .FirstOrDefaultAsync(p => p.Id == invoice.PrescriptionId.Value);
                }

                return View(invoice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading invoice details {InvoiceId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading invoice details.";
                return RedirectToAction(nameof(ViewInvoice), new { id });
            }
        }

        private async Task ReloadInvoiceDropdowns(GenerateInvoiceViewModel model)
        {
            var patient = await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == model.PatientId);

            if (patient != null)
                model.PatientName = patient.User != null ? patient.User.FullName : "Unknown";

            var appointments = await _context.Appointments
                .Where(a => a.PatientId == model.PatientId && a.Status == AppointmentStatus.Completed)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .OrderByDescending(a => a.AppointmentDateTime)
                .Take(10)
                .ToListAsync();

            model.RecentAppointments = appointments.Select(a => new GenerateInvoiceViewModel.AppointmentSelectDto
            {
                Id = a.Id,
                AppointmentDate = a.AppointmentDateTime,
                DoctorName = a.Doctor?.User != null ? a.Doctor.User.FullName : "Unknown",
                ConsultationFee = a.ConsultationFee,
                DisplayText = $"{a.AppointmentDateTime:yyyy-MM-dd} - {(a.Doctor?.User != null ? a.Doctor.User.FullName : "Unknown")} - {a.ConsultationFee:F2}"
            }).ToList();

            var prescriptions = await _context.Prescriptions
                .Where(p => p.PatientId == model.PatientId && p.Status == PrescriptionStatus.Completed)
                .Include(p => p.Doctor)
                    .ThenInclude(d => d.User)
                .OrderByDescending(p => p.PrescriptionDate)
                .Take(10)
                .ToListAsync();

            model.RecentPrescriptions = prescriptions.Select(p => new PrescriptionSelectDto
            {
                Id = p.Id,
                PrescriptionDate = p.PrescriptionDate,
                DoctorName = p.Doctor?.User != null ? p.Doctor.User.FullName : "Unknown",
                MedicationTotal = p.MedicationTotal ?? 0,
                DisplayText = $"{p.PrescriptionDate:yyyy-MM-dd} - {(p.Doctor?.User != null ? p.Doctor.User.FullName : "Unknown")} - ${(p.MedicationTotal ?? 0):F2}"
            }).ToList();
        }

        [HttpGet]
        public async Task<IActionResult> UpdatePaymentStatus(int id)
        {
            try
            {
                var invoice = await _context.Invoices
                    .Include(i => i.Patient)
                        .ThenInclude(p => p.User)
                    .Include(i => i.Appointment)
                        .ThenInclude(a => a.Doctor)
                            .ThenInclude(d => d.User)
                    .Include(i => i.Prescription)
                        .ThenInclude(p => p.Doctor)
                            .ThenInclude(d => d.User)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (invoice == null)
                {
                    TempData["ErrorMessage"] = "Invoice not found.";
                    return RedirectToAction("ManageInvoices");
                }

                if (invoice.PaymentStatus == PaymentStatus.Paid)
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
                var paymentStatusStr = Request.Form["PaymentStatus"].ToString();
                var amountPaidStr = Request.Form["AmountPaid"].ToString();
                var paymentDateStr = Request.Form["PaymentDate"].ToString();
                var notes = Request.Form["Notes"].ToString();

                var invoice = await _context.Invoices.FindAsync(id);
                if (invoice == null)
                {
                    TempData["ErrorMessage"] = "Invoice not found.";
                    return RedirectToAction("ManageInvoices");
                }

                if (invoice.PaymentStatus == PaymentStatus.Paid)
                {
                    TempData["WarningMessage"] = "This invoice is already marked as Paid and cannot be modified.";
                    return RedirectToAction("InvoiceDetails", new { id = invoice.Id });
                }

                if (string.IsNullOrEmpty(paymentStatusStr))
                {
                    TempData["ErrorMessage"] = "Payment status is required.";
                    return RedirectToAction("UpdatePaymentStatus", new { id });
                }

                if (!Enum.TryParse<PaymentStatus>(paymentStatusStr, out var newStatus))
                {
                    TempData["ErrorMessage"] = "Invalid payment status.";
                    return RedirectToAction("UpdatePaymentStatus", new { id });
                }

                if (newStatus == PaymentStatus.Paid)
                {
                    if (string.IsNullOrEmpty(amountPaidStr))
                    {
                        TempData["ErrorMessage"] = "Amount paid is required for paid status.";
                        return RedirectToAction("UpdatePaymentStatus", new { id });
                    }

                    if (!decimal.TryParse(amountPaidStr, out var amountPaid))
                    {
                        TempData["ErrorMessage"] = "Invalid amount format.";
                        return RedirectToAction("UpdatePaymentStatus", new { id });
                    }

                    if (amountPaid != invoice.TotalAmount)
                    {
                        TempData["ErrorMessage"] = $"Amount paid (${amountPaid:F2}) must equal the total amount (${invoice.TotalAmount:F2}).";
                        return RedirectToAction("UpdatePaymentStatus", new { id });
                    }

                    DateTime paymentDate;
                    if (string.IsNullOrEmpty(paymentDateStr))
                        paymentDate = ClinicTime.Now;
                    else if (!DateTime.TryParse(paymentDateStr, out paymentDate))
                    {
                        TempData["ErrorMessage"] = "Invalid payment date format.";
                        return RedirectToAction("UpdatePaymentStatus", new { id });
                    }

                    invoice.PaymentStatus = PaymentStatus.Paid;
                    invoice.AmountPaid = amountPaid;
                    invoice.PaymentDate = paymentDate;
                }
                else if (newStatus == PaymentStatus.Pending)
                {
                    invoice.PaymentStatus = PaymentStatus.Pending;
                    invoice.AmountPaid = null;
                    invoice.PaymentDate = null;
                }

                if (!string.IsNullOrWhiteSpace(notes))
                {
                    var prefix = $"[{ClinicTime.Now:yyyy-MM-dd HH:mm}] Payment Update: ";
                    invoice.Notes = string.IsNullOrWhiteSpace(invoice.Notes)
                        ? $"{prefix}{notes}"
                        : $"{invoice.Notes}\n{prefix}{notes}";
                }

                invoice.UpdatedAt = ClinicTime.Now;
                _context.Update(invoice);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Invoice {InvoiceId} payment status updated to {Status} by {User}",
                    invoice.Id, invoice.PaymentStatus, User.Identity?.Name ?? "Unknown");

                TempData["SuccessMessage"] = $"Payment status updated successfully to '{invoice.PaymentStatus}'.";
                return RedirectToAction("InvoiceDetails", new { id = invoice.Id });
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
                var targetDate = date ?? ClinicTime.Today;
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
                var targetDate = date ?? ClinicTime.Today;
                var report = await _reportService.GetDailyReportAsync(targetDate);

                var csv = new System.Text.StringBuilder();
                csv.AppendLine("Invoice ID,Patient Name,Pharmacist,Total Amount,Time,Payment Status,Prescription ID");

                foreach (var invoice in report.Invoices)
                {
                    csv.AppendLine($"{invoice.InvoiceId},{invoice.PatientName},{invoice.PharmacistName},{invoice.TotalAmount:C2},{invoice.InvoiceTime:yyyy-MM-dd HH:mm},{invoice.PaymentStatus},{invoice.PrescriptionId}");
                }

                csv.AppendLine();
                csv.AppendLine($"Total Invoices,{report.TotalInvoices}");
                csv.AppendLine($"Total Amount,{report.TotalAmount:C2}");
                csv.AppendLine($"Report Date,{targetDate:yyyy-MM-dd}");

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

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Complaints()
        {
            var complaints = await _context.Complaints
                .Include(c => c.Patient)
                    .ThenInclude(p => p.User)
                .OrderByDescending(c => c.SubmissionDate)
                .ToListAsync();

            return View(complaints);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ComplaintDetails(int id)
        {
            var complaint = await _context.Complaints
                .Include(c => c.Patient)
                    .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
            {
                TempData["ErrorMessage"] = "Complaint not found.";
                return RedirectToAction(nameof(Complaints));
            }

            if (complaint.Status == ComplaintStatus.Submitted)
            {
                complaint.Status = ComplaintStatus.UnderReview;
                await _context.SaveChangesAsync();
            }

            return View(complaint);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateComplaintStatus(int id, ComplaintStatus status, string? resolutionNotes)
        {
            var complaint = await _context.Complaints
                .Include(c => c.Patient)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
            {
                TempData["ErrorMessage"] = "Complaint not found.";
                return RedirectToAction(nameof(Complaints));
            }

            complaint.Status = status;
            complaint.ResolutionNotes = resolutionNotes;
            complaint.ResolvedAt = status == ComplaintStatus.Resolved || status == ComplaintStatus.Closed
                ? ClinicTime.Now
                : null;

            await _context.SaveChangesAsync();

            await _notificationService.CreateForUserAsync(
                complaint.Patient.UserId,
                "Complaint status updated",
                $"Your complaint {complaint.TrackingNumber} status is now {status}.",
                NotificationType.Complaint,
                "/Patient/Complaints",
                nameof(Complaint),
                complaint.Id);

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "ComplaintStatusUpdated",
                nameof(Complaint),
                $"Complaint {complaint.TrackingNumber} updated to {status}.",
                complaint.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Complaint updated successfully.";
            return RedirectToAction(nameof(ComplaintDetails), new { id });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Surveys()
        {
            var surveys = await _context.Surveys
                .Include(s => s.Questions)
                .Include(s => s.Assignments)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return View(surveys);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> CreateSurvey()
        {
            var viewModel = new SurveyEditorViewModel
            {
                AvailablePatients = await _context.Patients
                    .Include(p => p.User)
                    .Where(p => p.Status == PatientStatus.Active)
                    .Select(p => new PatientSelectDto
                    {
                        Id = p.Id,
                        FullName = p.User.FullName,
                        Email = p.User.Email ?? "N/A"
                    })
                    .ToListAsync()
            };

            return View(viewModel);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSurvey(SurveyEditorViewModel model)
        {
            if (model.EndDate.HasValue && model.EndDate.Value < model.StartDate)
            {
                ModelState.AddModelError(nameof(model.EndDate), "End date must be after start date.");
            }

            var validQuestions = model.Questions
                .Where(q => !string.IsNullOrWhiteSpace(q.QuestionText))
                .ToList();

            if (!validQuestions.Any())
            {
                ModelState.AddModelError(string.Empty, "You must add at least one question.");
            }

            if (model.TargetAudience == SurveyTargetAudience.SpecificPatients && !model.SpecificPatientIds.Any())
            {
                ModelState.AddModelError(string.Empty, "Please select at least one patient for a specific-patient survey.");
            }

            if (!ModelState.IsValid)
            {
                model.AvailablePatients = await _context.Patients
                    .Include(p => p.User)
                    .Where(p => p.Status == PatientStatus.Active)
                    .Select(p => new PatientSelectDto
                    {
                        Id = p.Id,
                        FullName = p.User.FullName,
                        Email = p.User.Email ?? "N/A"
                    })
                    .ToListAsync();
                return View(model);
            }

            var currentUser = await GetCurrentUserAsync();
            var survey = new Survey
            {
                Title = model.Title.Trim(),
                Description = model.Description?.Trim(),
                StartDate = model.StartDate.Date,
                EndDate = model.EndDate?.Date,
                TargetAudience = model.TargetAudience,
                TargetCriteria = model.TargetCriteria?.Trim(),
                Status = model.SendImmediately ? SurveyStatus.Active : SurveyStatus.Draft,
                CreatedByUserId = currentUser?.Id ?? string.Empty,
                CreatedAt = ClinicTime.Now,
                UpdatedAt = ClinicTime.Now,
                SentAt = model.SendImmediately ? ClinicTime.Now : null
            };

            _context.Surveys.Add(survey);
            await _context.SaveChangesAsync();

            var questionOrder = 0;
            foreach (var question in validQuestions)
            {
                var surveyQuestion = new SurveyQuestion
                {
                    SurveyId = survey.Id,
                    QuestionText = question.QuestionText.Trim(),
                    QuestionType = question.QuestionType,
                    IsRequired = question.IsRequired,
                    DisplayOrder = questionOrder++
                };

                _context.SurveyQuestions.Add(surveyQuestion);
                await _context.SaveChangesAsync();

                if (question.QuestionType == SurveyQuestionType.MultipleChoice &&
                    !string.IsNullOrWhiteSpace(question.OptionsText))
                {
                    var options = question.OptionsText
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(o => o.Trim())
                        .Where(o => !string.IsNullOrWhiteSpace(o))
                        .Distinct()
                        .ToList();

                    for (var i = 0; i < options.Count; i++)
                    {
                        _context.SurveyQuestionOptions.Add(new SurveyQuestionOption
                        {
                            SurveyQuestionId = surveyQuestion.Id,
                            OptionText = options[i],
                            DisplayOrder = i
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            if (model.SendImmediately)
            {
                var targetPatients = await GetSurveyTargetPatientsAsync(model);
                foreach (var patient in targetPatients)
                {
                    _context.SurveyAssignments.Add(new SurveyAssignment
                    {
                        SurveyId = survey.Id,
                        PatientId = patient.Id,
                        Status = SurveyAssignmentStatus.Pending,
                        AssignedAt = ClinicTime.Now
                    });

                    await _notificationService.CreateForUserAsync(
                        patient.UserId,
                        "New survey available",
                        $"A new survey '{survey.Title}' is available in your account.",
                        NotificationType.Survey,
                        "/Patient/Surveys",
                        nameof(Survey),
                        survey.Id);
                }

                await _context.SaveChangesAsync();
            }

            await _activityLogService.LogAsync(
                model.SendImmediately ? "SurveyCreatedAndSent" : "SurveyDraftCreated",
                nameof(Survey),
                $"Survey '{survey.Title}' was {(model.SendImmediately ? "created and sent" : "saved as draft")}.",
                survey.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Survey created successfully.";
            return RedirectToAction(nameof(Surveys));
        }

        private async Task<List<Patient>> GetSurveyTargetPatientsAsync(SurveyEditorViewModel model)
        {
            IQueryable<Patient> query = _context.Patients
                .Include(p => p.User)
                .Where(p => p.Status == PatientStatus.Active);

            if (model.TargetAudience == SurveyTargetAudience.SpecificPatients)
            {
                query = query.Where(p => model.SpecificPatientIds.Contains(p.Id));
            }
            else if (model.TargetAudience == SurveyTargetAudience.SpecificCategory &&
                     !string.IsNullOrWhiteSpace(model.TargetCriteria))
            {
                var criteria = model.TargetCriteria.Trim().ToLower();
                query = query.Where(p =>
                    (p.ChronicConditions != null && p.ChronicConditions.ToLower().Contains(criteria)) ||
                    (p.Address != null && p.Address.ToLower().Contains(criteria)) ||
                    (p.User.Email != null && p.User.Email.ToLower().Contains(criteria)) ||
                    (p.User.FirstName + " " + p.User.LastName).ToLower().Contains(criteria));
            }

            return await query.ToListAsync();
        }
    }
}
