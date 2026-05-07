using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Wdpl2.Helpers;
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
        // Create editor ViewModel: competition CRUD uses SQLite via DI,
        // player/team/venue lookups read from DataStore.Data (JSON).
        _editorViewModel = new CompetitionEditorViewModel(_viewModel.DataStore, competition, _currentSeasonId);

        _nameEntry = new Entry { Text = competition.Name };
        _statusPicker = new Picker
        {
            ItemsSource = Enum.GetNames(typeof(CompetitionStatus)).ToList(),
            SelectedIndex = (int)competition.Status
        };
        _startDatePicker = new DatePicker { Date = competition.StartDate ?? DateTime.Today };
        _notesEntry = new Entry { Text = competition.Notes ?? "", Placeholder = "Notes..." };
        _lockSwitch = new Switch { IsToggled = competition.IsLocked };
        _showOnWebsiteSwitch = new Switch { IsToggled = competition.ShowOnWebsite };

        var formatLabel = new Label
        {
            Text = competition.Format.ToString(),
            FontSize = 14,
            VerticalTextAlignment = TextAlignment.Center
        };

        // Participants list - bound to the editor ViewModel's collection
        // Capture format in closure so the template can render doubles teams differently
        var participantFormat = competition.Format;
        var isDoublesFormat = participantFormat == CompetitionFormat.DoublesKnockout
                           || participantFormat == CompetitionFormat.DoublesGroupStage;

        _participantsView = new CollectionView
        {
            ItemsSource = _editorViewModel.Participants,
            ItemTemplate = new DataTemplate(() =>
            {
                var grid = new Grid
                {
                    Padding = new Thickness(8, 6),
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Auto }
                    }
                };

                // Uniform participant card: bold primary name + optional muted sub-row.
                // The sub-row carries doubles player names, the singles player's team name,
                // or remains hidden when there's nothing to show.
                var primaryLabel = new Label
                {
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#0F172A"),
                    VerticalTextAlignment = TextAlignment.Center
                };
                primaryLabel.SetBinding(Label.TextProperty, nameof(ParticipantItem.Name));

                var subLabel = new Label
                {
                    FontSize = 11,
                    TextColor = Color.FromArgb("#6B7280"),
                    LineBreakMode = LineBreakMode.TailTruncation,
                    IsVisible = false
                };

                var stack = new VerticalStackLayout
                {
                    Spacing = 2,
                    Children = { primaryLabel, subLabel }
                };

                // Resolve sub-row content based on format
                stack.BindingContextChanged += (s, e) =>
                {
                    if (stack.BindingContext is not ParticipantItem item || _selectedCompetition == null)
                    {
                        subLabel.IsVisible = false;
                        return;
                    }

                    var allPlayers = _dataStore.GetData()?.Players ?? new List<Player>();

                    if (isDoublesFormat)
                    {
                        var team = _selectedCompetition.DoublesTeams.FirstOrDefault(d => d.Id == item.Id);
                        if (team != null)
                        {
                            var p1 = allPlayers.FirstOrDefault(p => p.Id == team.Player1Id)?.FullName ?? "?";
                            var p2 = allPlayers.FirstOrDefault(p => p.Id == team.Player2Id)?.FullName ?? "?";
                            subLabel.Text = $"\U0001F465 {p1}  \u00B7  {p2}";
                            subLabel.IsVisible = true;
                            return;
                        }
                    }
                    else if (participantFormat == CompetitionFormat.TeamKnockout)
                    {
                        // Sub-row: number of players in the team
                        var playerCount = allPlayers.Count(p => p.TeamId == item.Id);
                        if (playerCount > 0)
                        {
                            subLabel.Text = $"\U0001F465 {playerCount} player{(playerCount == 1 ? "" : "s")}";
                            subLabel.IsVisible = true;
                            return;
                        }
                    }
                    else
                    {
                        // Singles: show the player's team name when available
                        var player = allPlayers.FirstOrDefault(p => p.Id == item.Id);
                        if (player?.TeamId is Guid teamId)
                        {
                            var teamName = _dataStore.GetData()?.Teams.FirstOrDefault(t => t.Id == teamId)?.Name;
                            if (!string.IsNullOrWhiteSpace(teamName))
                            {
                                subLabel.Text = $"\U0001F3AF {teamName}";
                                subLabel.IsVisible = true;
                                return;
                            }
                        }
                    }

                    subLabel.IsVisible = false;
                };

                View nameContent = stack;

                var removeBtn = new Button
                {
                    Text = "✕",
                    FontSize = 14,
                    Padding = new Thickness(6, 2),
                    WidthRequest = 30,
                    BackgroundColor = Color.FromArgb("#EF4444"),
                    TextColor = Colors.White,
                    VerticalOptions = LayoutOptions.Center
                };
                removeBtn.SetBinding(Button.CommandParameterProperty, nameof(ParticipantItem.Id));
                removeBtn.Clicked += OnRemoveParticipant;

                grid.Add(nameContent, 0, 0);
                grid.Add(removeBtn, 1, 0);

                return new Border
                {
                    Padding = 2,
                    Margin = new Thickness(0, 2),
                    BackgroundColor = Color.FromArgb("#F8FAFC"),
                    Stroke = Color.FromArgb("#E2E8F0"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                    Content = grid
                };
            })
        };

        // ── LEFT COLUMN: Details + Participants ──
        var leftColumn = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },  // details
                new RowDefinition { Height = GridLength.Auto },  // participants header + buttons
                new RowDefinition { Height = GridLength.Star },  // participants list (fills space)
                new RowDefinition { Height = GridLength.Auto }   // save button
            },
            RowSpacing = 8
        };

        // Details section
        var detailsSection = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = competition.Name ?? "Competition", FontSize = 16, FontAttributes = FontAttributes.Bold },
                CreateLabeledField("Name:", _nameEntry),
                CreateLabeledField("Format:", formatLabel),
                CreateLabeledField("Status:", _statusPicker),
                CreateLabeledField("Date:", _startDatePicker),
                CreateLabeledField("Notes:", _notesEntry),
                CreateLabeledField("\U0001F512 Lock:", _lockSwitch),
                CreateLabeledField("\U0001F310 Show on website:", _showOnWebsiteSwitch)
            }
        };
        leftColumn.Add(detailsSection, 0, 0);

        // Participants header + buttons
        var participantsHeader = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 6,
            Children =
            {
                new Label { Text = "Participants", FontSize = 14, FontAttributes = FontAttributes.Bold, VerticalTextAlignment = TextAlignment.Center }
            }
        };
        var addBtn = new Button { Text = "+ Add", Command = new Command(OnAddParticipant), Padding = new Thickness(8, 4), FontSize = 12 };
        var clearBtn = new Button { Text = "Clear", Command = new Command(OnClearParticipants), BackgroundColor = Color.FromArgb("#EF4444"), TextColor = Colors.White, Padding = new Thickness(8, 4), FontSize = 12 };
        Grid.SetColumn(addBtn, 1);
        Grid.SetColumn(clearBtn, 2);
        participantsHeader.Children.Add(addBtn);
        participantsHeader.Children.Add(clearBtn);
        leftColumn.Add(participantsHeader, 0, 1);

        // Participants list (fills remaining space)
        var participantsBorder = new Border
        {
            Padding = 4,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
            Stroke = Color.FromArgb("#E5E7EB"),
            Content = _participantsView
        };
        leftColumn.Add(participantsBorder, 0, 2);

        // Save button
        leftColumn.Add(new Button
        {
            Text = "💾 Save Changes",
            Command = new Command(OnSaveCompetition),
            BackgroundColor = Color.FromArgb("#3B82F6"),
            TextColor = Colors.White,
            Padding = new Thickness(8, 6)
        }, 0, 3);

        // ── RIGHT COLUMN: Format-specific setup ──
        var rightColumn = new VerticalStackLayout { Spacing = 8 };
        AddFormatSpecificActions(rightColumn, competition);

        // ── SPLIT LAYOUT ──
        var splitGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) }
            },
            ColumnSpacing = 16
        };

        // Wrap left in a border
        var leftBorder = new Border
        {
            Padding = 12,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Stroke = Color.FromArgb("#E5E7EB"),
            Content = leftColumn
        };
        splitGrid.Add(leftBorder, 0, 0);

        // Wrap right in a scrollable border
        var rightBorder = new Border
        {
            Padding = 12,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Stroke = Color.FromArgb("#E5E7EB"),
            Content = new ScrollView { Content = rightColumn }
        };
        splitGrid.Add(rightBorder, 1, 0);

        SetContentPanel(splitGrid);
    }

    private void AddFormatSpecificActions(VerticalStackLayout content, Competition competition)
    {
        // Always-visible competition-level Best Of default — shown for every format,
        // regardless of whether a bracket/groups have been generated. Per-round overrides
        // appear inline in the round schedule once rounds exist.
        content.Children.Add(CreateCompetitionBestOfPicker(competition));

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

    private View CreateCompetitionBestOfPicker(Competition competition)
    {
        // 0 = Unlimited; positive = best of N
        var options = new[] { ("Unlimited", 0), ("1", 1), ("3", 3), ("5", 5),
                              ("7", 7), ("9", 9), ("11", 11), ("15", 15) };

        var summary = new Label
        {
            FontSize = 11,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#64748B"),
            Margin = new Thickness(0, 4, 0, 0)
        };

        void RefreshSummary()
        {
            summary.Text = competition.BestOf > 0
                ? $"Default for all rounds: Best of {competition.BestOf} (rounds may override)"
                : "Default for all rounds: Unlimited (rounds may override)";
        }

        var chipRow = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Direction = Microsoft.Maui.Layouts.FlexDirection.Row
        };

        Border MakeChip(string text, int value)
        {
            bool selected = competition.BestOf == value;
            var lbl = new Label
            {
                Text = text,
                FontSize = 11,
                FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = selected ? Colors.White : Color.FromArgb("#0F172A"),
                Padding = new Thickness(8, 4),
                VerticalTextAlignment = TextAlignment.Center
            };
            var border = new Border
            {
                BackgroundColor = selected ? Color.FromArgb("#3B82F6") : Color.FromArgb("#F1F5F9"),
                Stroke = selected ? Color.FromArgb("#3B82F6") : Color.FromArgb("#E2E8F0"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 999 },
                Padding = 0,
                Margin = new Thickness(0, 0, 6, 6),
                Content = lbl
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                if (_editorViewModel == null) return;
                await _editorViewModel.SaveCompetitionBestOfAsync(value);
                SetStatus(_editorViewModel.StatusMessage);
                if (_selectedCompetition != null)
                    ShowCompetitionEditor(_selectedCompetition);
            };
            border.GestureRecognizers.Add(tap);
            return border;
        }

        foreach (var (text, value) in options)
            chipRow.Children.Add(MakeChip(text, value));

        RefreshSummary();

        var stack = new VerticalStackLayout { Spacing = 2, Margin = new Thickness(0, 0, 0, 8) };
        stack.Children.Add(new Label { Text = "🏆 Best of (default):", FontSize = 13, FontAttributes = FontAttributes.Bold });
        stack.Children.Add(chipRow);
        stack.Children.Add(summary);
        return stack;
    }

    /// <summary>
    /// Chip row picker to change the number of groups after generation.
    /// Tapping a chip regenerates the groups with the new count.
    /// Only shown before knockout rounds exist (gated at call site).
    /// </summary>
    private View CreateChangeGroupCountPicker(Competition competition, int participantCount)
    {
        int currentGroups = competition.GroupSettings?.NumberOfGroups ?? 0;
        int maxGroups = Math.Max(2, participantCount / 2);

        var chipRow = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Direction = Microsoft.Maui.Layouts.FlexDirection.Row
        };

        Border MakeChip(int value)
        {
            bool selected = currentGroups == value;
            var lbl = new Label
            {
                Text = value.ToString(),
                FontSize = 11,
                FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = selected ? Colors.White : Color.FromArgb("#0F172A"),
                Padding = new Thickness(8, 4),
                VerticalTextAlignment = TextAlignment.Center
            };
            var border = new Border
            {
                BackgroundColor = selected ? Color.FromArgb("#8B5CF6") : Color.FromArgb("#F1F5F9"),
                Stroke = selected ? Color.FromArgb("#8B5CF6") : Color.FromArgb("#E2E8F0"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 999 },
                Padding = 0,
                Margin = new Thickness(0, 0, 6, 6),
                Content = lbl
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                if (_editorViewModel == null) return;
                if (value == currentGroups) return;

                // Manual-draw groups (no participants assigned yet) can be resized silently —
                // there's no shuffle and nothing to lose.
                bool isManualDraw = competition.Groups.Count > 0
                    && competition.Groups.All(g => g.ParticipantIds.Count == 0);

                if (!isManualDraw)
                {
                    bool confirm = await DisplayAlert(
                        "Regenerate groups?",
                        $"This will reshuffle all participants into {value} groups. Any group standings/match results will be cleared. Continue?",
                        "Regenerate", "Cancel");
                    if (!confirm) return;
                }

                await _editorViewModel.ChangeGroupCountAndRegenerateAsync(value);
                SetStatus(_editorViewModel.StatusMessage);
                if (_selectedCompetition != null)
                    ShowCompetitionEditor(_selectedCompetition);
            };
            border.GestureRecognizers.Add(tap);
            return border;
        }

        for (int i = 1; i <= maxGroups; i++)
            chipRow.Children.Add(MakeChip(i));

        var stack = new VerticalStackLayout { Spacing = 2, Margin = new Thickness(0, 8, 0, 8) };
        stack.Children.Add(new Label
        {
            Text = "\U0001F522 Change number of groups:",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold
        });
        stack.Children.Add(chipRow);
        return stack;
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
            int currentGroupRound = competition.Groups.Max(g => g.GroupRound);
            content.Children.Add(new Label { Text = $"✅ {competition.Groups.Count} groups generated (Round {currentGroupRound})", FontSize = 13, TextColor = Color.FromArgb("#10B981") });

            // Show current round venue summary (date card now lives inline with the action buttons below)
            if (settings.SelectedVenues.Count > 0)
            {
                int venueTables = settings.SelectedVenues.Sum(v => v.TableCount);
                var venueDetails = string.Join(", ", settings.SelectedVenues.Select(v =>
                {
                    var tableNames = string.Join(", ", v.SelectedTables.Select(t => t.Label));
                    return $"{v.VenueName} ({tableNames})";
                }));
                content.Children.Add(new Label
                {
                    Text = $"\U0001F4CD {venueDetails} \u00B7 {venueTables} table(s)",
                    FontSize = 12,
                    TextColor = Colors.Gray,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            // Group action buttons
            var groupActionBar = new HorizontalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var viewGroupsBtn = new Button
            {
                Text = $"View Groups ({competition.Groups.Count})",
                BackgroundColor = Color.FromArgb("#6366F1"),
                TextColor = Colors.White,
                FontSize = 13,
                Padding = new Thickness(14, 8),
                MinimumHeightRequest = 38
            };
            viewGroupsBtn.Clicked += (s, e) => ShowGroupsView();
            groupActionBar.Children.Add(viewGroupsBtn);

            // Only show randomise before KO rounds are created
            if (competition.Rounds.Count == 0)
            {
                var randomiseBtn = new Button
                {
                    Text = "🔀 Randomise",
                    BackgroundColor = Color.FromArgb("#F59E0B"),
                    TextColor = Colors.White,
                    FontSize = 13,
                    Padding = new Thickness(14, 8),
                    MinimumHeightRequest = 38
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

                var drawBtn = new Button
                {
                    Text = "🎱 Draw",
                    BackgroundColor = Color.FromArgb("#8B5CF6"),
                    TextColor = Colors.White,
                    FontSize = 13,
                    Padding = new Thickness(14, 8),
                    MinimumHeightRequest = 38
                };
                drawBtn.Clicked += (s, e) => OnRandomiseWithDraw();
                groupActionBar.Children.Add(drawBtn);

                var clearGroupsBtn = new Button
                {
                    Text = "🗑 Clear Groups",
                    BackgroundColor = Color.FromArgb("#EF4444"),
                    TextColor = Colors.White,
                    FontSize = 13,
                    Padding = new Thickness(14, 8),
                    MinimumHeightRequest = 38
                };
                clearGroupsBtn.Clicked += async (s, e) =>
                {
                    if (_editorViewModel == null) return;

                    // Skip the warning if it's an empty manual draw — nothing to lose
                    bool isManualDraw = competition.Groups.All(g => g.ParticipantIds.Count == 0);
                    if (!isManualDraw)
                    {
                        bool confirm = await DisplayAlert(
                            "Clear all groups?",
                            "All participant assignments, group matches and standings will be removed. Previous (archived) group rounds are kept. This cannot be undone.",
                            "Clear", "Cancel");
                        if (!confirm) return;
                    }

                    await _editorViewModel.ClearAllGroupsAsync();
                    await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);
                    SetStatus(_editorViewModel.StatusMessage);
                    if (_selectedCompetition != null)
                        ShowCompetitionEditor(_selectedCompetition);
                };
                groupActionBar.Children.Add(clearGroupsBtn);
            }

            // Action buttons on the left, interactive date card on the right
            var actionRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 12,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            groupActionBar.VerticalOptions = LayoutOptions.Center;
            actionRow.Add(groupActionBar, 0, 0);

            // Always show the date card so the round date can be set/changed (singles or doubles).
            {
                var dateCard = CreateDateCard(settings.GroupDate ?? DateTime.Today, async newDate =>
                {
                    await _editorViewModel.SaveGroupDateAsync(newDate);
                    SetStatus(_editorViewModel.StatusMessage);
                    if (_selectedCompetition != null)
                        ShowCompetitionEditor(_selectedCompetition);
                });
                dateCard.HorizontalOptions = LayoutOptions.End;
                dateCard.VerticalOptions = LayoutOptions.Center;
                dateCard.Margin = new Thickness(0);
                actionRow.Add(dateCard, 1, 0);
            }

            content.Children.Add(actionRow);

            // Show how many have been selected so far
            int selectedCount = competition.Groups.Sum(g =>
                g.Standings.Count(s => s.Position > 0 && s.Position <= settings.TopPlayersAdvance));
            int targetCount = competition.Groups.Count * settings.TopPlayersAdvance;

            // Only show finalize options before KO rounds are created
            if (competition.Rounds.Count == 0)
            {
                // ── Change number of groups (regenerate) ────────────────
                content.Children.Add(CreateChangeGroupCountPicker(competition, participantCount));

                int currentRound = competition.Groups.Max(g => g.GroupRound);
                string roundLabel = currentRound > 1 ? $" (Round {currentRound})" : "";

                content.Children.Add(new Label
                {
                    Text = $"Selected: {selectedCount}/{targetCount} winners{roundLabel}",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    Margin = new Thickness(0, 6, 0, 2)
                });

                // ?? Next Round Settings: date & tables ??????????????
                content.Children.Add(new Label
                {
                    Text = "\U0001F3AF Next Round Settings",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    Margin = new Thickness(0, 10, 0, 4)
                });

                content.Children.Add(new Label
                {
                    Text = "Set the tables for the next round before advancing. (Tap the date card above to change the round date.)",
                    FontSize = 11,
                    TextColor = Colors.Gray,
                    FontAttributes = FontAttributes.Italic,
                    Margin = new Thickness(0, 0, 0, 4)
                });

                // Table selection for next round
                content.Children.Add(new Label
                {
                    Text = "\U0001F3B1 Tables",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    Margin = new Thickness(0, 6, 0, 2)
                });
                AddVenueSelectionUI(content, competition, settings);

                int nextRoundTables = settings.SelectedVenues.Sum(v => v.TableCount);
                if (nextRoundTables > 0)
                {
                    content.Children.Add(new Label
                    {
                        Text = $"✅ {settings.SelectedVenues.Count} venue(s), {nextRoundTables} table(s)",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#10B981"),
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                }

                // ?? Action buttons ??????????????????????????????????
                var finalizeBar = new VerticalStackLayout { Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };

                var koBtn = new Button
                {
                    Text = $"\U0001F3C6 Create Knockout Bracket ({selectedCount} players)",
                    BackgroundColor = Color.FromArgb("#10B981"),
                    TextColor = Colors.White,
                    Padding = new Thickness(8, 4)
                };
                koBtn.Clicked += (s, e) => OnFinalizeGroups();
                finalizeBar.Children.Add(koBtn);

                var nextGroupBtn = new Button
                {
                    Text = "\U0001F501 Another Round of Groups",
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
                content.Children.Add(new Label { Text = $"✅ {competition.Rounds.Count} knockout rounds created", FontSize = 13, TextColor = Color.FromArgb("#10B981") });

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

                // Round schedule � dates and tables per KO round
                AddRoundScheduleUI(content, competition);
            }

            // Plate competition section � show after groups exist, regardless of KO status
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
                    Text = "\U0001F3C5 Create Plate Competition (losers)",
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
                        Text = $"\U0001F4C2 View Round {roundNum} ({round.Count()} groups \u00B7 completed)",
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

        // ? 1. Participants ??????????????????????????????????????????????
        var step1Color = participantCount >= 4 ? Color.FromArgb("#10B981") : Colors.Gray;
        content.Children.Add(new Label
        {
            Text = participantCount >= 4
                ? $"✅ {participantCount} participants added"
                : $"⚠️ Add participants first ({participantCount} added, need at least 4)",
            FontSize = 13,
            TextColor = step1Color
        });

        if (participantCount < 4) return;

        // ? 2. Venue Selection ???????????????????????????????????????????
        content.Children.Add(new Label
        {
            Text = "\U0001F4CD Select Venues & Tables",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 10, 0, 4)
        });

        AddVenueSelectionUI(content, competition, settings);

        int totalTables = settings.SelectedVenues.Sum(v => v.TableCount);
        var venueStatus = totalTables > 0
            ? $"✅ {settings.SelectedVenues.Count} venue(s), {totalTables} table(s)"
            : "⚠️ Select at least one venue with tables";
        content.Children.Add(new Label
        {
            Text = venueStatus,
            FontSize = 13,
            TextColor = totalTables > 0 ? Color.FromArgb("#10B981") : Colors.Gray
        });

        // Show recommendation if venues selected
        if (totalTables > 0)
        {
            var (recommended, _, _, explanation) = _editorViewModel.GetGroupRecommendation();
            if (recommended > 0)
            {
                content.Children.Add(new Border
                {
                    Padding = 8,
                    BackgroundColor = Color.FromArgb("#F0F9FF"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                    Margin = new Thickness(0, 4, 0, 0),
                    Content = new Label
                    {
                        Text = $"\U0001F4A1 {explanation}",
                        FontSize = 11,
                        TextColor = Color.FromArgb("#1E40AF")
                    }
                });
            }
        }

        // Group date picker
        content.Children.Add(new Label
        {
            Text = "\U0001F4C5 Group Round Date",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 10, 0, 4)
        });

        content.Children.Add(CreateDateCard(settings.GroupDate ?? DateTime.Today, async newDate =>
        {
            await _editorViewModel.SaveGroupDateAsync(newDate);
            SetStatus(_editorViewModel.StatusMessage);
        }));

        // ? 3. Group Count ???????????????????????????????????????????????
        content.Children.Add(new Label
        {
            Text = "\U0001F522 Choose Number of Groups",
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
                if (!koValid) label += $" {Emojis.Warning}";
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
                    ? $"✅ {currentGroups} groups � top {topAdvance} = {koTotal} to knockout"
                    : $"⚠️ {currentGroups} groups � top {topAdvance} = {koTotal} � not a bracket size (need 2, 4, 8, 16�)",
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
                    new Label { Text = $"\U0001F3AF Top {settings.TopPlayersAdvance} from each group advance", FontSize = 12 },
                    new Label { Text = $"\U0001F3C5 {plateDesc}", FontSize = 12 },
                    new Label { Text = "Players do their own draw within groups and report who got through", FontSize = 11, TextColor = Colors.Gray, FontAttributes = FontAttributes.Italic }
                }
            }
        });

        // ➍ Generate ──────────────────────────────────────────────────────
        bool readyToGenerate = currentGroups >= 2 && participantCount >= 4 && totalTables > 0;

        // Show Draw toggle
        var showDrawSwitch = new Switch { IsToggled = false, OnColor = Color.FromArgb("#8B5CF6") };
        var showDrawRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8,
            Margin = new Thickness(0, 10, 0, 0),
            Children =
            {
                showDrawSwitch
            }
        };
        var drawLabel = new Label
        {
            Text = "🎱 Show animated draw",
            FontSize = 13,
            VerticalTextAlignment = TextAlignment.Center,
            TextColor = Colors.Gray
        };
        Grid.SetColumn(drawLabel, 1);
        showDrawRow.Children.Add(drawLabel);
        content.Children.Add(showDrawRow);

        var generateGroupsBtn = new Button
        {
            Text = showDrawSwitch.IsToggled ? "🎱 Draw Groups" : "⚙️ Generate Groups",
            BackgroundColor = Color.FromArgb("#8B5CF6"),
            TextColor = Colors.White,
            Padding = new Thickness(12, 6),
            FontSize = 14,
            Margin = new Thickness(0, 4, 0, 0),
            IsEnabled = readyToGenerate
        };
        showDrawSwitch.Toggled += (_, e) =>
        {
            generateGroupsBtn.Text = e.Value ? "🎱 Draw Groups" : "⚙️ Generate Groups";
        };
        generateGroupsBtn.Clicked += (s, e) =>
        {
            if (showDrawSwitch.IsToggled)
                OnGenerateGroupsWithDraw();
            else
                OnGenerateGroups();
        };
        content.Children.Add(generateGroupsBtn);

        // Manual draw — create empty groups, then user assigns each player by hand
        var manualGroupsBtn = new Button
        {
            Text = "✋ Manual Draw (assign players by hand)",
            BackgroundColor = Color.FromArgb("#0EA5E9"),
            TextColor = Colors.White,
            Padding = new Thickness(12, 6),
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 0),
            IsEnabled = currentGroups >= 2 && participantCount >= 4
        };
        manualGroupsBtn.Clicked += async (s, e) =>
        {
            if (_editorViewModel == null) return;
            await _editorViewModel.GenerateEmptyGroupsAsync();
            await _viewModel.LoadCompetitionsCommand.ExecuteAsync(null);
            SetStatus(_editorViewModel.StatusMessage);
            if (_selectedCompetition != null)
            {
                ShowCompetitionEditor(_selectedCompetition);
                ShowGroupsView();
            }
        };
        content.Children.Add(manualGroupsBtn);
    }

    private void AddKnockoutActions(VerticalStackLayout content, Competition competition)
    {
        content.Children.Add(new Label { Text = "Bracket", FontSize = 16, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 12, 0, 4) });

        // Pre-draw venue/table selection (before bracket is generated)
        if (competition.Rounds.Count == 0)
        {
            AddPreDrawVenueSelection(content, competition);
        }

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

        // Round schedule — dates and tables per KO round (after bracket exists)
        if (competition.Rounds.Count > 0)
            AddRoundScheduleUI(content, competition);

        // Losers Cup section � only for main competitions with a bracket (not plates)
        if (competition.Rounds.Count > 0 && !competition.ParentCompetitionId.HasValue)
        {
            content.Children.Add(new Label { Text = "Losers Cup", FontSize = 16, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 12, 0, 4) });

            if (competition.PlateCompetitionId.HasValue)
            {
                // Already created � show info and link
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

    /// <summary>
    /// Adds venue/table selection UI for knockout competitions BEFORE the draw is generated.
    /// Stores selections in GroupSettings.SelectedVenues so they can be applied to round 1 after generation.
    /// </summary>
    private void AddPreDrawVenueSelection(VerticalStackLayout content, Competition competition)
    {
        if (_editorViewModel == null) return;

        var settings = competition.GroupSettings ?? new GroupStageSettings();

        content.Children.Add(new Label
        {
            Text = "\U0001F3AF Select Venues & Tables",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 0, 0, 2)
        });
        content.Children.Add(new Label
        {
            Text = "Choose the tables available for round 1 before generating the draw.",
            FontSize = 11,
            TextColor = Colors.Gray,
            FontAttributes = FontAttributes.Italic,
            Margin = new Thickness(0, 0, 0, 4)
        });

        // Date card for round 1
        content.Children.Add(CreateDateCard(settings.GroupDate ?? DateTime.Today, async newDate =>
        {
            await _editorViewModel.SaveGroupDateAsync(newDate);
            SetStatus(_editorViewModel.StatusMessage);
            if (_selectedCompetition != null)
                ShowCompetitionEditor(_selectedCompetition);
        }));

        // Venue/table checkboxes (reuses existing mechanism)
        AddVenueSelectionUI(content, competition, settings);

        int totalTables = settings.SelectedVenues.Sum(v => v.TableCount);
        if (totalTables > 0)
        {
            content.Children.Add(new Label
            {
                Text = $"\u2705 {settings.SelectedVenues.Count} venue(s), {totalTables} table(s) selected",
                FontSize = 13,
                TextColor = Color.FromArgb("#10B981"),
                Margin = new Thickness(0, 2, 0, 6)
            });
        }
        else
        {
            content.Children.Add(new Label
            {
                Text = "\u26A0\uFE0F Optional: select tables to auto-assign to round 1",
                FontSize = 12,
                TextColor = Colors.Gray,
                FontAttributes = FontAttributes.Italic,
                Margin = new Thickness(0, 2, 0, 6)
            });
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
        _editorViewModel.IsLocked = _lockSwitch?.IsToggled ?? _editorViewModel.IsLocked;
        _editorViewModel.ShowOnWebsite = _showOnWebsiteSwitch?.IsToggled ?? _editorViewModel.ShowOnWebsite;

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

    /// <summary>
    /// Add date pickers and table selection for each KO round.
    /// For plate competitions, tables used by the parent comp on the same date are marked as unavailable.
    /// A container is added synchronously; its content is populated asynchronously.
    /// </summary>
    private void AddRoundScheduleUI(VerticalStackLayout content, Competition competition)
    {
        if (_editorViewModel == null || competition.Rounds.Count == 0) return;

        content.Children.Add(new Label
        {
            Text = "⚠️ Round Schedule & Tables",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 12, 0, 4)
        });

        // Add a container synchronously so callers don't interleave children
        var roundsContainer = new VerticalStackLayout { Spacing = 6 };
        content.Children.Add(roundsContainer);

        // Populate asynchronously
        _ = PopulateRoundScheduleAsync(roundsContainer, competition);
    }

    private async Task PopulateRoundScheduleAsync(VerticalStackLayout container, Competition competition)
    {
        if (_editorViewModel == null) return;

        var allVenues = await _editorViewModel.GetAvailableVenuesAsync();

        foreach (var round in competition.Rounds.OrderBy(r => r.RoundNumber))
        {
            var roundBorder = new Border
            {
                Padding = 10,
                Stroke = Color.FromArgb("#E5E7EB"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
                Margin = new Thickness(0, 4, 0, 0)
            };

            var roundStack = new VerticalStackLayout { Spacing = 6 };

            // Round name header
            var matchInfo = $"{round.Matches.Count} match(es)";
            roundStack.Children.Add(new Label
            {
                Text = $"{round.Name ?? $"Round {round.RoundNumber}"} � {matchInfo}",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold
            });

            // Interactive date card for this round
            var capturedRound = round;
            roundStack.Children.Add(CreateDateCard(round.Date ?? DateTime.Today, async newDate =>
            {
                await _editorViewModel.SaveRoundDetailsAsync(capturedRound.Id, newDate, null);
                SetStatus(_editorViewModel.StatusMessage);
                // Refresh to update plate restrictions
                if (_selectedCompetition != null)
                    ShowCompetitionEditor(_selectedCompetition);
            }));

            // Per-round "Best of" picker (overrides competition-level default)
            roundStack.Children.Add(CreateRoundBestOfPicker(competition, capturedRound));

            // Table selection for this round
            if (allVenues.Count > 0 && allVenues.Any(v => v.Tables.Count > 0))
            {
                // Get tables in use by other competitions on this date
                var restrictedTableIds = new HashSet<Guid>();
                var tableConflictSource = new Dictionary<Guid, string>();
                if (round.Date.HasValue)
                {
                    tableConflictSource = await _editorViewModel.GetTablesInUseByOtherCompsOnDateAsync(round.Date.Value);
                    restrictedTableIds = new HashSet<Guid>(tableConflictSource.Keys);
                }

                var selectedTableIds = new HashSet<Guid>(
                    round.SelectedVenues.SelectMany(v => v.SelectedTables).Select(t => t.TableId));

                var tablesLayout = new VerticalStackLayout { Spacing = 6 };

                int totalAvailableTables = 0;
                int totalRestrictedTables = 0;

                foreach (var venue in allVenues)
                {
                    if (venue.Tables.Count == 0) continue;

                    var venueGroup = new VerticalStackLayout { Spacing = 4 };
                    venueGroup.Children.Add(new Label
                    {
                        Text = venue.Name ?? "Unnamed Venue",
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#475569")
                    });

                    var chipRow = new FlexLayout
                    {
                        Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
                        Direction = Microsoft.Maui.Layouts.FlexDirection.Row
                    };

                    foreach (var table in venue.Tables)
                    {
                        bool isRestricted = restrictedTableIds.Contains(table.Id);
                        bool isSelected = selectedTableIds.Contains(table.Id) && !isRestricted;

                        if (isRestricted) totalRestrictedTables++;
                        else totalAvailableTables++;

                        var conflictName = isRestricted && tableConflictSource.TryGetValue(table.Id, out var cn) ? cn : null;
                        var capturedRound2 = round;

                        var chip = CreateTableChip(venue.Id, table.Id, table.Label, isSelected, isRestricted, conflictName,
                            onToggle: async () =>
                            {
                                await SaveRoundTableSelections(competition, capturedRound2, allVenues, tablesLayout, restrictedTableIds);
                            });
                        chip.Margin = new Thickness(0, 0, 6, 6);
                        chipRow.Children.Add(chip);
                    }

                    venueGroup.Children.Add(chipRow);
                    tablesLayout.Children.Add(venueGroup);
                }

                if (tablesLayout.Children.Count > 0)
                {
                    roundStack.Children.Add(new Label { Text = "Tables:", FontSize = 12, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 2, 0, 0) });
                    roundStack.Children.Add(tablesLayout);

                    // Warning if all tables are restricted (conflict with other competitions)
                    if (totalAvailableTables == 0 && totalRestrictedTables > 0)
                    {
                        roundStack.Children.Add(new Border
                        {
                            Padding = 8,
                            BackgroundColor = Color.FromArgb("#FEF2F2"),
                            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                            Stroke = Color.FromArgb("#FECACA"),
                            Content = new Label
                            {
                                Text = "⚠️ All tables are in use by other competitions on this date. Choose a different date or add more tables.",
                                FontSize = 11,
                                TextColor = Color.FromArgb("#DC2626")
                            }
                        });
                    }
                    else if (totalRestrictedTables > 0)
                    {
                        roundStack.Children.Add(new Label
                        {
                            Text = $"⚠️ {totalRestrictedTables} table(s) in use by other competitions on this date",
                            FontSize = 11,
                            TextColor = Color.FromArgb("#D97706"),
                            FontAttributes = FontAttributes.Italic
                        });
                    }

                    if (round.TotalTables > 0)
                    {
                        roundStack.Children.Add(new Label
                        {
                            Text = $"✅ {round.TotalTables} table(s) selected",
                            FontSize = 11,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#10B981"),
                            Margin = new Thickness(0, 4, 0, 0)
                        });
                        roundStack.Children.Add(BuildSelectionSummaryChips(round.SelectedVenues));

                        var capturedRoundForShuffle = round;
                        var randomiseBtn = new Button
                        {
                            Text = "🎲 Randomise Venues",
                            FontSize = 12,
                            Padding = new Thickness(10, 4),
                            BackgroundColor = Color.FromArgb("#6366F1"),
                            TextColor = Colors.White,
                            HorizontalOptions = LayoutOptions.Start,
                            Margin = new Thickness(0, 4, 0, 0)
                        };
                        randomiseBtn.Clicked += async (s, e) =>
                        {
                            await _editorViewModel!.RandomiseVenueAssignmentsAsync(capturedRoundForShuffle.Id);
                            SetStatus(_editorViewModel.StatusMessage);
                            if (_selectedCompetition != null)
                                ShowCompetitionEditor(_selectedCompetition);
                        };
                        roundStack.Children.Add(randomiseBtn);
                    }
                }
            }

            roundBorder.Content = roundStack;
            container.Children.Add(roundBorder);
        }
    }

    /// <summary>
    /// Read round table checkbox state and persist.
    /// </summary>
    private async Task SaveRoundTableSelections(Competition competition, CompetitionRound round,
        List<Venue> allVenues, VerticalStackLayout tablesLayout, HashSet<Guid> restrictedTableIds)
    {
        if (_editorViewModel == null) return;

        var tableById = allVenues.SelectMany(v => v.Tables.Select(t => (v, t)))
            .ToDictionary(x => x.t.Id, x => (Venue: x.v, Table: x.t));

        var venueSelections = new Dictionary<Guid, List<SelectedTable>>();

        foreach (var (venueId, tableId) in CollectSelectedChips(tablesLayout))
        {
            if (restrictedTableIds.Contains(tableId)) continue;
            if (!venueSelections.ContainsKey(venueId))
                venueSelections[venueId] = new List<SelectedTable>();

            var label = tableById.TryGetValue(tableId, out var info) ? info.Table.Label : "";
            venueSelections[venueId].Add(new SelectedTable { TableId = tableId, Label = label });
        }

        var venueById = allVenues.ToDictionary(v => v.Id);

        var venues = new List<CompetitionVenue>();
        foreach (var kvp in venueSelections)
        {
            var venueName = venueById.TryGetValue(kvp.Key, out var v) ? (v.Name ?? "") : "";
            venues.Add(new CompetitionVenue
            {
                VenueId = kvp.Key,
                VenueName = venueName,
                SelectedTables = kvp.Value
            });
        }

        await _editorViewModel.SaveRoundDetailsAsync(round.Id, null, venues);
        SetStatus(_editorViewModel.StatusMessage);
    }

    /// <summary>
    /// Build the venue selection UI � shows each venue's named tables as individual checkboxes.
    /// Adds a container synchronously; populates it asynchronously.
    /// </summary>
    private void AddVenueSelectionUI(VerticalStackLayout content, Competition competition, GroupStageSettings settings)
    {
        var venueContainer = new VerticalStackLayout { Spacing = 4 };
        content.Children.Add(venueContainer);

        _ = PopulateVenueSelectionAsync(venueContainer, competition, settings);
    }

    private async Task PopulateVenueSelectionAsync(VerticalStackLayout container, Competition competition, GroupStageSettings settings)
    {
        if (_editorViewModel == null) return;

        var venues = await _editorViewModel.GetAvailableVenuesAsync();
        if (venues.Count == 0)
        {
            container.Children.Add(new Label
            {
                Text = "No venues found for this season. Add venues first.",
                FontSize = 12,
                TextColor = Color.FromArgb("#DC2626"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }

        // Check tables in use by OTHER competitions on the same date
        var restrictedTableIds = new HashSet<Guid>();
        var tableConflictSource = new Dictionary<Guid, string>(); // tableId → comp name
        if (settings.GroupDate.HasValue)
        {
            tableConflictSource = await _editorViewModel.GetTablesInUseByOtherCompsOnDateAsync(settings.GroupDate.Value);
            restrictedTableIds = new HashSet<Guid>(tableConflictSource.Keys);
        }

        // Build a quick lookup of already-selected table IDs
        var selectedTableIds = new HashSet<Guid>(
            settings.SelectedVenues.SelectMany(v => v.SelectedTables).Select(t => t.TableId));

        var venuesLayout = new VerticalStackLayout { Spacing = 10 };

        foreach (var venue in venues)
        {
            if (venue.Tables.Count == 0) continue; // skip venues with no tables defined

            var venueSection = new VerticalStackLayout { Spacing = 4 };

            // Venue name header
            venueSection.Children.Add(new Label
            {
                Text = venue.Name ?? "Unnamed Venue",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#475569")
            });

            var chipRow = new FlexLayout
            {
                Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
                Direction = Microsoft.Maui.Layouts.FlexDirection.Row
            };

            foreach (var table in venue.Tables)
            {
                bool isRestricted = restrictedTableIds.Contains(table.Id);
                bool isSelected = selectedTableIds.Contains(table.Id) && !isRestricted;

                var conflictName = isRestricted && tableConflictSource.TryGetValue(table.Id, out var cn) ? cn : null;

                var chip = CreateTableChip(venue.Id, table.Id, table.Label, isSelected, isRestricted, conflictName,
                    onToggle: async () =>
                    {
                        await SaveVenueSelections(competition, venues, venuesLayout);
                    });
                chip.Margin = new Thickness(0, 0, 6, 6);
                chipRow.Children.Add(chip);
            }

            venueSection.Children.Add(chipRow);
            venuesLayout.Children.Add(venueSection);
        }

        // Check if no venues had tables
        if (venuesLayout.Children.Count == 0)
        {
            container.Children.Add(new Label
            {
                Text = "No tables defined at any venue. Add tables to your venues first.",
                FontSize = 12,
                TextColor = Color.FromArgb("#DC2626"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }

        var venuesBorder = new Border
        {
            Padding = 8,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
            Stroke = Color.FromArgb("#E5E7EB"),
            Content = venuesLayout
        };

        container.Children.Add(venuesBorder);

        // Warning for table conflicts with other competitions
        if (restrictedTableIds.Count > 0)
        {
            int totalTables = venues.Sum(v => v.Tables.Count);
            if (restrictedTableIds.Count >= totalTables)
            {
                container.Children.Add(new Border
                {
                    Padding = 8,
                    BackgroundColor = Color.FromArgb("#FEF2F2"),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                    Stroke = Color.FromArgb("#FECACA"),
                    Margin = new Thickness(0, 4, 0, 0),
                    Content = new Label
                    {
                        Text = "⚠️ All tables are in use by other competitions on this date. Choose a different date or add more tables.",
                        FontSize = 11,
                        TextColor = Color.FromArgb("#DC2626")
                    }
                });
            }
            else
            {
                container.Children.Add(new Label
                {
                    Text = $"⚠️ {restrictedTableIds.Count} table(s) in use by other competitions on this date",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#D97706"),
                    FontAttributes = FontAttributes.Italic,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }
        }
    }

    /// <summary>
    /// Read the current table checkbox state from the UI and persist.
    /// </summary>
    private async Task SaveVenueSelections(Competition competition, List<Venue> allVenues, VerticalStackLayout venuesLayout)
    {
        if (_editorViewModel == null) return;

        var venueById = allVenues.ToDictionary(v => v.Id);
        var tableById = allVenues.SelectMany(v => v.Tables.Select(t => (v, t)))
            .ToDictionary(x => x.t.Id, x => (Venue: x.v, Table: x.t));

        // Gather selected tables grouped by venue
        var venueSelections = new Dictionary<Guid, List<SelectedTable>>();

        foreach (var (venueId, tableId) in CollectSelectedChips(venuesLayout))
        {
            if (!venueSelections.ContainsKey(venueId))
                venueSelections[venueId] = new List<SelectedTable>();

            var label = tableById.TryGetValue(tableId, out var info) ? info.Table.Label : "";
            venueSelections[venueId].Add(new SelectedTable { TableId = tableId, Label = label });
        }

        // Build the final list
        var selected = new List<CompetitionVenue>();
        foreach (var kvp in venueSelections)
        {
            var venueName = venueById.TryGetValue(kvp.Key, out var v) ? (v.Name ?? "") : "";
            selected.Add(new CompetitionVenue
            {
                VenueId = kvp.Key,
                VenueName = venueName,
                SelectedTables = kvp.Value
            });
        }

        await _editorViewModel.SaveSelectedVenuesAsync(selected);
        SetStatus(_editorViewModel.StatusMessage);
    }

    // ── Chip-based table selection helpers ────────────────────────────────────────────
    // A chip is a Border wrapping a Label. State is encoded via StyleId:
    //   "selected"   → user has chosen this table
    //   "unselected" → available but not chosen
    //   "restricted" → in use by another comp on the same date (read-only)
    // AutomationId on every chip is "venueId|tableId" so save methods can read back.

    private static readonly Color ChipSelectedBg     = Color.FromArgb("#3B82F6");
    private static readonly Color ChipSelectedStroke = Color.FromArgb("#2563EB");
    private static readonly Color ChipSelectedText   = Colors.White;
    private static readonly Color ChipUnselectedBg   = Color.FromArgb("#F1F5F9");
    private static readonly Color ChipUnselectedStroke = Color.FromArgb("#CBD5E1");
    private static readonly Color ChipUnselectedText = Color.FromArgb("#0F172A");
    private static readonly Color ChipRestrictedBg   = Color.FromArgb("#F3F4F6");
    private static readonly Color ChipRestrictedStroke = Color.FromArgb("#E5E7EB");
    private static readonly Color ChipRestrictedText = Color.FromArgb("#9CA3AF");

    /// <summary>
    /// Builds a fixtures-sheet-style date card: a small rounded "tile" showing the
    /// day-of-week, large day number, uppercase month abbreviation and year.
    /// Visual style mirrors the .wk-card pattern used in FixturesSheetGenerator.cs.
    /// When <paramref name="onChanged"/> is supplied, the card becomes a date picker:
    /// tapping the card opens the OS DatePicker; selecting a new date invokes the callback.
    /// A subtle 3D tilt (lift + scale + shadow) is applied on hover/tap to mirror the
    /// CSS .wk-card hover effect.
    /// </summary>
    private static Border CreateDateCard(DateTime date, Action<DateTime>? onChanged = null)
    {
        // Colours sourced from .wk-card in FixturesSheetGenerator.cs (metallic silver gradient, dark text).
        var dow = new Label
        {
            Text = date.ToString("ddd").ToUpperInvariant(),
            FontSize = 9,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#555555"),
            HorizontalTextAlignment = TextAlignment.Center,
            CharacterSpacing = 2.5
        };

        var day = new Label
        {
            Text = date.Day.ToString("00"),
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1A1A1A"),
            HorizontalTextAlignment = TextAlignment.Center,
            LineHeight = 1
        };

        var month = new Label
        {
            Text = date.ToString("MMM").ToUpperInvariant(),
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#555555"),
            HorizontalTextAlignment = TextAlignment.Center,
            CharacterSpacing = 2.5
        };

        var year = new Label
        {
            Text = date.Year.ToString(),
            FontSize = 9,
            TextColor = Color.FromArgb("#888888"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        var stack = new VerticalStackLayout
        {
            Spacing = 0,
            HorizontalOptions = LayoutOptions.Center,
            Children = { dow, day, month, year }
        };

        // Approximate 170deg silver gradient (#F8F8F8 → #E8E8E8 → #F4F4F4 → #D0D0D0 → #E0E0E0 → #C0C0C0)
        var cardGradient = new LinearGradientBrush
        {
            StartPoint = new Point(0.1, 0),
            EndPoint = new Point(0.9, 1),
            GradientStops =
            {
                new GradientStop(Color.FromArgb("#F8F8F8"), 0.0f),
                new GradientStop(Color.FromArgb("#E8E8E8"), 0.2f),
                new GradientStop(Color.FromArgb("#F4F4F4"), 0.4f),
                new GradientStop(Color.FromArgb("#D0D0D0"), 0.6f),
                new GradientStop(Color.FromArgb("#E0E0E0"), 0.8f),
                new GradientStop(Color.FromArgb("#C0C0C0"), 1.0f)
            }
        };

        var border = new Border
        {
            Padding = new Thickness(12, 8),
            Background = cardGradient,
            Stroke = Color.FromArgb("#99FFFFFF"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 4, 0, 4),
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Color.FromArgb("#38000000")),
                Offset = new Point(0, 6),
                Radius = 12,
                Opacity = 1f
            }
        };

        if (onChanged != null)
        {
            // Embed an (almost) invisible DatePicker behind the card so a tap can open the OS picker.
            var picker = new DatePicker
            {
                Date = date,
                Opacity = 0,
                InputTransparent = false,
                BackgroundColor = Colors.Transparent,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
            picker.DateSelected += (s, e) => onChanged(e.NewDate);

            var grid = new Grid();
            grid.Children.Add(stack);
            grid.Children.Add(picker);
            border.Content = grid;

            // Tap anywhere on the card → focus the picker (opens calendar on Windows).
            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) => picker.Focus();
            border.GestureRecognizers.Add(tap);
        }
        else
        {
            border.Content = stack;
        }

        // 3D tilt: lift + scale on pointer hover (desktop) — mirrors .wk-card:hover.
        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += async (s, e) =>
        {
            await Task.WhenAll(
                border.TranslateTo(0, -3, 150, Easing.CubicOut),
                border.ScaleTo(1.03, 150, Easing.CubicOut));
        };
        pointer.PointerExited += async (s, e) =>
        {
            await Task.WhenAll(
                border.TranslateTo(0, 0, 150, Easing.CubicOut),
                border.ScaleTo(1.0, 150, Easing.CubicOut));
        };
        border.GestureRecognizers.Add(pointer);

        return border;
    }

    private View CreateRoundBestOfPicker(Competition competition, CompetitionRound round)
    {
        // Common Best Of choices: 1, 3, 5, 7, 9, 11, 15. "Default" = inherit competition value.
        var options = new[] { ("Default", (int?)null), ("1", (int?)1), ("3", (int?)3), ("5", (int?)5),
                              ("7", (int?)7), ("9", (int?)9), ("11", (int?)11), ("15", (int?)15) };

        var label = new Label
        {
            FontSize = 11,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#64748B"),
            Margin = new Thickness(0, 4, 0, 0)
        };

        void RefreshSummary()
        {
            var compDefault = competition.BestOf > 0 ? $"Best of {competition.BestOf}" : "unlimited";
            if (round.BestOf.HasValue)
                label.Text = round.BestOf.Value > 0
                    ? $"This round: Best of {round.BestOf.Value}"
                    : "This round: Unlimited";
            else
                label.Text = $"This round: Default ({compDefault})";
        }

        var chipRow = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Direction = Microsoft.Maui.Layouts.FlexDirection.Row
        };

        Border MakeChip(string text, int? value)
        {
            bool selected = round.BestOf == value || (!round.BestOf.HasValue && value == null);
            var lbl = new Label
            {
                Text = text,
                FontSize = 11,
                FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = selected ? Colors.White : Color.FromArgb("#0F172A"),
                Padding = new Thickness(8, 4),
                VerticalTextAlignment = TextAlignment.Center
            };
            var border = new Border
            {
                BackgroundColor = selected ? Color.FromArgb("#3B82F6") : Color.FromArgb("#F1F5F9"),
                Stroke = selected ? Color.FromArgb("#3B82F6") : Color.FromArgb("#E2E8F0"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 999 },
                Padding = 0,
                Margin = new Thickness(0, 0, 6, 6),
                Content = lbl
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                if (_editorViewModel == null) return;
                await _editorViewModel.SaveRoundDetailsAsync(round.Id, null, null,
                    bestOf: value, clearBestOf: value == null);
                SetStatus(_editorViewModel.StatusMessage);
                if (_selectedCompetition != null)
                    ShowCompetitionEditor(_selectedCompetition);
            };
            border.GestureRecognizers.Add(tap);
            return border;
        }

        foreach (var (text, value) in options)
            chipRow.Children.Add(MakeChip(text, value));

        RefreshSummary();

        var stack = new VerticalStackLayout { Spacing = 2, Margin = new Thickness(0, 6, 0, 0) };
        stack.Children.Add(new Label { Text = "🏆 Best of:", FontSize = 12, FontAttributes = FontAttributes.Bold });
        stack.Children.Add(chipRow);
        stack.Children.Add(label);
        return stack;
    }

    private static Border CreateTableChip(Guid venueId, Guid tableId, string label,
        bool isSelected, bool isRestricted, string? conflictName, Action? onToggle)
    {
        var displayText = string.IsNullOrWhiteSpace(label) ? "Unnamed" : label;
        if (isRestricted && !string.IsNullOrEmpty(conflictName))
            displayText += $"  •  {conflictName}";

        var chipLabel = new Label
        {
            Text = displayText,
            FontSize = 12,
            FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None,
            TextColor = isRestricted ? ChipRestrictedText
                       : isSelected ? ChipSelectedText
                       : ChipUnselectedText,
            VerticalTextAlignment = TextAlignment.Center
        };

        var chip = new Border
        {
            AutomationId = $"{venueId}|{tableId}",
            StyleId = isRestricted ? "restricted" : (isSelected ? "selected" : "unselected"),
            Padding = new Thickness(10, 4),
            BackgroundColor = isRestricted ? ChipRestrictedBg
                            : isSelected ? ChipSelectedBg
                            : ChipUnselectedBg,
            Stroke = isRestricted ? ChipRestrictedStroke
                   : isSelected ? ChipSelectedStroke
                   : ChipUnselectedStroke,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 999 },
            Content = chipLabel
        };

        if (!isRestricted && onToggle != null)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) =>
            {
                bool nowSelected = chip.StyleId != "selected";
                chip.StyleId = nowSelected ? "selected" : "unselected";
                chip.BackgroundColor = nowSelected ? ChipSelectedBg : ChipUnselectedBg;
                chip.Stroke = nowSelected ? ChipSelectedStroke : ChipUnselectedStroke;
                chipLabel.TextColor = nowSelected ? ChipSelectedText : ChipUnselectedText;
                chipLabel.FontAttributes = nowSelected ? FontAttributes.Bold : FontAttributes.None;
                onToggle();
            };
            chip.GestureRecognizers.Add(tap);
        }

        return chip;
    }

    /// <summary>
    /// Recursively walk a layout collecting all chip Borders whose StyleId == "selected".
    /// Returns parsed (venueId, tableId) pairs.
    /// </summary>
    private static IEnumerable<(Guid venueId, Guid tableId)> CollectSelectedChips(Microsoft.Maui.IView root)
    {
        if (root is Border b && b.StyleId == "selected" && !string.IsNullOrEmpty(b.AutomationId))
        {
            var parts = b.AutomationId.Split('|');
            if (parts.Length == 2 && Guid.TryParse(parts[0], out var v) && Guid.TryParse(parts[1], out var t))
                yield return (v, t);
        }

        if (root is Microsoft.Maui.ILayout layout)
        {
            foreach (var child in layout)
                foreach (var hit in CollectSelectedChips(child))
                    yield return hit;
        }
        else if (root is IContentView cv && cv.Content is Microsoft.Maui.IView inner)
        {
            foreach (var hit in CollectSelectedChips(inner))
                yield return hit;
        }
    }

    /// <summary>
    /// Build a wrapping FlexLayout summary of currently-selected venue/table chips for display only.
    /// </summary>
    private static FlexLayout BuildSelectionSummaryChips(IEnumerable<CompetitionVenue> selectedVenues)
    {
        var summary = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Direction = Microsoft.Maui.Layouts.FlexDirection.Row,
            Margin = new Thickness(0, 4, 0, 0)
        };

        foreach (var v in selectedVenues)
        {
            foreach (var t in v.SelectedTables)
            {
                var chip = new Border
                {
                    Padding = new Thickness(8, 3),
                    Margin = new Thickness(0, 0, 6, 6),
                    BackgroundColor = Color.FromArgb("#ECFDF5"),
                    Stroke = Color.FromArgb("#A7F3D0"),
                    StrokeThickness = 1,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 999 },
                    Content = new Label
                    {
                        Text = $"✓ {v.VenueName} · {t.Label}",
                        FontSize = 11,
                        TextColor = Color.FromArgb("#065F46")
                    }
                };
                summary.Children.Add(chip);
            }
        }

        return summary;
    }
}
