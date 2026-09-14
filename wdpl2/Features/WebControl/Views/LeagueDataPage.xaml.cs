using System.Text;
using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services.Web;

namespace Wdpl2.Views.WebControl;

/// <summary>
/// Publishes one season's league data to the website.
/// </summary>
public partial class LeagueDataPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private readonly List<Season> _seasons = new();

    public LeagueDataPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadSeasons();
    }

    private void LoadSeasons()
    {
        _seasons.Clear();
        _seasons.AddRange(League.Seasons.OrderByDescending(s => s.StartDate));

        SeasonPicker.ItemsSource = _seasons.Select(s => s.IsActive ? $"{s.Name} (current)" : s.Name).ToList();

        if (_seasons.Count == 0)
        {
            SummaryLabel.Text = "No seasons exist yet.";
            PushButton.IsEnabled = false;
            return;
        }

        var active = _seasons.FindIndex(s => s.IsActive);
        SeasonPicker.SelectedIndex = active >= 0 ? active : 0;
    }

    private Season? Selected =>
        SeasonPicker.SelectedIndex >= 0 && SeasonPicker.SelectedIndex < _seasons.Count
            ? _seasons[SeasonPicker.SelectedIndex]
            : null;

    private void OnSeasonChanged(object? sender, EventArgs e) => UpdateSummary();

    private void UpdateSummary()
    {
        var season = Selected;
        if (season is null)
        {
            SummaryLabel.Text = "";
            return;
        }

        try
        {
            var (_, counts) = LeagueSnapshot.Build(League, season, League.Settings);
            SummaryLabel.Text = $"Ready to publish: {counts}.";
            PushButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            SummaryLabel.Text = $"Could not read this season: {ex.Message}";
            PushButton.IsEnabled = false;
        }
    }

    private async void OnPushClicked(object? sender, EventArgs e)
    {
        var season = Selected;
        if (season is null) return;

        var (payload, counts) = LeagueSnapshot.Build(League, season, League.Settings);

        if (!await DisplayAlert("Publish this season?",
                $"Season: {season.Name}\n{counts}\n\n" +
                "This replaces everything the website holds for this season. Other seasons are not affected.",
                "Publish", "Cancel"))
            return;

        PushButton.IsEnabled = false;
        Report("Publishing...", error: false);

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var result = await client.AdminAsync("league", "push", payload);

            var parts = new List<string>();
            foreach (var key in new[] { "divisions", "venues", "teams", "players", "fixtures", "standings" })
            {
                if (result.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number)
                    parts.Add($"{v.GetInt32()} {key}");
            }

            Report($"Published. Website now holds {string.Join(", ", parts)}.", error: false);
        }
        catch (WebApiException ex)
        {
            Report(ex.Code switch
            {
                "unknown_module" => "The website does not have the league module yet. Deploy the backend, then install tables.",
                "no_season" => "The website has no seasons yet. Publish one first.",
                _ => ex.Message,
            }, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
        finally
        {
            PushButton.IsEnabled = true;
        }
    }

    private async void OnVerifyClicked(object? sender, EventArgs e)
    {
        VerifyButton.IsEnabled = false;
        VerifyFrame.IsVisible = false;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var text = new StringBuilder();

            var seasons = await client.PublicAsync("league", "seasons");
            text.AppendLine($"Seasons published: {Count(seasons)}");
            if (seasons.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in seasons.EnumerateArray())
                {
                    var name = s.TryGetProperty("name", out var n) ? n.GetString() : "?";
                    var current = s.TryGetProperty("is_current", out var c) && c.ToString() == "1";
                    text.AppendLine($"  {(current ? "★" : "·")} {name}");
                }
            }

            text.AppendLine();
            text.AppendLine($"Teams: {Count(await client.PublicAsync("league", "teams"))}");
            text.AppendLine($"Fixtures: {Count(await client.PublicAsync("league", "fixtures"))}");
            text.AppendLine($"Results: {Count(await client.PublicAsync("league", "results"))}");
            text.AppendLine($"Table rows: {Count(await client.PublicAsync("league", "standings"))}");

            VerifyLabel.Text = text.ToString().TrimEnd();
            VerifyFrame.IsVisible = true;
            Report("Read back from the website.", error: false);
        }
        catch (WebApiException ex)
        {
            Report(ex.Code == "no_season"
                ? "The website has no seasons yet. Publish one first."
                : ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
        finally
        {
            VerifyButton.IsEnabled = true;
        }
    }

    private static int Count(JsonElement element) =>
        element.ValueKind == JsonValueKind.Array ? element.GetArrayLength() : 0;

    private void Report(string message, bool error)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = Color.FromArgb(error ? "#EF4444" : "#10B981");
    }
}
