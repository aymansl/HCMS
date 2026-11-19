using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCMS4.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inc2_L3_LabRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<int>(type: "int", nullable: false),
                    DoctorId = table.Column<int>(type: "int", nullable: false),
                    ClinicalNotes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)"),
                    StatusChangedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)"),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReturnNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabRequests", x => x.Id);
                    table.CheckConstraint("CK_LabRequests_Status", "[Status] IN ('Requested', 'Received', 'InProgress', 'ResultIssued', 'Cancelled', 'ReturnedToDoctor')");
                    table.ForeignKey(
                        name: "FK_LabRequests_Doctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Doctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabRequests_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabRequestItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabRequestId = table.Column<int>(type: "int", nullable: false),
                    LabTestId = table.Column<int>(type: "int", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabRequestItems", x => x.Id);
                    table.CheckConstraint("CK_LabRequestItems_Price", "[Price] >= 0");
                    table.ForeignKey(
                        name: "FK_LabRequestItems_LabRequests_LabRequestId",
                        column: x => x.LabRequestId,
                        principalTable: "LabRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabRequestItems_LabTests_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabRequestItems_LabRequestId_LabTestId",
                table: "LabRequestItems",
                columns: new[] { "LabRequestId", "LabTestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabRequestItems_LabTestId",
                table: "LabRequestItems",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_LabRequests_DoctorId",
                table: "LabRequests",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_LabRequests_PatientId",
                table: "LabRequests",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_LabRequests_Status_RequestedAt",
                table: "LabRequests",
                columns: new[] { "Status", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabRequestItems");

            migrationBuilder.DropTable(
                name: "LabRequests");
        }
    }
}
