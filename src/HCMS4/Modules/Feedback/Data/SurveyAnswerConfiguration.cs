using HCMS4.Modules.Feedback.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Feedback.Data
{
    public class SurveyAnswerConfiguration : IEntityTypeConfiguration<SurveyAnswer>
    {
        public void Configure(EntityTypeBuilder<SurveyAnswer> builder)
        {
            builder.HasOne(sa => sa.SurveyAssignment)
                .WithMany(a => a.Answers)
                .HasForeignKey(sa => sa.SurveyAssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(sa => sa.SurveyQuestion)
                .WithMany(q => q.Answers)
                .HasForeignKey(sa => sa.SurveyQuestionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
