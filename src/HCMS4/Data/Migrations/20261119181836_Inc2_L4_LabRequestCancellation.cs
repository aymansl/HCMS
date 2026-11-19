using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCMS4.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inc2_L4_LabRequestCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "LabRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationNote",
                table: "LabRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationNoteAt",
                table: "LabRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationNoteBy",
                table: "LabRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "LabRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LabRequests_CancelReason",
                table: "LabRequests",
                sql: "[Status] <> 'Cancelled' OR [CancelReason] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LabRequests_CancelReason",
                table: "LabRequests");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "LabRequests");

            migrationBuilder.DropColumn(
                name: "CancellationNote",
                table: "LabRequests");

            migrationBuilder.DropColumn(
                name: "CancellationNoteAt",
                table: "LabRequests");

            migrationBuilder.DropColumn(
                name: "CancellationNoteBy",
                table: "LabRequests");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "LabRequests");
        }
    }
}
