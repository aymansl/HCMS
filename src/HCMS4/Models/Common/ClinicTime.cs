namespace HCMS4.Models.Common
{
    /// <summary>
    /// The system's single source of "now". Every date and time is stored and shown in the clinic's time zone
    /// (Damascus, UTC+3), not UTC — a project decision that overrides the original "store UTC" rule.
    /// </summary>
    public interface IClinicClock
    {
        /// <summary>Current date and time in Damascus.</summary>
        DateTime Now { get; }

        /// <summary>Current date in Damascus.</summary>
        DateTime Today { get; }
    }

    public sealed class ClinicClock : IClinicClock
    {
        private readonly TimeProvider _timeProvider;
        private readonly TimeZoneInfo _timeZone;

        public ClinicClock(TimeProvider timeProvider, TimeZoneInfo timeZone)
        {
            _timeProvider = timeProvider;
            _timeZone = timeZone;
        }

        public DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _timeZone);

        public DateTime Today => Now.Date;
    }

    /// <summary>Static access to the clinic clock for code that cannot take dependencies (models, views, attributes).</summary>
    public static class ClinicTime
    {
        /// <summary>IANA id of the clinic's time zone (works on Windows and Linux with .NET 6+).</summary>
        public const string TimeZoneId = "Asia/Damascus";

        /// <summary>SQL Server expression for "now in Damascus", used for column defaults.</summary>
        public const string SqlNow = "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)";

        public static TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

        public static IClinicClock Default { get; } = new ClinicClock(TimeProvider.System, TimeZone);

        public static DateTime Now => Default.Now;

        public static DateTime Today => Default.Today;
    }
}
