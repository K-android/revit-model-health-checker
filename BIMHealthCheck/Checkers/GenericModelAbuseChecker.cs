using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Audits model for "Generic Model" category overuse.
    /// Elements dumped into Generic Models bypass proper category scheduling, view filters,
    /// IFC mapping, and subcategory visibility controls.
    /// </summary>
    public class GenericModelAbuseChecker : IHealthChecker
    {
        public string Name => "GenericModelAbuseChecker";
        public string DisplayName => "Generic Model Categorization";
        public double Weight => 0.08;

        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                var genericElements = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_GenericModel)
                    .WhereElementIsNotElementType()
                    .ToElements();

                var genericFamilies = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Where(f => f.FamilyCategory != null && f.FamilyCategory.Id == new ElementId(BuiltInCategory.OST_GenericModel))
                    .ToList();

                int genericInstanceCount = genericElements.Count;
                int genericFamilyCount = genericFamilies.Count;

                // Total placed components across major categories for ratio comparison
                int totalComponents = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .GetElementCount();

                double genericRatio = totalComponents > 0 ? (double)genericInstanceCount / totalComponents : 0;

                // Group by family name to identify heavily repeated generic families
                var instancesByFamily = new Dictionary<string, List<ElementId>>(StringComparer.OrdinalIgnoreCase);
                foreach (var elem in genericElements)
                {
                    string famName = "Unknown Generic Family";
                    if (elem is FamilyInstance fi && fi.Symbol != null && fi.Symbol.Family != null)
                    {
                        famName = fi.Symbol.Family.Name;
                    }
                    else if (elem.GetTypeId() != ElementId.InvalidElementId)
                    {
                        var typeElem = doc.GetElement(elem.GetTypeId());
                        famName = typeElem?.Name ?? elem.Name;
                    }

                    if (!instancesByFamily.ContainsKey(famName))
                        instancesByFamily[famName] = new List<ElementId>();

                    instancesByFamily[famName].Add(elem.Id);
                }

                // Report the top generic families
                foreach (var kvp in instancesByFamily.OrderByDescending(k => k.Value.Count).Take(15))
                {
                    string idStr = string.Join(", ", kvp.Value.Take(10).Select(i => i.ToString()));
                    Severity sev = kvp.Value.Count > 10 ? Severity.Warning : Severity.Info;

                    result.Items.Add(new CheckItem
                    {
                        Description = $"Generic Model Family: \"{kvp.Key}\" ({kvp.Value.Count} instances - recommend reclassifying to proper discipline category)",
                        Severity = sev,
                        ElementId = idStr,
                        Category = "Generic Models"
                    });
                }

                // Realistic scoring:
                // If generic models are < 3% of elements: clean, 100
                // 3% - 8%: -5
                // 8% - 15%: -15
                // > 15%: -25
                double deduction = 0;
                if (genericRatio > 0.15) deduction = 25;
                else if (genericRatio > 0.08) deduction = 15;
                else if (genericRatio > 0.03) deduction = 5;

                result.Score = Math.Max(0, Math.Round(100.0 - deduction, 1));

                result.Metrics["GenericModelInstances"] = genericInstanceCount;
                result.Metrics["GenericModelFamilies"] = genericFamilyCount;
                result.Metrics["GenericModelRatio"] = $"{genericRatio:P1}";
                result.Metrics["TotalModelElements"] = totalComponents;

                if (genericInstanceCount == 0)
                {
                    result.Summary = "0 Generic Models placed. All elements categorized to explicit BIM categories!";
                }
                else
                {
                    result.Summary = $"Found {genericInstanceCount} Generic Model elements ({genericRatio:P1} of model) across {genericFamilyCount} families.";
                }
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during generic model audit: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
