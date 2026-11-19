using System.Net;
using System.Reflection;

namespace HCMS4.Tests.Integration
{
    // Regression tests for N4: CreateClinicalNote blocked on .Result (sync-over-async).
    public class DoctorClinicalNoteTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public DoctorClinicalNoteTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public void CreateClinicalNoteGet_IsAsync()
        {
            var action = typeof(HCMS4.Modules.Records.Controllers.DoctorRecordsController).GetMethods()
                .Single(m => m.Name == nameof(HCMS4.Modules.Records.Controllers.DoctorRecordsController.CreateClinicalNote)
                          && m.GetParameters().Single().ParameterType == typeof(int));

            Assert.True(typeof(Task).IsAssignableFrom(action.ReturnType));
        }

        [Fact]
        public async Task CreateClinicalNoteGet_AsDoctor_ShowsForm()
        {
            string doctorUserId = string.Empty;
            int patientId = 0;
            await _factory.WithDbAsync(async db =>
            {
                var (user, _, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                doctorUserId = user.Id;
                patientId = patient.Id;
            });
            var client = _factory.CreateClientAs("Doctor", doctorUserId);

            var response = await client.GetAsync($"/Doctor/CreateClinicalNote?patientId={patientId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
