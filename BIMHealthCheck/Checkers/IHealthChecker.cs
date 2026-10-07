using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Interface for all health check implementations.
    /// </summary>
    public interface IHealthChecker
    {
        /// <summary>Internal identifier for this checker.</summary>
        string Name { get; }

        /// <summary>Human-friendly display name.</summary>
        string DisplayName { get; }

        /// <summary>Weight of this checker in the overall score (0.0 to 1.0).</summary>
        double Weight { get; }

        /// <summary>
        /// Runs the health check against the specified Revit document.
        /// </summary>
        /// <param name="doc">The Revit document to analyze.</param>
        /// <returns>The check result with score, items, and metrics.</returns>
        CheckResult Run(Document doc);
    }
}
