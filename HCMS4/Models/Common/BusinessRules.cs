namespace HCMS4.Models.Common
{
    /// <summary>
    /// Centralized business rule constants to avoid magic numbers throughout the codebase.
    /// </summary>
    public static class BusinessRules
    {
        /// <summary>Hours before appointment time within which cancellation is not allowed.</summary>
        public const int AppointmentCancellationWindowHours = 24;

        /// <summary>Minutes buffer before/after an appointment time to prevent overlapping slots.</summary>
        public const int AppointmentSlotBufferMinutes = 30;

        /// <summary>Default number of days to look ahead for upcoming appointments.</summary>
        public const int UpcomingAppointmentsWindowDays = 30;

        /// <summary>Maximum number of upcoming appointments to return.</summary>
        public const int MaxUpcomingAppointments = 50;

        /// <summary>Days before expiry to start showing warnings for drugs.</summary>
        public const int DrugExpiryWarningDays = 30;

        /// <summary>Days before expiry to show a "expiring shortly" status.</summary>
        public const int DrugExpiryShortWarningDays = 60;

        /// <summary>Quantity threshold below which a drug is considered low stock.</summary>
        public const int LowStockThreshold = 10;

        /// <summary>Score at or above which an appointment is considered high no-show risk.</summary>
        public const double HighRiskThreshold = 0.7;

        /// <summary>Score at or above which an appointment is considered medium no-show risk.</summary>
        public const double MediumRiskThreshold = 0.4;

        /// <summary>Number of days after which a prescription cannot be re-issued.</summary>
        public const int PrescriptionReissueValidityDays = 30;

        /// <summary>Maximum attachment size for complaint uploads in bytes.</summary>
        public const int ComplaintAttachmentMaxBytes = 5 * 1024 * 1024;

        /// <summary>Default page size for paginated results.</summary>
        public const int DefaultPageSize = 20;

        /// <summary>Maximum page size to prevent loading too many records.</summary>
        public const int MaxPageSize = 100;
    }
}
