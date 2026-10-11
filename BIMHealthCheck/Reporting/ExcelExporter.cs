using System;
using System.IO;
using System.Text;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Reporting
{
    /// <summary>
    /// Exports health check results, discipline scorecards, trends, and findings to CSV.
    /// </summary>
    public static class ExcelExporter
    {
        public static void ExportToCsv(HealthScore healthScore, string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                // Section 1: Project Scorecard Summary
                writer.WriteLine("Project Health Scorecard Summary");
                writer.WriteLine("Model Name,Model Path,Audit Date,Overall Score,Grade,Trend,Architecture Score,Structure Score,MEP Score,Coordination Score,Total Findings,Critical,Error,Warning,Info");
                
                string trendSummary = healthScore.Trend?.SummaryText ?? "Baseline run";
                writer.WriteLine($"{EscapeCsv(healthScore.ModelName)},{EscapeCsv(healthScore.ModelPath)},{EscapeCsv(healthScore.Timestamp.ToString("s"))},{healthScore.OverallScore:F1},{EscapeCsv(healthScore.Grade.ToString())},{EscapeCsv(trendSummary)},{healthScore.ArchitectureScore:F1},{healthScore.StructureScore:F1},{healthScore.MepScore:F1},{healthScore.CoordinationScore:F1},{healthScore.TotalIssues},{healthScore.CriticalCount},{healthScore.ErrorCount},{healthScore.WarningCount},{healthScore.InfoCount}");
                writer.WriteLine();

                // Section 2: Category Breakdown
                writer.WriteLine("Category Breakdown");
                writer.WriteLine("Category Name,Score,Weight,Findings Count,Summary");
                foreach (var result in healthScore.Results)
                {
                    writer.WriteLine($"{EscapeCsv(result.DisplayName)},{result.Score:F1},{result.Weight:F2},{result.Items.Count},{EscapeCsv(result.Summary)}");
                }
                writer.WriteLine();

                // Section 3: Detailed Findings
                writer.WriteLine("Detailed Findings & Elements");
                writer.WriteLine("Category,Severity,Sub-Category,Description,Element ID");

                foreach (var result in healthScore.Results)
                {
                    if (result.Items == null) continue;

                    foreach (var item in result.Items)
                    {
                        writer.WriteLine($"{EscapeCsv(result.DisplayName)},{EscapeCsv(item.Severity.ToString())},{EscapeCsv(item.Category)},{EscapeCsv(item.Description)},{EscapeCsv(item.ElementId)}");
                    }
                }
            }
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            bool containsQuotes = value.Contains("\"");
            bool containsCommas = value.Contains(",");
            bool containsNewlines = value.Contains("\n") || value.Contains("\r");

            if (containsQuotes || containsCommas || containsNewlines)
            {
                value = value.Replace("\"", "\"\"");
                return $"\"{value}\"";
            }

            return value;
        }
    }
}
