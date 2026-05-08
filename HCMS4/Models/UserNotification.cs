using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class UserNotification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("User")]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        public NotificationType Type { get; set; } = NotificationType.General;

        [StringLength(500)]
        public string? LinkUrl { get; set; }

        [StringLength(100)]
        public string? RelatedEntityType { get; set; }

        public string? RelatedEntityId { get; set; }

        public bool IsRead { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ApplicationUser User { get; set; } = null!;
    }

    public enum NotificationType
    {
        General,
        AppointmentReminder,
        Complaint,
        PrescriptionReissue,
        DoctorReview,
        PrescriptionNote,
        Survey
    }
}
