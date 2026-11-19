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

namespace HCMS4.Modules.Pharmacy.Controllers
{
    // Part of the former PatientController; [ControllerName] keeps the /Patient/... URLs.
    [Authorize(Roles = "Patient")]
    [ControllerName("Patient")]
    public class PatientPrescriptionsController : BaseController
    {
        private readonly IAppointmentService _appointmentService;
        private readonly ILeaveService _leaveService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;
        private readonly IComplaintAttachmentService _attachmentService;
        private readonly IInvoicePaymentService _paymentService;

        public PatientPrescriptionsController(
            ApplicationDbContext context,
            ILogger<PatientPrescriptionsController> logger,
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
        public async Task<IActionResult> Prescriptions()
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var prescriptions = await _context.Prescriptions
                    .Where(p => p.PatientId == patientId.Value)
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.User)
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.Specialization)
                    .Include(p => p.PrescriptionItems)
                        .ThenInclude(pi => pi.Drug)
                    .Include(p => p.Appointment)
                    .OrderByDescending(p => p.PrescriptionDate)
                    .ToListAsync();

                var reissueRequests = await _context.PrescriptionReissueRequests
                    .Where(r => r.PatientId == patientId.Value)
                    .GroupBy(r => r.PrescriptionId)
                    .Select(g => g.OrderByDescending(r => r.RequestDate).First())
                    .ToDictionaryAsync(r => r.PrescriptionId);

                var prescriptionViewModels = prescriptions.Select(prescription =>
                {
                    var drugNames = prescription.PrescriptionItems
                        .Select(pi => pi.Drug?.Name ?? pi.DrugName)
                        .Distinct()
                        .ToList();

                    var dosages = prescription.PrescriptionItems
                        .Select(pi => pi.Dosage)
                        .Distinct()
                        .ToList();

                    var durations = prescription.PrescriptionItems
                        .Select(pi => pi.Duration)
                        .Distinct()
                        .ToList();

                    var expiresAt = prescription.PrescriptionDate.AddDays(BusinessRules.PrescriptionReissueValidityDays);
                    var latestRequest = reissueRequests.TryGetValue(prescription.Id, out var request)
                        ? request
                        : null;

                    return new PatientPrescriptionsViewModel
                    {
                        Id = prescription.Id,
                        PrescriptionDate = prescription.PrescriptionDate,
                        DoctorName = prescription.Doctor?.User?.FullName ?? "UNKNOWN",
                        Specialization = prescription.Doctor?.Specialization?.Name ?? "UNDEFINED",
                        DrugName = drugNames.Any() ? string.Join(", ", drugNames.Take(2)) +
                            (drugNames.Count > 2 ? $" and{drugNames.Count - 2} more" : "") : "NO DRUGS",
                        Dosage = dosages.Any() ? string.Join("، ", dosages) : "-",
                        Duration = durations.Any() ? string.Join("، ", durations) : "-",
                        Status = prescription.Status.ToString(),
                        TotalCost = prescription.TotalCost,
                        Notes = prescription.Notes,
                        DispensedDate = prescription.DispensedDate,
                        DispensedBy = prescription.DispensedBy,
                        PrescriptionExpiryDate = expiresAt,
                        CanRequestReissue = IsPrescriptionEligibleForReissue(prescription) &&
                                            latestRequest?.Status != ReissueRequestStatus.Pending,
                        HasPendingReissueRequest = latestRequest?.Status == ReissueRequestStatus.Pending,
                        LatestReissueStatus = latestRequest?.Status.ToString()
                    };
                }).ToList();

                var listViewModel = new PatientPrescriptionsListViewModel
                {
                    Prescriptions = prescriptionViewModels
                };

