using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services.Web;

namespace Wdpl2.Views.WebControl;

/// <summary>
/// Sets each team's captain PIN and publishes them to the website.
/// </summary>
/// <remarks>
/// Edits the same <see cref="Team.CaptainPin"/> as the Website → Captains
/// Access page, so there is still one source of truth - this just puts the
/// control next to the button that publishes it, rather than several pages
/// away among unrelated website settings.
/// </remarks>
public partial class CaptainsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    /// <summary>Long enough that the per-team rate limit makes guessing hopeless.</summary>
    private const int GeneratedPinLength = 6;

    private sealed class TeamRow
    {
        public required Guid TeamId { get; init; }
        public required string TeamName { get; init; }
        public required string DivisionName { get; init; }
        public required Entry Field { get; init; }
    }

    private readonly List<Season> _seasons = new();
    private readonly List<TeamRow> _rows = new();
    private bool _dirty;

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
            SaveButton.IsEnabled = false;
            FillButton.IsEnabled = false;
            return;
        }

        var active = _seasons.FindIndex(s => s.IsActive);
        SeasonPicker.SelectedIndex = active >= 0 ? active : 0;

        // Re-selecting the same index raises no change event, so build the list
        // directly rather than waiting for one that will not arrive.
        BuildTeamList();
    }

    private Season? Selected =>
        SeasonPicker.SelectedIndex >= 0 && SeasonPicker.SelectedIndex < _seasons.Count
            ? _seasons[SeasonPicker.SelectedIndex]
            : null;

    private void OnSeasonChanged(object? sender, EventArgs e) => BuildTeamList();

    // ------------------------------------------------------------- PIN editor

    private void BuildTeamList()
    {
        TeamList.Children.Clear();
        _rows.Clear();
        _dirty = false;

        var season = Selected;
        if (season is null)
        {
            SummaryLabel.Text = "Choose a season.";
            return;
        }

        var divisions = League.Divisions
            .Where(d => d.SeasonId == season.Id)
            .ToDictionary(d => d.Id, d => d.Name);

        var teams = League.Teams
            .Where(t => t.SeasonId == season.Id)
            .OrderBy(t => t.DivisionId.HasValue && divisions.ContainsKey(t.DivisionId.Value)
                ? divisions[t.DivisionId.Value] : "")
            .ThenBy(t => t.Name)
            .ToList();

        NoTeamsLabel.IsVisible = teams.Count == 0;

        foreach (var team in teams)
        {
            var field = new Entry
            {
                Text = team.CaptainPin ?? "",
                Placeholder = "no PIN",
                FontSize = 13,
                MaxLength = 32,
            };
            field.TextChanged += (_, _) => { _dirty = true; UpdateSummary(); };

            var generate = new Button
            {
                Text = "New",
                BackgroundColor = Color.FromArgb("#E2E8F0"),
                TextColor = Color.FromArgb("#334155"),
                CornerRadius = 6,
                FontSize = 11,
                Padding = new Thickness(10, 4),
            };
            var captured = field;
            generate.Clicked += (_, _) =>
            {
                captured.Text = GeneratePin();
                _dirty = true;
                UpdateSummary();
            };

            var divisionName = team.DivisionId.HasValue && divisions.TryGetValue(team.DivisionId.Value, out var dn)
                ? dn
                : "";

            var name = new Label
            {
                Text = team.Name ?? "(unnamed)",
                FontSize = 13,
                TextColor = Color.FromArgb("#1E293B"),
                VerticalOptions = LayoutOptions.Center,
            };
            var division = new Label
            {
                Text = divisionName,
                FontSize = 10,
                TextColor = Color.FromArgb("#94A3B8"),
            };

            var grid = new Grid
            {
                ColumnDefinitions = { new(new GridLength(2, GridUnitType.Star)), new(new GridLength(1.4, GridUnitType.Star)), new(GridLength.Auto) },
                ColumnSpacing = 8,
                Padding = new Thickness(0, 2),
            };
            grid.Add(new VerticalStackLayout { Spacing = 0, Children = { name, division } });
            grid.Add(field, 1);
            grid.Add(generate, 2);

            TeamList.Children.Add(grid);
            _rows.Add(new TeamRow
            {
                TeamId = team.Id,
                TeamName = team.Name ?? "(unnamed)",
                DivisionName = divisionName,
                Field = field,
            });
        }

        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var season = Selected;
        if (season is null) return;

        var withPin = _rows.Count(r => !string.IsNullOrWhiteSpace(r.Field.Text));
        var without = _rows.Count - withPin;

        SummaryLabel.Text = _rows.Count == 0
            ? "No teams in this season."
            : $"{withPin} of {_rows.Count} team(s) have a PIN" + (without > 0 ? $", {without} blank." : ".")
              + (_dirty ? "  • unsaved changes" : "");

        var weak = _rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Field.Text) && r.Field.Text!.Trim().Length < GeneratedPinLength)
            .Select(r => r.TeamName)
            .ToList();

        if (weak.Count > 0)
        {
            WeakLabel.Text =
                $"⚠ Short PINs (under {GeneratedPinLength} characters): {string.Join(", ", weak)}. " +
                "Sign-in is rate limited per team, but longer is better.";
            WeakLabel.IsVisible = true;
        }
        else
        {
            WeakLabel.IsVisible = false;
        }

        PushButton.IsEnabled = withPin > 0;
        SaveButton.IsEnabled = _rows.Count > 0;
        FillButton.IsEnabled = without > 0;
    }

    /// <summary>
    /// A random numeric PIN, from a cryptographic source.
    /// </summary>
    /// <remarks>
    /// Numeric because captains type these on a phone, and rejection sampling
    /// rather than modulo so every digit is equally likely - a biased PIN
    /// generator quietly shrinks the space an attacker has to search.
    /// </remarks>
    private static string GeneratePin()
    {
        var digits = new char[GeneratedPinLength];
        for (var i = 0; i < digits.Length; i++)
            digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        return new string(digits);
    }

    private void OnFillBlanksClicked(object? sender, EventArgs e)
    {
        var filled = 0;
        foreach (var row in _rows.Where(r => string.IsNullOrWhiteSpace(r.Field.Text)))
        {
            row.Field.Text = GeneratePin();
            filled++;
        }

        if (filled > 0)
        {
            _dirty = true;
            UpdateSummary();
            Report($"Generated {filled} PIN(s). Choose Save PINs to keep them.", error: false);
        }
        else
        {
            Report("Every team already has a PIN.", error: false);
        }
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var changed = 0;

        foreach (var row in _rows)
        {
            var team = League.Teams.FirstOrDefault(t => t.Id == row.TeamId);
            if (team is null) continue;

            var value = row.Field.Text?.Trim();
            var next = string.IsNullOrWhiteSpace(value) ? null : value;

            if (team.CaptainPin == next) continue;

            team.CaptainPin = next;
            team.ModifiedDate = DateTime.UtcNow;
            changed++;
        }

        if (changed == 0)
        {
            Report("No changes to save.", error: false);
            return;
        }

        DataStore.Save();
        _dirty = false;
        UpdateSummary();
        Report($"Saved {changed} PIN(s). Publish them to let captains sign in.", error: false);
    }

    // --------------------------------------------------------------- publish

    private async void OnPushClicked(object? sender, EventArgs e)
    {
        var season = Selected;
        if (season is null)
        {
            Report("Choose a season first.", error: true);
            return;
        }

        if (_dirty && !await DisplayAlert("Unsaved PINs",
                "Some PINs have been changed but not saved. Publishing sends the saved PINs, not what is on screen.",
                "Publish saved PINs", "Cancel"))
            return;

        var (payload, summary) = CaptainPins.Build(League, season);

        if (summary.WithPin == 0)
        {
            Report($"Nothing to publish: no team in {season.Name} has a saved PIN yet.", error: true);
            return;
        }

        if (!await DisplayAlert("Publish captain PINs?",
                $"Season: {season.Name}\n{summary.WithPin} team(s) will be able to sign in.\n\n" +
                "This replaces the whole PIN set for this season. Any team without a PIN loses access.",
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

            Report($"Published. {count} team(s) can now sign in at your website's /captain/ page.", error: false);
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
