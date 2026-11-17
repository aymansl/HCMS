using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class SurveyQuestionOption
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("SurveyQuestion")]
        public int SurveyQuestionId { get; set; }

        [Required]
        [StringLength(200)]
        public string OptionText { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }

        public SurveyQuestion SurveyQuestion { get; set; } = null!;
    }
}
