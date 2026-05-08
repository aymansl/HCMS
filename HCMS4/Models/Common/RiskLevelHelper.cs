namespace HCMS4.Models.Common
{
    public static class RiskLevelHelper
    {
        public static string GetLevel(double score)
        {
            if (score >= BusinessRules.HighRiskThreshold)
            {
                return "high";
            }

            if (score >= BusinessRules.MediumRiskThreshold)
            {
                return "medium";
            }

            return "low";
        }

        public static bool Matches(string selectedRiskLevel, double score)
        {
            if (string.IsNullOrWhiteSpace(selectedRiskLevel) || selectedRiskLevel.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return GetLevel(score).Equals(selectedRiskLevel, StringComparison.OrdinalIgnoreCase);
        }
    }
}
