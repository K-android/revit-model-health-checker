using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Industry-aligned checker for analyzing worksets and worksharing settings.
    /// Evaluates workset configuration, unrenamed default worksets, and distribution.
    /// </summary>
    public class WorksetChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "WorksetChecker";

        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "Workset Organization";

        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.10;

        /// <summary>
        /// Runs the health check for worksets.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with findings.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                if (!doc.IsWorkshared)
                {
                    result.Summary = "Model is not workshared (standalone file). Workset check not applicable.";
                    result.Metrics["Workshared"] = false;
                    result.Metrics["UserWorksetCount"] = 0;
                    result.Items.Add(new CheckItem
                    {
                        Description = "Standalone (non-workshared) model. Worksets not required.",
                        Severity = Severity.Info,
                        Category = "Worksets"
                    });
                    return result;
                }

                var userWorksets = new FilteredWorksetCollector(doc)
                    .OfKind(WorksetKind.UserWorkset)
                    .ToWorksets()
                    .ToList();

                int worksetCount = userWorksets.Count;
                var elementsPerWorkset = new Dictionary<WorksetId, int>();

                bool hasWorkset1 = false;
                foreach (var w in userWorksets)
                {
                    elementsPerWorkset[w.Id] = 0;
                    if (w.Name.Equals("Workset1", StringComparison.OrdinalIgnoreCase))
                    {
                        hasWorkset1 = true;
                        result.Items.Add(new CheckItem
                        {
                            Description = "Default 'Workset1' found and not renamed to a project-specific workset.",
                            Severity = Severity.Warning,
                            Category = "Workset Naming"
                        });
                    }
                }

                var allElements = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements();
                int totalElements = 0;
                foreach (var e in allElements)
                {
                    var wsId = e.WorksetId;
                    if (wsId != WorksetId.InvalidWorksetId && elementsPerWorkset.ContainsKey(wsId))
                    {
                        elementsPerWorkset[wsId]++;
                        totalElements++;
                    }
                }

                int emptyWorksetsCount = 0;
                double largestPct = 0;
                double workset1Pct = 0;

                foreach (var kvp in elementsPerWorkset)
                {
                    var w = userWorksets.FirstOrDefault(x => x.Id == kvp.Key);
                    if (kvp.Value == 0)
                    {
                        emptyWorksetsCount++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Empty Workset: \"{w?.Name}\" (contains 0 model elements)",
                            Severity = Severity.Info,
                            Category = "Unused Workset"
                        });
                    }

                    if (totalElements > 0)
                    {
                        double pct = (double)kvp.Value / totalElements;
                        if (pct > largestPct) largestPct = pct;

                        if (w?.Name.Equals("Workset1", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            workset1Pct = pct;
                        }
                    }
                }

                // Realistic scoring:
                // - Only 1 workset created in a workshared model: -10
                // - Workset1 left unrenamed: -10
                // - Empty worksets: -2 each (max -10)
                // - Model placed entirely on a single workset (>95% when multiple worksets exist): -5
                double deduction = 0;
                if (worksetCount <= 1)
                {
                    deduction += 10;
                    result.Items.Add(new CheckItem
                    {
                        Description = "Model only has 1 user workset. Consider organizing elements across discipline worksets.",
                        Severity = Severity.Warning,
                        Category = "Workset Strategy"
                    });
                }

                if (hasWorkset1)
                {
                    deduction += 10;
                }

                deduction += Math.Min(10, emptyWorksetsCount * 2.0);

                if (worksetCount > 2 && largestPct > 0.95)
                {
                    deduction += 5;
                    result.Items.Add(new CheckItem
                    {
                        Description = $"Over 95% of model elements are placed on a single workset ({largestPct:P0}).",
                        Severity = Severity.Info,
                        Category = "Distribution"
                    });
                }

                result.Score = Math.Max(0, Math.Round(100.0 - Math.Min(100, deduction), 1));

                result.Metrics["Workshared"] = true;
                result.Metrics["UserWorksetCount"] = worksetCount;
                result.Metrics["Workset1Present"] = hasWorkset1;
                result.Metrics["ElementsInWorkset1Pct"] = $"{workset1Pct:P1}";
                result.Metrics["EmptyWorksetsCount"] = emptyWorksetsCount;
                result.Metrics["LargestWorksetRatio"] = $"{largestPct:P1}";

                result.Summary = $"Workshared model with {worksetCount} user worksets (Workset1: {(hasWorkset1 ? "Present" : "Renamed")}, {emptyWorksetsCount} empty).";
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during workset check: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
