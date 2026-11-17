using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class SurveyQuestion
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Survey")]
        public int SurveyId { get; set; }

        [Required]
        [StringLength(500)]
        public string QuestionText { get; set; } = string.Empty;

        public SurveyQuestionType QuestionType { get; set; } = SurveyQuestionType.OpenText;

        public bool IsRequired { get; set; }

        public int DisplayOrder { get; set; }

        public Survey Survey { get; set; } = null!;
        public ICollection<SurveyQuestionOption> Options { get; set; } = new List<SurveyQuestionOption>();
        public ICollection<SurveyAnswer> Answers { get; set; } = new List<SurveyAnswer>();
    }

    public enum SurveyQuestionType
    {
        MultipleChoice,
        Rating,
        YesNo,
        OpenText
    }
}
