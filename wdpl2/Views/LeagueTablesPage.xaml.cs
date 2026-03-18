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
        public int Ded { get; set; }
        public int Pts { get; set; }
        /// <summary>Zone background: green=promotion, red=relegation, transparent=mid-table.</summary>
        public Color ZoneColor { get; set; } = Colors.Transparent;
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

    private sealed class DoublesRow
    {
        public int Pos { get; set; }
        public string Player1 { get; set; } = "";
        public string Player2 { get; set; } = "";
        public string Team { get; set; } = "";
        public int Played { get; set; }
        public int Won { get; set; }
        public int Lost { get; set; }
        public int BestRating { get; set; }
        public string BestRatingDate { get; set; } = "";
        public int CurrentRating { get; set; }
        public string PairName => $"{Player1} & {Player2}";
    }

    private readonly ObservableCollection<TeamRow> _teamRows = new();
    private readonly ObservableCollection<PlayerRow> _playerRows = new();
    private readonly ObservableCollection<DoublesRow> _doublesRows = new();
    private readonly ObservableCollection<Division> _divisions = new();

    private Guid? _currentSeasonId;
    private Division? _selectedDivision;

    public LeagueTablesPage()
    {
        InitializeComponent();

        TeamTableList.ItemsSource = _teamRows;
        PlayerRatingsList.ItemsSource = _playerRows;
        DoublesRatingsList.ItemsSource = _doublesRows;
        DivisionPicker.ItemsSource = _divisions;

        DivisionPicker.SelectedIndexChanged += (_, __) => OnDivisionChanged();
        SortPicker.SelectedIndex = 0;
        SortPicker.SelectedIndexChanged += (_, __) => RefreshPlayerRatings();
        ExportBtn.Clicked += async (_, __) => await ExportCsvAsync();
        RecalculateBtn.Clicked += (_, __) => OnRecalculateClicked();

        // SUBSCRIBE to global season changes
        SeasonService.Current.SeasonChanged += OnGlobalSeasonChanged;

        RefreshAll();
    }

    ~LeagueTablesPage()
    {
        SeasonService.Current.SeasonChanged -= OnGlobalSeasonChanged;
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
        _currentSeasonId = SeasonService.Current.CurrentSeasonId;
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
        RenderDoublesRatingsHeader();
        RefreshDoublesRatings();
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
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });  // Ded
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });  // Pts

        // Removed "D" (Drawn) column - WDPL uses best-of-15 so draws not possible
        string[] headers = { "#", "Team", "P", "W", "L", "F", "A", "Diff", "Ded", "Pts" };
        TextAlignment[] aligns = {
            TextAlignment.Center, TextAlignment.Start, TextAlignment.Center, TextAlignment.Center,
            TextAlignment.Center, TextAlignment.Center, TextAlignment.Center,
            TextAlignment.Center, TextAlignment.Center, TextAlignment.Center
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
            .Where(f => f.Frames.Count != 0)
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

            // Apply late card penalties
            if (f.HomeLatePenalty > 0)
            {
                hr.Ded += f.HomeLatePenalty;
                hr.Pts -= f.HomeLatePenalty;
            }
            if (f.AwayLatePenalty > 0)
            {
                ar.Ded += f.AwayLatePenalty;
                ar.Pts -= f.AwayLatePenalty;
            }

            // Apply cancellation penalty
            if (f.CancelledByTeam == FrameWinner.Home && f.CancellationPenalty > 0)
            {
                hr.Ded += f.CancellationPenalty;
                hr.Pts -= f.CancellationPenalty;
            }
            else if (f.CancelledByTeam == FrameWinner.Away && f.CancellationPenalty > 0)
            {
                ar.Ded += f.CancellationPenalty;
                ar.Pts -= f.CancellationPenalty;
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

        // Assign promotion/relegation zone colours
        var promoSlots = Settings.PromotionSlots;
        var relegSlots = Settings.RelegationSlots;
        for (int i = 0; i < rows.Count; i++)
        {
            if (promoSlots > 0 && rows[i].Pos <= promoSlots)
                rows[i].ZoneColor = Color.FromArgb("#DCFCE7"); // green – promotion
            else if (relegSlots > 0 && rows[i].Pos > rows.Count - relegSlots)
                rows[i].ZoneColor = Color.FromArgb("#FEE2E2"); // red – relegation
        }

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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });  // Ded
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
            
            // Make team name a clickable link
            var teamNameLabel = new Label
            {
                HorizontalTextAlignment = TextAlignment.Start,
                VerticalTextAlignment = TextAlignment.Center,
                FontAttributes = FontAttributes.Bold,
                FontSize = 12,
                TextColor = Color.FromArgb("#0066CC"),
                TextDecorations = TextDecorations.Underline
            };
            teamNameLabel.SetBinding(Label.TextProperty, new Binding(nameof(TeamRow.Team)));
            grid.Add(teamNameLabel, 1, 0);
            
            grid.Add(L(nameof(TeamRow.P)), 2, 0);
            grid.Add(L(nameof(TeamRow.W)), 3, 0);
            grid.Add(L(nameof(TeamRow.L)), 4, 0);
            grid.Add(L(nameof(TeamRow.F)), 5, 0);
            grid.Add(L(nameof(TeamRow.A)), 6, 0);
            grid.Add(L(nameof(TeamRow.Diff)), 7, 0);

            // Ded column - show in red if > 0
            var dedLabel = new Label
            {
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                FontSize = 12,
                TextColor = Color.FromArgb("#DC2626")
            };
            dedLabel.SetBinding(Label.TextProperty, new Binding(nameof(TeamRow.Ded)));
            grid.Add(dedLabel, 8, 0);

            grid.Add(L(nameof(TeamRow.Pts), TextAlignment.Center, true), 9, 0);

            var border = new Border { StrokeShape = new RoundRectangle { CornerRadius = 8 }, Content = grid, StrokeThickness = 0 };
            border.SetBinding(Border.BackgroundColorProperty, new Binding(nameof(TeamRow.ZoneColor)));

            // Add tap gesture to navigate to team details
            var tapGesture = new TapGestureRecognizer();
            tapGesture.SetBinding(TapGestureRecognizer.CommandParameterProperty, new Binding("."));
            tapGesture.Tapped += async (s, e) =>
            {
                if (e is TappedEventArgs tapped && tapped.Parameter is TeamRow row)
                {
                    // Navigate to TeamsPage with the selected team
                    var teamsPage = new TeamsPage();
                    teamsPage.SelectTeam(row.TeamId);
                    await Application.Current?.MainPage?.Navigation.PushAsync(teamsPage)!;
                }
            };
            border.GestureRecognizers.Add(tapGesture);

            return border;
        });
    }

    // ========== PLAYER RATINGS ==========

    // ? VBA RATING ALGORITHM - Based on analysis of tblRatings and tblPlayerResult data
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
    //   Rating = S(RatingAttn × BiasX) / S(BiasX)
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
            .Where(f => f.Frames.Count != 0)
            .OrderBy(f => f.Date)
            .ThenBy(f => f.Id)
            .ToList();

        if (allSeasonFixtures.Count == 0)
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
                        // Get week number from frame if available (VBA import), otherwise use calculated week
                        var frameWeekNo = frame.WeekNo ?? wkNo;
                        
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
                                WeekNo = frameWeekNo,
                                // VBA pre-calculated values (if available from SQL import)
                                VbaOppRating = frame.HomeOppRating,
                                VbaPlayerRating = frame.HomePlayerRating
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
                                WeekNo = frameWeekNo,
                                // VBA pre-calculated values (if available from SQL import)
                                VbaOppRating = frame.AwayOppRating,
                                VbaPlayerRating = frame.AwayPlayerRating
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
                    int ratingAttn;
                    
                    // Use VBA pre-calculated PlayerRating if available (exact match with VBA)
                    // Otherwise calculate from opponent's weekly rating
                    if (frameData.VbaPlayerRating.HasValue && frameData.VbaPlayerRating.Value > 0)
                    {
                        // Use the exact value stored by VBA at time of result entry
                        ratingAttn = frameData.VbaPlayerRating.Value;
                    }
                    else
                    {
                        // Fallback: Calculate using opponent's weekly rating
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
                        ratingAttn = (int)ratingAttnDouble;
                    }
                    
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
        int maxFramesInSeason = rows.Count != 0 ? rows.Max(r => r.Played) : 0;
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
        
        // VBA pre-calculated values (from SQL import)
        // If these are set, use them directly instead of recalculating
        public int? VbaOppRating { get; set; }
        public int? VbaPlayerRating { get; set; }
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

    // ========== DOUBLES RATINGS ==========

    private void RenderDoublesRatingsHeader()
    {
        DoublesHeaderGrid.ColumnDefinitions.Clear();
        DoublesHeaderGrid.Children.Clear();

        DoublesHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });   // #
        DoublesHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });       // Pair
        DoublesHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });   // Team
        DoublesHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });    // P
        DoublesHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });    // W
        DoublesHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });    // L
        DoublesHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });    // Best
        DoublesHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });    // Best On
        DoublesHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });    // Current

        string[] headers = { "#", "Pair", "Team", "Played", "W", "L", "Best", "Best On", "Rating" };
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
            DoublesHeaderGrid.Add(label, i, 0);
        }
    }

    private void RefreshDoublesRatings()
    {
        _doublesRows.Clear();

        if (!_currentSeasonId.HasValue || _selectedDivision == null)
        {
            DoublesRatingsList.ItemTemplate = null;
            DoublesRatingsBorder.IsVisible = false;
            return;
        }

        var data = DataStore.Data;

        // Get doubles pairings for this season and division
        var pairings = data.DoublesPairings
            .Where(dp => dp.SeasonId == _currentSeasonId &&
                         dp.DivisionId == _selectedDivision.Id)
            .OrderByDescending(dp => dp.CurrentRating)
            .ThenByDescending(dp => dp.Won)
            .ToList();

        // If no stored pairings for this division, try to calculate from doubles frames
        if (pairings.Count == 0)
        {
            pairings = CalculateDoublesFromFrames();
        }

        if (pairings.Count == 0)
        {
            DoublesRatingsBorder.IsVisible = false;
            return;
        }

        DoublesRatingsBorder.IsVisible = true;

        var rows = new List<DoublesRow>();
        for (int i = 0; i < pairings.Count; i++)
        {
            var dp = pairings[i];
            rows.Add(new DoublesRow
            {
                Pos = i + 1,
                Player1 = ResolvePlayerName(dp.Player1Id, dp.Player1Name),
                Player2 = ResolvePlayerName(dp.Player2Id, dp.Player2Name),
                Team = ResolveTeamName(dp.TeamId, dp.TeamName),
                Played = dp.Played,
                Won = dp.Won,
                Lost = dp.Lost,
                BestRating = dp.BestRating,
                BestRatingDate = dp.BestRatingDate?.ToString("dd/MM/yy") ?? "",
                CurrentRating = dp.CurrentRating
            });
        }

        DoublesRatingsList.ItemTemplate = DoublesRowTemplate();
        foreach (var r in rows)
            _doublesRows.Add(r);
    }

    private string ResolvePlayerName(Guid? playerId, string fallbackName)
    {
        if (playerId.HasValue)
        {
            var player = DataStore.Data.Players.FirstOrDefault(p => p.Id == playerId.Value);
            if (player != null)
                return player.FullName ?? $"{player.FirstName} {player.LastName}".Trim();
        }
        return fallbackName;
    }

    private static string ResolveTeamName(Guid? teamId, string fallbackName)
    {
        if (teamId.HasValue)
        {
            var team = DataStore.Data.Teams.FirstOrDefault(t => t.Id == teamId.Value);
            if (team != null)
                return team.Name ?? fallbackName;
        }
        return fallbackName;
    }

    /// <summary>
    /// Calculate doubles pair stats from doubles frames (FrameResult.IsDoubles == true)
    /// when no stored DoublesPairing records exist.
    /// </summary>
    private List<DoublesPairing> CalculateDoublesFromFrames()
    {
        if (!_currentSeasonId.HasValue || _selectedDivision == null)
            return new();

        var data = DataStore.Data;
        var divisionTeamIds = new HashSet<Guid>(
            data.Teams.Where(t => t.DivisionId == _selectedDivision.Id).Select(t => t.Id));

        var doublesFixtures = data.Fixtures
            .Where(f => f.SeasonId == _currentSeasonId &&
                        (divisionTeamIds.Contains(f.HomeTeamId) || divisionTeamIds.Contains(f.AwayTeamId)))
            .Where(f => f.Frames.Any(fr => fr.IsDoubles))
            .ToList();

        if (doublesFixtures.Count == 0) return new();

        // Track pair stats: key = sorted (player1Id, player2Id)
        var pairStats = new Dictionary<(Guid, Guid), DoublesPairing>();
        var playerById = data.Players.ToDictionary(p => p.Id, p => p);
        var teamById = data.Teams.ToDictionary(t => t.Id, t => t);

        foreach (var fixture in doublesFixtures)
        {
            foreach (var frame in fixture.Frames.Where(fr => fr.IsDoubles))
            {
                if (!frame.HomePlayerId.HasValue || !frame.AwayPlayerId.HasValue) continue;

                // For simplified doubles storage (one player per side), track individual participation
                var homeId = frame.HomePlayerId.Value;
                var awayId = frame.AwayPlayerId.Value;
                var pairKey = homeId.CompareTo(awayId) < 0 ? (homeId, awayId) : (awayId, homeId);

                // This simplified approach treats each stored doubles frame as a pair encounter
                // In reality, we need both players per side, but the current model only stores one
                // So we skip calculation and rely on imported data
            }
        }

        return new();
    }

    private static DataTemplate DoublesRowTemplate()
    {
        return new DataTemplate(() =>
        {
            var grid = new Grid
            {
                ColumnSpacing = 8,
                Padding = new Thickness(10, 6)
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });   // #
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });       // Pair
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });   // Team
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });    // P
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });    // W
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });    // L
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });    // Best
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });    // Best On
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });    // Current

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

            grid.Add(L(nameof(DoublesRow.Pos), TextAlignment.Center, true), 0, 0);
            grid.Add(L(nameof(DoublesRow.PairName), TextAlignment.Start, true), 1, 0);
            grid.Add(L(nameof(DoublesRow.Team), TextAlignment.Start), 2, 0);
            grid.Add(L(nameof(DoublesRow.Played)), 3, 0);
            grid.Add(L(nameof(DoublesRow.Won)), 4, 0);
            grid.Add(L(nameof(DoublesRow.Lost)), 5, 0);
            grid.Add(L(nameof(DoublesRow.BestRating), TextAlignment.Center, false), 6, 0);
            grid.Add(L(nameof(DoublesRow.BestRatingDate), TextAlignment.Center, false), 7, 0);
            grid.Add(L(nameof(DoublesRow.CurrentRating), TextAlignment.Center, true), 8, 0);

            return new Border { StrokeShape = new RoundRectangle { CornerRadius = 8 }, Content = grid, StrokeThickness = 0 };
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

        if (_doublesRows.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("=== DOUBLES RATINGS ===");
            sb.AppendLine("Pos,Player 1,Player 2,Team,Played,Won,Lost,Best Rating,Best On,Current Rating");
            foreach (var o in _doublesRows)
                sb.AppendLine($"{o.Pos},{Csv(o.Player1)},{Csv(o.Player2)},{Csv(o.Team)},{o.Played},{o.Won},{o.Lost},{o.BestRating},{o.BestRatingDate},{o.CurrentRating}");
        }

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
        RefreshDoublesRatings();

        SetStatus($"Ratings recalculated at {DateTime.Now:HH:mm:ss}");
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
