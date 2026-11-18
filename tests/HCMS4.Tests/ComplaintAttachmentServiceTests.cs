using HCMS4.Modules.Feedback.Services;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Shared.Common;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HCMS4.Tests
{
    // Regression tests for issue #29: attachments were stored in wwwroot without content checks.
    public class ComplaintAttachmentServiceTests : IDisposable
    {
        private static readonly byte[] PdfHeader = Encoding.ASCII.GetBytes("%PDF-1.7\n");
        private readonly string _storagePath = Path.Combine(Path.GetTempPath(), $"hcms-attach-{Guid.NewGuid():N}");

        private ComplaintAttachmentService CreateService(HCMS4.Data.ApplicationDbContext context)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Uploads:ComplaintsPath"] = _storagePath })
                .Build();
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());
            return new ComplaintAttachmentService(context, environment.Object, configuration,
                NullLogger<ComplaintAttachmentService>.Instance);
        }

        private static IFormFile CreateFile(string fileName, byte[] content)
        {
            var stream = new MemoryStream(content);
            return new FormFile(stream, 0, content.Length, "Attachment", fileName);
        }

        public void Dispose()
        {
            if (Directory.Exists(_storagePath))
            {
                Directory.Delete(_storagePath, recursive: true);
            }
        }

        [Fact]
        public async Task SaveAsync_ValidPdf_StoresFileOutsideWebRoot()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var service = CreateService(context);

            var result = await service.SaveAsync(CreateFile("report.pdf", PdfHeader.Concat(new byte[100]).ToArray()));

            Assert.True(result.Success);
            Assert.EndsWith(".pdf", result.Data);
            Assert.True(File.Exists(Path.Combine(_storagePath, result.Data)));
            Assert.DoesNotContain("wwwroot", _storagePath);
        }

        [Fact]
        public async Task SaveAsync_DisallowedExtension_IsRejected()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var service = CreateService(context);

            var result = await service.SaveAsync(CreateFile("script.exe", PdfHeader));

            Assert.False(result.Success);
            Assert.False(Directory.Exists(_storagePath) && Directory.EnumerateFiles(_storagePath).Any());
        }

        [Fact]
        public async Task SaveAsync_ContentDoesNotMatchExtension_IsRejected()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var service = CreateService(context);

            var result = await service.SaveAsync(CreateFile("photo.png", Encoding.UTF8.GetBytes("<script>alert(1)</script>")));

            Assert.False(result.Success);
        }

        [Fact]
        public async Task SaveAsync_TooLarge_IsRejected()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var service = CreateService(context);
            var content = PdfHeader.Concat(new byte[BusinessRules.ComplaintAttachmentMaxBytes]).ToArray();

            var result = await service.SaveAsync(CreateFile("big.pdf", content));

            Assert.False(result.Success);
        }

        [Fact]
        public async Task GetForUserAsync_OtherPatient_IsDenied_OwnerAndAdminAllowed()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var service = CreateService(context);
            var (ownerUser, owner) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var (otherUser, _) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var stored = await service.SaveAsync(CreateFile("report.pdf", PdfHeader));
            var complaint = new Complaint
            {
                PatientId = owner.Id,
                Title = "t",
                Description = "d",
                TrackingNumber = "CMP-TEST",
                AttachmentPath = stored.Data
            };
            context.Complaints.Add(complaint);
            await context.SaveChangesAsync();

            var asOwner = await service.GetForUserAsync(complaint.Id, ownerUser.Id, isAdmin: false);
            var asOther = await service.GetForUserAsync(complaint.Id, otherUser.Id, isAdmin: false);
            var asAdmin = await service.GetForUserAsync(complaint.Id, "admin-id", isAdmin: true);

            Assert.True(asOwner.Success);
            Assert.Equal("application/pdf", asOwner.Data.ContentType);
            Assert.False(asOther.Success);
            Assert.True(asAdmin.Success);
        }

        [Fact]
        public async Task GetForUserAsync_PathTraversalInStoredPath_IsContained()
        {
            using var context = TestDbContextFactory.CreateInMemoryContext();
            var service = CreateService(context);
            var (_, owner) = await TestDbContextFactory.CreateTestPatientAsync(context);
            var complaint = new Complaint
            {
                PatientId = owner.Id,
                Title = "t",
                Description = "d",
                TrackingNumber = "CMP-TEST2",
                AttachmentPath = "../../appsettings.json"
            };
            context.Complaints.Add(complaint);
            await context.SaveChangesAsync();

            var result = await service.GetForUserAsync(complaint.Id, "admin-id", isAdmin: true);

            Assert.False(result.Success);
        }
    }
}
