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

namespace HCMS4.Modules.Feedback.Controllers
{
    // Part of the former DoctorController; [ControllerName] keeps the /Doctor/... URLs.
    [Authorize(Roles = "Doctor")]
    [ControllerName("Doctor")]
    public class DoctorArticlesController : BaseController
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IPrescriptionService _prescriptionService;
        private readonly ILeaveService _leaveService;
        private readonly INoShowRiskService _noShowRiskService;
        private readonly INotificationService _notificationService;
        private readonly IActivityLogService _activityLogService;

        public DoctorArticlesController(
            ApplicationDbContext context,
            ILogger<DoctorArticlesController> logger,
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
    }
}
