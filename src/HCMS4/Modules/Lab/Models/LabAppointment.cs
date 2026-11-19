using HCMS4.Shared.Common;

namespace HCMS4.Modules.Lab.Models
{
    public enum LabAppointmentStatus
    {
        Booked,
        Cancelled
    }

    /// <summary>A patient's sample-collection slot for a lab request (UC-L05). One patient per slot.</summary>
    public class LabAppointment
    {
        public int Id { get; set; }

        public int LabRequestId { get; set; }
        public LabRequest LabRequest { get; set; } = null!;

        /// <summary>The start of the 15-minute slot (Damascus time).</summary>
        public DateTime SlotStart { get; set; }

        public LabAppointmentStatus Status { get; set; } = LabAppointmentStatus.Booked;

        public DateTime BookedAt { get; set; } = ClinicTime.Now;
        public DateTime UpdatedAt { get; set; } = ClinicTime.Now;
    }

    /// <summary>
    /// The lab's sample-collection hours (decided 2026-11-19): Saturday to Thursday, 08:00–14:00,
    /// 15-minute slots, bookable up to <see cref="BookingDays"/> days ahead.
    /// </summary>
    public static class LabSchedule
    {
        public static readonly TimeOnly Opens = new(8, 0);
        public static readonly TimeOnly Closes = new(14, 0);
        public static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(15);
        public const int BookingDays = 14;

        public static bool IsWorkingDay(DateOnly date) => date.DayOfWeek != DayOfWeek.Friday;

        /// <summary>All the slots of a working day (none on Friday).</summary>
        public static IEnumerable<DateTime> SlotsFor(DateOnly date)
        {
            if (!IsWorkingDay(date))
                yield break;

            for (var time = date.ToDateTime(Opens); time < date.ToDateTime(Closes); time += SlotLength)
                yield return time;
        }

        /// <summary>Whether <paramref name="slot"/> is the exact start of a slot in working hours.</summary>
        public static bool IsValidSlot(DateTime slot)
        {
            var date = DateOnly.FromDateTime(slot);
            var time = TimeOnly.FromDateTime(slot);
            return IsWorkingDay(date)
                && time >= Opens && time < Closes
                && slot.TimeOfDay.Ticks % SlotLength.Ticks == 0;
        }
    }
}
