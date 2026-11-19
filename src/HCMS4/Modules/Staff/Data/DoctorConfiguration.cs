using HCMS4.Modules.Staff.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Staff.Data
{
    public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
    {
        public void Configure(EntityTypeBuilder<Doctor> builder)
        {
            builder.HasOne(d => d.Specialization)
                .WithMany(s => s.Doctors)
                .HasForeignKey(d => d.SpecializationId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Property(d => d.AverageRating).HasColumnType("decimal(3,2)");
            builder.ToTable(t => t.HasCheckConstraint("CK_Doctors_AverageRating", "[AverageRating] BETWEEN 0 AND 5"));
        }
    }
}
