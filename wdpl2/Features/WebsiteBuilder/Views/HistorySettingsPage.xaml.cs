using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class HistorySettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public HistorySettingsPage()
    {
        InitializeComponent();
        RefreshList();
    }

    private void RefreshList()
    {
        HonoursList.Children.Clear();
        var honours = League.WebsiteSettings.HistoricHonours;

        if (honours.Count == 0)
        {
            HonoursList.Children.Add(new Label
            {
                Text = "No historic honours imported yet.\nTap 'Import from Excel' to load your Roll of Honour spreadsheet.",
                FontSize = 14,
                TextColor = Color.FromArgb("#6B7280"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            StatusLabel.Text = "";
            return;
        }

        StatusLabel.Text = $"{honours.Count} honours across {honours.Select(h => h.Season).Distinct().Count()} seasons";

        var seasons = honours
            .GroupBy(h => h.Season)
            .OrderByDescending(g => g.Key)
            .ToList();

        foreach (var group in seasons)
        {
            var border = new Border
            {
                Stroke = Color.FromArgb("#E5E7EB"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(16),
                BackgroundColor = Colors.White
            };

            var stack = new VerticalStackLayout { Spacing = 6 };

            // Season header with delete button
            var headerGrid = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
            };
            headerGrid.Add(new Label
            {
                Text = $"🏆 {group.Key}",
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1E293B")
            });

            var deleteSeasonBtn = new Button
            {
                Text = "✕",
                FontSize = 12,
                BackgroundColor = Color.FromArgb("#FEE2E2"),
                TextColor = Color.FromArgb("#DC2626"),
                WidthRequest = 32,
                HeightRequest = 32,
                Padding = 0,
                CornerRadius = 6,
                CommandParameter = group.Key
            };
            deleteSeasonBtn.Clicked += OnDeleteSeasonClicked;
            Grid.SetColumn(deleteSeasonBtn, 1);
            headerGrid.Add(deleteSeasonBtn);

            stack.Add(headerGrid);
            stack.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#E5E7EB") });

            // Column headers
            var colHeader = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(new GridLength(2, GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(2, GridUnitType.Star)),
                    new ColumnDefinition(new GridLength(2, GridUnitType.Star))
                ],
                Padding = new Thickness(0, 4)
            };
            colHeader.Add(new Label { Text = "Competition", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#9CA3AF") });
            colHeader.Add(new Label { Text = "Winner", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#9CA3AF") });
            Grid.SetColumn((BindableObject)colHeader.Children[1], 1);
            colHeader.Add(new Label { Text = "Runner-Up", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#9CA3AF") });
            Grid.SetColumn((BindableObject)colHeader.Children[2], 2);
            stack.Add(colHeader);

            foreach (var honour in group.OrderBy(h => h.SortOrder))
            {
                var row = new Grid
                {
                    ColumnDefinitions =
                    [
                        new ColumnDefinition(new GridLength(2, GridUnitType.Star)),
                        new ColumnDefinition(new GridLength(2, GridUnitType.Star)),
                        new ColumnDefinition(new GridLength(2, GridUnitType.Star))
                    ],
                    Padding = new Thickness(0, 2)
                };
                row.Add(new Label { Text = honour.Title, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#374151") });
                row.Add(new Label { Text = honour.Winner, FontSize = 13, TextColor = Color.FromArgb("#374151") });
                Grid.SetColumn((BindableObject)row.Children[1], 1);
                row.Add(new Label { Text = honour.RunnerUp, FontSize = 13, TextColor = Color.FromArgb("#6B7280") });
                Grid.SetColumn((BindableObject)row.Children[2], 2);
                stack.Add(row);
            }

            border.Content = stack;
            HonoursList.Children.Add(border);
        }
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select Honours Excel File",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".xlsx"] },
                    { DevicePlatform.macOS, ["xlsx"] },
                    { DevicePlatform.iOS, ["org.openxmlformats.spreadsheetml.sheet"] },
                    { DevicePlatform.Android, ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] }
                })
            });

            if (result == null) return;

            StatusLabel.Text = "Importing...";
            var importResult = await HonoursExcelImporter.ImportAsync(result.FullPath);

            if (importResult.Success)
            {
                DataStore.Save();
                StatusLabel.Text = $"Imported {importResult.HonoursImported} honours";
                RefreshList();

                if (importResult.Warnings.Count > 0)
                    await DisplayAlert("Import Warnings", string.Join("\n", importResult.Warnings), "OK");
            }
            else
            {
                await DisplayAlert("Import Failed", importResult.Error ?? "Unknown error", "OK");
                StatusLabel.Text = "Import failed";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to import: {ex.Message}", "OK");
        }
    }

    private async void OnClearClicked(object? sender, EventArgs e)
    {
        if (League.WebsiteSettings.HistoricHonours.Count == 0) return;

        bool confirm = await DisplayAlert("Clear All Honours",
            $"This will remove all {League.WebsiteSettings.HistoricHonours.Count} historic honours. Continue?",
            "Clear All", "Cancel");

        if (!confirm) return;

        League.WebsiteSettings.HistoricHonours.Clear();
        DataStore.Save();
        StatusLabel.Text = "All honours cleared";
        RefreshList();
    }

    private async void OnDeleteSeasonClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string season)
        {
            bool confirm = await DisplayAlert("Delete Season",
                $"Remove all honours for '{season}'?", "Delete", "Cancel");
            if (!confirm) return;

            League.WebsiteSettings.HistoricHonours.RemoveAll(h => h.Season == season);
            DataStore.Save();
            RefreshList();
        }
    }
}
