using HCMS4.Shared.Audit.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Shared.Audit.Data
{
    public class SystemActivityLogConfiguration : IEntityTypeConfiguration<SystemActivityLog>
    {
        public void Configure(EntityTypeBuilder<SystemActivityLog> builder)
        {
            builder.Property(al => al.CreatedAt).HasDefaultValueSql(ClinicTime.SqlNow);
        }
    }
}
