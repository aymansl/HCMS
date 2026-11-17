using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class SurveyAnswer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("SurveyAssignment")]
        public int SurveyAssignmentId { get; set; }

        [Required]
        [ForeignKey("SurveyQuestion")]
        public int SurveyQuestionId { get; set; }

        [StringLength(2000)]
        public string? AnswerText { get; set; }

        public int? NumericValue { get; set; }

        public bool? BooleanValue { get; set; }

        public SurveyAssignment SurveyAssignment { get; set; } = null!;
        public SurveyQuestion SurveyQuestion { get; set; } = null!;
    }
}
