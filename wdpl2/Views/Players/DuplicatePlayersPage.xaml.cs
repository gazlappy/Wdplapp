using Microsoft.Maui.Controls.Shapes;
using Wdpl2.Domain.Players;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.Players;

/// <summary>
/// Shows the same person recorded more than once, and ties those rows together
/// on the word of the secretary rather than on its own judgement.
/// </summary>
/// <remarks>
/// Linking deletes nothing, so this page cannot lose anybody's record. What it
/// can do is take too long: the league it runs against is forty-nine seasons
/// deep, and an earlier version asked how many records named each row while it
/// was drawing them - forty million passes over the fixtures, on the thread
/// drawing the screen, which stopped the app dead. So the scan runs off the UI
/// thread, references are counted once for everybody, and a long list is drawn
/// a page at a time.
/// </remarks>
public partial class DuplicatePlayersPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    /// <summary>
    /// How many sets to draw before offering the rest.
    /// </summary>
    /// <remarks>
    /// Each set is a nested layout with a row per player. Four hundred of them
    /// is slow to build and unreadable anyway - and with "Link them all" there
    /// is usually no reason to read past the first screen.
    /// </remarks>
    private const int PageSize = 25;

    private List<PlayerLinks.Duplicate> _found = new();
    private Dictionary<Guid, int> _references = new();
    private int _shown;

    public DuplicatePlayersPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ScanAsync();
    }

    private async void OnScanClicked(object? sender, EventArgs e) => await ScanAsync();

    private async Task ScanAsync()
    {
        ScanButton.IsEnabled = false;
        SummaryLabel.Text = "Scanning…";
        GroupList.Children.Clear();
        BulkFrame.IsVisible = MoreButton.IsVisible = EmptyLabel.IsVisible = false;

        try
        {
            // Both walk the whole league. On the working set that is a couple of
            // hundred thousand rows, which is quick but not instant, and the
            // screen should not be frozen while it happens.
            var league = League;
            var (found, references) = await Task.Run(() => (
                PlayerLinks.Find(league),
                PlayerLinks.CountReferences(league)));

            _found = found;
            _references = references;
            _shown = 0;

            Render();
        }
        catch (Exception ex)
        {
            SummaryLabel.Text = "Could not scan.";
            Report(ex.Message, error: true);
        }
        finally
        {
            ScanButton.IsEnabled = true;
        }
    }

    private void Render()
    {
        GroupList.Children.Clear();
        _shown = 0;

        EmptyLabel.IsVisible = _found.Count == 0;

        var tidy = _found.Count(d => d.Tidy);
        var needsALook = _found.Count - tidy;

        SummaryLabel.Text = _found.Count switch
        {
            0 => "Nothing to link",
            1 => "1 person recorded twice",
            _ => $"{_found.Count} people recorded more than once"
                 + (needsALook > 0 ? $" — {needsALook} with two rows in one season" : ""),
        };

        BulkFrame.IsVisible = tidy > 0;
        if (tidy > 0)
        {
            BulkLabel.Text = tidy == 1
                ? "1 person has one row per season"
                : $"{tidy} people have one row per season";

            BulkDetail.Text = "Nothing to decide about these — linking ties each set together "
                              + "and deletes nothing.";
        }

        ShowMore();
    }

    private void OnMoreClicked(object? sender, EventArgs e) => ShowMore();

    private void ShowMore()
    {
        foreach (var duplicate in _found.Skip(_shown).Take(PageSize))
        {
            GroupList.Children.Add(Card(duplicate));
        }

        _shown = Math.Min(_shown + PageSize, _found.Count);

        MoreButton.IsVisible = _shown < _found.Count;
        MoreButton.Text = $"Show more ({_found.Count - _shown} left)";
    }

    private View Card(PlayerLinks.Duplicate duplicate)
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
                new Label { Text = duplicate.Name, FontAttributes = FontAttributes.Bold, FontSize = 16 },
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

        if (!duplicate.Tidy)
        {
            // Linking is still right - they are one person - but it is not the
            // whole fix, and saying so is better than leaving it to be noticed.
            body.Add(new Label
            {
                Text = "Two of these share a season. Linking ties their record together, but that "
                       + "season's squad will still list them twice until you remove one in Manage players.",
                FontSize = 11,
                TextColor = Color.FromArgb("#B45309"),
                LineBreakMode = LineBreakMode.WordWrap,
            });
        }

        foreach (var player in duplicate.Players)
        {
            body.Add(Row(duplicate, player));
        }

        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#E2E8F0"),
            BackgroundColor = Colors.White,
            Padding = new Thickness(14, 12),
            Content = body,
        };
    }

    private View Row(PlayerLinks.Duplicate duplicate, Player player)
    {
        var season = League.Seasons.FirstOrDefault(s => s.Id == player.SeasonId);
        var team = League.Teams.FirstOrDefault(t => t.Id == player.TeamId);
        var references = _references.TryGetValue(player.Id, out var n) ? n : 0;

        var detail = new List<string>
        {
            season?.Name ?? "no season",
            team?.Name ?? "no team",
            references == 1 ? "1 record" : $"{references} records",
        };

        if (!player.IsActive) detail.Add("inactive");
        if (player.GlobalPlayerId.HasValue) detail.Add("linked");

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

        var link = new Button
        {
            Text = "Link to this one",
            BackgroundColor = Color.FromArgb("#16634B"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 12,
            Padding = new Thickness(14, 6),
            VerticalOptions = LayoutOptions.Center,
        };

        link.Clicked += async (_, _) => await LinkAsync(duplicate, player);
        grid.Add(link, 1);

        return grid;
    }

    private static View Badge(PlayerLinks.Duplicate duplicate)
    {
        var (background, text) = duplicate.Confidence switch
        {
            PlayerLinks.Confidence.Certain => ("#DCFCE7", "#166534"),
            PlayerLinks.Confidence.Likely => ("#FEF3C7", "#92400E"),
            _ => ("#F1F5F9", "#475569"),
        };

        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
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

    private async Task LinkAsync(PlayerLinks.Duplicate duplicate, Player keeper)
    {
        var others = duplicate.Players.Where(p => p.Id != keeper.Id).ToList();
        if (others.Count == 0) return;

        var name = $"{keeper.FirstName} {keeper.LastName}".Trim();

        if (!await DisplayAlert("Link these as one person?",
                $"Known as: {name}\n"
                + $"Rows: {string.Join(", ", duplicate.Players.Select(Describe))}\n\n"
                + "Their records are counted together from now on. Nothing is deleted, "
                + "and you can unlink from this page.",
                "Link", "Cancel"))
            return;

        await ApplyAsync(() => PlayerLinks.Link(League, keeper.Id, others.Select(p => p.Id)));
    }

    private async void OnLinkAllClicked(object? sender, EventArgs e)
    {
        var tidy = _found.Where(d => d.Tidy).ToList();
        if (tidy.Count == 0) return;

        if (!await DisplayAlert("Link them all?",
                $"{tidy.Count} people have one row per season, so there is nothing to choose between "
                + "the rows — each set is tied together under its fullest spelling.\n\n"
                + "Nothing is deleted. Anything with two rows in one season is left for you to look at.",
                "Link them all", "Cancel"))
            return;

        BulkButton.IsEnabled = false;
        try
        {
            await ApplyAsync(() => PlayerLinks.LinkAll(League, tidy));
        }
        finally
        {
            BulkButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Runs a link, saves, and scans again.
    /// </summary>
    /// <remarks>
    /// Linking only writes <c>GlobalPlayerId</c> on player rows, so the JSON
    /// snapshot and the entity copy both need it - which is what the full save
    /// does. Nothing else in the league is touched.
    /// </remarks>
    private async Task ApplyAsync(Func<PlayerLinks.Report> work)
    {
        Report("Linking…", error: false);

        try
        {
            // Saving writes the whole snapshot and rebuilds the entity copy from
            // it. On this league that is twenty megabytes of JSON, which is not
            // something to do on the thread drawing the screen.
            var report = await Task.Run(() =>
            {
                var done = work();
                DataStore.Save();
                return done;
            });

            // If the database copy could not be brought level the link is in the
            // file but not in what the rest of the app reads, and saying so
            // beats letting the analytics quietly disagree.
            if (DataStore.LastSyncError is { } trouble)
            {
                Report($"{report} But the app's database copy could not be updated: {trouble}",
                       error: true);
            }
            else
            {
                Report(report.ToString(), error: false);
            }

            await ScanAsync();
        }
        catch (Exception ex)
        {
            Report($"Could not link: {ex.Message}", error: true);
        }
    }

    private string Describe(Player player)
    {
        var season = League.Seasons.FirstOrDefault(s => s.Id == player.SeasonId);
        return $"{player.FirstName} {player.LastName}".Trim()
               + (season is null ? "" : $" ({season.Name})");
    }

    private void Report(string message, bool error)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = Color.FromArgb(error ? "#EF4444" : "#16634B");
    }
}
