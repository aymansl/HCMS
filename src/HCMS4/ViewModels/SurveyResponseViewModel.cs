using HCMS4.Models;

namespace HCMS4.ViewModels
{
    public class SurveyResponseViewModel
    {
        public int SurveyId { get; set; }
        public int SurveyAssignmentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public List<SurveyResponseQuestionViewModel> Questions { get; set; } = new();
    }

    public class SurveyResponseQuestionViewModel
    {
        public int SurveyQuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public SurveyQuestionType QuestionType { get; set; }
        public bool IsRequired { get; set; }
        public List<SurveyQuestionOption> Options { get; set; } = new();
        public string? AnswerText { get; set; }
        public int? NumericValue { get; set; }
        public bool? BooleanValue { get; set; }
    }
}
