using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Checker for identifying elements that do not follow naming conventions.
    /// </summary>
    public class NamingConventionChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "NamingConventionChecker";
        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "Naming Conventions";
        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.15;

        /// <summary>
        /// Runs the health check for naming conventions.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with the findings.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                int totalChecked = 0;
                int nonCompliantViews = 0;
                int nonCompliantSheets = 0;
                int nonCompliantLevels = 0;
                int nonCompliantGrids = 0;
                int nonCompliantFamilies = 0;

                var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate).ToList();
                foreach (var v in views)
                {
                    totalChecked++;
                    if (v.Name.StartsWith("Copy of", StringComparison.OrdinalIgnoreCase) || v.Name.Contains("{") || v.Name.Contains("}"))
                    {
                        nonCompliantViews++;
                        result.Score -= 1;
                        result.Items.Add(new CheckItem { Description = $"Invalid View Name: {v.Name}", Severity = Severity.Warning, ElementId = v.Id.ToString(), Category = "View" });
                    }
                }

                var sheets = new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().ToList();
                var sheetRegex = new Regex("^[0-9]+$");
                foreach (var s in sheets)
                {
                    totalChecked++;
                    if (string.IsNullOrWhiteSpace(s.SheetNumber) || sheetRegex.IsMatch(s.SheetNumber))
                    {
                        nonCompliantSheets++;
                        result.Score -= 1;
                        result.Items.Add(new CheckItem { Description = $"Invalid Sheet Number: {s.SheetNumber}", Severity = Severity.Warning, ElementId = s.Id.ToString(), Category = "Sheet" });
                    }
                }

                var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
                var levelRegex = new Regex(@"^(Level \d+|L-\d+|[A-Za-z]+ \d+)$", RegexOptions.IgnoreCase);
                foreach (var l in levels)
                {
                    totalChecked++;
                    if (l.Name.IndexOf("copy", StringComparison.OrdinalIgnoreCase) >= 0 || Regex.IsMatch(l.Name, "^[0-9]+$") || !levelRegex.IsMatch(l.Name))
                    {
                        nonCompliantLevels++;
                        result.Score -= 3;
                        result.Items.Add(new CheckItem { Description = $"Invalid Level Name: {l.Name}", Severity = Severity.Warning, ElementId = l.Id.ToString(), Category = "Level" });
                    }
                }

                var grids = new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().ToList();
                var gridRegex = new Regex("^[A-Za-z0-9]{1,2}$");
                foreach (var g in grids)
                {
                    totalChecked++;
                    if (g.Name.IndexOf("copy", StringComparison.OrdinalIgnoreCase) >= 0 || !gridRegex.IsMatch(g.Name))
                    {
                        nonCompliantGrids++;
                        result.Score -= 3;
                        result.Items.Add(new CheckItem { Description = $"Invalid Grid Name: {g.Name}", Severity = Severity.Warning, ElementId = g.Id.ToString(), Category = "Grid" });
                    }
                }

                var families = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().ToList();
                var numRegex = new Regex("^[0-9]+$");
                foreach (var f in families)
                {
                    totalChecked++;
                    if (f.Name.IndexOf("Copy", StringComparison.OrdinalIgnoreCase) >= 0 || numRegex.IsMatch(f.Name) || f.Name.Length > 80)
                    {
                        nonCompliantFamilies++;
                        result.Score -= 0.5;
                        result.Items.Add(new CheckItem { Description = $"Invalid Family Name: {f.Name}", Severity = Severity.Warning, ElementId = f.Id.ToString(), Category = "Family" });
                    }
                }

                result.Score = Math.Max(0, result.Score);

                result.Metrics["NonCompliantViews"] = nonCompliantViews;
                result.Metrics["NonCompliantSheets"] = nonCompliantSheets;
                result.Metrics["NonCompliantLevels"] = nonCompliantLevels;
                result.Metrics["NonCompliantGrids"] = nonCompliantGrids;
                result.Metrics["NonCompliantFamilies"] = nonCompliantFamilies;
                result.Metrics["TotalChecked"] = totalChecked;

                result.Summary = $"Checked {totalChecked} elements. Found {nonCompliantViews + nonCompliantSheets + nonCompliantLevels + nonCompliantGrids + nonCompliantFamilies} non-compliant names.";
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
