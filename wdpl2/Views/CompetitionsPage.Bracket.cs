using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

/// <summary>
/// Tournament bracket generation and display
/// </summary>
public partial class CompetitionsPage
{
    private void OnGenerateBracket()
    {
        if (_selectedCompetition == null) return;

        int participantCount = _selectedCompetition.Format == CompetitionFormat.DoublesKnockout
            ? _selectedCompetition.DoublesTeams.Count
            : _selectedCompetition.ParticipantIds.Count;

        if (participantCount < 2)
        {
            SetStatus("Need at least 2 participants to generate bracket");
            return;
        }

        List<Guid> participants;
        
        if (_selectedCompetition.Format == CompetitionFormat.DoublesKnockout || _selectedCompetition.Format == CompetitionFormat.DoublesGroupStage)
        {
            participants = _selectedCompetition.DoublesTeams.Select(t => t.Id).ToList();
        }
        else
        {
            participants = _selectedCompetition.ParticipantIds;
        }

        // Generate bracket WITHOUT randomization (maintains input order)
        List<CompetitionRound> rounds = _selectedCompetition.Format switch
        {
            CompetitionFormat.SinglesKnockout => CompetitionGenerator.GenerateSingleKnockout(participants, randomize: false),
            CompetitionFormat.DoublesKnockout => CompetitionGenerator.GenerateSingleKnockout(participants, randomize: false),
            CompetitionFormat.TeamKnockout => CompetitionGenerator.GenerateSingleKnockout(participants, randomize: false),
            CompetitionFormat.RoundRobin => CompetitionGenerator.GenerateRoundRobin(participants, randomize: false),
            _ => new List<CompetitionRound>()
        };

        _selectedCompetition.Rounds = rounds;
        _selectedCompetition.Status = CompetitionStatus.InProgress;
        DataStore.Save();

        SetStatus($"Generated {rounds.Count} rounds with {rounds.Sum(r => r.Matches.Count)} matches (ordered draw)");
    }

    private void OnRandomDraw()
    {
        if (_selectedCompetition == null) return;

        int participantCount = _selectedCompetition.Format == CompetitionFormat.DoublesKnockout
            ? _selectedCompetition.DoublesTeams.Count
            : _selectedCompetition.ParticipantIds.Count;

        if (participantCount < 2)
        {
            SetStatus("Need at least 2 participants to generate bracket");
            return;
        }

        List<Guid> participants;
        
        if (_selectedCompetition.Format == CompetitionFormat.DoublesKnockout || _selectedCompetition.Format == CompetitionFormat.DoublesGroupStage)
        {
            participants = _selectedCompetition.DoublesTeams.Select(t => t.Id).ToList();
        }
        else
        {
            participants = _selectedCompetition.ParticipantIds;
        }

        // Generate bracket WITH randomization (completely random draw)
        List<CompetitionRound> rounds = _selectedCompetition.Format switch
        {
            CompetitionFormat.SinglesKnockout => CompetitionGenerator.GenerateSingleKnockout(participants, randomize: true),
            CompetitionFormat.DoublesKnockout => CompetitionGenerator.GenerateSingleKnockout(participants, randomize: true),
            CompetitionFormat.TeamKnockout => CompetitionGenerator.GenerateSingleKnockout(participants, randomize: true),
            CompetitionFormat.RoundRobin => CompetitionGenerator.GenerateRoundRobin(participants, randomize: true),
            _ => new List<CompetitionRound>()
        };

        _selectedCompetition.Rounds = rounds;
        _selectedCompetition.Status = CompetitionStatus.InProgress;
        DataStore.Save();

        SetStatus($"Generated {rounds.Count} rounds with {rounds.Sum(r => r.Matches.Count)} matches (RANDOM DRAW)");
    }

    private void OnViewBracket()
    {
        if (_selectedCompetition == null) return;

        if (_selectedCompetition.Rounds.Count == 0)
        {
            SetStatus("No bracket generated yet. Click 'Generate Bracket' first.");
            return;
        }

        ShowTournamentBracket(_selectedCompetition);
    }

