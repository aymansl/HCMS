using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCMS4.Data.Migrations
{
    /// <inheritdoc />
    public partial class Inc1_Cleanup_PaymentMethodAndLeaveRejectionReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "LeaveRequests",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "Invoices",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Invoices");
        }
    }
}
