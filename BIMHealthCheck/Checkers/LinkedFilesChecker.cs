using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Checker for analyzing Revit links.
    /// </summary>
    public class LinkedFilesChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "LinkedFilesChecker";
        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "Linked Files Analysis";
        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.10;

        /// <summary>
        /// Runs the health check for linked files.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with the findings.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                var linkInstances = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList();
                var linkTypes = new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>().ToList();

                int unpinnedCount = 0;
                int overloadedCount = 0;
                
                foreach (var link in linkInstances)
                {
                    var type = doc.GetElement(link.GetTypeId()) as RevitLinkType;
                    string typeName = type?.Name ?? "Unknown Link";
                    
                    if (!link.Pinned)
                    {
                        unpinnedCount++;
                        result.Score -= 5;
                        result.Items.Add(new CheckItem { Description = $"Unpinned Link: {typeName}", Severity = Severity.Warning, ElementId = link.Id.ToString(), Category = "Link Instance" });
                    }
                }

                foreach (var lt in linkTypes)
                {
                    int instanceCount = linkInstances.Count(i => i.GetTypeId() == lt.Id);
                    if (instanceCount > 3)
                    {
                        overloadedCount++;
                        result.Score -= 3;
                        result.Items.Add(new CheckItem { Description = $"Overloaded Link Type: {lt.Name} has {instanceCount} instances", Severity = Severity.Warning, ElementId = lt.Id.ToString(), Category = "Link Type" });
                    }
                    
                    if (lt.IsNestedLink)
                    {
                        result.Items.Add(new CheckItem { Description = $"Nested Link Found: {lt.Name}", Severity = Severity.Info, ElementId = lt.Id.ToString(), Category = "Link Type" });
                    }
                }

                if (linkTypes.Count > 10)
                {
                    result.Score -= 10;
                }

                result.Score = Math.Max(0, result.Score);

                result.Metrics["LinkTypeCount"] = linkTypes.Count;
                result.Metrics["LinkInstanceCount"] = linkInstances.Count;
                result.Metrics["UnpinnedLinks"] = unpinnedCount;
                result.Metrics["OverloadedLinkTypes"] = overloadedCount;

                result.Summary = $"Found {linkTypes.Count} link types and {linkInstances.Count} link instances.";
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
