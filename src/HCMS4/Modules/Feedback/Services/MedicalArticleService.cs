using HCMS4.Data;
using HCMS4.Modules.Feedback.Models;
using HCMS4.Shared.Common;
using HCMS4.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HCMS4.Modules.Feedback.Services
{
    /// <summary>Medical articles written by doctors who have the publishing permission.</summary>
    public interface IMedicalArticleService
    {
        Task<bool> CanPublishAsync(int doctorId);
        Task<List<MedicalArticle>> GetForDoctorAsync(int doctorId);
        /// <summary>The newest published articles of all doctors, with the author.</summary>
        Task<List<MedicalArticle>> GetPublishedAsync(int count);
        Task<ServiceResult<MedicalArticle>> CreateAsync(int doctorId, MedicalArticleEditorViewModel model, bool publishNow);
        Task<ServiceResult<MedicalArticle>> PublishAsync(int doctorId, int articleId);
    }

    public class MedicalArticleService : IMedicalArticleService
    {
        public const string NoPermissionMessage = "You do not have permission to publish articles.";

        private readonly ApplicationDbContext _context;
        private readonly IClinicClock _clock;

        public MedicalArticleService(ApplicationDbContext context, IClinicClock clock)
        {
            _context = context;
            _clock = clock;
        }

        public Task<bool> CanPublishAsync(int doctorId) =>
            _context.Doctors.AnyAsync(d => d.Id == doctorId && d.CanPublishArticles);

        public Task<List<MedicalArticle>> GetForDoctorAsync(int doctorId) =>
            _context.MedicalArticles.AsNoTracking()
                .Where(a => a.DoctorId == doctorId)
                .OrderByDescending(a => a.UpdatedAt)
                .ToListAsync();

        public Task<List<MedicalArticle>> GetPublishedAsync(int count) =>
            _context.MedicalArticles.AsNoTracking()
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Where(a => a.Status == ArticleStatus.Published)
                .OrderByDescending(a => a.PublishedAt ?? a.CreatedAt)
                .Take(count)
                .ToListAsync();

        public async Task<ServiceResult<MedicalArticle>> CreateAsync(int doctorId, MedicalArticleEditorViewModel model, bool publishNow)
        {
            if (!await CanPublishAsync(doctorId))
                return ServiceResult<MedicalArticle>.Fail(NoPermissionMessage);

            var now = _clock.Now;
            var article = new MedicalArticle
            {
                DoctorId = doctorId,
                Title = model.Title.Trim(),
                Summary = model.Summary?.Trim(),
                Content = model.Content.Trim(),
                Category = model.Category?.Trim(),
                Status = publishNow ? ArticleStatus.Published : ArticleStatus.Draft,
                CreatedAt = now,
                UpdatedAt = now,
                PublishedAt = publishNow ? now : null
            };

            _context.MedicalArticles.Add(article);
            await _context.SaveChangesAsync();
            return ServiceResult<MedicalArticle>.Ok(article, "Article saved successfully.");
        }

        public async Task<ServiceResult<MedicalArticle>> PublishAsync(int doctorId, int articleId)
        {
            if (!await CanPublishAsync(doctorId))
                return ServiceResult<MedicalArticle>.Fail(NoPermissionMessage);

            var article = await _context.MedicalArticles.FirstOrDefaultAsync(a => a.Id == articleId && a.DoctorId == doctorId);
            if (article == null)
                return ServiceResult<MedicalArticle>.Fail("Article not found.");

            if (article.Status == ArticleStatus.Published)
                return ServiceResult<MedicalArticle>.Fail("This article is already published.");

            article.Status = ArticleStatus.Published;
            article.PublishedAt = _clock.Now;
            article.UpdatedAt = _clock.Now;
            await _context.SaveChangesAsync();
            return ServiceResult<MedicalArticle>.Ok(article, "Article published successfully.");
        }
    }
}
