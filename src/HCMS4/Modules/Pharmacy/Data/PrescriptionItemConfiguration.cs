using HCMS4.Modules.Pharmacy.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Pharmacy.Data
{
    public class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
    {
        public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
        {
            // Restrict: a drug used by a prescription cannot be deleted.
            builder.HasOne(pi => pi.Drug)
                .WithMany(d => d.PrescriptionItems)
                .HasForeignKey(pi => pi.DrugId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(pi => pi.Prescription)
                .WithMany(p => p.PrescriptionItems)
                .HasForeignKey(pi => pi.PrescriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.ToTable(t => t.HasCheckConstraint("CK_PrescriptionItems_Quantity", "[Quantity] > 0"));
        }
    }
}
