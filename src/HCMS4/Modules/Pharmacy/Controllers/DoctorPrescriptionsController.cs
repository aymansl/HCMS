using HCMS4.Controllers;
using HCMS4.Data;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Pharmacy.Services;
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

namespace HCMS4.Modules.Pharmacy.Controllers
{
    // Part of the former DoctorController; [ControllerName] keeps the /Doctor/... URLs.
    [Authorize(Roles = "Doctor")]
    [ControllerName("Doctor")]
    public class DoctorPrescriptionsController : BaseController
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IPrescriptionService _prescriptionService;
        private readonly ILeaveService _leaveService;
        private readonly INoShowRiskService _noShowRiskService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;

        public DoctorPrescriptionsController(
            ApplicationDbContext context,
            ILogger<DoctorPrescriptionsController> logger,
            IAppointmentService appointmentService,
            IPrescriptionService prescriptionService,
            ILeaveService leaveService,
            INoShowRiskService noShowRiskService,
            INotificationService notificationService,
            IActivityLogService activityLogService)
            : base(context, logger)
        {
            _appointmentService = appointmentService;
            _prescriptionService = prescriptionService;
            _leaveService = leaveService;
            _noShowRiskService = noShowRiskService;
            _notificationService = notificationService;
            _activityLogService = activityLogService;
        }

        [HttpGet]
        public async Task<IActionResult> ReissueRequests()
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var requests = await _context.PrescriptionReissueRequests
                .Include(r => r.Patient)
                    .ThenInclude(p => p.User)
                .Include(r => r.Prescription)
                .Where(r => r.DoctorId == doctorId.Value)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewReissueRequest(int id, bool approve, string? doctorResponse)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var request = await _context.PrescriptionReissueRequests
                .Include(r => r.Patient)
                .FirstOrDefaultAsync(r => r.Id == id && r.DoctorId == doctorId.Value);

            if (request == null)
            {
                TempData["ErrorMessage"] = "Request not found.";
                return RedirectToAction(nameof(ReissueRequests));
            }

            request.Status = approve ? ReissueRequestStatus.Approved : ReissueRequestStatus.Rejected;
            request.DoctorResponse = doctorResponse;
            request.ReviewedAt = ClinicTime.Now;
            await _context.SaveChangesAsync();

