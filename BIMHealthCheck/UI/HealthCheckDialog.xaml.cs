using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMHealthCheck.Engine;
using BIMHealthCheck.Reporting;
using Grid = System.Windows.Controls.Grid;
using Binding = System.Windows.Data.Binding;

namespace BIMHealthCheck.UI
{
    /// <summary>
    /// Master-Detail Executive Dashboard & Health Hub for Revit.
    /// Provides interactive sidebar navigation, discipline scorecards, historical trends,
    /// live search/filtering, and reliable element selection/zoom.
    /// </summary>
    public partial class HealthCheckDialog : Window
    {
        private readonly HealthScore _healthScore;
        private readonly UIDocument _uidoc;
        private CheckResult _selectedCategory = null;
        private Severity? _categorySeverityFilter = null;
        private string _categorySearchQuery = string.Empty;

        public HealthCheckDialog(HealthScore healthScore) : this(healthScore, null)
        {
        }

        public HealthCheckDialog(HealthScore healthScore, UIDocument uidoc)
        {
            _healthScore = healthScore;
            _uidoc = uidoc;
            InitializeComponent();
            AttachToRevitWindow();
            InitializeDashboard();
        }

        private void AttachToRevitWindow()
        {
            try
            {
                IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    new WindowInteropHelper(this).Owner = handle;
                }
            }
            catch { }
        }