                return View(listViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prescriptions for patient");
                TempData["ErrorMessage"] = "Error loading prescriptions.";
                return RedirectToAction("Dashboard", "Patient");
            }
        }

        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> PrescriptionDetails(int id)
        {
            try
            {
                var patientId = await GetCurrentPatientIdAsync();
                if (!patientId.HasValue)
                    return RedirectToAction("Login", "Account");

                var prescription = await _context.Prescriptions
                    .Include(p => p.Patient)
                        .ThenInclude(pt => pt.User)
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.User)
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.Specialization)
                    .Include(p => p.PrescriptionItems)
                        .ThenInclude(pi => pi.Drug)
                    .Include(p => p.Appointment)
                    .FirstOrDefaultAsync(p => p.Id == id && p.PatientId == patientId.Value);

                if (prescription == null)
                {
                    TempData["ErrorMessage"] = "No prescription found.";
                    return RedirectToAction(nameof(Prescriptions));
                }

                var latestReissueRequest = await _context.PrescriptionReissueRequests
                    .Where(r => r.PatientId == patientId.Value && r.PrescriptionId == prescription.Id)
                    .OrderByDescending(r => r.RequestDate)
                    .FirstOrDefaultAsync();

                var pharmacistNotes = await _context.PharmacistPrescriptionNotes
                    .Include(n => n.Pharmacist)
                        .ThenInclude(p => p.User)
                    .Where(n => n.PrescriptionId == prescription.Id)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                var expiresAt = prescription.PrescriptionDate.AddDays(BusinessRules.PrescriptionReissueValidityDays);

                var detailedViewModel = new PatientPrescriptionDetailViewModel
                {
                    Id = prescription.Id,
                    PrescriptionDate = prescription.PrescriptionDate,
                    PatientName = prescription.Patient?.User?.FullName ?? "UNKNOWN",
                    DoctorName = prescription.Doctor?.User?.FullName ?? "UNKNOWN",
                    Specialization = prescription.Doctor?.Specialization?.Name ?? "UNDEFINED",
                    Status = prescription.Status.ToString(),
                    Notes = prescription.Notes,
                    TotalCost = prescription.TotalCost,
                    DispensedDate = prescription.DispensedDate,
                    DispensedBy = prescription.DispensedBy,
                    CreatedAt = prescription.CreatedAt,
                    AppointmentDate = prescription.Appointment?.AppointmentDateTime,
                    PrescriptionItems = prescription.PrescriptionItems.Select(pi => new PrescriptionItemDetailViewModel
                    {
                        DrugName = pi.Drug?.Name ?? pi.DrugName,
                        Dosage = pi.Dosage,
                        Duration = pi.Duration,
                        Frequency = pi.Frequency,
                        Quantity = pi.Quantity,
                        Instructions = pi.Instructions,
                        Price = pi.Drug?.Price ?? 0,
                        Subtotal = pi.Quantity * (pi.Drug?.Price ?? 0)
                    }).ToList(),
                    PharmacistNotes = pharmacistNotes.Select(note => new PrescriptionNoteDisplayViewModel
                    {
                        PharmacistName = note.Pharmacist?.User?.FullName ?? "Pharmacist",
                        NoteText = note.NoteText,
                        NotifyDoctor = note.NotifyDoctor,
                        CreatedAt = note.CreatedAt
                    }).ToList(),
                    PrescriptionExpiryDate = expiresAt,
                    CanRequestReissue = IsPrescriptionEligibleForReissue(prescription) &&
                                        latestReissueRequest?.Status != ReissueRequestStatus.Pending,
                    HasPendingReissueRequest = latestReissueRequest?.Status == ReissueRequestStatus.Pending,
                    LatestReissueStatus = latestReissueRequest?.Status.ToString()
                };

                return View(detailedViewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prescription {PrescriptionId}", id);
                TempData["ErrorMessage"] = "Error loading prescription.";
                return RedirectToAction(nameof(Prescriptions));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestPrescriptionReissue(int prescriptionId)
        {
            var patientId = await GetCurrentPatientIdAsync();
            if (!patientId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var prescription = await _context.Prescriptions
                .Include(p => p.Doctor)
                .FirstOrDefaultAsync(p => p.Id == prescriptionId && p.PatientId == patientId.Value);

            if (prescription == null)
            {
                TempData["ErrorMessage"] = "Prescription not found.";
                return RedirectToAction(nameof(Prescriptions));
            }

            if (!IsPrescriptionEligibleForReissue(prescription))
            {
                TempData["ErrorMessage"] = "This prescription has expired and cannot be re-issued.";
                return RedirectToAction(nameof(PrescriptionDetails), new { id = prescriptionId });
            }

            var hasPendingRequest = await _context.PrescriptionReissueRequests
                .AnyAsync(r => r.PrescriptionId == prescriptionId &&
                               r.PatientId == patientId.Value &&
                               r.Status == ReissueRequestStatus.Pending);

            if (hasPendingRequest)
            {
                TempData["ErrorMessage"] = "A re-issuance request is already pending for this prescription.";
                return RedirectToAction(nameof(PrescriptionDetails), new { id = prescriptionId });
            }

            var request = new PrescriptionReissueRequest
            {
                PatientId = patientId.Value,
                PrescriptionId = prescription.Id,
                DoctorId = prescription.DoctorId,
                RequestDate = ClinicTime.Now,
                Status = ReissueRequestStatus.Pending
            };

            _context.PrescriptionReissueRequests.Add(request);
            await _context.SaveChangesAsync();

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == prescription.DoctorId);
            if (doctor != null)
            {
                await _notificationService.CreateForUserAsync(
                    doctor.UserId,
                    "Prescription re-issuance request",
                    $"A patient requested re-issuance for prescription #{prescription.Id}.",
                    NotificationType.PrescriptionReissue,
                    "/Doctor/ReissueRequests",
                    "PrescriptionReissueRequest",
                    request.Id);
            }

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "PrescriptionReissueRequested",
                "PrescriptionReissueRequest",
                $"Patient requested re-issuance for prescription #{prescription.Id}.",
                request.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Re-issuance request has been sent to the doctor.";
            return RedirectToAction(nameof(PrescriptionDetails), new { id = prescriptionId });
        }

        private bool IsPrescriptionEligibleForReissue(Prescription prescription)
        {
            return prescription.Status == PrescriptionStatus.Completed &&
                   prescription.PrescriptionDate.AddDays(BusinessRules.PrescriptionReissueValidityDays) >= ClinicTime.Now;
        }
    }
}
