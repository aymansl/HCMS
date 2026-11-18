using HCMS4.Shared.Common;
using HCMS4.Data;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Feedback.Services
{
    public class ComplaintAttachmentService : IComplaintAttachmentService
    {
        // Extension → (content type, accepted file signatures).
        private static readonly Dictionary<string, (string ContentType, byte[][] Signatures)> AllowedTypes = new()
        {
            [".jpg"] = ("image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),
            [".jpeg"] = ("image/jpeg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),
            [".png"] = ("image/png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } }),
            [".pdf"] = ("application/pdf", new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } }),
            [".doc"] = ("application/msword", new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } }),
            [".docx"] = ("application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 } })
        };

        private readonly ApplicationDbContext _context;
        private readonly ILogger<ComplaintAttachmentService> _logger;
        private readonly string _storagePath;

        public ComplaintAttachmentService(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<ComplaintAttachmentService> logger)
        {
            _context = context;
            _logger = logger;
            _storagePath = configuration["Uploads:ComplaintsPath"]
                ?? Path.Combine(environment.ContentRootPath, "App_Data", "uploads", "complaints");
        }

        public async Task<ServiceResult<string>> SaveAsync(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedTypes.TryGetValue(extension, out var type))
            {
                return ServiceResult<string>.Fail("File type not supported.");
            }

            if (file.Length == 0 || file.Length > BusinessRules.ComplaintAttachmentMaxBytes)
            {
                return ServiceResult<string>.Fail("File size exceeds the allowed limit.");
            }

            if (!await HasValidSignatureAsync(file, type.Signatures))
            {
                return ServiceResult<string>.Fail("File content does not match its type.");
            }

            Directory.CreateDirectory(_storagePath);
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            await using (var stream = File.Create(Path.Combine(_storagePath, storedFileName)))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("Complaint attachment stored as {FileName}", storedFileName);
            return ServiceResult<string>.Ok(storedFileName);
        }

        public async Task<ServiceResult<ComplaintAttachmentFile>> GetForUserAsync(int complaintId, string userId, bool isAdmin)
        {
            var complaint = await _context.Complaints
                .AsNoTracking()
                .Include(c => c.Patient)
                .FirstOrDefaultAsync(c => c.Id == complaintId);

            if (complaint == null || string.IsNullOrWhiteSpace(complaint.AttachmentPath))
            {
                return ServiceResult<ComplaintAttachmentFile>.Fail("Attachment not found.");
            }

            if (!isAdmin && complaint.Patient?.UserId != userId)
            {
                // Same answer as "not found" so other patients cannot probe for attachments.
                return ServiceResult<ComplaintAttachmentFile>.Fail("Attachment not found.");
            }

            // Only the file name is trusted; older rows stored a web path ("/uploads/complaints/<name>").
            var fileName = Path.GetFileName(complaint.AttachmentPath);
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var physicalPath = Path.Combine(_storagePath, fileName);

            if (!AllowedTypes.TryGetValue(extension, out var type) || !File.Exists(physicalPath))
            {
                return ServiceResult<ComplaintAttachmentFile>.Fail("Attachment not found.");
            }

            var downloadName = $"complaint-{complaint.Id}{extension}";
            return ServiceResult<ComplaintAttachmentFile>.Ok(
                new ComplaintAttachmentFile(physicalPath, type.ContentType, downloadName));
        }

        private static async Task<bool> HasValidSignatureAsync(IFormFile file, byte[][] signatures)
        {
            var header = new byte[signatures.Max(s => s.Length)];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
            return signatures.Any(s => read >= s.Length && header.AsSpan(0, s.Length).SequenceEqual(s));
        }
    }
}
