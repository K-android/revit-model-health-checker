using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Industry-aligned checker for analyzing and classifying Revit model warnings.
    /// Categorizes all warnings into actionable BIM classifications and links failing elements.
    /// </summary>
    public class WarningsChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "WarningsChecker";

        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "Model Warnings & Classifications";

        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.15;

        /// <summary>
        /// Runs the health check for warnings with industry-standard classification and realistic scoring.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with categorized findings and clickable element IDs.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                var warnings = doc.GetWarnings();
                int totalWarnings = warnings.Count;

                var classificationCounts = new Dictionary<string, int>
                {
                    { "Duplicate Elements", 0 },
                    { "Rooms & Areas", 0 },
                    { "Overlapping Geometry", 0 },
                    { "Joins & Cuts", 0 },
                    { "Stairs & Railings", 0 },
                    { "MEP & Systems", 0 },
                    { "Analytical & Structural", 0 },
                    { "Dimensions & Annotations", 0 },
                    { "General Warnings", 0 }
                };

                var warningTypeFrequency = new Dictionary<string, int>();
                int duplicateCount = 0;
                int roomAreaCount = 0;

                // Max individual items to add to the findings list to keep UI lightning fast
                const int maxItemsToReport = 300;
                int itemsAdded = 0;

                foreach (var fw in warnings)
                {
                    string desc = fw.GetDescriptionText()?.Trim() ?? "Unspecified Warning";

                    // Track frequency by exact description
                    if (!warningTypeFrequency.ContainsKey(desc))
                        warningTypeFrequency[desc] = 0;
                    warningTypeFrequency[desc]++;

                    // Classify the warning
                    string classification = ClassifyWarning(desc);
                    classificationCounts[classification]++;

                    Severity severity = DetermineSeverity(classification, desc);

                    if (classification == "Duplicate Elements")
                        duplicateCount++;
                    else if (classification == "Rooms & Areas")
                        roomAreaCount++;

                    // Extract all failing elements for click-to-zoom navigation
                    var failingIds = fw.GetFailingElements() ?? new List<ElementId>();
                    var additionalIds = fw.GetAdditionalElements() ?? new List<ElementId>();
                    var allIds = failingIds.Concat(additionalIds).Distinct().ToList();

                    string elementIdStr = string.Join(", ", allIds.Select(id => id.ToString()));

                    if (itemsAdded < maxItemsToReport)
                    {
                        result.Items.Add(new CheckItem
                        {
                            Description = desc,
                            Severity = severity,
                            ElementId = elementIdStr,
                            Category = classification
                        });
                        itemsAdded++;
                    }
                }

                if (totalWarnings > maxItemsToReport)
                {
                    result.Items.Add(new CheckItem
                    {
                        Description = $"... and {totalWarnings - maxItemsToReport} more warnings (showing first {maxItemsToReport})",
                        Severity = Severity.Info,
                        ElementId = string.Empty,
                        Category = "Summary"
                    });
                }

                // Realistic industry scoring curve (Autodesk Model Performance Guidelines):
                // 0-25: -0.2/ea (up to -5)
                // 26-100: -0.15/ea (up to -11.25)
                // 101-300: -0.1/ea (up to -20)
                // >300: -0.05/ea
                // Plus specific penalties for high-risk issues (duplicates corrupt quantities!)
                double warningDeduction = 0;
                if (totalWarnings <= 25)
                {
                    warningDeduction = totalWarnings * 0.2;
                }
                else if (totalWarnings <= 100)
                {
                    warningDeduction = (25 * 0.2) + ((totalWarnings - 25) * 0.15);
                }
                else if (totalWarnings <= 300)
                {
                    warningDeduction = (25 * 0.2) + (75 * 0.15) + ((totalWarnings - 100) * 0.10);
                }
                else
                {
                    warningDeduction = (25 * 0.2) + (75 * 0.15) + (200 * 0.10) + ((totalWarnings - 300) * 0.05);
                }

                // Severe duplicate elements penalty (max -25)
                double duplicateDeduction = Math.Min(25, duplicateCount * 2.0);

                // Room/Area issues penalty (max -15)
                double roomAreaDeduction = Math.Min(15, roomAreaCount * 1.0);

                double totalDeduction = warningDeduction + duplicateDeduction + roomAreaDeduction;
                result.Score = Math.Max(0, Math.Round(100.0 - totalDeduction, 1));

                // Record detailed metrics
                result.Metrics["TotalWarnings"] = totalWarnings;
                result.Metrics["DuplicateInstances"] = duplicateCount;
                result.Metrics["RoomAreaIssues"] = roomAreaCount;
                result.Metrics["OverlappingGeometry"] = classificationCounts["Overlapping Geometry"];
                result.Metrics["JoinsAndCuts"] = classificationCounts["Joins & Cuts"];
                result.Metrics["MEPIssues"] = classificationCounts["MEP & Systems"];

                var topWarnings = warningTypeFrequency
                    .OrderByDescending(kv => kv.Value)
                    .Take(5)
                    .ToDictionary(kv => kv.Key, kv => (object)$"{kv.Value}x");
                result.Metrics["TopWarningTypes"] = topWarnings;

                if (totalWarnings == 0)
                {
                    result.Summary = "0 warnings in model. Exceptional model health!";
                }
                else
                {
                    result.Summary = $"{totalWarnings} total warnings ({duplicateCount} duplicate instances, {roomAreaCount} room/area issues).";
                }
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during warnings analysis: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }

        private static string ClassifyWarning(string desc)
        {
            if (string.IsNullOrWhiteSpace(desc))
                return "General Warnings";

            string lower = desc.ToLowerInvariant();

            if (lower.Contains("identical instances") || lower.Contains("duplicate") || lower.Contains("same place"))
                return "Duplicate Elements";

            if (lower.Contains("room") || lower.Contains("area") || lower.Contains("space") || lower.Contains("enclosed region") || lower.Contains("boundary line"))
                return "Rooms & Areas";

            if (lower.Contains("overlap") || lower.Contains("highlighted walls overlap") || lower.Contains("floors overlap") || lower.Contains("elements overlap"))
                return "Overlapping Geometry";

            if (lower.Contains("joined") || lower.Contains("join") || lower.Contains("can't keep elements joined") || lower.Contains("cannot join") || lower.Contains("cut"))
                return "Joins & Cuts";

            if (lower.Contains("stair") || lower.Contains("railing") || lower.Contains("baluster") || lower.Contains("tread") || lower.Contains("riser"))
                return "Stairs & Railings";

            if (lower.Contains("duct") || lower.Contains("pipe") || lower.Contains("conduit") || lower.Contains("cable tray") || lower.Contains("circuit") || lower.Contains("disconnected") || lower.Contains("mep") || lower.Contains("flow"))
                return "MEP & Systems";

            if (lower.Contains("analytical") || lower.Contains("structural") || lower.Contains("node") || lower.Contains("rebar") || lower.Contains("stale analytical"))
                return "Analytical & Structural";

            if (lower.Contains("dimension") || lower.Contains("witness") || lower.Contains("reference plane") || lower.Contains("tag") || lower.Contains("leader"))
                return "Dimensions & Annotations";

            return "General Warnings";
        }

        private static Severity DetermineSeverity(string classification, string desc)
        {
            if (classification == "Duplicate Elements")
                return Severity.Critical;

            if (classification == "Rooms & Areas")
                return Severity.Error;

            if (classification == "Overlapping Geometry")
                return Severity.Warning;

            return Severity.Warning;
        }
    }
}
