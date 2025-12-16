using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2.Services;
using Wdpl2.Models;

namespace Wdpl2.Views;

public partial class AchievementsPage : ContentPage
{
    private readonly ObservableCollection<PlayerOption> _players = new();
    private readonly ObservableCollection<AchievementDisplay> _achievements = new();
    private PlayerOption? _selectedPlayer;
    private bool _allSeasonsMode = true;

    public AchievementsPage()
    {
        InitializeComponent();
        
        PlayerPicker.ItemsSource = _players;
        AchievementsList.ItemsSource = _achievements;
        
        LoadPlayers();
    }

    private void LoadPlayers()
    {
        _players.Clear();
        
        if (_allSeasonsMode)
        {
            // Group players - include those WITH GlobalPlayerId grouped together,
            // AND those WITHOUT GlobalPlayerId individually
            var playersWithGlobal = DataStore.Data.Players
                .Where(p => p.GlobalPlayerId.HasValue)
                .GroupBy(p => p.GlobalPlayerId!.Value)
                .Select(g => new PlayerOption
                {
                    GlobalPlayerId = g.Key,
                    DisplayName = g.First().FullName,
                    SubText = $"{g.Count()} season(s)",
                    PlayerIds = g.Select(p => p.Id).ToList()
                })
                .ToList();

            // Also include players without GlobalPlayerId (single season players)
            var playersWithoutGlobal = DataStore.Data.Players
                .Where(p => !p.GlobalPlayerId.HasValue)
                .Select(p => new PlayerOption
                {
                    GlobalPlayerId = p.Id, // Use their own ID as identifier
                    DisplayName = p.FullName,
                    SubText = "1 season",
                    PlayerIds = new List<Guid> { p.Id }
                })
                .ToList();

            // Combine and sort
            var allPlayers = playersWithGlobal
                .Concat(playersWithoutGlobal)
                .GroupBy(p => p.DisplayName.ToLower()) // Dedupe by name
                .Select(g => g.OrderByDescending(p => p.PlayerIds.Count).First())
                .OrderBy(p => p.DisplayName)
                .ToList();

            foreach (var player in allPlayers)
                _players.Add(player);
            
            StatusLabel.Text = $"{_players.Count} player(s) available (all seasons)";
        }
        else
        {
            // Current season only
            var currentSeasonId = SeasonService.CurrentSeasonId;
            if (!currentSeasonId.HasValue)
            {
                StatusLabel.Text = "No season selected";
                return;
            }

            var players = DataStore.Data.Players
                .Where(p => p.SeasonId == currentSeasonId)
                .OrderBy(p => p.FullName)
                .ToList();

            foreach (var player in players)
            {
                _players.Add(new PlayerOption
                {
                    GlobalPlayerId = player.GlobalPlayerId ?? player.Id,
                    DisplayName = player.FullName,
                    SubText = "Current season",
                    PlayerIds = new List<Guid> { player.Id }
                });
            }

            StatusLabel.Text = $"{_players.Count} player(s) available";
        }
    }

    private void OnPlayerSelected(object? sender, EventArgs e)
    {
        _selectedPlayer = PlayerPicker.SelectedItem as PlayerOption;
        if (_selectedPlayer != null)
        {
            LoadAchievements();
        }
    }

    private void OnAllSeasonsToggled(object? sender, ToggledEventArgs e)
    {
        _allSeasonsMode = e.Value;
        _selectedPlayer = null;
        PlayerPicker.SelectedItem = null;
        _achievements.Clear();
        ClearSummary();
        LoadPlayers();
    }

