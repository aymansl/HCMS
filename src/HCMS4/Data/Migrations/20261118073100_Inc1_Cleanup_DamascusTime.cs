using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCMS4.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inc1_Cleanup_DamascusTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "VisitRatings",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "UserNotifications",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "SystemActivityLogs",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Surveys",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PrescriptionDate",
                table: "Prescriptions",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RequestDate",
                table: "PrescriptionReissueRequests",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "PharmacistPrescriptionNotes",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "MedicalArticles",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "InvoiceDate",
                table: "Invoices",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Drugs",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RequestDate",
                table: "DoctorReviewRequests",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmissionDate",
                table: "Complaints",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "ClinicalNotes",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true,
                oldDefaultValueSql: "GETUTCDATE()");

            // One-time data conversion: system timestamps written as UTC (UtcNow / GETUTCDATE) become Damascus time (+3h).
            // Dates entered by users (appointment time, birth date, expiry, leave and survey dates, payment date) are already local and are not touched.
            migrationBuilder.Sql("UPDATE [Appointments] SET [CreatedAt] = DATEADD(hour, 3, [CreatedAt]), [UpdatedAt] = DATEADD(hour, 3, [UpdatedAt]), [NoShowEvaluatedAt] = DATEADD(hour, 3, [NoShowEvaluatedAt]);");
            migrationBuilder.Sql("UPDATE [ClinicalNotes] SET [Date] = DATEADD(hour, 3, [Date]);");
            migrationBuilder.Sql("UPDATE [Complaints] SET [SubmissionDate] = DATEADD(hour, 3, [SubmissionDate]), [ResolvedAt] = DATEADD(hour, 3, [ResolvedAt]);");
            migrationBuilder.Sql("UPDATE [DailyReports] SET [GeneratedAt] = DATEADD(hour, 3, [GeneratedAt]);");
            migrationBuilder.Sql("UPDATE [DoctorLeaves] SET [CreatedAt] = DATEADD(hour, 3, [CreatedAt]);");
            migrationBuilder.Sql("UPDATE [LeaveRequests] SET [CreatedAt] = DATEADD(hour, 3, [CreatedAt]);");
            migrationBuilder.Sql("UPDATE [DoctorReviewRequests] SET [RequestDate] = DATEADD(hour, 3, [RequestDate]), [ReviewedAt] = DATEADD(hour, 3, [ReviewedAt]);");
            migrationBuilder.Sql("UPDATE [Drugs] SET [CreatedAt] = DATEADD(hour, 3, [CreatedAt]), [UpdatedAt] = DATEADD(hour, 3, [UpdatedAt]);");
            migrationBuilder.Sql("UPDATE [Invoices] SET [InvoiceDate] = DATEADD(hour, 3, [InvoiceDate]), [DispensedAt] = DATEADD(hour, 3, [DispensedAt]);");
            migrationBuilder.Sql("UPDATE [MedicalArticles] SET [CreatedAt] = DATEADD(hour, 3, [CreatedAt]), [UpdatedAt] = DATEADD(hour, 3, [UpdatedAt]), [PublishedAt] = DATEADD(hour, 3, [PublishedAt]);");
            migrationBuilder.Sql("UPDATE [Patients] SET [DisabledAt] = DATEADD(hour, 3, [DisabledAt]);");
            migrationBuilder.Sql("UPDATE [PharmacistPrescriptionNotes] SET [CreatedAt] = DATEADD(hour, 3, [CreatedAt]);");
            migrationBuilder.Sql("UPDATE [Pharmacists] SET [CreatedAt] = DATEADD(hour, 3, [CreatedAt]), [LastLoginAt] = DATEADD(hour, 3, [LastLoginAt]);");
            migrationBuilder.Sql("UPDATE [PrescriptionReissueRequests] SET [RequestDate] = DATEADD(hour, 3, [RequestDate]), [ReviewedAt] = DATEADD(hour, 3, [ReviewedAt]);");
            migrationBuilder.Sql("UPDATE [Prescriptions] SET [CreatedAt] = DATEADD(hour, 3, [CreatedAt]), [DispensedDate] = DATEADD(hour, 3, [DispensedDate]), [PrescriptionDate] = DATEADD(hour, 3, [PrescriptionDate]), [UpdatedAt] = DATEADD(hour, 3, [UpdatedAt]);");
            migrationBuilder.Sql("UPDATE [PurchaseRequests] SET [ApprovedDate] = DATEADD(hour, 3, [ApprovedDate]), [CreatedAt] = DATEADD(hour, 3, [CreatedAt]), [ReceivedDate] = DATEADD(hour, 3, [ReceivedDate]), [RequestDate] = DATEADD(hour, 3, [RequestDate]), [UpdatedAt] = DATEADD(hour, 3, [UpdatedAt]);");
            migrationBuilder.Sql("UPDATE [Suppliers] SET [CreatedAt] = DATEADD(hour, 3, [CreatedAt]);");
            migrationBuilder.Sql("UPDATE [SurveyAssignments] SET [AssignedAt] = DATEADD(hour, 3, [AssignedAt]), [CompletedAt] = DATEADD(hour, 3, [CompletedAt]);");
            migrationBuilder.Sql("UPDATE [Surveys] SET [CreatedAt] = DATEADD(hour, 3, [CreatedAt]), [SentAt] = DATEADD(hour, 3, [SentAt]), [UpdatedAt] = DATEADD(hour, 3, [UpdatedAt]);");
            migrationBuilder.Sql("UPDATE [SystemActivityLogs] SET [CreatedAt] = DATEADD(hour, 3, [CreatedAt]);");
            migrationBuilder.Sql("UPDATE [UserNotifications] SET [CreatedAt] = DATEADD(hour, 3, [CreatedAt]);");
            migrationBuilder.Sql("UPDATE [VisitRatings] SET [CreatedAt] = DATEADD(hour, 3, [CreatedAt]);");
            // Invoices.UpdatedAt was local when written by a patient payment (same value as PaymentDate); convert only the UTC rows.
            migrationBuilder.Sql("UPDATE [Invoices] SET [UpdatedAt] = DATEADD(hour, 3, [UpdatedAt]) WHERE [PaymentDate] IS NULL OR [UpdatedAt] <> [PaymentDate];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            // One-time data conversion: system timestamps written as UTC (UtcNow / GETUTCDATE) become Damascus time (-3h).
            // Dates entered by users (appointment time, birth date, expiry, leave and survey dates, payment date) are already local and are not touched.
            migrationBuilder.Sql("UPDATE [Appointments] SET [CreatedAt] = DATEADD(hour, -3, [CreatedAt]), [UpdatedAt] = DATEADD(hour, -3, [UpdatedAt]), [NoShowEvaluatedAt] = DATEADD(hour, -3, [NoShowEvaluatedAt]);");
            migrationBuilder.Sql("UPDATE [ClinicalNotes] SET [Date] = DATEADD(hour, -3, [Date]);");
            migrationBuilder.Sql("UPDATE [Complaints] SET [SubmissionDate] = DATEADD(hour, -3, [SubmissionDate]), [ResolvedAt] = DATEADD(hour, -3, [ResolvedAt]);");
            migrationBuilder.Sql("UPDATE [DailyReports] SET [GeneratedAt] = DATEADD(hour, -3, [GeneratedAt]);");
            migrationBuilder.Sql("UPDATE [DoctorLeaves] SET [CreatedAt] = DATEADD(hour, -3, [CreatedAt]);");
            migrationBuilder.Sql("UPDATE [LeaveRequests] SET [CreatedAt] = DATEADD(hour, -3, [CreatedAt]);");
            migrationBuilder.Sql("UPDATE [DoctorReviewRequests] SET [RequestDate] = DATEADD(hour, -3, [RequestDate]), [ReviewedAt] = DATEADD(hour, -3, [ReviewedAt]);");
            migrationBuilder.Sql("UPDATE [Drugs] SET [CreatedAt] = DATEADD(hour, -3, [CreatedAt]), [UpdatedAt] = DATEADD(hour, -3, [UpdatedAt]);");
            migrationBuilder.Sql("UPDATE [Invoices] SET [InvoiceDate] = DATEADD(hour, -3, [InvoiceDate]), [DispensedAt] = DATEADD(hour, -3, [DispensedAt]);");
            migrationBuilder.Sql("UPDATE [MedicalArticles] SET [CreatedAt] = DATEADD(hour, -3, [CreatedAt]), [UpdatedAt] = DATEADD(hour, -3, [UpdatedAt]), [PublishedAt] = DATEADD(hour, -3, [PublishedAt]);");
            migrationBuilder.Sql("UPDATE [Patients] SET [DisabledAt] = DATEADD(hour, -3, [DisabledAt]);");
            migrationBuilder.Sql("UPDATE [PharmacistPrescriptionNotes] SET [CreatedAt] = DATEADD(hour, -3, [CreatedAt]);");
            migrationBuilder.Sql("UPDATE [Pharmacists] SET [CreatedAt] = DATEADD(hour, -3, [CreatedAt]), [LastLoginAt] = DATEADD(hour, -3, [LastLoginAt]);");
            migrationBuilder.Sql("UPDATE [PrescriptionReissueRequests] SET [RequestDate] = DATEADD(hour, -3, [RequestDate]), [ReviewedAt] = DATEADD(hour, -3, [ReviewedAt]);");
            migrationBuilder.Sql("UPDATE [Prescriptions] SET [CreatedAt] = DATEADD(hour, -3, [CreatedAt]), [DispensedDate] = DATEADD(hour, -3, [DispensedDate]), [PrescriptionDate] = DATEADD(hour, -3, [PrescriptionDate]), [UpdatedAt] = DATEADD(hour, -3, [UpdatedAt]);");
            migrationBuilder.Sql("UPDATE [PurchaseRequests] SET [ApprovedDate] = DATEADD(hour, -3, [ApprovedDate]), [CreatedAt] = DATEADD(hour, -3, [CreatedAt]), [ReceivedDate] = DATEADD(hour, -3, [ReceivedDate]), [RequestDate] = DATEADD(hour, -3, [RequestDate]), [UpdatedAt] = DATEADD(hour, -3, [UpdatedAt]);");
            migrationBuilder.Sql("UPDATE [Suppliers] SET [CreatedAt] = DATEADD(hour, -3, [CreatedAt]);");
            migrationBuilder.Sql("UPDATE [SurveyAssignments] SET [AssignedAt] = DATEADD(hour, -3, [AssignedAt]), [CompletedAt] = DATEADD(hour, -3, [CompletedAt]);");
            migrationBuilder.Sql("UPDATE [Surveys] SET [CreatedAt] = DATEADD(hour, -3, [CreatedAt]), [SentAt] = DATEADD(hour, -3, [SentAt]), [UpdatedAt] = DATEADD(hour, -3, [UpdatedAt]);");
            migrationBuilder.Sql("UPDATE [SystemActivityLogs] SET [CreatedAt] = DATEADD(hour, -3, [CreatedAt]);");
            migrationBuilder.Sql("UPDATE [UserNotifications] SET [CreatedAt] = DATEADD(hour, -3, [CreatedAt]);");
            migrationBuilder.Sql("UPDATE [VisitRatings] SET [CreatedAt] = DATEADD(hour, -3, [CreatedAt]);");
            // Invoices.UpdatedAt was local when written by a patient payment (same value as PaymentDate); convert only the UTC rows.
            migrationBuilder.Sql("UPDATE [Invoices] SET [UpdatedAt] = DATEADD(hour, -3, [UpdatedAt]) WHERE [PaymentDate] IS NULL OR [UpdatedAt] <> [PaymentDate];");
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "VisitRatings",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "UserNotifications",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "SystemActivityLogs",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Surveys",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "PrescriptionDate",
                table: "Prescriptions",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RequestDate",
                table: "PrescriptionReissueRequests",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "PharmacistPrescriptionNotes",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "MedicalArticles",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "InvoiceDate",
                table: "Invoices",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Drugs",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "RequestDate",
                table: "DoctorReviewRequests",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SubmissionDate",
                table: "Complaints",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Date",
                table: "ClinicalNotes",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Appointments",
                type: "datetime2",
                nullable: true,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true,
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");
        }
    }
}
