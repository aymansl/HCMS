using HCMS4.Data;
using HCMS4.Models;
using HCMS4.Models.Common;
using HCMS4.Services;
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

                var today = DateTime.Today;

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

        public async Task<IActionResult> ViewAppointments(string viewMode = "today", DateTime? selectedDate = null,
                                                     DateTime? startDate = null, DateTime? endDate = null,
                                                     string riskLevel = "all")
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                var viewModel = new DoctorAppointmentsViewModel
                {
                    SelectedDate = selectedDate,
                    StartDate = startDate,
                    EndDate = endDate,
                    ViewMode = viewMode,
                    SelectedRiskLevel = riskLevel
                };

                var today = DateTime.Today;
                var now = DateTime.UtcNow;

                if (viewMode == "today")
                {
                    viewModel.TodayAppointments = await _context.Appointments
                        .Include(a => a.Patient)
                            .ThenInclude(p => p.User)
                        .Include(a => a.Doctor)
                        .Where(a => a.DoctorId == doctorId.Value &&
                               a.AppointmentDateTime.Date == today &&
                               a.Status == AppointmentStatus.Scheduled)
                        .OrderBy(a => a.AppointmentDateTime)
                        .ToListAsync();

                    viewModel.UpcomingAppointments = await _context.Appointments
                        .Include(a => a.Patient)
                            .ThenInclude(p => p.User)
                        .Include(a => a.Doctor)
                        .Where(a => a.DoctorId == doctorId.Value &&
                               a.AppointmentDateTime > today.AddDays(1) &&
                               a.Status == AppointmentStatus.Scheduled)
                        .OrderBy(a => a.AppointmentDateTime)
                        .Take(50)
                        .ToListAsync();
                }
                else if (viewMode == "date" && selectedDate.HasValue)
                {
                    viewModel.TodayAppointments = await _context.Appointments
                        .Include(a => a.Patient)
                            .ThenInclude(p => p.User)
                        .Include(a => a.Doctor)
                        .Where(a => a.DoctorId == doctorId.Value &&
                               a.AppointmentDateTime.Date == selectedDate.Value.Date &&
                               a.Status == AppointmentStatus.Scheduled)
                        .OrderBy(a => a.AppointmentDateTime)
                        .ToListAsync();
                }
                else if (viewMode == "range" && startDate.HasValue && endDate.HasValue)
                {
                    var appointments = await _context.Appointments
                        .Include(a => a.Patient)
                            .ThenInclude(p => p.User)
                        .Include(a => a.Doctor)
                        .Where(a => a.DoctorId == doctorId.Value &&
                               a.AppointmentDateTime.Date >= startDate.Value.Date &&
                               a.AppointmentDateTime.Date <= endDate.Value.Date)
                        .OrderBy(a => a.AppointmentDateTime)
                        .ToListAsync();

                    foreach (var appointment in appointments)
                    {
                        if (appointment.AppointmentDateTime.Date == today &&
                            appointment.Status == AppointmentStatus.Scheduled)
                        {
                            viewModel.TodayAppointments.Add(appointment);
                        }
                        else if (appointment.AppointmentDateTime > now &&
                                 appointment.Status == AppointmentStatus.Scheduled)
                        {
                            viewModel.UpcomingAppointments.Add(appointment);
                        }
                        else
                        {
                            viewModel.PastAppointments.Add(appointment);
                        }
                    }
                }

                var appointmentIds = viewModel.TodayAppointments
                    .Concat(viewModel.UpcomingAppointments)
                    .Concat(viewModel.PastAppointments)
                    .Select(a => a.Id)
                    .Distinct()
                    .ToList();

                if (appointmentIds.Any())
                {
                    viewModel.AppointmentRiskScores = await _noShowRiskService.CalculateRiskScoresAsync(appointmentIds);
                    viewModel.AnalyticsServiceAvailable = _noShowRiskService.IsServiceAvailable;
                    viewModel.IsUsingAI = _noShowRiskService.IsUsingAI;

                    viewModel.TodayAppointments = viewModel.TodayAppointments
                        .Where(a => RiskLevelHelper.Matches(riskLevel, viewModel.AppointmentRiskScores.GetValueOrDefault(a.Id)))
                        .ToList();
                    viewModel.UpcomingAppointments = viewModel.UpcomingAppointments
                        .Where(a => RiskLevelHelper.Matches(riskLevel, viewModel.AppointmentRiskScores.GetValueOrDefault(a.Id)))
                        .ToList();
                    viewModel.PastAppointments = viewModel.PastAppointments
                        .Where(a => RiskLevelHelper.Matches(riskLevel, viewModel.AppointmentRiskScores.GetValueOrDefault(a.Id)))
                        .ToList();
                }

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading doctor appointments");
                TempData["ErrorMessage"] = "Error loading appointments. Please try again.";
                return View(new DoctorAppointmentsViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendReminder(int id)
        {
            var doctorId = await GetCurrentDoctorIdAsync();
            if (!doctorId.HasValue)
            {
                return RedirectToAction("Login", "Account");
            }

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == id && a.DoctorId == doctorId.Value);

            if (appointment == null || appointment.Status != AppointmentStatus.Scheduled)
            {
                TempData["ErrorMessage"] = "Appointment not found or cannot receive reminders.";
                return RedirectToAction(nameof(ViewAppointments));
            }

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == appointment.PatientId);
            if (patient != null)
            {
                await _notificationService.CreateForUserAsync(
                    patient.UserId,
                    "Appointment reminder",
                    $"Reminder: you have an appointment on {appointment.AppointmentDateTime:yyyy-MM-dd HH:mm}.",
                    NotificationType.AppointmentReminder,
                    "/Patient/ViewAppointmentHistory",
                    nameof(Appointment),
                    appointment.Id);
            }

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "AppointmentReminderSent",
                nameof(Appointment),
                $"Doctor sent a reminder for appointment #{appointment.Id}.",
                appointment.Id.ToString(),
                currentUser?.Id,
                currentUser?.UserName);

            TempData["SuccessMessage"] = "Reminder sent to the patient.";
            return RedirectToAction(nameof(ViewAppointments));
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
            request.ReviewedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _notificationService.CreateForUserAsync(
                request.Patient.UserId,
                "Prescription re-issuance updated",
                $"Your re-issuance request for prescription #{request.PrescriptionId} was {(approve ? "approved" : "rejected")}.",
                NotificationType.PrescriptionReissue,
                "/Patient/Prescriptions",
                nameof(PrescriptionReissueRequest),
                request.Id);

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                approve ? "PrescriptionReissueApproved" : "PrescriptionReissueRejected",
                nameof(PrescriptionReissueRequest),
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
            request.ReviewedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _notificationService.CreateForUserAsync(
                request.Pharmacist.UserId,
                "Doctor review response",
                $"Doctor responded to review request for prescription #{request.PrescriptionId}.",
                NotificationType.DoctorReview,
                "/Pharmacist/Prescriptions",
                nameof(DoctorReviewRequest),
                request.Id);

            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(
                "DoctorReviewResponded",
                nameof(DoctorReviewRequest),
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
            article.PublishedAt = DateTime.UtcNow;
            article.UpdatedAt = DateTime.UtcNow;

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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PublishedAt = publishNow ? DateTime.UtcNow : null
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
        public async Task<IActionResult> PatientRecord(int patientId)
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                var hasAccess = await _context.Appointments
                    .AnyAsync(a => a.DoctorId == doctorId.Value && a.PatientId == patientId);

                if (!hasAccess)
                {
                    TempData["ErrorMessage"] = "You do not have access to this patient's record.";
                    return RedirectToAction("ViewAppointments");
                }

                var patient = await _context.Patients
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.Id == patientId);

                if (patient == null)
                {
                    TempData["ErrorMessage"] = "Patient not found.";
                    return RedirectToAction("ViewAppointments");
                }

                var viewModel = new PatientMedicalRecordViewModel
                {
                    PatientId = patient.Id,
                    FullName = patient.User != null ? patient.User.FullName : "Unknown",
                    Email = patient.User != null ? patient.User.Email : "No email",
                    PhoneNumber = patient.User?.PhoneNumber,
                    DateOfBirth = patient.DateOfBirth,
                    Address = patient.Address,
                    EmergencyContact = patient.EmergencyContact,
                    ChronicConditions = patient.ChronicConditions
                };

                var appointments = await _context.Appointments
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.User)
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d.Specialization)
                    .Include(a => a.Patient)
                    .Where(a => a.PatientId == patientId)
                    .OrderByDescending(a => a.AppointmentDateTime)
                    .ToListAsync();

                viewModel.AppointmentHistory = appointments.Select(a => new AppointmentHistoryViewModel
                {
                    Id = a.Id,
                    AppointmentDateTime = a.AppointmentDateTime,
                    DoctorName = a.Doctor?.User != null ? a.Doctor.User.FullName : "Unknown Doctor",
                    Specialization = a.Doctor?.Specialization?.Name ?? "General",
                    Status = a.Status.ToString(),
                    ConsultationFee = a.ConsultationFee,
                    CreatedAt = a.CreatedAt
                }).ToList();

                var prescriptions = await _context.Prescriptions
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.User)
                    .Include(p => p.Doctor)
                        .ThenInclude(d => d.Specialization)
                    .Include(p => p.PrescriptionItems)
                        .ThenInclude(pi => pi.Drug)
                    .Where(p => p.PatientId == patientId)
                    .OrderByDescending(p => p.PrescriptionDate)
                    .ToListAsync();

                var prescriptionNotes = await _context.PharmacistPrescriptionNotes
                    .Include(n => n.Pharmacist)
                        .ThenInclude(p => p.User)
                    .Where(n => prescriptions.Select(p => p.Id).Contains(n.PrescriptionId))
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                viewModel.Prescriptions = prescriptions.Select(p => new PrescriptionViewModel
                {
                    Id = p.Id,
                    PrescriptionDate = p.PrescriptionDate,
                    DoctorName = p.Doctor?.User != null ? p.Doctor.User.FullName : "Unknown Doctor",
                    Specialization = p.Doctor?.Specialization?.Name ?? "General",
                    Status = p.Status.ToString(),
                    Notes = p.Notes,
                    TotalCost = p.TotalCost,
                    Items = p.PrescriptionItems.Select(pi => new PrescriptionItemViewModel
                    {
                        DrugName = pi.DrugName,
                        Dosage = pi.Dosage,
                        Frequency = pi.Frequency,
                        Duration = int.TryParse(pi.Duration, out int dur) ? dur : 0,
                        Instructions = pi.Instructions
                    }).ToList(),
                    PharmacistNotes = prescriptionNotes
                        .Where(note => note.PrescriptionId == p.Id)
                        .Select(note => new PrescriptionNoteDisplayViewModel
                        {
                            PharmacistName = note.Pharmacist?.User?.FullName ?? "Pharmacist",
                            NoteText = note.NoteText,
                            NotifyDoctor = note.NotifyDoctor,
                            CreatedAt = note.CreatedAt
                        }).ToList()
                }).ToList();

                var clinicalNotes = await _context.ClinicalNotes
                    .Include(cn => cn.Doctor)
                        .ThenInclude(d => d.User)
                    .Where(cn => cn.PatientId == patientId)
                    .OrderByDescending(cn => cn.Date)
                    .ToListAsync();

                viewModel.ClinicalNotes = clinicalNotes.Select(cn => new ClinicalNoteViewModel
                {
                    Id = cn.Id,
                    Date = cn.Date,
                    NoteType = cn.NoteType,
                    Content = cn.Content,
                    Diagnosis = cn.Diagnosis,
                    DoctorName = cn.Doctor?.User != null ? cn.Doctor.User.FullName : "Unknown Doctor"
                }).ToList();

                var invoices = await _context.Invoices
                    .Include(i => i.Appointment)
                    .Where(i => i.PatientId == patientId)
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToListAsync();

                viewModel.Invoices = invoices.Select(i => new InvoiceViewModel
                {
                    Id = i.Id,
                    InvoiceDate = i.InvoiceDate,
                    TotalAmount = i.TotalAmount,
                    PaymentStatus = i.PaymentStatus.ToString(),
                    PaymentDate = i.PaymentDate
                }).ToList();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading patient record for patient ID: {PatientId}", patientId);
                TempData["ErrorMessage"] = "Unable to load patient record. Please try again.";
                return RedirectToAction("ViewAppointments");
            }
        }

        [HttpGet]
        public IActionResult CreateClinicalNote(int patientId)
        {
            try
            {
                var doctorId = GetCurrentDoctorIdAsync().Result;
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                ViewBag.PatientId = patientId;
                var viewModel = new CreateClinicalNoteViewModel
                {
                    PatientId = patientId
                };
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading create clinical note form");
                TempData["ErrorMessage"] = "Error loading form. Please try again.";
                return RedirectToAction("PatientRecord", new { patientId });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClinicalNote(CreateClinicalNoteViewModel viewModel)
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                if (ModelState.IsValid)
                {
                    var clinicalNote = new ClinicalNote
                    {
                        PatientId = viewModel.PatientId,
                        DoctorId = doctorId.Value,
                        NoteType = viewModel.NoteType,
                        Content = viewModel.Content,
                        Diagnosis = viewModel.Diagnosis,
                        Date = DateTime.UtcNow
                    };

                    _context.ClinicalNotes.Add(clinicalNote);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Clinical note added successfully.";
                    return RedirectToAction("PatientRecord", new { patientId = viewModel.PatientId });
                }

                ViewBag.PatientId = viewModel.PatientId;
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating clinical note");
                TempData["ErrorMessage"] = "Error creating clinical note. Please try again.";
                return RedirectToAction("PatientRecord", new { patientId = viewModel.PatientId });
            }
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
                    .Where(d => d.Quantity > 0 && d.ExpiryDate > DateTime.UtcNow)
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

                        if (drug.ExpiryDate <= DateTime.UtcNow)
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
                            .Where(d => d.Quantity > 0 && d.ExpiryDate > DateTime.UtcNow)
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
                        PrescriptionDate = DateTime.UtcNow,
                        Notes = viewModel.Notes,
                        Status = PrescriptionStatus.Pending,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
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
                            Instructions = item.Instructions
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
                    .Where(d => d.Quantity > 0 && d.ExpiryDate > DateTime.UtcNow)
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

        public async Task<IActionResult> Profile()
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                var doctor = await _context.Doctors
                    .Include(d => d.User)
                    .Include(d => d.Specialization)
                    .FirstOrDefaultAsync(d => d.Id == doctorId.Value);

                if (doctor == null)
                {
                    TempData["ErrorMessage"] = "Doctor profile not found.";
                    return RedirectToAction("Dashboard");
                }

                var leaveRequests = await _leaveService.GetPendingRequestsForDoctorAsync(doctorId.Value);
                var approvedLeaves = await _leaveService.GetDoctorLeavesAsync(doctorId.Value);

                var viewModel = new DoctorLeaveViewModel
                {
                    DoctorId = doctor.Id,
                    DoctorName = doctor.FullName,
                    LeaveRequests = leaveRequests.Select(lr => new LeaveRequestViewModel
                    {
                        Id = lr.Id,
                        StartDate = lr.StartDate,
                        EndDate = lr.EndDate,
                        LeaveType = lr.LeaveType,
                        Reason = lr.Reason,
                        Status = lr.Status,
                        CreatedAt = lr.CreatedAt
                    }).ToList(),
                    ApprovedLeaves = approvedLeaves.Select(dl => new DoctorLeaveItem
                    {
                        Id = dl.Id,
                        StartDate = dl.StartDate,
                        EndDate = dl.EndDate,
                        LeaveType = dl.LeaveType.ToString(),
                        Notes = dl.Notes,
                        Status = dl.Status
                    }).ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading doctor profile");
                TempData["ErrorMessage"] = "Error loading profile. Please try again.";
                return RedirectToAction("Dashboard");
            }
        }

        [HttpGet]
        public IActionResult RequestLeave()
        {
            var viewModel = new LeaveRequestViewModel
            {
                StartDate = DateTime.Today.AddDays(1),
                EndDate = DateTime.Today.AddDays(2)
            };
            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestLeave(LeaveRequestViewModel model)
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                if (model.StartDate < DateTime.Today)
                {
                    ModelState.AddModelError("StartDate", "Cannot request leave for a past date.");
                    return View(model);
                }

                if (model.EndDate < model.StartDate)
                {
                    ModelState.AddModelError("EndDate", "End date must be after or equal to start date.");
                    return View(model);
                }

                var request = new LeaveRequest
                {
                    DoctorId = doctorId.Value,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    LeaveType = model.LeaveType,
                    Reason = model.Reason,
                    CreatedAt = DateTime.UtcNow,
                    Status = LeaveStatus.Pending
                };

                await _leaveService.CreateLeaveRequestAsync(request);

                TempData["SuccessMessage"] = "Leave request submitted successfully.";
                return RedirectToAction("Profile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting leave request");
                TempData["ErrorMessage"] = "Failed to submit request, please try again later.";
                return View(model);
            }
        }

        public async Task<IActionResult> ViewReports()
        {
            try
            {
                var doctorId = await GetCurrentDoctorIdAsync();
                if (!doctorId.HasValue)
                    return RedirectToAction("Login", "Account");

                return RedirectToAction("DailyVisitingPatientsReport", "Admin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reports");
                TempData["ErrorMessage"] = "Error loading reports. Please try again.";
                return RedirectToAction("Dashboard");
            }
        }
    }
}