    private void LoadAchievements()
    {
        if (_selectedPlayer == null)
            return;

        try
        {
            List<Fixture> fixtures;
            List<Player> allPlayers;

            if (_allSeasonsMode)
            {
                // Get ALL fixtures from ALL seasons
                fixtures = DataStore.Data.Fixtures
                    .Where(f => f.Frames.Any())
                    .ToList();
                allPlayers = DataStore.Data.Players.ToList();
            }
            else
            {
                var currentSeasonId = SeasonService.CurrentSeasonId;
                fixtures = DataStore.Data.Fixtures
                    .Where(f => f.SeasonId == currentSeasonId && f.Frames.Any())
                    .ToList();
                allPlayers = DataStore.Data.Players
                    .Where(p => p.SeasonId == currentSeasonId)
                    .ToList();
            }

            // Calculate achievements for all player IDs (cross-season)
            var achievements = AchievementService.CalculateAchievementsForMultiplePlayers(
                _selectedPlayer.PlayerIds,
                fixtures,
                allPlayers
            );

            _achievements.Clear();
            foreach (var achievement in achievements.OrderByDescending(a => a.IsUnlocked).ThenBy(a => a.Tier))
            {
                _achievements.Add(new AchievementDisplay
                {
                    Icon = GetIconText(achievement.Icon),
                    Name = achievement.Name,
                    Description = achievement.Description,
                    Tier = achievement.Tier,
                    TierName = achievement.Tier.ToString(),
                    TierColor = AchievementService.GetTierColor(achievement.Tier),
                    IsUnlocked = achievement.IsUnlocked,
                    StatusText = achievement.IsUnlocked ? "UNLOCKED" : $"{achievement.Progress}/{achievement.Target}",
                    Progress = achievement.Progress,
                    Target = achievement.Target,
                    ProgressPercent = achievement.Target > 0 ? (double)achievement.Progress / achievement.Target : 0,
                    ShowProgress = !achievement.IsUnlocked && achievement.Progress > 0,
                    Opacity = achievement.IsUnlocked ? 1.0 : 0.5
                });
            }

            // Update summary
            int bronze = achievements.Count(a => a.IsUnlocked && a.Tier == AchievementService.AchievementTier.Bronze);
            int silver = achievements.Count(a => a.IsUnlocked && a.Tier == AchievementService.AchievementTier.Silver);
            int gold = achievements.Count(a => a.IsUnlocked && a.Tier == AchievementService.AchievementTier.Gold);
            int platinum = achievements.Count(a => a.IsUnlocked && a.Tier == AchievementService.AchievementTier.Platinum);

            BronzeCountLabel.Text = bronze.ToString();
            SilverCountLabel.Text = silver.ToString();
            GoldCountLabel.Text = gold.ToString();
            PlatinumCountLabel.Text = platinum.ToString();

            int totalUnlocked = achievements.Count(a => a.IsUnlocked);
            var modeText = _allSeasonsMode ? "(All Seasons)" : "(Current Season)";
            StatusLabel.Text = $"{totalUnlocked} of {achievements.Count} achievements unlocked {modeText}";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private string GetIconText(string iconId)
    {
        // Use Unicode escape sequences for emojis to ensure proper rendering
        return iconId switch
        {
            "fire" => "\U0001F525",        // ??
            "star" => "\u2B50",            // ?
            "crown" => "\U0001F451",       // ??
            "8ball" => "\U0001F3B1",       // ??
            "target" => "\U0001F3AF",      // ??
            "sparkle" => "\u2728",         // ?
            "footprints" => "\U0001F463",  // ??
            "hundred" => "\U0001F4AF",     // ??
            "medal" => "\U0001F3C5",       // ??
            "trophy" => "\U0001F3C6",      // ??
            "star_gold" => "\U0001F31F",   // ??
            "diamond" => "\U0001F48E",     // ??
            "superhero" => "\U0001F9B8",   // ??
            "sword" => "\u2694\uFE0F",     // ??
            "sparkles" => "\U0001F320",    // ??
            "ribbon" => "\U0001F396\uFE0F", // ???
            "star_shine" => "\U0001F31F",  // ??
            "crown_gold" => "\U0001F451",  // ??
            "chart_up" => "\U0001F4C8",    // ??
            _ => "\U0001F3C5"              // ??
        };
    }

    private void ClearSummary()
    {
        BronzeCountLabel.Text = "0";
        SilverCountLabel.Text = "0";
        GoldCountLabel.Text = "0";
        PlatinumCountLabel.Text = "0";
    }
}

public class PlayerOption
{
    public Guid GlobalPlayerId { get; set; }
    public string DisplayName { get; set; } = "";
    public string SubText { get; set; } = "";
    public List<Guid> PlayerIds { get; set; } = new();
    
    public override string ToString() => DisplayName;
}

public class AchievementDisplay
{
    public string Icon { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public AchievementService.AchievementTier Tier { get; set; }
    public string TierName { get; set; } = "";
    public Microsoft.Maui.Graphics.Color TierColor { get; set; } = Microsoft.Maui.Graphics.Colors.Gray;
    public bool IsUnlocked { get; set; }
    public string StatusText { get; set; } = "";
    public int Progress { get; set; }
    public int Target { get; set; }
    public double ProgressPercent { get; set; }
    public bool ShowProgress { get; set; }
    public double Opacity { get; set; } = 1.0;
}
