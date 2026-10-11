using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Industry-aligned checker for identifying unplaced rooms, unplaced working views,
    /// and unused view templates. Uses realistic BIM health tolerances.
    /// </summary>
    public class UnusedElementsChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "UnusedElementsChecker";

        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "Unplaced & Unused Elements";

        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.15;

        /// <summary>
        /// Runs the health check for unused elements.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with findings and element IDs.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                // 1. Unplaced Views
                var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().ToList();
                var placedViews = new HashSet<ElementId>();
                foreach (var sheet in sheets)
                {
                    foreach (var vId in sheet.GetAllPlacedViews())
                    {
                        placedViews.Add(vId);
                    }
                }

                // Check doc views (exclude templates, schedules, internal, legends, 3D views which are typically working views)
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

                var unplacedViews = docViews.Where(v => !placedViews.Contains(v.Id)).ToList();
                int unplacedViewsCount = unplacedViews.Count;

                // Only add individual items up to 50 to prevent UI bloat
                foreach (var v in unplacedViews.Take(50))
                {
                    result.Items.Add(new CheckItem
                    {
                        Description = $"Unplaced Model View: {v.Name} ({v.ViewType})",
                        Severity = Severity.Info,
                        ElementId = v.Id.ToString(),
                        Category = "Unplaced Views"
                    });
                }

                if (unplacedViewsCount > 50)
                {
                    result.Items.Add(new CheckItem
                    {
                        Description = $"... and {unplacedViewsCount - 50} more unplaced views (total: {unplacedViewsCount})",
                        Severity = Severity.Info,
                        ElementId = string.Empty,
                        Category = "Unplaced Views"
                    });
                }

                // 2. Unplaced / Unenclosed Rooms (High BIM priority!)
                var rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).Cast<Room>().ToList();
                int unplacedRoomsCount = 0;
                foreach (var room in rooms)
                {
                    if (room.Area <= 0.0001 || room.Location == null)
                    {
                        unplacedRoomsCount++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Unplaced / Unenclosed Room: \"{room.Name}\" (Number: {room.Number})",
                            Severity = Severity.Warning,
                            ElementId = room.Id.ToString(),
                            Category = "Unplaced Rooms"
                        });
                    }
                }

                // 3. View Templates audit
                var allTemplates = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => v.IsTemplate).ToList();
                var usedTemplateIds = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.ViewTemplateId != ElementId.InvalidElementId)
                    .Select(v => v.ViewTemplateId)
                    .ToHashSet();

                var unusedTemplates = allTemplates.Where(t => !usedTemplateIds.Contains(t.Id)).ToList();
                int unusedTemplatesCount = unusedTemplates.Count;

                foreach (var t in unusedTemplates.Take(20))
                {
                    result.Items.Add(new CheckItem
                    {
                        Description = $"Unused View Template: {t.Name}",
                        Severity = Severity.Info,
                        ElementId = t.Id.ToString(),
                        Category = "View Templates"
                    });
                }

                // Realistic scoring:
                // - Unplaced rooms: -1.5 per unplaced room (capped at -25)
                // - Unplaced views: Having some working views is normal; only deduct if ratio is high or count > 20
                double unplacedViewsDeduction = 0;
                if (unplacedViewsCount > 20)
                {
                    unplacedViewsDeduction = Math.Min(20, (unplacedViewsCount - 20) * 0.3);
                }

                // - Unused templates: -0.2 per unused template over 10 (capped at -10)
                double unusedTemplatesDeduction = 0;
                if (unusedTemplatesCount > 10)
                {
                    unusedTemplatesDeduction = Math.Min(10, (unusedTemplatesCount - 10) * 0.2);
                }

                double roomDeduction = Math.Min(25, unplacedRoomsCount * 1.5);

                double totalDeduction = roomDeduction + unplacedViewsDeduction + unusedTemplatesDeduction;
                result.Score = Math.Max(0, Math.Round(100.0 - totalDeduction, 1));

                result.Metrics["TotalSheets"] = sheets.Count;
                result.Metrics["PlacedViews"] = placedViews.Count;
                result.Metrics["UnplacedViews"] = unplacedViewsCount;
                result.Metrics["UnplacedRooms"] = unplacedRoomsCount;
                result.Metrics["TotalRooms"] = rooms.Count;
                result.Metrics["TotalViewTemplates"] = allTemplates.Count;
                result.Metrics["UnusedViewTemplates"] = unusedTemplatesCount;

                result.Summary = $"Found {unplacedRoomsCount} unplaced rooms, {unplacedViewsCount} unplaced views, and {unusedTemplatesCount} unused templates.";
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during unused elements check: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
