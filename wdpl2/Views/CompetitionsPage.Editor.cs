using System;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.ViewModels;

namespace Wdpl2.Views;

/// <summary>
/// Competition editor UI generation
/// </summary>
public partial class CompetitionsPage
{
    internal void ShowCompetitionEditor(Competition competition)
    {
        // Create editor ViewModel: competition CRUD goes to the ViewModel's store (SQLite via DI),
        // player/team lookups go to the page's _dataStore (JSON where that data lives).
        _editorViewModel = new CompetitionEditorViewModel(_viewModel.DataStore, _dataStore, competition, _currentSeasonId);

        _nameEntry = new Entry { Text = competition.Name };
        _statusPicker = new Picker
        {
            ItemsSource = Enum.GetNames(typeof(CompetitionStatus)).ToList(),
            SelectedIndex = (int)competition.Status
        };
        _startDatePicker = new DatePicker { Date = competition.StartDate ?? DateTime.Today };
        _notesEntry = new Entry { Text = competition.Notes ?? "", Placeholder = "Notes..." };

        var formatLabel = new Label
        {
            Text = competition.Format.ToString(),
            FontSize = 14,
            VerticalTextAlignment = TextAlignment.Center
        };

        // Participants list - bound to the editor ViewModel's collection
        _participantsView = new CollectionView
        {
            ItemsSource = _editorViewModel.Participants,
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

                return new Border
                {
                    Padding = 2,
                    Margin = new Thickness(0, 1),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
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
                new Border
                {
                    Padding = 4,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
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

        ContentPanel.Content = content;
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
        if (_editorViewModel == null) return;
        var settings = competition.GroupSettings ?? new GroupStageSettings();
        int participantCount = competition.Format == CompetitionFormat.DoublesGroupStage
            ? competition.DoublesTeams.Count
            : competition.ParticipantIds.Count;

        // ?? Header ???????????????????????????????????????????????????????
        content.Children.Add(new Label
        {
            Text = "Group Stage Setup",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 12, 0, 4)
        });

        // If groups already generated, show view/finalize only
        if (competition.Groups.Count > 0)
        {
            content.Children.Add(new Label { Text = $"? {competition.Groups.Count} groups generated", FontSize = 13, TextColor = Color.FromArgb("#10B981") });

            // Group action buttons
            var groupActionBar = new HorizontalStackLayout
            {
                Spacing = 6,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var viewGroupsBtn = new Button
            {
                Text = $"View Groups ({competition.Groups.Count})",
                BackgroundColor = Color.FromArgb("#6366F1"),
                TextColor = Colors.White,
                Padding = new Thickness(8, 4)
            };
            viewGroupsBtn.Clicked += (s, e) => ShowGroupsView();
            groupActionBar.Children.Add(viewGroupsBtn);

            // Only show randomise before KO rounds are created
            if (competition.Rounds.Count == 0)
            {
                var randomiseBtn = new Button
                {
                    Text = "?? Randomise",
                    BackgroundColor = Color.FromArgb("#F59E0B"),
                    TextColor = Colors.White,
                    Padding = new Thickness(8, 4)
                };
                randomiseBtn.Clicked += async (s, e) =>
                {
                    await _editorViewModel.RandomiseGroupsAsync();
                    await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);
                    SetStatus(_editorViewModel.StatusMessage);
                    if (_selectedCompetition != null)
                        ShowCompetitionEditor(_selectedCompetition);
                };
                groupActionBar.Children.Add(randomiseBtn);
            }

            content.Children.Add(groupActionBar);

            // Show how many have been selected so far
            int selectedCount = competition.Groups.Sum(g =>
                g.Standings.Count(s => s.Position > 0 && s.Position <= settings.TopPlayersAdvance));
            int targetCount = competition.Groups.Count * settings.TopPlayersAdvance;

            // Only show finalize options before KO rounds are created
            if (competition.Rounds.Count == 0)
            {
                int currentRound = competition.Groups.Max(g => g.GroupRound);
                string roundLabel = currentRound > 1 ? $" (Round {currentRound})" : "";

                content.Children.Add(new Label
                {
                    Text = $"Selected: {selectedCount}/{targetCount} winners{roundLabel}",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    Margin = new Thickness(0, 6, 0, 2)
                });

                var finalizeBar = new VerticalStackLayout { Spacing = 4, Margin = new Thickness(0, 2, 0, 0) };

                var koBtn = new Button
                {
                    Text = $"?? Create Knockout Bracket ({selectedCount} players)",
                    BackgroundColor = Color.FromArgb("#10B981"),
                    TextColor = Colors.White,
                    Padding = new Thickness(8, 4)
                };
                koBtn.Clicked += (s, e) => OnFinalizeGroups();
                finalizeBar.Children.Add(koBtn);

                var nextGroupBtn = new Button
                {
                    Text = "?? Another Round of Groups",
                    BackgroundColor = Color.FromArgb("#6366F1"),
                    TextColor = Colors.White,
                    Padding = new Thickness(8, 4)
                };
                nextGroupBtn.Clicked += (s, e) => OnAdvanceToNextGroupRound();
                finalizeBar.Children.Add(nextGroupBtn);

                content.Children.Add(finalizeBar);
            }

            // If knockout rounds have already been created from groups, show bracket controls
            if (competition.Rounds.Count > 0)
            {
                content.Children.Add(new Label { Text = "Knockout Stage", FontSize = 16, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 12, 0, 4) });
                content.Children.Add(new Label { Text = $"? {competition.Rounds.Count} knockout rounds created", FontSize = 13, TextColor = Color.FromArgb("#10B981") });

                var viewBracketBtn = new Button
                {
                    Text = "View Knockout Bracket",
                    BackgroundColor = Color.FromArgb("#6366F1"),
                    TextColor = Colors.White,
                    Padding = new Thickness(8, 4),
                    Margin = new Thickness(0, 4, 0, 0)
                };
                viewBracketBtn.Clicked += (s, e) => OnViewBracket();
                content.Children.Add(viewBracketBtn);
            }

            // Plate competition section — show after groups exist, regardless of KO status
            if (competition.PlateCompetitionId.HasValue)
            {
                content.Children.Add(new Border
                {
                    Padding = 10,
                    BackgroundColor = Color.FromArgb("#F0FDF4"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                    Stroke = Color.FromArgb("#10B981"),
                    Margin = new Thickness(0, 4, 0, 0),
                    Content = new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            new Label { Text = "\u2705 Plate competition created", FontSize = 13, VerticalTextAlignment = TextAlignment.Center },
                            new Button
                            {
                                Text = "Open Plate",
                                BackgroundColor = Color.FromArgb("#3B82F6"),
                                TextColor = Colors.White,
                                Padding = new Thickness(10, 4),
                                FontSize = 12,
                                Command = new Command(() => OnOpenLosersCup(null, EventArgs.Empty))
                            }
                        }
                    }
                });
            }
            else if (!competition.ParentCompetitionId.HasValue)
            {
                var createPlateBtn = new Button
                {
                    Text = "?? Create Plate Competition (losers)",
                    BackgroundColor = Color.FromArgb("#F59E0B"),
                    TextColor = Colors.White,
                    Padding = new Thickness(8, 4),
                    Margin = new Thickness(0, 4, 0, 0)
                };
                createPlateBtn.Clicked += async (s, e) =>
                {
                    await _editorViewModel.CreatePlateFromGroupsAsync();
                    await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);
                    SetStatus(_editorViewModel.StatusMessage);
                    if (_selectedCompetition != null)
                        ShowCompetitionEditor(_selectedCompetition);
                };
                content.Children.Add(createPlateBtn);
            }

            // Show previous group rounds if any
            if (competition.PreviousGroups.Count > 0)
            {
                var previousRounds = competition.PreviousGroups
                    .GroupBy(g => g.GroupRound)
                    .OrderBy(r => r.Key);

                foreach (var round in previousRounds)
                {
                    var roundNum = round.Key;
                    var viewPrevBtn = new Button
                    {
                        Text = $"?? View Round {roundNum} ({round.Count()} groups — completed)",
                        FontSize = 12,
                        TextColor = Colors.Gray,
                        BackgroundColor = Colors.Transparent,
                        Padding = new Thickness(0, 4),
                        Margin = new Thickness(0, 2, 0, 0),
                        HorizontalOptions = LayoutOptions.Start
                    };
                    viewPrevBtn.Clicked += (s, e) => ShowPreviousGroupRound(roundNum);
                    content.Children.Add(viewPrevBtn);
                }
            }

            return;
        }

