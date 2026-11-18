using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HCMS4.Models;

namespace HCMS4.Tests.Integration
{
    // Regression tests: "View attachment" downloaded PDFs instead of opening them, and
    // "Update Complaint" reset the status to the first option (Submitted), so updates looked ignored.
    public class ComplaintAdminFlowTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public ComplaintAdminFlowTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<int> CreateComplaintAsync(string? attachmentFileName = null, byte[]? content = null)
        {
            if (attachmentFileName != null)
            {
                Directory.CreateDirectory(_factory.UploadsPath);
                await File.WriteAllBytesAsync(Path.Combine(_factory.UploadsPath, attachmentFileName), content!);
            }

            int id = 0;
            await _factory.WithDbAsync(async db =>
            {
                var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                var complaint = new Complaint
                {
                    PatientId = patient.Id,
                    Title = "Service",
                    Description = "Long waiting time",
                    TrackingNumber = $"CMP-{Guid.NewGuid():N}",
                    AttachmentPath = attachmentFileName
                };
                db.Complaints.Add(complaint);
                await db.SaveChangesAsync();
                id = complaint.Id;
            });
            return id;
        }

        private static string? SelectedStatus(string html)
        {
            var select = Regex.Match(html, "<select[^>]*name=\"Status\"[^>]*>(.*?)</select>", RegexOptions.Singleline);
            var selected = Regex.Match(select.Groups[1].Value, "<option selected=\"selected\" value=\"(\\d+)\"");
            return selected.Success ? selected.Groups[1].Value : null;
        }

        [Fact]
        public async Task Download_Pdf_OpensInline()
        {
            var id = await CreateComplaintAsync($"{Guid.NewGuid():N}.pdf", Encoding.ASCII.GetBytes("%PDF-1.7 x"));
            var client = _factory.CreateClientAs("Admin");

            var response = await client.GetAsync($"/ComplaintAttachments/Download/{id}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("inline", response.Content.Headers.ContentDisposition?.DispositionType);
        }

        [Fact]
        public async Task Download_Docx_IsDownloaded()
        {
            var id = await CreateComplaintAsync($"{Guid.NewGuid():N}.docx", new byte[] { 0x50, 0x4B, 0x03, 0x04, 0 });
            var client = _factory.CreateClientAs("Admin");

            var response = await client.GetAsync($"/ComplaintAttachments/Download/{id}");

            Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        }

        [Fact]
        public async Task ComplaintDetails_SelectsCurrentStatus()
        {
            var id = await CreateComplaintAsync();
            var client = _factory.CreateClientAs("Admin");

            var html = await client.GetStringAsync($"/Admin/ComplaintDetails/{id}");

            // Opening the details moves a new complaint to "Under Review" (value 1).
            Assert.Equal(((int)ComplaintStatus.UnderReview).ToString(), SelectedStatus(html));
        }

        [Fact]
        public async Task UpdateComplaint_ToResolved_IsSavedAndConfirmed()
        {
            var id = await CreateComplaintAsync();
            var client = _factory.CreateClientAs("Admin");

            var post = await FormHelper.PostFormAsync(client, $"/Admin/ComplaintDetails/{id}", "/Admin/UpdateComplaintStatus",
                new Dictionary<string, string>
                {
                    ["id"] = id.ToString(),
                    ["Status"] = ((int)ComplaintStatus.Resolved).ToString(),
                    ["resolutionNotes"] = "Refund issued"
                });
            Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);

            var html = await client.GetStringAsync(post.Headers.Location!.OriginalString);

            Assert.Equal(((int)ComplaintStatus.Resolved).ToString(), SelectedStatus(html));
            Assert.Contains("Complaint updated successfully.", html);
            await _factory.WithDbAsync(async db =>
            {
                var complaint = await db.Complaints.FindAsync(id);
                Assert.Equal(ComplaintStatus.Resolved, complaint!.Status);
                Assert.NotNull(complaint.ResolvedAt);
                await Task.CompletedTask;
            });
        }
    }
}
