using System.Net;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Tests.Integration
{
    // Posts the doctor's prescription form exactly as the browser does.
    public class CreatePrescriptionTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public CreatePrescriptionTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(string DoctorUserId, int PatientId, int DrugId)> SeedAsync()
        {
            string doctorUserId = string.Empty;
            int patientId = 0, drugId = 0;
            await _factory.WithDbAsync(async db =>
            {
                var (user, _, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                var drug = await TestDbContextFactory.CreateTestDrugAsync(db);
                doctorUserId = user.Id;
                patientId = patient.Id;
                drugId = drug.Id;
            });
            return (doctorUserId, patientId, drugId);
        }

        [Theory]
        [InlineData("Take after meals")]
        [InlineData("")] // the Instructions field is optional on the form
        public async Task CreatePrescription_ValidForm_SavesAndRedirectsToPatientRecord(string instructions)
        {
            var (doctorUserId, patientId, drugId) = await SeedAsync();
            var client = _factory.CreateClientAs("Doctor", doctorUserId);

            var response = await FormHelper.PostFormAsync(client,
                $"/Doctor/CreatePrescription?patientId={patientId}", "/Doctor/CreatePrescription",
                new Dictionary<string, string>
                {
                    ["PatientId"] = patientId.ToString(),
                    ["AppointmentId"] = "",
                    ["Items[0].SelectedDrugId"] = drugId.ToString(),
                    ["Items[0].DrugName"] = "Amoxicillin",
                    ["Items[0].Price"] = "25.00",
                    ["Items[0].AvailableStock"] = "100",
                    ["Items[0].ExpiryStatus"] = "Valid",
                    ["Items[0].Dosage"] = "500mg",
                    ["Items[0].Frequency"] = "Twice daily",
                    ["Items[0].Duration"] = "5 days",
                    ["Items[0].Quantity"] = "2",
                    ["Items[0].Instructions"] = instructions,
                    ["Notes"] = "Bacterial infection",
                    ["DoctorSignature"] = "Dr. Test"
                });

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains($"/Doctor/PatientRecord?patientId={patientId}", response.Headers.Location?.OriginalString);
            await _factory.WithDbAsync(async db =>
            {
                Assert.True(await db.Prescriptions.AnyAsync(p => p.PatientId == patientId));
            });
        }
    }
}
