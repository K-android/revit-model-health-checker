using System;
using System.IO;
using System.Linq;
using System.Text;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Reporting
{
    /// <summary>
    /// Generates self-contained HTML reports for BIM Health Check results.
    /// </summary>
    public static class HtmlReportGenerator
    {
        /// <summary>
        /// Generates a full HTML string representing the health score report.
        /// </summary>
        /// <param name="healthScore">The health score data.</param>
        /// <returns>A complete HTML string.</returns>
        public static string Generate(HealthScore healthScore)
        {
            var sb = new StringBuilder();

            string gradeColor = GetGradeColor(healthScore.Grade);
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"    <title>BIM Health Check Report - {healthScore.ModelName}</title>");
            sb.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/chart.js\"></script>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        :root {");
            sb.AppendLine("            --primary-bg: #121212;");
            sb.AppendLine("            --card-bg: #1e1e1e;");
            sb.AppendLine("            --text-main: #ffffff;");
            sb.AppendLine("            --text-muted: #aaaaaa;");
            sb.AppendLine("            --healthy: #27ae60;");
            sb.AppendLine("            --attention: #f39c12;");
            sb.AppendLine("            --critical: #e74c3c;");
            sb.AppendLine("            --info: #3498db;");
            sb.AppendLine("        }");
            sb.AppendLine("        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: var(--primary-bg); color: var(--text-main); margin: 0; padding: 0; line-height: 1.6; }");
            sb.AppendLine("        header { background-color: #000; padding: 20px; text-align: center; border-bottom: 2px solid #333; }");
            sb.AppendLine("        .container { max-width: 1200px; margin: 0 auto; padding: 20px; }");
            sb.AppendLine("        h1, h2, h3 { margin-top: 0; }");
            sb.AppendLine("        .meta-info { color: var(--text-muted); font-size: 0.9em; margin-bottom: 20px; }");
            sb.AppendLine("        .gauge-container { display: flex; justify-content: center; align-items: center; margin: 30px 0; flex-direction: column; }");
            sb.AppendLine($"        .gauge {{ width: 200px; height: 200px; border-radius: 50%; background: conic-gradient({gradeColor} {healthScore.OverallScore}%, #444 0); display: flex; justify-content: center; align-items: center; position: relative; }}");
            sb.AppendLine("        .gauge::before { content: ''; width: 160px; height: 160px; border-radius: 50%; background-color: var(--primary-bg); position: absolute; }");
            sb.AppendLine("        .gauge-value { position: relative; font-size: 3em; font-weight: bold; }");
            sb.AppendLine("        .grade-text { font-size: 1.5em; font-weight: bold; margin-top: 15px; color: " + gradeColor + "; }");
            sb.AppendLine("        .cards-row { display: flex; flex-wrap: wrap; gap: 15px; margin-bottom: 40px; justify-content: center; }");
            sb.AppendLine("        .card { background-color: var(--card-bg); border-radius: 8px; padding: 15px; width: 250px; box-shadow: 0 4px 6px rgba(0,0,0,0.3); border-top: 4px solid var(--healthy); }");
            sb.AppendLine("        .chart-container { width: 100%; max-width: 600px; margin: 0 auto 40px auto; background-color: var(--card-bg); padding: 20px; border-radius: 8px; }");
            sb.AppendLine("        .checker-section { background-color: var(--card-bg); border-radius: 8px; padding: 20px; margin-bottom: 20px; box-shadow: 0 4px 6px rgba(0,0,0,0.3); }");
            sb.AppendLine("        .score-bar-bg { width: 100%; background-color: #333; height: 10px; border-radius: 5px; margin: 10px 0 20px 0; overflow: hidden; }");
            sb.AppendLine("        .score-bar-fill { height: 100%; border-radius: 5px; }");
            sb.AppendLine("        table { width: 100%; border-collapse: collapse; margin-top: 15px; }");
            sb.AppendLine("        th, td { padding: 10px; text-align: left; border-bottom: 1px solid #333; }");
            sb.AppendLine("        th { background-color: #2a2a2a; color: var(--text-muted); }");
            sb.AppendLine("        .badge { padding: 4px 8px; border-radius: 4px; font-size: 0.8em; font-weight: bold; color: white; }");
            sb.AppendLine("        .badge-info { background-color: var(--info); }");
            sb.AppendLine("        .badge-warning { background-color: var(--attention); }");
            sb.AppendLine("        .badge-error { background-color: var(--critical); }");
            sb.AppendLine("        .badge-critical { background-color: #8b0000; }");
            sb.AppendLine("        footer { text-align: center; padding: 20px; color: var(--text-muted); font-size: 0.8em; border-top: 1px solid #333; margin-top: 40px; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Header
            sb.AppendLine("    <header>");
            sb.AppendLine($"        <h1>BIM Health Check Report</h1>");
            sb.AppendLine($"        <div class=\"meta-info\">");
            sb.AppendLine($"            Model: <strong>{healthScore.ModelName}</strong> | Path: {healthScore.ModelPath}<br/>");
            sb.AppendLine($"            Generated on: {healthScore.Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"        </div>");
            sb.AppendLine("    </header>");

            sb.AppendLine("    <div class=\"container\">");

            // Overall Score Gauge
            sb.AppendLine("        <div class=\"gauge-container\">");
            sb.AppendLine("            <div class=\"gauge\">");
            sb.AppendLine($"                <span class=\"gauge-value\">{healthScore.OverallScore:F0}</span>");
            sb.AppendLine("            </div>");
            sb.AppendLine($"            <div class=\"grade-text\">Grade: {healthScore.Grade}</div>");
            sb.AppendLine("        </div>");

            // Chart
            sb.AppendLine("        <div class=\"chart-container\">");
            sb.AppendLine("            <canvas id=\"radarChart\"></canvas>");
            sb.AppendLine("        </div>");

            // Summary Cards
            sb.AppendLine("        <h2>Summary</h2>");
            sb.AppendLine("        <div class=\"cards-row\">");
            foreach (var result in healthScore.Results)
            {
                string resultColor = GetScoreColor(result.Score);
                sb.AppendLine($"            <div class=\"card\" style=\"border-top-color: {resultColor};\">");
                sb.AppendLine($"                <h3>{result.DisplayName}</h3>");
                sb.AppendLine($"                <div style=\"font-size: 1.5em; font-weight: bold; color: {resultColor};\">{result.Score:F0} / 100</div>");
                sb.AppendLine($"                <p style=\"font-size: 0.85em; color: var(--text-muted);\">{result.Summary}</p>");
                sb.AppendLine("            </div>");
            }
            sb.AppendLine("        </div>");

            // Detailed Sections
            sb.AppendLine("        <h2>Detailed Findings</h2>");
            foreach (var result in healthScore.Results)
            {
                string resultColor = GetScoreColor(result.Score);
                sb.AppendLine("        <div class=\"checker-section\">");
                sb.AppendLine($"            <h3>{result.DisplayName} - Score: {result.Score:F1}</h3>");
                sb.AppendLine($"            <div class=\"score-bar-bg\"><div class=\"score-bar-fill\" style=\"width: {Math.Max(0, Math.Min(100, result.Score))}%; background-color: {resultColor};\"></div></div>");
                sb.AppendLine($"            <p>{result.Summary}</p>");

                // Metrics
                if (result.Metrics != null && result.Metrics.Count > 0)
                {
                    sb.AppendLine("            <h4>Metrics</h4>");
                    sb.AppendLine("            <ul>");
                    foreach (var kvp in result.Metrics)
                    {
                        sb.AppendLine($"                <li><strong>{kvp.Key}:</strong> {kvp.Value}</li>");
                    }
                    sb.AppendLine("            </ul>");
                }

                // Items Table
                if (result.Items != null && result.Items.Count > 0)
                {
                    sb.AppendLine("            <h4>Findings</h4>");
                    sb.AppendLine("            <table>");
                    sb.AppendLine("                <thead><tr><th>Severity</th><th>Category</th><th>Description</th><th>Element ID</th></tr></thead>");
                    sb.AppendLine("                <tbody>");
                    
                    var sortedItems = result.Items.OrderByDescending(i => i.Severity).ToList();
                    
                    foreach (var item in sortedItems)
                    {
                        string badgeClass = GetBadgeClass(item.Severity);
                        sb.AppendLine("                <tr>");
                        sb.AppendLine($"                    <td><span class=\"badge {badgeClass}\">{item.Severity}</span></td>");
                        sb.AppendLine($"                    <td>{item.Category}</td>");
                        sb.AppendLine($"                    <td>{item.Description}</td>");
                        sb.AppendLine($"                    <td>{item.ElementId}</td>");
                        sb.AppendLine("                </tr>");
                    }
                    sb.AppendLine("                </tbody>");
                    sb.AppendLine("            </table>");
                }
                
                sb.AppendLine("        </div>");
            }

            sb.AppendLine("    </div>");
            
            // Footer
            sb.AppendLine("    <footer>");
            sb.AppendLine($"        Generated by BIM Health Check for Revit &bull; {healthScore.Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("    </footer>");

            // Chart JS Script
            var labels = string.Join(", ", healthScore.Results.Select(r => $"\"{r.DisplayName}\""));
            var data = string.Join(", ", healthScore.Results.Select(r => r.Score.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));
            
            sb.AppendLine("    <script>");
            sb.AppendLine("        const ctx = document.getElementById('radarChart').getContext('2d');");
            sb.AppendLine("        new Chart(ctx, {");
            sb.AppendLine("            type: 'radar',");
            sb.AppendLine("            data: {");
            sb.AppendLine($"                labels: [{labels}],");
            sb.AppendLine("                datasets: [{");
            sb.AppendLine("                    label: 'Health Scores',");
            sb.AppendLine($"                    data: [{data}],");
            sb.AppendLine("                    backgroundColor: 'rgba(39, 174, 96, 0.2)',");
            sb.AppendLine("                    borderColor: 'rgba(39, 174, 96, 1)',");
            sb.AppendLine("                    pointBackgroundColor: 'rgba(39, 174, 96, 1)',");
            sb.AppendLine("                    pointBorderColor: '#fff',");
            sb.AppendLine("                    pointHoverBackgroundColor: '#fff',");
            sb.AppendLine("                    pointHoverBorderColor: 'rgba(39, 174, 96, 1)'");
            sb.AppendLine("                }]");
            sb.AppendLine("            },");
            sb.AppendLine("            options: {");
            sb.AppendLine("                scales: {");
            sb.AppendLine("                    r: {");
            sb.AppendLine("                        angleLines: { color: 'rgba(255, 255, 255, 0.2)' },");
            sb.AppendLine("                        grid: { color: 'rgba(255, 255, 255, 0.2)' },");
            sb.AppendLine("                        pointLabels: { color: 'rgba(255, 255, 255, 0.8)', font: { size: 12 } },");
            sb.AppendLine("                        ticks: { color: 'transparent', backdropColor: 'transparent', min: 0, max: 100 }");
            sb.AppendLine("                    }");
            sb.AppendLine("                },");
            sb.AppendLine("                plugins: { legend: { display: false } }");
            sb.AppendLine("            }");
            sb.AppendLine("        });");
            sb.AppendLine("    </script>");

            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        /// <summary>
        /// Generates the HTML report and writes it to the specified file path.
        /// </summary>
        /// <param name="healthScore">The health score data.</param>
        /// <param name="filePath">The destination file path.</param>
        public static void SaveToFile(HealthScore healthScore, string filePath)
        {
            string html = Generate(healthScore);
            File.WriteAllText(filePath, html, Encoding.UTF8);
        }

        private static string GetGradeColor(HealthGrade grade)
        {
            switch (grade)
            {
                case HealthGrade.Healthy: return "var(--healthy)";
                case HealthGrade.NeedsAttention: return "var(--attention)";
                case HealthGrade.Critical: return "var(--critical)";
                default: return "var(--text-muted)";
            }
        }

        private static string GetScoreColor(double score)
        {
            if (score >= 80) return "var(--healthy)";
            if (score >= 50) return "var(--attention)";
            return "var(--critical)";
        }

        private static string GetBadgeClass(Severity severity)
        {
            switch (severity)
            {
                case Severity.Info: return "badge-info";
                case Severity.Warning: return "badge-warning";
                case Severity.Error: return "badge-error";
                case Severity.Critical: return "badge-critical";
                default: return "badge-info";
            }
        }
    }
}
