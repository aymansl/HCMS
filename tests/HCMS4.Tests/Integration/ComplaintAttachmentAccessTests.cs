using System.Net;
using System.Text;
using HCMS4.Models;

namespace HCMS4.Tests.Integration
{
    // Attachments are only reachable through the authorized download action, never as static files.
    public class ComplaintAttachmentAccessTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public ComplaintAttachmentAccessTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(string OwnerUserId, string OtherUserId, int ComplaintId, string FileName)> CreateComplaintAsync()
        {
            Directory.CreateDirectory(_factory.UploadsPath);
            var fileName = $"{Guid.NewGuid():N}.pdf";
            await File.WriteAllBytesAsync(Path.Combine(_factory.UploadsPath, fileName), Encoding.ASCII.GetBytes("%PDF-1.7 test"));

            string ownerUserId = string.Empty, otherUserId = string.Empty;
            int complaintId = 0;
            await _factory.WithDbAsync(async db =>
            {
                var (ownerUser, owner) = await TestDbContextFactory.CreateTestPatientAsync(db);
                var (otherUser, _) = await TestDbContextFactory.CreateTestPatientAsync(db);
                var complaint = new Complaint
                {
                    PatientId = owner.Id,
                    Title = "Billing",
                    Description = "Wrong amount",
                    TrackingNumber = $"CMP-{Guid.NewGuid():N}",
                    AttachmentPath = fileName
                };
                db.Complaints.Add(complaint);
                await db.SaveChangesAsync();
                ownerUserId = ownerUser.Id;
                otherUserId = otherUser.Id;
                complaintId = complaint.Id;
            });
            return (ownerUserId, otherUserId, complaintId, fileName);
        }

        [Fact]
        public async Task Download_AsOwner_ReturnsFile()
        {
            var (ownerUserId, _, complaintId, _) = await CreateComplaintAsync();
            var client = _factory.CreateClientAs("Patient", ownerUserId);

            var response = await client.GetAsync($"/ComplaintAttachments/Download/{complaintId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task Download_AsAdmin_ReturnsFile()
        {
            var (_, _, complaintId, _) = await CreateComplaintAsync();
            var client = _factory.CreateClientAs("Admin");

            var response = await client.GetAsync($"/ComplaintAttachments/Download/{complaintId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Download_AsOtherPatient_ReturnsNotFound()
        {
            var (_, otherUserId, complaintId, _) = await CreateComplaintAsync();
            var client = _factory.CreateClientAs("Patient", otherUserId);

            var response = await client.GetAsync($"/ComplaintAttachments/Download/{complaintId}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Download_AsDoctor_IsForbidden()
        {
            var (_, _, complaintId, _) = await CreateComplaintAsync();
            var client = _factory.CreateClientAs("Doctor");

            var response = await client.GetAsync($"/ComplaintAttachments/Download/{complaintId}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task StoredFile_IsNotServedAsStaticContent()
        {
            var (_, _, _, fileName) = await CreateComplaintAsync();
            var client = _factory.CreateClientAs("Admin");

            var response = await client.GetAsync($"/uploads/complaints/{fileName}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
