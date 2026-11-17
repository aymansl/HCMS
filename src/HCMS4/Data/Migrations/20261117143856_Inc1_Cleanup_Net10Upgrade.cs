using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCMS4.Data.Migrations
{
    /// <summary>
    /// Baseline migration for the .NET 10 / EF Core 10 upgrade.
    /// The model snapshot had drifted from the real model (some Graduation 1 migrations were written
    /// by hand without updating the snapshot), while the database itself already matches the model.
    /// This migration only refreshes the snapshot; it intentionally performs no schema changes.
    /// </summary>
    public partial class Inc1_Cleanup_Net10Upgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: the database schema already matches the model.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty: nothing to undo.
        }
    }
}
