using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Generate groups and show the animated draw ceremony.
    /// </summary>
    private async void OnGenerateGroupsWithDraw()
    {
        if (_editorViewModel == null) return;

        // Generate the groups first (silently)
        await _editorViewModel.GenerateGroupsCommand.ExecuteAsync(null);
        await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);

        if (_selectedCompetition == null || _selectedCompetition.Groups.Count == 0)
        {
            SetStatus(_editorViewModel.StatusMessage);
            if (_selectedCompetition != null)
                ShowCompetitionEditor(_selectedCompetition);
            return;
        }

        // Show the draw animation
        await ShowDrawAnimation(_selectedCompetition);

        SetStatus(_editorViewModel.StatusMessage);
        if (_selectedCompetition != null)
            ShowCompetitionEditor(_selectedCompetition);
    }

    /// <summary>
    /// Randomise existing groups and show the animated draw ceremony.
    /// </summary>
    private async void OnRandomiseWithDraw()
    {
        if (_editorViewModel == null || _selectedCompetition == null) return;

        await _editorViewModel.RandomiseGroupsAsync();
        await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);

        if (_selectedCompetition.Groups.Count == 0)
        {
            SetStatus(_editorViewModel.StatusMessage);
            ShowCompetitionEditor(_selectedCompetition);
            return;
        }

        await ShowDrawAnimation(_selectedCompetition);

        SetStatus(_editorViewModel.StatusMessage);
        ShowCompetitionEditor(_selectedCompetition);
    }

    /// <summary>
    /// Build the player names and group assignments, then show the draw animation page.
    /// </summary>
    private async Task ShowDrawAnimation(Competition competition)
    {
        if (_editorViewModel == null) return;

        // Resolve player names for each group
        var groupAssignments = new List<List<string>>();
        var allNames = new List<string>();

        foreach (var group in competition.Groups.OrderBy(g => g.GroupNumber))
        {
            var names = new List<string>();
            foreach (var pid in group.ParticipantIds)
            {
                var name = _editorViewModel.GetParticipantName(pid) ?? $"Player {pid.ToString()[..6]}";
                names.Add(name);
                allNames.Add(name);
            }
            groupAssignments.Add(names);
        }

        if (allNames.Count == 0) return;

        var drawPage = new GroupDrawAnimationPage(allNames, groupAssignments, competition.Groups.Count);
        await Navigation.PushModalAsync(new NavigationPage(drawPage)
        {
            BarBackgroundColor = Color.FromArgb("#0F172A"),
            BarTextColor = Colors.White
        });
        await drawPage.GetResultAsync();
    }

    private void ShowGroupsView()
    {
        // Capture current scroll position before rebuilding the view
        double scrollY = GetGroupsScrollY();
        ShowGroupsView(_selectedCompetition?.Groups, editable: true, restoreScrollY: scrollY);
    }

    private void ShowPreviousGroupRound(int roundNumber)
    {
        if (_selectedCompetition == null) return;

        var roundGroups = _selectedCompetition.PreviousGroups
            .Where(g => g.GroupRound == roundNumber)
            .OrderBy(g => g.GroupNumber)
            .ToList();

        if (roundGroups.Count == 0) return;

        ShowGroupsView(roundGroups, editable: false, roundLabel: $" — Round {roundNumber}");
    }

    private void ShowGroupsView(List<CompetitionGroup>? groups, bool editable, string roundLabel = "", double restoreScrollY = 0)
    {
        if (_selectedCompetition == null || groups == null || groups.Count == 0) return;

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
            Text = editable
                ? $"{_selectedCompetition.Name}{roundLabel} — Select Winners"
                : $"{_selectedCompetition.Name}{roundLabel} — Results",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };

        headerGrid.Add(backBtn, 0, 0);
        headerGrid.Add(titleLabel, 1, 0);
        mainLayout.Children.Add(headerGrid);

        // For previous rounds, determine the topAdvance that was used for that round
        // by counting the max selected per group in the standings
        int effectiveTopAdvance = topAdvance;
        if (!editable && groups.Count > 0)
        {
            int maxSelected = groups.Max(g => g.Standings.Count(s => s.Position > 0));
            if (maxSelected > 0)
                effectiveTopAdvance = maxSelected;
        }

        mainLayout.Children.Add(new Label
        {
            Text = editable
                ? $"Tap the top {effectiveTopAdvance} player(s) in each group who got through."
                : $"Top {effectiveTopAdvance} from each group advanced.",
            FontSize = 12,
            TextColor = Colors.Gray,
            Margin = new Thickness(0, 0, 0, 4)
        });

        // Build each group as a selection list
        var groupsLayout = new VerticalStackLayout { Spacing = 12 };

        // Manual-draw "Unassigned" pool (shown when there are players not yet placed in any group)
        if (editable && _editorViewModel != null && _selectedCompetition.Rounds.Count == 0)
        {
            var unassignedIds = _editorViewModel.GetUnassignedGroupParticipants();
            if (unassignedIds.Count > 0)
            {
                groupsLayout.Children.Add(CreateUnassignedPoolView(unassignedIds, groups, _selectedCompetition.Format));
            }
        }

        foreach (var group in groups)
        {
            groupsLayout.Children.Add(CreateGroupSelectionView(group, _selectedCompetition.Format, effectiveTopAdvance, editable));
        }

        mainLayout.Children.Add(groupsLayout);

        var scrollView = new ScrollView { Content = mainLayout };
        SetContentPanel(scrollView);

        // Restore scroll position after layout completes
        if (restoreScrollY > 0)
        {
            scrollView.Dispatcher.Dispatch(async () =>
            {
                // Small delay so layout has finished measuring
                await Task.Delay(50);
                await scrollView.ScrollToAsync(0, restoreScrollY, animated: false);
            });
        }
    }

    /// <summary>
    /// Read the current vertical scroll offset from the groups ScrollView (if any).
    /// </summary>
    private double GetGroupsScrollY()
    {
        if (ContentPanel.Children.FirstOrDefault() is ScrollView sv)
            return sv.ScrollY;
        return 0;
    }

    private View CreateGroupSelectionView(CompetitionGroup group, CompetitionFormat format, int topAdvance, bool editable = true)
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
        var headerText = $"{group.Name} ({group.ParticipantIds.Count} players) — {selected}/{topAdvance} selected";

        var headerStack = new VerticalStackLayout { Spacing = 2 };
        headerStack.Children.Add(new Label
        {
            Text = headerText,
            TextColor = Colors.White,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold
        });

        if (!string.IsNullOrEmpty(group.VenueDisplay))
        {
            var venueLine = new Label
            {
                Text = $"📍 {group.VenueDisplay}",
                TextColor = Color.FromArgb("#BFDBFE"),
                FontSize = 12
            };
            if (editable && _selectedCompetition != null)
            {
                var comp = _selectedCompetition;
                var grp = group;
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, _) => await AssignGroupVenueAsync(grp, comp);
                venueLine.GestureRecognizers.Add(tap);
            }
            headerStack.Children.Add(venueLine);
        }
        else if (editable && _selectedCompetition != null)
        {
            var comp = _selectedCompetition;
            var grp = group;
            var venueLine = new Label
            {
                Text = "📍 Tap to assign venue / table",
                TextColor = Color.FromArgb("#BFDBFE"),
                FontSize = 12,
                FontAttributes = FontAttributes.Italic
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await AssignGroupVenueAsync(grp, comp);
            venueLine.GestureRecognizers.Add(tap);
            headerStack.Children.Add(venueLine);
        }

        headerBorder.Content = headerStack;

        var playersLayout = new VerticalStackLayout { Spacing = 0 };

        foreach (var participantId in group.ParticipantIds)
        {
            var name = GetParticipantName(participantId, format) ?? "Unknown";
            bool isSelected = selectedIds.Contains(participantId);
            bool isNoShow = _selectedCompetition != null && _selectedCompetition.NoShowIds.Contains(participantId);

            var rowBorder = new Border
            {
                Padding = new Thickness(12, 8),
                BackgroundColor = isNoShow ? Color.FromArgb("#FEE2E2")
                    : isSelected ? Color.FromArgb("#DBEAFE")
                    : Colors.White,
                Margin = new Thickness(0, 1),
            };

            var rowGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                ColumnSpacing = 10
            };

            var checkLabel = new Label
            {
                Text = isNoShow ? "🚫" : isSelected ? "✅" : "⬜",
                FontSize = 18,
                VerticalTextAlignment = TextAlignment.Center
            };

            var nameLabel = new Label
            {
                Text = name,
                FontSize = 14,
                VerticalTextAlignment = TextAlignment.Center,
                FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isNoShow ? Color.FromArgb("#991B1B")
                    : isSelected ? Color.FromArgb("#1D4ED8")
                    : Colors.Black,
                TextDecorations = isNoShow ? TextDecorations.Strikethrough : TextDecorations.None
            };

            var statusLabel = new Label
            {
                Text = isNoShow ? "No Show" : isSelected ? "Through" : "",
                FontSize = 11,
                TextColor = isNoShow ? Color.FromArgb("#DC2626") : Color.FromArgb("#059669"),
                VerticalTextAlignment = TextAlignment.Center,
                FontAttributes = FontAttributes.Italic
            };

            rowGrid.Add(checkLabel, 0, 0);
            rowGrid.Add(nameLabel, 1, 0);
            rowGrid.Add(statusLabel, 2, 0);

            // Move-to-group button (only when editable, before KO rounds, and 2+ groups in same round)
            if (editable && _selectedCompetition != null && _selectedCompetition.Rounds.Count == 0)
            {
                var otherGroupsInRound = _selectedCompetition.Groups
                    .Where(g => g.GroupRound == group.GroupRound && g.Id != group.Id)
                    .ToList();
                if (otherGroupsInRound.Count > 0)
                {
                    var pidMove = participantId;
                    var srcGroup = group;
                    var moveBtn = new Button
                    {
                        Text = "↔",
                        FontSize = 11,
                        Padding = new Thickness(6, 2),
                        MinimumWidthRequest = 32,
                        HeightRequest = 28,
                        BackgroundColor = Color.FromArgb("#6366F1"),
                        TextColor = Colors.White,
                        CornerRadius = 4,
                        Margin = new Thickness(4, 0, 0, 0)
                    };
                    moveBtn.Clicked += async (_, _) =>
                    {
                        if (_editorViewModel == null) return;
                        var pname = GetParticipantName(pidMove, format) ?? "player";
                        var options = otherGroupsInRound
                            .OrderBy(g => g.GroupNumber)
                            .Select(g => g.Name)
                            .ToArray();
                        var chosen = await DisplayActionSheet(
                            $"Move {pname} to…", "Cancel", null, options);
                        if (string.IsNullOrEmpty(chosen) || chosen == "Cancel") return;
                        var target = otherGroupsInRound.FirstOrDefault(g => g.Name == chosen);
                        if (target == null) return;
                        await _editorViewModel.MoveParticipantToGroupAsync(pidMove, target.Id);
                        SetStatus(_editorViewModel.StatusMessage);
                        ShowGroupsView();
                    };
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    rowGrid.Add(moveBtn, rowGrid.ColumnDefinitions.Count - 1, 0);
                }
            }

            // Remove-from-group button (only when editable, before KO rounds)
            // Detaches the participant from this group (back to the Unassigned pool)
            // without removing them from the competition.
            if (editable && _selectedCompetition != null && _selectedCompetition.Rounds.Count == 0)
            {
                var pidRem = participantId;
                var grpRem = group;
                var removeBtn = new Button
                {
                    Text = "🗑",
                    FontSize = 11,
                    Padding = new Thickness(6, 2),
                    MinimumWidthRequest = 32,
                    HeightRequest = 28,
                    BackgroundColor = Color.FromArgb("#9CA3AF"),
                    TextColor = Colors.White,
                    CornerRadius = 4,
                    Margin = new Thickness(4, 0, 0, 0)
                };
                removeBtn.Clicked += async (_, _) =>
                {
                    if (_editorViewModel == null) return;
                    var pname = GetParticipantName(pidRem, format) ?? "player";
                    var ok = await DisplayAlert(
                        "Remove from Group",
                        $"Remove {pname} from {grpRem.Name}? They'll return to the Unassigned pool but stay in the competition.",
                        "Remove", "Cancel");
                    if (!ok) return;
                    await _editorViewModel.RemoveParticipantFromGroupAsync(pidRem);
                    SetStatus(_editorViewModel.StatusMessage);
                    ShowGroupsView();
                };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                rowGrid.Add(removeBtn, rowGrid.ColumnDefinitions.Count - 1, 0);
            }

            // No Show toggle button (only when editable)
            if (editable)
            {
                var pid = participantId;
                var noShowBtn = new Button
                {
                    Text = isNoShow ? "✓" : "✗",
                    FontSize = 11,
                    Padding = new Thickness(6, 2),
                    MinimumWidthRequest = 32,
                    HeightRequest = 28,
                    BackgroundColor = isNoShow ? Color.FromArgb("#6B7280") : Color.FromArgb("#DC2626"),
                    TextColor = Colors.White,
                    CornerRadius = 4
                };
                noShowBtn.Clicked += async (_, _) =>
                {
                    if (_editorViewModel == null) return;

                    if (selectedIds.Contains(pid))
                    {
                        selectedIds.Remove(pid);
                        await SaveGroupSelections(group, selectedIds, topAdvance);
                    }

                    await _editorViewModel.ToggleNoShowAsync(pid);
                    SetStatus(_editorViewModel.StatusMessage);
                    ShowGroupsView();
                };
                rowGrid.Add(noShowBtn, 3, 0);
            }

            rowBorder.Content = rowGrid;

            // Only allow tapping to toggle selection when editable
            if (editable)
            {
                var pid = participantId;

                // Tap = toggle winner selection (skip no-shows)
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, _) =>
                {
                    if (_selectedCompetition != null && _selectedCompetition.NoShowIds.Contains(pid))
                    {
                        await DisplayAlert("No Show", "This player is marked as a No Show. Press the ✗ button to remove the mark first.", "OK");
                        return;
                    }

                    if (selectedIds.Contains(pid))
                    {
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

                    await SaveGroupSelections(group, selectedIds, topAdvance);
                    ShowGroupsView();
                };
                rowBorder.GestureRecognizers.Add(tap);
            }

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
    /// Build the "Unassigned" pool view for manual draw — lists every participant
    /// not yet placed in any group, with a button to place them into a chosen group.
    /// </summary>
    private View CreateUnassignedPoolView(List<Guid> unassignedIds, List<CompetitionGroup> groups, CompetitionFormat format)
    {
        var headerBorder = new Border
        {
            Padding = 10,
            BackgroundColor = Color.FromArgb("#0EA5E9"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
            Content = new Label
            {
                Text = $"✋ Unassigned ({unassignedIds.Count}) — tap a player to place into a group",
                TextColor = Colors.White,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold
            }
        };

        var listLayout = new VerticalStackLayout { Spacing = 0 };
        foreach (var pid in unassignedIds)
        {
            var name = GetParticipantName(pid, format) ?? "Unknown";
            var idCopy = pid;

            var rowGrid = new Grid
            {
                Padding = new Thickness(12, 8),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 8
            };

            rowGrid.Add(new Label
            {
                Text = name,
                FontSize = 14,
                VerticalTextAlignment = TextAlignment.Center
            }, 0, 0);

            var placeBtn = new Button
            {
                Text = "Place →",
                FontSize = 12,
                Padding = new Thickness(8, 4),
                BackgroundColor = Color.FromArgb("#0EA5E9"),
                TextColor = Colors.White,
                CornerRadius = 4
            };
            placeBtn.Clicked += async (_, _) =>
            {
                if (_editorViewModel == null) return;
                var ordered = groups.OrderBy(g => g.GroupNumber).ToList();
                var options = ordered
                    .Select(g => $"{g.Name} ({g.ParticipantIds.Count})")
                    .ToArray();
                var chosen = await DisplayActionSheet($"Place {name} into…", "Cancel", null, options);
                if (string.IsNullOrEmpty(chosen) || chosen == "Cancel") return;
                var idx = Array.IndexOf(options, chosen);
                if (idx < 0 || idx >= ordered.Count) return;
                await _editorViewModel.AssignParticipantToGroupAsync(idCopy, ordered[idx].Id);
                SetStatus(_editorViewModel.StatusMessage);
                ShowGroupsView();
            };
            rowGrid.Add(placeBtn, 1, 0);

            listLayout.Children.Add(new Border
            {
                Padding = 0,
                Margin = new Thickness(0, 1),
                BackgroundColor = Colors.White,
                Content = rowGrid
            });
        }

        return new Border
        {
            Padding = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Stroke = Color.FromArgb("#0EA5E9"),
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                Children = { headerBorder, listLayout }
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

        int winnerCount = _selectedCompetition.Groups.Count * topAdvance;

        var confirm = await DisplayAlert(
            "Create Knockout",
            $"This will create a knockout bracket from the {winnerCount} selected winners.\n\nContinue?",
            "Yes, Create Knockout",
            "Cancel");

        if (!confirm) return;

        await _editorViewModel.FinalizeGroupsCommand.ExecuteAsync(null);
        await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);
        SetStatus(_editorViewModel.StatusMessage);

        if (_selectedCompetition != null)
            ShowCompetitionEditor(_selectedCompetition);
    }

    private async void OnAdvanceToNextGroupRound()
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

        int winnerCount = _selectedCompetition.Groups.Count * topAdvance;

        // Ask how many groups for the next round and how many to advance per group
        var groupOptions = new List<string>();
        for (int g = 1; g <= Math.Max(1, winnerCount / 2); g++)
        {
            int perGroup = winnerCount / g;
            if (perGroup < 2) break;
            int rem = winnerCount % g;
            var label = g == 1
                ? $"1 group ({winnerCount} players)"
                : $"{g} groups (~{perGroup}{(rem > 0 ? $"-{perGroup + 1}" : "")} per group)";
            groupOptions.Add(label);
        }

        if (groupOptions.Count == 0)
        {
            await DisplayAlert("Not Enough", "Not enough winners for another group round.", "OK");
            return;
        }

        var choice = await DisplayActionSheet(
            $"Next Group Round — {winnerCount} winners",
            "Cancel",
            null,
            groupOptions.ToArray());

        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        // Parse the group count from the selection
        int selectedIndex = groupOptions.IndexOf(choice);
        int newGroupCount = selectedIndex + 1;

        // Ask how many should advance per group for this round
        int maxPerGroup = winnerCount / newGroupCount;
        int maxAdvance = Math.Max(1, maxPerGroup - 1); // at least 1, at most groupSize-1

        if (maxAdvance == 1)
        {
            // Only 1 can advance, no need to ask
            await _editorViewModel.AdvanceToNextGroupRoundAsync(newGroupCount, 1);
        }
        else
        {
            var advanceOptions = new List<string>();
            for (int a = 1; a <= maxAdvance; a++)
                advanceOptions.Add($"Top {a}");

            var advanceChoice = await DisplayActionSheet(
                $"How many advance per group?",
                "Cancel",
                null,
                advanceOptions.ToArray());

            if (string.IsNullOrEmpty(advanceChoice) || advanceChoice == "Cancel") return;

            int advanceCount = advanceOptions.IndexOf(advanceChoice) + 1;
            await _editorViewModel.AdvanceToNextGroupRoundAsync(newGroupCount, advanceCount);
        }
        await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);
        SetStatus(_editorViewModel.StatusMessage);

        if (_selectedCompetition != null)
            ShowCompetitionEditor(_selectedCompetition);
    }
}
