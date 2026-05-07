using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

/// <summary>
/// Participant management and selection dialogs
/// </summary>
public partial class CompetitionsPage
{
    private async void OnAddParticipant()
    {
        if (_editorViewModel == null || _selectedCompetition == null) return;

        var format = _selectedCompetition.Format;

        if (format == CompetitionFormat.SinglesKnockout || format == CompetitionFormat.RoundRobin || 
            format == CompetitionFormat.Swiss || format == CompetitionFormat.SinglesGroupStage)
        {
            // Add players with multi-select
            await ShowMultiSelectPlayersDialog();
        }
        else if (format == CompetitionFormat.DoublesKnockout || format == CompetitionFormat.DoublesGroupStage)
        {
            // Add doubles team
            await ShowDoublesTeamSelectionDialog();
        }
        else if (format == CompetitionFormat.TeamKnockout)
        {
            // Add teams with multi-select
            await ShowMultiSelectTeamsDialog();
        }

        // Rebuild the editor so group stage steps (venues, groups) update
        // based on the new participant count
        if (format is CompetitionFormat.SinglesGroupStage or CompetitionFormat.DoublesGroupStage)
        {
            ShowCompetitionEditor(_selectedCompetition);
        }
    }

    private async Task ShowDoublesTeamSelectionDialog()
    {
        if (_editorViewModel == null || _selectedCompetition == null) return;

        var availablePlayers = await _editorViewModel.GetAvailableDoublesPlayersAsync();

        if (availablePlayers.Count < 2)
        {
            await DisplayAlert("Not Enough Players", "Need at least 2 available players to create a doubles team. All players may already be assigned to teams.", "OK");
            return;
        }

        // Load teams for the season so we can group players by team
        var seasonId = _selectedCompetition.SeasonId ?? _currentSeasonId;
        var allTeams = _dataStore.GetData()?.Teams?
            .Where(t => t != null && t.SeasonId == seasonId)
            .OrderBy(t => t.Name)
            .ToList() ?? new List<Team>();

        var allItems = availablePlayers.Select(p => new SelectionItem<Guid>
        {
            Id = p.Id,
            Name = p.FullName,
            IsSelected = false,
            Tag = p.TeamId
        }).ToList();

        var teamEntries = new List<(Guid? id, string name)> { (null, "All Players") };
        var teamsWithPlayers = allTeams
            .Where(t => availablePlayers.Any(p => p.TeamId == t.Id))
            .OrderBy(t => t.Name)
            .ToList();
        foreach (var team in teamsWithPlayers)
            teamEntries.Add((team.Id, team.Name ?? "Unnamed Team"));
        if (availablePlayers.Any(p => p.TeamId == null))
            teamEntries.Add((Guid.Empty, "No Team"));

        // ── UI elements ─────────────────────────────────────────────────
        var teamListLayout = new VerticalStackLayout { Spacing = 0 };
        var playerListView = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemsSource = new ObservableCollection<SelectionItem<Guid>>(allItems)
        };

        // Live preview of team name (editable)
        var teamNameEntry = new Entry
        {
            Placeholder = "Team name (auto-fills when 2 players picked)",
            FontSize = 14
        };
        bool userEditedName = false;
        teamNameEntry.TextChanged += (s, e) =>
        {
            // Only flag as user-edited if they actually typed (not when we auto-set it)
            if (teamNameEntry.IsFocused)
                userEditedName = true;
        };

        var statusLabel = new Label
        {
            Text = "Pick 2 players",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = Color.FromArgb("#6B7280"),
            Margin = new Thickness(0, 4)
        };

        var addBtn = new Button
        {
            Text = "\U0001F465 Add Team",
            BackgroundColor = Color.FromArgb("#10B981"),
            TextColor = Colors.White,
            Padding = new Thickness(12, 6),
            FontSize = 14,
            IsEnabled = false
        };