    private void ShowTournamentBracket(Competition competition)
    {
        var mainLayout = new VerticalStackLayout
        {
            Spacing = 10
        };

        // Header with back button and apply all scores button
        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var backBtn = new Button
        {
            Text = "? Back",
            Command = new Command(() => ShowCompetitionEditor(competition)),
            Padding = new Thickness(8, 4),
            FontSize = 13
        };

        var titleLabel = new Label
        {
            Text = $"{competition.Name}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var applyAllBtn = new Button
        {
            Text = "Apply All Scores",
            BackgroundColor = Color.FromArgb("#10B981"),
            TextColor = Colors.White,
            Padding = new Thickness(12, 6),
            FontSize = 13
        };

        headerGrid.Add(backBtn, 0, 0);
        headerGrid.Add(titleLabel, 1, 0);
        headerGrid.Add(applyAllBtn, 2, 0);

        mainLayout.Children.Add(headerGrid);

        // Create horizontal scrollable tournament bracket
        var bracketGrid = CreateTournamentBracketGrid(competition);
        
        var scrollView = new ScrollView
        {
            Orientation = ScrollOrientation.Both,
            Content = bracketGrid
        };

        mainLayout.Children.Add(scrollView);

        // Apply All Scores button handler
        applyAllBtn.Clicked += (s, e) =>
        {
            ApplyAllScores(competition);
            ShowTournamentBracket(competition); // Refresh view
        };

        ContentPanel.Content = mainLayout;
    }

    private Grid CreateTournamentBracketGrid(Competition competition)
    {
        var grid = new Grid
        {
            RowSpacing = 10,
            ColumnSpacing = 20,
            Padding = new Thickness(10)
        };

        // Calculate bracket dimensions
        int rounds = competition.Rounds.Count;
        
        if (rounds == 0) return grid;

        // Define row height for each match slot
        double matchHeight = 80;

        // Create columns for each round
        for (int i = 0; i < rounds; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        }

        // Build the bracket from left to right
        for (int roundIndex = 0; roundIndex < rounds; roundIndex++)
        {
            var round = competition.Rounds[roundIndex];
            int matchCount = round.Matches.Count;

            // Calculate vertical spacing for this round (matches spread out as rounds progress)
            double roundSpacing = matchHeight * Math.Pow(2, roundIndex);

            for (int matchIndex = 0; matchIndex < matchCount; matchIndex++)
            {
                var match = round.Matches[matchIndex];

                // Calculate vertical position
                double yPosition = (matchIndex * 2 + 1) * roundSpacing / 2;

                // Create match card
                var matchCard = CreateMatchCard(match, competition.Format);

                // Add to grid at calculated position
                grid.Children.Add(matchCard);
                Grid.SetColumn(matchCard, roundIndex);
                Grid.SetRow(matchCard, 0);
                matchCard.Margin = new Thickness(0, yPosition, 0, 0);
            }
        }

        return grid;
    }

    private View CreateMatchCard(CompetitionMatch match, CompetitionFormat format)
    {
        var p1Name = GetParticipantName(match.Participant1Id, format);
        var p2Name = GetParticipantName(match.Participant2Id, format);

        var cardLayout = new VerticalStackLayout
        {
            Spacing = 0
        };

        // Participant 1 row
        var p1Grid = new Grid
        {
            BackgroundColor = match.WinnerId == match.Participant1Id ? Color.FromArgb("#D1FAE5") : Colors.White,
            Padding = new Thickness(8, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(40) }
            }
        };

        var p1Label = new Label
        {
            Text = p1Name ?? "TBD",
            FontSize = 12,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = match.WinnerId == match.Participant1Id ? FontAttributes.Bold : FontAttributes.None
        };

        var p1Score = new Entry
        {
            Text = match.Participant1Score.ToString(),
            Keyboard = Keyboard.Numeric,
            HorizontalTextAlignment = TextAlignment.Center,
            FontSize = 12,
            BackgroundColor = Color.FromArgb("#F3F4F6")
        };

        // Store match reference in command parameter for score updates
        p1Score.TextChanged += (s, e) =>
        {
            if (int.TryParse(e.NewTextValue, out int score))
            {
                match.Participant1Score = score;
            }
        };

        p1Grid.Add(p1Label, 0, 0);
        p1Grid.Add(p1Score, 1, 0);

        // Separator
        var separator = new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = Color.FromArgb("#E5E7EB")
        };

