using HCMS4.Shared.Reporting.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Shared.Reporting.Data
{
    public class DailyReportConfiguration : IEntityTypeConfiguration<DailyReport>
    {
        public void Configure(EntityTypeBuilder<DailyReport> builder)
        {
            builder.Property(r => r.TotalAmount).HasPrecision(18, 2);
        }
    }
}
