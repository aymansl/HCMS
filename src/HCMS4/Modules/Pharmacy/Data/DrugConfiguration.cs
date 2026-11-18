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
        }
    }
}
