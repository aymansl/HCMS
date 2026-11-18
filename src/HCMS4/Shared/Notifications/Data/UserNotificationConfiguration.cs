using HCMS4.Shared.Common;
using HCMS4.Shared.Notifications.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Shared.Notifications.Data
{
    public class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
    {
        public void Configure(EntityTypeBuilder<UserNotification> builder)
        {
            builder.HasOne(un => un.User)
                .WithMany()
                .HasForeignKey(un => un.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(un => new { un.UserId, un.IsRead });

            builder.Property(un => un.CreatedAt).HasDefaultValueSql(ClinicTime.SqlNow);
        }
    }
}
