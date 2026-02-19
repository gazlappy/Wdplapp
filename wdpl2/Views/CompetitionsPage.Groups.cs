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
    private async void OnGenerateGroups()
    {
        if (_editorViewModel == null) return;

        await _editorViewModel.GenerateGroupsCommand.ExecuteAsync(null);
        await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);
        SetStatus(_editorViewModel.StatusMessage);

        if (_selectedCompetition != null)
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
        int topAdvance = _selectedCompetition?.GroupSettings?.TopPlayersAdvance ?? 2;
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
                BackgroundColor = standing.Position <= topAdvance ? Color.FromArgb("#DBEAFE") : Colors.White,
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

    private async void ApplyAllGroupScores()
    {
        if (_editorViewModel == null) return;
        await _editorViewModel.ApplyGroupScoresCommand.ExecuteAsync(null);
        SetStatus(_editorViewModel.StatusMessage);
    }

    private async void OnFinalizeGroups()
    {
        if (_editorViewModel == null || _selectedCompetition?.GroupSettings == null) return;

        var confirm = await DisplayAlert(
            "Finalize Groups",
            "This will create knockout brackets from group standings. Make sure all group matches are complete. Continue?",
            "Yes, Create Knockouts",
            "Cancel");

        if (!confirm) return;

        await _editorViewModel.FinalizeGroupsCommand.ExecuteAsync(null);
        await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);
        SetStatus(_editorViewModel.StatusMessage);

        if (_selectedCompetition != null)
            ShowCompetitionEditor(_selectedCompetition);
    }
}
