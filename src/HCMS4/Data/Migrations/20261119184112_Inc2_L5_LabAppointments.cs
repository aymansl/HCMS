using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCMS4.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inc2_L5_LabAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabAppointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabRequestId = table.Column<int>(type: "int", nullable: false),
                    SlotStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BookedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabAppointments", x => x.Id);
                    table.CheckConstraint("CK_LabAppointments_SlotStart", "DATEPART(minute, [SlotStart]) % 15 = 0 AND DATEPART(second, [SlotStart]) = 0 AND DATEPART(nanosecond, [SlotStart]) = 0");
                    table.CheckConstraint("CK_LabAppointments_Status", "[Status] IN ('Booked', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_LabAppointments_LabRequests_LabRequestId",
                        column: x => x.LabRequestId,
                        principalTable: "LabRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabAppointments_LabRequestId_Booked",
                table: "LabAppointments",
                column: "LabRequestId",
                unique: true,
                filter: "[Status] = 'Booked'");

            migrationBuilder.CreateIndex(
                name: "IX_LabAppointments_SlotStart_Booked",
                table: "LabAppointments",
                column: "SlotStart",
                unique: true,
                filter: "[Status] = 'Booked'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabAppointments");
        }
    }
}