        void RefreshSelectionState()
        {
            var selected = allItems.Where(i => i.IsSelected).ToList();
            int count = selected.Count;

            if (count == 0)
            {
                statusLabel.Text = "Pick 2 players";
                statusLabel.TextColor = Color.FromArgb("#6B7280");
            }
            else if (count == 1)
            {
                statusLabel.Text = $"\u2713 1 selected \u00B7 pick 1 more";
                statusLabel.TextColor = Color.FromArgb("#F59E0B");
            }
            else if (count == 2)
            {
                statusLabel.Text = "\u2713 2 selected \u00B7 ready to add";
                statusLabel.TextColor = Color.FromArgb("#10B981");
            }
            else
            {
                statusLabel.Text = $"\u26A0 {count} selected \u00B7 only 2 allowed";
                statusLabel.TextColor = Color.FromArgb("#EF4444");
            }

            // Auto-update the team name if user hasn't manually edited it
            if (!userEditedName && count == 2)
            {
                var wasFocused = teamNameEntry.IsFocused;
                teamNameEntry.Text = $"{selected[0].Name} & {selected[1].Name}";
                userEditedName = false; // re-clear in case the TextChanged set it
            }

            addBtn.IsEnabled = count == 2 && !string.IsNullOrWhiteSpace(teamNameEntry.Text);
        }

        teamNameEntry.TextChanged += (s, e) => addBtn.IsEnabled = allItems.Count(i => i.IsSelected) == 2 && !string.IsNullOrWhiteSpace(teamNameEntry.Text);

        Guid? currentTeamFilter = null;
        void ApplyTeamFilter(Guid? teamId)
        {
            currentTeamFilter = teamId;
            IEnumerable<SelectionItem<Guid>> filtered;
            if (teamId == null)
                filtered = allItems;
            else if (teamId == Guid.Empty)
                filtered = allItems.Where(i => i.Tag == null);
            else
                filtered = allItems.Where(i => i.Tag is Guid tid && tid == teamId);
            playerListView.ItemsSource = new ObservableCollection<SelectionItem<Guid>>(filtered.ToList());
            RebuildTeamList(teamListLayout, teamEntries, allItems, teamId, ApplyTeamFilter, RefreshSelectionState);
        }

        playerListView.ItemTemplate = new DataTemplate(() =>
        {
            var grid = new Grid
            {
                Padding = new Thickness(8, 4),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(36) },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };

            var checkBox = new CheckBox { VerticalOptions = LayoutOptions.Center };
            checkBox.SetBinding(CheckBox.IsCheckedProperty, nameof(SelectionItem<Guid>.IsSelected), BindingMode.TwoWay);

            var nameLabel = new Label { VerticalTextAlignment = TextAlignment.Center, FontSize = 14 };
            nameLabel.SetBinding(Label.TextProperty, nameof(SelectionItem<Guid>.Name));

            grid.Add(checkBox, 0, 0);
            grid.Add(nameLabel, 1, 0);

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) =>
            {
                if (grid.BindingContext is SelectionItem<Guid> item)
                {
                    // If trying to select a 3rd, block it
                    if (!item.IsSelected && allItems.Count(i => i.IsSelected) >= 2)
                        return;
                    item.IsSelected = !item.IsSelected;
                }
            };
            grid.GestureRecognizers.Add(tap);

