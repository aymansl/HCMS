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
        }
    }
}
