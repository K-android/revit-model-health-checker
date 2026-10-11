using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Reporting
{
    /// <summary>
    /// Generates self-contained, publication-grade HTML reports with
    /// One-Click Project Scorecards, discipline breakdown, and historical trends.
    /// </summary>
    public static class HtmlReportGenerator
    {
        public static string Generate(HealthScore healthScore)
        {
            var sb = new StringBuilder();
            string gradeColor = GetGradeColor(healthScore.Grade);

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"    <title>BIM Health Scorecard - {healthScore.ModelName}</title>");
            sb.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/chart.js\"></script>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        :root {");
            sb.AppendLine("            --primary-bg: #0f172a;");
            sb.AppendLine("            --card-bg: #1e293b;");
            sb.AppendLine("            --card-border: #334155;");
            sb.AppendLine("            --text-main: #f8fafc;");
            sb.AppendLine("            --text-muted: #94a3b8;");
            sb.AppendLine("            --healthy: #22c55e;");
            sb.AppendLine("            --attention: #f59e0b;");
            sb.AppendLine("            --critical: #ef4444;");
            sb.AppendLine("            --info: #38bdf8;");
            sb.AppendLine("        }");
            sb.AppendLine("        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: var(--primary-bg); color: var(--text-main); margin: 0; padding: 0; line-height: 1.5; }");
            sb.AppendLine("        header { background: linear-gradient(180deg, #020617 0%, #0f172a 100%); padding: 30px 20px 20px 20px; text-align: center; border-bottom: 1px solid var(--card-border); }");
            sb.AppendLine("        .container { max-width: 1200px; margin: 0 auto; padding: 30px 20px; }");
            sb.AppendLine("        h1, h2, h3, h4 { margin-top: 0; font-weight: 700; }");
            sb.AppendLine("        .meta-info { color: var(--text-muted); font-size: 0.9em; margin-top: 8px; }");
            
            // Scorecard & Disciplines
            sb.AppendLine("        .scorecard-banner { background: var(--card-bg); border: 1px solid var(--card-border); border-radius: 12px; padding: 25px; margin-bottom: 30px; }");
            sb.AppendLine("        .scorecard-top { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 20px; }");
            sb.AppendLine("        .trend-pill { display: inline-flex; align-items: center; padding: 4px 12px; border-radius: 20px; font-size: 0.85em; font-weight: 600; background: #1e3a5f; color: #60a5fa; margin-top: 6px; }");
            sb.AppendLine("        .gauge-container { display: flex; align-items: center; gap: 20px; }");
            sb.AppendLine($"        .gauge {{ width: 110px; height: 110px; border-radius: 50%; background: conic-gradient({gradeColor} {healthScore.OverallScore}%, #334155 0); display: flex; justify-content: center; align-items: center; position: relative; }}");
            sb.AppendLine("        .gauge::before { content: ''; width: 85px; height: 85px; border-radius: 50%; background-color: var(--card-bg); position: absolute; }");
            sb.AppendLine("        .gauge-value { position: relative; font-size: 2em; font-weight: 800; color: #fff; }");
            sb.AppendLine("        .discipline-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 15px; margin-top: 25px; }");
            sb.AppendLine("        .discipline-card { background: #0f172a; border: 1px solid var(--card-border); border-radius: 8px; padding: 15px; }");
            sb.AppendLine("        .discipline-name { font-size: 0.85em; color: var(--text-muted); text-transform: uppercase; letter-spacing: 0.5px; }");
            sb.AppendLine("        .discipline-score { font-size: 1.8em; font-weight: 800; margin-top: 4px; }");
            
            // Severity Pill Summary
            sb.AppendLine("        .severity-pills { display: flex; flex-wrap: wrap; gap: 10px; margin: 25px 0 35px 0; }");
            sb.AppendLine("        .sev-pill { padding: 6px 14px; border-radius: 6px; font-weight: 700; font-size: 0.85em; display: inline-flex; align-items: center; }");
            sb.AppendLine("        .sev-critical { background: rgba(239, 68, 68, 0.2); color: #f87171; border: 1px solid #ef4444; }");
            sb.AppendLine("        .sev-error { background: rgba(245, 158, 11, 0.2); color: #fbbf24; border: 1px solid #f59e0b; }");
            sb.AppendLine("        .sev-warning { background: rgba(234, 179, 8, 0.15); color: #fde047; border: 1px solid #eab308; }");
            sb.AppendLine("        .sev-info { background: rgba(56, 189, 248, 0.15); color: #7dd3fc; border: 1px solid #38bdf8; }");

            // Cards & Tables
            sb.AppendLine("        .cards-row { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 15px; margin-bottom: 40px; }");
            sb.AppendLine("        .card { background-color: var(--card-bg); border-radius: 8px; padding: 18px; border: 1px solid var(--card-border); border-top-width: 4px; }");
            sb.AppendLine("        .chart-container { width: 100%; max-width: 700px; margin: 0 auto 40px auto; background-color: var(--card-bg); padding: 25px; border-radius: 12px; border: 1px solid var(--card-border); }");
            sb.AppendLine("        .checker-section { background-color: var(--card-bg); border-radius: 10px; padding: 22px; margin-bottom: 25px; border: 1px solid var(--card-border); }");
            sb.AppendLine("        .score-bar-bg { width: 100%; background-color: #334155; height: 8px; border-radius: 4px; margin: 10px 0 15px 0; overflow: hidden; }");
            sb.AppendLine("        .score-bar-fill { height: 100%; border-radius: 4px; }");
            sb.AppendLine("        table { width: 100%; border-collapse: collapse; margin-top: 15px; font-size: 0.9em; }");
            sb.AppendLine("        th, td { padding: 10px 12px; text-align: left; border-bottom: 1px solid var(--card-border); }");
            sb.AppendLine("        th { background-color: #0f172a; color: var(--text-muted); font-weight: 600; text-transform: uppercase; font-size: 0.75em; letter-spacing: 0.5px; }");
            sb.AppendLine("        code { background: #0f172a; padding: 2px 6px; border-radius: 4px; font-family: monospace; font-size: 0.9em; color: #38bdf8; border: 1px solid #334155; }");
            sb.AppendLine("        .badge { padding: 3px 8px; border-radius: 4px; font-size: 0.75em; font-weight: 700; text-transform: uppercase; }");
            sb.AppendLine("        .badge-info { background-color: #0284c7; color: white; }");
            sb.AppendLine("        .badge-warning { background-color: #d97706; color: white; }");
            sb.AppendLine("        .badge-error { background-color: #ea580c; color: white; }");
            sb.AppendLine("        .badge-critical { background-color: #dc2626; color: white; }");
            sb.AppendLine("        footer { text-align: center; padding: 30px; color: var(--text-muted); font-size: 0.85em; border-top: 1px solid var(--card-border); margin-top: 50px; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            // Header
            sb.AppendLine("    <header>");
            sb.AppendLine($"        <h1>BIM Model Health Scorecard</h1>");
            sb.AppendLine($"        <div class=\"meta-info\">");
            sb.AppendLine($"            Model: <strong>{System.Net.WebUtility.HtmlEncode(healthScore.ModelName)}</strong> &bull; {System.Net.WebUtility.HtmlEncode(healthScore.ModelPath)}<br/>");
            sb.AppendLine($"            Audit Date: {healthScore.Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"        </div>");
            sb.AppendLine("    </header>");

            sb.AppendLine("    <div class=\"container\">");

            // Scorecard Banner
            sb.AppendLine("        <div class=\"scorecard-banner\">");
            sb.AppendLine("            <div class=\"scorecard-top\">");
            sb.AppendLine("                <div>");
            sb.AppendLine("                    <span style=\"font-size: 0.85em; font-weight: 700; color: #38bdf8; text-transform: uppercase;\">Project Scorecard</span>");
            sb.AppendLine($"                    <h2 style=\"margin: 4px 0 0 0;\">{System.Net.WebUtility.HtmlEncode(healthScore.ModelName)}</h2>");
            if (healthScore.Trend != null && healthScore.Trend.HasHistory)
            {
                sb.AppendLine($"                    <div class=\"trend-pill\">{System.Net.WebUtility.HtmlEncode(healthScore.Trend.SummaryText)}</div>");
            }
            else
            {
                sb.AppendLine("                    <div class=\"trend-pill\">Baseline Health Check</div>");
            }
            sb.AppendLine("                </div>");

            sb.AppendLine("                <div class=\"gauge-container\">");
            sb.AppendLine("                    <div>");
            sb.AppendLine("                        <div style=\"font-size: 0.8em; color: var(--text-muted); text-transform: uppercase;\">Overall Health</div>");
            sb.AppendLine($"                        <div style=\"font-size: 1.4em; font-weight: 800; color: {gradeColor};\">{healthScore.Grade}</div>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                    <div class=\"gauge\">");
            sb.AppendLine($"                        <span class=\"gauge-value\">{healthScore.OverallScore:F0}</span>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");

            // Discipline Scores Grid
            sb.AppendLine("            <div class=\"discipline-grid\">");
            sb.AppendLine("                <div class=\"discipline-card\">");
            sb.AppendLine("                    <div class=\"discipline-name\">🏛 Architecture</div>");
            sb.AppendLine($"                    <div class=\"discipline-score\" style=\"color: {GetScoreColor(healthScore.ArchitectureScore)};\">{healthScore.ArchitectureScore:F0} <span style=\"font-size:0.5em;color:var(--text-muted);\">/100</span></div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div class=\"discipline-card\">");
            sb.AppendLine("                    <div class=\"discipline-name\">🏗 Structure</div>");
            sb.AppendLine($"                    <div class=\"discipline-score\" style=\"color: {GetScoreColor(healthScore.StructureScore)};\">{healthScore.StructureScore:F0} <span style=\"font-size:0.5em;color:var(--text-muted);\">/100</span></div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div class=\"discipline-card\">");
            sb.AppendLine("                    <div class=\"discipline-name\">⚡ MEP Systems</div>");
            sb.AppendLine($"                    <div class=\"discipline-score\" style=\"color: {GetScoreColor(healthScore.MepScore)};\">{healthScore.MepScore:F0} <span style=\"font-size:0.5em;color:var(--text-muted);\">/100</span></div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("                <div class=\"discipline-card\">");
            sb.AppendLine("                    <div class=\"discipline-name\">🌐 Coordination</div>");
            sb.AppendLine($"                    <div class=\"discipline-score\" style=\"color: {GetScoreColor(healthScore.CoordinationScore)};\">{healthScore.CoordinationScore:F0} <span style=\"font-size:0.5em;color:var(--text-muted);\">/100</span></div>");
            sb.AppendLine("                </div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");

            // Severity Level Summary Pills
            sb.AppendLine("        <div class=\"severity-pills\">");
            sb.AppendLine($"            <div class=\"sev-pill sev-critical\">🔴 {healthScore.CriticalCount} Critical Issues</div>");
            sb.AppendLine($"            <div class=\"sev-pill sev-error\">🟠 {healthScore.ErrorCount} Errors</div>");
            sb.AppendLine($"            <div class=\"sev-pill sev-warning\">🟡 {healthScore.WarningCount} Warnings</div>");
            sb.AppendLine($"            <div class=\"sev-pill sev-info\">🔵 {healthScore.InfoCount} Info Notices</div>");
            sb.AppendLine($"            <div class=\"sev-pill\" style=\"background: #1e293b; color: #fff; border: 1px solid var(--card-border);\">Total Findings: {healthScore.TotalIssues}</div>");
            sb.AppendLine("        </div>");

            // Chart
            sb.AppendLine("        <div class=\"chart-container\">");
            sb.AppendLine("            <h3 style=\"text-align:center;margin-bottom:15px;\">Category Health Profile</h3>");
            sb.AppendLine("            <canvas id=\"radarChart\"></canvas>");
            sb.AppendLine("        </div>");

            // Summary Cards
            sb.AppendLine("        <h2>Category Overview</h2>");
            sb.AppendLine("        <div class=\"cards-row\">");
            foreach (var result in healthScore.Results)
            {
                string resultColor = GetScoreColor(result.Score);
                sb.AppendLine($"            <div class=\"card\" style=\"border-top-color: {resultColor};\">");
                sb.AppendLine($"                <h3 style=\"font-size: 1.05em;\">{System.Net.WebUtility.HtmlEncode(result.DisplayName)}</h3>");
                sb.AppendLine($"                <div style=\"font-size: 1.5em; font-weight: 800; color: {resultColor};\">{result.Score:F0} <span style=\"font-size: 0.6em; color: var(--text-muted);\">/ 100</span></div>");
                sb.AppendLine($"                <p style=\"font-size: 0.85em; color: var(--text-muted); margin: 8px 0 0 0;\">{System.Net.WebUtility.HtmlEncode(result.Summary)}</p>");
                sb.AppendLine("            </div>");
            }
            sb.AppendLine("        </div>");

            // Detailed Findings Sections
            sb.AppendLine("        <h2>Detailed Audits &amp; Findings</h2>");
            foreach (var result in healthScore.Results)
            {
                string resultColor = GetScoreColor(result.Score);
                sb.AppendLine("        <div class=\"checker-section\">");
                sb.AppendLine("            <div style=\"display: flex; justify-content: space-between; align-items: center;\">");
                sb.AppendLine($"                <h3>{System.Net.WebUtility.HtmlEncode(result.DisplayName)}</h3>");
                sb.AppendLine($"                <span style=\"font-size: 1.3em; font-weight: 800; color: {resultColor};\">{result.Score:F0} / 100</span>");
                sb.AppendLine("            </div>");
                
                sb.AppendLine("            <div class=\"score-bar-bg\">");
                sb.AppendLine($"                <div class=\"score-bar-fill\" style=\"width: {result.Score}%; background-color: {resultColor};\"></div>");
                sb.AppendLine("            </div>");
                
                sb.AppendLine($"            <p style=\"color: var(--text-muted); font-size: 0.95em;\">{System.Net.WebUtility.HtmlEncode(result.Summary)}</p>");

                // Metrics
                if (result.Metrics != null && result.Metrics.Count > 0)
                {
                    sb.AppendLine("            <h4>Metrics</h4>");
                    sb.AppendLine("            <ul style=\"color: var(--text-muted); font-size: 0.9em;\">");
                    foreach (var kvp in result.Metrics)
                    {
                        if (kvp.Value is Dictionary<string, object> subDict)
                        {
                            sb.AppendLine($"                <li><strong>{System.Net.WebUtility.HtmlEncode(kvp.Key)}:</strong>");
                            sb.AppendLine("                    <ul>");
                            foreach (var sub in subDict)
                            {
                                sb.AppendLine($"                        <li>{System.Net.WebUtility.HtmlEncode(sub.Key)}: <strong>{System.Net.WebUtility.HtmlEncode(sub.Value?.ToString() ?? "")}</strong></li>");
                            }
                            sb.AppendLine("                    </ul>");
                            sb.AppendLine("                </li>");
                        }
                        else
                        {
                            sb.AppendLine($"                <li><strong>{System.Net.WebUtility.HtmlEncode(kvp.Key)}:</strong> {System.Net.WebUtility.HtmlEncode(kvp.Value?.ToString() ?? "")}</li>");
                        }
                    }
                    sb.AppendLine("            </ul>");
                }

                // Items Table
                if (result.Items != null && result.Items.Count > 0)
                {
                    sb.AppendLine($"            <h4>Findings ({result.Items.Count})</h4>");
                    sb.AppendLine("            <table>");
                    sb.AppendLine("                <thead><tr><th>Severity</th><th>Category</th><th>Description</th><th>Element ID</th></tr></thead>");
                    sb.AppendLine("                <tbody>");
                    
                    var sortedItems = result.Items.OrderByDescending(i => i.Severity).ToList();
                    
                    foreach (var item in sortedItems)
                    {
                        string badgeClass = GetBadgeClass(item.Severity);
                        string elemDisplay = string.IsNullOrWhiteSpace(item.ElementId) 
                            ? "-" 
                            : $"<code>{System.Net.WebUtility.HtmlEncode(item.ElementId)}</code>";

                        sb.AppendLine("                <tr>");
                        sb.AppendLine($"                    <td><span class=\"badge {badgeClass}\">{item.Severity}</span></td>");
                        sb.AppendLine($"                    <td><strong>{System.Net.WebUtility.HtmlEncode(item.Category ?? "")}</strong></td>");
                        sb.AppendLine($"                    <td>{System.Net.WebUtility.HtmlEncode(item.Description ?? "")}</td>");
                        sb.AppendLine($"                    <td>{elemDisplay}</td>");
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
            sb.AppendLine($"        Generated by BIM Model Health Checker for Autodesk Revit &bull; {healthScore.Timestamp:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("    </footer>");

            // Chart JS
            var labels = string.Join(", ", healthScore.Results.Select(r => $"\"{r.DisplayName}\""));
            var data = string.Join(", ", healthScore.Results.Select(r => r.Score.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));
            
            sb.AppendLine("    <script>");
            sb.AppendLine("        const ctx = document.getElementById('radarChart').getContext('2d');");
            sb.AppendLine("        new Chart(ctx, {");
            sb.AppendLine("            type: 'radar',");
            sb.AppendLine("            data: {");
            sb.AppendLine($"                labels: [{labels}],");
            sb.AppendLine("                datasets: [{");
            sb.AppendLine("                    label: 'Health Score',");
            sb.AppendLine($"                    data: [{data}],");
            sb.AppendLine("                    backgroundColor: 'rgba(34, 197, 94, 0.2)',");
            sb.AppendLine("                    borderColor: 'rgba(34, 197, 94, 1)',");
            sb.AppendLine("                    pointBackgroundColor: '#22c55e',");
            sb.AppendLine("                    borderWidth: 2");
            sb.AppendLine("                }]");
            sb.AppendLine("            },");
            sb.AppendLine("            options: {");
            sb.AppendLine("                responsive: true,");
            sb.AppendLine("                scales: {");
            sb.AppendLine("                    r: {");
            sb.AppendLine("                        angleLines: { color: '#334155' },");
            sb.AppendLine("                        grid: { color: '#334155' },");
            sb.AppendLine("                        pointLabels: { color: '#94a3b8', font: { size: 11 } },");
            sb.AppendLine("                        ticks: { backdropColor: 'transparent', color: '#64748b', stepSize: 20 },");
            sb.AppendLine("                        min: 0,");
            sb.AppendLine("                        max: 100");
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

        public static void SaveToFile(HealthScore healthScore, string filePath)
        {
            string html = Generate(healthScore);
            File.WriteAllText(filePath, html, Encoding.UTF8);
        }

        private static string GetGradeColor(HealthGrade grade)
        {
            switch (grade)
            {
                case HealthGrade.Healthy: return "#22c55e";
                case HealthGrade.NeedsAttention: return "#f59e0b";
                case HealthGrade.Critical: return "#ef4444";
                default: return "#94a3b8";
            }
        }

        private static string GetScoreColor(double score)
        {
            if (score >= 80) return "#22c55e";
            if (score >= 60) return "#f59e0b";
            return "#ef4444";
        }

        private static string GetBadgeClass(Severity severity)
        {
            switch (severity)
            {
                case Severity.Critical: return "badge-critical";
                case Severity.Error: return "badge-error";
                case Severity.Warning: return "badge-warning";
                case Severity.Info: return "badge-info";
                default: return "badge-info";
            }
        }
    }
}
