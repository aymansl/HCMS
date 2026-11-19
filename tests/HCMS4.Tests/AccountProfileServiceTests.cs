using HCMS4.Data;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Feedback.Services;
using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCMS4.Tests
{
    // Service methods used by the account (registration/profile) and home pages (cleanup phase 5b).
    public class AccountProfileServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _db = TestDbContextFactory.CreateInMemoryContext();

        private PatientService Patients => new(_db, NullLogger<PatientService>.Instance);
        private StaffService Staff => new(_db, null!, ClinicTime.Default, NullLogger<StaffService>.Instance);

        public void Dispose() => _db.Dispose();

        [Fact]
        public async Task Patient_CreateForUser_ThenUpdateOwnDetails()
        {
            var (user, _) = await TestDbContextFactory.CreateTestPatientAsync(_db);
            var newUserId = Guid.NewGuid().ToString();
            _db.Users.Add(new HCMS4.Shared.Identity.Models.ApplicationUser { Id = newUserId, UserName = "new@test.local", FirstName = "N", LastName = "U" });
            await _db.SaveChangesAsync();

            var created = await Patients.CreateForUserAsync(newUserId, new DateTime(1990, 5, 1), "Diabetes");
            Assert.Equal("Diabetes", (await Patients.GetByUserIdAsync(newUserId))!.ChronicConditions);

            Assert.True(await Patients.UpdateOwnDetailsAsync(newUserId, new DateTime(1991, 1, 1), "Damascus", "0999"));
            var stored = await _db.Patients.AsNoTracking().SingleAsync(p => p.Id == created.Id);
            Assert.Equal("Damascus", stored.Address);
            Assert.Equal(new DateTime(1991, 1, 1), stored.DateOfBirth);

            Assert.False(await Patients.UpdateOwnDetailsAsync("no-such-user", null, null, null));
            Assert.Null(await Patients.GetByUserIdAsync("no-such-user"));
            Assert.NotNull(await Patients.GetByUserIdAsync(user.Id));
        }

        [Fact]
        public async Task Doctor_GetByUserIncludesSpecialization_AndUpdateOwnDetails()
        {
            var (user, doctor, specialization) = await TestDbContextFactory.CreateTestDoctorAsync(_db);

            Assert.Equal(specialization.Name, (await Staff.GetDoctorByUserIdAsync(user.Id))!.SpecializationName);

            Assert.True(await Staff.UpdateOwnDoctorDetailsAsync(user.Id, specialization.Id, "MD", "ext 12"));
            var stored = await _db.Doctors.AsNoTracking().SingleAsync(d => d.Id == doctor.Id);
            Assert.Equal("MD", stored.Qualifications);
            Assert.Equal("ext 12", stored.ContactInfo);
            Assert.False(await Staff.UpdateOwnDoctorDetailsAsync("no-such-user", null, null, null));
        }

        [Fact]
        public async Task Articles_GetPublished_ReturnsNewestPublishedOnly()
        {
            var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(_db);
            _db.MedicalArticles.AddRange(
                new MedicalArticle { DoctorId = doctor.Id, Title = "old", Content = "c", Status = ArticleStatus.Published, PublishedAt = new DateTime(2026, 1, 1) },
                new MedicalArticle { DoctorId = doctor.Id, Title = "new", Content = "c", Status = ArticleStatus.Published, PublishedAt = new DateTime(2026, 6, 1) },
                new MedicalArticle { DoctorId = doctor.Id, Title = "draft", Content = "c", Status = ArticleStatus.Draft });
            await _db.SaveChangesAsync();

            var articles = await new MedicalArticleService(_db, ClinicTime.Default).GetPublishedAsync(1);

            Assert.Equal("new", Assert.Single(articles).Title);
            Assert.NotNull(articles[0].Doctor);
        }
    }
}
