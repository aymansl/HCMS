using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCMS4.Data.Migrations
{
    public partial class AddActivityLogMetadataColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "SystemActivityLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "SystemActivityLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: string.Empty);

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "SystemActivityLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                table: "SystemActivityLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Action",
                table: "SystemActivityLogs");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "SystemActivityLogs");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "SystemActivityLogs");

            migrationBuilder.DropColumn(
                name: "UserName",
                table: "SystemActivityLogs");
        }
    }
}
