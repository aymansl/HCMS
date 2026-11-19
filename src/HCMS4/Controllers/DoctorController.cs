using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Notifications.Services;
using HCMS4.Modules.Appointments.Models;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Notifications.Models;
using HCMS4.Shared.Common;
using HCMS4.Data;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Controllers
{
    [Authorize(Roles = "Doctor")]
    public class DoctorController : BaseController
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IPrescriptionService _prescriptionService;
        private readonly ILeaveService _leaveService;
        private readonly INoShowRiskService _noShowRiskService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;

        public DoctorController(
            ApplicationDbContext context,
            ILogger<DoctorController> logger,
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

        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                var today = ClinicTime.Today;

                var todayAppointments = await _context.Appointments
                    .Include(a => a.Patient)
                        .ThenInclude(p => p.User)
                    .Include(a => a.Doctor)
                    .Where(a => a.DoctorId == doctorId.Value &&
                           a.AppointmentDateTime.Date == today &&
                           a.Status == AppointmentStatus.Scheduled)
                    .OrderBy(a => a.AppointmentDateTime)
                    .ToListAsync();

                var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId.Value);
                var pendingReissueCount = await _context.PrescriptionReissueRequests
                    .CountAsync(r => r.DoctorId == doctorId.Value && r.Status == ReissueRequestStatus.Pending);
                var pendingReviewCount = await _context.DoctorReviewRequests
                    .CountAsync(r => r.DoctorId == doctorId.Value && r.Status == ReviewRequestStatus.Pending);
                var recentRatings = await _context.VisitRatings
                    .Include(r => r.Patient)
                        .ThenInclude(p => p.User)
                    .Where(r => r.DoctorId == doctorId.Value)
                    .OrderByDescending(r => r.CreatedAt)
                    .Take(5)
                    .ToListAsync();
                var notificationUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                List<UserNotification> notifications;
                try
                {
                    notifications = await _context.UserNotifications
                        .Where(n => n.UserId == notificationUserId)
                        .OrderByDescending(n => n.CreatedAt)
                        .Take(5)
                        .ToListAsync();
                }
                catch
                {
                    notifications = new List<UserNotification>();
                }

                var viewModel = new DoctorDashboardViewModel
                {
                    TodayAppointmentsCount = todayAppointments.Count,
                    PendingReissueRequestsCount = pendingReissueCount,
                    PendingReviewRequestsCount = pendingReviewCount,
                    PublishedArticlesCount = await _context.MedicalArticles.CountAsync(a => a.DoctorId == doctorId.Value && a.Status == ArticleStatus.Published),
                    DraftArticlesCount = await _context.MedicalArticles.CountAsync(a => a.DoctorId == doctorId.Value && a.Status == ArticleStatus.Draft),
                    AverageRating = doctor?.AverageRating ?? 0,
                    RatingCount = doctor?.RatingCount ?? 0,
                    RecentRatings = recentRatings,
                    RecentNotifications = notifications
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading doctor dashboard");
                TempData["ErrorMessage"] = "Error loading dashboard. Please try again.";
                return View(new DoctorDashboardViewModel());
            }
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
        public async Task<IActionResult> MedicalArticles()
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId.Value);
            ViewBag.CanPublishArticles = doctor?.CanPublishArticles ?? false;

            var articles = await _context.MedicalArticles
                .Where(a => a.DoctorId == doctorId.Value)
                .OrderByDescending(a => a.UpdatedAt)
                .ToListAsync();

            return View(articles);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PublishMedicalArticle(int id)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId.Value);
            if (doctor == null || !doctor.CanPublishArticles)
            {
                TempData["ErrorMessage"] = "You do not have permission to publish articles.";
                return RedirectToAction(nameof(MedicalArticles));
            }

            var article = await _context.MedicalArticles
                .FirstOrDefaultAsync(a => a.Id == id && a.DoctorId == doctorId.Value);

            if (article == null)
            {
                TempData["ErrorMessage"] = "Article not found.";
                return RedirectToAction(nameof(MedicalArticles));
            }

            if (article.Status == ArticleStatus.Published)
            {
                TempData["ErrorMessage"] = "This article is already published.";
                return RedirectToAction(nameof(MedicalArticles));
            }

            article.Status = ArticleStatus.Published;
            article.PublishedAt = ClinicTime.Now;
            article.UpdatedAt = ClinicTime.Now;

            await _context.SaveChangesAsync();

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "MedicalArticlePublished",
                nameof(MedicalArticle),
                $"Doctor published draft article '{article.Title}'.",
                article.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Article published successfully.";
            return RedirectToAction(nameof(MedicalArticles));
        }

        [HttpGet]
        public async Task<IActionResult> CreateMedicalArticle()
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId.Value);
            if (doctor == null || !doctor.CanPublishArticles)
            {
                TempData["ErrorMessage"] = "You do not have permission to publish articles.";
                return RedirectToAction(nameof(MedicalArticles));
            }

            return View(new MedicalArticleEditorViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMedicalArticle(MedicalArticleEditorViewModel model, string submitAction)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId.Value);
            if (doctor == null || !doctor.CanPublishArticles)
            {
                TempData["ErrorMessage"] = "You do not have permission to publish articles.";
                return RedirectToAction(nameof(MedicalArticles));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var publishNow = string.Equals(submitAction, "publish", StringComparison.OrdinalIgnoreCase);
            var article = new MedicalArticle
            {
                DoctorId = doctorId.Value,
                Title = model.Title.Trim(),
                Summary = model.Summary?.Trim(),
                Content = model.Content.Trim(),
                Category = model.Category?.Trim(),
                Status = publishNow ? ArticleStatus.Published : ArticleStatus.Draft,
                CreatedAt = ClinicTime.Now,
                UpdatedAt = ClinicTime.Now,
                PublishedAt = publishNow ? ClinicTime.Now : null
            };

            _context.MedicalArticles.Add(article);
            await _context.SaveChangesAsync();

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                publishNow ? "MedicalArticlePublished" : "MedicalArticleDraftSaved",
                nameof(MedicalArticle),
                $"Doctor {(publishNow ? "published" : "saved")} article '{article.Title}'.",
                article.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Article saved successfully.";
            return RedirectToAction(nameof(MedicalArticles));
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
