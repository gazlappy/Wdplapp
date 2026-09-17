using Wdpl2.Domain.Players;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.ViewModels;

/// <summary>
/// ViewModel for Career Statistics - shows player performance across all seasons
/// </summary>
public partial class CareerStatsViewModel : BaseViewModel
{
    private readonly IDataStore _dataStore;
    
    [ObservableProperty]
    private ObservableCollection<PlayerCareerStats> _players = new();
    
    [ObservableProperty]
    private PlayerCareerStats? _selectedPlayer;
    
    [ObservableProperty]
    private ObservableCollection<SeasonStats> _seasonBreakdown = new();
    
    [ObservableProperty]
    private string _searchText = "";
    
    [ObservableProperty]
    private bool _showActiveOnly = true;

    public CareerStatsViewModel(IDataStore dataStore, ISeasonService seasonService) : base(seasonService)
    {
        _dataStore = dataStore;
        SafeFireAndForget(InitializeAsync);
    }

    private async Task InitializeAsync()
    {
        await LoadCareerStatsAsync();
    }

    [RelayCommand]
    private async Task LoadCareerStatsAsync()
    {
        IsLoading = true;
        
        try
        {
            var data = _dataStore.GetData();
            var allPlayers = data.Players;
            var allFixtures = data.Fixtures;
            var allSeasons = data.Seasons;
            
            // Everybody, not only those already tied to another season. This
            // asked for a GlobalPlayerId, so a player who has only ever played
            // one season had no career at all.
            var playerGroups = PlayerCareers.All(data);

            var careerStats = new List<PlayerCareerStats>();

            foreach (var career in playerGroups)
            {
                var group = career.Rows;
                var playerName = career.Name;

                // Get all seasons this player participated in
                var seasonIds = group.Select(p => p.SeasonId).Distinct().ToList();
                var seasons = allSeasons.Where(s => seasonIds.Contains(s.Id)).OrderByDescending(s => s.StartDate).ToList();
                
                // Calculate career totals
                int totalFramesPlayed = 0;
                int totalFramesWon = 0;
                int totalEightBalls = 0;
                int seasonsPlayed = seasons.Count;
                
                var seasonBreakdown = new List<SeasonStats>();

                foreach (var season in seasons)
                {
                    // Every row this person has in the season, not the first of
                    // them: a season that ended up with two rows for one player
                    // would otherwise have half their frames go uncounted.
                    var rowsInSeason = group.Where(p => p.SeasonId == season.Id)
                                            .Select(p => p.Id)
                                            .ToHashSet();
                    if (rowsInSeason.Count == 0) continue;

                    int framesPlayed = 0;
                    int framesWon = 0;
                    int eightBalls = 0;

                    // Find all frames for this player in this season
                    var seasonFixtures = allFixtures.Where(f => f.SeasonId == season.Id);
                    
                    foreach (var fixture in seasonFixtures)
                    {
                        foreach (var frame in fixture.Frames)
                        {
                            // Home player
                            if (frame.HomePlayerId.HasValue && rowsInSeason.Contains(frame.HomePlayerId.Value))
                            {
                                framesPlayed++;
                                if (frame.Winner == FrameWinner.Home)
                                {
                                    framesWon++;
                                    if (frame.EightBall) eightBalls++;
                                }
                            }
                            // Away player
                            else if (frame.AwayPlayerId.HasValue && rowsInSeason.Contains(frame.AwayPlayerId.Value))
                            {
                                framesPlayed++;
                                if (frame.Winner == FrameWinner.Away)
                                {
                                    framesWon++;
                                    if (frame.EightBall) eightBalls++;
                                }
                            }
                        }
                    }

                    if (framesPlayed > 0)
                    {
                        seasonBreakdown.Add(new SeasonStats
                        {
                            SeasonName = season.Name,
                            SeasonYear = season.StartDate.Year,
                            FramesPlayed = framesPlayed,
                            FramesWon = framesWon,
                            FramesLost = framesPlayed - framesWon,
                            WinPercentage = (double)framesWon / framesPlayed * 100,
                            EightBalls = eightBalls
                        });

                        totalFramesPlayed += framesPlayed;
                        totalFramesWon += framesWon;
                        totalEightBalls += eightBalls;
                    }
                }

                if (totalFramesPlayed > 0)
                {
                    careerStats.Add(new PlayerCareerStats
                    {
                        GlobalPlayerId = career.Id,
                        PlayerName = playerName,
                        SeasonsPlayed = seasonsPlayed,
                        TotalFramesPlayed = totalFramesPlayed,
                        TotalFramesWon = totalFramesWon,
                        TotalFramesLost = totalFramesPlayed - totalFramesWon,
                        CareerWinPercentage = (double)totalFramesWon / totalFramesPlayed * 100,
                        TotalEightBalls = totalEightBalls,
                        SeasonBreakdown = seasonBreakdown,
                        FirstSeasonYear = seasons.Min(s => s.StartDate.Year),
                        LastSeasonYear = seasons.Max(s => s.StartDate.Year)
                    });
                }
            }

            // Apply filters
            var filtered = careerStats.AsEnumerable();
            
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var lower = _searchText.ToLower();
                filtered = filtered.Where(p => p.PlayerName.ToLower().Contains(lower));
            }

            // Sort by total frames played (most active first)
            filtered = filtered.OrderByDescending(p => p.TotalFramesPlayed);

            _players.Clear();
            foreach (var player in filtered)
                _players.Add(player);

            SetStatus($"{_players.Count} player(s) with career stats");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading career stats: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SearchPlayersAsync(string? searchText)
    {
        SearchText = searchText ?? "";
        await LoadCareerStatsAsync();
    }

    [RelayCommand]
    private void SelectPlayer(PlayerCareerStats? player)
    {
        SelectedPlayer = player;

        if (player != null)
        {
            SeasonBreakdown.Clear();
            foreach (var season in player.SeasonBreakdown)
                SeasonBreakdown.Add(season);

            SetStatus($"{player.PlayerName} - {player.TotalFramesPlayed} frames across {player.SeasonsPlayed} seasons");
        }
    }
}

/// <summary>
/// Player career statistics across all seasons
/// </summary>
public class PlayerCareerStats
{
    public Guid GlobalPlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public int SeasonsPlayed { get; set; }
    public int TotalFramesPlayed { get; set; }
    public int TotalFramesWon { get; set; }
    public int TotalFramesLost { get; set; }
    public double CareerWinPercentage { get; set; }
    public int TotalEightBalls { get; set; }
    public List<SeasonStats> SeasonBreakdown { get; set; } = new();
    public int FirstSeasonYear { get; set; }
    public int LastSeasonYear { get; set; }
    
    public string CareerSpan => FirstSeasonYear == LastSeasonYear 
        ? FirstSeasonYear.ToString() 
        : $"{FirstSeasonYear} - {LastSeasonYear}";
        
    public string WinLossRecord => $"{TotalFramesWon}W - {TotalFramesLost}L";
}

/// <summary>
/// Player statistics for a single season
/// </summary>
public class SeasonStats
{
    public string SeasonName { get; set; } = "";
    public int SeasonYear { get; set; }
    public int FramesPlayed { get; set; }
    public int FramesWon { get; set; }
    public int FramesLost { get; set; }
    public double WinPercentage { get; set; }
    public int EightBalls { get; set; }
    
    public string WinLossRecord => $"{FramesWon}W - {FramesLost}L";
}
