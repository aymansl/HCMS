using HCMS4.Modules.Lab.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Lab.Data
{
    public class LabAppointmentConfiguration : IEntityTypeConfiguration<LabAppointment>
    {
        public void Configure(EntityTypeBuilder<LabAppointment> builder)
        {
            builder.HasOne(a => a.LabRequest).WithMany().HasForeignKey(a => a.LabRequestId).OnDelete(DeleteBehavior.Restrict);

            builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(a => a.BookedAt).HasDefaultValueSql(ClinicTime.SqlNow);
            builder.Property(a => a.UpdatedAt).HasDefaultValueSql(ClinicTime.SqlNow);

            // One patient per slot, and one active booking per request: the database refuses the
            // second of two simultaneous bookings (UC-L05 exception, TC-L5-02).
            var booked = $"[Status] = '{nameof(LabAppointmentStatus.Booked)}'";
            builder.HasIndex(a => a.SlotStart).IsUnique().HasFilter(booked).HasDatabaseName("IX_LabAppointments_SlotStart_Booked");
            builder.HasIndex(a => a.LabRequestId).IsUnique().HasFilter(booked).HasDatabaseName("IX_LabAppointments_LabRequestId_Booked");

            var statuses = string.Join(", ", Enum.GetNames<LabAppointmentStatus>().Select(s => $"'{s}'"));
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_LabAppointments_Status", $"[Status] IN ({statuses})");
                t.HasCheckConstraint("CK_LabAppointments_SlotStart",
                    "DATEPART(minute, [SlotStart]) % 15 = 0 AND DATEPART(second, [SlotStart]) = 0 AND DATEPART(nanosecond, [SlotStart]) = 0");
            });
        }
    }
}
