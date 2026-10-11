using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Industry-aligned checker to detect and evaluate in-place families.
    /// In-place families do not leverage standard family definition caching and should be monitored.
    /// </summary>
    public class InPlaceFamiliesChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "InPlaceFamiliesChecker";

        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "In-Place Families Audit";

        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.10;

        /// <summary>
        /// Runs the health check for in-place families using realistic thresholds.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with findings and element IDs.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                var inPlaceFamilies = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Where(f => f.IsInPlace)
                    .ToList();

                var affectedCategories = new HashSet<string>();
                int inPlaceCount = inPlaceFamilies.Count;
                int totalInPlaceInstances = 0;

                foreach (var fam in inPlaceFamilies)
                {
                    string catName = fam.FamilyCategory?.Name ?? "General";
                    affectedCategories.Add(catName);

                    int instanceCount = 0;
                    var symbolIds = fam.GetFamilySymbolIds();
                    var instanceIds = new List<ElementId>();

                    foreach (var sId in symbolIds)
                    {
                        var instances = new FilteredElementCollector(doc)
                            .WherePasses(new FamilyInstanceFilter(doc, sId))
                            .ToElementIds();

                        instanceCount += instances.Count;
                        instanceIds.AddRange(instances);
                    }

                    totalInPlaceInstances += instanceCount;

                    // Prefer pointing to the instance(s) if placed, else the family
                    string targetId = instanceIds.Count > 0
                        ? string.Join(", ", instanceIds.Select(id => id.ToString()))
                        : fam.Id.ToString();

                    Severity sev = instanceCount > 1 ? Severity.Warning : Severity.Info;

                    result.Items.Add(new CheckItem
                    {
                        Description = $"In-Place Family: \"{fam.Name}\" ({catName}, {instanceCount} instance(s))",
                        Severity = sev,
                        ElementId = targetId,
                        Category = catName
                    });
                }

                // Realistic industry curve:
                // 0: 100
                // 1-3: -2/ea (up to -6) -> score 94+
                // 4-8: -3/ea -> score ~79-90
                // 9-15: -4/ea -> score ~55-75
                // >15: -5/ea
                double deduction = 0;
                if (inPlaceCount <= 3)
                {
                    deduction = inPlaceCount * 2.0;
                }
                else if (inPlaceCount <= 8)
                {
                    deduction = (3 * 2.0) + ((inPlaceCount - 3) * 3.0);
                }
                else if (inPlaceCount <= 15)
                {
                    deduction = (3 * 2.0) + (5 * 3.0) + ((inPlaceCount - 8) * 4.0);
                }
                else
                {
                    deduction = (3 * 2.0) + (5 * 3.0) + (7 * 4.0) + ((inPlaceCount - 15) * 5.0);
                }

                result.Score = Math.Max(0, Math.Round(100.0 - Math.Min(100, deduction), 1));

                result.Metrics["InPlaceFamilyCount"] = inPlaceCount;
                result.Metrics["TotalInstances"] = totalInPlaceInstances;
                result.Metrics["AffectedCategoriesCount"] = affectedCategories.Count;

                if (inPlaceCount == 0)
                {
                    result.Summary = "0 in-place families found. Ideal modeling practice!";
                }
                else
                {
                    result.Summary = $"Found {inPlaceCount} in-place families ({totalInPlaceInstances} instances) across {affectedCategories.Count} categories.";
                }
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during in-place family audit: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
