using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCMS4.Data.Migrations
{
    public partial class EditedPharmacistTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Department",
                table: "Pharmacists");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "Pharmacists");

            migrationBuilder.DropColumn(
                name: "LicenseNumber",
                table: "Pharmacists");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "Pharmacists",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeId",
                table: "Pharmacists",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseNumber",
                table: "Pharmacists",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
