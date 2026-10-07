using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Checker for analyzing Revit model warnings.
    /// </summary>
    public class WarningsChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "WarningsChecker";
        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "Model Warnings";
        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.15;

        /// <summary>
        /// Runs the health check for warnings.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with the findings.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                var warnings = doc.GetWarnings();
                int criticalCount = 0;
                int normalCount = 0;
                var warningCounts = new Dictionary<string, int>();

                string[] criticalKeywords = new string[] 
                { 
                    "duplicate instances", "room is not in a properly enclosed region", 
                    "room overlap", "area is not in a properly enclosed region", 
                    "elements overlap", "stale analytical model" 
                };

                foreach (var fw in warnings)
                {
                    string desc = fw.GetDescriptionText();
                    if (!warningCounts.ContainsKey(desc))
                    {
                        warningCounts[desc] = 0;
                    }
                    warningCounts[desc]++;

                    bool isCritical = criticalKeywords.Any(k => desc.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (isCritical)
                    {
                        criticalCount++;
                        result.Score -= 5;
                        string elemIds = string.Join(", ", fw.GetFailingElements().Select(id => id.ToString()));
                        result.Items.Add(new CheckItem 
                        { 
                            Description = $"Critical Warning: {desc}", 
                            Severity = Severity.Error, 
                            ElementId = elemIds,
                            Category = "Warning"
                        });
                    }
                    else
                    {
                        normalCount++;
                        result.Score -= 2;
                    }
                }

                result.Score = Math.Max(0, result.Score);
                result.Metrics["TotalWarnings"] = warnings.Count;
                result.Metrics["CriticalWarnings"] = criticalCount;
                
                var topWarnings = warningCounts.OrderByDescending(kv => kv.Value).Take(5)
                                               .ToDictionary(kv => kv.Key, kv => (object)kv.Value);
                result.Metrics["TopWarningTypes"] = topWarnings;
                
                result.Summary = $"{warnings.Count} warnings found ({criticalCount} critical)";
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
