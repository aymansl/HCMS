using HCMS4.Modules.Feedback.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Feedback.Data
{
    public class SurveyQuestionOptionConfiguration : IEntityTypeConfiguration<SurveyQuestionOption>
    {
        public void Configure(EntityTypeBuilder<SurveyQuestionOption> builder)
        {
            builder.HasOne(o => o.SurveyQuestion)
                .WithMany(q => q.Options)
                .HasForeignKey(o => o.SurveyQuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
