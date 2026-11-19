using HCMS4.Data;
using HCMS4.Modules.Billing.Models;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Common;
using HCMS4.Shared.Reporting.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HCMS4.Tests
{
    // Damascus defaults, money precision, check constraints, unique indexes.
    public class DatabaseConstraintsModelTests
    {
        // A SQL Server model built without opening a connection.
        private static readonly IModel Model = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=(local);Database=ModelOnly;Trusted_Connection=True")
                .Options).GetService<IDesignTimeModel>().Model; // check constraints are kept only in the design-time model

        private static IEntityType Entity<T>() => Model.FindEntityType(typeof(T))!;

        [Theory]
        [InlineData(typeof(Survey), nameof(Survey.UpdatedAt))]
        [InlineData(typeof(MedicalArticle), nameof(MedicalArticle.UpdatedAt))]
        [InlineData(typeof(SurveyAssignment), nameof(SurveyAssignment.AssignedAt))]
        public void LeftoverUtcDefaults_NowUseDamascusTime(Type entity, string property)
        {
            Assert.Equal(ClinicTime.SqlNow, Model.FindEntityType(entity)!.FindProperty(property)!.GetDefaultValueSql());
        }

        [Fact]
        public void NoColumn_DefaultsToUtc()
        {
            var defaults = Model.GetEntityTypes().SelectMany(e => e.GetProperties())
                .Select(p => p.GetDefaultValueSql()).Where(sql => sql != null);

            Assert.DoesNotContain(defaults, sql => sql!.Contains("utc", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void EveryMoneyColumn_HasExplicitPrecision()
        {
            var unconfigured = Model.GetEntityTypes()
                .SelectMany(e => e.GetProperties().Select(p => (Entity: e, Property: p)))
                .Where(x => (x.Property.ClrType == typeof(decimal) || x.Property.ClrType == typeof(decimal?))
                            && x.Property.GetPrecision() == null && x.Property.GetColumnType() == null)
                .Select(x => $"{x.Entity.ClrType.Name}.{x.Property.Name}")
                .ToList();

            Assert.Empty(unconfigured);
            Assert.Equal(18, Entity<Invoice>().FindProperty(nameof(Invoice.TotalAmount))!.GetPrecision());
            Assert.Equal(2, Entity<DailyReport>().FindProperty(nameof(DailyReport.TotalAmount))!.GetScale());
        }

        [Theory]
        [InlineData(typeof(VisitRating), "CK_VisitRatings_Rating")]
        [InlineData(typeof(Drug), "CK_Drugs_Price")]
        [InlineData(typeof(Drug), "CK_Drugs_Quantity")]
        [InlineData(typeof(Invoice), "CK_Invoices_Amounts")]
        [InlineData(typeof(Invoice), "CK_Invoices_AmountPaid")]
        [InlineData(typeof(PrescriptionItem), "CK_PrescriptionItems_Quantity")]
        [InlineData(typeof(PurchaseRequest), "CK_PurchaseRequests_TotalAmount")]
        [InlineData(typeof(PurchaseRequestItem), "CK_PurchaseRequestItems_Quantity")]
        [InlineData(typeof(PurchaseRequestItem), "CK_PurchaseRequestItems_UnitPrice")]
        [InlineData(typeof(Specialization), "CK_Specializations_ConsultationFee")]
        [InlineData(typeof(Doctor), "CK_Doctors_AverageRating")]
        [InlineData(typeof(HCMS4.Modules.Appointments.Models.Appointment), "CK_Appointments_ConsultationFee")]
        [InlineData(typeof(HCMS4.Modules.Lab.Models.LabTest), "CK_LabTests_Price")]
        [InlineData(typeof(HCMS4.Modules.Lab.Models.LabTest), "CK_LabTests_ReferenceRange")]
        [InlineData(typeof(HCMS4.Modules.Lab.Models.LabRequest), "CK_LabRequests_Status")]
        [InlineData(typeof(HCMS4.Modules.Lab.Models.LabRequest), "CK_LabRequests_CancelReason")]
        [InlineData(typeof(HCMS4.Modules.Lab.Models.LabAppointment), "CK_LabAppointments_Status")]
        [InlineData(typeof(HCMS4.Modules.Lab.Models.LabAppointment), "CK_LabAppointments_SlotStart")]
        [InlineData(typeof(HCMS4.Modules.Lab.Models.LabTest), "CK_LabTests_CriticalLimits")]
        [InlineData(typeof(HCMS4.Modules.Lab.Models.LabResult), "CK_LabResults_Value")]
        [InlineData(typeof(HCMS4.Modules.Lab.Models.LabRequestItem), "CK_LabRequestItems_Price")]
        public void CheckConstraint_IsDefined(Type entity, string name)
        {
            Assert.Contains(Model.FindEntityType(entity)!.GetCheckConstraints(), c => c.Name == name);
        }

        [Theory]
        [InlineData(nameof(HCMS4.Modules.Lab.Models.LabAppointment.SlotStart), "IX_LabAppointments_SlotStart_Booked")]
        [InlineData(nameof(HCMS4.Modules.Lab.Models.LabAppointment.LabRequestId), "IX_LabAppointments_LabRequestId_Booked")]
        public void LabAppointment_ActiveBookings_AreUnique(string property, string indexName)
        {
            var index = Assert.Single(Model.FindEntityType(typeof(HCMS4.Modules.Lab.Models.LabAppointment))!.GetIndexes(),
                i => i.Properties.Single().Name == property);
            Assert.True(index.IsUnique);
            Assert.Equal(indexName, index.GetDatabaseName());
            Assert.Equal("[Status] = 'Booked'", index.GetFilter());
        }

        [Theory]
        [InlineData(typeof(VisitRating), nameof(VisitRating.AppointmentId))]
        [InlineData(typeof(Complaint), nameof(Complaint.TrackingNumber))]
        [InlineData(typeof(PurchaseRequest), nameof(PurchaseRequest.RequestNumber))]
        [InlineData(typeof(Specialization), nameof(Specialization.Name))]
        public void UniqueIndex_IsDefined(Type entity, string property)
        {
            Assert.Contains(Model.FindEntityType(entity)!.GetIndexes(),
                i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == property);
        }

        [Fact]
        public void UniqueTextColumns_HaveABoundedLength()
        {
            Assert.Equal(32, Entity<Complaint>().FindProperty(nameof(Complaint.TrackingNumber))!.GetMaxLength());
            Assert.Equal(32, Entity<PurchaseRequest>().FindProperty(nameof(PurchaseRequest.RequestNumber))!.GetMaxLength());
        }
    }
}
