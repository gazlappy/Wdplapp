using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using Wdpl2.Services;
using Wdpl2.Models;

namespace Wdpl2.Views;

public partial class TeamAnalyticsPage : ContentPage
{
    private readonly ObservableCollection<Team> _teams = new();
    private readonly ObservableCollection<PlayerContribution> _playerContributions = new();
    private readonly ObservableCollection<OpponentRecord> _opponentRecords = new();
    private Guid? _currentSeasonId;
    private Team? _selectedTeam;

    public TeamAnalyticsPage()
    {
        InitializeComponent();
        
        TeamPicker.ItemsSource = _teams;
        PlayerContributionsList.ItemsSource = _playerContributions;
        OpponentRecordsList.ItemsSource = _opponentRecords;
        
        SeasonService.SeasonChanged += OnSeasonChanged;
        
        LoadTeams();
    }

    ~TeamAnalyticsPage()
    {
        SeasonService.SeasonChanged -= OnSeasonChanged;
    }

    private void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentSeasonId = e.NewSeasonId;
            LoadTeams();
        });
    }

    private void LoadTeams()
    {
        _currentSeasonId = SeasonService.CurrentSeasonId;
        
        if (!_currentSeasonId.HasValue)
        {
            StatusLabel.Text = "No season selected";
            return;
        }

        _teams.Clear();
        var teams = DataStore.Data.Teams
            .Where(t => t.SeasonId == _currentSeasonId)
            .OrderBy(t => t.Name)
            .ToList();

        foreach (var team in teams)
            _teams.Add(team);

        StatusLabel.Text = $"{_teams.Count} team(s) available";
    }

    private void OnTeamSelected(object? sender, EventArgs e)
    {
        _selectedTeam = TeamPicker.SelectedItem as Team;
        if (_selectedTeam != null)
        {
            CalculateTeamAnalytics();
        }
    }

    private void CalculateTeamAnalytics()
    {
        if (_selectedTeam == null || !_currentSeasonId.HasValue)
            return;

        try
        {
            var fixtures = DataStore.Data.Fixtures
                .Where(f => f.SeasonId == _currentSeasonId && f.Frames.Any())
                .Where(f => f.HomeTeamId == _selectedTeam.Id || f.AwayTeamId == _selectedTeam.Id)
                .ToList();

            if (!fixtures.Any())
            {
                StatusLabel.Text = "No match data available";
                return;
            }

            int totalMatches = fixtures.Count;
            int wins = 0, draws = 0, losses = 0;
            int homeWins = 0, homeDraws = 0, homeLosses = 0, homeMatches = 0;
            int awayWins = 0, awayDraws = 0, awayLosses = 0, awayMatches = 0;
            int totalPoints = 0;

            var opponentStats = new System.Collections.Generic.Dictionary<Guid, (int w, int d, int l)>();
            var playerStats = new System.Collections.Generic.Dictionary<Guid, (int w, int l, int frames)>();

            foreach (var fixture in fixtures)
            {
                bool isHome = fixture.HomeTeamId == _selectedTeam.Id;
                var ourScore = isHome ? fixture.HomeScore : fixture.AwayScore;
                var theirScore = isHome ? fixture.AwayScore : fixture.HomeScore;
                var opponentId = isHome ? fixture.AwayTeamId : fixture.HomeTeamId;

                bool won = ourScore > theirScore;
                bool drew = ourScore == theirScore;
                bool lost = ourScore < theirScore;

                if (won) wins++;
                else if (drew) draws++;
                else losses++;

                if (isHome)
                {
                    homeMatches++;
                    if (won) homeWins++;
                    else if (drew) homeDraws++;
                    else homeLosses++;
                }
                else
                {
                    awayMatches++;
                    if (won) awayWins++;
                    else if (drew) awayDraws++;
                    else awayLosses++;
                }

                // Calculate points (frames + bonuses)
                var settings = DataStore.Data.Settings;
                totalPoints += ourScore;
                if (won) totalPoints += settings.MatchWinBonus;
                else if (drew) totalPoints += settings.MatchDrawBonus;

                // Opponent records
                if (!opponentStats.ContainsKey(opponentId))
                    opponentStats[opponentId] = (0, 0, 0);
                
                var current = opponentStats[opponentId];
                opponentStats[opponentId] = won ? (current.w + 1, current.d, current.l) :
                                            drew ? (current.w, current.d + 1, current.l) :
                                            (current.w, current.d, current.l + 1);

                // Player contributions
                foreach (var frame in fixture.Frames)
                {
                    Guid? ourPlayerId = isHome ? frame.HomePlayerId : frame.AwayPlayerId;
                    if (!ourPlayerId.HasValue) continue;

                    bool frameWon = (isHome && frame.Winner == FrameWinner.Home) || 
                                   (!isHome && frame.Winner == FrameWinner.Away);

                    if (!playerStats.ContainsKey(ourPlayerId.Value))
                        playerStats[ourPlayerId.Value] = (0, 0, 0);

                    var pCurrent = playerStats[ourPlayerId.Value];
                    playerStats[ourPlayerId.Value] = frameWon 
                        ? (pCurrent.w + 1, pCurrent.l, pCurrent.frames + 1)
                        : (pCurrent.w, pCurrent.l + 1, pCurrent.frames + 1);
                }
            }

            // Update UI
            MatchesPlayedLabel.Text = totalMatches.ToString();
            double winRate = totalMatches > 0 ? (double)wins / totalMatches * 100 : 0;
            TeamWinRateLabel.Text = $"{winRate:F1}%";
            TotalPointsLabel.Text = totalPoints.ToString();

            HomeRecordLabel.Text = $"{homeWins}-{homeDraws}-{homeLosses}";
            double homeWinRate = homeMatches > 0 ? (double)homeWins / homeMatches * 100 : 0;
            HomeWinRateLabel.Text = $"{homeWinRate:F1}% win rate";

            AwayRecordLabel.Text = $"{awayWins}-{awayDraws}-{awayLosses}";
            double awayWinRate = awayMatches > 0 ? (double)awayWins / awayMatches * 100 : 0;
            AwayWinRateLabel.Text = $"{awayWinRate:F1}% win rate";

            // Player contributions
            _playerContributions.Clear();
            var topPlayers = playerStats
                .OrderByDescending(p => (double)p.Value.w / p.Value.frames)
                .Take(10)
                .ToList();

            foreach (var kvp in topPlayers)
            {
                var player = DataStore.Data.Players.FirstOrDefault(p => p.Id == kvp.Key);
                if (player == null) continue;

                var stats = kvp.Value;
                double contribution = stats.frames > 0 ? (double)stats.w / stats.frames * 100 : 0;

                _playerContributions.Add(new PlayerContribution
                {
                    PlayerName = player.FullName,
                    Record = $"{stats.w}W-{stats.l}L ({stats.frames} frames)",
                    Contribution = $"{contribution:F1}%"
                });
            }

            // Opponent records
            _opponentRecords.Clear();
            foreach (var kvp in opponentStats.OrderByDescending(o => o.Value.w))
            {
                var opponent = DataStore.Data.Teams.FirstOrDefault(t => t.Id == kvp.Key);
                if (opponent == null) continue;

                var stats = kvp.Value;
                _opponentRecords.Add(new OpponentRecord
                {
                    OpponentName = opponent.Name ?? "Unknown",
                    Record = stats.d > 0 ? $"{stats.w}-{stats.d}-{stats.l}" : $"{stats.w}-{stats.l}"
                });
            }

            // Team rating (average of active players)
            var activePlayers = DataStore.Data.Players
                .Where(p => p.TeamId == _selectedTeam.Id && p.SeasonId == _currentSeasonId)
                .ToList();

            if (activePlayers.Any() && playerStats.Any())
            {
                var ratingSum = activePlayers
                    .Where(p => playerStats.ContainsKey(p.Id))
                    .Select(p =>
                    {
                        var stats = playerStats[p.Id];
                        return 1000 + (int)((double)stats.w / stats.frames * 500);
                    })
                    .ToList();

                if (ratingSum.Any())
                {
                    int avgRating = (int)ratingSum.Average();
                    TeamRatingLabel.Text = avgRating.ToString();
                    TeamRatingStatsLabel.Text = $"Based on {ratingSum.Count} active player(s)";
                }
            }

            StatusLabel.Text = $"Analyzed {totalMatches} match(es)";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (_selectedTeam == null)
        {
            await DisplayAlert("No Team", "Please select a team first", "OK");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== TEAM ANALYTICS: {_selectedTeam.Name} ===");
        sb.AppendLine();
        sb.AppendLine($"Matches Played: {MatchesPlayedLabel.Text}");
        sb.AppendLine($"Win Rate: {TeamWinRateLabel.Text}");
        sb.AppendLine($"Total Points: {TotalPointsLabel.Text}");
        sb.AppendLine($"Home Record: {HomeRecordLabel.Text} ({HomeWinRateLabel.Text})");
        sb.AppendLine($"Away Record: {AwayRecordLabel.Text} ({AwayWinRateLabel.Text})");
        sb.AppendLine($"Team Rating: {TeamRatingLabel.Text}");
        sb.AppendLine();
        sb.AppendLine("Top Contributors:");
        foreach (var player in _playerContributions)
        {
            sb.AppendLine($"{player.PlayerName},{player.Record},{player.Contribution}");
        }
        sb.AppendLine();
        sb.AppendLine("vs Opponents:");
        foreach (var opp in _opponentRecords)
        {
            sb.AppendLine($"{opp.OpponentName},{opp.Record}");
        }

        var fileName = $"TeamAnalytics_{_selectedTeam.Name?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";
        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
        await System.IO.File.WriteAllTextAsync(path, sb.ToString());

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Export Team Analytics",
            File = new ShareFile(path, "text/csv")
        });

        StatusLabel.Text = "Exported successfully";
    }
}

public class PlayerContribution
{
    public string PlayerName { get; set; } = "";
    public string Record { get; set; } = "";
    public string Contribution { get; set; } = "";
}

public class OpponentRecord
{
    public string OpponentName { get; set; } = "";
    public string Record { get; set; } = "";
}
