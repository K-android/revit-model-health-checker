using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Audits model elements for duplicate Mark parameter values across key BIM categories
    /// (Doors, Windows, Mechanical/Electrical Equipment, Fixtures).
    /// </summary>
    public class DuplicateMarksChecker : IHealthChecker
    {
        public string Name => "DuplicateMarksChecker";
        public string DisplayName => "Duplicate Mark Values";
        public double Weight => 0.08;

        private static readonly BuiltInCategory[] CategoriesToCheck = new[]
        {
            BuiltInCategory.OST_Doors,
            BuiltInCategory.OST_Windows,
            BuiltInCategory.OST_MechanicalEquipment,
            BuiltInCategory.OST_ElectricalEquipment,
            BuiltInCategory.OST_PlumbingFixtures,
            BuiltInCategory.OST_SpecialityEquipment
        };

        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                int totalDuplicateMarkGroups = 0;
                int totalElementsWithDuplicateMarks = 0;
                var duplicateMarksByCategory = new Dictionary<string, int>();

                foreach (var bic in CategoriesToCheck)
                {
                    string catName = bic.ToString().Replace("OST_", "");
                    var elements = new FilteredElementCollector(doc)
                        .OfCategory(bic)
                        .WhereElementIsNotElementType()
                        .ToElements();

                    var markBuckets = new Dictionary<string, List<ElementId>>(StringComparer.OrdinalIgnoreCase);

                    foreach (var elem in elements)
                    {
                        var param = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                        string markVal = param?.AsString()?.Trim();

                        if (!string.IsNullOrWhiteSpace(markVal))
                        {
                            if (!markBuckets.ContainsKey(markVal))
                                markBuckets[markVal] = new List<ElementId>();

                            markBuckets[markVal].Add(elem.Id);
                        }
                    }

                    int catDupGroups = 0;
                    foreach (var kvp in markBuckets)
                    {
                        if (kvp.Value.Count > 1)
                        {
                            catDupGroups++;
                            totalDuplicateMarkGroups++;
                            totalElementsWithDuplicateMarks += kvp.Value.Count;

                            string idStr = string.Join(", ", kvp.Value.Select(i => i.ToString()));
                            result.Items.Add(new CheckItem
                            {
                                Description = $"Duplicate {catName} Mark \"{kvp.Key}\" shared across {kvp.Value.Count} elements",
                                Severity = Severity.Warning,
                                ElementId = idStr,
                                Category = $"{catName} Marks"
                            });
                        }
                    }

                    if (catDupGroups > 0)
                    {
                        duplicateMarksByCategory[catName] = catDupGroups;
                    }
                }

                // Realistic scoring: -1.5 per duplicate mark group, max -30
                double deduction = Math.Min(35, totalDuplicateMarkGroups * 1.5);
                result.Score = Math.Max(0, Math.Round(100.0 - deduction, 1));

                result.Metrics["DuplicateMarkGroups"] = totalDuplicateMarkGroups;
                result.Metrics["TotalElementsAffected"] = totalElementsWithDuplicateMarks;
                foreach (var kvp in duplicateMarksByCategory)
                {
                    result.Metrics[$"{kvp.Key}_Duplicates"] = kvp.Value;
                }

                if (totalDuplicateMarkGroups == 0)
                {
                    result.Summary = "0 duplicate Mark values found across audited equipment and schedules.";
                }
                else
                {
                    result.Summary = $"Found {totalDuplicateMarkGroups} duplicate Mark values affecting {totalElementsWithDuplicateMarks} elements.";
                }
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during duplicate marks audit: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
