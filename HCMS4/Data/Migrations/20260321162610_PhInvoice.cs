using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCMS4.Data.Migrations
{
    public partial class PhInvoice : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DispensedAt",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PharmacistId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PharmacistId",
                table: "Invoices",
                column: "PharmacistId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Pharmacists_PharmacistId",
                table: "Invoices",
                column: "PharmacistId",
                principalTable: "Pharmacists",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Pharmacists_PharmacistId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PharmacistId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DispensedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PharmacistId",
                table: "Invoices");
        }
    }
}
