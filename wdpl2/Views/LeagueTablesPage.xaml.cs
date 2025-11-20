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
        public int D { get; set; }
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

        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        TeamTableHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });

        string[] headers = { "#", "Team", "P", "W", "D", "L", "F", "A", "Diff", "Pts" };
        TextAlignment[] aligns = {
            TextAlignment.Center, TextAlignment.Start, TextAlignment.Center, TextAlignment.Center,
            TextAlignment.Center, TextAlignment.Center, TextAlignment.Center, TextAlignment.Center,
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

            // NEW POINTS SYSTEM: Frames Won + Bonus
            if (hs > @as)
            {
                // Home wins
                hr.W++; ar.L++;
                hr.Pts += hs + Settings.MatchWinBonus;  // Frames won + win bonus
                ar.Pts += @as;                           // Just frames won (no bonus for loss)
            }
            else if (hs < @as)
            {
                // Away wins
                ar.W++; hr.L++;
                ar.Pts += @as + Settings.MatchWinBonus;  // Frames won + win bonus
                hr.Pts += hs;                             // Just frames won (no bonus for loss)
            }
            else
            {
                // Draw
                hr.D++; ar.D++;
                hr.Pts += hs + Settings.MatchDrawBonus;  // Frames won + draw bonus
                ar.Pts += @as + Settings.MatchDrawBonus; // Frames won + draw bonus
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

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });

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
            grid.Add(L(nameof(TeamRow.D)), 4, 0);
            grid.Add(L(nameof(TeamRow.L)), 5, 0);
            grid.Add(L(nameof(TeamRow.F)), 6, 0);
            grid.Add(L(nameof(TeamRow.A)), 7, 0);
            grid.Add(L(nameof(TeamRow.Diff)), 8, 0);
            grid.Add(L(nameof(TeamRow.Pts), TextAlignment.Center, true), 9, 0);

            return new Border { StrokeShape = new RoundRectangle { CornerRadius = 8 }, Content = grid, StrokeThickness = 0 };
        });
    }

    // ========== PLAYER RATINGS ==========

    // ⚠️ IMPORTANT NOTE: VBA Bug Replication & Weight Calculation
    // ==================================================================
    // The rating calculation below replicates the exact behavior from the original VBA Access database
    // to maintain compatibility with historical ratings.
    //
    // VBA CONSTANTS (from modGeneral):
    //   RATINGSTART = 1000
    //   Rating = 157 (base weighting constant)
  //   RatingBias = 4 (weight decrement per frame)
  //   RATINGWIN = 1.25
  //   RATINGLOSE = 0.75
    //   RATING8BALL = 1.35
    //
  // WEIGHT CALCULATION:
    //Starting biasX = 157 + (4 × totalFrames)
    //   For each frame: biasX decreases by 4
    //   Example for 24 frames:
    //     Frame 1: weight = 157 + (4 × 24) = 253 → uses 249 in calculation (bug)
    //     Frame 2: weight = 249 → uses 245
    //     Frame 24: weight = 161 → uses 157
