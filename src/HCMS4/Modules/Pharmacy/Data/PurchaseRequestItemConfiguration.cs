using HCMS4.Modules.Pharmacy.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Pharmacy.Data
{
    public class PurchaseRequestItemConfiguration : IEntityTypeConfiguration<PurchaseRequestItem>
    {
        public void Configure(EntityTypeBuilder<PurchaseRequestItem> builder)
        {
            builder.Property(i => i.UnitPrice).HasPrecision(18, 2);
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_PurchaseRequestItems_Quantity", "[Quantity] > 0");
                t.HasCheckConstraint("CK_PurchaseRequestItems_UnitPrice", "[UnitPrice] >= 0");
            });
        }
    }
}