            return grid;
        });

        // Enforce max-2 selection on PropertyChanged
        foreach (var item in allItems)
        {
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != nameof(SelectionItem<Guid>.IsSelected)) return;
                if (s is SelectionItem<Guid> changed && changed.IsSelected
                    && allItems.Count(i => i.IsSelected) > 2)
                {
                    // Revert: too many selected
                    changed.IsSelected = false;
                    return;
                }
                RefreshSelectionState();
            };
        }

        RebuildTeamList(teamListLayout, teamEntries, allItems, null, ApplyTeamFilter, RefreshSelectionState);

        var teamPanel = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
            Stroke = Color.FromArgb("#E5E7EB"),
            Padding = 0,
            Content = new ScrollView { Content = teamListLayout }
        };

        var playerPanel = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                statusLabel,
                new Border
                {
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                    Stroke = Color.FromArgb("#E5E7EB"),
                    Padding = 0,
                    Content = playerListView,
                    VerticalOptions = LayoutOptions.FillAndExpand
                }
            }
        };

        var splitGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }
            },
            ColumnSpacing = 8,
            RowDefinitions = { new RowDefinition { Height = GridLength.Star } },
            VerticalOptions = LayoutOptions.FillAndExpand
        };
        splitGrid.Add(teamPanel, 0, 0);
        splitGrid.Add(playerPanel, 1, 0);

        // Team name preview block
        var namePreviewBlock = new Border
        {
            Padding = 10,
            BackgroundColor = Color.FromArgb("#F0F9FF"),
            Stroke = Color.FromArgb("#6366F1"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = "\U0001F465 Team Name",
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#1E40AF")
                    },
                    teamNameEntry
                }
            }
        };

        var tcs = new TaskCompletionSource<bool>();

        addBtn.Clicked += async (s, e) =>
        {
            var selected = allItems.Where(i => i.IsSelected).ToList();
            if (selected.Count != 2)
            {
                await DisplayAlert("Pick 2 Players", "Please pick exactly 2 players.", "OK");
                return;
            }

            var team = new DoublesTeam
            {
                Player1Id = selected[0].Id,
                Player2Id = selected[1].Id,
                TeamName = string.IsNullOrWhiteSpace(teamNameEntry.Text)
                    ? $"{selected[0].Name} & {selected[1].Name}"
                    : teamNameEntry.Text.Trim()
            };

            await _editorViewModel!.AddDoublesTeamCommand.ExecuteAsync(team);
            SetStatus(_editorViewModel.StatusMessage);

            tcs.TrySetResult(true);
            await Navigation.PopModalAsync();
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#EF4444"),
            TextColor = Colors.White,
            Padding = new Thickness(12, 6),
            FontSize = 14
        };
        cancelBtn.Clicked += async (s, e) =>
        {
            tcs.TrySetResult(false);
            await Navigation.PopModalAsync();
        };

        var bottomBar = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Children = { addBtn, cancelBtn }
        };

        var rootGrid = new Grid
        {
            Padding = 12,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 8
        };
        rootGrid.Add(splitGrid, 0, 0);
        rootGrid.Add(namePreviewBlock, 0, 1);
        rootGrid.Add(bottomBar, 0, 2);

        var page = new ContentPage
        {
            Title = "Add Doubles Team",
            Content = rootGrid
        };

        await Navigation.PushModalAsync(new NavigationPage(page));
        await tcs.Task;
    }

    private async Task ShowMultiSelectPlayersDialog()
    {
        if (_editorViewModel == null || _selectedCompetition == null) return;

        var availablePlayers = await _editorViewModel.GetAvailablePlayersAsync();

        if (availablePlayers.Count == 0)
        {
            await DisplayAlert("No Players", "All players have been added or no players available.", "OK");
            return;
        }

        // Load teams for the season from JSON store
        var seasonId = _selectedCompetition.SeasonId ?? _currentSeasonId;
        var allTeams = _dataStore.GetData()?.Teams?
            .Where(t => t != null && t.SeasonId == seasonId)
            .OrderBy(t => t.Name)
            .ToList() ?? new List<Team>();

        // Create selection items for all available players
        var allItems = availablePlayers.Select(p => new SelectionItem<Guid>
        {
            Id = p.Id,
            Name = p.FullName,
            IsSelected = false,
            Tag = p.TeamId  // Store team ID for filtering
        }).ToList();

        // Build team list: "All Players", then each team, then "No Team"
        var teamEntries = new List<(Guid? id, string name)> { (null, "All Players") };
        var teamsWithPlayers = allTeams
            .Where(t => availablePlayers.Any(p => p.TeamId == t.Id))
            .OrderBy(t => t.Name)
            .ToList();
        foreach (var team in teamsWithPlayers)
            teamEntries.Add((team.Id, team.Name ?? "Unnamed Team"));
        if (availablePlayers.Any(p => p.TeamId == null))
            teamEntries.Add((Guid.Empty, "No Team"));

        // ── Build the UI ─────────────────────────────────────────────────
        Guid? currentTeamFilter = null; // null = show all

        var teamListLayout = new VerticalStackLayout { Spacing = 0 };
        var playerListView = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            ItemsSource = new ObservableCollection<SelectionItem<Guid>>(allItems)
        };

        var selectedCountLabel = new Label
        {
            Text = "0 selected",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4)
        };

        void UpdateSelectedCount()
        {
            var count = allItems.Count(i => i.IsSelected);
            selectedCountLabel.Text = $"{count} selected";
        }

        void ApplyTeamFilter(Guid? teamId)
        {
            currentTeamFilter = teamId;
            IEnumerable<SelectionItem<Guid>> filtered;
            if (teamId == null)
                filtered = allItems;
            else if (teamId == Guid.Empty)
                filtered = allItems.Where(i => i.Tag == null);
            else
                filtered = allItems.Where(i => i.Tag is Guid tid && tid == teamId);
            playerListView.ItemsSource = new ObservableCollection<SelectionItem<Guid>>(filtered.ToList());

            // Rebuild team buttons to show active state
            RebuildTeamList(teamListLayout, teamEntries, allItems, teamId, ApplyTeamFilter, UpdateSelectedCount);
        }

        // Player item template
        playerListView.ItemTemplate = new DataTemplate(() =>
        {
            var grid = new Grid
            {
                Padding = new Thickness(8, 4),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(36) },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };

            var checkBox = new CheckBox { VerticalOptions = LayoutOptions.Center };
            checkBox.SetBinding(CheckBox.IsCheckedProperty, nameof(SelectionItem<Guid>.IsSelected), BindingMode.TwoWay);

            var nameLabel = new Label { VerticalTextAlignment = TextAlignment.Center, FontSize = 14 };
            nameLabel.SetBinding(Label.TextProperty, nameof(SelectionItem<Guid>.Name));

            grid.Add(checkBox, 0, 0);
            grid.Add(nameLabel, 1, 0);

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) =>
            {
                if (grid.BindingContext is SelectionItem<Guid> item)
                    item.IsSelected = !item.IsSelected;
            };
            grid.GestureRecognizers.Add(tapGesture);

            return grid;
        });

        // Monitor selection changes
        foreach (var item in allItems)
            item.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SelectionItem<Guid>.IsSelected)) UpdateSelectedCount(); };

        // Build team sidebar
        RebuildTeamList(teamListLayout, teamEntries, allItems, null, ApplyTeamFilter, UpdateSelectedCount);

        // Team panel (left)
        var teamPanel = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
            Stroke = Color.FromArgb("#E5E7EB"),
            Padding = 0,
            Content = new ScrollView { Content = teamListLayout }
        };

        // Player panel (right)
        var playerPanel = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = 6,
                    Margin = new Thickness(0, 0, 0, 4),
                    Children =
                    {
                        new Button
                        {
                            Text = "Select All Visible",
                            BackgroundColor = Color.FromArgb("#3B82F6"),
                            TextColor = Colors.White,
                            Padding = new Thickness(8, 4),
                            FontSize = 12,
                            Command = new Command(() =>
                            {
                                var visible = (playerListView.ItemsSource as ObservableCollection<SelectionItem<Guid>>);
                                if (visible != null) foreach (var item in visible) item.IsSelected = true;
                                UpdateSelectedCount();
                            })
                        },
                        new Button
                        {
                            Text = "Deselect All",
                            BackgroundColor = Color.FromArgb("#6B7280"),
                            TextColor = Colors.White,
                            Padding = new Thickness(8, 4),
                            FontSize = 12,
                            Command = new Command(() =>
                            {
                                foreach (var item in allItems) item.IsSelected = false;
                                UpdateSelectedCount();
                            })
                        }
                    }
                },
                selectedCountLabel,
                new Border
                {
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                    Stroke = Color.FromArgb("#E5E7EB"),
                    Padding = 0,
                    Content = playerListView,
                    VerticalOptions = LayoutOptions.FillAndExpand
                }
            }
        };

        // Split layout: teams left, players right
        var splitGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }
            },
            ColumnSpacing = 8,
            RowDefinitions = { new RowDefinition { Height = GridLength.Star } },
            VerticalOptions = LayoutOptions.FillAndExpand
        };
        splitGrid.Add(teamPanel, 0, 0);
        splitGrid.Add(playerPanel, 1, 0);

        // Action buttons
        var tcs = new TaskCompletionSource<List<Guid>?>();

        var doneBtn = new Button
        {
            Text = "Add Selected",
            BackgroundColor = Color.FromArgb("#10B981"),
            TextColor = Colors.White,
            Padding = new Thickness(12, 6),
            FontSize = 14
        };
        doneBtn.Clicked += (s, e) =>
        {
            var selected = allItems.Where(i => i.IsSelected).Select(i => i.Id).ToList();
            tcs.TrySetResult(selected);
            Navigation.PopModalAsync();
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#EF4444"),
            TextColor = Colors.White,
            Padding = new Thickness(12, 6),
            FontSize = 14
        };
        cancelBtn.Clicked += (s, e) =>
        {
            tcs.TrySetResult(null);
            Navigation.PopModalAsync();
        };

        var bottomBar = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Children = { doneBtn, cancelBtn }
        };

        var rootGrid = new Grid
        {
            Padding = 12,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 8
        };
        rootGrid.Add(splitGrid, 0, 0);
        rootGrid.Add(bottomBar, 0, 1);

        var page = new ContentPage
        {
            Title = "Select Players",
            Content = rootGrid
        };

        await Navigation.PushModalAsync(new NavigationPage(page));

        var selectedIds = await tcs.Task;
        if (selectedIds != null && selectedIds.Count != 0)
        {
            await _editorViewModel!.AddParticipantIdsCommand.ExecuteAsync(selectedIds);
            SetStatus(_editorViewModel.StatusMessage);
        }
    }

    private static void RebuildTeamList(
        VerticalStackLayout layout,
        List<(Guid? id, string name)> teamEntries,
        List<SelectionItem<Guid>> allItems,
        Guid? activeTeamId,
        Action<Guid?> onTeamTapped,
        Action updateCount)
    {
        layout.Children.Clear();

        layout.Children.Add(new Label
        {
            Text = "Teams",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(10, 8, 10, 4)
        });

        foreach (var (teamId, teamName) in teamEntries)
        {
            bool isActive = teamId == activeTeamId;

            // Count: total available in this team + how many are selected
            int total, selected;
            if (teamId == null)
            {
                total = allItems.Count;
                selected = allItems.Count(i => i.IsSelected);
            }
            else if (teamId == Guid.Empty)
            {
                total = allItems.Count(i => i.Tag == null);
                selected = allItems.Count(i => i.Tag == null && i.IsSelected);
            }
            else
            {
                total = allItems.Count(i => i.Tag is Guid t && t == teamId);
                selected = allItems.Count(i => i.Tag is Guid t && t == teamId && i.IsSelected);
            }

            var teamBtn = new Border
            {
                Padding = new Thickness(10, 8),
                BackgroundColor = isActive ? Color.FromArgb("#DBEAFE") : Colors.Transparent,
                Content = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label
                        {
                            Text = teamName,
                            FontSize = 13,
                            FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None,
                            TextColor = isActive ? Color.FromArgb("#1D4ED8") : Colors.Black,
                            LineBreakMode = LineBreakMode.TailTruncation
                        },
                        new Label
                        {
                            Text = selected > 0 ? $"{selected}/{total} selected" : $"{total} players",
                            FontSize = 10,
                            TextColor = selected > 0 ? Color.FromArgb("#059669") : Colors.Gray
                        }
                    }
                }
            };

            var tid = teamId; // capture
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => onTeamTapped(tid);
            teamBtn.GestureRecognizers.Add(tap);

            layout.Children.Add(teamBtn);

            // Add "Select all team" shortcut
            if (teamId != null)
            {
                var selectTeamBtn = new Label
                {
                    Text = "  ＋ select all",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#3B82F6"),
                    Padding = new Thickness(10, 0, 10, 4)
                };
                var stap = new TapGestureRecognizer();
                var capturedId = teamId;
                stap.Tapped += (_, _) =>
                {
                    IEnumerable<SelectionItem<Guid>> teamPlayers;
                    if (capturedId == Guid.Empty)
                        teamPlayers = allItems.Where(i => i.Tag == null);
                    else
                        teamPlayers = allItems.Where(i => i.Tag is Guid t && t == capturedId);
                    foreach (var p in teamPlayers) p.IsSelected = true;
                    updateCount();
                    onTeamTapped(capturedId); // refresh
                };
                selectTeamBtn.GestureRecognizers.Add(stap);
                layout.Children.Add(selectTeamBtn);
            }
        }
    }

    private async Task ShowMultiSelectTeamsDialog()
    {
        if (_editorViewModel == null || _selectedCompetition == null) return;

        var availableTeams = await _editorViewModel.GetAvailableTeamsAsync();

        if (availableTeams.Count == 0)
        {
            await DisplayAlert("No Teams", "All teams have been added or no teams available.", "OK");
            return;
        }

        // Create selection items
        var selectionItems = availableTeams.Select(t => new SelectionItem<Guid>
        {
            Id = t.Id,
            Name = t.Name ?? "Unnamed Team",
            IsSelected = false
        }).ToList();

        // Show multi-select dialog
        var selectedIds = await ShowMultiSelectDialog("Select Teams", selectionItems);
        
        if (selectedIds != null && selectedIds.Count != 0)
        {
            await _editorViewModel!.AddParticipantIdsCommand.ExecuteAsync(selectedIds);
            SetStatus(_editorViewModel.StatusMessage);
        }
    }

    private async Task<System.Collections.Generic.List<Guid>?> ShowMultiSelectDialog(string title, System.Collections.Generic.List<SelectionItem<Guid>> items)
    {
        var selectionPage = new ContentPage
        {
            Title = title
        };

        var searchEntry = new Entry
        {
            Placeholder = "Search...",
            Margin = new Thickness(10)
        };

        var selectAllBtn = new Button
        {
            Text = "Select All",
            Margin = new Thickness(10, 0),
            BackgroundColor = Color.FromArgb("#3B82F6"),
            TextColor = Colors.White
        };

        var deselectAllBtn = new Button
        {
            Text = "Deselect All",
            Margin = new Thickness(10, 0),
            BackgroundColor = Color.FromArgb("#6B7280"),
            TextColor = Colors.White
        };

        var selectionList = new CollectionView
        {
            ItemsSource = new ObservableCollection<SelectionItem<Guid>>(items),
            SelectionMode = SelectionMode.None,
            ItemTemplate = new DataTemplate(() =>
            {
                var grid = new Grid
                {
                    Padding = new Thickness(10, 5),
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(40) },
                        new ColumnDefinition { Width = GridLength.Star }
                    }
                };

                var checkBox = new CheckBox
                {
                    VerticalOptions = LayoutOptions.Center
                };
                checkBox.SetBinding(CheckBox.IsCheckedProperty, nameof(SelectionItem<Guid>.IsSelected), BindingMode.TwoWay);

                var nameLabel = new Label
                {
                    VerticalTextAlignment = TextAlignment.Center,
                    FontSize = 15
                };
                nameLabel.SetBinding(Label.TextProperty, nameof(SelectionItem<Guid>.Name));

                grid.Add(checkBox, 0, 0);
                grid.Add(nameLabel, 1, 0);

                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += (s, e) =>
                {
                    if (grid.BindingContext is SelectionItem<Guid> item)
                    {
                        item.IsSelected = !item.IsSelected;
                    }
                };
                grid.GestureRecognizers.Add(tapGesture);

                return grid;
            })
        };

        var selectedCountLabel = new Label
        {
            Text = "0 selected",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(10),
            HorizontalTextAlignment = TextAlignment.Center
        };

        var doneBtn = new Button
        {
            Text = "Add Selected",
            Margin = new Thickness(10),
            BackgroundColor = Color.FromArgb("#10B981"),
            TextColor = Colors.White
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            Margin = new Thickness(10),
            BackgroundColor = Color.FromArgb("#EF4444"),
            TextColor = Colors.White
        };

        // Update selected count label
        void UpdateSelectedCount()
        {
            var count = items.Count(i => i.IsSelected);
            selectedCountLabel.Text = $"{count} selected";
        }

        // Wire up events
        selectAllBtn.Clicked += (s, e) =>
        {
            foreach (var item in items)
                item.IsSelected = true;
            UpdateSelectedCount();
        };

        deselectAllBtn.Clicked += (s, e) =>
        {
            foreach (var item in items)
                item.IsSelected = false;
            UpdateSelectedCount();
        };

        searchEntry.TextChanged += (s, e) =>
        {
            var filtered = string.IsNullOrWhiteSpace(e.NewTextValue)
                ? items
                : items.Where(i => i.Name.Contains(e.NewTextValue, StringComparison.OrdinalIgnoreCase)).ToList();
            
            selectionList.ItemsSource = new ObservableCollection<SelectionItem<Guid>>(filtered);
        };

        // Monitor selection changes
        foreach (var item in items)
        {
            item.PropertyChanged += (s, e) => UpdateSelectedCount();
        }

        var taskCompletionSource = new TaskCompletionSource<System.Collections.Generic.List<Guid>?>();

        doneBtn.Clicked += (s, e) =>
        {
            var selected = items.Where(i => i.IsSelected).Select(i => i.Id).ToList();
            taskCompletionSource.SetResult(selected);
            Navigation.PopModalAsync();
        };

        cancelBtn.Clicked += (s, e) =>
        {
            taskCompletionSource.SetResult(null);
            Navigation.PopModalAsync();
        };

        selectionPage.Content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                searchEntry,
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Margin = new Thickness(10, 0),
                    Children = { selectAllBtn, deselectAllBtn }
                },
                selectedCountLabel,
                new Frame
                {
                    Padding = 0,
                    Margin = new Thickness(10),
                    Content = selectionList,
                    HeightRequest = 400
                },
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Margin = new Thickness(10, 0),
                    Children = { doneBtn, cancelBtn }
                }
            }
        };

        await Navigation.PushModalAsync(new NavigationPage(selectionPage));
        
        return await taskCompletionSource.Task;
    }

    // Helper class for multi-select
    internal class SelectionItem<T> : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;

        public T Id { get; set; } = default!;
        public string Name { get; set; } = "";

        /// <summary>Optional tag for grouping/filtering (e.g. team ID).</summary>
        public object? Tag { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
