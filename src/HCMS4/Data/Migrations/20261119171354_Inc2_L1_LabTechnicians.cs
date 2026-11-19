using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCMS4.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inc2_L1_LabTechnicians : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabTechnicians",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Qualifications = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactInfo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeactivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTechnicians", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabTechnicians_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabTechnicians_UserId",
                table: "LabTechnicians",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabTechnicians");
        }
    }
}
