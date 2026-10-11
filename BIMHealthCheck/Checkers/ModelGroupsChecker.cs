using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Audits model groups: identifies single-instance group types, unused group types,
    /// and groups with high instance counts that impact model regeneration time.
    /// </summary>
    public class ModelGroupsChecker : IHealthChecker
    {
        public string Name => "ModelGroupsChecker";
        public string DisplayName => "Model Groups & Performance";
        public double Weight => 0.08;

        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                var groupInstances = new FilteredElementCollector(doc)
                    .OfClass(typeof(Group))
                    .Cast<Group>()
                    .ToList();

                var groupTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(GroupType))
                    .Cast<GroupType>()
                    .ToList();

                int totalGroupInstances = groupInstances.Count;
                int totalGroupTypes = groupTypes.Count;

                int singleInstanceTypes = 0;
                int unusedTypes = 0;
                int highCountTypes = 0;

                // Group instances by their GroupType
                var instancesByType = new Dictionary<ElementId, List<Group>>();
                foreach (var gt in groupTypes)
                {
                    instancesByType[gt.Id] = new List<Group>();
                }

                foreach (var g in groupInstances)
                {
                    var typeId = g.GetTypeId();
                    if (instancesByType.ContainsKey(typeId))
                    {
                        instancesByType[typeId].Add(g);
                    }
                }

                foreach (var kvp in instancesByType)
                {
                    var gt = doc.GetElement(kvp.Key) as GroupType;
                    string gtName = gt?.Name ?? "Group";
                    int count = kvp.Value.Count;

                    if (count == 0)
                    {
                        unusedTypes++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Unused Group Type: \"{gtName}\" (0 instances placed, can be purged)",
                            Severity = Severity.Info,
                            ElementId = gt?.Id.ToString() ?? "",
                            Category = "Unused Groups"
                        });
                    }
                    else if (count == 1)
                    {
                        singleInstanceTypes++;
                        string instId = kvp.Value[0].Id.ToString();
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Single-Instance Group: \"{gtName}\" (Consider ungrouping to reduce database overhead)",
                            Severity = Severity.Warning,
                            ElementId = instId,
                            Category = "Single-Instance Groups"
                        });
                    }
                    else if (count > 50)
                    {
                        highCountTypes++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"High Instance Group: \"{gtName}\" ({count} instances placed - monitor performance)",
                            Severity = Severity.Info,
                            ElementId = gt?.Id.ToString() ?? "",
                            Category = "High Instance Groups"
                        });
                    }
                }

                // Realistic scoring:
                // - Single-instance groups: -1 each (max -20)
                // - Unused group types: -0.5 each (max -10)
                double deduction = Math.Min(20, singleInstanceTypes * 1.0)
                                 + Math.Min(10, unusedTypes * 0.5);

                result.Score = Math.Max(0, Math.Round(100.0 - deduction, 1));

                result.Metrics["TotalGroupInstances"] = totalGroupInstances;
                result.Metrics["TotalGroupTypes"] = totalGroupTypes;
                result.Metrics["SingleInstanceGroupTypes"] = singleInstanceTypes;
                result.Metrics["UnusedGroupTypes"] = unusedTypes;
                result.Metrics["HighCountGroupTypes"] = highCountTypes;

                if (totalGroupTypes == 0)
                {
                    result.Summary = "0 model groups in project. Clean geometry organization!";
                }
                else
                {
                    result.Summary = $"Found {totalGroupInstances} group instances across {totalGroupTypes} types ({singleInstanceTypes} single-instance, {unusedTypes} unused).";
                }
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during model groups audit: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