//
    // THE BUG: In VBA (NewRatingsCalc line ~93), biasX is decremented AFTER WeightingTot
    // but BEFORE ValueTot, causing each frame to use the NEXT (lower) weight:
    //
    // VBA Code (with bug):
    //   WeightingTot = WeightingTot + BiasX     ' Uses current weight
  //   BiasX = BiasX - RatingBias     ' Decrements weight
  //ValueTot = ValueTot + (RatingAttn * BiasX)  ' ❌ Uses NEXT weight (BUG!)
    //
    // CORRECT Logic would be:
    //   weightingTot += biasX;
    //   valueTot += ratingAttn * biasX;  // Use CURRENT weight
    // biasX -= Settings.RatingsBias;   // Decrement AFTER
    //
    // TO RESTORE CORRECT CALCULATION:
    // In CalculateVBAStyleRating method, swap the last two lines to use current weight.
    // This will change all ratings but make them mathematically correct.
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

        // Get teams in this division
     var divisionTeamIds = new HashSet<Guid>(
  data.Teams.Where(t => t.DivisionId == _selectedDivision.Id).Select(t => t.Id));

        var fixtures = data.Fixtures
  .Where(f => f.SeasonId == _currentSeasonId)
         .Where(f => divisionTeamIds.Contains(f.HomeTeamId) || divisionTeamIds.Contains(f.AwayTeamId))
            .OrderBy(f => f.Date)
    .ToList();

        // Build frame history for each player
     // KEY CHANGE: Process fixtures in DATE-BASED BATCHES (like VBA's weekly processing)
        var playerFrames = new Dictionary<Guid, List<PlayerFrameHistory>>();
        var currentRatings = new Dictionary<Guid, int>();

        // Group fixtures by date (VBA-style: process all fixtures on same date, then update ratings)
        var fixturesByDate = fixtures.GroupBy(f => f.Date.Date).OrderBy(g => g.Key);

     foreach (var dateGroup in fixturesByDate)
        {
         // Snapshot ratings at START of this date's fixtures (VBA-style: uses previous batch's ratings)
      var batchStartRatings = new Dictionary<Guid, int>(currentRatings);
   
        foreach (var fixture in dateGroup)
            {
 foreach (var frame in fixture.Frames.OrderBy(fr => fr.Number))
         {
               // Process home player
        if (frame.HomePlayerId.HasValue)
       {
  var playerId = frame.HomePlayerId.Value;
        var oppId = frame.AwayPlayerId ?? Guid.Empty;
     // Use rating from BEFORE this batch started (VBA behavior)
             var oppRating = batchStartRatings.GetValueOrDefault(oppId, Settings.RatingStartValue);

 if (!playerFrames.ContainsKey(playerId))
              playerFrames[playerId] = new List<PlayerFrameHistory>();

    playerFrames[playerId].Add(new PlayerFrameHistory
         {
               PlayerId = playerId,
        OpponentId = oppId,
    OpponentRating = oppRating,
     Won = frame.Winner == FrameWinner.Home,
  EightBall = frame.EightBall && frame.Winner == FrameWinner.Home,
           FrameNumber = playerFrames[playerId].Count + 1,
     MatchDate = fixture.Date
        });
  }

    // Process away player
     if (frame.AwayPlayerId.HasValue)
           {
               var playerId = frame.AwayPlayerId.Value;
        var oppId = frame.HomePlayerId ?? Guid.Empty;
      // Use rating from BEFORE this batch started (VBA behavior)
    var oppRating = batchStartRatings.GetValueOrDefault(oppId, Settings.RatingStartValue);

                 if (!playerFrames.ContainsKey(playerId))
   playerFrames[playerId] = new List<PlayerFrameHistory>();

playerFrames[playerId].Add(new PlayerFrameHistory
         {
              PlayerId = playerId,
   OpponentId = oppId,
        OpponentRating = oppRating,
                Won = frame.Winner == FrameWinner.Away,
  EightBall = frame.EightBall && frame.Winner == FrameWinner.Away,
    FrameNumber = playerFrames[playerId].Count + 1,
       MatchDate = fixture.Date
    });
        }
  }
    }
        
 // AFTER processing entire DATE BATCH, update ratings (like VBA does at end of week)
            foreach (var playerId in playerFrames.Keys)
            {
  currentRatings[playerId] = CalculateVBAStyleRating(playerFrames[playerId]);
        }
   }

      // Calculate maximum frames and minimum required
        int maxFramesInSeason = playerFrames.Values.Any() ? playerFrames.Values.Max(frames => frames.Count) : 0;
  int minFramesRequired = Settings.CalculateMinimumFrames(maxFramesInSeason);

        // Build display rows
        var rows = new List<PlayerRow>();

        foreach (var kvp in playerFrames)
        {
 var playerId = kvp.Key;
         var frames = kvp.Value;

         var player = data.Players.FirstOrDefault(p => p.Id == playerId);
         if (player == null) continue;

   // Only include players from teams in this division
 if (!player.TeamId.HasValue || !divisionTeamIds.Contains(player.TeamId.Value))
                continue;

    var teamName = player.TeamId.HasValue && tById.TryGetValue(player.TeamId.Value, out var t)
     ? (t.Name ?? "")
         : "";

            var wins = frames.Count(f => f.Won);
            var losses = frames.Count(f => !f.Won);
            var eightBalls = frames.Count(f => f.EightBall);
     var rating = CalculateVBAStyleRating(frames);

            rows.Add(new PlayerRow
            {
Player = player.FullName ?? $"{player.FirstName} {player.LastName}".Trim(),
      PlayerId = player.Id,
     Team = teamName,
     Played = frames.Count,
      Wins = wins,
  Losses = losses,
   EightBalls = eightBalls,
            Rating = rating
      });
        }

