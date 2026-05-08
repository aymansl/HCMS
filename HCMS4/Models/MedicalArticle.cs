using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class MedicalArticle
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Doctor")]
        public int DoctorId { get; set; }

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

        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [DataType(DataType.DateTime)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [DataType(DataType.DateTime)]
        public DateTime? PublishedAt { get; set; }

        public Doctor Doctor { get; set; } = null!;
    }

    public enum ArticleStatus
    {
        Draft,
        Published
    }
}
