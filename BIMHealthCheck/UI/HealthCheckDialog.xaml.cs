using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// Interaction logic for HealthCheckDialog.xaml.
    /// Provides interactive inspection, category scoring, and Revit element selection/zoom.
    /// </summary>
    public partial class HealthCheckDialog : Window
    {
        private readonly HealthScore _healthScore;
        private readonly UIDocument _uidoc;

        /// <summary>
        /// Initializes dialog with health score.
        /// </summary>
        public HealthCheckDialog(HealthScore healthScore) : this(healthScore, null)
        {
        }

        /// <summary>
        /// Initializes dialog with health score and active UIDocument for element interaction.
        /// </summary>
        public HealthCheckDialog(HealthScore healthScore, UIDocument uidoc)
        {
            _healthScore = healthScore;
            _uidoc = uidoc;
            InitializeComponent();
            AttachToRevitWindow();
            PopulateResults();
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
            catch
            {
                // Silently continue if window handle cannot be attached
            }
        }

        private void PopulateResults()
        {
            // Set header values
            ModelNameText.Text = _healthScore.ModelName ?? "Current Model";
            ModelPathText.Text = _healthScore.ModelPath ?? "(Unsaved Model)";
            ScoreText.Text = _healthScore.OverallScore.ToString("F0");
            GradeText.Text = _healthScore.Grade.ToString();

            // Set score color
            var scoreColor = GetScoreColor(_healthScore.OverallScore);
            ScoreBorder.Background = scoreColor;
            GradeText.Foreground = scoreColor;

            // Populate checker result cards
            foreach (var result in _healthScore.Results)
            {
                var card = CreateResultCard(result);
                ResultsPanel.Children.Add(card);
            }
        }

        private UIElement CreateResultCard(CheckResult result)
        {
            var expander = new Expander
            {
                Margin = new Thickness(0, 0, 0, 10),
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                IsExpanded = result.Score < 80 // Automatically expand categories that need attention!
            };

            // Header grid
            var headerGrid = new Grid { Margin = new Thickness(5, 5, 5, 5) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
            var statusCircle = new System.Windows.Shapes.Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = GetScoreColor(result.Score),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            titlePanel.Children.Add(statusCircle);

            var titleText = new TextBlock
            {
                Text = result.DisplayName,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };
            titlePanel.Children.Add(titleText);
            Grid.SetColumn(titlePanel, 0);
            headerGrid.Children.Add(titlePanel);

            var scoreText = new TextBlock
            {
                Text = $"{result.Score:F0} / 100",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = GetScoreColor(result.Score),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0)
            };
            Grid.SetColumn(scoreText, 1);
            headerGrid.Children.Add(scoreText);

            expander.Header = headerGrid;

            // Content panel
            var contentPanel = new StackPanel { Margin = new Thickness(15) };

            // Progress bar
            var progressBar = new ProgressBar
            {
                Height = 8,
                Value = result.Score,
                Maximum = 100,
                Foreground = GetScoreColor(result.Score),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 10)
            };
            contentPanel.Children.Add(progressBar);

            // Summary
            var summaryText = new TextBlock
            {
                Text = result.Summary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80))
            };
            contentPanel.Children.Add(summaryText);

            // Metrics key-value display
            if (result.Metrics != null && result.Metrics.Count > 0)
            {
                var metricsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
                foreach (var kvp in result.Metrics)
                {
                    if (kvp.Value is Dictionary<string, object> dict)
                    {
                        // Render nested dictionary items
                        foreach (var sub in dict)
                        {
                            metricsWrap.Children.Add(CreateMetricBadge($"{kvp.Key}: {sub.Key}", sub.Value?.ToString()));
                        }
                    }
                    else
                    {
                        metricsWrap.Children.Add(CreateMetricBadge(kvp.Key, kvp.Value?.ToString()));
                    }
                }
                contentPanel.Children.Add(metricsWrap);
            }

            // Items table
            if (result.Items != null && result.Items.Count > 0)
            {
                var itemsHeaderGrid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
                itemsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                itemsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var itemsTitle = new TextBlock
                {
                    Text = $"Findings ({result.Items.Count}):",
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                itemsHeaderGrid.Children.Add(itemsTitle);

                var tipText = new TextBlock
                {
                    Text = "Double-click row or click 'Show' to zoom to element",
                    FontStyle = FontStyles.Italic,
                    FontSize = 11,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(tipText, 1);
                itemsHeaderGrid.Children.Add(tipText);

                contentPanel.Children.Add(itemsHeaderGrid);

                var itemsGrid = new DataGrid
                {
                    AutoGenerateColumns = false,
                    IsReadOnly = true,
                    HeadersVisibility = DataGridHeadersVisibility.Column,
                    Margin = new Thickness(0, 0, 0, 5),
                    Background = System.Windows.Media.Brushes.White,
                    RowBackground = System.Windows.Media.Brushes.White,
                    AlternatingRowBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(250, 250, 250)),
                    GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                    HorizontalGridLinesBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 230, 230)),
                    MaxHeight = 280,
                    SelectionMode = DataGridSelectionMode.Single
                };

                itemsGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Severity",
                    Binding = new Binding("Severity"),
                    Width = new DataGridLength(75)
                });

                itemsGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Category",
                    Binding = new Binding("Category"),
                    Width = new DataGridLength(140)
                });

                itemsGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Description",
                    Binding = new Binding("Description"),
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });

                itemsGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Element ID",
                    Binding = new Binding("ElementId"),
                    Width = new DataGridLength(110)
                });

                // Action Column: "🔍 Show" button
                var actionCol = new DataGridTemplateColumn
                {
                    Header = "Locate",
                    Width = new DataGridLength(80)
                };

                var btnFactory = new FrameworkElementFactory(typeof(Button));
                btnFactory.SetValue(Button.ContentProperty, "🔍 Show");
                btnFactory.SetValue(Button.PaddingProperty, new Thickness(6, 2, 6, 2));
                btnFactory.SetValue(Button.MarginProperty, new Thickness(2));
                btnFactory.SetValue(Button.CursorProperty, Cursors.Hand);
                btnFactory.SetValue(Button.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215)));
                btnFactory.SetValue(Button.ForegroundProperty, System.Windows.Media.Brushes.White);
                btnFactory.SetValue(Button.FontSizeProperty, 11.0);
                btnFactory.SetBinding(Button.TagProperty, new Binding("ElementId"));
                btnFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnShowElementButtonClick));

                var cellTemplate = new DataTemplate { VisualTree = btnFactory };
                actionCol.CellTemplate = cellTemplate;
                itemsGrid.Columns.Add(actionCol);

                itemsGrid.MouseDoubleClick += (s, e) =>
                {
                    if (itemsGrid.SelectedItem is CheckItem item && !string.IsNullOrWhiteSpace(item.ElementId))
                    {
                        SelectAndZoomElement(item.ElementId);
                    }
                };

                var sortedItems = result.Items.OrderByDescending(i => i.Severity).ToList();
                itemsGrid.ItemsSource = sortedItems;

                contentPanel.Children.Add(itemsGrid);
            }
            else
            {
                var noItemsText = new TextBlock
                {
                    Text = "No issues found in this category.",
                    FontStyle = FontStyles.Italic,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    Margin = new Thickness(0, 5, 0, 5)
                };
                contentPanel.Children.Add(noItemsText);
            }

            expander.Content = contentPanel;
            return expander;
        }

        private Border CreateMetricBadge(string key, string value)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 247, 250)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 224, 230)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 3, 6, 3),
                Margin = new Thickness(0, 2, 8, 4)
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock
            {
                Text = $"{key}: ",
                FontWeight = FontWeights.Medium,
                FontSize = 11,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 100, 100))
            });
            panel.Children.Add(new TextBlock
            {
                Text = value ?? "-",
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 40))
            });

            border.Child = panel;
            return border;
        }

        private void OnShowElementButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string idStr && !string.IsNullOrWhiteSpace(idStr))
            {
                SelectAndZoomElement(idStr);
            }
            else
            {
                SetStatus("Selected item does not have a direct Element ID.", true);
            }
        }

        private void SelectAndZoomElement(string elementIdString)
        {
            if (_uidoc == null)
            {
                SetStatus("Cannot locate element: Revit active UIDocument reference is not available.", true);
                return;
            }

            if (string.IsNullOrWhiteSpace(elementIdString))
            {
                SetStatus("Selected finding does not have an Element ID to navigate to.", false);
                return;
            }

            try
            {
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

                var doc = _uidoc.Document;
                var validIds = idList.Where(id => doc.GetElement(id) != null).ToList();

                if (validIds.Count > 0)
                {
                    _uidoc.Selection.SetElementIds(validIds);
                    _uidoc.ShowElements(validIds);
                    SetStatus($"✔ Selected and zoomed to {validIds.Count} element(s) [{string.Join(", ", validIds.Select(i => i.ToString()))}] in Revit.", false);
                }
                else
                {
                    SetStatus($"⚠ Element ID(s) [{elementIdString}] not found in active document (may be deleted or in linked file).", true);
                }
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
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(210, 60, 40))
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 130, 60));
            }
        }

        private SolidColorBrush GetScoreColor(double score)
        {
            if (score >= 80)
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96)); // Green
            if (score >= 60)
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 156, 18)); // Yellow/Orange
            return new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60)); // Red
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
