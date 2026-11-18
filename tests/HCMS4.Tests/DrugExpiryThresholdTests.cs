using HCMS4.Modules.Pharmacy.Models;
using HCMS4.Shared.Common;
namespace HCMS4.Tests
{
    // Regression tests for issue #23: Drug.ExpiryStatus used literals instead of the shared BusinessRules thresholds.
    public class DrugExpiryThresholdTests
    {
        private static string StatusInDays(int days) =>
            new Drug { Name = "X", ExpiryDate = DateTime.Now.AddDays(days).AddHours(1) }.ExpiryStatus;

        [Fact]
        public void ExpiryStatus_UsesBusinessRuleBoundaries()
        {
            Assert.Equal("Expired", StatusInDays(-1));
            Assert.Equal("Expiring Soon", StatusInDays(BusinessRules.DrugExpiryWarningDays));
            Assert.Equal("Expiring Shortly", StatusInDays(BusinessRules.DrugExpiryWarningDays + 1));
            Assert.Equal("Expiring Shortly", StatusInDays(BusinessRules.DrugExpiryShortWarningDays));
            Assert.Equal("Valid", StatusInDays(BusinessRules.DrugExpiryShortWarningDays + 1));
        }
    }
}
