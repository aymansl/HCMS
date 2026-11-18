using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Tests.Integration
{
    // End-to-end upload through the real complaint form (multipart POST with antiforgery token).
    public class CreateComplaintUploadTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public CreateComplaintUploadTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<HttpResponseMessage> PostComplaintAsync(HttpClient client, string? fileName, byte[]? content)
        {
            var token = await FormHelper.GetAntiforgeryTokenAsync(client, "/Patient/CreateComplaint");
            using var form = new MultipartFormDataContent
            {
                { new StringContent(token), "__RequestVerificationToken" },
                { new StringContent("Billing issue"), "Title" },
                { new StringContent("1"), "Type" },
                { new StringContent("The invoice amount is wrong."), "Description" }
            };
            if (fileName != null && content != null)
            {
                var file = new ByteArrayContent(content);
                file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                form.Add(file, "Attachment", fileName);
            }
            return await client.PostAsync("/Patient/CreateComplaint", form);
        }

        private async Task<string> CreatePatientUserAsync()
        {
            string userId = string.Empty;
            await _factory.WithDbAsync(async db =>
            {
                var (user, _) = await TestDbContextFactory.CreateTestPatientAsync(db);
                userId = user.Id;
            });
            return userId;
        }

        [Fact]
        public async Task CreateComplaint_WithPdfAttachment_SavesComplaintAndFile()
        {
            var userId = await CreatePatientUserAsync();
            var client = _factory.CreateClientAs("Patient", userId);

            var response = await PostComplaintAsync(client, "invoice.pdf", Encoding.ASCII.GetBytes("%PDF-1.7 test file"));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            string? storedName = null;
            await _factory.WithDbAsync(async db =>
            {
                var complaint = await db.Complaints.Include(c => c.Patient)
                    .SingleAsync(c => c.Patient.UserId == userId);
                storedName = complaint.AttachmentPath;
            });
            Assert.NotNull(storedName);
            Assert.True(File.Exists(Path.Combine(_factory.UploadsPath, storedName!)));
        }

        [Theory]
        [InlineData("scan.pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 })]
        [InlineData("photo.png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
        [InlineData("photo.jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })]
        [InlineData("letter.docx", new byte[] { 0x50, 0x4B, 0x03, 0x04 })]
        public async Task CreateComplaint_WithLargeAttachment_SavesComplaint(string fileName, byte[] header)
        {
            var userId = await CreatePatientUserAsync();
            var client = _factory.CreateClientAs("Patient", userId);
            // 600 KB: larger than the 64 KB in-memory form buffer, so the upload is buffered to disk.
            var content = header.Concat(new byte[600 * 1024]).ToArray();

            var response = await PostComplaintAsync(client, fileName, content);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        [Fact]
        public async Task CreateComplaint_WithoutAttachment_SavesComplaint()
        {
            var userId = await CreatePatientUserAsync();
            var client = _factory.CreateClientAs("Patient", userId);

            var response = await PostComplaintAsync(client, null, null);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
    }
}
