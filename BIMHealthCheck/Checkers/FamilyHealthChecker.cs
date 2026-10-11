using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Audits loaded families for geometric complexity, bloated type counts,
    /// and heavy imported mesh/DirectShape geometry.
    /// </summary>
    public class FamilyHealthChecker : IHealthChecker
    {
        public string Name => "FamilyHealthChecker";
        public string DisplayName => "Family Health & Geometry";
        public double Weight => 0.08;

        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                var families = new FilteredElementCollector(doc)
                    .OfClass(typeof(Family))
                    .Cast<Family>()
                    .Where(f => !f.IsInPlace)
                    .ToList();

                int totalFamilies = families.Count;
                int highTypeFamiliesCount = 0;
                int heavyMeshElements = 0;

                // 1. Audit family types count
                foreach (var f in families)
                {
                    var symbolIds = f.GetFamilySymbolIds();
                    if (symbolIds.Count > 35)
                    {
                        highTypeFamiliesCount++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Family with excessive types: \"{f.Name}\" ({symbolIds.Count} types loaded - consider catalog file)",
                            Severity = Severity.Warning,
                            ElementId = f.Id.ToString(),
                            Category = "Family Types"
                        });
                    }
                }

                // 2. Audit DirectShape and imported mesh elements
                var directShapes = new FilteredElementCollector(doc)
                    .OfClass(typeof(DirectShape))
                    .Cast<DirectShape>()
                    .ToList();

                heavyMeshElements = directShapes.Count;
                foreach (var ds in directShapes.Take(15))
                {
                    string shapeCat = ds.Category?.Name ?? "DirectShape";
                    result.Items.Add(new CheckItem
                    {
                        Description = $"DirectShape / Imported Mesh geometry: \"{ds.Name}\" ({shapeCat})",
                        Severity = Severity.Info,
                        ElementId = ds.Id.ToString(),
                        Category = "Imported Meshes"
                    });
                }

                // Realistic scoring:
                // - High type count families (>35): -2 each (max -15)
                // - Heavy directshapes / unmanaged meshes: -1 each (max -15)
                double deduction = Math.Min(15, highTypeFamiliesCount * 2.0)
                                 + Math.Min(15, heavyMeshElements * 1.0);

                result.Score = Math.Max(0, Math.Round(100.0 - deduction, 1));

                result.Metrics["TotalLoadableFamilies"] = totalFamilies;
                result.Metrics["HighTypeFamiliesCount"] = highTypeFamiliesCount;
                result.Metrics["DirectShapeMeshCount"] = heavyMeshElements;

                if (highTypeFamiliesCount == 0 && heavyMeshElements == 0)
                {
                    result.Summary = $"Audited {totalFamilies} loadable families. Clean family geometry and healthy type structures!";
                }
                else
                {
                    result.Summary = $"Audited {totalFamilies} families. Found {highTypeFamiliesCount} families with >35 types and {heavyMeshElements} DirectShape mesh elements.";
                }
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during family health audit: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
