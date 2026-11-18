using HCMS4.Modules.Staff.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Staff.Data
{
    public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
    {
        public void Configure(EntityTypeBuilder<LeaveRequest> builder)
        {
            builder.HasOne(lr => lr.Doctor)
                .WithMany()
                .HasForeignKey(lr => lr.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
