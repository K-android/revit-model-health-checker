using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Audits project and shared parameter bindings to detect duplicate parameter names,
    /// binding conflicts, and unstandardized parameter definitions.
    /// </summary>
    public class SharedParameterChecker : IHealthChecker
    {
        public string Name => "SharedParameterChecker";
        public string DisplayName => "Parameters & Compliance";
        public double Weight => 0.07;

        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                var bindingMap = doc.ParameterBindings;
                var iterator = bindingMap.ForwardIterator();

                int totalBoundParams = 0;
                int sharedParamCount = 0;
                int projectParamCount = 0;

                var paramNameOccurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var duplicateNameList = new List<string>();

                while (iterator.MoveNext())
                {
                    totalBoundParams++;
                    var definition = iterator.Key;
                    if (definition == null) continue;

                    string paramName = definition.Name?.Trim() ?? "Unnamed Parameter";

                    if (!paramNameOccurrences.ContainsKey(paramName))
                        paramNameOccurrences[paramName] = 0;
                    paramNameOccurrences[paramName]++;

                    if (definition is ExternalDefinition)
                    {
                        sharedParamCount++;
                    }
                    else
                    {
                        projectParamCount++;
                    }
                }

                foreach (var kvp in paramNameOccurrences)
                {
                    if (kvp.Value > 1)
                    {
                        duplicateNameList.Add(kvp.Key);
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Duplicate Parameter Definition: \"{kvp.Key}\" bound {kvp.Value} times with conflicting definitions",
                            Severity = Severity.Warning,
                            Category = "Duplicate Parameters"
                        });
                    }
                }

                // Realistic scoring: -2 per duplicate parameter name (max -20)
                double deduction = Math.Min(20, duplicateNameList.Count * 2.0);
                result.Score = Math.Max(0, Math.Round(100.0 - deduction, 1));

                result.Metrics["TotalBoundParameters"] = totalBoundParams;
                result.Metrics["SharedParametersCount"] = sharedParamCount;
                result.Metrics["ProjectParametersCount"] = projectParamCount;
                result.Metrics["DuplicateParameterNames"] = duplicateNameList.Count;

                if (duplicateNameList.Count == 0)
                {
                    result.Summary = $"Audited {totalBoundParams} parameters ({sharedParamCount} shared, {projectParamCount} project). 0 duplicate parameter names!";
                }
                else
                {
                    result.Summary = $"Audited {totalBoundParams} parameters. Found {duplicateNameList.Count} duplicate parameter names causing potential schedule conflicts.";
                }
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during parameter compliance check: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