            await _notificationService.CreateForUserAsync(
                request.Patient.UserId,
                "Prescription re-issuance updated",
                $"Your re-issuance request for prescription #{request.PrescriptionId} was {(approve ? "approved" : "rejected")}.",
                NotificationType.PrescriptionReissue,
                "/Patient/Prescriptions",
                "PrescriptionReissueRequest",
                request.Id);

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                approve ? "PrescriptionReissueApproved" : "PrescriptionReissueRejected",
                "PrescriptionReissueRequest",
                $"Doctor {(approve ? "approved" : "rejected")} re-issuance request #{request.Id}.",
                request.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = $"Request {(approve ? "approved" : "rejected")} successfully.";
            return RedirectToAction(nameof(ReissueRequests));
        }

        [HttpGet]
        public async Task<IActionResult> DoctorReviewRequests()
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var requests = await _context.DoctorReviewRequests
                .Include(r => r.Pharmacist)
                    .ThenInclude(p => p.User)
                .Include(r => r.Prescription)
                    .ThenInclude(p => p.Patient)
                        .ThenInclude(p => p.User)
                .Where(r => r.DoctorId == doctorId.Value)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RespondToReviewRequest(int id, string doctorResponse)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(doctorResponse))
            {
                TempData["ErrorMessage"] = "Please provide a response.";
                return RedirectToAction(nameof(DoctorReviewRequests));
            }

            var request = await _context.DoctorReviewRequests
                .Include(r => r.Pharmacist)
                .FirstOrDefaultAsync(r => r.Id == id && r.DoctorId == doctorId.Value);

            if (request == null)
            {
                TempData["ErrorMessage"] = "Review request not found.";
                return RedirectToAction(nameof(DoctorReviewRequests));
            }

            request.DoctorResponse = doctorResponse.Trim();
            request.Status = ReviewRequestStatus.Responded;
            request.ReviewedAt = ClinicTime.Now;
            await _context.SaveChangesAsync();

            await _notificationService.CreateForUserAsync(
                request.Pharmacist.UserId,
                "Doctor review response",
                $"Doctor responded to review request for prescription #{request.PrescriptionId}.",
                NotificationType.DoctorReview,
                "/Pharmacist/Prescriptions",
                "DoctorReviewRequest",
                request.Id);

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "DoctorReviewResponded",
                "DoctorReviewRequest",
                $"Doctor responded to doctor review request #{request.Id}.",
                request.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Response sent successfully.";
            return RedirectToAction(nameof(DoctorReviewRequests));
        }

        [HttpGet]
        public async Task<IActionResult> CreatePrescription(int patientId, int? appointmentId = null)
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == patientId);

                if (patient == null)
                {
                    TempData["ErrorMessage"] = "Patient not found.";
                    return RedirectToAction("ViewAppointments");
                }

                var drugs = await _context.Drugs
                    .Where(d => d.Quantity > 0 && d.ExpiryDate > ClinicTime.Now)
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                var viewModel = new CreatePrescriptionViewModel
                {
                    PatientId = patientId,
                    AppointmentId = appointmentId,
                    AvailableDrugs = drugs.Select(d => new DrugSelectionViewModel
                    {
                        Id = d.Id,
                        Name = d.Name,
                        Supplier = d.Supplier,
                        Price = d.Price,
                        Quantity = d.Quantity,
                        ExpiryDate = d.ExpiryDate,
                        ExpiryStatus = d.ExpiryStatus,
                        Description = d.Description
                    }).ToList()
                };

                ViewBag.PatientName = patient.User != null ? patient.User.FullName : "Unknown Patient";
                ViewBag.PatientInfo = $"ID: {patient.Id} | DOB: {patient.DateOfBirth?.ToString("yyyy-MM-dd") ?? "N/A"}";

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create prescription form");
                TempData["ErrorMessage"] = "Error loading form. Please try again.";
                return RedirectToAction("PatientRecord", new { patientId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePrescription(CreatePrescriptionViewModel viewModel)
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                if (ModelState.IsValid)
                {
                    if (string.IsNullOrWhiteSpace(viewModel.DoctorSignature))
                    {
                        ModelState.AddModelError("DoctorSignature", "Electronic signature is required.");
                        return View(viewModel);
                    }

                    var validationErrors = new List<string>();
                    foreach (var item in viewModel.Items)
                    {
                        var drug = await _context.Drugs.FindAsync(item.SelectedDrugId);
                        if (drug == null)
                        {
                            validationErrors.Add($"Drug '{item.DrugName}' not found in database.");
                            continue;
                        }

                        if (drug.Quantity < item.Quantity)
                        {
                            validationErrors.Add($"Insufficient stock for '{item.DrugName}'. Available: {drug.Quantity}, Requested: {item.Quantity}");
                        }

                        if (drug.ExpiryDate <= ClinicTime.Now)
                        {
                            validationErrors.Add($"Drug '{item.DrugName}' has expired on {drug.ExpiryDate:yyyy-MM-dd}");
                        }
                    }

                    if (validationErrors.Any())
                    {
                        foreach (var error in validationErrors)
                        {
                            ModelState.AddModelError(string.Empty, error);
                        }

                        var drugs = await _context.Drugs
                            .Where(d => d.Quantity > 0 && d.ExpiryDate > ClinicTime.Now)
                            .OrderBy(d => d.Name)
                            .ToListAsync();

                        viewModel.AvailableDrugs = drugs.Select(d => new DrugSelectionViewModel
                        {
                            Id = d.Id,
                            Name = d.Name,
                            Supplier = d.Supplier,
                            Price = d.Price,
                            Quantity = d.Quantity,
                            ExpiryDate = d.ExpiryDate,
                            ExpiryStatus = d.ExpiryStatus,
                            Description = d.Description
                        }).ToList();

                        var patient = await _context.Patients
                            .Include(p => p.User)
                            .FirstOrDefaultAsync(p => p.Id == viewModel.PatientId);

                        ViewBag.PatientName = patient?.User != null ? patient.User.FullName : "Unknown Patient";
                        return View(viewModel);
                    }

                    var prescription = new Prescription
                    {
                        PatientId = viewModel.PatientId,
                        DoctorId = doctorId.Value,
                        AppointmentId = viewModel.AppointmentId,
                        PrescriptionDate = ClinicTime.Now,
                        Notes = viewModel.Notes,
                        Status = PrescriptionStatus.Pending,
                        CreatedAt = ClinicTime.Now,
                        UpdatedAt = ClinicTime.Now
                    };

                    var items = viewModel.Items.Select(item =>
                    {
                        var drug = _context.Drugs.Find(item.SelectedDrugId);
                        return new PrescriptionItem
                        {
                            DrugId = item.SelectedDrugId,
                            DrugName = drug != null ? drug.Name : item.DrugName,
                            Dosage = item.Dosage,
                            Duration = item.Duration,
                            Frequency = item.Frequency,
                            Quantity = item.Quantity,
                            Instructions = item.Instructions ?? string.Empty
                        };
                    }).ToList();

                    var result = await _prescriptionService.CreateAsync(prescription, items);
                    if (result.Success)
                    {
                        TempData["SuccessMessage"] = result.Message;
                        return RedirectToAction("PatientRecord", new { patientId = viewModel.PatientId });
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error);
                    }
                }

                var drugsForReload = await _context.Drugs
                    .Where(d => d.Quantity > 0 && d.ExpiryDate > ClinicTime.Now)
                    .OrderBy(d => d.Name)
                    .ToListAsync();

                viewModel.AvailableDrugs = drugsForReload.Select(d => new DrugSelectionViewModel
                {
                    Id = d.Id,
                    Name = d.Name,
                    Supplier = d.Supplier,
                    Price = d.Price,
                    Quantity = d.Quantity,
                    ExpiryDate = d.ExpiryDate,
                    ExpiryStatus = d.ExpiryStatus,
                    Description = d.Description
                }).ToList();

                var patientRecord = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == viewModel.PatientId);

                ViewBag.PatientName = patientRecord?.User != null ? patientRecord.User.FullName : "Unknown Patient";
                return View(viewModel);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while creating prescription");
                TempData["ErrorMessage"] = "Database error occurred. Please try again.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating prescription");
                TempData["ErrorMessage"] = "Error creating prescription. Please try again.";
            }

            return RedirectToAction("PatientRecord", new { patientId = viewModel.PatientId });
        }
    }
}
