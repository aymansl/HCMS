using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCMS4.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inc3_L10_LabResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CriticalMax",
                table: "LabTests",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CriticalMin",
                table: "LabTests",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CriticalAcknowledgedAt",
                table: "LabRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CriticalAcknowledgedBy",
                table: "LabRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasCriticalResult",
                table: "LabRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResultIssuedAt",
                table: "LabRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultIssuedBy",
                table: "LabRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LabResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LabRequestItemId = table.Column<int>(type: "int", nullable: false),
                    NumericValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    TextValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsAbnormal = table.Column<bool>(type: "bit", nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    EnteredAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Syria Standard Time' AS datetime2)"),
                    EnteredBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResults", x => x.Id);
                    table.CheckConstraint("CK_LabResults_Value", "[NumericValue] IS NOT NULL OR [TextValue] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_LabResults_LabRequestItems_LabRequestItemId",
                        column: x => x.LabRequestItemId,
                        principalTable: "LabRequestItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LabTests_CriticalLimits",
                table: "LabTests",
                sql: "([CriticalMin] IS NULL OR [ReferenceMin] IS NULL OR [CriticalMin] <= [ReferenceMin]) AND ([CriticalMax] IS NULL OR [ReferenceMax] IS NULL OR [CriticalMax] >= [ReferenceMax]) AND ([CriticalMin] IS NULL OR [CriticalMax] IS NULL OR [CriticalMin] < [CriticalMax])");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_LabRequestItemId",
                table: "LabResults",
                column: "LabRequestItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabResults");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LabTests_CriticalLimits",
                table: "LabTests");

            migrationBuilder.DropColumn(
                name: "CriticalMax",
                table: "LabTests");

            migrationBuilder.DropColumn(
                name: "CriticalMin",
                table: "LabTests");

            migrationBuilder.DropColumn(
                name: "CriticalAcknowledgedAt",
                table: "LabRequests");

            migrationBuilder.DropColumn(
                name: "CriticalAcknowledgedBy",
                table: "LabRequests");

            migrationBuilder.DropColumn(
                name: "HasCriticalResult",
                table: "LabRequests");

            migrationBuilder.DropColumn(
                name: "ResultIssuedAt",
                table: "LabRequests");

            migrationBuilder.DropColumn(
                name: "ResultIssuedBy",
                table: "LabRequests");
        }
    }
}
