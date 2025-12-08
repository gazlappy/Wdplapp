using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;
using IOPath = System.IO.Path;

namespace Wdpl2.Views;

public partial class LeagueTablesPage : ContentPage
{
    // Access to settings
    private static AppSettings Settings => DataStore.Data.Settings;

    // Row types for tables
    private sealed class TeamRow
    {
        public int Pos { get; set; }
        public string Team { get; set; } = "";
        public Guid TeamId { get; set; }
        public int P { get; set; }
        public int W { get; set; }
        public int L { get; set; }
        public int F { get; set; }
        public int A { get; set; }
        public int Diff => F - A;
        public int Pts { get; set; }
    }

    private sealed class PlayerRow
    {
        public int Pos { get; set; }
        public string Player { get; set; } = "";
        public Guid PlayerId { get; set; }
        public string Team { get; set; } = "";
        public int Played { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int EightBalls { get; set; }
        public double WinPct => Played == 0 ? 0 : (double)Wins / Played * 100.0;
        public int Rating { get; set; } = 1000;
    }

    private readonly ObservableCollection<TeamRow> _teamRows = new();
    private readonly ObservableCollection<PlayerRow> _playerRows = new();
    private readonly ObservableCollection<Division> _divisions = new();

    private Guid? _currentSeasonId;
    private Division? _selectedDivision;

    public LeagueTablesPage()
    {
        InitializeComponent();

        TeamTableList.ItemsSource = _teamRows;
        PlayerRatingsList.ItemsSource = _playerRows;
        DivisionPicker.ItemsSource = _divisions;

        DivisionPicker.SelectedIndexChanged += (_, __) => OnDivisionChanged();
        SortPicker.SelectedIndex = 0;
        SortPicker.SelectedIndexChanged += (_, __) => RefreshPlayerRatings();
        ExportBtn.Clicked += async (_, __) => await ExportCsvAsync();
        RecalculateBtn.Clicked += (_, __) => OnRecalculateClicked();
        CompareVbaBtn.Clicked += async (_, __) => await CompareWithVbaAsync();

        // SUBSCRIBE to global season changes
        SeasonService.SeasonChanged += OnGlobalSeasonChanged;

        RefreshAll();
    }

    ~LeagueTablesPage()
    {
        SeasonService.SeasonChanged -= OnGlobalSeasonChanged;
    }

