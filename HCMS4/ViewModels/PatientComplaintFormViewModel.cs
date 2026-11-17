using HCMS4.Models;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace HCMS4.ViewModels
{
    public class PatientComplaintFormViewModel
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public ComplaintType Type { get; set; }

        [Required]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime? AssociatedVisitDate { get; set; }

        public IFormFile? Attachment { get; set; }
    }
}
