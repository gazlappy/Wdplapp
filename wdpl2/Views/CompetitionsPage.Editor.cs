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
                    Text = "✕",
                    FontSize = 14,
                    Padding = new Thickness(6, 2),
                    WidthRequest = 30,
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
                CreateLabeledField("Notes:", _notesEntry)
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
            int currentGroupRound = competition.Groups.Max(g => g.GroupRound);
            content.Children.Add(new Label { Text = $"✅ {competition.Groups.Count} groups generated (Round {currentGroupRound})", FontSize = 13, TextColor = Color.FromArgb("#10B981") });

            // Show current round date and venue summary
            if (settings.GroupDate.HasValue)
            {
                content.Children.Add(new Label
                {
                    Text = $"⚠️ {settings.GroupDate.Value:dd MMM yyyy}",
                    FontSize = 12,
                    TextColor = Colors.Gray,
                    Margin = new Thickness(0, 0, 0, 2)
                });
            }
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
                    Text = $"⚠️ {venueDetails} � {venueTables} table(s)",
                    FontSize = 12,
                    TextColor = Colors.Gray,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

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
                    Text = "🔀 Randomise",
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

                var drawBtn = new Button
                {
                    Text = "🎱 Draw",
                    BackgroundColor = Color.FromArgb("#8B5CF6"),
                    TextColor = Colors.White,
                    Padding = new Thickness(8, 4)
                };
                drawBtn.Clicked += (s, e) => OnRandomiseWithDraw();
                groupActionBar.Children.Add(drawBtn);
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

                // ?? Next Round Settings: date & tables ??????????????
                content.Children.Add(new Label
                {
                    Text = "⚠️ Next Round Settings",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    Margin = new Thickness(0, 10, 0, 4)
                });

                content.Children.Add(new Label
                {
                    Text = "Set the date and tables for the next round before advancing.",
                    FontSize = 11,
                    TextColor = Colors.Gray,
                    FontAttributes = FontAttributes.Italic,
                    Margin = new Thickness(0, 0, 0, 4)
                });

                // Date picker for next round
                var nextDatePicker = new DatePicker { Date = settings.GroupDate ?? DateTime.Today, FontSize = 13 };
                nextDatePicker.DateSelected += async (s, e) =>
                {
                    await _editorViewModel.SaveGroupDateAsync(e.NewDate);
                    SetStatus(_editorViewModel.StatusMessage);
                    if (_selectedCompetition != null)
                        ShowCompetitionEditor(_selectedCompetition);
                };
                content.Children.Add(CreateLabeledField("Date:", nextDatePicker));

                // Table selection for next round
                content.Children.Add(new Label
                {
                    Text = "⚠️ Tables",
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
                    Text = $"⚠️ Create Knockout Bracket ({selectedCount} players)",
                    BackgroundColor = Color.FromArgb("#10B981"),
                    TextColor = Colors.White,
                    Padding = new Thickness(8, 4)
                };
                koBtn.Clicked += (s, e) => OnFinalizeGroups();
                finalizeBar.Children.Add(koBtn);

                var nextGroupBtn = new Button
                {
                    Text = "⚠️ Another Round of Groups",
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
                    Text = "⚠️ Create Plate Competition (losers)",
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
                        Text = $"⚠️ View Round {roundNum} ({round.Count()} groups � completed)",
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
            Text = "⚠️ Select Venues & Tables",
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
                        Text = $"⚠️ {explanation}",
                        FontSize = 11,
                        TextColor = Color.FromArgb("#1E40AF")
                    }
                });
            }
        }

        // Group date picker
        content.Children.Add(new Label
        {
            Text = "⚠️ Group Round Date",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 10, 0, 4)
        });

        var groupDatePicker = new DatePicker { Date = settings.GroupDate ?? DateTime.Today, FontSize = 14 };
        groupDatePicker.DateSelected += async (s, e) =>
        {
            await _editorViewModel.SaveGroupDateAsync(e.NewDate);
            SetStatus(_editorViewModel.StatusMessage);
        };
        content.Children.Add(CreateLabeledField("Date:", groupDatePicker));

        if (settings.GroupDate.HasValue)
            content.Children.Add(new Label { Text = $"✅ {settings.GroupDate.Value:dd MMM yyyy}", FontSize = 13, TextColor = Color.FromArgb("#10B981") });

        // ? 3. Group Count ???????????????????????????????????????????????
        content.Children.Add(new Label
        {
            Text = "⚠️ Choose Number of Groups",
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
                    new Label { Text = $"⚠️ Top {settings.TopPlayersAdvance} from each group advance", FontSize = 12 },
                    new Label { Text = $"⚠️ {plateDesc}", FontSize = 12 },
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

        // Round schedule � dates and tables per KO round
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
        bool isPlate = competition.ParentCompetitionId.HasValue;

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

            // Date picker
            var datePicker = new DatePicker { Date = round.Date ?? DateTime.Today, FontSize = 13 };
            var capturedRound = round;

            datePicker.DateSelected += async (s, e) =>
            {
                await _editorViewModel.SaveRoundDetailsAsync(capturedRound.Id, e.NewDate, null);
                SetStatus(_editorViewModel.StatusMessage);
                // Refresh to update plate restrictions
                if (_selectedCompetition != null)
                    ShowCompetitionEditor(_selectedCompetition);
            };
            roundStack.Children.Add(CreateLabeledField("Date:", datePicker));

            if (round.Date.HasValue)
            {
                roundStack.Children.Add(new Label
                {
                    Text = $"⚠️ {round.Date.Value:dd MMM yyyy}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#10B981")
                });
            }

            // Table selection for this round
            if (allVenues.Count > 0 && allVenues.Any(v => v.Tables.Count > 0))
            {
                // Get tables restricted by parent comp on this date
                var restrictedTableIds = new HashSet<Guid>();
                if (isPlate && round.Date.HasValue)
                {
                    var restricted = await _editorViewModel.GetTablesInUseByParentOnDateAsync(round.Date.Value);
                    restrictedTableIds = new HashSet<Guid>(restricted);
                }

                var selectedTableIds = new HashSet<Guid>(
                    round.SelectedVenues.SelectMany(v => v.SelectedTables).Select(t => t.TableId));

                var tablesLayout = new VerticalStackLayout { Spacing = 2 };

                int totalAvailableTables = 0;
                int totalRestrictedTables = 0;

                foreach (var venue in allVenues)
                {
                    if (venue.Tables.Count == 0) continue;

                    tablesLayout.Children.Add(new Label
                    {
                        Text = venue.Name ?? "Unnamed Venue",
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        Margin = new Thickness(0, 2, 0, 0)
                    });

                    foreach (var table in venue.Tables)
                    {
                        bool isRestricted = restrictedTableIds.Contains(table.Id);
                        bool isSelected = selectedTableIds.Contains(table.Id) && !isRestricted;

                        if (isRestricted)
                            totalRestrictedTables++;
                        else
                            totalAvailableTables++;

                        var tableRow = new Grid
                        {
                            ColumnDefinitions =
                            {
                                new ColumnDefinition { Width = GridLength.Auto },
                                new ColumnDefinition { Width = GridLength.Star },
                            },
                            ColumnSpacing = 4,
                            Padding = new Thickness(16, 0, 0, 0)
                        };

                        var checkBox = new CheckBox
                        {
                            IsChecked = isSelected,
                            IsEnabled = !isRestricted,
                            VerticalOptions = LayoutOptions.Center
                        };

                        var tableLabelText = string.IsNullOrWhiteSpace(table.Label) ? "Unnamed" : table.Label;
                        if (isRestricted)
                            tableLabelText += $" {Emojis.Warning} main comp";

                        var tableLabel = new Label
                        {
                            Text = tableLabelText,
                            FontSize = 11,
                            VerticalTextAlignment = TextAlignment.Center,
                            TextColor = isRestricted ? Color.FromArgb("#9CA3AF") : Colors.Black,
                            FontAttributes = isRestricted ? FontAttributes.Italic : FontAttributes.None
                        };

                        tableRow.AutomationId = $"{venue.Id}|{table.Id}";

                        var capturedRound2 = round;
                        checkBox.CheckedChanged += async (s, e) =>
                        {
                            await SaveRoundTableSelections(competition, capturedRound2, allVenues, tablesLayout, restrictedTableIds);
                        };

                        tableRow.Add(checkBox, 0, 0);
                        tableRow.Add(tableLabel, 1, 0);
                        tablesLayout.Children.Add(tableRow);
                    }
                }

                if (tablesLayout.Children.Count > 0)
                {
                    roundStack.Children.Add(new Label { Text = "Tables:", FontSize = 12, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 2, 0, 0) });
                    roundStack.Children.Add(tablesLayout);

                    // Warning if all tables are restricted (plate conflict)
                    if (isPlate && totalAvailableTables == 0 && totalRestrictedTables > 0)
                    {
                        roundStack.Children.Add(new Border
                        {
                            Padding = 8,
                            BackgroundColor = Color.FromArgb("#FEF2F2"),
                            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                            Stroke = Color.FromArgb("#FECACA"),
                            Content = new Label
                            {
                                Text = "⚠️ All tables are in use by the main competition on this date. Choose a different date or add more tables.",
                                FontSize = 11,
                                TextColor = Color.FromArgb("#DC2626")
                            }
                        });
                    }
                    else if (isPlate && totalRestrictedTables > 0)
                    {
                        roundStack.Children.Add(new Label
                        {
                            Text = $"⚠️ {totalRestrictedTables} table(s) unavailable � in use by main competition on this date",
                            FontSize = 11,
                            TextColor = Color.FromArgb("#D97706"),
                            FontAttributes = FontAttributes.Italic
                        });
                    }

                    if (round.TotalTables > 0)
                    {
                        var tableSummary = string.Join(", ", round.SelectedVenues.Select(v =>
                        {
                            var names = string.Join(", ", v.SelectedTables.Select(t => t.Label));
                            return $"{v.VenueName} ({names})";
                        }));
                        roundStack.Children.Add(new Label
                        {
                            Text = $"✅ {tableSummary}",
                            FontSize = 11,
                            TextColor = Color.FromArgb("#10B981")
                        });
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

        foreach (var venueSection in tablesLayout.Children)
        {
            if (venueSection is not Grid tableRow) continue;
            if (string.IsNullOrEmpty(tableRow.AutomationId)) continue;

            var parts = tableRow.AutomationId.Split('|');
            if (parts.Length != 2) continue;
            if (!Guid.TryParse(parts[0], out var venueId)) continue;
            if (!Guid.TryParse(parts[1], out var tableId)) continue;

            // Skip restricted tables
            if (restrictedTableIds.Contains(tableId)) continue;

            var checkBox = tableRow.Children.OfType<CheckBox>().FirstOrDefault();
            if (checkBox == null || !checkBox.IsChecked) continue;

            if (!venueSelections.ContainsKey(venueId))
                venueSelections[venueId] = new List<SelectedTable>();

            var label = tableById.TryGetValue(tableId, out var info) ? info.Table.Label : "";
            venueSelections[venueId].Add(new SelectedTable { TableId = tableId, Label = label });
        }

        var venues = new List<CompetitionVenue>();
        var venueById = allVenues.ToDictionary(v => v.Id);
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

        // For plate competitions, check if parent uses tables on the group date
        bool isPlate = competition.ParentCompetitionId.HasValue;
        var restrictedTableIds = new HashSet<Guid>();
        if (isPlate && settings.GroupDate.HasValue)
        {
            var restricted = await _editorViewModel.GetTablesInUseByParentOnDateAsync(settings.GroupDate.Value);
            restrictedTableIds = new HashSet<Guid>(restricted);
        }

        // Build a quick lookup of already-selected table IDs
        var selectedTableIds = new HashSet<Guid>(
            settings.SelectedVenues.SelectMany(v => v.SelectedTables).Select(t => t.TableId));

        var venuesLayout = new VerticalStackLayout { Spacing = 8 };

        foreach (var venue in venues)
        {
            if (venue.Tables.Count == 0) continue; // skip venues with no tables defined

            var venueSection = new VerticalStackLayout { Spacing = 2 };

            // Venue name header
            venueSection.Children.Add(new Label
            {
                Text = venue.Name ?? "Unnamed Venue",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 2, 0, 0)
            });

            // One checkbox per table
            foreach (var table in venue.Tables)
            {
                bool isRestricted = restrictedTableIds.Contains(table.Id);
                bool isSelected = selectedTableIds.Contains(table.Id) && !isRestricted;

                var tableRow = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = GridLength.Star },
                    },
                    ColumnSpacing = 4,
                    Padding = new Thickness(16, 0, 0, 0) // indent under venue name
                };

                var checkBox = new CheckBox
                {
                    IsChecked = isSelected,
                    IsEnabled = !isRestricted,
                    VerticalOptions = LayoutOptions.Center
                };

                var tableLabelText = string.IsNullOrWhiteSpace(table.Label) ? "Unnamed Table" : table.Label;
                if (isRestricted)
                    tableLabelText += $" {Emojis.Warning} main comp";

                var tableLabel = new Label
                {
                    Text = tableLabelText,
                    FontSize = 12,
                    VerticalTextAlignment = TextAlignment.Center,
                    TextColor = isRestricted ? Color.FromArgb("#9CA3AF") : Colors.Black,
                    FontAttributes = isRestricted ? FontAttributes.Italic : FontAttributes.None
                };

                // Tag with composite ID so we can read it back during save
                tableRow.AutomationId = $"{venue.Id}|{table.Id}";

                checkBox.CheckedChanged += async (s, e) =>
                {
                    await SaveVenueSelections(competition, venues, venuesLayout);
                };

                tableRow.Add(checkBox, 0, 0);
                tableRow.Add(tableLabel, 1, 0);

                venueSection.Children.Add(tableRow);
            }

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

        // Warning for plate table conflicts
        if (isPlate && restrictedTableIds.Count > 0)
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
                        Text = "⚠️ All tables are in use by the main competition on this date. Choose a different group date or add more tables to your venues.",
                        FontSize = 11,
                        TextColor = Color.FromArgb("#DC2626")
                    }
                });
            }
            else
            {
                container.Children.Add(new Label
                {
                    Text = $"⚠️ {restrictedTableIds.Count} table(s) unavailable � in use by main competition on this date",
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

        // Walk all venue sections ? table rows
        foreach (var venueSection in venuesLayout.Children.OfType<VerticalStackLayout>())
        {
            foreach (var child in venueSection.Children)
            {
                if (child is not Grid tableRow) continue;
                if (string.IsNullOrEmpty(tableRow.AutomationId)) continue;

                var parts = tableRow.AutomationId.Split('|');
                if (parts.Length != 2) continue;
                if (!Guid.TryParse(parts[0], out var venueId)) continue;
                if (!Guid.TryParse(parts[1], out var tableId)) continue;

                var checkBox = tableRow.Children.OfType<CheckBox>().FirstOrDefault();
                if (checkBox == null || !checkBox.IsChecked) continue;

                if (!venueSelections.ContainsKey(venueId))
                    venueSelections[venueId] = new List<SelectedTable>();

                var label = tableById.TryGetValue(tableId, out var info) ? info.Table.Label : "";
                venueSelections[venueId].Add(new SelectedTable { TableId = tableId, Label = label });
            }
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
}
