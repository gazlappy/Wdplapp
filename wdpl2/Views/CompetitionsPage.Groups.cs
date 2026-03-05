using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

/// <summary>
/// Group stage competition methods — players do their own draw within groups
/// and report who got through. The UI shows a simple selection checklist.
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

        var settings = _selectedCompetition.GroupSettings ?? new GroupStageSettings();
        int topAdvance = settings.TopPlayersAdvance;

        var mainLayout = new VerticalStackLayout { Spacing = 10 };

        // Header
        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
            },
            ColumnSpacing = 8,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var backBtn = new Button
        {
            Text = "← Back",
            Command = new Command(() => ShowCompetitionEditor(_selectedCompetition)),
            Padding = new Thickness(8, 4),
            FontSize = 13
        };

        var titleLabel = new Label
        {
            Text = $"{_selectedCompetition.Name} — Select Winners",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };

        headerGrid.Add(backBtn, 0, 0);
        headerGrid.Add(titleLabel, 1, 0);
        mainLayout.Children.Add(headerGrid);

        mainLayout.Children.Add(new Label
        {
            Text = $"Tap the top {topAdvance} player(s) in each group who got through. Selected players advance to the knockout stage.",
            FontSize = 12,
            TextColor = Colors.Gray,
            Margin = new Thickness(0, 0, 0, 4)
        });

        // Build each group as a selection list
        var groupsLayout = new VerticalStackLayout { Spacing = 12 };

        foreach (var group in _selectedCompetition.Groups)
        {
            groupsLayout.Children.Add(CreateGroupSelectionView(group, _selectedCompetition.Format, topAdvance));
        }

        var scrollView = new ScrollView { Content = groupsLayout };
        mainLayout.Children.Add(scrollView);

        ContentPanel.Content = mainLayout;
    }

    private View CreateGroupSelectionView(CompetitionGroup group, CompetitionFormat format, int topAdvance)
    {
        // Track which participants are selected as winners
        var selectedIds = new HashSet<Guid>(
            group.Standings
                .Where(s => s.Position > 0 && s.Position <= topAdvance)
                .Select(s => s.ParticipantId));

        var headerBorder = new Border
        {
            Padding = 10,
            BackgroundColor = Color.FromArgb("#3B82F6"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
        };

        int selected = selectedIds.Count;
        var headerLabel = new Label
        {
            Text = $"{group.Name} ({group.ParticipantIds.Count} players) — {selected}/{topAdvance} selected",
            TextColor = Colors.White,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold
        };
        headerBorder.Content = headerLabel;

        var playersLayout = new VerticalStackLayout { Spacing = 0 };

        foreach (var participantId in group.ParticipantIds)
        {
            var name = GetParticipantName(participantId, format) ?? "Unknown";
            bool isSelected = selectedIds.Contains(participantId);

            var rowBorder = new Border
            {
                Padding = new Thickness(12, 8),
                BackgroundColor = isSelected ? Color.FromArgb("#DBEAFE") : Colors.White,
                Margin = new Thickness(0, 1),
            };

            var rowGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                ColumnSpacing = 10
            };

            var checkLabel = new Label
            {
                Text = isSelected ? "✅" : "⬜",
                FontSize = 18,
                VerticalTextAlignment = TextAlignment.Center
            };

            var nameLabel = new Label
            {
                Text = name,
                FontSize = 14,
                VerticalTextAlignment = TextAlignment.Center,
                FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isSelected ? Color.FromArgb("#1D4ED8") : Colors.Black
            };

            var statusLabel = new Label
            {
                Text = isSelected ? "Through" : "",
                FontSize = 11,
                TextColor = Color.FromArgb("#059669"),
                VerticalTextAlignment = TextAlignment.Center,
                FontAttributes = FontAttributes.Italic
            };

            rowGrid.Add(checkLabel, 0, 0);
            rowGrid.Add(nameLabel, 1, 0);
            rowGrid.Add(statusLabel, 2, 0);
            rowBorder.Content = rowGrid;

            // Capture for closure
            var pid = participantId;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                if (selectedIds.Contains(pid))
                {
                    // Deselect
                    selectedIds.Remove(pid);
                }
                else
                {
                    if (selectedIds.Count >= topAdvance)
                    {
                        await DisplayAlert("Limit Reached",
                            $"You can only select {topAdvance} player(s) per group. Deselect someone first.",
                            "OK");
                        return;
                    }
                    selectedIds.Add(pid);
                }

                // Save selection to standings
                await SaveGroupSelections(group, selectedIds, topAdvance);

                // Refresh the view
                ShowGroupsView();
            };
            rowBorder.GestureRecognizers.Add(tap);

            playersLayout.Children.Add(rowBorder);
        }

        return new Border
        {
            Padding = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Stroke = Color.FromArgb("#E5E7EB"),
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                Children = { headerBorder, playersLayout }
            }
        };
    }

    /// <summary>
    /// Save the manually selected winners to the group's Standings list.
    /// Uses Position to indicate selection: 1..topAdvance = selected, 0 = not selected.
    /// </summary>
    private async Task SaveGroupSelections(CompetitionGroup group, HashSet<Guid> selectedIds, int topAdvance)
    {
        if (_editorViewModel == null || _selectedCompetition == null) return;

        // Build standings from selections
        var standings = new System.Collections.Generic.List<GroupStanding>();
        int pos = 1;

        // Selected players get positions 1..N
        foreach (var id in group.ParticipantIds.Where(p => selectedIds.Contains(p)))
        {
            standings.Add(new GroupStanding { ParticipantId = id, Position = pos++ });
        }

        // Unselected players get position 0 (not advancing)
        foreach (var id in group.ParticipantIds.Where(p => !selectedIds.Contains(p)))
        {
            standings.Add(new GroupStanding { ParticipantId = id, Position = 0 });
        }

        group.Standings = standings;

        await _editorViewModel.SaveCompetitionAsync();
    }

    private async void OnFinalizeGroups()
    {
        if (_editorViewModel == null || _selectedCompetition?.GroupSettings == null) return;

        int topAdvance = _selectedCompetition.GroupSettings.TopPlayersAdvance;

        // Check all groups have enough selections
        var incomplete = _selectedCompetition.Groups
            .Where(g => g.Standings.Count(s => s.Position > 0 && s.Position <= topAdvance) < topAdvance)
            .ToList();

        if (incomplete.Count > 0)
        {
            var names = string.Join(", ", incomplete.Select(g => g.Name));
            await DisplayAlert("Incomplete Groups",
                $"The following groups don't have {topAdvance} winner(s) selected yet:\n{names}\n\nGo to View Groups and select who got through.",
                "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Finalize Groups",
            $"This will create the knockout bracket from the {_selectedCompetition.Groups.Count * topAdvance} selected winners. Continue?",
            "Yes, Create Knockout",
            "Cancel");

        if (!confirm) return;

        await _editorViewModel.FinalizeGroupsCommand.ExecuteAsync(null);
        await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);
        SetStatus(_editorViewModel.StatusMessage);

        if (_selectedCompetition != null)
            ShowCompetitionEditor(_selectedCompetition);
    }
}
