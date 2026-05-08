using System.ComponentModel.DataAnnotations;
using HCMS4.Models;

namespace HCMS4.ViewModels
{
    public class LeaveRequestViewModel
    {
        public int Id { get; set; }

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

        [StringLength(500)]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Reason")]
        public string? Reason { get; set; }

        public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
        public DateTime? CreatedAt { get; set; }
    }

    public class DoctorLeaveViewModel
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public List<LeaveRequestViewModel> LeaveRequests { get; set; } = new();
        public List<DoctorLeaveItem> ApprovedLeaves { get; set; } = new();
    }

    public class DoctorLeaveItem
    {
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string LeaveType { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public LeaveStatus Status { get; set; }
    }

    public class RegisterDoctorLeaveViewModel
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;

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

        [StringLength(500)]
        [DataType(DataType.MultilineText)]
        [Display(Name = "Notes")]
        public string? Notes { get; set; }
    }
}
