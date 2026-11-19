using System.Globalization;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Feedback.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HCMS4.Shared.Identity.Authorization;

namespace HCMS4.Modules.Feedback.Controllers
{
    // Part of the former DoctorController; [ControllerName] keeps the /Doctor/... URLs.
    [Authorize(Policy = Permissions.Feedback.PublishArticles)]
    [ControllerName("Doctor")]
    public class DoctorArticlesController : HcmsControllerBase
    {
        private readonly IMedicalArticleService _articleService;
        private readonly IActivityLogService _activityLogService;

        public DoctorArticlesController(IMedicalArticleService articleService, IActivityLogService activityLogService)
        {
            _articleService = articleService;
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

            ViewBag.CanPublishArticles = await _articleService.CanPublishAsync(doctorId.Value);
            return View(await _articleService.GetForDoctorAsync(doctorId.Value));
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

            var result = await _articleService.PublishAsync(doctorId.Value, id);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(MedicalArticles));
            }

            await LogAsync("MedicalArticlePublished", $"Doctor published draft article '{result.Data.Title}'.", result.Data.Id);

            TempData["SuccessMessage"] = result.Message;
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

            if (!await _articleService.CanPublishAsync(doctorId.Value))
            {
                TempData["ErrorMessage"] = MedicalArticleService.NoPermissionMessage;
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

            if (!await _articleService.CanPublishAsync(doctorId.Value))
            {
                TempData["ErrorMessage"] = MedicalArticleService.NoPermissionMessage;
                return RedirectToAction(nameof(MedicalArticles));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var publishNow = string.Equals(submitAction, "publish", StringComparison.OrdinalIgnoreCase);
            var result = await _articleService.CreateAsync(doctorId.Value, model, publishNow);
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction(nameof(MedicalArticles));
            }

            await LogAsync(publishNow ? "MedicalArticlePublished" : "MedicalArticleDraftSaved",
                $"Doctor {(publishNow ? "published" : "saved")} article '{result.Data.Title}'.", result.Data.Id);

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(MedicalArticles));
        }

        private async Task LogAsync(string action, string description, int articleId)
        {
            var currentUser = await GetCurrentUserAsync();
            await _activityLogService.LogAsync(action, nameof(MedicalArticle), description, articleId.ToString(CultureInfo.InvariantCulture),
                currentUser?.Id, currentUser?.UserName);
        }
    }
}
