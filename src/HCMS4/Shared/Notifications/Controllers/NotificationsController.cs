using HCMS4.Shared.Common;
using HCMS4.Shared.Identity.Authorization;
using HCMS4.Shared.Notifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Shared.Notifications.Controllers
{
    /// <summary>The signed-in user's notifications, for every role.</summary>
    [Authorize(Policy = Permissions.Notifications.View)]
    public class NotificationsController : HcmsControllerBase
    {
        public const int PageSize = 50;

        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            return View(await _notificationService.GetRecentForUserAsync(CurrentUserId ?? string.Empty, PageSize));
        }

        /// <summary>Marks the notification as read and opens its page.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Open(int id)
        {
            var notification = await _notificationService.MarkAsReadAsync(CurrentUserId ?? string.Empty, id);
            if (notification == null)
            {
                return NotFound();
            }

            // Only links inside the application are followed.
            return !string.IsNullOrEmpty(notification.LinkUrl) && Url.IsLocalUrl(notification.LinkUrl)
                ? LocalRedirect(notification.LinkUrl)
                : RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var count = await _notificationService.MarkAllAsReadAsync(CurrentUserId ?? string.Empty);
            TempData["SuccessMessage"] = count == 0 ? "No unread notifications." : $"{count} notification(s) marked as read.";
            return RedirectToAction(nameof(Index));
        }
    }
}
