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
        }
    }
}
