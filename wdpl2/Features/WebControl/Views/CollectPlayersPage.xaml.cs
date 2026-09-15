using Wdpl2.Models;
using Wdpl2.Services.Web;

namespace Wdpl2.Views.WebControl;

/// <summary>
/// Asks what to do with each player a captain added online.
/// </summary>
/// <remarks>
/// Three answers are possible for each: link to someone the app already has,
/// create a new player, or leave them waiting. Linking is never chosen
/// automatically — two people really can share a name, and silently merging
/// them would merge their records too.
/// </remarks>
public partial class CollectPlayersPage : ContentPage
{
    private sealed class Row
    {
        public required CaptainRosterService.AddedPlayer Player { get; init; }
        public required Picker Choice { get; init; }
        public required List<Guid?> Options { get; init; }
    }

    private readonly List<Row> _rows = new();
    private readonly TaskCompletionSource<List<CaptainRosterService.Decision>?> _answer = new();

    private CollectPlayersPage(
        IReadOnlyList<CaptainRosterService.AddedPlayer> waiting,
        IReadOnlyList<Player> squad,
        string intro)
    {
        InitializeComponent();
        IntroLabel.Text = intro;
        Build(waiting, squad);
    }

    /// <summary>Shows the chooser, or returns null if the secretary cancelled.</summary>
    public static async Task<List<CaptainRosterService.Decision>?> AskAsync(
        Page host,
        IReadOnlyList<CaptainRosterService.AddedPlayer> waiting,
        IReadOnlyList<Player> squad,
        string intro)
    {
        var page = new CollectPlayersPage(waiting, squad, intro);
        await host.Navigation.PushModalAsync(page);
        return await page._answer.Task;
    }

    private void Build(
        IReadOnlyList<CaptainRosterService.AddedPlayer> waiting,
        IReadOnlyList<Player> squad)
    {
        var matched = 0;

        foreach (var player in waiting)
        {
            var candidates = CaptainPlayerMatcher.Suggest(player.Name, player.SeasonId, squad);
            if (candidates.Count > 0) matched++;

            var options = new List<Guid?>();
            var picker = new Picker { FontSize = 13, Title = "What should happen?" };

            foreach (var candidate in candidates)
            {
                options.Add(candidate.Player.Id);
                picker.Items.Add(candidate.IsExact
                    ? $"Link to {CaptainPlayerMatcher.Describe(candidate.Player)} — already here, no team"
                    : $"Link to {CaptainPlayerMatcher.Describe(candidate.Player)} — {candidate.Reason}");
            }

            options.Add(null);
            picker.Items.Add("Add as a new player");

            options.Add(Guid.Empty);
            picker.Items.Add("Leave them for now");

            // An exact match is worth proposing; anything looser is a guess, so
            // the safe answer leads and the secretary opts into the link.
            picker.SelectedIndex = candidates.Count > 0 && candidates[0].IsExact
                ? 0
                : options.IndexOf(null);

            picker.SelectedIndexChanged += (_, _) => UpdateSummary();

            var heading = new Label
            {
                Text = player.Name,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1E293B"),
            };

            var detail = new Label
            {
                Text = candidates.Count == 0
                    ? $"{player.TeamName} · nobody here by that name"
                    : $"{player.TeamName} · {candidates.Count} possible match"
                      + (candidates.Count == 1 ? "" : "es"),
                FontSize = 11,
                TextColor = Color.FromArgb(candidates.Count == 0 ? "#94A3B8" : "#D97706"),
            };

            PlayerList.Children.Add(new Frame
            {
                BorderColor = Color.FromArgb("#E2E8F0"),
                BackgroundColor = Colors.White,
                CornerRadius = 10,
                Padding = new Thickness(14, 12),
                HasShadow = false,
                Content = new VerticalStackLayout
                {
                    Spacing = 6,
                    Children = { heading, detail, picker },
                },
            });

            _rows.Add(new Row { Player = player, Choice = picker, Options = options });
        }

        IntroLabel.Text += matched > 0
            ? $" {matched} of them look like someone already in the season."
            : "";

        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var link = 0;
        var create = 0;
        var skip = 0;

        foreach (var row in _rows)
        {
            switch (Chosen(row))
            {
                case null: create++; break;
                case { } id when id == Guid.Empty: skip++; break;
                default: link++; break;
            }
        }

        var parts = new List<string>();
        if (create > 0) parts.Add($"{create} new");
        if (link > 0) parts.Add($"{link} linked");
        if (skip > 0) parts.Add($"{skip} left");

        SummaryLabel.Text = parts.Count == 0 ? "" : string.Join(", ", parts);
        ConfirmButton.IsEnabled = create + link > 0;
    }

    /// <summary>null creates a new player, <c>Guid.Empty</c> leaves them.</summary>
    private static Guid? Chosen(Row row)
    {
        var index = row.Choice.SelectedIndex;
        return index < 0 || index >= row.Options.Count ? null : row.Options[index];
    }

    private async void OnConfirmClicked(object? sender, EventArgs e)
    {
        var decisions = new List<CaptainRosterService.Decision>();

        foreach (var row in _rows)
        {
            var chosen = Chosen(row);
            if (chosen is { } id && id == Guid.Empty) continue;

            decisions.Add(new CaptainRosterService.Decision(row.Player, chosen));
        }

        _answer.TrySetResult(decisions);
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _answer.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _answer.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}
