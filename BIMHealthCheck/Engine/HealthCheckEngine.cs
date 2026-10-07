using Autodesk.Revit.DB;
using BIMHealthCheck.Checkers;
using System;
using System.Collections.Generic;

namespace BIMHealthCheck.Engine
{
    /// <summary>
    /// Orchestrator class that runs all health checks and computes the final score.
    /// </summary>
    public class HealthCheckEngine
    {
        private readonly List<IHealthChecker> _checkers;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthCheckEngine"/> class
        /// and registers all health checkers.
        /// </summary>
        public HealthCheckEngine()
        {
            _checkers = new List<IHealthChecker>
            {
                new WarningsChecker(),
                new ModelMetricsChecker(),
                new UnusedElementsChecker(),
                new CadImportsChecker(),
                new InPlaceFamiliesChecker(),
                new WorksetChecker(),
                new NamingConventionChecker(),
                new LinkedFilesChecker()
            };
        }

        /// <summary>
        /// Runs all registered health checks against the provided document.
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

            score.ComputeOverallScore();
            return score;
        }
    }
}
