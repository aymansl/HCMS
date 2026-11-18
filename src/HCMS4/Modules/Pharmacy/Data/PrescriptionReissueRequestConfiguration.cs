using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Pharmacy.Data
{
    public class PrescriptionReissueRequestConfiguration : IEntityTypeConfiguration<PrescriptionReissueRequest>
    {
        public void Configure(EntityTypeBuilder<PrescriptionReissueRequest> builder)
        {
            builder.HasOne(prr => prr.Patient)
                .WithMany()
                .HasForeignKey(prr => prr.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(prr => prr.Prescription)
                .WithMany()
                .HasForeignKey(prr => prr.PrescriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(prr => prr.Doctor)
                .WithMany()
                .HasForeignKey(prr => prr.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(prr => prr.RequestDate).HasDefaultValueSql(ClinicTime.SqlNow);
        }
    }
}
