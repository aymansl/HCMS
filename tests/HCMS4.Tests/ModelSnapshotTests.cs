using Microsoft.EntityFrameworkCore;
using HCMS4.Data;

namespace HCMS4.Tests
{
    public class ModelSnapshotTests
    {
        [Fact]
        public void Model_HasNoPendingChanges_ComparedToMigrationSnapshot()
        {
            // Arrange: the SQL Server provider is needed to build the relational model; no connection is opened.
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=(local);Database=HCMS_ModelCheck;Trusted_Connection=True")
                .Options;
            using var context = new ApplicationDbContext(options);

            // Act
            var hasPendingChanges = context.Database.HasPendingModelChanges();

            // Assert: EF Core 9+ refuses to run Migrate() when the model and the snapshot differ.
            Assert.False(hasPendingChanges, "The EF model differs from the migration snapshot. Add a migration.");
        }
    }
}
