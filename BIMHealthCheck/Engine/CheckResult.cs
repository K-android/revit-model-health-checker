using System.Collections.Generic;

namespace BIMHealthCheck.Engine
{
    /// <summary>
    /// Severity level for individual check items.
    /// </summary>
    public enum Severity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Represents a single finding from a health check.
    /// </summary>
    public class CheckItem
    {
        /// <summary>Human-readable description of the finding.</summary>
        public string Description { get; set; }

        /// <summary>Severity of this finding.</summary>
        public Severity Severity { get; set; }

        /// <summary>Optional Revit ElementId as string for navigation.</summary>
        public string ElementId { get; set; }

        /// <summary>Sub-category or grouping label.</summary>
        public string Category { get; set; }
    }

    /// <summary>
    /// Aggregated result from a single health checker.
    /// </summary>
    public class CheckResult
    {
        /// <summary>Internal name of the checker.</summary>
        public string CheckerName { get; set; }

        /// <summary>User-friendly display name.</summary>
        public string DisplayName { get; set; }

        /// <summary>Score from 0 (worst) to 100 (best).</summary>
        public double Score { get; set; }

        /// <summary>Weight of this check in the overall score (0.0 to 1.0).</summary>
        public double Weight { get; set; }

        /// <summary>Individual findings.</summary>
        public List<CheckItem> Items { get; set; } = new List<CheckItem>();

        /// <summary>Key-value metrics collected during the check.</summary>
        public Dictionary<string, object> Metrics { get; set; } = new Dictionary<string, object>();

        /// <summary>Human-readable summary of the check result.</summary>
        public string Summary { get; set; }

        /// <summary>Count of items by severity.</summary>
        public int CriticalCount => CountBySeverity(Severity.Critical);
        public int ErrorCount => CountBySeverity(Severity.Error);
        public int WarningCount => CountBySeverity(Severity.Warning);
        public int InfoCount => CountBySeverity(Severity.Info);

        private int CountBySeverity(Severity severity)
        {
            int count = 0;
            foreach (var item in Items)
                if (item.Severity == severity) count++;
            return count;
        }
    }
}
