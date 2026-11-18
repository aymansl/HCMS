using System.Security.Claims;
using HCMS4.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCMS4.Controllers
{
    /// <summary>Serves complaint attachments, which are stored outside wwwroot (issue #29).</summary>
    [Authorize(Roles = "Admin,Patient")]
    public class ComplaintAttachmentsController : Controller
    {
        private readonly IComplaintAttachmentService _attachmentService;

        public ComplaintAttachmentsController(IComplaintAttachmentService attachmentService)
        {
            _attachmentService = attachmentService;
        }

        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var result = await _attachmentService.GetForUserAsync(id, userId, User.IsInRole("Admin"));
            if (!result.Success)
            {
                return NotFound();
            }

            Response.Headers.Append("X-Content-Type-Options", "nosniff");
            return PhysicalFile(result.Data.PhysicalPath, result.Data.ContentType, result.Data.DownloadName);
        }
    }
}
