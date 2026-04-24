using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Wdpl2.Helpers;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

/// <summary>
/// Tournament bracket generation and display — professional look with tap-to-score input.
/// </summary>
public partial class CompetitionsPage
{
    // ── Colour palette ──────────────────────────────────────────────────
    static readonly Color _accentBlue      = Color.FromArgb("#3B82F6");
    static readonly Color _accentGreen     = Color.FromArgb("#10B981");
    static readonly Color _winnerGreenBg   = Color.FromArgb("#ECFDF5");
    static readonly Color _winnerGreenText = Color.FromArgb("#065F46");
    static readonly Color _loserGrayBg     = Color.FromArgb("#F9FAFB");
    static readonly Color _borderDefault   = Color.FromArgb("#E5E7EB");
    static readonly Color _borderComplete  = Color.FromArgb("#10B981");
    static readonly Color _headerBg        = Color.FromArgb("#F8FAFC");
    static readonly Color _subtleText      = Color.FromArgb("#6B7280");
    static readonly Color _dangerRed       = Color.FromArgb("#EF4444");
    static readonly Color _scoreBtnBg      = Color.FromArgb("#F3F4F6");
    static readonly Color _scoreBtnActive  = Color.FromArgb("#DBEAFE");

    // ── Bracket entry points ────────────────────────────────────────────

    private async void OnGenerateBracket()
    {
        if (_editorViewModel == null) return;

        if (_selectedCompetition?.Rounds.Count > 0)
        {
            var confirm = await DisplayAlert("Regenerate Bracket",
                "This will overwrite the existing bracket and any entered scores. Continue?",
                "Yes, Regenerate", "Cancel");
            if (!confirm) return;
        }

        await _editorViewModel.GenerateBracketCommand.ExecuteAsync(false);
        SetStatus(_editorViewModel.StatusMessage);
    }

    private async void OnRandomDraw()
    {
        if (_editorViewModel == null) return;

        if (_selectedCompetition?.Rounds.Count > 0)
        {
            var confirm = await DisplayAlert("Random Draw",
                "This will overwrite the existing bracket and any entered scores. Continue?",
                "Yes, Random Draw", "Cancel");
            if (!confirm) return;
        }

        await _editorViewModel.GenerateBracketCommand.ExecuteAsync(true);
        SetStatus(_editorViewModel.StatusMessage);
    }

    private void OnViewBracket()
    {
        if (_selectedCompetition == null) return;

        if (_selectedCompetition.Rounds.Count == 0)
        {
            SetStatus("No bracket generated yet. Click 'Generate Bracket' first.");
            return;
        }

        if (_selectedCompetition.Format == CompetitionFormat.RoundRobin)
            ShowRoundRobinView(_selectedCompetition);
        else
            ShowTournamentBracket(_selectedCompetition);
    }

    private async void OnManualDraw()
    {
        if (_editorViewModel == null) return;

        if (_selectedCompetition?.Rounds.Count > 0)
        {
            var confirm = await DisplayAlert("Manual Draw",
                "This will create an empty bracket for you to assign matchups. Any existing bracket will be overwritten. Continue?",
                "Yes, Manual Draw", "Cancel");
            if (!confirm) return;
        }

        await _editorViewModel.GenerateManualBracketCommand.ExecuteAsync(null);
        SetStatus(_editorViewModel.StatusMessage);

        // Jump straight into the bracket view so the user can start assigning
        if (_selectedCompetition?.Rounds.Count > 0)
            ShowTournamentBracket(_selectedCompetition);
    }

    private async void OnCreateLosersCup(object? sender, EventArgs e)
    {
        if (_editorViewModel == null || _selectedCompetition == null) return;

        var confirm = await DisplayAlert(
            "Create Losers Cup",
            "This will create a new competition with all first-round losers automatically entered. Continue?",
            "Yes, Create",
            "Cancel");

        if (!confirm) return;

        await _editorViewModel.CreateLosersCupCommand.ExecuteAsync(null);
        SetStatus(_editorViewModel.StatusMessage);

        // Refresh the competition list so the new entry appears
        await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);
        RefreshList();

