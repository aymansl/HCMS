using HCMS4.Modules.Billing.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Billing.Data
{
    public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            builder.HasOne(i => i.Patient)
                .WithMany(p => p.Invoices)
                .HasForeignKey(i => i.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(i => i.Appointment)
                .WithMany()
                .HasForeignKey(i => i.AppointmentId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(i => i.Prescription)
                .WithMany()
                .HasForeignKey(i => i.PrescriptionId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Property(i => i.InvoiceDate).HasDefaultValueSql(ClinicTime.SqlNow);

            builder.Property(i => i.ConsultationFee).HasPrecision(18, 2);
            builder.Property(i => i.MedicationTotal).HasPrecision(18, 2);
            builder.Property(i => i.TotalAmount).HasPrecision(18, 2);
            builder.Property(i => i.AmountPaid).HasPrecision(18, 2);
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Invoices_Amounts",
                    "[ConsultationFee] >= 0 AND [MedicationTotal] >= 0 AND [TotalAmount] >= 0");
                t.HasCheckConstraint("CK_Invoices_AmountPaid", "[AmountPaid] IS NULL OR [AmountPaid] >= 0");
            });
        }
    }
}
