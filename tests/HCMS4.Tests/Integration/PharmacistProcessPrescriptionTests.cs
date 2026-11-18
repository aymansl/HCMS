using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HCMS4.Models;

namespace HCMS4.Tests.Integration
{
    // Regression tests: the dispensing page called /Pharmacist/HoldPrescription and /Pharmacist/GetAlternatives,
    // which did not exist.
    public class PharmacistProcessPrescriptionTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public PharmacistProcessPrescriptionTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<int> CreatePendingPrescriptionAsync()
        {
            int id = 0;
            await _factory.WithDbAsync(async db =>
            {
                var (_, doctor, _) = await TestDbContextFactory.CreateTestDoctorAsync(db);
                var (_, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                var drug = await TestDbContextFactory.CreateTestDrugAsync(db);
                var prescription = new Prescription
                {
                    PatientId = patient.Id,
                    DoctorId = doctor.Id,
                    Notes = "Test",
                    Status = PrescriptionStatus.Pending,
                    PrescriptionItems = new List<PrescriptionItem>
                    {
                        new() { DrugId = drug.Id, DrugName = drug.Name, Dosage = "1", Duration = "5d", Frequency = "daily", Quantity = 1, Instructions = "-" }
                    }
                };
                db.Prescriptions.Add(prescription);
                await db.SaveChangesAsync();
                id = prescription.Id;
            });
            return id;
        }

        [Fact]
        public async Task HoldPrescription_Pending_ReturnsSuccess()
        {
            var id = await CreatePendingPrescriptionAsync();
            var client = _factory.CreateClientAs("Pharmacist");

            var response = await FormHelper.PostFormAsync(client, $"/Pharmacist/ProcessPrescription/{id}", "/Pharmacist/HoldPrescription",
                new Dictionary<string, string> { ["id"] = id.ToString() });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.GetProperty("success").GetBoolean());
        }

        [Fact]
        public async Task ProcessPrescriptionPage_DoesNotCallMissingAlternativesEndpoint()
        {
            var id = await CreatePendingPrescriptionAsync();
            var client = _factory.CreateClientAs("Pharmacist");

            var html = await client.GetStringAsync($"/Pharmacist/ProcessPrescription/{id}");

            Assert.DoesNotContain("GetAlternatives", html);
            Assert.Contains("/Pharmacist/HoldPrescription", html);
        }
    }
}
