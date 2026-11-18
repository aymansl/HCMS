using HCMS4.Modules.Feedback.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

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

            // PDFs and images open in the browser tab; Word files are downloaded.
            var file = result.Data;
            var inline = file.ContentType == "application/pdf" || file.ContentType.StartsWith("image/", StringComparison.Ordinal);
            var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment") { FileName = file.DownloadName };
            Response.Headers.ContentDisposition = disposition.ToString();
            Response.Headers.Append("X-Content-Type-Options", "nosniff");
            return PhysicalFile(file.PhysicalPath, file.ContentType, enableRangeProcessing: true);
        }
    }
}
