using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Industry-aligned checker for auditing linked Revit models, pinned status, and link health.
    /// </summary>
    public class LinkedFilesChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "LinkedFilesChecker";

        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "Revit Links & Coordination";

        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.10;

        /// <summary>
        /// Runs the health check for linked Revit files.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with findings and element IDs.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                var linkInstances = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                var linkTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkType))
                    .Cast<RevitLinkType>()
                    .ToList();

                int unpinnedCount = 0;
                int overloadedCount = 0;
                int unloadedCount = 0;

                // Check link instances: Pinned status is essential for BIM coordination!
                foreach (var link in linkInstances)
                {
                    var type = doc.GetElement(link.GetTypeId()) as RevitLinkType;
                    string typeName = type?.Name ?? "Revit Link";

                    if (!link.Pinned)
                    {
                        unpinnedCount++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Unpinned Link Instance: \"{typeName}\" (Risk: Accidental displacement)",
                            Severity = Severity.Warning,
                            ElementId = link.Id.ToString(),
                            Category = "Link Instances"
                        });
                    }
                    else
                    {
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Pinned Link Instance: \"{typeName}\"",
                            Severity = Severity.Info,
                            ElementId = link.Id.ToString(),
                            Category = "Link Instances"
                        });
                    }
                }

                // Check link types: unloaded / missing or nested
                foreach (var lt in linkTypes)
                {
                    int instanceCount = linkInstances.Count(i => i.GetTypeId() == lt.Id);
                    bool isLoaded = RevitLinkType.IsLoaded(doc, lt.Id);

                    if (!isLoaded)
                    {
                        unloadedCount++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Unloaded / Missing Link: \"{lt.Name}\"",
                            Severity = Severity.Warning,
                            ElementId = lt.Id.ToString(),
                            Category = "Link Types"
                        });
                    }

                    if (instanceCount > 5)
                    {
                        overloadedCount++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Link Type with high instance count: \"{lt.Name}\" ({instanceCount} instances)",
                            Severity = Severity.Info,
                            ElementId = lt.Id.ToString(),
                            Category = "Link Types"
                        });
                    }

                    if (lt.IsNestedLink)
                    {
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Nested Link: \"{lt.Name}\"",
                            Severity = Severity.Info,
                            ElementId = lt.Id.ToString(),
                            Category = "Link Types"
                        });
                    }
                }

                // Realistic scoring:
                // - Unpinned links: -4 each (max -20)
                // - Unloaded links: -3 each (max -15)
                double deduction = Math.Min(25, unpinnedCount * 4.0) + Math.Min(15, unloadedCount * 3.0);
                result.Score = Math.Max(0, Math.Round(100.0 - deduction, 1));

                result.Metrics["TotalLinkTypes"] = linkTypes.Count;
                result.Metrics["TotalLinkInstances"] = linkInstances.Count;
                result.Metrics["UnpinnedLinkInstances"] = unpinnedCount;
                result.Metrics["UnloadedLinks"] = unloadedCount;

                if (linkTypes.Count == 0)
                {
                    result.Summary = "No external Revit links in model.";
                }
                else
                {
                    result.Summary = $"Found {linkTypes.Count} link types, {linkInstances.Count} instances ({unpinnedCount} unpinned, {unloadedCount} unloaded).";
                }
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during linked files audit: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
