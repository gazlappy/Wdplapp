using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

/// <summary>
/// Group stage competition methods
/// </summary>
public partial class CompetitionsPage
{
    private void OnGenerateGroups()
    {
        if (_selectedCompetition == null || _selectedCompetition.GroupSettings == null) return;

        var participants = _selectedCompetition.Format == CompetitionFormat.DoublesGroupStage
            ? _selectedCompetition.DoublesTeams.Select(t => t.Id).ToList()
            : _selectedCompetition.ParticipantIds;

        if (participants.Count < _selectedCompetition.GroupSettings.NumberOfGroups * 2)
        {
            SetStatus($"Need at least {_selectedCompetition.GroupSettings.NumberOfGroups * 2} participants for {_selectedCompetition.GroupSettings.NumberOfGroups} groups");
            return;
        }

        // Generate groups with randomization
        var (groups, plateCompetition) = CompetitionGenerator.GenerateGroupStage(
            participants,
            _selectedCompetition.GroupSettings,
            _selectedCompetition.Format,
            _selectedCompetition.SeasonId,
            _selectedCompetition.Name,
            randomize: true
        );

        _selectedCompetition.Groups = groups;

        // Create plate competition if requested
        if (plateCompetition != null)
        {
            DataStore.Data.Competitions.Add(plateCompetition);
            _selectedCompetition.PlateCompetitionId = plateCompetition.Id;
        }

        _selectedCompetition.Status = CompetitionStatus.InProgress;
        DataStore.Save();

        SetStatus($"Generated {groups.Count} groups with {groups.Sum(g => g.Matches.Count)} total matches");
        
        // Refresh the editor view
        ShowCompetitionEditor(_selectedCompetition);
    }

    private void ShowGroupsView()
    {
        if (_selectedCompetition == null || _selectedCompetition.Groups.Count == 0) return;

        var mainLayout = new VerticalStackLayout
        {
            Spacing = 10
        };

        // Header with back button
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
            Command = new Command(() => ShowCompetitionEditor(_selectedCompetition)),
            Padding = new Thickness(8, 4),
            FontSize = 13
        };

        var titleLabel = new Label
        {
            Text = $"{_selectedCompetition.Name} - Groups",
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

        // Create tabs/accordion for each group
        var groupsLayout = new VerticalStackLayout { Spacing = 10 };

        foreach (var group in _selectedCompetition.Groups)
        {
            var groupView = CreateGroupView(group, _selectedCompetition.Format);
            groupsLayout.Children.Add(groupView);
        }

        var scrollView = new ScrollView
        {
            Content = groupsLayout
        };

        mainLayout.Children.Add(scrollView);

        // Apply All Scores button handler
        applyAllBtn.Clicked += (s, e) =>
        {
            ApplyAllGroupScores();
            ShowGroupsView(); // Refresh view
        };

        ContentPanel.Content = mainLayout;
    }

