using HCMS4.Modules.Appointments.Services;
using HCMS4.Modules.Billing.Services;
using HCMS4.Modules.Feedback.Services;
using HCMS4.Modules.Pharmacy.Services;
using HCMS4.Modules.Records.Services;
using HCMS4.Modules.Staff.Services;
using HCMS4.Shared.Audit.Services;
using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Services;
using HCMS4.Shared.Reporting.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HCMS4.Tests.Integration
{
    // Every module registers its services through its own Add*Module() extension.
    public class ModuleRegistrationTests : IClassFixture<HcmsWebApplicationFactory>
    {
        private readonly HcmsWebApplicationFactory _factory;

        public ModuleRegistrationTests(HcmsWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData(typeof(IClinicClock))]
        [InlineData(typeof(INotificationService))]
        [InlineData(typeof(IActivityLogService))]
        [InlineData(typeof(IDailyReportService))]
        [InlineData(typeof(IAppointmentService))]
        [InlineData(typeof(INoShowRiskService))]
        [InlineData(typeof(IPatientService))]
        [InlineData(typeof(IDrugService))]
        [InlineData(typeof(IPrescriptionService))]
        [InlineData(typeof(IPharmacistService))]
        [InlineData(typeof(IInvoicePaymentService))]
        [InlineData(typeof(IComplaintAttachmentService))]
        [InlineData(typeof(ILeaveService))]
        [InlineData(typeof(IStaffService))]
        [InlineData(typeof(IMedicalRecordService))]
        [InlineData(typeof(IDailyVisitReportService))]
        [InlineData(typeof(IVisitRatingService))]
        [InlineData(typeof(ISupplierService))]
        [InlineData(typeof(IPurchaseRequestService))]
        [InlineData(typeof(IDispensingService))]
        [InlineData(typeof(IPrescriptionRequestService))]
        [InlineData(typeof(IInvoiceService))]
        [InlineData(typeof(IComplaintService))]
        [InlineData(typeof(ISurveyService))]
        [InlineData(typeof(IMedicalArticleService))]
        [InlineData(typeof(HCMS4.Modules.Portal.Services.IDashboardService))]
        [InlineData(typeof(HCMS4.Modules.Lab.Services.ILabTechnicianService))]
        [InlineData(typeof(HCMS4.Modules.Lab.Services.ILabTestService))]
        [InlineData(typeof(HCMS4.Modules.Lab.Services.ILabRequestService))]
        [InlineData(typeof(HCMS4.Modules.Lab.Services.ILabAppointmentService))]
        [InlineData(typeof(HCMS4.Modules.Lab.Services.ILabResultService))]
        [InlineData(typeof(HCMS4.Modules.Lab.Services.ILabTestUsage))]
        public void ModuleService_IsRegistered(Type serviceType)
        {
            using var scope = _factory.Services.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService(serviceType));
        }
    }
}
