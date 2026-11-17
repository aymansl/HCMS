using HCMS4.Models;
using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class SurveyEditorViewModel
    {
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

        public bool SendImmediately { get; set; }

        public List<int> SpecificPatientIds { get; set; } = new();
        public List<PatientSelectDto> AvailablePatients { get; set; } = new();
        public List<SurveyQuestionEditorViewModel> Questions { get; set; } = new() { new SurveyQuestionEditorViewModel() };
    }

    public class SurveyQuestionEditorViewModel
    {
        [Required]
        [StringLength(500)]
        public string QuestionText { get; set; } = string.Empty;

        public SurveyQuestionType QuestionType { get; set; } = SurveyQuestionType.OpenText;

        public bool IsRequired { get; set; }

        public string? OptionsText { get; set; }
    }
}
