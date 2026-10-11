using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMHealthCheck.Engine
{
    /// <summary>
    /// Overall health grade for the model.
    /// </summary>
    public enum HealthGrade
    {
        Healthy,
        NeedsAttention,
        Critical
    }

    /// <summary>
    /// Snapshot of a previous health check run for historical trend tracking.
    /// </summary>
    public class ScoreSnapshot
    {
        public DateTime Timestamp { get; set; }
        public double OverallScore { get; set; }
        public string Grade { get; set; }
        public double ArchitectureScore { get; set; }
        public double StructureScore { get; set; }
        public double MepScore { get; set; }
        public double CoordinationScore { get; set; }
        public int TotalIssues { get; set; }
    }

    /// <summary>
    /// Trend comparison between current health check and previous runs.
    /// </summary>
    public class ModelTrend
    {
        public bool HasHistory { get; set; }
        public double PreviousScore { get; set; }
        public double DeltaScore { get; set; }
        public DateTime? PreviousDate { get; set; }
        public string SummaryText { get; set; }
        public List<ScoreSnapshot> HistoryTimeline { get; set; } = new List<ScoreSnapshot>();
    }

    /// <summary>
    /// Aggregated health score for the entire model, including discipline scorecards and trends.
    /// </summary>
    public class HealthScore
    {
        /// <summary>Weighted overall score (0-100).</summary>
        public double OverallScore { get; set; }

        /// <summary>Health grade based on the overall score.</summary>
        public HealthGrade Grade { get; set; }

        /// <summary>Discipline score: Architecture (0-100).</summary>
        public double ArchitectureScore { get; set; }

        /// <summary>Discipline score: Structure (0-100).</summary>
        public double StructureScore { get; set; }

        /// <summary>Discipline score: MEP (0-100).</summary>
        public double MepScore { get; set; }

        /// <summary>Discipline score: BIM Coordination & Management (0-100).</summary>
        public double CoordinationScore { get; set; }

        /// <summary>Historical trend compared to previous checks.</summary>
        public ModelTrend Trend { get; set; } = new ModelTrend();

        /// <summary>Results from each individual health checker.</summary>
        public List<CheckResult> Results { get; set; } = new List<CheckResult>();

        /// <summary>When the health check was performed.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Name of the Revit model file.</summary>
        public string ModelName { get; set; }

        /// <summary>Full path of the Revit model file.</summary>
        public string ModelPath { get; set; }

        /// <summary>Total issues found across all checkers.</summary>
        public int TotalIssues => Results?.Sum(r => r.Items.Count) ?? 0;
        public int CriticalCount => Results?.Sum(r => r.CriticalCount) ?? 0;
        public int ErrorCount => Results?.Sum(r => r.ErrorCount) ?? 0;
        public int WarningCount => Results?.Sum(r => r.WarningCount) ?? 0;
        public int InfoCount => Results?.Sum(r => r.InfoCount) ?? 0;

        /// <summary>
        /// Determines the health grade from a numeric score.
        /// </summary>
        public static HealthGrade GetGrade(double score)
        {
            if (score >= 80) return HealthGrade.Healthy;
            if (score >= 60) return HealthGrade.NeedsAttention;
            return HealthGrade.Critical;
        }

        /// <summary>
        /// Computes the weighted overall score and discipline scores from individual results.
        /// </summary>
        public void ComputeOverallScore()
        {
            if (Results == null || Results.Count == 0)
            {
                OverallScore = 0;
                Grade = HealthGrade.Critical;
                return;
            }

            double totalWeight = Results.Sum(r => r.Weight);
            if (totalWeight <= 0)
            {
                OverallScore = Results.Average(r => r.Score);
            }
            else
            {
                OverallScore = Results.Sum(r => r.Score * r.Weight) / totalWeight;
            }

            OverallScore = Math.Round(OverallScore, 1);
            Grade = GetGrade(OverallScore);

            ComputeDisciplineScores();
        }

        private void ComputeDisciplineScores()
        {
            // Group checkers by discipline relevance
            var archCheckers = new[] { "RoomAndTagHealthChecker", "DuplicateMarksChecker", "GenericModelAbuseChecker", "NamingConventionChecker", "UnusedElementsChecker" };
            var structCheckers = new[] { "WarningsChecker", "FamilyHealthChecker", "ModelMetricsChecker" };
            var mepCheckers = new[] { "WarningsChecker", "DuplicateMarksChecker", "RoomAndTagHealthChecker" };
            var coordCheckers = new[] { "WarningsChecker", "CadImportsChecker", "LinkedFilesChecker", "WorksetChecker", "SharedParameterChecker", "ModelGroupsChecker", "InPlaceFamiliesChecker" };

            ArchitectureScore = CalculateSubScore(archCheckers);
            StructureScore = CalculateSubScore(structCheckers);
            MepScore = CalculateSubScore(mepCheckers);
            CoordinationScore = CalculateSubScore(coordCheckers);
        }

        private double CalculateSubScore(string[] checkerNames)
        {
            var matched = Results.Where(r => checkerNames.Contains(r.CheckerName)).ToList();
            if (matched.Count == 0) return OverallScore;

            double totalWeight = matched.Sum(r => r.Weight);
            if (totalWeight <= 0) return Math.Round(matched.Average(r => r.Score), 1);

            return Math.Round(matched.Sum(r => r.Score * r.Weight) / totalWeight, 1);
        }
    }
}
