using System;
using System.IO;
using System.Text;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Reporting
{
    /// <summary>
    /// Exports health check results to a CSV file without external dependencies.
    /// </summary>
    public static class ExcelExporter
    {
        /// <summary>
        /// Exports the HealthScore to a CSV format file.
        /// </summary>
        /// <param name="healthScore">The health score results.</param>
        /// <param name="filePath">The destination file path.</param>
        public static void ExportToCsv(HealthScore healthScore, string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                // Section 1: Header
                writer.WriteLine("Model Name,Model Path,Timestamp,Overall Score,Grade");
                writer.WriteLine($"{EscapeCsv(healthScore.ModelName)},{EscapeCsv(healthScore.ModelPath)},{EscapeCsv(healthScore.Timestamp.ToString("s"))},{healthScore.OverallScore:F2},{EscapeCsv(healthScore.Grade.ToString())}");
                writer.WriteLine(); // Blank line

                // Section 3: Category Scores
                writer.WriteLine("Category Scores");
                writer.WriteLine("Category,Score,Weight,Summary");
                foreach (var result in healthScore.Results)
                {
                    writer.WriteLine($"{EscapeCsv(result.DisplayName)},{result.Score:F2},{result.Weight:F2},{EscapeCsv(result.Summary)}");
                }
                writer.WriteLine(); // Blank line

                // Section 5: Detailed Findings
                writer.WriteLine("Detailed Findings");
                writer.WriteLine("Category,Description,Severity,Element ID,Sub-Category");
                
                foreach (var result in healthScore.Results)
                {
                    if (result.Items == null) continue;

                    foreach (var item in result.Items)
                    {
                        writer.WriteLine($"{EscapeCsv(result.DisplayName)},{EscapeCsv(item.Description)},{EscapeCsv(item.Severity.ToString())},{EscapeCsv(item.ElementId)},{EscapeCsv(item.Category)}");
                    }
                }
            }
        }

        /// <summary>
        /// Escapes a string to conform to CSV formatting rules.
        /// </summary>
        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            
            bool containsQuotes = value.Contains("\"");
            bool containsCommas = value.Contains(",");
            bool containsNewlines = value.Contains("\n") || value.Contains("\r");

            if (containsQuotes || containsCommas || containsNewlines)
            {
                // Double up internal quotes
                value = value.Replace("\"", "\"\"");
                // Wrap in quotes
                return $"\"{value}\"";
            }

            return value;
        }
    }
}
