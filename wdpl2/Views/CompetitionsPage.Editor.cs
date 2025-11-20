using System;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

/// <summary>
/// Competition editor UI generation
/// </summary>
public partial class CompetitionsPage
{
    internal void ShowCompetitionEditor(Competition competition)
    {
        _nameEntry = new Entry { Text = competition.Name };
        _statusPicker = new Picker
        {
            ItemsSource = Enum.GetNames(typeof(CompetitionStatus)).ToList(),
            SelectedIndex = (int)competition.Status
        };
        _startDatePicker = new DatePicker { Date = competition.StartDate ?? DateTime.Today };
        _notesEntry = new Entry { Text = competition.Notes ?? "", Placeholder = "Notes..." };

        // Display format (read-only)
        var formatLabel = new Label
        {
            Text = competition.Format.ToString(),
            FontSize = 14,
            VerticalTextAlignment = TextAlignment.Center
        };

        // Participants list
        RefreshParticipants(competition);
        _participantsView = new CollectionView
        {
            ItemsSource = _participants,
            HeightRequest = 250,
            ItemTemplate = new DataTemplate(() =>
            {
                var grid = new Grid
                {
                    Padding = new Thickness(6, 3),
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Auto }
                    }
                };

                var nameLabel = new Label
                {
                    VerticalTextAlignment = TextAlignment.Center,
                    FontSize = 13
                };
                nameLabel.SetBinding(Label.TextProperty, nameof(ParticipantItem.Name));

                var removeBtn = new Button
                {
                    Text = "×",
                    FontSize = 16,
                    Padding = new Thickness(8, 2),
                    WidthRequest = 32,
                    BackgroundColor = Color.FromArgb("#EF4444"),
                    TextColor = Colors.White
                };
                removeBtn.SetBinding(Button.CommandParameterProperty, nameof(ParticipantItem.Id));
                removeBtn.Clicked += OnRemoveParticipant;

                grid.Add(nameLabel, 0, 0);
                grid.Add(removeBtn, 1, 0);

                return new Frame
                {
                    Padding = 2,
                    Margin = new Thickness(0, 1),
                    Content = grid
                };
            })
        };

        var content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Competition Details", FontSize = 18, FontAttributes = FontAttributes.Bold },
                
                // Basic Info
                CreateLabeledField("Name:", _nameEntry),
                CreateLabeledField("Format:", formatLabel),
                CreateLabeledField("Status:", _statusPicker),
                CreateLabeledField("Start Date:", _startDatePicker),
                CreateLabeledField("Notes:", _notesEntry),

                // Participants Section
                new Label { Text = "Participants", FontSize = 16, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 12, 0, 4) },
                new HorizontalStackLayout
                {
                    Spacing = 6,
                    Children =
                    {
                        new Button { Text = "Add", Command = new Command(OnAddParticipant), HorizontalOptions = LayoutOptions.FillAndExpand, Padding = new Thickness(8, 4) },
                        new Button { Text = "Clear", Command = new Command(OnClearParticipants), BackgroundColor = Color.FromArgb("#EF4444"), TextColor = Colors.White, Padding = new Thickness(8, 4) }
                    }
                },
                new Frame
                {
                    Padding = 4,
                    Content = _participantsView
                }
            }
        };

        // Add format-specific actions
        AddFormatSpecificActions(content, competition);

        // Save Button
        content.Children.Add(new Button
        {
            Text = "Save Changes",
            Command = new Command(OnSaveCompetition),
            Margin = new Thickness(0, 12, 0, 0),
            BackgroundColor = Color.FromArgb("#3B82F6"),
            TextColor = Colors.White,
            Padding = new Thickness(8, 6)
        });

        ContentPanel.Content = new ScrollView { Content = content };
    }

    private void AddFormatSpecificActions(VerticalStackLayout content, Competition competition)
    {
        if (competition.Format == CompetitionFormat.SinglesGroupStage || 
            competition.Format == CompetitionFormat.DoublesGroupStage)
        {
            AddGroupStageActions(content, competition);
        }
        else
        {
            AddKnockoutActions(content, competition);
        }
    }

    private void AddGroupStageActions(VerticalStackLayout content, Competition competition)
    {
        content.Children.Add(new Label { Text = "Group Stage Actions", FontSize = 16, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 12, 0, 4) });
        
        // Show group settings summary
        if (competition.GroupSettings != null)
        {
            var settingsSummary = new Frame
            {
                Padding = 10,
                BackgroundColor = Color.FromArgb("#F3F4F6"),
                Content = new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        new Label { Text = $"?? {competition.GroupSettings.NumberOfGroups} Groups", FontSize = 13 },
                        new Label { Text = $"?? Top {competition.GroupSettings.TopPlayersAdvance} advance to knockout", FontSize = 13 },
                        new Label { Text = $"?? Next {competition.GroupSettings.LowerPlayersToPlate} to plate", FontSize = 13 },
                        new Label { Text = $"?? Plate: {(competition.GroupSettings.CreatePlateCompetition ? "Yes" : "No")}", FontSize = 13 }
                    }
                }
            };
            content.Children.Add(settingsSummary);
        }

        // Generate Groups Button
        if (competition.Groups.Count == 0)
        {
            var generateGroupsBtn = new Button
            {
                Text = "Generate Groups",
                BackgroundColor = Color.FromArgb("#8B5CF6"),
                TextColor = Colors.White,
                Padding = new Thickness(8, 4),
                Margin = new Thickness(0, 8, 0, 0)
            };
            generateGroupsBtn.Clicked += (s, e) => OnGenerateGroups();
            content.Children.Add(generateGroupsBtn);
        }
        else
        {
            // View and Finalize buttons
            var viewGroupsBtn = new Button
            {
                Text = $"View Groups ({competition.Groups.Count})",
                BackgroundColor = Color.FromArgb("#6366F1"),
                TextColor = Colors.White,
                Padding = new Thickness(8, 4),
                Margin = new Thickness(0, 4, 0, 0)
            };
            viewGroupsBtn.Clicked += (s, e) => ShowGroupsView();
            content.Children.Add(viewGroupsBtn);

            var finalizeBtn = new Button
            {
                Text = "Finalize Groups & Create Knockouts",
                BackgroundColor = Color.FromArgb("#10B981"),
                TextColor = Colors.White,
                Padding = new Thickness(8, 4),
                Margin = new Thickness(0, 4, 0, 0)
            };
            finalizeBtn.Clicked += (s, e) => OnFinalizeGroups();
            content.Children.Add(finalizeBtn);
        }
    }

    private void AddKnockoutActions(VerticalStackLayout content, Competition competition)
    {
        content.Children.Add(new Label { Text = "Bracket", FontSize = 16, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 12, 0, 4) });
        content.Children.Add(new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Button { Text = "Generate", Command = new Command(OnGenerateBracket), HorizontalOptions = LayoutOptions.FillAndExpand, BackgroundColor = Color.FromArgb("#10B981"), TextColor = Colors.White, Padding = new Thickness(8, 4) },
                new Button { Text = "Random", Command = new Command(OnRandomDraw), HorizontalOptions = LayoutOptions.FillAndExpand, BackgroundColor = Color.FromArgb("#F59E0B"), TextColor = Colors.White, Padding = new Thickness(8, 4) },
                new Button { Text = "View", Command = new Command(OnViewBracket), HorizontalOptions = LayoutOptions.FillAndExpand, Padding = new Thickness(8, 4) }
            }
        });
    }

    private void OnSaveCompetition()
    {
        if (_selectedCompetition == null) return;

        _selectedCompetition.Name = _nameEntry?.Text ?? _selectedCompetition.Name;
        _selectedCompetition.Status = _statusPicker?.SelectedIndex >= 0 
            ? (CompetitionStatus)_statusPicker.SelectedIndex 
            : _selectedCompetition.Status;
        _selectedCompetition.StartDate = _startDatePicker?.Date;
        _selectedCompetition.Notes = _notesEntry?.Text;

        DataStore.Save();
        SetStatus("Competition saved");
    }

    private Grid CreateLabeledField(string label, View field)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(100) },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8
        };

        grid.Add(new Label
        {
            Text = label,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = FontAttributes.Bold,
            FontSize = 13
        }, 0, 0);

        grid.Add(field, 1, 0);

        return grid;
    }

    private void RefreshParticipants(Competition competition)
    {
        _participants.Clear();

        var format = competition.Format;

        if (format == CompetitionFormat.SinglesKnockout || format == CompetitionFormat.RoundRobin || 
            format == CompetitionFormat.Swiss || format == CompetitionFormat.SinglesGroupStage)
        {
            // Singles - use players
            foreach (var playerId in competition.ParticipantIds)
            {
                var player = DataStore.Data.Players.FirstOrDefault(p => p.Id == playerId);
                if (player != null)
                {
                    _participants.Add(new ParticipantItem
                    {
                        Id = player.Id,
                        Name = player.FullName
                    });
                }
            }
        }
        else if (format == CompetitionFormat.DoublesKnockout || format == CompetitionFormat.DoublesGroupStage)
        {
            // Doubles - use doubles teams
            foreach (var team in competition.DoublesTeams)
            {
                var p1 = DataStore.Data.Players.FirstOrDefault(p => p.Id == team.Player1Id);
                var p2 = DataStore.Data.Players.FirstOrDefault(p => p.Id == team.Player2Id);
                var name = $"{p1?.FullName ?? "?"} & {p2?.FullName ?? "?"}";
                
                _participants.Add(new ParticipantItem
                {
                    Id = team.Id,
                    Name = name
                });
            }
        }
        else if (format == CompetitionFormat.TeamKnockout)
        {
            // Team knockout - use teams
            foreach (var teamId in competition.ParticipantIds)
            {
                var team = DataStore.Data.Teams.FirstOrDefault(t => t.Id == teamId);
                if (team != null)
                {
                    _participants.Add(new ParticipantItem
                    {
                        Id = team.Id,
                        Name = team.Name ?? "Unnamed Team"
                    });
                }
            }
        }
    }

    private void OnRemoveParticipant(object? sender, EventArgs e)
    {
        if (_selectedCompetition == null || sender is not Button btn || btn.CommandParameter is not Guid id)
            return;

        _selectedCompetition.ParticipantIds.Remove(id);
        _selectedCompetition.DoublesTeams.RemoveAll(t => t.Id == id);
        RefreshParticipants(_selectedCompetition);
        SetStatus("Participant removed");
    }

    private async void OnClearParticipants()
    {
        if (_selectedCompetition == null) return;

        var confirm = await DisplayAlert("Clear Participants", 
            "Remove all participants from this competition?", "Yes", "No");
        
        if (!confirm) return;

        _selectedCompetition.ParticipantIds.Clear();
        _selectedCompetition.DoublesTeams.Clear();
        RefreshParticipants(_selectedCompetition);
        SetStatus("Participants cleared");
    }
}
