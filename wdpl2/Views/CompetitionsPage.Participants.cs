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
        if (_selectedCompetition == null) return;

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
    }

    private async Task ShowDoublesTeamSelectionDialog()
    {
        if (_selectedCompetition == null) return;

        // Get all player IDs already in doubles teams
        var usedPlayerIds = new HashSet<Guid>();
        foreach (var team in _selectedCompetition.DoublesTeams)
        {
            usedPlayerIds.Add(team.Player1Id);
            usedPlayerIds.Add(team.Player2Id);
        }

        // Filter out already-used players
        var availablePlayers = DataStore.Data.Players
            .Where(p => p.SeasonId == _currentSeasonId)
            .Where(p => !usedPlayerIds.Contains(p.Id))
            .OrderBy(p => p.FullName)
            .ToList();

        if (availablePlayers.Count < 2)
        {
            await DisplayAlert("Not Enough Players", "Need at least 2 available players to create a doubles team. All players may already be assigned to teams.", "OK");
            return;
        }

        var selectionPage = new ContentPage
        {
            Title = "Select Doubles Team"
        };

        var player1Picker = new Picker
        {
            Title = "Select Player 1",
            ItemsSource = availablePlayers.Select(p => p.FullName).ToList()
        };

        var player2Picker = new Picker
        {
            Title = "Select Player 2",
            ItemsSource = availablePlayers.Select(p => p.FullName).ToList()
        };

        var addBtn = new Button
        {
            Text = "Add Team",
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

        var taskCompletionSource = new TaskCompletionSource<bool>();

        addBtn.Clicked += (s, e) =>
        {
            if (player1Picker.SelectedIndex < 0 || player2Picker.SelectedIndex < 0)
            {
                DisplayAlert("Error", "Please select both players", "OK");
                return;
            }

            if (player1Picker.SelectedIndex == player2Picker.SelectedIndex)
            {
                DisplayAlert("Error", "Please select different players", "OK");
                return;
            }

            var p1 = availablePlayers[player1Picker.SelectedIndex];
            var p2 = availablePlayers[player2Picker.SelectedIndex];

            var team = new DoublesTeam
            {
                Player1Id = p1.Id,
                Player2Id = p2.Id,
                TeamName = $"{p1.FullName} & {p2.FullName}"
            };

            _selectedCompetition.DoublesTeams.Add(team);
            RefreshParticipants(_selectedCompetition);
            SetStatus($"Added doubles team: {team.TeamName}");

            taskCompletionSource.SetResult(true);
            Navigation.PopModalAsync();
        };

        cancelBtn.Clicked += (s, e) =>
        {
            taskCompletionSource.SetResult(false);
            Navigation.PopModalAsync();
        };

        selectionPage.Content = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = 20,
            Children =
            {
                new Label { Text = "Select 2 Players", FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Frame
                {
                    Padding = 10,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            new Label { Text = "Player 1:", FontAttributes = FontAttributes.Bold },
                            player1Picker,
                            new Label { Text = "Player 2:", FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 10, 0, 0) },
                            player2Picker
                        }
                    }
                },
                new HorizontalStackLayout
                {
                    Spacing = 8,
                    Margin = new Thickness(0, 20, 0, 0),
                    Children = { addBtn, cancelBtn }
                }
            }
        };

        await Navigation.PushModalAsync(new NavigationPage(selectionPage));
        await taskCompletionSource.Task;
    }

    private async Task ShowMultiSelectPlayersDialog()
    {
        if (_selectedCompetition == null) return;

        var availablePlayers = DataStore.Data.Players
            .Where(p => p.SeasonId == _currentSeasonId)
            .Where(p => !_selectedCompetition.ParticipantIds.Contains(p.Id))
            .OrderBy(p => p.FullName)
            .ToList();

        if (availablePlayers.Count == 0)
        {
            await DisplayAlert("No Players", "All players have been added or no players available.", "OK");
            return;
        }

        // Create selection items
        var selectionItems = availablePlayers.Select(p => new SelectionItem<Guid>
        {
            Id = p.Id,
            Name = p.FullName,
            IsSelected = false
        }).ToList();

        // Show multi-select dialog
        var selectedIds = await ShowMultiSelectDialog("Select Players", selectionItems);
        
        if (selectedIds != null && selectedIds.Any())
        {
            foreach (var id in selectedIds)
            {
                _selectedCompetition.ParticipantIds.Add(id);
            }
            RefreshParticipants(_selectedCompetition);
            SetStatus($"Added {selectedIds.Count} player(s)");
        }
    }

    private async Task ShowMultiSelectTeamsDialog()
    {
        if (_selectedCompetition == null) return;

        var availableTeams = DataStore.Data.Teams
            .Where(t => t.SeasonId == _currentSeasonId)
            .Where(t => !_selectedCompetition.ParticipantIds.Contains(t.Id))
            .OrderBy(t => t.Name)
            .ToList();

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
        
        if (selectedIds != null && selectedIds.Any())
        {
            foreach (var id in selectedIds)
            {
                _selectedCompetition.ParticipantIds.Add(id);
            }
            RefreshParticipants(_selectedCompetition);
            SetStatus($"Added {selectedIds.Count} team(s)");
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
