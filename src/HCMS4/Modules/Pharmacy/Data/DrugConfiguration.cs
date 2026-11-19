using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Pharmacy.Data
{
    public class DrugConfiguration : IEntityTypeConfiguration<Drug>
    {
        public void Configure(EntityTypeBuilder<Drug> builder)
        {
            builder.Property(d => d.CreatedAt).HasDefaultValueSql(ClinicTime.SqlNow);

            builder.Property(d => d.Price).HasPrecision(18, 2);
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Drugs_Price", "[Price] >= 0");
                t.HasCheckConstraint("CK_Drugs_Quantity", "[Quantity] >= 0");
            });
        }
    }
}
