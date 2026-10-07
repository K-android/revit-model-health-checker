using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using BIMHealthCheck.Engine;
using BIMHealthCheck.Reporting;

namespace BIMHealthCheck.UI
{
    /// <summary>
    /// Interaction logic for HealthCheckDialog.xaml
    /// </summary>
    public partial class HealthCheckDialog : Window
    {
        private readonly HealthScore _healthScore;

        public HealthCheckDialog(HealthScore healthScore)
        {
            _healthScore = healthScore;
            InitializeComponent();
            PopulateResults();
        }

        private void PopulateResults()
        {
            // Set header values
            ModelNameText.Text = _healthScore.ModelName;
            ModelPathText.Text = _healthScore.ModelPath;
            ScoreText.Text = _healthScore.OverallScore.ToString("F0");
            GradeText.Text = _healthScore.Grade.ToString();
            
            // Set score color
            ScoreBorder.Background = GetScoreColor(_healthScore.OverallScore);
            GradeText.Foreground = GetScoreColor(_healthScore.OverallScore);

            // Populate cards
            foreach (var result in _healthScore.Results)
            {
                var card = CreateResultCard(result);
                ResultsPanel.Children.Add(card);
            }
        }

        private UIElement CreateResultCard(CheckResult result)
        {
            // Create main container
            var expander = new Expander
            {
                Margin = new Thickness(0, 0, 0, 15),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0)
            };

            // Custom header grid
            var headerGrid = new Grid { Width = 800, Margin = new Thickness(5, 10, 5, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            var titleText = new TextBlock 
            { 
                Text = result.DisplayName, 
                FontWeight = FontWeights.SemiBold, 
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(titleText, 0);
            headerGrid.Children.Add(titleText);

            var scoreText = new TextBlock
            {
                Text = $"{result.Score:F0} / 100",
                FontWeight = FontWeights.Bold,
                FontSize = 16,
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
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 15)
            };
            contentPanel.Children.Add(progressBar);

            // Summary
            var summaryText = new TextBlock
            {
                Text = result.Summary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15),
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80))
            };
            contentPanel.Children.Add(summaryText);

            // Metrics
            if (result.Metrics != null && result.Metrics.Count > 0)
            {
                var metricsTitle = new TextBlock { Text = "Metrics:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) };
                contentPanel.Children.Add(metricsTitle);

                var metricsGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
                metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                metricsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                int row = 0;
                foreach (var kvp in result.Metrics)
                {
                    metricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    
                    var keyText = new TextBlock { Text = $"{kvp.Key}:", FontWeight = FontWeights.Medium, Margin = new Thickness(0, 2, 10, 2) };
                    Grid.SetRow(keyText, row);
                    Grid.SetColumn(keyText, 0);
                    
                    var valText = new TextBlock { Text = kvp.Value?.ToString(), Margin = new Thickness(0, 2, 0, 2) };
                    Grid.SetRow(valText, row);
                    Grid.SetColumn(valText, 1);
                    
                    metricsGrid.Children.Add(keyText);
                    metricsGrid.Children.Add(valText);
                    row++;
                }
                contentPanel.Children.Add(metricsGrid);
            }

            // Items
            if (result.Items != null && result.Items.Count > 0)
            {
                var itemsTitle = new TextBlock { Text = "Findings:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) };
                contentPanel.Children.Add(itemsTitle);

                var itemsGrid = new DataGrid
                {
                    AutoGenerateColumns = false,
                    IsReadOnly = true,
                    HeadersVisibility = DataGridHeadersVisibility.Column,
                    Margin = new Thickness(0, 0, 0, 5),
                    Background = Brushes.White,
                    RowBackground = Brushes.White,
                    AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                    GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                    HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230))
                };

                itemsGrid.Columns.Add(new DataGridTextColumn { Header = "Severity", Binding = new System.Windows.Data.Binding("Severity"), Width = new DataGridLength(80) });
                itemsGrid.Columns.Add(new DataGridTextColumn { Header = "Category", Binding = new System.Windows.Data.Binding("Category"), Width = new DataGridLength(150) });
                itemsGrid.Columns.Add(new DataGridTextColumn { Header = "Description", Binding = new System.Windows.Data.Binding("Description"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                itemsGrid.Columns.Add(new DataGridTextColumn { Header = "Element ID", Binding = new System.Windows.Data.Binding("ElementId"), Width = new DataGridLength(100) });

                var sortedItems = result.Items.OrderByDescending(i => i.Severity).ToList();
                itemsGrid.ItemsSource = sortedItems;

                contentPanel.Children.Add(itemsGrid);
            }
            else
            {
                var noItemsText = new TextBlock
                {
                    Text = "No issues found.",
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray
                };
                contentPanel.Children.Add(noItemsText);
            }

            expander.Content = contentPanel;
            return expander;
        }

        private SolidColorBrush GetScoreColor(double score)
        {
            if (score >= 80)
                return new SolidColorBrush(Color.FromRgb(39, 174, 96)); // Green
            if (score >= 50)
                return new SolidColorBrush(Color.FromRgb(243, 156, 18)); // Yellow/Orange
            return new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Red
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
