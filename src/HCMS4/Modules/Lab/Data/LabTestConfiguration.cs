using HCMS4.Modules.Lab.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Lab.Data
{
    public class LabTestConfiguration : IEntityTypeConfiguration<LabTest>
    {
        public void Configure(EntityTypeBuilder<LabTest> builder)
        {
            builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
            builder.HasIndex(t => t.Name).IsUnique();

            builder.Property(t => t.Unit).IsRequired().HasMaxLength(30);
            builder.Property(t => t.ReferenceMin).HasPrecision(18, 4);
            builder.Property(t => t.ReferenceMax).HasPrecision(18, 4);
            builder.Property(t => t.Price).HasPrecision(18, 2);

            builder.Property(t => t.CreatedAt).HasDefaultValueSql(ClinicTime.SqlNow);
            builder.Property(t => t.UpdatedAt).HasDefaultValueSql(ClinicTime.SqlNow);

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_LabTests_Price", "[Price] >= 0");
                t.HasCheckConstraint("CK_LabTests_ReferenceRange",
                    "[ReferenceMin] IS NULL OR [ReferenceMax] IS NULL OR [ReferenceMin] <= [ReferenceMax]");
            });
        }
    }
}
