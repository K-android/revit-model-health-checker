using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BIMHealthCheck.Engine
{
    /// <summary>
    /// Manages persistent storage and historical trend computation for model health checks.
    /// Stores run history under %APPDATA%\BIMHealthCheck\history\
    /// </summary>
    public static class HistoryManager
    {
        private static string GetHistoryDirectory()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "BIMHealthCheck", "history");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        private static string GetHistoryFilePath(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                modelName = "UnknownModel";

            string safeName = Regex.Replace(modelName, @"[^\w\.-]", "_");
            return Path.Combine(GetHistoryDirectory(), $"{safeName}_history.json");
        }

        /// <summary>
        /// Loads historical snapshots and computes the trend against the current score.
        /// </summary>
        public static ModelTrend ComputeTrend(string modelName, double currentScore, DateTime currentTime)
        {
            var trend = new ModelTrend();
            try
            {
                var history = LoadHistory(modelName);
                if (history != null && history.Count > 0)
                {
                    // Order by timestamp ascending
                    var ordered = history.OrderBy(h => h.Timestamp).ToList();
                    trend.HistoryTimeline = ordered;

                    // Get most recent previous run (before current run)
                    var lastRun = ordered.LastOrDefault(h => h.Timestamp < currentTime.AddSeconds(-2)) ?? ordered.Last();

                    trend.HasHistory = true;
                    trend.PreviousScore = lastRun.OverallScore;
                    trend.PreviousDate = lastRun.Timestamp;
                    trend.DeltaScore = Math.Round(currentScore - lastRun.OverallScore, 1);

                    string daysAgo = FormatDateDelta(lastRun.Timestamp, currentTime);
                    if (trend.DeltaScore > 0)
                    {
                        trend.SummaryText = $"+{trend.DeltaScore:F1} pts improvement vs {daysAgo} ({lastRun.OverallScore:F0})";
                    }
                    else if (trend.DeltaScore < 0)
                    {
                        trend.SummaryText = $"{trend.DeltaScore:F1} pts change vs {daysAgo} ({lastRun.OverallScore:F0})";
                    }
                    else
                    {
                        trend.SummaryText = $"Stable score (same as {daysAgo})";
                    }
                }
                else
                {
                    trend.HasHistory = false;
                    trend.SummaryText = "First baseline health check for this project.";
                }
            }
            catch (Exception ex)
            {
                trend.HasHistory = false;
                trend.SummaryText = $"Trend tracking unavailable: {ex.Message}";
            }

            return trend;
        }

        /// <summary>
        /// Records the current run into the model's history file.
        /// </summary>
        public static void SaveSnapshot(HealthScore score)
        {
            if (score == null || string.IsNullOrWhiteSpace(score.ModelName))
                return;

            try
            {
                var history = LoadHistory(score.ModelName) ?? new List<ScoreSnapshot>();

                // Add current snapshot
                history.Add(new ScoreSnapshot
                {
                    Timestamp = score.Timestamp,
                    OverallScore = score.OverallScore,
                    Grade = score.Grade.ToString(),
                    ArchitectureScore = score.ArchitectureScore,
                    StructureScore = score.StructureScore,
                    MepScore = score.MepScore,
                    CoordinationScore = score.CoordinationScore,
                    TotalIssues = score.TotalIssues
                });

                // Keep up to 50 most recent snapshots
                if (history.Count > 50)
                {
                    history = history.OrderByDescending(h => h.Timestamp).Take(50).OrderBy(h => h.Timestamp).ToList();
                }

                SaveHistory(score.ModelName, history);
            }
            catch
            {
                // Silently handle history write failures
            }
        }

        private static string FormatDateDelta(DateTime past, DateTime now)
        {
            var span = now - past;
            if (span.TotalDays >= 6.5 && span.TotalDays <= 7.5) return "last week";
            if (span.TotalDays >= 13.5 && span.TotalDays <= 14.5) return "2 weeks ago";
            if (span.TotalDays >= 28) return $"{Math.Round(span.TotalDays / 30)} month(s) ago";
            if (span.TotalDays >= 1) return $"{Math.Round(span.TotalDays)} day(s) ago";
            if (span.TotalHours >= 1) return $"{Math.Round(span.TotalHours)} hour(s) ago";
            return "previous run";
        }

        private static List<ScoreSnapshot> LoadHistory(string modelName)
        {
            string filePath = GetHistoryFilePath(modelName);
            var list = new List<ScoreSnapshot>();

            if (!File.Exists(filePath))
                return list;

            string json = File.ReadAllText(filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return list;

            // Simple robust regex-based parser (no external library required)
            var matches = Regex.Matches(json, @"\{[^{}]*\}");
            foreach (Match m in matches)
            {
                string block = m.Value;
                var snapshot = new ScoreSnapshot();

                var tsMatch = Regex.Match(block, @"""Timestamp""\s*:\s*""([^""]+)""");
                if (tsMatch.Success && DateTime.TryParse(tsMatch.Groups[1].Value, null, DateTimeStyles.RoundtripKind, out DateTime dt))
                    snapshot.Timestamp = dt;

                var scoreMatch = Regex.Match(block, @"""OverallScore""\s*:\s*([\d\.]+)");
                if (scoreMatch.Success && double.TryParse(scoreMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double sc))
                    snapshot.OverallScore = sc;

                var gradeMatch = Regex.Match(block, @"""Grade""\s*:\s*""([^""]+)""");
                if (gradeMatch.Success)
                    snapshot.Grade = gradeMatch.Groups[1].Value;

                var archMatch = Regex.Match(block, @"""ArchitectureScore""\s*:\s*([\d\.]+)");
                if (archMatch.Success && double.TryParse(archMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double arch))
                    snapshot.ArchitectureScore = arch;

                var structMatch = Regex.Match(block, @"""StructureScore""\s*:\s*([\d\.]+)");
                if (structMatch.Success && double.TryParse(structMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double str))
                    snapshot.StructureScore = str;

                var mepMatch = Regex.Match(block, @"""MepScore""\s*:\s*([\d\.]+)");
                if (mepMatch.Success && double.TryParse(mepMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double mep))
                    snapshot.MepScore = mep;

                var coordMatch = Regex.Match(block, @"""CoordinationScore""\s*:\s*([\d\.]+)");
                if (coordMatch.Success && double.TryParse(coordMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double coord))
                    snapshot.CoordinationScore = coord;

                var issuesMatch = Regex.Match(block, @"""TotalIssues""\s*:\s*(\d+)");
                if (issuesMatch.Success && int.TryParse(issuesMatch.Groups[1].Value, out int issues))
                    snapshot.TotalIssues = issues;

                list.Add(snapshot);
            }

            return list;
        }

        private static void SaveHistory(string modelName, List<ScoreSnapshot> snapshots)
        {
            string filePath = GetHistoryFilePath(modelName);
            var sb = new StringBuilder();
            sb.AppendLine("[");
            for (int i = 0; i < snapshots.Count; i++)
            {
                var s = snapshots[i];
                sb.AppendLine("  {");
                sb.AppendLine($"    \"Timestamp\": \"{s.Timestamp:o}\",");
                sb.AppendLine($"    \"OverallScore\": {s.OverallScore.ToString("F1", CultureInfo.InvariantCulture)},");
                sb.AppendLine($"    \"Grade\": \"{s.Grade}\",");
                sb.AppendLine($"    \"ArchitectureScore\": {s.ArchitectureScore.ToString("F1", CultureInfo.InvariantCulture)},");
                sb.AppendLine($"    \"StructureScore\": {s.StructureScore.ToString("F1", CultureInfo.InvariantCulture)},");
                sb.AppendLine($"    \"MepScore\": {s.MepScore.ToString("F1", CultureInfo.InvariantCulture)},");
                sb.AppendLine($"    \"CoordinationScore\": {s.CoordinationScore.ToString("F1", CultureInfo.InvariantCulture)},");
                sb.AppendLine($"    \"TotalIssues\": {s.TotalIssues}");
                sb.Append("  }");
                if (i < snapshots.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("]");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
    }
}
