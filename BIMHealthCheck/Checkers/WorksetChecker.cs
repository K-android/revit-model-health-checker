using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Checker for analyzing worksets and worksharing settings.
    /// </summary>
    public class WorksetChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "WorksetChecker";
        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "Workset Configuration";
        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.10;

        /// <summary>
        /// Runs the health check for worksets.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with the findings.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                if (!doc.IsWorkshared)
                {
                    result.Summary = "Non-workshared model";
                    result.Metrics["WorksetCount"] = 0;
                    return result;
                }

                var worksets = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset).ToWorksets();
                int worksetCount = worksets.Count;
                
                var elementsPerWorkset = new Dictionary<WorksetId, int>();
                foreach(var w in worksets) {
                    elementsPerWorkset[w.Id] = 0;
                    if (w.Name.Equals("Workset1", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Score -= 15;
                        result.Items.Add(new CheckItem { Description = "Default 'Workset1' found and not renamed.", Severity = Severity.Warning, Category = "Workset" });
                    }
                }

                var allElements = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements();
                int totalWorksharedElements = 0;
                foreach (var e in allElements)
                {
                    var wsId = e.WorksetId;
                    if (wsId != WorksetId.InvalidWorksetId && elementsPerWorkset.ContainsKey(wsId))
                    {
                        elementsPerWorkset[wsId]++;
                        totalWorksharedElements++;
                    }
                }

                int emptyWorksetsCount = 0;
                double largestPct = 0;
                double workset1Pct = 0;

                foreach (var kvp in elementsPerWorkset)
                {
                    var w = worksets.FirstOrDefault(x => x.Id == kvp.Key);
                    if (kvp.Value == 0)
                    {
                        emptyWorksetsCount++;
                        result.Score -= 5;
                        result.Items.Add(new CheckItem { Description = $"Empty Workset: {w?.Name}", Severity = Severity.Warning, Category = "Workset" });
                    }

                    if (totalWorksharedElements > 0)
                    {
                        double pct = (double)kvp.Value / totalWorksharedElements;
                        if (pct > largestPct) largestPct = pct;
                        
                        if (w?.Name.Equals("Workset1", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            workset1Pct = pct;
                        }
                    }
                }

                if (largestPct > 0.8)
                {
                    result.Score -= 10;
                    result.Items.Add(new CheckItem { Description = $"Unbalanced Worksets: One workset contains {largestPct:P1} of all elements.", Severity = Severity.Warning, Category = "Workset" });
                }

                result.Score = Math.Max(0, result.Score);
                
                result.Metrics["WorksetCount"] = worksetCount;
                result.Metrics["ElementsInWorkset1Pct"] = workset1Pct;
                result.Metrics["EmptyWorksets"] = emptyWorksetsCount;
                result.Metrics["LargestWorksetPct"] = largestPct;

                result.Summary = $"Model is workshared with {worksetCount} user worksets.";
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during check: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
