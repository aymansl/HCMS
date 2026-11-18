using HCMS4.Modules.Staff.Models;
using System.Net;
namespace HCMS4.Tests.Integration
{
    public class DoctorProfileLeaveTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public DoctorProfileLeaveTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task DoctorProfile_ShowsRejectedRequestWithReason()
        {
            string doctorUserId = string.Empty;
            await _factory.WithDbAsync(async db =>
            {
                var (user, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                db.LeaveRequests.Add(new LeaveRequest
                {
                    DoctorId = doctor.Id,
                    StartDate = new DateTime(2026, 12, 1),
                    EndDate = new DateTime(2026, 12, 3),
                    LeaveType = LeaveType.Annual,
                    Status = LeaveStatus.Rejected,
                    RejectionReason = "No cover available"
                });
                await db.SaveChangesAsync();
                doctorUserId = user.Id;
            });
            var client = _factory.CreateClientAs("Doctor", doctorUserId);

            var response = await client.GetAsync("/Doctor/Profile");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("No cover available", html);
        }
    }
}
