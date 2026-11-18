using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Pharmacy.Data
{
    public class PharmacistPrescriptionNoteConfiguration : IEntityTypeConfiguration<PharmacistPrescriptionNote>
    {
        public void Configure(EntityTypeBuilder<PharmacistPrescriptionNote> builder)
        {
            builder.HasOne(ppn => ppn.Prescription)
                .WithMany()
                .HasForeignKey(ppn => ppn.PrescriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ppn => ppn.Pharmacist)
                .WithMany()
                .HasForeignKey(ppn => ppn.PharmacistId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(ppn => ppn.CreatedAt).HasDefaultValueSql(ClinicTime.SqlNow);
        }
    }
}
