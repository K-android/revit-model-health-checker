using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using BIMHealthCheck.Engine;

namespace BIMHealthCheck.Checkers
{
    /// <summary>
    /// Industry-aligned checker for identifying sloppy naming artifacts (unintended copies,
    /// unrenamed placeholder families, and abandoned local views) without penalizing standard Revit defaults.
    /// </summary>
    public class NamingConventionChecker : IHealthChecker
    {
        /// <summary> Gets the checker name. </summary>
        public string Name => "NamingConventionChecker";

        /// <summary> Gets the checker display name. </summary>
        public string DisplayName => "Naming Standards & Cleanup";

        /// <summary> Gets the checker weight. </summary>
        public double Weight => 0.15;

        /// <summary>
        /// Runs the naming check focusing on actual model clutter rather than penalizing standard Revit names.
        /// </summary>
        /// <param name="doc">The Revit document to check.</param>
        /// <returns>A CheckResult object with findings and element IDs.</returns>
        public CheckResult Run(Document doc)
        {
            var result = new CheckResult { CheckerName = Name, DisplayName = DisplayName, Weight = Weight, Score = 100 };
            try
            {
                int totalChecked = 0;
                int copyViews = 0;
                int temp3DViews = 0;
                int unnamedSheets = 0;
                int copyLevelsAndGrids = 0;
                int copyOrGenericFamilies = 0;

                // 1. Views audit: Flag copy artifacts and abandoned "{3D - username}" views
                // Note: Standard "{3D}", "Level 1", "Site", "Ref. Level", "Ground Floor" etc. are perfectly valid!
                var views = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => !v.IsTemplate && v.ViewType != ViewType.Internal)
                    .ToList();

                var temp3DRegex = new Regex(@"^\{3D\s*-\s*.+\}$", RegexOptions.IgnoreCase);

                foreach (var v in views)
                {
                    totalChecked++;
                    string vName = v.Name?.Trim() ?? string.Empty;

                    if (IsCopyArtifact(vName))
                    {
                        copyViews++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"View with 'Copy' artifact: \"{vName}\"",
                            Severity = Severity.Warning,
                            ElementId = v.Id.ToString(),
                            Category = "Views"
                        });
                    }
                    else if (temp3DRegex.IsMatch(vName))
                    {
                        temp3DViews++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Abandoned user 3D view: \"{vName}\" (should be renamed or cleaned up)",
                            Severity = Severity.Info,
                            ElementId = v.Id.ToString(),
                            Category = "Views"
                        });
                    }
                }

                // 2. Sheets audit: Flag empty numbers or default "Unnamed" sheets
                var sheets = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .ToList();

                foreach (var s in sheets)
                {
                    totalChecked++;
                    string sNum = s.SheetNumber?.Trim() ?? string.Empty;
                    string sName = s.Name?.Trim() ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(sNum))
                    {
                        unnamedSheets++;
                        result.Items.Add(new CheckItem
                        {
                            Description = "Sheet with empty or missing sheet number",
                            Severity = Severity.Error,
                            ElementId = s.Id.ToString(),
                            Category = "Sheets"
                        });
                    }
                    else if (sName.Equals("Unnamed", StringComparison.OrdinalIgnoreCase))
                    {
                        unnamedSheets++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Sheet {sNum} has default name \"Unnamed\"",
                            Severity = Severity.Warning,
                            ElementId = s.Id.ToString(),
                            Category = "Sheets"
                        });
                    }
                    else if (IsCopyArtifact(sNum) || IsCopyArtifact(sName))
                    {
                        unnamedSheets++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Sheet with copy artifact: {sNum} - \"{sName}\"",
                            Severity = Severity.Warning,
                            ElementId = s.Id.ToString(),
                            Category = "Sheets"
                        });
                    }
                }

                // 3. Levels & Grids audit: Flag accidental copy names
                var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().ToList();
                foreach (var l in levels)
                {
                    totalChecked++;
                    if (IsCopyArtifact(l.Name))
                    {
                        copyLevelsAndGrids++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Level with 'Copy' artifact: \"{l.Name}\"",
                            Severity = Severity.Warning,
                            ElementId = l.Id.ToString(),
                            Category = "Datum Elements"
                        });
                    }
                }

                var grids = new FilteredElementCollector(doc).OfClass(typeof(Grid)).Cast<Grid>().ToList();
                foreach (var g in grids)
                {
                    totalChecked++;
                    if (IsCopyArtifact(g.Name))
                    {
                        copyLevelsAndGrids++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Grid with 'Copy' artifact: \"{g.Name}\"",
                            Severity = Severity.Warning,
                            ElementId = g.Id.ToString(),
                            Category = "Datum Elements"
                        });
                    }
                }

                // 4. Families audit: Flag unrenamed "Family1", "Family2" or "Copy of" families
                var genericFamilyRegex = new Regex(@"^Family\d+$", RegexOptions.IgnoreCase);
                var families = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().ToList();
                foreach (var f in families)
                {
                    totalChecked++;
                    string fName = f.Name?.Trim() ?? string.Empty;
                    if (genericFamilyRegex.IsMatch(fName))
                    {
                        copyOrGenericFamilies++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Generic placeholder family name: \"{fName}\"",
                            Severity = Severity.Warning,
                            ElementId = f.Id.ToString(),
                            Category = "Families"
                        });
                    }
                    else if (IsCopyArtifact(fName))
                    {
                        copyOrGenericFamilies++;
                        result.Items.Add(new CheckItem
                        {
                            Description = $"Family with 'Copy' artifact: \"{fName}\"",
                            Severity = Severity.Warning,
                            ElementId = f.Id.ToString(),
                            Category = "Families"
                        });
                    }
                }

                int totalIssues = copyViews + temp3DViews + unnamedSheets + copyLevelsAndGrids + copyOrGenericFamilies;

                // Pragmatic scoring: deduction scales gracefully so standard models score 90-100
                double deduction = (copyViews * 1.5)
                                 + (temp3DViews * 0.5)
                                 + (unnamedSheets * 2.0)
                                 + (copyLevelsAndGrids * 2.0)
                                 + (copyOrGenericFamilies * 1.0);

                result.Score = Math.Max(0, Math.Round(100.0 - Math.Min(100, deduction), 1));

                result.Metrics["TotalElementsAudited"] = totalChecked;
                result.Metrics["CopyViews"] = copyViews;
                result.Metrics["Abandoned3DViews"] = temp3DViews;
                result.Metrics["UnnamedSheets"] = unnamedSheets;
                result.Metrics["CopyDatumElements"] = copyLevelsAndGrids;
                result.Metrics["GenericOrCopyFamilies"] = copyOrGenericFamilies;

                if (totalIssues == 0)
                {
                    result.Summary = $"Checked {totalChecked} elements. Clean naming standards with 0 copy artifacts!";
                }
                else
                {
                    result.Summary = $"Checked {totalChecked} elements. Found {totalIssues} items to clean up ({copyViews} copy views, {temp3DViews} user 3D views, {copyOrGenericFamilies} generic/copy families).";
                }
            }
            catch (Exception ex)
            {
                result.Score = 0;
                result.Summary = $"Error during naming standards check: {ex.Message}";
                result.Items.Add(new CheckItem { Description = ex.Message, Severity = Severity.Critical });
            }

            return result;
        }

        private static bool IsCopyArtifact(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (name.StartsWith("Copy of", StringComparison.OrdinalIgnoreCase))
                return true;

            if (name.IndexOf(" - Copy", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (name.IndexOf(" Copy ", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (name.EndsWith(" Copy", StringComparison.OrdinalIgnoreCase))
                return true;

            // Matches e.g. "Floor Plan (1)", "Wall 1 (2)"
            if (Regex.IsMatch(name, @"\s+\(\d+\)$"))
                return true;

            return false;
        }
    }
}
