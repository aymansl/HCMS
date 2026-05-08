using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCMS4.Models
{
    public class LeaveRequest
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Doctor is required")]
        [ForeignKey("Doctor")]
        [Display(Name = "Doctor")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Doctor is required")]
        [Display(Name = "Doctor")]
        public Doctor Doctor { get; set; } = null!;

        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Required(ErrorMessage = "Leave type is required")]
        [Display(Name = "Leave Type")]
        public LeaveType LeaveType { get; set; }

        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Reason")]
        public string? Reason { get; set; }

        [Display(Name = "Status")]
        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

        [DataType(DataType.DateTime)]
        [Display(Name = "Created At")]
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
