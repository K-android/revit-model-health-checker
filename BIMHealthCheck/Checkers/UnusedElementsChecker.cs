using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Audits unplaced and unreferenced views (sections, elevations, callouts, floor plans
    /// that are not placed on sheets), as well as unused view templates.
    /// </summary>
    public class UnusedElementsChecker : IHealthChecker
    {
        public string Name => "UnusedElementsChecker";
        public string DisplayName => "Unreferenced Views & Templates";
        public double Weight => 0.08;

        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                // 1. Gather all views placed on sheets
                var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().ToList();
                var placedViews = new HashSet<ElementId>();
                foreach (var sheet in sheets)
                {
                    foreach (var vId in sheet.GetAllPlacedViews())
                    {
                        placedViews.Add(vId);
                    }
                }

                // Check doc views (exclude templates, schedules, internal, legends, 3D views)
                var docViews = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate &&
                                v.ViewType != ViewType.ThreeD &&
                                v.ViewType != ViewType.Schedule &&
                                v.ViewType != ViewType.Legend &&
                                v.ViewType != ViewType.DraftingView &&
                                v.ViewType != ViewType.Internal)
                    .ToList();

                var unreferencedViews = docViews.Where(v => !placedViews.Contains(v.Id)).ToList();
                int unreferencedCount = unreferencedViews.Count;

                // Group by view type
                int unplacedPlans = 0;
                int unplacedSections = 0;
                int unplacedElevations = 0;

                foreach (var v in unreferencedViews)
                {
                    if (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.CeilingPlan) unplacedPlans++;
                    else if (v.ViewType == ViewType.Section) unplacedSections++;
                    else if (v.ViewType == ViewType.Elevation) unplacedElevations++;
                }

                // Add up to 40 unreferenced views to items
                foreach (var v in unreferencedViews.Take(40))
                {
                    result.Items.Add(new CheckItem
                    {
                        Description = $"Unreferenced View: \"{v.Name}\" ({v.ViewType} not on any sheet)",
                        Severity = Severity.Info,
                        ElementId = v.Id.ToString(),
                        Category = "Unreferenced Views"
                    });
                }

                if (unreferencedCount > 40)
                {
                    result.Items.Add(new CheckItem
                    {
                        Description = $"... and {unreferencedCount - 40} more unreferenced views (total: {unreferencedCount})",
                        Severity = Severity.Info,
                        ElementId = string.Empty,
                        Category = "Unreferenced Views"
                    });
                }

                // 2. View Templates audit
                var allTemplates = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => v.IsTemplate).ToList();
                var usedTemplateIds = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.ViewTemplateId != ElementId.InvalidElementId)
                    .Select(v => v.ViewTemplateId)
                    .ToHashSet();

                var unusedTemplates = allTemplates.Where(t => !usedTemplateIds.Contains(t.Id)).ToList();
                int unusedTemplatesCount = unusedTemplates.Count;

                foreach (var t in unusedTemplates.Take(15))
                {
                    result.Items.Add(new CheckItem
                    {
                        Description = $"Unused View Template: \"{t.Name}\" (not assigned to any active view)",
                        Severity = Severity.Info,
                        ElementId = t.Id.ToString(),
                        Category = "View Templates"
                    });
                }

                // Realistic scoring:
                // Having working views is standard; only deduct if unplaced count > 25
                double unplacedDeduction = 0;
                if (unreferencedCount > 25)
                {
                    unplacedDeduction = Math.Min(25, (unreferencedCount - 25) * 0.4);
                }

                double unusedTemplatesDeduction = 0;
                if (unusedTemplatesCount > 15)
                {
                    unusedTemplatesDeduction = Math.Min(10, (unusedTemplatesCount - 15) * 0.2);
                }

                double totalDeduction = unplacedDeduction + unusedTemplatesDeduction;
                result.Score = Math.Max(0, Math.Round(100.0 - totalDeduction, 1));

                result.Metrics["TotalSheets"] = sheets.Count;
                result.Metrics["PlacedViews"] = placedViews.Count;
                result.Metrics["UnreferencedViewsTotal"] = unreferencedCount;
                result.Metrics["UnreferencedSections"] = unplacedSections;
                result.Metrics["UnreferencedPlans"] = unplacedPlans;
                result.Metrics["UnreferencedElevations"] = unplacedElevations;
                result.Metrics["TotalViewTemplates"] = allTemplates.Count;
                result.Metrics["UnusedViewTemplates"] = unusedTemplatesCount;

                result.Summary = $"Audited views & sheets: {placedViews.Count} placed views, {unreferencedCount} unreferenced working views, and {unusedTemplatesCount} unused templates.";
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during unreferenced views check: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