    private void OnGlobalSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _currentSeasonId = e.NewSeasonId;
            RefreshAll();
            SetStatus($"Season: {e.NewSeason?.Name ?? "None"}");
        });
    }

    private void RefreshAll()
    {
        _currentSeasonId = SeasonService.CurrentSeasonId;
        RefreshDivisions();

        // Auto-select first division if available
        if (_divisions.Count > 0 && DivisionPicker.SelectedIndex == -1)
        {
            DivisionPicker.SelectedIndex = 0;
        }
        else
        {
            OnDivisionChanged();
        }
    }

    private void RefreshDivisions()
    {
        _divisions.Clear();

        if (!_currentSeasonId.HasValue)
        {
            SetStatus("No season selected");
            return;
        }

        foreach (var d in DataStore.Data.Divisions
            .Where(d => d.SeasonId == _currentSeasonId)
            .OrderBy(d => d.Name))
        {
            _divisions.Add(d);
        }

        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
        SetStatus($"Season: {season?.Name ?? "Unknown"} | {_divisions.Count} division(s)");
    }

    private void OnDivisionChanged()
    {
        _selectedDivision = DivisionPicker.SelectedItem as Division;
        RenderTeamTableHeader();
        RefreshTeamTable();
        RenderPlayerRatingsHeader();
        RefreshPlayerRatings();
    }

    // ========== TEAM TABLE ==========

    private void RenderTeamTableHeader()
    {
        TeamTableHeaderGrid.ColumnDefinitions.Clear();
        TeamTableHeaderGrid.Children.Clear();

        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });  // #
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });     // Team
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });  // P
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });  // W
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });  // L
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });  // F
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });  // A
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });  // Diff
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });  // Pts

        // Removed "D" (Drawn) column - WDPL uses best-of-15 so draws not possible
        string[] headers = { "#", "Team", "P", "W", "L", "F", "A", "Diff", "Pts" };
        TextAlignment[] aligns = {
            TextAlignment.Center, TextAlignment.Start, TextAlignment.Center, TextAlignment.Center,
            TextAlignment.Center, TextAlignment.Center, TextAlignment.Center,
            TextAlignment.Center, TextAlignment.Center
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var label = new Label
            {
                Text = headers[i],
                FontAttributes = FontAttributes.Bold,
                FontSize = 11,
                HorizontalTextAlignment = aligns[i],
                VerticalTextAlignment = TextAlignment.Center
            };
            TeamTableHeaderGrid.Add(label, i, 0);
        }
    }

    private void RefreshTeamTable()
    {
        _teamRows.Clear();

        if (!_currentSeasonId.HasValue || _selectedDivision == null)
        {
            TeamTableList.ItemTemplate = null;
            return;
        }

        var data = DataStore.Data;
        var teams = data.Teams.Where(t => t.DivisionId == _selectedDivision.Id).ToList();
        var tById = teams.ToDictionary(t => t.Id, t => t);

        var fixtures = data.Fixtures
            .Where(f => f.Frames.Any())
            .Where(f => f.SeasonId == _currentSeasonId)
            .ToList();

        var teamIds = new HashSet<Guid>(teams.Select(t => t.Id));
        fixtures = fixtures.Where(f => teamIds.Contains(f.HomeTeamId) || teamIds.Contains(f.AwayTeamId)).ToList();

        var table = teams.ToDictionary(t => t.Id, t => new TeamRow { Team = t.Name ?? "", TeamId = t.Id });

        foreach (var f in fixtures)
        {
            if (!tById.TryGetValue(f.HomeTeamId, out var homeTeam) ||
                !tById.TryGetValue(f.AwayTeamId, out var awayTeam))
                continue;

            var hs = f.HomeScore;
            var @as = f.AwayScore;

            var hr = table[f.HomeTeamId];
            var ar = table[f.AwayTeamId];

            hr.P++; ar.P++;
            hr.F += hs; hr.A += @as;
            ar.F += @as; ar.A += hs;

            // WDPL uses best-of-15 frames - no draws possible
            // Points = Frames Won + Win Bonus (for winner only)
            if (hs > @as)
            {
                // Home wins
                hr.W++; ar.L++;
                hr.Pts += hs + Settings.MatchWinBonus;  // Frames won + win bonus
                ar.Pts += @as;                           // Just frames won (no bonus for loss)
            }
            else
            {
                // Away wins (or technically a draw, but not possible in best-of-15)
                ar.W++; hr.L++;
                ar.Pts += @as + Settings.MatchWinBonus;  // Frames won + win bonus
                hr.Pts += hs;                             // Just frames won (no bonus for loss)
            }
        }

        var rows = table.Values
            .OrderByDescending(r => r.Pts)
            .ThenByDescending(r => r.Diff)
            .ThenByDescending(r => r.F)
            .ThenBy(r => r.Team, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < rows.Count; i++)
            rows[i].Pos = i + 1;

        TeamTableList.ItemTemplate = TeamRowTemplate();
        foreach (var r in rows)
            _teamRows.Add(r);
    }

    private static DataTemplate TeamRowTemplate()
    {
        return new DataTemplate(() =>
        {
            var grid = new Grid
            {
                ColumnSpacing = 8,
                Padding = new Thickness(10, 6)
            };

            // Removed D (Drawn) column - WDPL best-of-15 has no draws
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });  // #
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });     // Team
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });  // P
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });  // W
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });  // L
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });  // F
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });  // A
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });  // Diff
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });  // Pts

            Label L(string path, TextAlignment align = TextAlignment.Center, bool bold = false)
            {
                var lbl = new Label
                {
                    HorizontalTextAlignment = align,
                    VerticalTextAlignment = TextAlignment.Center,
                    FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
                    FontSize = 12
                };
                lbl.SetBinding(Label.TextProperty, new Binding(path));
                return lbl;
            }

            grid.Add(L(nameof(TeamRow.Pos), TextAlignment.Center, true), 0, 0);
            grid.Add(L(nameof(TeamRow.Team), TextAlignment.Start, true), 1, 0);
            grid.Add(L(nameof(TeamRow.P)), 2, 0);
            grid.Add(L(nameof(TeamRow.W)), 3, 0);
            grid.Add(L(nameof(TeamRow.L)), 4, 0);
            grid.Add(L(nameof(TeamRow.F)), 5, 0);
            grid.Add(L(nameof(TeamRow.A)), 6, 0);
            grid.Add(L(nameof(TeamRow.Diff)), 7, 0);
            grid.Add(L(nameof(TeamRow.Pts), TextAlignment.Center, true), 8, 0);

            return new Border { StrokeShape = new RoundRectangle { CornerRadius = 8 }, Content = grid, StrokeThickness = 0 };
        });
    }

    // ========== PLAYER RATINGS ==========

    // ✅ VBA RATING ALGORITHM - Based on analysis of tblRatings and tblPlayerResult data
    // ==================================================================
    // 
    // DATA STRUCTURES IN VBA:
    // - tblRatings: Stores rating per player per week (ID, WeekNo, PlayerID, Rating)
    // - tblPlayerResult: Frame results with OppRating/PlayerRating snapshots
    //
    // VBA ALGORITHM FLOW:
    // 1. All players start Week 1 at RatingStartValue (1000)
    // 2. After each week's matches, ratings are recalculated for ALL players
    // 3. The new rating becomes the lookup value for NEXT week's matches
    //
    // RATING FORMULA:
    //   Rating = Σ(RatingAttn × BiasX) / Σ(BiasX)
    //
    // WHERE:
    //   RatingAttn = OpponentRating × Factor
    //     - Win:      OpponentRating × 1.25 (RATINGWIN)
    //     - Loss:     OpponentRating × 0.75 (RATINGLOSE)
    //     - 8-Ball:   OpponentRating × 1.35 (RATING8BALL)
    //   
    //   BiasX = Weight for each frame (progressive weighting)
    //     - Oldest frame:  RatingWeighting - (4 × (TotalFrames - 1))
    //     - Each newer frame: Previous BiasX + 4
    //     - Newest frame:  RatingWeighting (always base weight)
    //
    // EXAMPLE (Player with 6 frames, Weighting=220, Bias=4):
    //   Frame 1 (oldest): BiasX = 220 - (4 × 5) = 200
    //   Frame 2:          BiasX = 204
    //   Frame 3:          BiasX = 208
    //   Frame 4:          BiasX = 212
    //   Frame 5:          BiasX = 216
    //   Frame 6 (newest): BiasX = 220
    //
    // IMPORTANT: VBA uses INTEGER arithmetic (truncation, not rounding) for RatingAttn
    //   e.g., 1000 × 1.25 = 1250 (integer)
    //   e.g., 1000 × 0.75 = 750 (integer)
    // ==================================================================

    private void RenderPlayerRatingsHeader()
    {
        RatingsHeaderGrid.ColumnDefinitions.Clear();
        RatingsHeaderGrid.Children.Clear();

        RatingsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        RatingsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        RatingsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        RatingsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        RatingsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        RatingsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        RatingsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        RatingsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
        RatingsHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

        string[] headers = { "#", "Player", "Team", "Played", "W", "L", "Win %", "8-Ball", "Rating" };
        TextAlignment[] aligns = {
            TextAlignment.Center, TextAlignment.Start, TextAlignment.Start, TextAlignment.Center,
            TextAlignment.Center, TextAlignment.Center, TextAlignment.Center, TextAlignment.Center,
            TextAlignment.Center
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var label = new Label
            {
                Text = headers[i],
                FontAttributes = FontAttributes.Bold,
                FontSize = 11,
                HorizontalTextAlignment = aligns[i],
                VerticalTextAlignment = TextAlignment.Center
            };
            RatingsHeaderGrid.Add(label, i, 0);
        }
    }

    private void RefreshPlayerRatings()
    {
        _playerRows.Clear();

        if (!_currentSeasonId.HasValue || _selectedDivision == null)
        {
            PlayerRatingsList.ItemTemplate = null;
            return;
        }

        var data = DataStore.Data;
        var tById = data.Teams.ToDictionary(t => t.Id, t => t);

        // Get teams in this division (for filtering display only)
        var divisionTeamIds = new HashSet<Guid>(
            data.Teams.Where(t => t.DivisionId == _selectedDivision.Id).Select(t => t.Id));

        // Get the season's START DATE (not earliest fixture date!)
        var season = data.Seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
        if (season == null)
        {
            SetStatus("Season not found");
            return;
        }
        var seasonStartDate = season.StartDate;

        // Get ALL fixtures for the season (not just this division!)
        var allSeasonFixtures = data.Fixtures
            .Where(f => f.SeasonId == _currentSeasonId)
            .Where(f => f.Frames.Any())
            .OrderBy(f => f.Date)
            .ThenBy(f => f.Id)
            .ToList();

        if (!allSeasonFixtures.Any())
        {
            SetStatus($"{_teamRows.Count} teams | 0 players (no results)");
            return;
        }

        // Track frames per player with week info
        var playerFrameData = new Dictionary<Guid, List<FrameData>>();
        
        // VBA tblRatings: stores rating GOING INTO each week (not after)
        // Week 1 = 1000 for all (before any matches)
        // Week 2 = calculated after Week 1 matches
        // Week N = calculated after Week N-1 matches
        var weeklyRatings = new Dictionary<(Guid, int), int>();
        
        // Get ALL player IDs and initialize for Week 1 (before any matches)
        var allPlayerIds = new HashSet<Guid>();
        foreach (var fixture in allSeasonFixtures)
        {
            foreach (var frame in fixture.Frames)
            {
                if (frame.HomePlayerId.HasValue) allPlayerIds.Add(frame.HomePlayerId.Value);
                if (frame.AwayPlayerId.HasValue) allPlayerIds.Add(frame.AwayPlayerId.Value);
            }
        }
        
        // VBA: Week 1 = 1000 for ALL players (this is BEFORE any matches)
        foreach (var playerId in allPlayerIds)
        {
            weeklyRatings[(playerId, 1)] = Settings.RatingStartValue;
        }

        // Group fixtures by week
        var fixturesByWeek = allSeasonFixtures
            .GroupBy(f => GetSeasonWeekNumber(f.Date, seasonStartDate))
            .OrderBy(g => g.Key)
            .ToList();

        int maxWeek = fixturesByWeek.Max(g => g.Key);

        // VBA Algorithm - Process week by week:
        // After processing Week N's frames, calculate and store rating for Week N+1
        for (int wkNo = 1; wkNo <= maxWeek; wkNo++)
        {
            // Add frames from this week
            var thisWeekFixtures = fixturesByWeek.FirstOrDefault(g => g.Key == wkNo);
            if (thisWeekFixtures != null)
            {
                foreach (var fixture in thisWeekFixtures.OrderBy(f => f.Date).ThenBy(f => f.Id))
                {
                    foreach (var frame in fixture.Frames.OrderBy(fr => fr.Number))
                    {
                        if (frame.HomePlayerId.HasValue)
                        {
                            var playerId = frame.HomePlayerId.Value;
                            if (!playerFrameData.ContainsKey(playerId))
                                playerFrameData[playerId] = new List<FrameData>();
                            
                            playerFrameData[playerId].Add(new FrameData
                            {
                                OpponentId = frame.AwayPlayerId ?? Guid.Empty,
                                Won = frame.Winner == FrameWinner.Home,
                                EightBall = frame.EightBall && frame.Winner == FrameWinner.Home,
                                WeekNo = wkNo
                            });
                        }
                        
                        if (frame.AwayPlayerId.HasValue)
                        {
                            var playerId = frame.AwayPlayerId.Value;
                            if (!playerFrameData.ContainsKey(playerId))
                                playerFrameData[playerId] = new List<FrameData>();
                            
                            playerFrameData[playerId].Add(new FrameData
                            {
                                OpponentId = frame.HomePlayerId ?? Guid.Empty,
                                Won = frame.Winner == FrameWinner.Away,
                                EightBall = frame.EightBall && frame.Winner == FrameWinner.Away,
                                WeekNo = wkNo
                            });
                        }
                    }
                }
            }

            // Calculate ratings for NEXT week (wkNo + 1)
            // VBA: After Week 1 matches, store as Week 2 rating
            foreach (var playerId in playerFrameData.Keys.ToList())
            {
                var framesUpToNow = playerFrameData[playerId].Where(f => f.WeekNo <= wkNo).ToList();
                if (framesUpToNow.Count == 0) continue;

                int totalFrames = framesUpToNow.Count;
                int biasX = Settings.RatingWeighting - (Settings.RatingsBias * (totalFrames - 1));
                if (biasX < 1) biasX = 1;

                long valueTot = 0;
                long weightingTot = 0;

                foreach (var frameData in framesUpToNow)
                {
                    // VBA: Opponent rating lookup uses the rating GOING INTO that frame's week
                    // For a frame played in Week 1, use opponent's Week 1 rating (1000)
                    // For a frame played in Week 2, use opponent's Week 2 rating
                    int oppRating = weeklyRatings.TryGetValue((frameData.OpponentId, frameData.WeekNo), out var r) 
                        ? r 
                        : Settings.RatingStartValue;

                    double ratingAttnDouble;
                    if (frameData.Won)
                    {
                        if (frameData.EightBall && Settings.UseEightBallFactor)
                            ratingAttnDouble = oppRating * Settings.EightBallFactor;
                        else
                            ratingAttnDouble = oppRating * Settings.WinFactor;
                    }
                    else
                    {
                        ratingAttnDouble = oppRating * Settings.LossFactor;
                    }

                    // Use integer truncation (not rounding) as VBA does
                    int ratingAttn = (int)ratingAttnDouble;
                    
                    valueTot += (long)ratingAttn * biasX;
                    weightingTot += biasX;
                    biasX += Settings.RatingsBias;
                }

                // Store as NEXT week's rating (VBA stores rating for week AFTER matches)
                int rating = weightingTot > 0 ? (int)(valueTot / weightingTot) : Settings.RatingStartValue;
                weeklyRatings[(playerId, wkNo + 1)] = rating;
            }
        }

        // Build display rows - filter to this division only
        var rows = new List<PlayerRow>();
        int finalWeek = maxWeek + 1; // Current rating = week after last matches

        foreach (var kvp in playerFrameData)
        {
            var playerId = kvp.Key;
            var frames = kvp.Value;

            var player = data.Players.FirstOrDefault(p => p.Id == playerId);
            if (player == null) continue;

            // FILTER: Only show players from teams in this division
            if (!player.TeamId.HasValue || !divisionTeamIds.Contains(player.TeamId.Value))
                continue;

            var teamName = player.TeamId.HasValue && tById.TryGetValue(player.TeamId.Value, out var t)
                ? (t.Name ?? "")
                : "";

            // Get final rating (the rating going into next week, after all matches played)
            int finalRating = weeklyRatings.TryGetValue((playerId, finalWeek), out var fr)
                ? fr
                : Settings.RatingStartValue;

            rows.Add(new PlayerRow
            {
                Player = player.FullName ?? $"{player.FirstName} {player.LastName}".Trim(),
                PlayerId = player.Id,
                Team = teamName,
                Played = frames.Count,
                Wins = frames.Count(f => f.Won),
                Losses = frames.Count(f => !f.Won),
                EightBalls = frames.Count(f => f.EightBall),
                Rating = finalRating
            });
        }

        // Calculate minimum frames required
        int maxFramesInSeason = rows.Any() ? rows.Max(r => r.Played) : 0;
        int minFramesRequired = Settings.CalculateMinimumFrames(maxFramesInSeason);

        // Filter by minimum frames
        var displayRows = rows.Where(r => r.Played >= minFramesRequired).ToList();

        // Sort
        switch (Math.Max(0, SortPicker.SelectedIndex))
        {
            case 0:
                displayRows = displayRows.OrderByDescending(r => r.Rating)
                    .ThenBy(r => r.Player, StringComparer.OrdinalIgnoreCase).ToList();
                break;
            case 1:
                displayRows = displayRows.OrderByDescending(r => r.WinPct)
                    .ThenByDescending(r => r.Played)
                    .ThenBy(r => r.Player, StringComparer.OrdinalIgnoreCase).ToList();
                break;
            case 2:
                displayRows = displayRows.OrderByDescending(r => r.Played)
                    .ThenByDescending(r => r.WinPct).ToList();
                break;
            case 3:
                displayRows = displayRows.OrderBy(r => r.Player, StringComparer.OrdinalIgnoreCase).ToList();
                break;
        }

        for (int i = 0; i < displayRows.Count; i++)
            displayRows[i].Pos = i + 1;

        PlayerRatingsList.ItemTemplate = PlayerRowTemplate();
        foreach (var r in displayRows)
            _playerRows.Add(r);

        if (maxFramesInSeason > 0 && minFramesRequired > 0)
            SetStatus($"{_teamRows.Count} teams | {displayRows.Count} players (min {minFramesRequired} frames, {Settings.MinFramesPercentage}%)");
        else
            SetStatus($"{_teamRows.Count} teams | {displayRows.Count} players");
    }

    // Helper class for frame data
    private class FrameData
    {
        public Guid OpponentId { get; set; }
        public bool Won { get; set; }
        public bool EightBall { get; set; }
        public int WeekNo { get; set; }
    }

    private static DataTemplate PlayerRowTemplate()
    {
        return new DataTemplate(() =>
        {
            var grid = new Grid
            {
                ColumnSpacing = 8,
                Padding = new Thickness(10, 6)
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            Label L(string path, TextAlignment align = TextAlignment.Center, bool bold = false, string? format = null)
            {
                var lbl = new Label
                {
                    HorizontalTextAlignment = align,
                    VerticalTextAlignment = TextAlignment.Center,
                    FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
                    FontSize = 12
                };
                lbl.SetBinding(Label.TextProperty, new Binding(path, stringFormat: format));
                return lbl;
            }

            grid.Add(L(nameof(PlayerRow.Pos), TextAlignment.Center, true), 0, 0);
            
            // Make player name a clickable link
            var playerNameLabel = new Label
            {
                HorizontalTextAlignment = TextAlignment.Start,
                VerticalTextAlignment = TextAlignment.Center,
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                TextColor = Color.FromArgb("#0066CC"),
                TextDecorations = TextDecorations.Underline
            };
            playerNameLabel.SetBinding(Label.TextProperty, new Binding(nameof(PlayerRow.Player)));
            grid.Add(playerNameLabel, 1, 0);
            
            grid.Add(L(nameof(PlayerRow.Team), TextAlignment.Start), 2, 0);
            grid.Add(L(nameof(PlayerRow.Played)), 3, 0);
            grid.Add(L(nameof(PlayerRow.Wins)), 4, 0);
            grid.Add(L(nameof(PlayerRow.Losses)), 5, 0);
            grid.Add(L(nameof(PlayerRow.WinPct), TextAlignment.Center, false, "{0:0.#}%"), 6, 0);
            grid.Add(L(nameof(PlayerRow.EightBalls)), 7, 0);
            grid.Add(L(nameof(PlayerRow.Rating), TextAlignment.Center, true), 8, 0);

            var border = new Border { StrokeShape = new RoundRectangle { CornerRadius = 8 }, Content = grid, StrokeThickness = 0 };
            
            // Add tap gesture to entire row
            var tapGesture = new TapGestureRecognizer();
            tapGesture.SetBinding(TapGestureRecognizer.CommandParameterProperty, new Binding("."));
            tapGesture.Tapped += async (s, e) =>
            {
                if (e is TappedEventArgs tapped && tapped.Parameter is PlayerRow row)
                {
                    var resultsPage = new PlayerResultsPage();
                    resultsPage.LoadPlayer(row.PlayerId, row.Player, row.Rating);
                    await Application.Current?.MainPage?.Navigation.PushAsync(resultsPage)!;
                }
            };
            border.GestureRecognizers.Add(tapGesture);

            return border;
        });
    }

    // ========== EXPORT ==========

    private async Task ExportCsvAsync()
    {
        if (_teamRows.Count == 0 && _playerRows.Count == 0)
        {
            await DisplayAlert("Export", "Nothing to export.", "OK");
            return;
        }

        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
        var divName = _selectedDivision?.Name?.Replace(" ", "_") ?? "All";

        var sb = new StringBuilder();

        // Export team table (no D column - WDPL best-of-15 has no draws)
        sb.AppendLine("=== DIVISION TABLE ===");
        sb.AppendLine("Pos,Team,P,W,L,F,A,Diff,Points");
        foreach (var o in _teamRows)
            sb.AppendLine($"{o.Pos},{Csv(o.Team)},{o.P},{o.W},{o.L},{o.F},{o.A},{o.Diff},{o.Pts}");

        sb.AppendLine();
        sb.AppendLine("=== PLAYER RATINGS ===");
        sb.AppendLine("Pos,Player,Team,Played,Wins,Losses,Win%,8-balls,Rating");
        foreach (var o in _playerRows)
            sb.AppendLine($"{o.Pos},{Csv(o.Player)},{Csv(o.Team)},{o.Played},{o.Wins},{o.Losses},{o.WinPct.ToString("0.#", CultureInfo.InvariantCulture)},{o.EightBalls},{o.Rating}");

        var fileName = $"LeagueTable_{season?.Name?.Replace(" ", "_")}_{divName}_{DateTime.Now:yyyyMMdd}.csv";
        var path = IOPath.Combine(FileSystem.CacheDirectory, fileName);
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Export League Table & Ratings",
            File = new ShareFile(path, "text/csv")
        });

        SetStatus("Exported to CSV");
    }

    private static string Csv(string? s)
    {
        s ??= "";
        return s.Contains(',') || s.Contains('"')
            ? "\"" + s.Replace("\"", "\"\"") + "\""
            : s;
    }

    private void OnRecalculateClicked()
    {
        SetStatus("Recalculating ratings...");
        
        // Force a full refresh of both team table and player ratings
        RefreshTeamTable();
        RefreshPlayerRatings();
        
        SetStatus($"Ratings recalculated at {DateTime.Now:HH:mm:ss}");
    }

    private async Task CompareWithVbaAsync()
    {
        try
        {
            SetStatus("Comparing with VBA data...");

            // Try multiple paths to find the VBA data files
            string? vbaRatingsData = null;
            string? vbaPlayersData = null;

            // Path 1: Check project VBA_Data folder (for development)
            var possiblePaths = new[]
            {
                // Development paths
                @"C:\Users\bobgc\source\repos\gazlappy\Wdplapp\wdpl2\VBA_Ratings_Data.txt",
                @"C:\Users\bobgc\source\repos\gazlappy\Wdplapp\wdpl2\VBA_Players_Data.txt",
                // Relative to app data
                IOPath.Combine(FileSystem.AppDataDirectory, "VBA_Ratings_Data.txt"),
                IOPath.Combine(FileSystem.AppDataDirectory, "VBA_Players_Data.txt"),
            };

            // Try to find ratings data
            foreach (var path in possiblePaths.Where(p => p.Contains("Ratings")))
            {
                if (File.Exists(path))
                {
                    vbaRatingsData = await File.ReadAllTextAsync(path);
                    break;
                }
            }

            // Try to find players data
            foreach (var path in possiblePaths.Where(p => p.Contains("Players")))
            {
                if (File.Exists(path))
                {
                    vbaPlayersData = await File.ReadAllTextAsync(path);
                    break;
                }
            }

            if (string.IsNullOrEmpty(vbaRatingsData))
            {
                await DisplayAlert("VBA Data Not Found", 
                    "Could not find VBA_Ratings_Data.txt.\n\n" +
                    "Please ensure the VBA data files exist at:\n" +
                    @"C:\Users\bobgc\source\repos\gazlappy\Wdplapp\wdpl2\VBA_Ratings_Data.txt", "OK");
                return;
            }

            // Parse VBA data
            var vbaRatings = VbaComparisonService.ParseVbaRatings(vbaRatingsData);
            var vbaPlayers = VbaComparisonService.ParseVbaPlayers(vbaPlayersData ?? "");

            if (vbaRatings.Count == 0)
            {
                await DisplayAlert("Error", "No VBA ratings data found or parsed.", "OK");
                return;
            }

            SetStatus($"Parsed {vbaRatings.Count} VBA ratings, {vbaPlayers.Count} players...");

            // Build player mapping
            var mauiPlayers = DataStore.Data.Players.ToList();
            var playerMapping = VbaComparisonService.BuildPlayerIdMapping(vbaPlayers, mauiPlayers);

            SetStatus($"Mapped {playerMapping.Count} players...");

            // Get season info
            var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _currentSeasonId);
            if (season == null)
            {
                await DisplayAlert("Error", "No season selected.", "OK");
                return;
            }

            // Get fixtures
            var fixtures = DataStore.Data.Fixtures
                .Where(f => f.SeasonId == _currentSeasonId && f.Frames.Any())
                .OrderBy(f => f.Date)
                .ToList();

            SetStatus($"Analyzing {fixtures.Count} fixtures...");

            // Run comparison
            var report = VbaComparisonService.CompareRatings(
                vbaRatings,
                vbaPlayers,
                playerMapping,
                fixtures,
                Settings,
                season.StartDate);

            // Save report to file
            var fileName = $"VBA_Comparison_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            var reportPath = IOPath.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(reportPath, report);

            // Show summary
            var lines = report.Split('\n');
            var summaryLine = lines.FirstOrDefault(l => l.StartsWith("SUMMARY:")) ?? "Comparison complete";
            
            var action = await DisplayActionSheet(
                $"VBA Comparison Complete\n{summaryLine}",
                "Close",
                null,
                "View Full Report",
                "Share Report");

            if (action == "View Full Report")
            {
                // Show in a scrollable popup
                var displayText = report.Length > 5000 
                    ? report.Substring(0, 5000) + "\n\n[Truncated - share for full report]" 
                    : report;
                await DisplayAlert("VBA Comparison Report", displayText, "OK");
            }
            else if (action == "Share Report")
            {
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = "VBA Comparison Report",
                    File = new ShareFile(reportPath, "text/plain")
                });
            }

            SetStatus($"VBA comparison: {summaryLine}");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Comparison failed: {ex.Message}\n\n{ex.StackTrace}", "OK");
            SetStatus($"Error: {ex.Message}");
        }
    }

    private void SetStatus(string text)
        => StatusLbl.Text = $"{DateTime.Now:HH:mm:ss}  {text}";

    // Get season week number (1-based, weeks since season start)
    private static int GetSeasonWeekNumber(DateTime matchDate, DateTime seasonStartDate)
    {
        // Calculate days since season start
        var daysSinceStart = (matchDate.Date - seasonStartDate).Days;
        // Week 1 = first week, Week 2 = days 7-13, etc.
        return (daysSinceStart / 7) + 1;
    }
}
