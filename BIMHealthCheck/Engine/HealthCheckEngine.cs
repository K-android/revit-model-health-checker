using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using BIMHealthCheck.Checkers;

namespace BIMHealthCheck.Engine
{
    /// <summary>
    /// Orchestrator class that executes all comprehensive health checkers,
    /// computes discipline scorecards, and computes historical trends.
    /// </summary>
    public class HealthCheckEngine
    {
        private readonly List<IHealthChecker> _checkers;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthCheckEngine"/> class
        /// and registers all industry-standard health checkers.
        /// </summary>
        public HealthCheckEngine()
        {
            _checkers = new List<IHealthChecker>
            {
                new WarningsChecker(),
                new RoomAndTagHealthChecker(),
                new DuplicateMarksChecker(),
                new CadImportsChecker(),
                new ModelGroupsChecker(),
                new InPlaceFamiliesChecker(),
                new GenericModelAbuseChecker(),
                new FamilyHealthChecker(),
                new WorksetChecker(),
                new NamingConventionChecker(),
                new LinkedFilesChecker(),
                new SharedParameterChecker(),
                new UnusedElementsChecker(),
                new ModelMetricsChecker()
            };
        }

        /// <summary>
        /// Runs all registered health checks against the provided document.
        /// Computes overall score, discipline scorecards, and historical trends.
        /// </summary>
        /// <param name="doc">The active Revit document.</param>
        /// <returns>A comprehensive HealthScore object.</returns>
        public HealthScore RunAllChecks(Document doc)
        {
            HealthScore score = new HealthScore
            {
                ModelName = doc.Title,
                ModelPath = doc.PathName,
                Timestamp = DateTime.Now,
                Results = new List<CheckResult>()
            };

            foreach (var checker in _checkers)
            {
                try
                {
                    CheckResult result = checker.Run(doc);
                    if (result != null)
                    {
                        score.Results.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    score.Results.Add(new CheckResult
                    {
                        CheckerName = checker.Name,
                        DisplayName = checker.DisplayName,
                        Score = 0,
                        Weight = checker.Weight,
                        Summary = $"Error during analysis: {ex.Message}"
                    });
                }
            }

            // Compute overall weighted score and discipline scores
            score.ComputeOverallScore();

            // Compute and record historical trend
            score.Trend = HistoryManager.ComputeTrend(score.ModelName, score.OverallScore, score.Timestamp);
            HistoryManager.SaveSnapshot(score);

            return score;
        }
    }
}
