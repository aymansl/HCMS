using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class SurveyAssignment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Survey")]
        public int SurveyId { get; set; }

        [Required]
        [ForeignKey("Patient")]
        public int PatientId { get; set; }

        public SurveyAssignmentStatus Status { get; set; } = SurveyAssignmentStatus.Pending;

        [DataType(DataType.DateTime)]
        public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

        [DataType(DataType.DateTime)]
        public DateTime? CompletedAt { get; set; }

        public Survey Survey { get; set; } = null!;
        public Patient Patient { get; set; } = null!;
        public ICollection<SurveyAnswer> Answers { get; set; } = new List<SurveyAnswer>();
    }

    public enum SurveyAssignmentStatus
    {
        Pending,
        Completed
    }
}
