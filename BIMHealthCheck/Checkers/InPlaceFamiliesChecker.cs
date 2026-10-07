using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Checker to detect and count in-place families.
    /// </summary>
    public class InPlaceFamiliesChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "InPlaceFamiliesChecker";
        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "In-Place Families";
        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.10;

        /// <summary>
        /// Runs the health check for in-place families.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with the findings.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                var inPlaceFamilies = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().Where(f => f.IsInPlace).ToList();
                var affectedCategories = new HashSet<string>();
                
                int inPlaceCount = inPlaceFamilies.Count;

                foreach (var fam in inPlaceFamilies)
                {
                    string catName = fam.FamilyCategory?.Name ?? "Unknown";
                    affectedCategories.Add(catName);
                    
                    int instanceCount = 0;
                    var symbolIds = fam.GetFamilySymbolIds();
                    foreach (var sId in symbolIds)
                    {
                        instanceCount += new FilteredElementCollector(doc).WherePasses(new FamilyInstanceFilter(doc, sId)).GetElementCount();
                    }

                    result.Score -= 10;
                    result.Items.Add(new CheckItem 
                    { 
                        Description = $"In-Place Family: {fam.Name} (Instances: {instanceCount})", 
                        Severity = Severity.Warning, 
                        ElementId = fam.Id.ToString(), 
                        Category = catName 
                    });
                }

                result.Score = Math.Max(0, result.Score);
                result.Metrics["InPlaceFamilyCount"] = inPlaceCount;
                result.Metrics["AffectedCategories"] = affectedCategories.ToList();

                if (inPlaceCount == 0)
                {
                    result.Summary = "No in-place families found. Excellent!";
                }
                else
                {
                    result.Summary = $"Found {inPlaceCount} in-place families across {affectedCategories.Count} categories.";
                }
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
