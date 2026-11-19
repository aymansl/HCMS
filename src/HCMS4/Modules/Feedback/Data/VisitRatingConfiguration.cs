using HCMS4.Modules.Feedback.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Feedback.Data
{
    public class VisitRatingConfiguration : IEntityTypeConfiguration<VisitRating>
    {
        public void Configure(EntityTypeBuilder<VisitRating> builder)
        {
            builder.HasOne(vr => vr.Patient)
                .WithMany()
                .HasForeignKey(vr => vr.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(vr => vr.Doctor)
                .WithMany()
                .HasForeignKey(vr => vr.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(vr => vr.Appointment)
                .WithMany()
                .HasForeignKey(vr => vr.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(vr => vr.CreatedAt).HasDefaultValueSql(ClinicTime.SqlNow);

            // One rating per visit; ratings are 1 to 5 stars.
            builder.HasIndex(vr => vr.AppointmentId).IsUnique();
            builder.ToTable(t => t.HasCheckConstraint("CK_VisitRatings_Rating", "[Rating] BETWEEN 1 AND 5"));
        }
    }
}
