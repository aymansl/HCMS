using HCMS4.Modules.Staff.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Staff.Data
{
    public class SpecializationConfiguration : IEntityTypeConfiguration<Specialization>
    {
        public void Configure(EntityTypeBuilder<Specialization> builder)
        {
            builder.Property(s => s.ConsultationFee).HasPrecision(18, 2);
            builder.HasIndex(s => s.Name).IsUnique();
            builder.ToTable(t => t.HasCheckConstraint("CK_Specializations_ConsultationFee", "[ConsultationFee] >= 0"));

            builder.HasData(
                new Specialization { Id = 1, Name = "Cardiology", ConsultationFee = 150, IsActive = true, Description = "قلبية" },
                new Specialization { Id = 2, Name = "Dermatology", ConsultationFee = 120, IsActive = true, Description = "جلدية" },
                new Specialization { Id = 3, Name = "Orthopedics", ConsultationFee = 130, IsActive = true, Description = "عظمية" },
                new Specialization { Id = 4, Name = "Neurology", ConsultationFee = 180, IsActive = true, Description = "عصبية" },
                new Specialization { Id = 5, Name = "Pediatrics", ConsultationFee = 110, IsActive = true, Description = "أطفال" },
                new Specialization { Id = 6, Name = "General Medicine", ConsultationFee = 100, IsActive = true, Description = "طب عام" });
        }
    }
}
