using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Records.Models;
using System.Net;
namespace HCMS4.Tests.Integration
{
    // Regression tests: the patient's "View" invoice button failed because Views/Patient/InvoiceDetails.cshtml did not exist.
    public class PatientInvoiceDetailsTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public PatientInvoiceDetailsTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<(string OwnerUserId, string OtherUserId, int InvoiceId)> SeedAsync()
        {
            string owner = string.Empty, other = string.Empty;
            int invoiceId = 0;
            await _factory.WithDbAsync(async db =>
            {
                var (ownerUser, patient) = await TestDbContextFactory.CreateTestPatientAsync(db);
                var (otherUser, _) = await TestDbContextFactory.CreateTestPatientAsync(db);
                var invoice = new Invoice
                {
                    PatientId = patient.Id,
                    ConsultationFee = 100,
                    TotalAmount = 100,
                    AmountPaid = 40,
                    PaymentMethod = PaymentMethod.Cash,
                    PaymentDate = new DateTime(2026, 11, 18, 10, 0, 0)
                };
                db.Invoices.Add(invoice);
                await db.SaveChangesAsync();
                owner = ownerUser.Id;
                other = otherUser.Id;
                invoiceId = invoice.Id;
            });
            return (owner, other, invoiceId);
        }

        [Fact]
        public async Task InvoiceDetails_Owner_ShowsInvoice()
        {
            var (owner, _, invoiceId) = await SeedAsync();
            var client = _factory.CreateClientAs("Patient", owner);

            var response = await client.GetAsync($"/Patient/InvoiceDetails/{invoiceId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains($"INV-{invoiceId:D6}", html);
            Assert.Contains("المتبقي", html);
        }

        [Fact]
        public async Task InvoiceDetails_OtherPatient_IsRedirected()
        {
            var (_, other, invoiceId) = await SeedAsync();
            var client = _factory.CreateClientAs("Patient", other);

            var response = await client.GetAsync($"/Patient/InvoiceDetails/{invoiceId}");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Patient/InvoiceHistory", response.Headers.Location?.OriginalString);
        }
    }
}
