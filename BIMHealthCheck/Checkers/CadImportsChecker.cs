using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Checker for identifying imported and linked CAD files.
    /// </summary>
    public class CadImportsChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "CadImportsChecker";
        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "CAD Imports and Links";
        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.15;

        /// <summary>
        /// Runs the health check for CAD imports.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with the findings.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                var imports = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)).Cast<ImportInstance>().ToList();
                int importedCount = 0;
                int linkedCount = 0;

                foreach (var import in imports)
                {
                    bool isLinked = import.IsLinked;
                    string catName = import.Category?.Name ?? "Unknown";
                    string viewSpecificStr = import.ViewSpecific ? "(View Specific)" : "(Model Wide)";
                    
                    if (isLinked)
                    {
                        linkedCount++;
                        result.Score -= 5;
                        result.Items.Add(new CheckItem 
                        { 
                            Description = $"Linked CAD: {catName} {viewSpecificStr}", 
                            Severity = Severity.Warning, 
                            ElementId = import.Id.ToString(), 
                            Category = "CAD Link" 
                        });
                    }
                    else
                    {
                        importedCount++;
                        result.Score -= 15;
                        result.Items.Add(new CheckItem 
                        { 
                            Description = $"Imported CAD: {catName} {viewSpecificStr}", 
                            Severity = Severity.Error, 
                            ElementId = import.Id.ToString(), 
                            Category = "CAD Import" 
                        });
                    }
                }

                result.Score = Math.Max(0, result.Score);

                result.Metrics["ImportedCadCount"] = importedCount;
                result.Metrics["LinkedCadCount"] = linkedCount;
                result.Metrics["TotalCadCount"] = imports.Count;

                if (imports.Count == 0)
                {
                    result.Summary = "No CAD imports or links found. Good job!";
                }
                else
                {
                    result.Summary = $"Found {importedCount} imported CAD files and {linkedCount} linked CAD files.";
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
