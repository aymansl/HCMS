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
                new Specialization { Id = 1, Name = "Cardiology", ConsultationFee = 150, IsActive = true, Description = "Heart and blood vessels" },
                new Specialization { Id = 2, Name = "Dermatology", ConsultationFee = 120, IsActive = true, Description = "Skin, hair and nails" },
                new Specialization { Id = 3, Name = "Orthopedics", ConsultationFee = 130, IsActive = true, Description = "Bones, joints and muscles" },
                new Specialization { Id = 4, Name = "Neurology", ConsultationFee = 180, IsActive = true, Description = "Brain and nervous system" },
                new Specialization { Id = 5, Name = "Pediatrics", ConsultationFee = 110, IsActive = true, Description = "Children's health" },
                new Specialization { Id = 6, Name = "General Medicine", ConsultationFee = 100, IsActive = true, Description = "General health care" });
        }
    }
}
