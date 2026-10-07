using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Checks for unused or unplaced elements in the Revit model.
    /// </summary>
    public class UnusedElementsChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "UnusedElementsChecker";
        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "Unused Elements";
        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.15;

        /// <summary>
        /// Runs the health check for unused elements.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with the findings.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>();
                var placedViews = new HashSet<ElementId>();
                foreach (var sheet in sheets)
                {
                    foreach (var vId in sheet.GetAllPlacedViews())
                    {
                        placedViews.Add(vId);
                    }
                }

                var allViews = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                    .Where(v => !v.IsTemplate && v.ViewType != ViewType.ThreeD && v.ViewType != ViewType.Schedule).ToList();
                
                int unplacedViewsCount = 0;
                foreach (var v in allViews)
                {
                    if (!placedViews.Contains(v.Id))
                    {
                        unplacedViewsCount++;
                        result.Score -= 1;
                        result.Items.Add(new CheckItem { Description = $"Unplaced View: {v.Name}", Severity = Severity.Warning, ElementId = v.Id.ToString(), Category = "View" });
                    }
                }

                var rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).Cast<Room>().ToList();
                int unplacedRoomsCount = 0;
                foreach (var room in rooms)
                {
                    if (room.Area == 0 || room.Location == null)
                    {
                        unplacedRoomsCount++;
                        result.Score -= 2;
                        result.Items.Add(new CheckItem { Description = $"Unplaced/Unenclosed Room: {room.Name}", Severity = Severity.Warning, ElementId = room.Id.ToString(), Category = "Room" });
                    }
                }

                var usedTemplates = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
                    .Where(v => v.ViewTemplateId != ElementId.InvalidElementId)
                    .Select(v => v.ViewTemplateId).ToHashSet();

                var allTemplates = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => v.IsTemplate).ToList();
                int unusedTemplatesCount = 0;
                foreach (var t in allTemplates)
                {
                    if (!usedTemplates.Contains(t.Id))
                    {
                        unusedTemplatesCount++;
                        result.Score -= 1;
                        result.Items.Add(new CheckItem { Description = $"Unused View Template: {t.Name}", Severity = Severity.Warning, ElementId = t.Id.ToString(), Category = "ViewTemplate" });
                    }
                }

                var symbols = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).Cast<FamilySymbol>().Take(100).ToList();
                int purgeableCount = 0;
                foreach (var sym in symbols)
                {
                    var filter = new FamilyInstanceFilter(doc, sym.Id);
                    int count = new FilteredElementCollector(doc).WherePasses(filter).GetElementCount();
                    if (count == 0)
                    {
                        purgeableCount++;
                        result.Score -= 0.5;
                    }
                }

                result.Score = Math.Max(0, result.Score);

                result.Metrics["UnplacedViews"] = unplacedViewsCount;
                result.Metrics["UnplacedRooms"] = unplacedRoomsCount;
                result.Metrics["UnusedViewTemplates"] = unusedTemplatesCount;
                result.Metrics["PurgeableTypes"] = purgeableCount;

                result.Summary = $"Found {unplacedViewsCount} unplaced views, {unplacedRoomsCount} unplaced rooms, {unusedTemplatesCount} unused templates.";
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during check: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