// Filter by minimum frames
        var displayRows = rows.Where(r => r.Played >= minFramesRequired).ToList();

    // Sort based on picker
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

    private int CalculateVBAStyleRating(List<PlayerFrameHistory> frames)
    {
        // ⚠️ VBA BUG REPLICATION - Exact replication of NewRatingsCalc() from VBA Access
        // VBA Code:
        //   BiasX = RatingWeighting - (RatingBias * (RecordCount - 1))
        //   WeightingTot = WeightingTot + BiasX
        //   BiasX = BiasX + RatingBias   ' ← Increments BEFORE using in ValueTot
      //   ValueTot = ValueTot + (CLng(RatingAttn) * BiasX)  ' ← Uses INCREMENTED value + CLng!
   
        if (frames == null || frames.Count == 0)
            return Settings.RatingStartValue;

     long valueTot = 0;    // Use long to match VBA's Long type
        long weightingTot = 0;

        // VBA Formula: BiasX = RatingWeighting - (RatingBias * (RecordCount - 1))
        // For 24 frames: BiasX = 240 - (4 * 23) = 148 (starts LOW, then increments)
        int biasX = Settings.RatingWeighting - (Settings.RatingsBias * (frames.Count - 1));
        if (biasX < 1) biasX = 1;

        foreach (var frame in frames.OrderBy(f => f.FrameNumber))
        {
       double ratingAttn;

            if (frame.Won)
     {
  if (frame.EightBall && Settings.UseEightBallFactor)
                {
        ratingAttn = frame.OpponentRating * Settings.EightBallFactor;
          }
           else
        {
    ratingAttn = frame.OpponentRating * Settings.WinFactor;
                }
}
        else
        {
    ratingAttn = frame.OpponentRating * Settings.LossFactor;
          }

     // EXACT VBA BUG REPLICATION:
         // VBA: ValueTot = ValueTot + (CLng(RatingAttn) * BiasX)
        // CLng rounds to nearest integer BEFORE multiplying
            long ratingAttnLong = (long)Math.Round(ratingAttn);  // VBA's CLng behavior
    
            weightingTot += biasX;   // Add current weight to total
      biasX += Settings.RatingsBias;// Increment BEFORE using in valueTot (THE BUG!)
    valueTot += ratingAttnLong * biasX; // Use INCREMENTED weight with rounded rating
}

        return weightingTot > 0 ? (int)Math.Round((double)valueTot / weightingTot) : Settings.RatingStartValue;
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
            grid.Add(L(nameof(PlayerRow.Player), TextAlignment.Start, true), 1, 0);
            grid.Add(L(nameof(PlayerRow.Team), TextAlignment.Start), 2, 0);
            grid.Add(L(nameof(PlayerRow.Played)), 3, 0);
            grid.Add(L(nameof(PlayerRow.Wins)), 4, 0);
            grid.Add(L(nameof(PlayerRow.Losses)), 5, 0);
            grid.Add(L(nameof(PlayerRow.WinPct), TextAlignment.Center, false, "{0:0.#}%"), 6, 0);
            grid.Add(L(nameof(PlayerRow.EightBalls)), 7, 0);
            grid.Add(L(nameof(PlayerRow.Rating), TextAlignment.Center, true), 8, 0);

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

        // Export team table
        sb.AppendLine("=== DIVISION TABLE ===");
        sb.AppendLine("Pos,Team,P,W,D,L,F,A,Diff,Points");
        foreach (var o in _teamRows)
            sb.AppendLine($"{o.Pos},{Csv(o.Team)},{o.P},{o.W},{o.D},{o.L},{o.F},{o.A},{o.Diff},{o.Pts}");

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

    private void SetStatus(string text)
        => StatusLbl.Text = $"{DateTime.Now:HH:mm:ss}  {text}";
}

class PlayerFrameHistory
{
    public Guid PlayerId { get; set; }
    public Guid OpponentId { get; set; }
    public int OpponentRating { get; set; }
    public bool Won { get; set; }
    public bool EightBall { get; set; }
    public int FrameNumber { get; set; }
    public DateTime MatchDate { get; set; }
}
