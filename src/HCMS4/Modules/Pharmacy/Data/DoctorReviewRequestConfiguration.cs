using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Pharmacy.Data
{
    public class DoctorReviewRequestConfiguration : IEntityTypeConfiguration<DoctorReviewRequest>
    {
        public void Configure(EntityTypeBuilder<DoctorReviewRequest> builder)
        {
            builder.HasOne(drr => drr.Pharmacist)
                .WithMany()
                .HasForeignKey(drr => drr.PharmacistId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(drr => drr.Prescription)
                .WithMany()
                .HasForeignKey(drr => drr.PrescriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(drr => drr.Doctor)
                .WithMany()
                .HasForeignKey(drr => drr.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(drr => drr.RequestDate).HasDefaultValueSql(ClinicTime.SqlNow);
        }
    }
}
