using HCMS4.Modules.Records.Models;
using HCMS4.Modules.Staff.Models;
using HCMS4.Shared.Common;
using HCMS4.Shared.Workflow;

namespace HCMS4.Modules.Lab.Models
{
    /// <summary>The status names agreed for Increment 2 (stored as text).</summary>
    public enum LabRequestStatus
    {
        Requested,
        Received,
        InProgress,
        ResultIssued,
        Cancelled,
        ReturnedToDoctor
    }

    /// <summary>A doctor's lab test request for a patient (UC-L03).</summary>
    public class LabRequest : IHasWorkflowStatus<LabRequestStatus>
    {
        public int Id { get; set; }

        public int PatientId { get; set; }
        public Patient Patient { get; set; } = null!;

        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; } = null!;

        /// <summary>The doctor's clinical information for the lab.</summary>
        public string? ClinicalNotes { get; set; }

        public LabRequestStatus Status { get; set; } = LabRequestStatus.Requested;
        public DateTime StatusChangedAt { get; set; } = ClinicTime.Now;
        public string? StatusChangedBy { get; set; }

        /// <summary>When the doctor sent (or last resent) the request.</summary>
        public DateTime RequestedAt { get; set; } = ClinicTime.Now;

        public DateTime? ReceivedAt { get; set; }
        /// <summary>The user id of the technician who confirmed receipt.</summary>
        public string? ReceivedBy { get; set; }

        /// <summary>The last time the lab returned the request to the doctor, and why.</summary>
        public DateTime? ReturnedAt { get; set; }
        public string? ReturnNote { get; set; }

        /// <summary>When and why the doctor cancelled the request (UC-L04); the doctor is in StatusChangedBy.</summary>
        public DateTime? CancelledAt { get; set; }
        public string? CancelReason { get; set; }

        /// <summary>
        /// The technician's one note on a cancelled request, e.g. "sample taken" (UC-L04 alternative flow).
        /// Kept apart from the general lab notes (UC-L07), which a cancelled request does not accept.
        /// </summary>
        public string? CancellationNote { get; set; }
        public DateTime? CancellationNoteAt { get; set; }
        public string? CancellationNoteBy { get; set; }

        public ICollection<LabRequestItem> Items { get; set; } = new List<LabRequestItem>();
    }

    /// <summary>One catalogue test in a request, with its price when the request was made.</summary>
    public class LabRequestItem
    {
        public int Id { get; set; }

        public int LabRequestId { get; set; }
        public LabRequest LabRequest { get; set; } = null!;

        public int LabTestId { get; set; }
        public LabTest LabTest { get; set; } = null!;

        public decimal Price { get; set; }
    }
}
