using HCMS4.Modules.Pharmacy.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Pharmacy.Data
{
    public class PurchaseRequestConfiguration : IEntityTypeConfiguration<PurchaseRequest>
    {
        public void Configure(EntityTypeBuilder<PurchaseRequest> builder)
        {
            builder.Property(pr => pr.TotalAmount).HasPrecision(18, 2);
            // "PO-yyyyMMdd-NNNN" (16 characters); a bounded length allows the unique index.
            builder.Property(pr => pr.RequestNumber).HasMaxLength(32);
            builder.HasIndex(pr => pr.RequestNumber).IsUnique();
            builder.ToTable(t => t.HasCheckConstraint("CK_PurchaseRequests_TotalAmount", "[TotalAmount] >= 0"));
        }
    }
}
