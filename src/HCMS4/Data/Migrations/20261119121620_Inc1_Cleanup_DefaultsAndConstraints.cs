using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCMS4.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inc1_Cleanup_DefaultsAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VisitRatings_AppointmentId",
                table: "VisitRatings");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Surveys",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AssignedAt",
                table: "SurveyAssignments",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "RequestNumber",
                table: "PurchaseRequests",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "MedicalArticles",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "TrackingNumber",
                table: "Complaints",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_VisitRatings_AppointmentId",
                table: "VisitRatings",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_VisitRatings_Rating",
                table: "VisitRatings",
                sql: "[Rating] BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_Specializations_Name",
                table: "Specializations",
                column: "Name",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Specializations_ConsultationFee",
                table: "Specializations",
                sql: "[ConsultationFee] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_RequestNumber",
                table: "PurchaseRequests",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseRequests_TotalAmount",
                table: "PurchaseRequests",
                sql: "[TotalAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseRequestItems_Quantity",
                table: "PurchaseRequestItems",
                sql: "[Quantity] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PurchaseRequestItems_UnitPrice",
                table: "PurchaseRequestItems",
                sql: "[UnitPrice] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PrescriptionItems_Quantity",
                table: "PrescriptionItems",
                sql: "[Quantity] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_AmountPaid",
                table: "Invoices",
                sql: "[AmountPaid] IS NULL OR [AmountPaid] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_Amounts",
                table: "Invoices",
                sql: "[ConsultationFee] >= 0 AND [MedicationTotal] >= 0 AND [TotalAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Drugs_Price",
                table: "Drugs",
                sql: "[Price] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Drugs_Quantity",
                table: "Drugs",
                sql: "[Quantity] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Doctors_AverageRating",
                table: "Doctors",
                sql: "[AverageRating] BETWEEN 0 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_TrackingNumber",
                table: "Complaints",
                column: "TrackingNumber",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_ConsultationFee",
                table: "Appointments",
                sql: "[ConsultationFee] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VisitRatings_AppointmentId",
                table: "VisitRatings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VisitRatings_Rating",
                table: "VisitRatings");

            migrationBuilder.DropIndex(
                name: "IX_Specializations_Name",
                table: "Specializations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Specializations_ConsultationFee",
                table: "Specializations");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequests_RequestNumber",
                table: "PurchaseRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseRequests_TotalAmount",
                table: "PurchaseRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseRequestItems_Quantity",
                table: "PurchaseRequestItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PurchaseRequestItems_UnitPrice",
                table: "PurchaseRequestItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PrescriptionItems_Quantity",
                table: "PrescriptionItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_AmountPaid",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_Amounts",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Drugs_Price",
                table: "Drugs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Drugs_Quantity",
                table: "Drugs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Doctors_AverageRating",
                table: "Doctors");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_TrackingNumber",
                table: "Complaints");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_ConsultationFee",
                table: "Appointments");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Surveys",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AssignedAt",
                table: "SurveyAssignments",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<string>(
                name: "RequestNumber",
                table: "PurchaseRequests",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "MedicalArticles",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)");

            migrationBuilder.AlterColumn<string>(
                name: "TrackingNumber",
                table: "Complaints",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.CreateIndex(
                name: "IX_VisitRatings_AppointmentId",
                table: "VisitRatings",
                column: "AppointmentId");
        }
    }
}
