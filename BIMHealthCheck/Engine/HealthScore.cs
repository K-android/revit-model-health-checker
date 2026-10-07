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
    /// Aggregated health score for the entire model.
    /// </summary>
    public class HealthScore
    {
        /// <summary>Weighted overall score (0-100).</summary>
        public double OverallScore { get; set; }

        /// <summary>Health grade based on the overall score.</summary>
        public HealthGrade Grade { get; set; }

        /// <summary>Results from each individual health checker.</summary>
        public List<CheckResult> Results { get; set; } = new List<CheckResult>();

        /// <summary>When the health check was performed.</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>Name of the Revit model file.</summary>
        public string ModelName { get; set; }

        /// <summary>Full path of the Revit model file.</summary>
        public string ModelPath { get; set; }

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
        /// Computes the weighted overall score from individual results.
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
        }
    }
}
