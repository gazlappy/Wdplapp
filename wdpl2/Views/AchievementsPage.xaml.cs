using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2.Services;
using Wdpl2.Models;

namespace Wdpl2.Views;

public partial class AchievementsPage : ContentPage
{
    private readonly ObservableCollection<Player> _players = new();
    private readonly ObservableCollection<AchievementDisplay> _achievements = new();
    private Guid? _currentSeasonId;
    private Player? _selectedPlayer;

    public AchievementsPage()
    {
        InitializeComponent();
        
        PlayerPicker.ItemsSource = _players;
        AchievementsList.ItemsSource = _achievements;
        
        SeasonService.SeasonChanged += OnSeasonChanged;
        
        LoadPlayers();
    }

    ~AchievementsPage()
    {
        SeasonService.SeasonChanged -= OnSeasonChanged;
    }

    private void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentSeasonId = e.NewSeasonId;
            LoadPlayers();
        });
    }

    private void LoadPlayers()
    {
        _currentSeasonId = SeasonService.CurrentSeasonId;
        
        if (!_currentSeasonId.HasValue)
        {
            StatusLabel.Text = "No season selected";
            return;
        }

        _players.Clear();
        var players = DataStore.Data.Players
            .Where(p => p.SeasonId == _currentSeasonId)
            .OrderBy(p => p.FullName)
            .ToList();

        foreach (var player in players)
            _players.Add(player);

        StatusLabel.Text = $"{_players.Count} player(s) available";
    }

    private void OnPlayerSelected(object? sender, EventArgs e)
    {
        _selectedPlayer = PlayerPicker.SelectedItem as Player;
        if (_selectedPlayer != null)
        {
            LoadAchievements();
        }
    }

    private void LoadAchievements()
    {
        if (_selectedPlayer == null || !_currentSeasonId.HasValue)
            return;

        try
        {
            var fixtures = DataStore.Data.Fixtures
                .Where(f => f.SeasonId == _currentSeasonId && f.Frames.Any())
                .ToList();

            var achievements = AchievementService.CalculateAchievements(
                _selectedPlayer.Id,
                fixtures,
                DataStore.Data.Players.ToList()
            );

            _achievements.Clear();
            foreach (var achievement in achievements.OrderByDescending(a => a.IsUnlocked).ThenBy(a => a.Tier))
            {
                _achievements.Add(new AchievementDisplay
                {
                    Icon = achievement.Icon,
                    Name = achievement.Name,
                    Description = achievement.Description,
                    Tier = achievement.Tier,
                    TierName = achievement.Tier.ToString(),
                    TierColor = AchievementService.GetTierColor(achievement.Tier),
                    IsUnlocked = achievement.IsUnlocked,
                    StatusText = achievement.IsUnlocked ? "? UNLOCKED" : $"{achievement.Progress}/{achievement.Target}",
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
            StatusLabel.Text = $"{totalUnlocked} of {achievements.Count} achievements unlocked";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }
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
