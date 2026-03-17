using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public class SeasonComparisonPage : ContentPage
{
    private Picker _season1Picker;
    private Picker _season2Picker;
    private VerticalStackLayout _resultsStack;

    public SeasonComparisonPage()
    {
        Title = "📊 Season Comparison";

        var seasons = DataStore.Data.Seasons.OrderByDescending(s => s.StartDate).ToList();

        _season1Picker = new Picker { Title = "Select Season 1", ItemsSource = seasons, ItemDisplayBinding = new Binding("Name") };
        _season2Picker = new Picker { Title = "Select Season 2", ItemsSource = seasons, ItemDisplayBinding = new Binding("Name") };
        _resultsStack = new VerticalStackLayout { Spacing = 8 };

        var compareBtn = new Button
        {
            Text = "Compare",
            BackgroundColor = Color.FromArgb("#3B82F6"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Margin = new Thickness(0, 8)
        };
        compareBtn.Clicked += OnCompare;

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Compare two seasons side-by-side", FontSize = 16, FontAttributes = FontAttributes.Bold },
                    _season1Picker,
                    _season2Picker,
                    compareBtn,
                    _resultsStack
                }
            }
        };
    }

    private void OnCompare(object? sender, EventArgs e)
    {
        _resultsStack.Children.Clear();

        if (_season1Picker.SelectedItem is not Season s1 || _season2Picker.SelectedItem is not Season s2)
        {
            _resultsStack.Children.Add(new Label { Text = "Please select two seasons.", TextColor = Colors.Red });
            return;
        }

        if (s1.Id == s2.Id)
        {
            _resultsStack.Children.Add(new Label { Text = "Please select two different seasons.", TextColor = Colors.Red });
            return;
        }

        var data = DataStore.Data;
        var (_, _, teams1, players1, fixtures1) = data.GetSeasonData(s1.Id);
        var (_, _, teams2, players2, fixtures2) = data.GetSeasonData(s2.Id);

        var comparison = LeagueStatsService.CompareSeasons(
            s1, fixtures1, teams1, players1,
            s2, fixtures2, teams2, players2);

        AddRow("Matches Played", comparison.Season1Fixtures, comparison.Season2Fixtures);
        AddRow("Total Frames", comparison.Season1Frames, comparison.Season2Frames);
        AddRow("8-Balls", comparison.Season1EightBalls, comparison.Season2EightBalls);
        AddRow("Teams", comparison.Season1Teams, comparison.Season2Teams);
        AddRow("Players", comparison.Season1Players, comparison.Season2Players);
        AddRow("Avg Frames/Match", comparison.Season1AvgFramesPerMatch, comparison.Season2AvgFramesPerMatch, "F1");
        AddRow("Home Win %", comparison.Season1HomeWinPct, comparison.Season2HomeWinPct, "F1");
    }

    private void AddRow(string label, double val1, double val2, string format = "F0")
    {
        var color1 = val1 > val2 ? Color.FromArgb("#10B981") : val1 < val2 ? Color.FromArgb("#EF4444") : Color.FromArgb("#6B7280");
        var color2 = val2 > val1 ? Color.FromArgb("#10B981") : val2 < val1 ? Color.FromArgb("#EF4444") : Color.FromArgb("#6B7280");

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            Padding = new Thickness(0, 4)
        };

        grid.Add(new Label { Text = val1.ToString(format), TextColor = color1, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center }, 0, 0);
        grid.Add(new Label { Text = label, HorizontalTextAlignment = TextAlignment.Center, FontSize = 12 }, 1, 0);
        grid.Add(new Label { Text = val2.ToString(format), TextColor = color2, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center }, 2, 0);

        _resultsStack.Children.Add(grid);
    }
}
