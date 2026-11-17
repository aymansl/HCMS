using System.ComponentModel.DataAnnotations;

namespace HCMS4.Models
{
    public class Survey
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Description { get; set; }

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        public SurveyTargetAudience TargetAudience { get; set; } = SurveyTargetAudience.AllPatients;

        [StringLength(300)]
        public string? TargetCriteria { get; set; }

        public SurveyStatus Status { get; set; } = SurveyStatus.Draft;

        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;

        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [DataType(DataType.DateTime)]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [DataType(DataType.DateTime)]
        public DateTime? SentAt { get; set; }

        public ApplicationUser CreatedByUser { get; set; } = null!;
        public ICollection<SurveyQuestion> Questions { get; set; } = new List<SurveyQuestion>();
        public ICollection<SurveyAssignment> Assignments { get; set; } = new List<SurveyAssignment>();
    }

    public enum SurveyTargetAudience
    {
        AllPatients,
        SpecificCategory,
        SpecificPatients
    }

    public enum SurveyStatus
    {
        Draft,
        Active,
        Closed
    }
}
