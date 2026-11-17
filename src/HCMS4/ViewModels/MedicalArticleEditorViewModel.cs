using HCMS4.Models;
using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class MedicalArticleEditorViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Summary { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Category { get; set; }

        public ArticleStatus Status { get; set; } = ArticleStatus.Draft;
    }
}
