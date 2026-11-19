using HCMS4.Modules.Lab.Models;

namespace HCMS4.Modules.Lab.ViewModels
{
    /// <summary>One of the patient's lab requests with its sample-collection booking.</summary>
    public class PatientLabRequestRow
    {
        public LabRequest Request { get; set; } = null!;
        public LabAppointment? Appointment { get; set; }

        /// <summary>Whether a slot can be booked or changed (the sample is not collected yet).</summary>
        public bool CanBook { get; set; }
    }

    /// <summary>The patient's booking page for one request (UC-L05).</summary>
    public class BookLabAppointmentViewModel
    {
        public LabRequest Request { get; set; } = null!;

        /// <summary>The current booking; when set, choosing a slot changes it (alternative flow).</summary>
        public LabAppointment? Appointment { get; set; }

        public DateOnly SelectedDate { get; set; }
        public List<DateOnly> BookableDates { get; set; } = new();
        public List<DateTime> FreeSlots { get; set; } = new();
    }

    /// <summary>The lab's daily sample-collection schedule.</summary>
    public class LabScheduleViewModel
    {
        public DateOnly Date { get; set; }
        public List<LabAppointment> Appointments { get; set; } = new();
    }
}
