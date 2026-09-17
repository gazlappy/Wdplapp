using Wdpl2.Domain.Players;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.Players;

/// <summary>
/// Shows the same person recorded twice, and merges them on the word of the
/// secretary rather than on its own judgement.
/// </summary>
/// <remarks>
/// Everything here is a proposal. A merge deletes rows and rewrites every
/// record that named them, so it asks first, says exactly what it will do, and
/// reports what it did. See <see cref="PlayerMerge"/> for the rules.
/// </remarks>
public partial class DuplicatePlayersPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private List<PlayerMerge.Duplicate> _found = new();

    public DuplicatePlayersPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Scan();
    }

    private void OnScanClicked(object? sender, EventArgs e) => Scan();

    private void Scan()
    {
        _found = PlayerMerge.Find(League);
        Render();
    }

    private void Render()
    {
        GroupList.Children.Clear();
        EmptyLabel.IsVisible = _found.Count == 0;

        var collapsing = _found.Count(d => d.Collapses);

        SummaryLabel.Text = _found.Count switch
        {
            0 => "Nothing to merge",
            1 => "1 possible duplicate",
            _ => $"{_found.Count} possible duplicates"
                 + (collapsing > 0 ? $" — {collapsing} with two rows in one season" : ""),
        };

        foreach (var duplicate in _found)
        {
            GroupList.Children.Add(Card(duplicate));
        }
    }

    private View Card(PlayerMerge.Duplicate duplicate)
    {
        var body = new VerticalStackLayout { Spacing = 8 };

        var heading = new Grid
        {
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) },
            ColumnSpacing = 8,
        };

        heading.Add(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label
                {
                    Text = duplicate.Name,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 16,
                },
                new Label
                {
                    Text = $"{duplicate.Players.Count} rows · {duplicate.Reason}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#64748B"),
                },
            },
        });

        heading.Add(Badge(duplicate), 1);
        body.Add(heading);

        // What merging would actually do, said before it is offered.
        body.Add(new Label
        {
            Text = duplicate.Collapses
                ? $"Two rows share a season, so merging removes {Losers(duplicate)} and moves their records across."
                : "One row per season, so merging only links them as the same person. Nothing is deleted.",
            FontSize = 11,
            TextColor = Color.FromArgb(duplicate.Collapses ? "#B45309" : "#52665D"),
            LineBreakMode = LineBreakMode.WordWrap,
        });

        foreach (var player in duplicate.Players)
        {
            body.Add(Row(duplicate, player));
        }

        return new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#E2E8F0"),
            BackgroundColor = Color.FromArgb("#FFFFFF"),
            Padding = new Thickness(14, 12),
            Content = body,
        };
    }

    private View Row(PlayerMerge.Duplicate duplicate, Player player)
    {
        var season = League.Seasons.FirstOrDefault(s => s.Id == player.SeasonId);
        var team = League.Teams.FirstOrDefault(t => t.Id == player.TeamId);
        var references = PlayerMerge.References(League, player.Id);

        var detail = new List<string>
        {
            season?.Name ?? "no season",
            team?.Name ?? "no team",
            references == 1 ? "1 record names them" : $"{references} records name them",
        };

        if (!player.IsActive) detail.Add("inactive");

        var grid = new Grid
        {
            ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) },
            ColumnSpacing = 8,
            Padding = new Thickness(0, 4),
        };

        grid.Add(new VerticalStackLayout
        {
            Spacing = 1,
            Children =
            {
                new Label
                {
                    Text = $"{player.FirstName} {player.LastName}".Trim(),
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                },
                new Label
                {
                    Text = string.Join(" · ", detail),
                    FontSize = 11,
                    TextColor = Color.FromArgb("#64748B"),
                },
            },
        });

        var keep = new Button
        {
            Text = "Keep this one",
            BackgroundColor = Color.FromArgb("#16634B"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 12,
            Padding = new Thickness(14, 6),
            VerticalOptions = LayoutOptions.Center,
        };

        keep.Clicked += async (_, _) => await MergeAsync(duplicate, player);
        grid.Add(keep, 1);

        return grid;
    }

    private static View Badge(PlayerMerge.Duplicate duplicate)
    {
        var (background, text) = duplicate.Confidence switch
        {
            PlayerMerge.Confidence.Certain => ("#DCFCE7", "#166534"),
            PlayerMerge.Confidence.Likely => ("#FEF3C7", "#92400E"),
            _ => ("#F1F5F9", "#475569"),
        };

        return new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 999 },
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb(background),
            Padding = new Thickness(10, 4),
            VerticalOptions = LayoutOptions.Center,
            Content = new Label
            {
                Text = duplicate.Confidence.ToString().ToUpperInvariant(),
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(text),
            },
        };
    }

    private static string Losers(PlayerMerge.Duplicate duplicate) =>
        duplicate.Players
            .Where(p => p.SeasonId.HasValue)
            .GroupBy(p => p.SeasonId!.Value)
            .Sum(g => g.Count() - 1) switch
        {
            1 => "1 row",
            var n => $"{n} rows",
        };

    /// <summary>
    /// Confirms in full, merges, then saves through the path that rebuilds the
    /// database from the snapshot.
    /// </summary>
    /// <remarks>
    /// A merge touches fixtures, competitions, teams and players at once.
    /// <see cref="DataStore.SaveJsonOnly"/> would leave the SQLite copy naming
    /// a player that no longer exists, and that copy is what the Fixtures page
    /// reads - so this is one of the few places that needs the full save.
    /// </remarks>
    private async Task MergeAsync(PlayerMerge.Duplicate duplicate, Player keeper)
    {
        var others = duplicate.Players.Where(p => p.Id != keeper.Id).ToList();
        if (others.Count == 0) return;

        var keptName = $"{keeper.FirstName} {keeper.LastName}".Trim();
        var keptSeason = League.Seasons.FirstOrDefault(s => s.Id == keeper.SeasonId)?.Name ?? "no season";

        var sameSeason = others.Count(p => p.SeasonId == keeper.SeasonId);

        var warning = duplicate.Collapses
            ? $"\n\n{Losers(duplicate)} will be deleted and everything that named them moved across. "
              + "This cannot be undone."
            : "\n\nNothing will be deleted — the rows are in different seasons, so they are only linked.";

        if (!await DisplayAlert("Merge these players?",
                $"Keeping: {keptName} ({keptSeason})\n"
                + $"Merging in: {string.Join(", ", others.Select(p => $"{p.FirstName} {p.LastName}".Trim()))}"
                + (sameSeason > 0 ? $"\n\n{sameSeason} of them share {keptName}'s season." : "")
                + warning,
                "Merge", "Cancel"))
            return;

        try
        {
            var report = PlayerMerge.Apply(League, keeper.Id, others.Select(p => p.Id));

            // Rebuilds SQLite from the snapshot, which is what a change across
            // this many tables needs.
            DataStore.Save();

            Report(report.ToString(), error: false);
            Scan();
        }
        catch (Exception ex)
        {
            Report($"Could not merge: {ex.Message}", error: true);
        }
    }

    private void Report(string message, bool error)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = Color.FromArgb(error ? "#EF4444" : "#16634B");
    }
}
