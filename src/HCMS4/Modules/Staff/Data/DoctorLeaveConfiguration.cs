using HCMS4.Modules.Staff.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Staff.Data
{
    public class DoctorLeaveConfiguration : IEntityTypeConfiguration<DoctorLeave>
    {
        public void Configure(EntityTypeBuilder<DoctorLeave> builder)
        {
            builder.HasOne(dl => dl.Doctor)
                .WithMany(d => d.DoctorLeaves)
                .HasForeignKey(dl => dl.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
