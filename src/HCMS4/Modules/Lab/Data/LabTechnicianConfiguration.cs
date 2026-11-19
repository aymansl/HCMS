using HCMS4.Modules.Lab.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Lab.Data
{
    public class LabTechnicianConfiguration : IEntityTypeConfiguration<LabTechnician>
    {
        public void Configure(EntityTypeBuilder<LabTechnician> builder)
        {
            builder.Property(t => t.UserId).IsRequired();
            builder.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // One technician profile per user.
            builder.HasIndex(t => t.UserId).IsUnique();

            builder.Property(t => t.Qualifications).HasMaxLength(500);
            builder.Property(t => t.ContactInfo).HasMaxLength(20);
            builder.Property(t => t.CreatedAt).HasDefaultValueSql(ClinicTime.SqlNow);
        }
    }
}