        private void InitializeDashboard()
        {
            // Set header info
            ModelNameText.Text = _healthScore.ModelName ?? "Current Model";
            ModelPathText.Text = string.IsNullOrWhiteSpace(_healthScore.ModelPath) ? "(Unsaved / Local Document)" : _healthScore.ModelPath;

            // Trend Badge
            if (_healthScore.Trend != null && _healthScore.Trend.HasHistory)
            {
                double delta = _healthScore.Trend.DeltaScore;
                if (delta > 0)
                {
                    TrendText.Text = $"📈 {_healthScore.Trend.SummaryText}";
                    TrendBadgeBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(236, 253, 245));
                    TrendText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(4, 120, 87));
                }
                else if (delta < 0)
                {
                    TrendText.Text = $"📉 {_healthScore.Trend.SummaryText}";
                    TrendBadgeBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 242, 242));
                    TrendText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 28, 28));
                }
                else
                {
                    TrendText.Text = $"📊 {_healthScore.Trend.SummaryText}";
                    TrendBadgeBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249));
                    TrendText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
                }
            }
            else
            {
                TrendText.Text = "📊 Baseline run (tracking active)";
                TrendBadgeBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249));
                TrendText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
            }

            // Populate Sidebar Hero Card
            SidebarScoreText.Text = _healthScore.OverallScore.ToString("F0");
            SidebarGradeText.Text = _healthScore.Grade.ToString();
            var scoreColor = GetScoreColor(_healthScore.OverallScore);
            SidebarScoreBorder.Background = scoreColor;
            SidebarGradeText.Foreground = scoreColor;

            // Populate Sidebar List
            PopulateSidebarList();

            // Populate Executive Scorecard View
            PopulateExecutiveScorecard();

            // Default to Executive Scorecard view
            ShowScorecardView();
        }

        private void PopulateSidebarList()
        {
            CategoryListBox.Items.Clear();

            foreach (var result in _healthScore.Results)
            {
                var itemGrid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var leftPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                leftPanel.Children.Add(new TextBlock
                {
                    Text = GetCategoryIcon(result.CheckerName),
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });

                leftPanel.Children.Add(new TextBlock
                {
                    Text = result.DisplayName,
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 160
                });
                Grid.SetColumn(leftPanel, 0);
                itemGrid.Children.Add(leftPanel);

                var rightPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                
                // Score Pill
                var scorePill = new Border
                {
                    Background = GetScorePillBg(result.Score),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                scorePill.Child = new TextBlock
                {
                    Text = result.Score.ToString("F0"),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = GetScoreColor(result.Score)
                };
                rightPanel.Children.Add(scorePill);

                // Issues Count Pill (if any)
                if (result.Items != null && result.Items.Count > 0)
                {
                    var countPill = new Border
                    {
                        Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(6, 1, 6, 1),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    countPill.Child = new TextBlock
                    {
                        Text = result.Items.Count.ToString(),
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139))
                    };
                    rightPanel.Children.Add(countPill);
                }

                Grid.SetColumn(rightPanel, 1);
                itemGrid.Children.Add(rightPanel);

                var listBoxItem = new ListBoxItem
                {
                    Content = itemGrid,
                    Tag = result
                };
                CategoryListBox.Items.Add(listBoxItem);
            }
        }

        private void PopulateExecutiveScorecard()
        {
            // Hero stats
            HeroScoreText.Text = _healthScore.OverallScore.ToString("F0");
            HeroGradeText.Text = _healthScore.Grade.ToString();
            var heroColor = GetScoreColor(_healthScore.OverallScore);
            HeroScoreBorder.Background = heroColor;
            HeroGradeText.Foreground = heroColor;

            if (_healthScore.Trend != null && _healthScore.Trend.HasHistory)
            {
                HeroTrendSummaryText.Text = $"📈 {_healthScore.Trend.SummaryText}";
                HeroTrendSummaryText.Foreground = _healthScore.Trend.DeltaScore >= 0 
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(4, 120, 87)) 
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 28, 28));
            }
            else
            {
                HeroTrendSummaryText.Text = "📊 Initial Baseline Health Audit";
                HeroTrendSummaryText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
            }

            // Quick Stats Chips
            HeroStatsPanel.Children.Clear();
            var metricsChecker = _healthScore.Results.FirstOrDefault(r => r.CheckerName == "ModelMetricsChecker");
            if (metricsChecker?.Metrics != null)
            {
                if (metricsChecker.Metrics.TryGetValue("ElementCount", out var ec))
                    HeroStatsPanel.Children.Add(CreateStatChip("Elements", $"{Convert.ToInt64(ec):N0}"));
                if (metricsChecker.Metrics.TryGetValue("FamilyCount", out var fc))
                    HeroStatsPanel.Children.Add(CreateStatChip("Families", $"{Convert.ToInt64(fc):N0}"));
                if (metricsChecker.Metrics.TryGetValue("FileSizeMB", out var fs))
                    HeroStatsPanel.Children.Add(CreateStatChip("File Size", fs?.ToString() ?? ""));
            }
            HeroStatsPanel.Children.Add(CreateStatChip("Audited Rules", $"{_healthScore.Results.Count} checks"));

            // Discipline Scores
            SetDisciplineMeter(ArchScoreText, ArchProgress, _healthScore.ArchitectureScore);
            SetDisciplineMeter(StructScoreText, StructProgress, _healthScore.StructureScore);
            SetDisciplineMeter(MepScoreText, MepProgress, _healthScore.MepScore);
            SetDisciplineMeter(CoordScoreText, CoordProgress, _healthScore.CoordinationScore);

            // Severity Cards
            CriticalCountText.Text = _healthScore.CriticalCount.ToString();
            ErrorCountText.Text = _healthScore.ErrorCount.ToString();
            WarningCountText.Text = _healthScore.WarningCount.ToString();
            InfoCountText.Text = _healthScore.InfoCount.ToString();

            // Top Priority Issues (All Critical + Errors across entire model)
            var topIssues = _healthScore.Results
                .SelectMany(r => r.Items)
                .OrderByDescending(i => i.Severity)
                .Take(25)
                .ToList();

            TopIssuesGrid.ItemsSource = topIssues;
            ScorecardIssuesSummaryText.Text = $"Showing top {topIssues.Count} of {_healthScore.TotalIssues} findings";
        }

        private void SetDisciplineMeter(TextBlock textBlock, ProgressBar progressBar, double score)
        {
            textBlock.Text = score.ToString("F0");
            textBlock.Foreground = GetScoreColor(score);
            progressBar.Value = score;
            progressBar.Foreground = GetScoreColor(score);
        }

        private Border CreateStatChip(string label, string val)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 8, 4)
            };

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text = $"{label}: ",
                FontSize = 11,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139))
            });
            sp.Children.Add(new TextBlock
            {
                Text = val,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59))
            });

            border.Child = sp;
            return border;
        }

        private void OnScorecardNavClick(object sender, MouseButtonEventArgs e)
        {
            CategoryListBox.SelectedItem = null;
            ShowScorecardView();
        }

        private void ShowScorecardView()
        {
            ScorecardNavCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 246, 255));
            ScorecardNavCard.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(191, 219, 254));

            ScorecardView.Visibility = System.Windows.Visibility.Visible;
            CategoryDetailView.Visibility = System.Windows.Visibility.Collapsed;
            SetStatus("Executive Dashboard: Viewing project health scorecard and top priority items.", false);
        }

        private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryListBox.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag is CheckResult result)
            {
                ScorecardNavCard.Background = System.Windows.Media.Brushes.Transparent;
                ScorecardNavCard.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));

                _selectedCategory = result;
                _categorySeverityFilter = null;
                _categorySearchQuery = string.Empty;
                SearchBox.Text = string.Empty;

                ShowCategoryView(result);
            }
        }

        private void ShowCategoryView(CheckResult result)
        {
            ScorecardView.Visibility = System.Windows.Visibility.Collapsed;
            CategoryDetailView.Visibility = System.Windows.Visibility.Visible;

            CategoryTitleText.Text = result.DisplayName;
            CategorySummaryText.Text = result.Summary;

            CategoryScoreText.Text = $"{result.Score:F0} / 100";
            var color = GetScoreColor(result.Score);
            CategoryScoreText.Foreground = color;
            CategoryScorePill.Background = GetScorePillBg(result.Score);
            CategoryScorePill.BorderBrush = color;

            CategoryProgressBar.Value = result.Score;
            CategoryProgressBar.Foreground = color;

            // Populate Metrics
            CategoryMetricsPanel.Children.Clear();
            if (result.Metrics != null)
            {
                foreach (var kvp in result.Metrics)
                {
                    if (kvp.Value is Dictionary<string, object> dict)
                    {
                        foreach (var sub in dict)
                            CategoryMetricsPanel.Children.Add(CreateStatChip($"{kvp.Key}: {sub.Key}", sub.Value?.ToString() ?? ""));
                    }
                    else
                    {
                        CategoryMetricsPanel.Children.Add(CreateStatChip(kvp.Key, kvp.Value?.ToString() ?? ""));
                    }
                }
            }

            HighlightCategoryFilterButton(CatFilterAllBtn);
            UpdateCategoryFindingsGrid();
        }

        private void UpdateCategoryFindingsGrid()
        {
            if (_selectedCategory?.Items == null)
            {
                CategoryFindingsGrid.ItemsSource = null;
                EmptyStateText.Visibility = System.Windows.Visibility.Visible;
                return;
            }

            var query = _selectedCategory.Items.AsEnumerable();

            // Severity Filter
            if (_categorySeverityFilter.HasValue)
            {
                query = query.Where(i => i.Severity == _categorySeverityFilter.Value);
            }

            // Search Query Filter
            if (!string.IsNullOrWhiteSpace(_categorySearchQuery))
            {
                string searchLower = _categorySearchQuery.ToLowerInvariant();
                query = query.Where(i =>
                    (i.Description != null && i.Description.ToLowerInvariant().Contains(searchLower)) ||
                    (i.Category != null && i.Category.ToLowerInvariant().Contains(searchLower)) ||
                    (i.ElementId != null && i.ElementId.Contains(searchLower)));
            }

            var filtered = query.OrderByDescending(i => i.Severity).ToList();
            CategoryFindingsGrid.ItemsSource = filtered;

            if (filtered.Count == 0)
            {
                EmptyStateText.Visibility = System.Windows.Visibility.Visible;
                EmptyStateText.Text = _selectedCategory.Items.Count == 0 
                    ? "🎉 0 findings — perfect compliance in this category!" 
                    : "No findings match the current filter or search criteria.";
            }
            else
            {
                EmptyStateText.Visibility = System.Windows.Visibility.Collapsed;
            }

            SetStatus($"Viewing {_selectedCategory.DisplayName}: {filtered.Count} findings displayed.", false);
        }

        private void OnCategorySeverityFilterClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagStr)
            {
                if (tagStr.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    _categorySeverityFilter = null;
                }
                else if (Enum.TryParse(tagStr, out Severity sev))
                {
                    _categorySeverityFilter = sev;
                }

                HighlightCategoryFilterButton(btn);
                UpdateCategoryFindingsGrid();
            }
        }

        private void HighlightCategoryFilterButton(Button activeBtn)
        {
            var buttons = new[] { CatFilterAllBtn, CatFilterCriticalBtn, CatFilterErrorBtn, CatFilterWarningBtn, CatFilterInfoBtn };
            foreach (var b in buttons)
            {
                if (b == null) continue;
                if (b == activeBtn)
                {
                    b.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
                    b.Foreground = System.Windows.Media.Brushes.White;
                    b.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));
                }
                else
                {
                    b.Background = System.Windows.Media.Brushes.White;
                    b.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
                    b.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225));
                }
            }
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _categorySearchQuery = SearchBox.Text?.Trim() ?? string.Empty;
            UpdateCategoryFindingsGrid();
        }

        private void OnSevCardClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string tagStr && Enum.TryParse(tagStr, out Severity sev))
            {
                // Find first category that has findings with this severity and select it
                var targetCategory = _healthScore.Results.FirstOrDefault(r => r.Items.Any(i => i.Severity == sev));
                if (targetCategory != null)
                {
                    var item = CategoryListBox.Items.Cast<ListBoxItem>().FirstOrDefault(lbi => lbi.Tag == targetCategory);
                    if (item != null)
                    {
                        CategoryListBox.SelectedItem = item;
                        _categorySeverityFilter = sev;
                        var btn = GetSeverityFilterButton(sev);
                        if (btn != null) HighlightCategoryFilterButton(btn);
                        UpdateCategoryFindingsGrid();
                    }
                }
            }
        }

        private Button GetSeverityFilterButton(Severity sev)
        {
            switch (sev)
            {
                case Severity.Critical: return CatFilterCriticalBtn;
                case Severity.Error: return CatFilterErrorBtn;
                case Severity.Warning: return CatFilterWarningBtn;
                case Severity.Info: return CatFilterInfoBtn;
                default: return CatFilterAllBtn;
            }
        }

        private void OnLocateButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string idStr && !string.IsNullOrWhiteSpace(idStr))
            {
                NavigateToElement(idStr);
            }
            else
            {
                SetStatus("Selected item does not have a direct Element ID to locate.", true);
            }
        }

        private void OnGridRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid grid && grid.SelectedItem is CheckItem item && !string.IsNullOrWhiteSpace(item.ElementId))
            {
                NavigateToElement(item.ElementId);
            }
        }

        /// <summary>
        /// Robust, comprehensive element navigation and zoom across all Revit element types:
        /// handles model geometry, views, sheets, families, types, groups, and schedules.
        /// </summary>
        private void NavigateToElement(string elementIdString)
        {
            if (_uidoc == null)
            {
                SetStatus("Cannot locate element: Active Revit UIDocument reference is not available.", true);
                return;
            }

            if (string.IsNullOrWhiteSpace(elementIdString))
            {
                SetStatus("Selected finding does not have an Element ID to navigate to.", false);
                return;
            }

            var doc = _uidoc.Document;

            try
            {
                // 1. Parse IDs
                var idList = new List<ElementId>();
                var parts = elementIdString.Split(new[] { ',', ';', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var p in parts)
                {
                    if (long.TryParse(p.Trim(), out long idVal))
                    {
                        idList.Add(new ElementId(idVal));
                    }
                }

                if (idList.Count == 0)
                {
                    SetStatus($"Could not parse valid Element ID from: \"{elementIdString}\"", true);
                    return;
                }

                // 2. Fetch existing elements
                var elements = idList.Select(id => doc.GetElement(id)).Where(e => e != null).ToList();
                if (elements.Count == 0)
                {
                    SetStatus($"Element ID(s) [{elementIdString}] not found in document (may be deleted or from unloaded link).", true);
                    return;
                }

                var firstElem = elements[0];

                // Case A: The element is a View or ViewSheet!
                if (firstElem is View viewElem)
                {
                    if (!viewElem.IsTemplate)
                    {
                        _uidoc.ActiveView = viewElem;
                        SetStatus($"✔ Activated view: \"{viewElem.Name}\" in Revit.", false);
                        return;
                    }
                    else
                    {
                        SetStatus($"View \"{viewElem.Name}\" is a View Template (templates cannot be displayed as active views).", false);
                        return;
                    }
                }

                // Case B: The element is a Family definition (database definition, not a placed instance)
                if (firstElem is Family fam)
                {
                    var placedInstance = new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .FirstOrDefault(fi => fi.Symbol?.Family?.Id == fam.Id);

                    if (placedInstance != null)
                    {
                        elements = new List<Element> { placedInstance };
                        firstElem = placedInstance;
                    }
                    else
                    {
                        SetStatus($"Family \"{fam.Name}\" is loaded in the project, but has 0 placed instances to zoom to.", false);
                        return;
                    }
                }

                // Case C: The element is a FamilySymbol (Type, not an instance)
                if (firstElem is FamilySymbol sym)
                {
                    var placedInstance = new FilteredElementCollector(doc)
                        .WherePasses(new FamilyInstanceFilter(doc, sym.Id))
                        .FirstElement();

                    if (placedInstance != null)
                    {
                        elements = new List<Element> { placedInstance };
                        firstElem = placedInstance;
                    }
                    else
                    {
                        SetStatus($"Family Type \"{sym.Name}\" has 0 placed instances in the model.", false);
                        return;
                    }
                }

                // Case D: The element is a GroupType
                if (firstElem is GroupType gt)
                {
                    var placedGroup = new FilteredElementCollector(doc)
                        .OfClass(typeof(Group))
                        .Cast<Group>()
                        .FirstOrDefault(g => g.GetTypeId() == gt.Id);

                    if (placedGroup != null)
                    {
                        elements = new List<Element> { placedGroup };
                        firstElem = placedGroup;
                    }
                    else
                    {
                        SetStatus($"Group Type \"{gt.Name}\" has 0 placed instances in the model.", false);
                        return;
                    }
                }

                // Case E: If active view is a Schedule, switch to a model view so elements can be zoomed
                if (_uidoc.ActiveView is ViewSchedule)
                {
                    var default3D = new FilteredElementCollector(doc)
                        .OfClass(typeof(View3D))
                        .Cast<View3D>()
                        .FirstOrDefault(v => !v.IsTemplate && !v.IsPerspective);

                    if (default3D != null)
                    {
                        _uidoc.ActiveView = default3D;
                    }
                }

                // Case F: Element is view-specific (e.g. tag, dimension, drafting line)
                if (firstElem.OwnerViewId != ElementId.InvalidElementId)
                {
                    var ownerView = doc.GetElement(firstElem.OwnerViewId) as View;
                    if (ownerView != null && !ownerView.IsTemplate && _uidoc.ActiveView.Id != ownerView.Id)
                    {
                        _uidoc.ActiveView = ownerView;
                    }
                }

                // Perform selection and zoom
                var validIds = elements.Select(e => e.Id).ToList();
                _uidoc.Selection.SetElementIds(validIds);

                try
                {
                    _uidoc.ShowElements(validIds);
                }
                catch
                {
                    try
                    {
                        _uidoc.ShowElements(validIds[0]);
                    }
                    catch (Exception ex)
                    {
                        SetStatus($"Selected element(s) [{string.Join(", ", validIds.Select(i => i.ToString()))}] in Revit (Element may be hidden in current view: {ex.Message}).", false);
                        return;
                    }
                }

                _uidoc.RefreshActiveView();
                SetStatus($"✔ Selected & zoomed to {validIds.Count} element(s) [{string.Join(", ", validIds.Select(i => i.ToString()))}] in Revit.", false);
            }
            catch (Exception ex)
            {
                SetStatus($"Revit navigation error: {ex.Message}", true);
            }
        }

        private void SetStatus(string message, bool isWarning)
        {
            if (StatusMessageText != null)
            {
                StatusMessageText.Text = message;
                StatusMessageText.Foreground = isWarning
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38))
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 118, 110));
            }
        }

        private static string GetCategoryIcon(string checkerName)
        {
            switch (checkerName)
            {
                case "WarningsChecker": return "⚠️";
                case "RoomAndTagHealthChecker": return "🏷️";
                case "DuplicateMarksChecker": return "🔢";
                case "CadImportsChecker": return "📐";
                case "ModelGroupsChecker": return "📦";
                case "InPlaceFamiliesChecker": return "🧩";
                case "GenericModelAbuseChecker": return "🏛️";
                case "FamilyHealthChecker": return "🚪";
                case "WorksetChecker": return "📁";
                case "NamingConventionChecker": return "🔤";
                case "LinkedFilesChecker": return "🔗";
                case "SharedParameterChecker": return "⚙️";
                case "UnusedElementsChecker": return "📄";
                case "ModelMetricsChecker": return "📊";
                default: return "📌";
            }
        }

        private static SolidColorBrush GetScoreColor(double score)
        {
            if (score >= 80) return new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74));   // Green
            if (score >= 60) return new SolidColorBrush(System.Windows.Media.Color.FromRgb(217, 119, 6));   // Orange/Amber
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38));                    // Red
        }

        private static SolidColorBrush GetScorePillBg(double score)
        {
            if (score >= 80) return new SolidColorBrush(System.Windows.Media.Color.FromRgb(236, 253, 245));
            if (score >= 60) return new SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 243, 199));
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 242, 242));
        }

        private void ExportHtmlButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "HTML Report (*.html)|*.html",
                DefaultExt = ".html",
                FileName = $"BIMHealthCheck_{_healthScore.ModelName}_{DateTime.Now:yyyyMMdd_HHmmss}.html"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    HtmlReportGenerator.SaveToFile(_healthScore, dialog.FileName);
                    MessageBox.Show("HTML report exported successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export HTML report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV File (*.csv)|*.csv",
                DefaultExt = ".csv",
                FileName = $"BIMHealthCheck_{_healthScore.ModelName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ExcelExporter.ExportToCsv(_healthScore, dialog.FileName);
                    MessageBox.Show("CSV exported successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export CSV: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
