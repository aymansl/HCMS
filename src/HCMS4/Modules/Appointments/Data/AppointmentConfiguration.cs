using HCMS4.Modules.Appointments.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Appointments.Data
{
    public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
    {
        public void Configure(EntityTypeBuilder<Appointment> builder)
        {
            builder.HasOne(a => a.Doctor)
                .WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(a => a.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(a => a.CreatedAt).HasDefaultValueSql(ClinicTime.SqlNow);

            builder.Property(a => a.ConsultationFee).HasPrecision(18, 2);
            builder.ToTable(t => t.HasCheckConstraint("CK_Appointments_ConsultationFee", "[ConsultationFee] >= 0"));
        }
    }
}
