using HCMS4.Modules.Feedback.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Feedback.Data
{
    public class SurveyAssignmentConfiguration : IEntityTypeConfiguration<SurveyAssignment>
    {
        public void Configure(EntityTypeBuilder<SurveyAssignment> builder)
        {
            builder.HasOne(sa => sa.Survey)
                .WithMany(s => s.Assignments)
                .HasForeignKey(sa => sa.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(sa => sa.Patient)
                .WithMany()
                .HasForeignKey(sa => sa.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            // A patient is assigned to a survey at most once.
            builder.HasIndex(sa => new { sa.SurveyId, sa.PatientId }).IsUnique();
        }
    }
}
