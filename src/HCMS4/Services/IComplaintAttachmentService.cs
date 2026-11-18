using HCMS4.Models.Common;

namespace HCMS4.Services
{
    public interface IComplaintAttachmentService
    {
        /// <summary>Validates and stores an uploaded file outside the web root; returns the stored file name.</summary>
        Task<ServiceResult<string>> SaveAsync(IFormFile file);

        /// <summary>Returns the attachment of a complaint if the user is an admin or the complaint's patient.</summary>
        Task<ServiceResult<ComplaintAttachmentFile>> GetForUserAsync(int complaintId, string userId, bool isAdmin);
    }

    public record ComplaintAttachmentFile(string PhysicalPath, string ContentType, string DownloadName);
}
