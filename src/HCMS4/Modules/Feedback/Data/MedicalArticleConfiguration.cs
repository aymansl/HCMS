using HCMS4.Modules.Feedback.Models;
using HCMS4.Shared.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HCMS4.Modules.Feedback.Data
{
    public class MedicalArticleConfiguration : IEntityTypeConfiguration<MedicalArticle>
    {
        public void Configure(EntityTypeBuilder<MedicalArticle> builder)
        {
            builder.HasOne(ma => ma.Doctor)
                .WithMany(d => d.MedicalArticles)
                .HasForeignKey(ma => ma.DoctorId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(ma => ma.CreatedAt).HasDefaultValueSql(ClinicTime.SqlNow);
        }
    }
}
