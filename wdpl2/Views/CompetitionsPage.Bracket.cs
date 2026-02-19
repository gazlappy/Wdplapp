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
        return _editorViewModel?.GetParticipantName(participantId);
    }

    private async void ApplyAllScores(Competition competition)
    {
        if (_editorViewModel == null) return;
        await _editorViewModel.ApplyBracketScoresCommand.ExecuteAsync(null);
        SetStatus(_editorViewModel.StatusMessage);
    }
}
