using HCMS4.Modules.Records.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Records.Data
{
    public class ClinicalNoteConfiguration : IEntityTypeConfiguration<ClinicalNote>
    {
        public void Configure(EntityTypeBuilder<ClinicalNote> builder)
        {
            builder.HasOne(cn => cn.Doctor)
                .WithMany()
                .HasForeignKey(cn => cn.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(cn => cn.Date).HasDefaultValueSql(ClinicTime.SqlNow);
        }
    }
}
