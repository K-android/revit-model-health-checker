using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Checks general model scale metrics such as file size and element counts against BIM industry benchmarks.
    /// </summary>
    public class ModelMetricsChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "ModelMetricsChecker";

        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "Model Scale & Performance Metrics";

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
                long mb = 0;
                if (!string.IsNullOrEmpty(doc.PathName) && File.Exists(doc.PathName))
                {
                    fileSizeBytes = new FileInfo(doc.PathName).Length;
                    mb = fileSizeBytes / (1024 * 1024);
                }

                // Realistic industry thresholds for Autodesk Revit:
                // - File size: Recommended < 300-500 MB per discipline model
                double sizeDeduction = 0;
                if (mb > 800) sizeDeduction = 25;
                else if (mb > 500) sizeDeduction = 15;
                else if (mb > 350) sizeDeduction = 5;

                // - Total elements: > 500k is huge
                double elementDeduction = 0;
                if (elementCount > 800000) elementDeduction = 20;
                else if (elementCount > 500000) elementDeduction = 10;

                // - Families count: > 1000 can cause cache bloat
                double familyDeduction = 0;
                if (familyCount > 1200) familyDeduction = 15;
                else if (familyCount > 800) familyDeduction = 5;

                double totalDeduction = sizeDeduction + elementDeduction + familyDeduction;
                result.Score = Math.Max(0, Math.Round(100.0 - totalDeduction, 1));

                result.Metrics["ElementCount"] = elementCount;
                result.Metrics["ViewCount"] = viewCount;
                result.Metrics["SheetCount"] = sheetCount;
                result.Metrics["FamilyCount"] = familyCount;
                result.Metrics["FamilyTypeCount"] = familyTypeCount;
                result.Metrics["FileSizeMB"] = mb > 0 ? $"{mb} MB" : "Unsaved / Cloud";

                result.Items.Add(new CheckItem { Description = $"Total Placed Elements: {elementCount:N0}", Severity = Severity.Info, Category = "Scale" });
                result.Items.Add(new CheckItem { Description = $"Total Views: {viewCount:N0}", Severity = Severity.Info, Category = "Scale" });
                result.Items.Add(new CheckItem { Description = $"Total Sheets: {sheetCount:N0}", Severity = Severity.Info, Category = "Scale" });
                result.Items.Add(new CheckItem { Description = $"Loaded Families: {familyCount:N0} ({familyTypeCount:N0} types)", Severity = Severity.Info, Category = "Scale" });
                if (mb > 0)
                {
                    result.Items.Add(new CheckItem { Description = $"Local File Size: {mb} MB", Severity = mb > 500 ? Severity.Warning : Severity.Info, Category = "Scale" });
                }

                result.Summary = $"Model contains {elementCount:N0} elements across {viewCount} views and {familyCount} families. Size: {(mb > 0 ? mb + " MB" : "N/A")}.";
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during model metrics check: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
