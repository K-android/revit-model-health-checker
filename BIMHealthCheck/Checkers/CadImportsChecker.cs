using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Industry-aligned checker for auditing imported vs linked CAD files.
    /// Distinguishes between imported CAD (model bloat risk) and linked CAD (standard practice).
    /// </summary>
    public class CadImportsChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "CadImportsChecker";

        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "CAD Imports & Links Audit";

        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.15;

        /// <summary>
        /// Runs the health check for CAD imports and links.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with findings and element IDs.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                var imports = new FilteredElementCollector(doc)
                    .OfClass(typeof(ImportInstance))
                    .Cast<ImportInstance>()
                    .ToList();

                int importedModelWide = 0;
                int importedViewSpecific = 0;
                int linkedPinned = 0;
                int linkedUnpinned = 0;

                foreach (var import in imports)
                {
                    bool isLinked = import.IsLinked;
                    var typeElem = doc.GetElement(import.GetTypeId());
                    string fileName = typeElem?.Name ?? import.Category?.Name ?? "CAD File";
                    string placement = import.ViewSpecific ? "View Specific" : "Model Wide";

                    if (!isLinked)
                    {
                        // True CAD Import - Embeds DWG layers/styles into Revit
                        if (!import.ViewSpecific)
                        {
                            importedModelWide++;
                            result.Items.Add(new CheckItem
                            {
                                Description = $"Imported CAD (Model-Wide): \"{fileName}\" (Critical: permanently embeds DWG layers into Revit)",
                                Severity = Severity.Critical,
                                ElementId = import.Id.ToString(),
                                Category = "Imported CAD"
                            });
                        }
                        else
                        {
                            importedViewSpecific++;
                            result.Items.Add(new CheckItem
                            {
                                Description = $"Imported CAD (Current View Only): \"{fileName}\" (Recommend linking instead of importing)",
                                Severity = Severity.Error,
                                ElementId = import.Id.ToString(),
                                Category = "Imported CAD"
                            });
                        }
                    }
                    else
                    {
                        // Linked CAD - standard practice, but should be pinned and managed
                        if (!import.Pinned)
                        {
                            linkedUnpinned++;
                            result.Items.Add(new CheckItem
                            {
                                Description = $"Unpinned Linked CAD: \"{fileName}\" ({placement})",
                                Severity = Severity.Warning,
                                ElementId = import.Id.ToString(),
                                Category = "Linked CAD"
                            });
                        }
                        else
                        {
                            linkedPinned++;
                            result.Items.Add(new CheckItem
                            {
                                Description = $"Pinned Linked CAD: \"{fileName}\" ({placement})",
                                Severity = Severity.Info,
                                ElementId = import.Id.ToString(),
                                Category = "Linked CAD"
                            });
                        }
                    }
                }

                // Audit imported DWG layers in Object Styles
                int totalDwgLayers = 0;
                try
                {
                    foreach (Category rootCat in doc.Settings.Categories)
                    {
                        if (rootCat.Name.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) ||
                            rootCat.Name.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
                        {
                            if (rootCat.SubCategories != null)
                            {
                                totalDwgLayers += rootCat.SubCategories.Size;
                            }
                        }
                    }

                    if (totalDwgLayers > 40)
                    {
                        result.Items.Add(new CheckItem
                        {
                            Description = $"DWG Layer Bloat: {totalDwgLayers} CAD layers currently embedded in Object Styles line styles & subcategories",
                            Severity = Severity.Warning,
                            Category = "DWG Layers"
                        });
                    }
                }
                catch { }

                // Realistic scoring:
                // - Model-wide imported CAD: -10 each (severe performance/cleanliness hazard)
                // - View-specific imported CAD: -5 each
                // - Unpinned linked CAD: -2 each
                // - Pinned linked CAD: 0 deduction (it's standard BIM workflow!)
                // - Excessive DWG layers (>40): -5
                double layerDeduction = totalDwgLayers > 40 ? Math.Min(10, (totalDwgLayers - 40) * 0.1) : 0;
                double deduction = (importedModelWide * 10.0)
                                 + (importedViewSpecific * 5.0)
                                 + (linkedUnpinned * 2.0)
                                 + layerDeduction;

                result.Score = Math.Max(0, Math.Round(100.0 - Math.Min(100, deduction), 1));

                int totalImported = importedModelWide + importedViewSpecific;
                int totalLinked = linkedPinned + linkedUnpinned;

                result.Metrics["TotalCADFiles"] = imports.Count;
                result.Metrics["ImportedCADModelWide"] = importedModelWide;
                result.Metrics["ImportedCADViewSpecific"] = importedViewSpecific;
                result.Metrics["LinkedCADPinned"] = linkedPinned;
                result.Metrics["LinkedCADUnpinned"] = linkedUnpinned;
                result.Metrics["EmbeddedDWGLayers"] = totalDwgLayers;

                if (imports.Count == 0)
                {
                    result.Summary = "0 CAD files found in model. Perfect model hygiene!";
                }
                else if (totalImported == 0)
                {
                    result.Summary = $"{totalLinked} linked CAD files found (0 dangerous unlinked imports).";
                }
                else
                {
                    result.Summary = $"Found {totalImported} imported CAD files ({importedModelWide} model-wide) and {totalLinked} linked CAD files.";
                }
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during CAD audit: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
