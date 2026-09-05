using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Import;

namespace Wdpl2.Views;

public partial class SeasonSetupPage
{
    private ManualSeasonRoster _manualRoster = new();

    private sealed record RosterTeamOption(Guid? Id, string Name);

    private View CreateManualRosterContent()
    {
        var data = _dataStore.GetData();
        var seasons = data.Seasons.OrderByDescending(s => s.StartDate).ThenBy(s => s.Name).ToList();
        var sourceSeason = new Picker
        {
            Title = "Previous season", ItemsSource = seasons,
            ItemDisplayBinding = new Binding(nameof(Season.Name))
        };
        var sourceTeam = new Picker { Title = "Their team in that season", ItemDisplayBinding = new Binding("Name") };
        var targetTeam = new Picker { Title = "Place players in new-season team", ItemDisplayBinding = new Binding(nameof(Team.Name)) };
        var teamRows = new VerticalStackLayout { Spacing = 6 };
        var venueRows = new VerticalStackLayout { Spacing = 6 };
        var venueReviewRows = new VerticalStackLayout { Spacing = 6 };
        var venueSources = new Dictionary<Guid, string>();
        var playerRows = new VerticalStackLayout { Spacing = 6 };
        var reviewRows = new VerticalStackLayout { Spacing = 6 };
        var summary = new Label();
        var message = new Label { FontSize = 12 };
        var newTeamName = new Entry { Placeholder = "Or enter a new team name", MaxLength = 100 };
        var addNewTeam = new Button { Text = "Add new team" };
        var removeTeam = new Button { Text = "Remove selected destination team" };
        var teamPage = 0;
        var venuePage = 0;
        var venueReviewPage = 0;
        var playerPage = 0;
        var reviewPage = 0;

        void TryEdit(Action action)
        {
            try { action(); message.Text = "Draft updated. Nothing saved yet."; Refresh(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            { message.Text = ex.Message; }
        }

        void RefreshDestinations()
        {
            var selectedId = (targetTeam.SelectedItem as Team)?.Id;
            var teams = _manualRoster.Teams.ToList();
            targetTeam.ItemsSource = teams;
            targetTeam.SelectedItem = teams.FirstOrDefault(t => t.Id == selectedId);
            if (targetTeam.SelectedItem == null && sourceTeam.SelectedItem is RosterTeamOption { Id: Guid id })
            {
                var historical = data.Teams.FirstOrDefault(t => t.Id == id);
                if (historical != null) targetTeam.SelectedItem = _manualRoster.FindTeam(historical);
            }
            if (targetTeam.SelectedItem == null && teams.Count == 1) targetTeam.SelectedItem = teams[0];
            removeTeam.IsEnabled = targetTeam.SelectedItem is Team;
        }

        void BuildTeams()
        {
            teamRows.Children.Clear();
            var teams = data.Teams.Where(t => t.SeasonId == (sourceSeason.SelectedItem as Season)?.Id && t.SeasonId.HasValue)
                .OrderBy(t => t.Name).ThenBy(t => t.Id).ToList();
            var page = new ReviewPagination<Team>(teams, teamPage, 10);
            teamPage = page.PageIndex;
            teamRows.Children.Add(RosterPager(page.PageIndex, page.PageCount, i => { teamPage = i; BuildTeams(); }));
            foreach (var team in page.Items)
            {
                var included = _manualRoster.FindTeam(team) != null;
                var add = new Button { Text = included ? "Added" : "Add team", IsEnabled = !included };
                add.Clicked += (_, _) => TryEdit(() => _manualRoster.AddHistoricalTeam(team));
                teamRows.Children.Add(RosterRow(team.Name ?? "Unnamed team", add));
            }
            if (teams.Count == 0) teamRows.Children.Add(new Label { Text = "Choose a previous season with teams, or add a new team above." });
        }

        void BuildVenues()
        {
            venueRows.Children.Clear();
            var season = sourceSeason.SelectedItem as Season;
            var venues = data.Venues.Where(v => v.SeasonId.HasValue && v.SeasonId == season?.Id)
                .OrderBy(v => v.Name).ThenBy(v => v.Address).ThenBy(v => v.Id).ToList();
            var page = new ReviewPagination<Venue>(venues, venuePage, 10);
            venuePage = page.PageIndex;
            venueRows.Children.Add(RosterPager(page.PageIndex, page.PageCount, i => { venuePage = i; BuildVenues(); }));
            foreach (var venue in page.Items)
            {
                var add = new Button { Text = _manualRoster.FindVenue(venue) == null ? "Add venue" : "Added", IsEnabled = _manualRoster.FindVenue(venue) == null };
                add.Clicked += (_, _) => TryEdit(() =>
                {
                    var copied = _manualRoster.AddHistoricalVenue(venue);
                    venueSources[copied.Id] = season!.Name;
                });
                venueRows.Children.Add(RosterRow($"{venue.Name} — {season!.Name}\n{venue.Address}\n{venue.Tables.Count} table(s)", add));
            }
            if (venues.Count == 0) venueRows.Children.Add(new Label { Text = "No venues found. Choose a previous season with venues above." });
        }

        void BuildVenueReview()
        {
            venueReviewRows.Children.Clear();
            var page = new ReviewPagination<Venue>(_manualRoster.Venues, venueReviewPage, 10);
            venueReviewPage = page.PageIndex;
            venueReviewRows.Children.Add(RosterPager(page.PageIndex, page.PageCount, i => { venueReviewPage = i; BuildVenueReview(); }));
            foreach (var venue in page.Items)
            {
                var remove = new Button { Text = "Remove venue" };
                remove.Clicked += (_, _) => TryEdit(() =>
                {
                    _manualRoster.RemoveVenue(venue.Id);
                    venueSources.Remove(venue.Id);
                });
                var tables = string.Join(", ", venue.Tables.Select(t => $"{t.Label} (max {t.MaxTeams} teams)"));
                venueReviewRows.Children.Add(RosterRow($"{venue.Name} — from {venueSources.GetValueOrDefault(venue.Id)}\n{venue.Address}\nTables: {tables}\n{venue.Notes}", remove));
            }
            if (_manualRoster.Venues.Count == 0) venueReviewRows.Children.Add(new Label { Text = "No venues selected (optional)." });
        }

        void BuildPlayers()
        {
            playerRows.Children.Clear();
            if (sourceSeason.SelectedItem is not Season season || sourceTeam.SelectedItem is not RosterTeamOption team)
            {
                playerRows.Children.Add(new Label { Text = "Choose the source season and historical team to see its players." });
                return;
            }
            var players = ManualSeasonRoster.SourceRoster(data, season.Id, team.Id);
            var page = new ReviewPagination<Player>(players, playerPage, 10);
            playerPage = page.PageIndex;
            playerRows.Children.Add(RosterPager(page.PageIndex, page.PageCount, i => { playerPage = i; BuildPlayers(); }));
            foreach (var player in page.Items)
            {
                var drafted = _manualRoster.FindPlayer(player);
                var assigned = _manualRoster.Teams.FirstOrDefault(t => t.Id == drafted?.TeamId);
                var assign = new Button { Text = drafted == null ? "Add player" : "Move player", IsEnabled = targetTeam.SelectedItem is Team };
                assign.Clicked += (_, _) => TryEdit(() =>
                {
                    if (targetTeam.SelectedItem is not Team destination) throw new InvalidOperationException("Choose a destination team first.");
                    _manualRoster.AssignPlayer(player, destination.Id);
                });
                var label = $"{player.Name} — {team.Name} ({season.Name})";
                if (assigned != null) label += $"\nNew season: {assigned.Name}";
                playerRows.Children.Add(RosterRow(label, assign));
            }
            if (players.Count == 0) playerRows.Children.Add(new Label { Text = "No players recorded for this team in the selected season." });
        }

        void BuildReview()
        {
            reviewRows.Children.Clear();
            summary.Text = $"New season draft: {_manualRoster.Venues.Count} venues, {_manualRoster.Teams.Count} teams, {_manualRoster.Players.Count} players. Nothing saved yet.";
            var page = new ReviewPagination<Player>(_manualRoster.Players, reviewPage, 10);
            reviewPage = page.PageIndex;
            reviewRows.Children.Add(RosterPager(page.PageIndex, page.PageCount, i => { reviewPage = i; BuildReview(); }));
            foreach (var player in page.Items)
            {
                var destination = new Picker
                {
                    Title = "New-season team", ItemsSource = _manualRoster.Teams.ToList(),
                    ItemDisplayBinding = new Binding(nameof(Team.Name)),
                    SelectedItem = _manualRoster.Teams.First(t => t.Id == player.TeamId)
                };
                destination.SelectedIndexChanged += (_, _) =>
                {
                    if (destination.SelectedItem is Team team)
                    {
                        _manualRoster.MovePlayer(player.Id, team.Id);
                        BuildPlayers();
                    }
                };
                var remove = new Button { Text = "Remove player" };
                remove.Clicked += (_, _) => TryEdit(() => _manualRoster.RemovePlayer(player.Id));
                reviewRows.Children.Add(new VerticalStackLayout
                {
                    Spacing = 4, Children = { new Label { Text = player.Name }, destination, remove }
                });
            }
        }

        void Refresh()
        {
            RefreshDestinations();
            BuildTeams();
            BuildVenues();
            BuildVenueReview();
            BuildPlayers();
            BuildReview();
        }

        sourceSeason.SelectedIndexChanged += (_, _) =>
        {
            teamPage = playerPage = venuePage = 0;
            var options = data.Teams.Where(t => t.SeasonId == (sourceSeason.SelectedItem as Season)?.Id && t.SeasonId.HasValue)
                .OrderBy(t => t.Name).Select(t => new RosterTeamOption(t.Id, t.Name ?? "Unnamed team")).ToList();
            options.Add(new RosterTeamOption(null, "Unassigned players"));
            sourceTeam.ItemsSource = options;
            sourceTeam.SelectedIndex = -1;
            Refresh();
        };
        sourceTeam.SelectedIndexChanged += (_, _) =>
        {
            playerPage = 0;
            targetTeam.SelectedItem = null;
            RefreshDestinations();
            BuildPlayers();
        };
        targetTeam.SelectedIndexChanged += (_, _) =>
        {
            removeTeam.IsEnabled = targetTeam.SelectedItem is Team;
            BuildPlayers();
        };
        addNewTeam.Clicked += (_, _) => TryEdit(() => { _manualRoster.AddTeam(newTeamName.Text ?? ""); newTeamName.Text = ""; });
        removeTeam.Clicked += (_, _) => TryEdit(() =>
        {
            if (targetTeam.SelectedItem is Team team) _manualRoster.RemoveTeam(team.Id);
        });

        var content = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label { Text = "Previous season to browse", FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Select venues, teams and players from one or more previous seasons. Earlier seasons are never changed." },
                sourceSeason,
                new Label { Text = "1. Add venues (optional)", FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Copies addresses, notes and tables. Assign teams to the copied venues after creation. Same-named venues from different seasons remain separate; select only the records you need." },
                venueRows,
                new Label { Text = "2. Add teams (optional)", FontSize = 18, FontAttributes = FontAttributes.Bold },
                newTeamName, addNewTeam, teamRows,
                new Label { Text = "3. Choose players by their previous team", FontSize = 18, FontAttributes = FontAttributes.Bold },
                sourceTeam,
                new Label { Text = "Destination in the new season (same team or a different team):" },
                targetTeam, removeTeam, playerRows,
                new Label { Text = "4. Review new-season setup", FontSize = 18, FontAttributes = FontAttributes.Bold },
                summary, message,
                new Label { Text = "Selected venues", FontAttributes = FontAttributes.Bold },
                venueReviewRows,
                new Label { Text = "Selected players", FontAttributes = FontAttributes.Bold },
                reviewRows
            }
        };
        Refresh();
        return content;
    }

    private static View RosterRow(string text, View action)
    {
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
        grid.Add(new Label { Text = text, VerticalOptions = LayoutOptions.Center }, 0, 0);
        grid.Add(action, 1, 0);
        return grid;
    }

    private static View RosterPager(int index, int count, Action<int> navigate)
    {
        var previous = new Button { Text = "Previous", IsEnabled = index > 0 };
        var next = new Button { Text = "Next", IsEnabled = index + 1 < count };
        previous.Clicked += (_, _) => navigate(index - 1);
        next.Clicked += (_, _) => navigate(index + 1);
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) } };
        grid.Add(previous, 0, 0);
        grid.Add(new Label { Text = $"{index + 1} / {count}", HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center }, 1, 0);
        grid.Add(next, 2, 0);
        return grid;
    }
}