        // Refresh the editor to show the "Losers Cup created" state
        ShowCompetitionEditor(_selectedCompetition);
    }

    private async void OnOpenLosersCup(object? sender, EventArgs e)
    {
        if (_editorViewModel == null || _selectedCompetition?.PlateCompetitionId == null) return;

        var plateId = _selectedCompetition.PlateCompetitionId.Value;

        // Find the losers cup in the loaded competitions
        var losersCup = _viewModel.Competitions.FirstOrDefault(c => c.Id == plateId)
                     ?? _viewModel.ActiveCompetitions.FirstOrDefault(c => c.Id == plateId);

        if (losersCup == null)
        {
            // It might not be loaded yet — reload and try again
            await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);
            RefreshList();
            losersCup = _viewModel.Competitions.FirstOrDefault(c => c.Id == plateId);
        }

        if (losersCup != null)
        {
            OnCompetitionTapped(losersCup);
        }
        else
        {
            SetStatus("Losers Cup competition not found");
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  KNOCKOUT BRACKET
    // ════════════════════════════════════════════════════════════════════

    private void ShowTournamentBracket(Competition competition)
    {
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            },
            RowSpacing = 0
        };

        // ── Top bar ─────────────────────────────────────────────────────
        root.Add(CreateBracketHeader(competition, isRoundRobin: false), 0, 0);

        // ── Bracket area ────────────────────────────────────────────────
        var bracketGrid = BuildKnockoutBracket(competition);
        var scroll = new ScrollView
        {
            Orientation = ScrollOrientation.Both,
            Content = bracketGrid
        };
        root.Add(scroll, 0, 1);

        SetContentPanel(root);
    }

    private Grid BuildKnockoutBracket(Competition competition)
    {
        int totalRounds = competition.Rounds.Count;
        if (totalRounds == 0) return new Grid();

        var grid = new Grid
        {
            ColumnSpacing = 12,
            RowSpacing = 0,
            Padding = new Thickness(16, 8)
        };

        // One column per round
        for (int r = 0; r < totalRounds; r++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

        // Single content row
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // header
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star }); // matches

        // ── Round headers ───────────────────────────────────────────────
        for (int r = 0; r < totalRounds; r++)
        {
            var round = competition.Rounds[r];
            string label = r == totalRounds - 1
                ? "Final"
                : r == totalRounds - 2 ? "Semi-Finals" : round.Name ?? $"Round {r + 1}";

            var hdrStack = new VerticalStackLayout
            {
                Spacing = 2,
                Margin = new Thickness(0, 0, 0, 6)
            };
            hdrStack.Children.Add(new Label
            {
                Text = label,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = _subtleText,
                HorizontalTextAlignment = TextAlignment.Center
            });
            hdrStack.Children.Add(new Label
            {
                Text = round.Date.HasValue
                    ? round.Date.Value.ToString("dd MMM yyyy")
                    : "Date TBC",
                FontSize = 11,
                TextColor = _subtleText,
                HorizontalTextAlignment = TextAlignment.Center
            });

            // Venues & tables
            if (round.SelectedVenues.Count > 0)
            {
                var venueText = string.Join(", ", round.SelectedVenues.Select(v =>
                {
                    var tables = string.Join(", ", v.SelectedTables.Select(t => t.Label));
                    return string.IsNullOrEmpty(tables) ? v.VenueName : $"{v.VenueName} ({tables})";
                }));
                hdrStack.Children.Add(new Label
                {
                    Text = $"\U0001F4CD {venueText}",
                    FontSize = 10,
                    TextColor = _subtleText,
                    HorizontalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.TailTruncation
                });
            }

            grid.Add(hdrStack, r, 0);
        }

        // ── Matches per round (simple vertical stack with proportional spacing) ─
        int firstRoundCount = competition.Rounds[0].Matches.Count;

        for (int r = 0; r < totalRounds; r++)
        {
            var round = competition.Rounds[r];
            int matchCount = round.Matches.Count;

            // Spacing to vertically centre later-round matches against their feeders
            // Each successive round has half as many matches — double the gap.
            int spacingMultiplier = 1 << r; // 1, 2, 4, 8 …
            double gap = Math.Max(0, (spacingMultiplier - 1) * 40);

            var stack = new VerticalStackLayout
            {
                Spacing = gap,
                VerticalOptions = LayoutOptions.Center
            };

            for (int m = 0; m < matchCount; m++)
                stack.Children.Add(CreateMatchCard(round.Matches[m], competition.Format, competition));

            grid.Add(stack, r, 1);
        }

        return grid;
    }

    // ════════════════════════════════════════════════════════════════════
    //  ROUND ROBIN
    // ════════════════════════════════════════════════════════════════════

    private void ShowRoundRobinView(Competition competition)
    {
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            },
            RowSpacing = 0
        };

        root.Add(CreateBracketHeader(competition, isRoundRobin: true), 0, 0);

        var body = new VerticalStackLayout { Spacing = 16, Padding = new Thickness(12, 8) };

        // Standings table
        body.Children.Add(CreateRoundRobinStandings(competition));

        // Rounds
        foreach (var round in competition.Rounds)
        {
            body.Children.Add(CreateRoundCard(round, competition));
        }

        root.Add(body, 0, 1);
        SetContentPanel(root);
    }

    private View CreateRoundCard(CompetitionRound round, Competition competition)
    {
        var completedCount = round.Matches.Count(m => m.IsComplete);
        var totalCount = round.Matches.Count;

        var stack = new VerticalStackLayout { Spacing = 6 };

        // Round header row
        var hdrGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Margin = new Thickness(0, 0, 0, 4)
        };
        hdrGrid.Add(new Label
        {
            Text = round.Name ?? $"Round {round.RoundNumber}",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold
        }, 0, 0);
        hdrGrid.Add(new Label
        {
            Text = $"{completedCount}/{totalCount} played",
            FontSize = 12,
            TextColor = completedCount == totalCount ? _accentGreen : _subtleText,
            VerticalTextAlignment = TextAlignment.Center
        }, 1, 0);
        stack.Children.Add(hdrGrid);

        // Date and venue info
        if (round.Date.HasValue || round.SelectedVenues.Count > 0)
        {
            var infoStack = new HorizontalStackLayout { Spacing = 12, Margin = new Thickness(0, 0, 0, 4) };

            if (round.Date.HasValue)
            {
                infoStack.Children.Add(new Label
                {
                    Text = $"\U0001F4C5 {round.Date.Value:dd MMM yyyy}",
                    FontSize = 11,
                    TextColor = _subtleText
                });
            }

            if (round.SelectedVenues.Count > 0)
            {
                var venueText = string.Join(", ", round.SelectedVenues.Select(v =>
                {
                    var tables = string.Join(", ", v.SelectedTables.Select(t => t.Label));
                    return string.IsNullOrEmpty(tables) ? v.VenueName : $"{v.VenueName} ({tables})";
                }));
                infoStack.Children.Add(new Label
                {
                    Text = $"\U0001F4CD {venueText}",
                    FontSize = 11,
                    TextColor = _subtleText,
                    LineBreakMode = LineBreakMode.TailTruncation
                });
            }

            stack.Children.Add(infoStack);
        }

        foreach (var match in round.Matches)
            stack.Children.Add(CreateMatchCard(match, competition.Format, competition));

        return new Border
        {
            Stroke = _borderDefault,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            BackgroundColor = Colors.White,
            Padding = 12,
            Content = stack
        };
    }

    private View CreateRoundRobinStandings(Competition competition)
    {
        var stats = new Dictionary<Guid, (int played, int won, int drawn, int lost, int ff, int fa, int pts)>();

        foreach (var round in competition.Rounds)
        {
            foreach (var match in round.Matches)
            {
                if (match.Participant1Id.HasValue && !stats.ContainsKey(match.Participant1Id.Value))
                    stats[match.Participant1Id.Value] = (0, 0, 0, 0, 0, 0, 0);
                if (match.Participant2Id.HasValue && !stats.ContainsKey(match.Participant2Id.Value))
                    stats[match.Participant2Id.Value] = (0, 0, 0, 0, 0, 0, 0);

                if (!match.IsComplete) continue;
                if (!match.Participant1Id.HasValue || !match.Participant2Id.HasValue) continue;

                var p1 = match.Participant1Id.Value;
                var p2 = match.Participant2Id.Value;
                var s1 = stats[p1]; var s2 = stats[p2];

                s1.played++; s2.played++;
                s1.ff += match.Participant1Score; s1.fa += match.Participant2Score;
                s2.ff += match.Participant2Score; s2.fa += match.Participant1Score;

                if (match.Participant1Score > match.Participant2Score)
                { s1.won++; s1.pts += match.Participant1Score + 2; s2.lost++; s2.pts += match.Participant2Score; }
                else if (match.Participant2Score > match.Participant1Score)
                { s2.won++; s2.pts += match.Participant2Score + 2; s1.lost++; s1.pts += match.Participant1Score; }
                else
                { s1.drawn++; s1.pts += match.Participant1Score + 1; s2.drawn++; s2.pts += match.Participant2Score + 1; }

                stats[p1] = s1; stats[p2] = s2;
            }
        }

        var sorted = StandingsSorter.Sort(
            stats,
            DataStore.Data.GetSettingsForSeason(_currentSeasonId),
            s => s.Value.pts,
            s => s.Value.ff,
            s => s.Value.fa,
            s => s.Value.won,
            s => s.Key);

        var table = new VerticalStackLayout { Spacing = 0 };

        // Title
        table.Children.Add(new Label
        {
            Text = "STANDINGS",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = _subtleText,
            Margin = new Thickness(8, 0, 0, 6),
            CharacterSpacing = 1.5
        });

        // Header
        table.Children.Add(BuildStandingsRow("#", "Player", "P", "W", "D", "L", "FD", "Pts", isHeader: true, pos: 0));

        int pos = 1;
        foreach (var entry in sorted)
        {
            var name = GetParticipantName(entry.Key, competition.Format) ?? "Unknown";
            var s = entry.Value;
            var fd = s.ff - s.fa;
            table.Children.Add(BuildStandingsRow(
                pos.ToString(), name,
                s.played.ToString(), s.won.ToString(), s.drawn.ToString(), s.lost.ToString(),
                (fd >= 0 ? "+" : "") + fd, s.pts.ToString(),
                isHeader: false, pos: pos));
            pos++;
        }

        return new Border
        {
            Stroke = _borderDefault,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            BackgroundColor = Colors.White,
            Padding = 10,
            Content = table
        };
    }

    private static View BuildStandingsRow(string rank, string name,
        string p, string w, string d, string l, string fd, string pts,
        bool isHeader, int pos)
    {
        var bg = isHeader ? _headerBg : (pos % 2 == 0 ? Color.FromArgb("#F9FAFB") : Colors.White);
        var fs = isHeader ? 11 : 12;
        var attr = isHeader ? FontAttributes.Bold : FontAttributes.None;
        var ptsAttr = FontAttributes.Bold;

        var g = new Grid
        {
            Padding = new Thickness(10, 7),
            BackgroundColor = bg,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(28) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(32) },
                new ColumnDefinition { Width = new GridLength(42) },
                new ColumnDefinition { Width = new GridLength(42) },
            }
        };

        g.Add(new Label { Text = rank, FontSize = fs, FontAttributes = attr, TextColor = _subtleText }, 0, 0);
        g.Add(new Label { Text = name, FontSize = fs, FontAttributes = attr, LineBreakMode = LineBreakMode.TailTruncation }, 1, 0);
        g.Add(CentredLabel(p, fs, attr), 2, 0);
        g.Add(CentredLabel(w, fs, attr), 3, 0);
        g.Add(CentredLabel(d, fs, attr), 4, 0);
        g.Add(CentredLabel(l, fs, attr), 5, 0);
        g.Add(CentredLabel(fd, fs, attr), 6, 0);
        g.Add(CentredLabel(pts, fs, ptsAttr), 7, 0);
        return g;
    }

    private static Label CentredLabel(string text, int fs, FontAttributes attr) =>
        new() { Text = text, FontSize = fs, FontAttributes = attr, HorizontalTextAlignment = TextAlignment.Center };

    // ════════════════════════════════════════════════════════════════════
    //  SHARED: HEADER BAR
    // ════════════════════════════════════════════════════════════════════

    private View CreateBracketHeader(Competition competition, bool isRoundRobin)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12,
            Padding = new Thickness(16, 10),
            BackgroundColor = _headerBg
        };

        var backBtn = new Button
        {
            Text = "\u2190 Back",
            BackgroundColor = Colors.Transparent,
            TextColor = _accentBlue,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(8, 4),
            FontSize = 13,
            BorderWidth = 0
        };
        backBtn.Clicked += (_, _) => ShowCompetitionEditor(competition);

        var title = new Label
        {
            Text = competition.Name,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var subtitle = new Label
        {
            Text = isRoundRobin ? "Round Robin" : "Knockout Bracket",
            FontSize = 12,
            TextColor = _subtleText,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var titleStack = new VerticalStackLayout { Spacing = 0 };
        titleStack.Children.Add(title);
        titleStack.Children.Add(subtitle);

        var saveBtn = new Button
        {
            Text = "\u2714 Save Scores",
            BackgroundColor = _accentGreen,
            TextColor = Colors.White,
            FontSize = 13,
            Padding = new Thickness(14, 8),
            CornerRadius = 8
        };
        saveBtn.Clicked += (_, _) =>
        {
            ApplyAllScores(competition);
            if (isRoundRobin) ShowRoundRobinView(competition);
            else ShowTournamentBracket(competition);
        };

        grid.Add(backBtn, 0, 0);
        grid.Add(titleStack, 1, 0);
        grid.Add(saveBtn, 2, 0);

        return grid;
    }

    // ════════════════════════════════════════════════════════════════════
    //  SHARED: MATCH CARD  — tap +/− to adjust scores (updates in-place)
    // ════════════════════════════════════════════════════════════════════

    private View CreateMatchCard(CompetitionMatch match, CompetitionFormat format, Competition competition)
    {
        var p1Name = GetParticipantName(match.Participant1Id, format) ?? "TBD";
        var p2Name = GetParticipantName(match.Participant2Id, format) ?? "TBD";

        bool hasP1 = match.Participant1Id.HasValue;
        bool hasP2 = match.Participant2Id.HasValue;
        bool canScore = hasP1 && hasP2;

        bool p1Won = match.IsComplete && match.WinnerId == match.Participant1Id;
        bool p2Won = match.IsComplete && match.WinnerId == match.Participant2Id;

        // Check if this match is in the first round (allows manual assignment)
        bool isFirstRound = competition.Rounds.Count > 0 &&
                            competition.Rounds[0].Matches.Any(m => m.Id == match.Id);

        // Score labels that get updated in-place (no view rebuild)
        Label? p1ScoreLbl = null;
        Label? p2ScoreLbl = null;

        // Max total frames is BestOf (e.g. best of 15 = 15 frames max).
        // Combined score can't exceed this. 0 = no limit.
        int maxFrames = competition.BestOf;

        var card = new VerticalStackLayout { Spacing = 0 };

        // Venue/table label — tap to assign manually. "Edit Teams" sits on the right.
        bool hasVenue = !string.IsNullOrEmpty(match.VenueDisplay);
        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            BackgroundColor = _headerBg,
            Padding = new Thickness(10, 4, 6, 2),
            ColumnSpacing = 6
        };
        var venueLabel = new Label
        {
            Text = hasVenue
                ? $"\U0001F4CD {match.VenueDisplay}"
                : "\U0001F4CD Tap to assign venue / table",
            FontSize = 10,
            TextColor = hasVenue ? _subtleText : _accentBlue,
            FontAttributes = hasVenue ? FontAttributes.None : FontAttributes.Italic,
            VerticalTextAlignment = TextAlignment.Center
        };
        var venueTap = new TapGestureRecognizer();
        venueTap.Tapped += async (_, _) => await AssignMatchVenueAsync(match, competition);
        venueLabel.GestureRecognizers.Add(venueTap);
        headerGrid.Add(venueLabel, 0, 0);

        var editTeamsLabel = new Label
        {
            Text = "✏️ Teams",
            FontSize = 10,
            TextColor = _accentBlue,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        };
        var editTap = new TapGestureRecognizer();
        editTap.Tapped += async (_, _) => await EditMatchTeamsAsync(match, competition);
        editTeamsLabel.GestureRecognizers.Add(editTap);
        headerGrid.Add(editTeamsLabel, 1, 0);

        card.Children.Add(headerGrid);
        card.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = _borderDefault });

        // Player 1 row
        var p1Row = CreatePlayerRow(
            p1Name, match.Participant1Score, p1Won, match.IsComplete, canScore,
            onPlus:  () => { if (maxFrames <= 0 || match.Participant1Score + match.Participant2Score < maxFrames) { match.Participant1Score++; p1ScoreLbl!.Text = match.Participant1Score.ToString(); } },
            onMinus: () => { if (match.Participant1Score > 0) { match.Participant1Score--; p1ScoreLbl!.Text = match.Participant1Score.ToString(); } },
            isTop: true,
            scoreLabelOut: out p1ScoreLbl);

        // Make TBD slots tappable in first-round matches
        if (isFirstRound && !hasP1)
        {
            var tap1 = new TapGestureRecognizer();
            tap1.Tapped += async (_, _) => await OnTbdSlotTapped(match, isSlot1: true, competition);
            p1Row.GestureRecognizers.Add(tap1);
        }

        card.Children.Add(p1Row);

        // Divider
        card.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = _borderDefault });

        // Player 2 row
        var p2Row = CreatePlayerRow(
            p2Name, match.Participant2Score, p2Won, match.IsComplete, canScore,
            onPlus:  () => { if (maxFrames <= 0 || match.Participant1Score + match.Participant2Score < maxFrames) { match.Participant2Score++; p2ScoreLbl!.Text = match.Participant2Score.ToString(); } },
            onMinus: () => { if (match.Participant2Score > 0) { match.Participant2Score--; p2ScoreLbl!.Text = match.Participant2Score.ToString(); } },
            isTop: false,
            scoreLabelOut: out p2ScoreLbl);

        // Make TBD slots tappable in first-round matches
        if (isFirstRound && !hasP2)
        {
            var tap2 = new TapGestureRecognizer();
            tap2.Tapped += async (_, _) => await OnTbdSlotTapped(match, isSlot1: false, competition);
            p2Row.GestureRecognizers.Add(tap2);
        }

        card.Children.Add(p2Row);

        var borderColor = match.IsComplete ? _borderComplete : _borderDefault;
        var border = new Border
        {
            Stroke = borderColor,
            StrokeThickness = match.IsComplete ? 2 : 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            BackgroundColor = Colors.White,
            Content = card,
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Colors.Black),
                Offset = new Point(0, 1),
                Radius = 4,
                Opacity = 0.08f
            },
            Margin = new Thickness(4, 3)
        };

        return border;
    }

    private View CreatePlayerRow(string name, int score, bool isWinner, bool isComplete,
        bool canScore, Action onPlus, Action onMinus, bool isTop, out Label? scoreLabelOut)
    {
        scoreLabelOut = null;
        var rowBg = isWinner ? _winnerGreenBg : Colors.Transparent;

        var grid = new Grid
        {
            BackgroundColor = rowBg,
            Padding = new Thickness(10, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },   // name
                new ColumnDefinition { Width = GridLength.Auto },   // score area
            },
            ColumnSpacing = 8,
            MinimumHeightRequest = 36
        };

        // ── Name ────────────────────────────────────────────────────────
        bool isTbd = name == "TBD";
        var nameLabel = new Label
        {
            Text = isWinner ? "\u2714 " + name : (isTbd ? "TBD \u2014 tap to assign" : name),
            FontSize = 13,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            FontAttributes = isWinner ? FontAttributes.Bold : (isTbd ? FontAttributes.Italic : FontAttributes.None),
            TextColor = isWinner ? _winnerGreenText : (isTbd ? _accentBlue : Colors.Black)
        };
        grid.Add(nameLabel, 0, 0);

        // ── Score controls ──────────────────────────────────────────────
        if (canScore)
        {
            // [ − ]  score  [ + ]
            var scoreControls = new HorizontalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };

            var minusBtn = new Button
            {
                Text = "\u2212",
                FontSize = 14,
                WidthRequest = 30,
                HeightRequest = 30,
                Padding = 0,
                CornerRadius = 6,
                BackgroundColor = _scoreBtnBg,
                TextColor = Colors.Black
            };
            minusBtn.Clicked += (_, _) => onMinus();

            var scoreLbl = new Label
            {
                Text = score.ToString(),
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                WidthRequest = 28,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };
            scoreLabelOut = scoreLbl;

            var plusBtn = new Button
            {
                Text = "+",
                FontSize = 14,
                WidthRequest = 30,
                HeightRequest = 30,
                Padding = 0,
                CornerRadius = 6,
                BackgroundColor = _scoreBtnActive,
                TextColor = _accentBlue
            };
            plusBtn.Clicked += (_, _) => onPlus();

            scoreControls.Children.Add(minusBtn);
            scoreControls.Children.Add(scoreLbl);
            scoreControls.Children.Add(plusBtn);

            grid.Add(scoreControls, 1, 0);
        }
        else
        {
            // Read-only score pill
            var scorePill = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 6 },
                Stroke = Colors.Transparent,
                BackgroundColor = isWinner ? _accentGreen : (isComplete ? _loserGrayBg : _scoreBtnBg),
                Padding = new Thickness(10, 4),
                Content = new Label
                {
                    Text = score.ToString(),
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = isWinner ? Colors.White : Colors.Black
                },
                VerticalOptions = LayoutOptions.Center,
                MinimumWidthRequest = 32
            };
            grid.Add(scorePill, 1, 0);
        }

        return grid;
    }

    // ════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ════════════════════════════════════════════════════════════════════

    private string? GetParticipantName(Guid? participantId, CompetitionFormat format)
    {
        return _editorViewModel?.GetParticipantName(participantId);
    }

    /// <summary>
    /// Shows a picker to let the user assign an unassigned participant to a TBD match slot.
    /// After assignment the bracket view refreshes so the name appears immediately.
    /// </summary>
    private async Task OnTbdSlotTapped(CompetitionMatch match, bool isSlot1, Competition competition)
    {
        if (_editorViewModel == null) return;

        var unassigned = _editorViewModel.GetUnassignedParticipants();
        if (unassigned.Count == 0)
        {
            await DisplayAlert("No Players Available", "All participants have been assigned to matches.", "OK");
            return;
        }

        var names = unassigned.Select(p => p.Name).ToArray();
        var chosen = await DisplayActionSheet("Select Participant", "Cancel", null, names);

        if (string.IsNullOrEmpty(chosen) || chosen == "Cancel") return;

        var selected = unassigned.FirstOrDefault(p => p.Name == chosen);
        if (selected == null) return;

        await _editorViewModel.AssignParticipantToMatchAsync(match.Id, isSlot1, selected.Id);
        SetStatus(_editorViewModel.StatusMessage);

        // Refresh the bracket view to show the assignment
        ShowTournamentBracket(competition);
    }

    private async void ApplyAllScores(Competition competition)
    {
        if (_editorViewModel == null) return;
        await _editorViewModel.ApplyBracketScoresCommand.ExecuteAsync(null);
        SetStatus(_editorViewModel.StatusMessage);
    }
}
