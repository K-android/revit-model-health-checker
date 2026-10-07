using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Checks general model metrics such as file size and element counts.
    /// </summary>
    public class ModelMetricsChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "ModelMetricsChecker";
        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "Model Metrics";
        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.10;

        /// <summary>
        /// Runs the health check for model metrics.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with the findings.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                int elementCount = new FilteredElementCollector(doc).WhereElementIsNotElementType().GetElementCount();
                
                var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate).ToList();
                int viewCount = views.Count;
                
                int sheetCount = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).GetElementCount();
                int familyCount = new FilteredElementCollector(doc).OfClass(typeof(Family)).GetElementCount();
                int familyTypeCount = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).GetElementCount();
                
                long fileSizeBytes = 0;
                if (!string.IsNullOrEmpty(doc.PathName) && File.Exists(doc.PathName))
                {
                    fileSizeBytes = new FileInfo(doc.PathName).Length;
                }

                if (elementCount > 1000000) result.Score -= 40;
                else if (elementCount > 500000) result.Score -= 20;

                if (viewCount > 500) result.Score -= 20;
                else if (viewCount > 200) result.Score -= 10;

                if (familyCount > 1000) result.Score -= 30;
                else if (familyCount > 500) result.Score -= 15;

                long mb = fileSizeBytes / (1024 * 1024);
                if (mb > 500) result.Score -= 20;
                else if (mb > 200) result.Score -= 10;

                result.Score = Math.Max(0, result.Score);

                result.Metrics["ElementCount"] = elementCount;
                result.Metrics["ViewCount"] = viewCount;
                result.Metrics["SheetCount"] = sheetCount;
                result.Metrics["FamilyCount"] = familyCount;
                result.Metrics["FamilyTypeCount"] = familyTypeCount;
                result.Metrics["FileSizeBytes"] = fileSizeBytes;

                result.Items.Add(new CheckItem { Description = $"Total Elements: {elementCount}", Severity = Severity.Info, Category = "Metrics" });
                result.Items.Add(new CheckItem { Description = $"Total Views: {viewCount}", Severity = Severity.Info, Category = "Metrics" });
                result.Items.Add(new CheckItem { Description = $"Total Sheets: {sheetCount}", Severity = Severity.Info, Category = "Metrics" });
                result.Items.Add(new CheckItem { Description = $"Total Families: {familyCount}", Severity = Severity.Info, Category = "Metrics" });
                result.Items.Add(new CheckItem { Description = $"File Size: {mb} MB", Severity = Severity.Info, Category = "Metrics" });

                result.Summary = $"Model contains {elementCount} elements, {viewCount} views, file size is {mb} MB.";
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
