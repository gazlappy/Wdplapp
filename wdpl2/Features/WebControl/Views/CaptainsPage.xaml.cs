using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services;
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
    private HashSet<string> _changedElsewhere = new();
    private bool _dirty;

    private readonly IDataStore _dataStore;

    public CaptainsPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
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

    /// <summary>
    /// Reads back who can sign in, and whose PIN the app no longer knows.
    /// </summary>
    /// <remarks>
    /// The website stores a PIN as a hash and nothing can turn that back into
    /// the PIN itself, so a captain's own change cannot be read down into this
    /// page. What can be read is that it happened — which is the part that
    /// matters, because the PIN shown here is then no longer the live one.
    /// </remarks>
    private async void OnStatusClicked(object? sender, EventArgs e)
    {
        StatusButton.IsEnabled = false;
        StatusFrame.IsVisible = false;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var rows = await client.AdminAsync("captains", "status");

            var live = new List<(string Team, string When, string SetBy)>();

            if (rows.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in rows.EnumerateArray())
                {
                    live.Add((
                        row.TryGetProperty("team_name", out var n) ? n.GetString() ?? "(unknown team)" : "(unknown team)",
                        row.TryGetProperty("updated_at", out var u) ? u.GetString() ?? "" : "",
                        row.TryGetProperty("set_by", out var b) ? b.GetString() ?? "admin" : "admin"));
                }
            }

            // Anything the app did not publish is a PIN it no longer knows.
            _changedElsewhere = live.Where(r => r.SetBy != "admin").Select(r => r.Team).ToHashSet();

            StatusDetail.Text = Summarise(live);
            StatusFrame.IsVisible = true;
            MarkStaleRows();

            Report(_changedElsewhere.Count == 0
                ? "Read back from the website."
                : $"{_changedElsewhere.Count} PIN(s) have been changed since you published.",
                error: false);
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

    private static string Summarise(List<(string Team, string When, string SetBy)> live)
    {
        if (live.Count == 0) return "No team can sign in yet.";

        var text = new StringBuilder();
        text.AppendLine($"{live.Count} team(s) can sign in:");

        foreach (var row in live.OrderBy(r => r.Team, StringComparer.CurrentCultureIgnoreCase))
        {
            text.AppendLine(row.SetBy switch
            {
                "captain" => $"  {row.Team}  ({When(row.When)}) — captain set their own",
                "portal"  => $"  {row.Team}  ({When(row.When)}) — you reset it on the website",
                _         => $"  {row.Team}  ({When(row.When)})",
            });
        }

        if (live.Any(r => r.SetBy != "admin"))
        {
            text.AppendLine();
            text.Append(
                "A PIN set anywhere but here is stored hashed and cannot be read back, so the "
                + "PIN shown above for those teams is the one you last published, not the one "
                + "that works. Type a new PIN and publish to take it back.");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>The website sends UTC; the secretary reads local time.</summary>
    private static string When(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "unknown";

        return DateTime.TryParse(
            raw, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var utc)
            ? utc.ToLocalTime().ToString("d MMM, HH:mm")
            : raw;
    }

    /// <summary>Flags the PIN boxes the website no longer agrees with.</summary>
    private void MarkStaleRows()
    {
        foreach (var row in _rows)
        {
            var stale = _changedElsewhere.Contains(row.TeamName);

            row.Field.BackgroundColor = stale ? Color.FromArgb("#FEF3C7") : Colors.Transparent;
            row.Field.Placeholder = stale ? "changed elsewhere" : "no PIN";
        }
    }

    // ------------------------------------------- players captains added online

    private List<CaptainRosterService.AddedPlayer> _added = new();

    private async void OnCheckAddedClicked(object? sender, EventArgs e)
    {
        CheckAddedButton.IsEnabled = false;
        AddedFrame.IsVisible = false;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            _added = await CaptainRosterService.GetUncollectedAsync(client);

            if (_added.Count == 0)
            {
                AddedDetail.Text = "No captain has added a player since you last collected.";
                CollectButton.IsEnabled = false;
            }
            else
            {
                var lines = _added
                    .GroupBy(p => p.TeamName)
                    .Select(g => $"  {g.Key}: {string.Join(", ", g.Select(p => p.Name))}");

                AddedDetail.Text = $"{_added.Count} player(s) waiting:\n{string.Join("\n", lines)}";
                CollectButton.IsEnabled = true;
            }

            AddedFrame.IsVisible = true;
            Report("Read back from the website.", error: false);
        }
        catch (WebApiException ex)
        {
            Report(ex.Code == "unknown_module"
                ? "The website does not have the captains module yet. Deploy the backend, then install tables."
                : ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
        finally
        {
            CheckAddedButton.IsEnabled = true;
        }
    }

    private async void OnCollectClicked(object? sender, EventArgs e)
    {
        if (_added.Count == 0) return;

        CollectButton.IsEnabled = false;

        try
        {
            var squad = await _dataStore.GetPlayersAsync(SeasonId());

            var decisions = await CollectPlayersPage.AskAsync(
                this, _added, squad,
                "Choose what to do with each one.");

            if (decisions is null || decisions.Count == 0)
            {
                Report("Nothing collected.", error: false);
                return;
            }

            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var (created, linked) = await CaptainRosterService.CollectAsync(
                client, _dataStore, League, decisions);

            var left = _added.Count - decisions.Count;

            Report(Describe(created, linked, left), error: false);

            _added.RemoveAll(p => decisions.Any(d => d.Player.Id == p.Id));
            AddedDetail.Text = _added.Count == 0
                ? "All collected."
                : $"{_added.Count} still waiting.";
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
            CollectButton.IsEnabled = _added.Count > 0;
        }
    }

    private static string Describe(int created, int linked, int left)
    {
        var parts = new List<string>();
        if (created > 0) parts.Add($"added {created} new player(s)");
        if (linked > 0) parts.Add($"linked {linked} to players already here");
        if (left > 0) parts.Add($"left {left} waiting");

        return parts.Count == 0
            ? "Nothing collected."
            : char.ToUpper(parts[0][0]) + string.Join(", ", parts)[1..]
              + ". Publish the season to finish tying them in.";
    }

    /// <summary>The season the picker is on, which is the one being collected into.</summary>
    private Guid? SeasonId()
    {
        var index = SeasonPicker.SelectedIndex;
        return index >= 0 && index < _seasons.Count ? _seasons[index].Id : League.ActiveSeasonId;
    }

    private void Report(string message, bool error)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = Color.FromArgb(error ? "#EF4444" : "#10B981");
    }
}