    private View CreateGroupView(CompetitionGroup group, CompetitionFormat format)
    {
        var expandedState = new { IsExpanded = true }; // Simple toggle state

        var headerFrame = new Frame
        {
            Padding = 10,
            BackgroundColor = Color.FromArgb("#3B82F6"),
            CornerRadius = 6,
            Margin = new Thickness(0, 5, 0, 0)
        };

        var headerLabel = new Label
        {
            Text = $"{group.Name} ({group.ParticipantIds.Count} participants)",
            TextColor = Colors.White,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold
        };

        headerFrame.Content = headerLabel;

        var matchesLayout = new VerticalStackLayout { Spacing = 5 };

        // Display matches
        foreach (var match in group.Matches)
        {
            matchesLayout.Children.Add(CreateGroupMatchCard(match, format));
        }

        // Display standings
        var standings = CompetitionGenerator.CalculateGroupStandings(group);
        var standingsView = CreateStandingsView(standings, format);

        var contentLayout = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Matches", FontSize = 14, FontAttributes = FontAttributes.Bold },
                matchesLayout,
                new Label { Text = "Standings", FontSize = 14, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 10, 0, 0) },
                standingsView
            }
        };

        return new Frame
        {
            Padding = 10,
            Content = new VerticalStackLayout
            {
                Spacing = 5,
                Children = { headerFrame, contentLayout }
            }
        };
    }

    private View CreateGroupMatchCard(CompetitionMatch match, CompetitionFormat format)
    {
        var p1Name = GetParticipantName(match.Participant1Id, format) ?? "TBD";
        var p2Name = GetParticipantName(match.Participant2Id, format) ?? "TBD";

        var matchGrid = new Grid
        {
            Padding = 8,
            BackgroundColor = match.IsComplete ? Color.FromArgb("#F0FDF4") : Colors.White,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(50) },
                new ColumnDefinition { Width = new GridLength(20) },
                new ColumnDefinition { Width = new GridLength(50) },
                new ColumnDefinition { Width = GridLength.Star }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        var p1Label = new Label
        {
            Text = p1Name,
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.End,  // Changed from Right to End
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = match.WinnerId == match.Participant1Id ? FontAttributes.Bold : FontAttributes.None
        };

        var p1Entry = new Entry
        {
            Text = match.Participant1Score.ToString(),
            Keyboard = Keyboard.Numeric,
            HorizontalTextAlignment = TextAlignment.Center,
            FontSize = 13,
            BackgroundColor = Color.FromArgb("#F3F4F6"),
            WidthRequest = 50
        };

        p1Entry.TextChanged += (s, e) =>
        {
            if (int.TryParse(e.NewTextValue, out int score))
                match.Participant1Score = score;
        };

        var vsLabel = new Label
        {
            Text = "v",
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            FontSize = 12,
            TextColor = Colors.Gray
        };

        var p2Entry = new Entry
        {
            Text = match.Participant2Score.ToString(),
            Keyboard = Keyboard.Numeric,
            HorizontalTextAlignment = TextAlignment.Center,
            FontSize = 13,
            BackgroundColor = Color.FromArgb("#F3F4F6"),
            WidthRequest = 50
        };

        p2Entry.TextChanged += (s, e) =>
        {
            if (int.TryParse(e.NewTextValue, out int score))
                match.Participant2Score = score;
        };

        var p2Label = new Label
        {
            Text = p2Name,
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.Start,  // Changed from Left to Start
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = match.WinnerId == match.Participant2Id ? FontAttributes.Bold : FontAttributes.None
        };

        matchGrid.Add(p1Label, 0, 0);
        matchGrid.Add(p1Entry, 1, 0);
        matchGrid.Add(vsLabel, 2, 0);
        matchGrid.Add(p2Entry, 3, 0);
        matchGrid.Add(p2Label, 4, 0);

        return new Frame
        {
            Padding = 2,
            Margin = new Thickness(0, 2),
            Content = matchGrid
        };
    }

    private View CreateStandingsView(System.Collections.Generic.List<GroupStanding> standings, CompetitionFormat format)
    {
        var standingsLayout = new VerticalStackLayout { Spacing = 2 };

        // Header
        var headerGrid = new Grid
        {
            Padding = 8,
            BackgroundColor = Color.FromArgb("#F3F4F6"),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(30) },  // Pos
                new ColumnDefinition { Width = GridLength.Star },     // Name
                new ColumnDefinition { Width = new GridLength(40) },  // P
                new ColumnDefinition { Width = new GridLength(40) },  // W
                new ColumnDefinition { Width = new GridLength(40) },  // D
                new ColumnDefinition { Width = new GridLength(40) },  // L
                new ColumnDefinition { Width = new GridLength(50) },  // Pts
            }
        };

        headerGrid.Add(new Label { Text = "Pos", FontSize = 11, FontAttributes = FontAttributes.Bold }, 0, 0);
        headerGrid.Add(new Label { Text = "Player", FontSize = 11, FontAttributes = FontAttributes.Bold }, 1, 0);
        headerGrid.Add(new Label { Text = "P", FontSize = 11, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center }, 2, 0);
        headerGrid.Add(new Label { Text = "W", FontSize = 11, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center }, 3, 0);
        headerGrid.Add(new Label { Text = "D", FontSize = 11, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center }, 4, 0);
        headerGrid.Add(new Label { Text = "L", FontSize = 11, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center }, 5, 0);
        headerGrid.Add(new Label { Text = "Pts", FontSize = 11, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center }, 6, 0);

        standingsLayout.Children.Add(headerGrid);

        // Standings rows
        foreach (var standing in standings)
        {
            var name = GetParticipantName(standing.ParticipantId, format) ?? "Unknown";

            var rowGrid = new Grid
            {
                Padding = 8,
                BackgroundColor = standing.Position <= 2 ? Color.FromArgb("#DBEAFE") : Colors.White,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(30) },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = new GridLength(40) },
                    new ColumnDefinition { Width = new GridLength(40) },
                    new ColumnDefinition { Width = new GridLength(40) },
                    new ColumnDefinition { Width = new GridLength(40) },
                    new ColumnDefinition { Width = new GridLength(50) },
                }
            };

            rowGrid.Add(new Label { Text = standing.Position.ToString(), FontSize = 12 }, 0, 0);
            rowGrid.Add(new Label { Text = name, FontSize = 12 }, 1, 0);
            rowGrid.Add(new Label { Text = standing.Played.ToString(), FontSize = 12, HorizontalTextAlignment = TextAlignment.Center }, 2, 0);
            rowGrid.Add(new Label { Text = standing.Won.ToString(), FontSize = 12, HorizontalTextAlignment = TextAlignment.Center }, 3, 0);
            rowGrid.Add(new Label { Text = standing.Drawn.ToString(), FontSize = 12, HorizontalTextAlignment = TextAlignment.Center }, 4, 0);
            rowGrid.Add(new Label { Text = standing.Lost.ToString(), FontSize = 12, HorizontalTextAlignment = TextAlignment.Center }, 5, 0);
            rowGrid.Add(new Label { Text = standing.Points.ToString(), FontSize = 12, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center }, 6, 0);

            standingsLayout.Children.Add(rowGrid);
        }

        return new Frame
        {
            Padding = 0,
            Content = standingsLayout
        };
    }

    private void ApplyAllGroupScores()
    {
        if (_selectedCompetition == null) return;

        bool anyUpdates = false;
        
        foreach (var group in _selectedCompetition.Groups)
        {
            foreach (var match in group.Matches)
            {
                if (!match.IsComplete && match.Participant1Id.HasValue && match.Participant2Id.HasValue)
                {
                    // Determine winner based on scores
                    if (match.Participant1Score > match.Participant2Score)
                        match.WinnerId = match.Participant1Id;
                    else if (match.Participant2Score > match.Participant1Score)
                        match.WinnerId = match.Participant2Id;
                    else if (match.Participant1Score > 0 || match.Participant2Score > 0)
                    {
                        // It's a draw
                        match.WinnerId = null;
                    }
                    else
                    {
                        continue; // No scores entered
                    }

                    match.IsComplete = true;
                    anyUpdates = true;
                }
            }
        }

        if (anyUpdates)
        {
            DataStore.Save();
            SetStatus("All group scores applied");
        }
        else
        {
            SetStatus("No new scores to apply");
        }
    }

    private async void OnFinalizeGroups()
    {
        if (_selectedCompetition == null || _selectedCompetition.GroupSettings == null) return;

        var confirm = await DisplayAlert(
            "Finalize Groups",
            "This will create knockout brackets from group standings. Make sure all group matches are complete. Continue?",
            "Yes, Create Knockouts",
            "Cancel");

        if (!confirm) return;

        // Advance participants from groups
        var (knockoutParticipants, plateParticipants) = CompetitionGenerator.AdvanceFromGroups(
            _selectedCompetition.Groups,
            _selectedCompetition.GroupSettings.TopPlayersAdvance,
            _selectedCompetition.GroupSettings.LowerPlayersToPlate
        );

        // Create main knockout bracket
        if (knockoutParticipants.Count >= 2)
        {
            _selectedCompetition.Rounds = CompetitionGenerator.GenerateSingleKnockout(
                knockoutParticipants,
                randomize: false // Seeded by group position
            );
            SetStatus($"Created knockout bracket with {knockoutParticipants.Count} players");
        }

        // Create plate competition
        if (plateParticipants.Count >= 2 && _selectedCompetition.PlateCompetitionId.HasValue)
        {
            var plateComp = DataStore.Data.Competitions
                .FirstOrDefault(c => c.Id == _selectedCompetition.PlateCompetitionId.Value);

            if (plateComp != null)
            {
                // Assign participants to plate
                if (_selectedCompetition.Format == CompetitionFormat.DoublesGroupStage)
                {
                    plateComp.DoublesTeams = _selectedCompetition.DoublesTeams
                        .Where(t => plateParticipants.Contains(t.Id))
                        .ToList();
                }
                else
                {
                    plateComp.ParticipantIds = plateParticipants;
                }

                // Generate plate knockout
                plateComp.Rounds = CompetitionGenerator.GenerateSingleKnockout(
                    plateParticipants,
                    randomize: false
                );
                plateComp.Status = CompetitionStatus.InProgress;

                SetStatus($"Created plate knockout with {plateParticipants.Count} players");
            }
        }

        _selectedCompetition.Status = CompetitionStatus.InProgress;
        DataStore.Save();
        await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);

        await DisplayAlert("Success",
            $"Knockout brackets created!\nMain: {knockoutParticipants.Count} players\nPlate: {plateParticipants.Count} players",
            "OK");

        // Refresh view
        ShowCompetitionEditor(_selectedCompetition);
    }
}