        // Participant 2 row
        var p2Grid = new Grid
        {
            BackgroundColor = match.WinnerId == match.Participant2Id ? Color.FromArgb("#D1FAE5") : Colors.White,
            Padding = new Thickness(8, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(40) }
            }
        };

        var p2Label = new Label
        {
            Text = p2Name ?? "TBD",
            FontSize = 12,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = match.WinnerId == match.Participant2Id ? FontAttributes.Bold : FontAttributes.None
        };

        var p2Score = new Entry
        {
            Text = match.Participant2Score.ToString(),
            Keyboard = Keyboard.Numeric,
            HorizontalTextAlignment = TextAlignment.Center,
            FontSize = 12,
            BackgroundColor = Color.FromArgb("#F3F4F6")
        };

        p2Score.TextChanged += (s, e) =>
        {
            if (int.TryParse(e.NewTextValue, out int score))
            {
                match.Participant2Score = score;
            }
        };

        p2Grid.Add(p2Label, 0, 0);
        p2Grid.Add(p2Score, 1, 0);

        cardLayout.Children.Add(p1Grid);
        cardLayout.Children.Add(separator);
        cardLayout.Children.Add(p2Grid);

        var border = new Border
        {
            Stroke = match.IsComplete ? Color.FromArgb("#10B981") : Color.FromArgb("#D1D5DB"),
            StrokeThickness = match.IsComplete ? 2 : 1,
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Content = cardLayout,
            BackgroundColor = Colors.White
        };

        return border;
    }

    private string? GetParticipantName(Guid? participantId, CompetitionFormat format)
    {
        if (!participantId.HasValue) return null;

        if (format == CompetitionFormat.DoublesKnockout || format == CompetitionFormat.DoublesGroupStage)
        {
            var team = _selectedCompetition?.DoublesTeams.FirstOrDefault(t => t.Id == participantId.Value);
            return team?.TeamName;
        }
        else if (format == CompetitionFormat.TeamKnockout)
        {
            var team = DataStore.Data.Teams.FirstOrDefault(t => t.Id == participantId.Value);
            return team?.Name;
        }
        else
        {
            var player = DataStore.Data.Players.FirstOrDefault(p => p.Id == participantId.Value);
            return player?.FullName;
        }
    }

    private void ApplyAllScores(Competition competition)
    {
        bool anyUpdates = false;
        
        foreach (var round in competition.Rounds)
        {
            foreach (var match in round.Matches)
            {
                if (!match.IsComplete && match.Participant1Id.HasValue && match.Participant2Id.HasValue)
                {
                    // Determine winner based on scores
                    if (match.Participant1Score > match.Participant2Score)
                        match.WinnerId = match.Participant1Id;
                    else if (match.Participant2Score > match.Participant1Score)
                        match.WinnerId = match.Participant2Id;
                    else
                        continue; // Skip draws - can't determine winner

                    match.IsComplete = true;
                    anyUpdates = true;

                    // Advance winner to next round
                    AdvanceWinner(competition, round, match);
                }
            }
        }

        if (anyUpdates)
        {
            DataStore.Save();
            SetStatus("All scores applied - winners advanced to next rounds");
        }
        else
        {
            SetStatus("No new scores to apply");
        }
    }

    private void AdvanceWinner(Competition competition, CompetitionRound round, CompetitionMatch match)
    {
        // Find next round
        var nextRound = competition.Rounds.FirstOrDefault(r => r.RoundNumber == round.RoundNumber + 1);
        if (nextRound == null || !match.WinnerId.HasValue) return;

        // Find which match slot in previous round (0, 1, 2, 3...)
        int matchIndex = round.Matches.IndexOf(match);
        if (matchIndex < 0) return;

        // Determine which match in next round (match 0 and 1 go to next match 0, etc.)
        int nextMatchIndex = matchIndex / 2;
        if (nextMatchIndex >= nextRound.Matches.Count) return;

        var nextMatch = nextRound.Matches[nextMatchIndex];
        
        // Determine if this goes to participant 1 or 2 slot
        if (matchIndex % 2 == 0)
        {
            nextMatch.Participant1Id = match.WinnerId;
        }
        else
        {
            nextMatch.Participant2Id = match.WinnerId;
        }
    }
}
