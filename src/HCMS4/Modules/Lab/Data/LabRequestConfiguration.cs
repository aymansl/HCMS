using HCMS4.Modules.Lab.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Lab.Data
{
    public class LabRequestConfiguration : IEntityTypeConfiguration<LabRequest>
    {
        public const int NoteMaxLength = 500;

        public void Configure(EntityTypeBuilder<LabRequest> builder)
        {
            // A request is part of the medical record: patients and doctors with requests are not deleted.
            builder.HasOne(r => r.Patient).WithMany().HasForeignKey(r => r.PatientId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(r => r.Doctor).WithMany().HasForeignKey(r => r.DoctorId).OnDelete(DeleteBehavior.Restrict);

            builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(r => r.ClinicalNotes).HasMaxLength(NoteMaxLength);
            builder.Property(r => r.ReturnNote).HasMaxLength(NoteMaxLength);
            builder.Property(r => r.StatusChangedBy).HasMaxLength(450);
            builder.Property(r => r.ReceivedBy).HasMaxLength(450);

            builder.Property(r => r.RequestedAt).HasDefaultValueSql(ClinicTime.SqlNow);
            builder.Property(r => r.StatusChangedAt).HasDefaultValueSql(ClinicTime.SqlNow);

            builder.HasIndex(r => new { r.Status, r.RequestedAt });

            var statuses = string.Join(", ", Enum.GetNames<LabRequestStatus>().Select(s => $"'{s}'"));
            builder.ToTable(t => t.HasCheckConstraint("CK_LabRequests_Status", $"[Status] IN ({statuses})"));
        }
    }

    public class LabRequestItemConfiguration : IEntityTypeConfiguration<LabRequestItem>
    {
        public void Configure(EntityTypeBuilder<LabRequestItem> builder)
        {
            builder.HasOne(i => i.LabRequest).WithMany(r => r.Items).HasForeignKey(i => i.LabRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(i => i.LabTest).WithMany().HasForeignKey(i => i.LabTestId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(i => new { i.LabRequestId, i.LabTestId }).IsUnique();
            builder.Property(i => i.Price).HasPrecision(18, 2);

            builder.ToTable(t => t.HasCheckConstraint("CK_LabRequestItems_Price", "[Price] >= 0"));
        }
    }
}
