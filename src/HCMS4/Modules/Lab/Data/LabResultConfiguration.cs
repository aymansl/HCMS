using HCMS4.Modules.Lab.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Lab.Data
{
    public class LabResultConfiguration : IEntityTypeConfiguration<LabResult>
    {
        public void Configure(EntityTypeBuilder<LabResult> builder)
        {
            builder.HasIndex(r => r.LabRequestItemId).IsUnique();
            builder.Property(r => r.NumericValue).HasPrecision(18, 4);
            builder.Property(r => r.TextValue).HasMaxLength(200);
            builder.Property(r => r.EnteredBy).HasMaxLength(450);
            builder.Property(r => r.EnteredAt).HasDefaultValueSql(ClinicTime.SqlNow);

            builder.ToTable(t => t.HasCheckConstraint("CK_LabResults_Value",
                "[NumericValue] IS NOT NULL OR [TextValue] IS NOT NULL"));
        }
    }
}
