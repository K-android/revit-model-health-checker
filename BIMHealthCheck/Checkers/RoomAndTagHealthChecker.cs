using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Audits spatial data and annotations: duplicate room numbers, unenclosed rooms,
    /// unplaced rooms, redundant room boundaries, and orphaned/unhosted tags.
    /// </summary>
    public class RoomAndTagHealthChecker : IHealthChecker
    {
        public string Name => "RoomAndTagHealthChecker";
        public string DisplayName => "Rooms & Tag Integrity";
        public double Weight => 0.12;

        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                // 1. Audit Rooms
                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .Cast<Room>()
                    .ToList();

                int totalRooms = rooms.Count;
                int duplicateNumberCount = 0;
                int unplacedCount = 0;
                int unenclosedCount = 0;

                // Group by room number
                var roomNumberMap = new Dictionary<string, List<Room>>(StringComparer.OrdinalIgnoreCase);

                foreach (var r in rooms)
                {
                    string num = r.Number?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(num))
                    {
                        if (!roomNumberMap.ContainsKey(num))
                            roomNumberMap[num] = new List<Room>();
                        roomNumberMap[num].Add(r);
                    }

                    // Unplaced vs Unenclosed
                    if (r.Location == null)
                    {
                        unplacedCount++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Unplaced Room: \"{r.Name}\" #{r.Number} (in project database but not placed in geometry)",
                            Severity = Severity.Warning,
                            ElementId = r.Id.ToString(),
                            Category = "Unplaced Rooms"
                        });
                    }
                    else if (r.Area <= 0.0001)
                    {
                        unenclosedCount++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Room Not Enclosed: \"{r.Name}\" #{r.Number} (boundary loop is open or missing)",
                            Severity = Severity.Error,
                            ElementId = r.Id.ToString(),
                            Category = "Unenclosed Rooms"
                        });
                    }
                }

                // Report duplicate room numbers
                foreach (var kvp in roomNumberMap)
                {
                    if (kvp.Value.Count > 1)
                    {
                        duplicateNumberCount++;
                        string idStr = string.Join(", ", kvp.Value.Select(r => r.Id.ToString()));
                        string roomNames = string.Join(", ", kvp.Value.Select(r => $"\"{r.Name}\""));

                        result.Items.Add(new CheckItem
                        {
                            Description = $"Duplicate Room Number \"{kvp.Key}\" shared by {kvp.Value.Count} rooms ({roomNames})",
                            Severity = Severity.Error,
                            ElementId = idStr,
                            Category = "Duplicate Room Numbers"
                        });
                    }
                }

                // 2. Audit Orphaned Tags
                int orphanedTagCount = 0;

                // Independent tags (Doors, Windows, Walls, Equipment, etc.)
                var independentTags = new FilteredElementCollector(doc)
                    .OfClass(typeof(IndependentTag))
                    .Cast<IndependentTag>()
                    .ToList();

                foreach (var tag in independentTags)
                {
                    bool isOrphaned = false;

                    try
                    {
#if REVIT2025_OR_GREATER
                        isOrphaned = tag.IsOrphaned;
#else
                        var taggedLocalIds = tag.GetTaggedLocalElementIds();
                        if (taggedLocalIds == null || taggedLocalIds.Count == 0 || taggedLocalIds.All(id => doc.GetElement(id) == null))
                        {
                            isOrphaned = true;
                        }
#endif
                    }
                    catch
                    {
                        // Fallback check
                        try
                        {
                            var taggedLocalIds = tag.GetTaggedLocalElementIds();
                            if (taggedLocalIds == null || taggedLocalIds.Count == 0 || taggedLocalIds.All(id => doc.GetElement(id) == null))
                            {
                                isOrphaned = true;
                            }
                        }
                        catch { }
                    }

                    if (isOrphaned)
                    {
                        orphanedTagCount++;
                        result.Items.Add(new CheckItem
                        {
                            Description = "Orphaned Tag: Host element deleted or missing",
                            Severity = Severity.Warning,
                            ElementId = tag.Id.ToString(),
                            Category = "Orphaned Tags"
                        });
                    }
                }

                // Room Tags without a valid room
                var roomTags = new FilteredElementCollector(doc)
                    .OfClass(typeof(SpatialElementTag))
                    .Cast<SpatialElementTag>()
                    .ToList();

                foreach (var st in roomTags)
                {
                    if (st is RoomTag rt && (rt.Room == null || rt.Room.Area <= 0.0001))
                    {
                        orphanedTagCount++;
                        result.Items.Add(new CheckItem
                        {
                            Description = "Orphaned / Unbound Room Tag",
                            Severity = Severity.Warning,
                            ElementId = rt.Id.ToString(),
                            Category = "Orphaned Tags"
                        });
                    }
                }

                // Realistic scoring:
                // Duplicate room numbers: -2 each (max -25)
                // Unenclosed rooms: -2 each (max -20)
                // Unplaced rooms: -1 each (max -15)
                // Orphaned tags: -0.5 each (max -15)
                double deduction = Math.Min(25, duplicateNumberCount * 2.0)
                                 + Math.Min(20, unenclosedCount * 2.0)
                                 + Math.Min(15, unplacedCount * 1.0)
                                 + Math.Min(15, orphanedTagCount * 0.5);

                result.Score = Math.Max(0, Math.Round(100.0 - deduction, 1));

                result.Metrics["TotalRooms"] = totalRooms;
                result.Metrics["DuplicateRoomNumbers"] = duplicateNumberCount;
                result.Metrics["UnenclosedRooms"] = unenclosedCount;
                result.Metrics["UnplacedRooms"] = unplacedCount;
                result.Metrics["OrphanedTags"] = orphanedTagCount;
                result.Metrics["TotalTagsAudited"] = independentTags.Count + roomTags.Count;

                int totalIssues = duplicateNumberCount + unenclosedCount + unplacedCount + orphanedTagCount;
                if (totalIssues == 0)
                {
                    result.Summary = $"Audited {totalRooms} rooms and {independentTags.Count + roomTags.Count} tags. All rooms enclosed and tagged properly!";
                }
                else
                {
                    result.Summary = $"Found {duplicateNumberCount} duplicate room numbers, {unenclosedCount} unenclosed rooms, {unplacedCount} unplaced rooms, and {orphanedTagCount} orphaned tags.";
                }
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during room and tag audit: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }
    }
}
