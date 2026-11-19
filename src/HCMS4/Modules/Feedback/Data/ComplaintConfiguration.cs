using HCMS4.Modules.Feedback.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Feedback.Data
{
    public class ComplaintConfiguration : IEntityTypeConfiguration<Complaint>
    {
        public void Configure(EntityTypeBuilder<Complaint> builder)
        {
            builder.HasOne(c => c.Patient)
                .WithMany()
                .HasForeignKey(c => c.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(c => c.SubmissionDate).HasDefaultValueSql(ClinicTime.SqlNow);

            // "CMP-yyyyMMdd-XXXXXX" (19 characters); a bounded length allows the unique index.
            builder.Property(c => c.TrackingNumber).HasMaxLength(32);
            builder.HasIndex(c => c.TrackingNumber).IsUnique();
        }
    }
}
