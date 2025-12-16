using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using Wdpl2.Services;
using Wdpl2.Models;

namespace Wdpl2.Views;

public partial class PlayerProfilePage : ContentPage
{
    private readonly ObservableCollection<SeasonHistory> _seasonHistory = new();
    private readonly ObservableCollection<HeadToHeadRecord> _headToHead = new();
    private Guid? _globalPlayerId;
    private string _playerName = "";

    public PlayerProfilePage()
    {
        InitializeComponent();
        
        SeasonHistoryList.ItemsSource = _seasonHistory;
        HeadToHeadList.ItemsSource = _headToHead;
    }

    public void LoadPlayer(Guid globalPlayerId, string playerName)
    {
        _globalPlayerId = globalPlayerId;
        _playerName = playerName;
        
        PlayerNameLabel.Text = playerName;
        LoadPlayerProfile();
    }

    private void LoadPlayerProfile()
    {
        try
        {
            if (!_globalPlayerId.HasValue)
            {
                StatusLabel.Text = "No player selected";
                return;
            }

            // Get all player instances across seasons
            var playerInstances = DataStore.Data.Players
                .Where(p => p.GlobalPlayerId == _globalPlayerId)
                .ToList();

            if (!playerInstances.Any())
            {
                StatusLabel.Text = "Player data not found";
                return;
            }

            // Get all seasons this player played in
            var seasonIds = playerInstances.Select(p => p.SeasonId).Distinct().ToList();
            var seasons = DataStore.Data.Seasons
                .Where(s => seasonIds.Contains(s.Id))
                .OrderByDescending(s => s.StartDate)
                .ToList();

            // Calculate career totals
            int totalFrames = 0;
            int totalWins = 0;
            int totalLosses = 0;
            int total8Balls = 0;

            _seasonHistory.Clear();

            foreach (var season in seasons)
            {
                var playerInSeason = playerInstances.FirstOrDefault(p => p.SeasonId == season.Id);
                if (playerInSeason == null) continue;

                var seasonStats = CalculateSeasonStats(playerInSeason, season);
                if (seasonStats != null)
                {
                    _seasonHistory.Add(seasonStats);
                    totalFrames += seasonStats.FramesPlayed;
                    totalWins += seasonStats.Wins;
                    totalLosses += seasonStats.Losses;
                    total8Balls += seasonStats.EightBalls;
                }
            }

            // Update career summary
            CareerSpanLabel.Text = seasons.Count > 1 
                ? $"Career: {seasons.Min(s => s.StartDate.Year)}-{seasons.Max(s => s.StartDate.Year)}"
                : $"Career: {seasons.First().StartDate.Year}";

            var currentPlayer = playerInstances.OrderByDescending(p => DataStore.Data.Seasons.FirstOrDefault(s => s.Id == p.SeasonId)?.StartDate).FirstOrDefault();
            var currentTeam = currentPlayer?.TeamId.HasValue == true ? DataStore.Data.Teams.FirstOrDefault(t => t.Id == currentPlayer.TeamId) : null;
            CurrentTeamLabel.Text = currentTeam != null ? $"Team: {currentTeam.Name}" : "Team: Free Agent";

            TotalFramesLabel.Text = totalFrames.ToString();
            WinsLabel.Text = totalWins.ToString();
            LossesLabel.Text = totalLosses.ToString();
            SeasonsLabel.Text = seasons.Count.ToString();
            EightBallsLabel.Text = total8Balls.ToString();

            double winRate = totalFrames > 0 ? (double)totalWins / totalFrames * 100.0 : 0;
            WinRateLabel.Text = $"{winRate:F1}%";

            var latestStats = _seasonHistory.FirstOrDefault();
            RatingLabel.Text = latestStats?.Rating.ToString() ?? "1000";

            // Calculate head-to-head records
            CalculateHeadToHead(playerInstances);

            // Performance stats
            var bestSeason = _seasonHistory.OrderByDescending(s => s.WinPercentage).FirstOrDefault();
            BestSeasonLabel.Text = bestSeason != null ? $"{bestSeason.SeasonName} ({bestSeason.WinPercentage:F1}%)" : "-";

            var highestRating = _seasonHistory.OrderByDescending(s => s.Rating).FirstOrDefault();
            HighestRatingLabel.Text = highestRating != null ? $"{highestRating.Rating} ({highestRating.SeasonName})" : "-";

            var mostActive = _seasonHistory.OrderByDescending(s => s.FramesPlayed).FirstOrDefault();
            MostActiveSeasonLabel.Text = mostActive != null ? $"{mostActive.SeasonName} ({mostActive.FramesPlayed} frames)" : "-";

            double eightBallRate = totalFrames > 0 ? (double)total8Balls / totalWins * 100.0 : 0;
            EightBallRateLabel.Text = totalWins > 0 ? $"{eightBallRate:F1}% of wins" : "-";

            StatusLabel.Text = $"Loaded {seasons.Count} season(s)";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error loading profile: {ex.Message}";
        }
    }

