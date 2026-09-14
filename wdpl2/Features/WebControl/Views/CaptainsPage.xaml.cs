using System.Text;
using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services.Web;

namespace Wdpl2.Views.WebControl;

/// <summary>
/// Publishes captain PINs so team captains can sign in to the website.
/// </summary>
public partial class CaptainsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private readonly List<Season> _seasons = new();

    public CaptainsPage()
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
        if (season is null) return;

        var (_, summary) = CaptainPins.Build(League, season);

        SummaryLabel.Text = summary.WithPin == 0
            ? "No team in this season has a PIN set. Set them under Website → Captains Access."
            : $"{summary.WithPin} team(s) have a PIN, {summary.WithoutPin} do not.";

        if (summary.Weak.Count > 0)
        {
            WeakLabel.Text =
                $"⚠ Short PINs (under {CaptainPins.RecommendedMinimumLength} characters): {string.Join(", ", summary.Weak)}. " +
                "Sign-in attempts are rate limited, but a short PIN is still worth lengthening.";
            WeakLabel.IsVisible = true;
        }
        else
        {
            WeakLabel.IsVisible = false;
        }

        PushButton.IsEnabled = summary.WithPin > 0;
    }

    private async void OnPushClicked(object? sender, EventArgs e)
    {
        var season = Selected;
        if (season is null) return;

        var (payload, summary) = CaptainPins.Build(League, season);

        if (!await DisplayAlert("Publish captain PINs?",
                $"Season: {season.Name}\n{summary.WithPin} team(s) will be able to sign in.\n\n" +
                "This replaces the whole PIN set for this season. Any team without a PIN here loses access.",
                "Publish", "Cancel"))
            return;

        PushButton.IsEnabled = false;
        Report("Publishing...", error: false);

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var result = await client.AdminAsync("captains", "push", payload);
            var count = result.TryGetProperty("teams", out var t) && t.ValueKind == JsonValueKind.Number
                ? t.GetInt32()
                : summary.WithPin;

            Report($"Published. {count} team(s) can now sign in.", error: false);
        }
        catch (WebApiException ex)
        {
            Report(ex.Code switch
            {
                "unknown_module" => "The website does not have the captains module yet. Deploy the backend, then install tables.",
                "bad_pin_hash" => "The website rejected a PIN. Rebuild the app and try again.",
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

    private async void OnStatusClicked(object? sender, EventArgs e)
    {
        StatusButton.IsEnabled = false;
        StatusFrame.IsVisible = false;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var rows = await client.AdminAsync("captains", "status");

            var text = new StringBuilder();
            var count = 0;
            if (rows.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in rows.EnumerateArray())
                {
                    count++;
                    var name = row.TryGetProperty("team_name", out var n) ? n.GetString() : "(unknown team)";
                    var when = row.TryGetProperty("updated_at", out var u) ? u.GetString() : "";
                    text.AppendLine($"  {name}  ({when})");
                }
            }

            StatusDetail.Text = count == 0
                ? "No team can sign in yet."
                : $"{count} team(s) can sign in:\n{text}".TrimEnd();
            StatusFrame.IsVisible = true;
            Report("Read back from the website.", error: false);
        }
        catch (WebApiException ex)
        {
            Report(ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
        finally
        {
            StatusButton.IsEnabled = true;
        }
    }

    private void Report(string message, bool error)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = Color.FromArgb(error ? "#EF4444" : "#10B981");
    }
}