        // ?? 1. Participants ??????????????????????????????????????????????
        var step1Color = participantCount >= 4 ? Color.FromArgb("#10B981") : Colors.Gray;
        content.Children.Add(new Label
        {
            Text = participantCount >= 4
                ? $"? {participantCount} participants added"
                : $"? Add participants first ({participantCount} added, need at least 4)",
            FontSize = 13,
            TextColor = step1Color
        });

        if (participantCount < 4) return;

        // ?? 2. Group Count ???????????????????????????????????????????????
        content.Children.Add(new Label
        {
            Text = "? Choose Number of Groups",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 10, 0, 4)
        });

        int topAdvance = settings.TopPlayersAdvance;
        int currentGroups = settings.NumberOfGroups > 0 ? settings.NumberOfGroups : 0;
        int maxGroups = Math.Max(participantCount / 2, 2);

        var groupCountPicker = new Picker
        {
            Title = "Number of Groups",
            FontSize = 14
        };
        for (int i = 1; i <= maxGroups; i++)
        {
            int perGroup = participantCount / i;
            int rem = participantCount % i;
            int koTotal = i * topAdvance;
            bool koValid = koTotal >= 2 && (koTotal & (koTotal - 1)) == 0;
            string label;
            if (i == 1)
                label = $"1 group ({participantCount} players)";
            else
            {
                label = $"{i} groups (~{perGroup}{(rem > 0 ? $"-{perGroup + 1}" : "")} per group) ? {koTotal} to KO";
                if (!koValid) label += " ??";
            }
            groupCountPicker.Items.Add(label);
        }
        if (currentGroups >= 1)
            groupCountPicker.SelectedIndex = Math.Min(currentGroups - 1, maxGroups - 1);
        groupCountPicker.SelectedIndexChanged += async (s, e) =>
        {
            int newCount = groupCountPicker.SelectedIndex + 1;
            await _editorViewModel.SaveGroupCountAsync(newCount);
            SetStatus(_editorViewModel.StatusMessage);
            // Refresh editor to update KO indicator
            ShowCompetitionEditor(competition);
        };
        content.Children.Add(CreateLabeledField("Groups:", groupCountPicker));

        // KO bracket validity
        if (currentGroups >= 1)
        {
            int koTotal = currentGroups * topAdvance;
            bool koValid = koTotal >= 2 && (koTotal & (koTotal - 1)) == 0;
            content.Children.Add(new Label
            {
                Text = koValid
                    ? $"? {currentGroups} groups × top {topAdvance} = {koTotal} to knockout"
                    : $"?? {currentGroups} groups × top {topAdvance} = {koTotal} — not a bracket size (need 2, 4, 8, 16…)",
                FontSize = 11,
                TextColor = koValid ? Color.FromArgb("#059669") : Color.FromArgb("#DC2626"),
                FontAttributes = koValid ? FontAttributes.None : FontAttributes.Italic,
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        // Settings summary
        string plateDesc = !settings.CreatePlateCompetition
            ? "No plate"
            : settings.AllLosersToPlate
                ? "All losers go to plate"
                : $"Next {settings.LowerPlayersToPlate} per group go to plate";

        content.Children.Add(new Border
        {
            Padding = 8,
            BackgroundColor = Color.FromArgb("#F3F4F6"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
            Margin = new Thickness(0, 6, 0, 0),
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label { Text = $"?? Top {settings.TopPlayersAdvance} from each group advance", FontSize = 12 },
                    new Label { Text = $"?? {plateDesc}", FontSize = 12 },
                    new Label { Text = "Players do their own draw within groups and report who got through", FontSize = 11, TextColor = Colors.Gray, FontAttributes = FontAttributes.Italic }
                }
            }
        });

        // ?? 3. Generate ??????????????????????????????????????????????????
        bool readyToGenerate = currentGroups >= 2 && participantCount >= 4;

        var generateGroupsBtn = new Button
        {
            Text = "?? Generate Groups",
            BackgroundColor = Color.FromArgb("#8B5CF6"),
            TextColor = Colors.White,
            Padding = new Thickness(12, 6),
            FontSize = 14,
            Margin = new Thickness(0, 10, 0, 0),
            IsEnabled = readyToGenerate
        };
        generateGroupsBtn.Clicked += (s, e) => OnGenerateGroups();
        content.Children.Add(generateGroupsBtn);
    }

    private void AddKnockoutActions(VerticalStackLayout content, Competition competition)
    {
        content.Children.Add(new Label { Text = "Bracket", FontSize = 16, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 12, 0, 4) });

        // Draw order indicator
        content.Children.Add(new Label
        {
            Text = competition.RandomDraw
                ? "\U0001F3B2 Draw: Random"
                : "\U0001F4CB Draw: Manual (order added)",
            FontSize = 12,
            TextColor = Colors.Gray,
            Margin = new Thickness(0, 0, 0, 4)
        });

        var primaryText = competition.RandomDraw ? "Generate (Random)" : "Generate (Ordered)";
        var secondaryText = competition.RandomDraw ? "Generate Ordered" : "Generate Random";
        Action primaryAction = competition.RandomDraw ? OnRandomDraw : OnGenerateBracket;
        Action secondaryAction = competition.RandomDraw ? OnGenerateBracket : OnRandomDraw;

        content.Children.Add(new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Button { Text = primaryText, Command = new Command(primaryAction), HorizontalOptions = LayoutOptions.FillAndExpand, BackgroundColor = Color.FromArgb("#10B981"), TextColor = Colors.White, Padding = new Thickness(8, 4) },
                new Button { Text = secondaryText, Command = new Command(secondaryAction), HorizontalOptions = LayoutOptions.FillAndExpand, BackgroundColor = Color.FromArgb("#F59E0B"), TextColor = Colors.White, Padding = new Thickness(8, 4) },
                new Button { Text = "Manual Draw", Command = new Command(OnManualDraw), HorizontalOptions = LayoutOptions.FillAndExpand, BackgroundColor = Color.FromArgb("#8B5CF6"), TextColor = Colors.White, Padding = new Thickness(8, 4) },
                new Button { Text = "View", Command = new Command(OnViewBracket), HorizontalOptions = LayoutOptions.FillAndExpand, Padding = new Thickness(8, 4) }
            }
        });

        // Losers Cup section — only for main competitions with a bracket (not plates)
        if (competition.Rounds.Count > 0 && !competition.ParentCompetitionId.HasValue)
        {
            content.Children.Add(new Label { Text = "Losers Cup", FontSize = 16, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 12, 0, 4) });

            if (competition.PlateCompetitionId.HasValue)
            {
                // Already created — show info and link
                content.Children.Add(new Border
                {
                    Padding = 10,
                    BackgroundColor = Color.FromArgb("#F0FDF4"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                    Stroke = Color.FromArgb("#10B981"),
                    Content = new HorizontalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            new Label { Text = "\u2705 Losers Cup created", FontSize = 13, VerticalTextAlignment = TextAlignment.Center },
                            new Button
                            {
                                Text = "Open Losers Cup",
                                BackgroundColor = Color.FromArgb("#3B82F6"),
                                TextColor = Colors.White,
                                Padding = new Thickness(10, 4),
                                FontSize = 12,
                                Command = new Command(() => OnOpenLosersCup(null, EventArgs.Empty))
                            }
                        }
                    }
                });
            }
            else
            {
                // Count first-round losers for the description
                var firstRound = competition.Rounds.FirstOrDefault();
                int completedMatches = firstRound?.Matches.Count(m => m.IsComplete && m.WinnerId.HasValue) ?? 0;
                int totalFirstRound = firstRound?.Matches.Count ?? 0;

                content.Children.Add(new Label
                {
                    Text = $"First-round results: {completedMatches}/{totalFirstRound} matches complete",
                    FontSize = 12,
                    TextColor = Colors.Gray,
                    Margin = new Thickness(0, 0, 0, 4)
                });

                var createBtn = new Button
                {
                    Text = "\U0001F3C6 Create Losers Cup",
                    BackgroundColor = Color.FromArgb("#EC4899"),
                    TextColor = Colors.White,
                    Padding = new Thickness(12, 6),
                    FontSize = 13,
                    IsEnabled = completedMatches >= 2
                };
                createBtn.Clicked += OnCreateLosersCup;
                content.Children.Add(createBtn);

                if (completedMatches < 2)
                {
                    content.Children.Add(new Label
                    {
                        Text = "Complete at least 2 first-round matches to create a Losers Cup",
                        FontSize = 11,
                        TextColor = Colors.Gray,
                        FontAttributes = FontAttributes.Italic
                    });
                }
            }
        }
    }

    private async void OnSaveCompetition()
    {
        if (_editorViewModel == null || _selectedCompetition == null) return;

        _editorViewModel.Name = _nameEntry?.Text ?? _editorViewModel.Name;
        _editorViewModel.Status = _statusPicker?.SelectedIndex >= 0 
            ? (CompetitionStatus)_statusPicker.SelectedIndex 
            : _editorViewModel.Status;
        _editorViewModel.StartDate = _startDatePicker?.Date ?? _editorViewModel.StartDate;
        _editorViewModel.Notes = _notesEntry?.Text ?? _editorViewModel.Notes;

        await _editorViewModel.SaveCommand.ExecuteAsync(null);
        SetStatus(_editorViewModel.StatusMessage);
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
        // Delegate to the editor ViewModel - participants are loaded in its constructor
        if (_editorViewModel != null)
        {
            _ = _editorViewModel.LoadParticipantsCommand.ExecuteAsync(null);
        }
    }

    private async void OnRemoveParticipant(object? sender, EventArgs e)
    {
        if (_editorViewModel == null || sender is not Button btn || btn.CommandParameter is not Guid id)
            return;

        await _editorViewModel.RemoveParticipantCommand.ExecuteAsync(id);
        SetStatus(_editorViewModel.StatusMessage);

        // Rebuild editor for group stage so step indicators update
        if (_selectedCompetition?.Format is CompetitionFormat.SinglesGroupStage or CompetitionFormat.DoublesGroupStage)
            ShowCompetitionEditor(_selectedCompetition);
    }

    private async void OnClearParticipants()
    {
        if (_editorViewModel == null || _selectedCompetition == null) return;

        var confirm = await DisplayAlert("Clear Participants", 
            "Remove all participants from this competition?", "Yes", "No");

        if (!confirm) return;

        await _editorViewModel.ClearParticipantsCommand.ExecuteAsync(null);
        SetStatus(_editorViewModel.StatusMessage);

        // Rebuild editor for group stage so step indicators update
        if (_selectedCompetition.Format is CompetitionFormat.SinglesGroupStage or CompetitionFormat.DoublesGroupStage)
            ShowCompetitionEditor(_selectedCompetition);
    }
}