    private SeasonHistory? CalculateSeasonStats(Player player, Season season)
    {
        var fixtures = DataStore.Data.Fixtures
            .Where(f => f.SeasonId == season.Id && f.Frames.Any())
            .ToList();

        int framesPlayed = 0;
        int wins = 0;
        int losses = 0;
        int eightBalls = 0;

        foreach (var fixture in fixtures)
        {
            foreach (var frame in fixture.Frames)
            {
                if (frame.HomePlayerId == player.Id)
                {
                    framesPlayed++;
                    if (frame.Winner == FrameWinner.Home)
                    {
                        wins++;
                        if (frame.EightBall) eightBalls++;
                    }
                    else losses++;
                }
                else if (frame.AwayPlayerId == player.Id)
                {
                    framesPlayed++;
                    if (frame.Winner == FrameWinner.Away)
                    {
                        wins++;
                        if (frame.EightBall) eightBalls++;
                    }
                    else losses++;
                }
            }
        }

        if (framesPlayed == 0) return null;

        var team = player.TeamId.HasValue ? DataStore.Data.Teams.FirstOrDefault(t => t.Id == player.TeamId) : null;
        var division = team?.DivisionId.HasValue ?? false ? DataStore.Data.Divisions.FirstOrDefault(d => d.Id == team.DivisionId) : null;

        return new SeasonHistory
        {
            SeasonName = season.Name,
            TeamName = team?.Name ?? "Unknown",
            DivisionName = division?.Name ?? "",
            FramesPlayed = framesPlayed,
            Wins = wins,
            Losses = losses,
            WinPercentage = (double)wins / framesPlayed * 100.0,
            EightBalls = eightBalls,
            Rating = 1000 + (int)(((double)wins / framesPlayed - 0.5) * 500), // Simplified rating
            Record = $"{wins}W-{losses}L",
            Stats = $"{framesPlayed} frames, {division?.Name ?? "Unknown Division"}, {eightBalls} 8-balls"
        };
    }

    private void CalculateHeadToHead(System.Collections.Generic.List<Player> playerInstances)
    {
        _headToHead.Clear();

        var opponentStats = new System.Collections.Generic.Dictionary<Guid, HeadToHeadRecord>();
        var fixtures = DataStore.Data.Fixtures.Where(f => f.Frames.Any()).ToList();
        var playerIds = new System.Collections.Generic.HashSet<Guid>(playerInstances.Select(p => p.Id));

        foreach (var fixture in fixtures)
        {
            foreach (var frame in fixture.Frames)
            {
                Guid? opponentId = null;
                bool won = false;

                if (frame.HomePlayerId.HasValue && playerIds.Contains(frame.HomePlayerId.Value) && frame.AwayPlayerId.HasValue)
                {
                    opponentId = frame.AwayPlayerId.Value;
                    won = frame.Winner == FrameWinner.Home;
                }
                else if (frame.AwayPlayerId.HasValue && playerIds.Contains(frame.AwayPlayerId.Value) && frame.HomePlayerId.HasValue)
                {
                    opponentId = frame.HomePlayerId.Value;
                    won = frame.Winner == FrameWinner.Away;
                }

                if (!opponentId.HasValue) continue;

                var opponent = DataStore.Data.Players.FirstOrDefault(p => p.Id == opponentId.Value);
                if (opponent == null) continue;

                var globalOpponentId = opponent.GlobalPlayerId ?? opponent.Id;

                if (!opponentStats.ContainsKey(globalOpponentId))
                {
                    opponentStats[globalOpponentId] = new HeadToHeadRecord
                    {
                        OpponentId = globalOpponentId,
                        OpponentName = opponent.FullName
                    };
                }

                opponentStats[globalOpponentId].TotalFrames++;
                if (won) opponentStats[globalOpponentId].Wins++;
                else opponentStats[globalOpponentId].Losses++;
            }
        }

        // Top 10 most-played opponents
        var top10 = opponentStats.Values
            .OrderByDescending(h => h.TotalFrames)
            .Take(10)
            .ToList();

        foreach (var h2h in top10)
        {
            h2h.WinPercentage = h2h.TotalFrames > 0 ? (double)h2h.Wins / h2h.TotalFrames * 100.0 : 0;
            h2h.Stats = $"{h2h.TotalFrames} frames, {h2h.WinPercentage:F1}% win rate";
            _headToHead.Add(h2h);
        }
    }

    private async void OnExportProfileClicked(object? sender, EventArgs e)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== PLAYER PROFILE: {_playerName} ===");
        sb.AppendLine();
        sb.AppendLine($"Career Span: {CareerSpanLabel.Text}");
        sb.AppendLine($"Current Team: {CurrentTeamLabel.Text}");
        sb.AppendLine($"Total Frames: {TotalFramesLabel.Text}");
        sb.AppendLine($"Win Rate: {WinRateLabel.Text}");
        sb.AppendLine($"Current Rating: {RatingLabel.Text}");
        sb.AppendLine($"8-Balls: {EightBallsLabel.Text}");
        sb.AppendLine();
        sb.AppendLine("Season History:");
        foreach (var season in _seasonHistory)
        {
            sb.AppendLine($"{season.SeasonName},{season.TeamName},{season.Record},{season.WinPercentage:F1}%,{season.Rating}");
        }
        sb.AppendLine();
        sb.AppendLine("Head-to-Head Records:");
        foreach (var h2h in _headToHead)
        {
            sb.AppendLine($"{h2h.OpponentName},{h2h.Wins}W-{h2h.Losses}L,{h2h.WinPercentage:F1}%");
        }

        var fileName = $"PlayerProfile_{_playerName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";
        var path = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
        await System.IO.File.WriteAllTextAsync(path, sb.ToString());

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Export Player Profile",
            File = new ShareFile(path, "text/csv")
        });

        StatusLabel.Text = "Profile exported successfully";
    }
}

public class SeasonHistory
{
    public string SeasonName { get; set; } = "";
    public string TeamName { get; set; } = "";
    public string DivisionName { get; set; } = "";
    public int FramesPlayed { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public double WinPercentage { get; set; }
    public int EightBalls { get; set; }
    public int Rating { get; set; }
    public string Record { get; set; } = "";
    public string Stats { get; set; } = "";
}

public class HeadToHeadRecord
{
    public Guid OpponentId { get; set; }
    public string OpponentName { get; set; } = "";
    public int TotalFrames { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public double WinPercentage { get; set; }
    public string Stats { get; set; } = "";
}
