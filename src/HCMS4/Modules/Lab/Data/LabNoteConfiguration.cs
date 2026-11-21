using HCMS4.Modules.Lab.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Lab.Data
{
    public class LabNoteConfiguration : IEntityTypeConfiguration<LabNote>
    {
        public void Configure(EntityTypeBuilder<LabNote> builder)
        {
            builder.HasOne(n => n.LabRequest).WithMany(r => r.Notes).HasForeignKey(n => n.LabRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(n => n.Author).WithMany().HasForeignKey(n => n.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(n => n.Text).IsRequired().HasMaxLength(LabNote.TextMaxLength);
            builder.Property(n => n.CreatedBy).IsRequired().HasMaxLength(450);
            builder.Property(n => n.CreatedAt).HasDefaultValueSql(ClinicTime.SqlNow);

            builder.HasIndex(n => new { n.LabRequestId, n.CreatedAt });
        }
    }
}
