using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2.Models;

namespace Wdpl2.Views;

public class CompetitionSetupDialog : ContentPage
{
    private Entry _nameEntry;
    private Picker _formatPicker;
    private Entry _framesPerMatchEntry;
    private Switch _homeAndAwaySwitch;
    
    // Group Stage Settings
    private VerticalStackLayout _groupStagePanel;
    private Border _doublesHintPanel;
    private Entry _numberOfGroupsEntry;
    private Entry _topAdvanceEntry;
    private Entry _lowerPlateEntry;
    private Switch _createPlateSwitch;
    private Entry _plateSuffixEntry;
    
    private readonly TaskCompletionSource<Competition?> _taskCompletionSource;
    private readonly Guid _seasonId;

    public CompetitionSetupDialog(Guid seasonId)
    {
        _seasonId = seasonId;
        _taskCompletionSource = new TaskCompletionSource<Competition?>();
        
        Title = "Create New Competition";
        BuildUI();
    }

    public Task<Competition?> GetResultAsync() => _taskCompletionSource.Task;

    private void BuildUI()
    {
        // Name
        _nameEntry = new Entry
        {
            Placeholder = "Competition Name",
            Text = "New Competition"
        };

        // Format
        _formatPicker = new Picker
        {
            Title = "Select Format",
            ItemsSource = new List<string>
            {
                "Singles Knockout",
                "Doubles Knockout",
                "Team Knockout",
                "Round Robin",
                "Swiss System",
                "Singles Group Stage",
                "Doubles Group Stage"
            },
            SelectedIndex = 0
        };

        // Frames per match
        _framesPerMatchEntry = new Entry
        {
            Placeholder = "Frames per match",
            Text = "8",
            Keyboard = Keyboard.Numeric
        };

        // Home and Away
        _homeAndAwaySwitch = new Switch
        {
            IsToggled = false
        };

        // Group Stage Settings Panel (initially hidden)
        _groupStagePanel = new VerticalStackLayout
        {
            Spacing = 10,
            IsVisible = false,
            Children =
            {
                new Label 
                { 
                    Text = "Group Stage Configuration", 
                    FontSize = 16, 
                    FontAttributes = FontAttributes.Bold,
                    Margin = new Thickness(0, 10, 0, 5)
                },
                new Label
                {
                    Text = "ℹ️ Number of groups will be configured after adding players, with recommended group sizes.",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#6366F1"),
                    Margin = new Thickness(0, 0, 0, 5)
                }
            }
        };

        _numberOfGroupsEntry = new Entry
        {
            Placeholder = "Number of groups",
            Text = "0",
            Keyboard = Keyboard.Numeric,
            IsVisible = false
        };

        _topAdvanceEntry = new Entry
        {
            Placeholder = "Top players advancing to knockout",
            Text = "2",
            Keyboard = Keyboard.Numeric
        };

        _lowerPlateEntry = new Entry
        {
            Placeholder = "Lower players to plate competition",
            Text = "2",
            Keyboard = Keyboard.Numeric
        };

        _createPlateSwitch = new Switch
        {
            IsToggled = true
        };

        _plateSuffixEntry = new Entry
        {
            Placeholder = "Plate competition suffix",
            Text = "Plate"
        };

        _groupStagePanel.Children.Add(CreateFieldRow("Top Players Advance:", _topAdvanceEntry));
        _groupStagePanel.Children.Add(CreateFieldRow("Lower to Plate:", _lowerPlateEntry));
        _groupStagePanel.Children.Add(CreateFieldRow("Create Plate Competition:", _createPlateSwitch));
        _groupStagePanel.Children.Add(CreateFieldRow("Plate Suffix:", _plateSuffixEntry));

        // Doubles format hint (shown only for doubles formats)
        _doublesHintPanel = new Border
        {
            IsVisible = false,
            Padding = 12,
            BackgroundColor = Color.FromArgb("#F0F9FF"),
            Stroke = Color.FromArgb("#6366F1"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
            Margin = new Thickness(0, 6, 0, 0),
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = "\U0001F465 Doubles Competition",
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#1E40AF")
                    },
                    new Label
                    {
                        Text = "You'll add 2-player teams after creation. Each team is treated as a single entry in the draw.",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#1E40AF")
                    }
                }
            }
        };

        // Format changed handler
        _formatPicker.SelectedIndexChanged += (s, e) =>
        {
            var selectedFormat = GetSelectedFormat();
            _groupStagePanel.IsVisible = selectedFormat == CompetitionFormat.SinglesGroupStage || 
                                         selectedFormat == CompetitionFormat.DoublesGroupStage;
            _doublesHintPanel.IsVisible = selectedFormat == CompetitionFormat.DoublesKnockout ||
                                          selectedFormat == CompetitionFormat.DoublesGroupStage;
        };

        // Buttons
        var createBtn = new Button
        {
            Text = "Create Competition",
            BackgroundColor = Color.FromArgb("#10B981"),
            TextColor = Colors.White,
            Margin = new Thickness(0, 20, 0, 0),
            Padding = new Thickness(12, 8)
        };
        createBtn.Clicked += OnCreateClicked;

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#6B7280"),
            TextColor = Colors.White,
            Margin = new Thickness(0, 5, 0, 0),
            Padding = new Thickness(12, 8)
        };
        cancelBtn.Clicked += OnCancelClicked;

        // Main Layout
        var scrollView = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    new Label 
                    { 
                        Text = "Competition Setup", 
                        FontSize = 20, 
                        FontAttributes = FontAttributes.Bold,
                        Margin = new Thickness(0, 0, 0, 10)
                    },
                    
                    // Basic Settings
                    new Label 
                    { 
                        Text = "Basic Information", 
                        FontSize = 16, 
                        FontAttributes = FontAttributes.Bold,
                        Margin = new Thickness(0, 5, 0, 5)
                    },
                    CreateFieldRow("Competition Name:", _nameEntry),
                    CreateFieldRow("Format:", _formatPicker),
                    
                    // Match Settings
                    new Label 
                    { 
                        Text = "Match Settings", 
                        FontSize = 16, 
                        FontAttributes = FontAttributes.Bold,
                        Margin = new Thickness(0, 10, 0, 5)
                    },
                    CreateFieldRow("Frames per Match:", _framesPerMatchEntry),
                    CreateFieldRow("Home & Away:", _homeAndAwaySwitch),
                    
                    new Border
                    {
                        Padding = 8,
                        BackgroundColor = Color.FromArgb("#FEF3C7"),
                        Stroke = Color.FromArgb("#F59E0B"),
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                        Content = new Label
                        {
                            Text = "⚠️ Home & Away: Each pair plays twice (once at each venue)",
                            FontSize = 12,
                            TextColor = Color.FromArgb("#92400E")
                        }
                    },
                    
                    // Doubles hint (conditionally visible)
                    _doublesHintPanel,

                    // Group Stage Settings (conditionally visible)
                    _groupStagePanel,

                    // Buttons
                    createBtn,
                    cancelBtn
                }
            }
        };

        Content = scrollView;
    }

    private Grid CreateFieldRow(string label, View field)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(160) },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };

        grid.Add(new Label
        {
            Text = label,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = FontAttributes.Bold,
            FontSize = 14
        }, 0, 0);

        grid.Add(field, 1, 0);

        return grid;
    }

    private CompetitionFormat GetSelectedFormat()
    {
        return _formatPicker.SelectedIndex switch
        {
            0 => CompetitionFormat.SinglesKnockout,
            1 => CompetitionFormat.DoublesKnockout,
            2 => CompetitionFormat.TeamKnockout,
            3 => CompetitionFormat.RoundRobin,
            4 => CompetitionFormat.Swiss,
            5 => CompetitionFormat.SinglesGroupStage,
            6 => CompetitionFormat.DoublesGroupStage,
            _ => CompetitionFormat.SinglesKnockout
        };
    }

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(_nameEntry.Text))
        {
            await DisplayAlert("Validation Error", "Please enter a competition name", "OK");
            return;
        }

        if (!int.TryParse(_framesPerMatchEntry.Text, out int framesPerMatch) || framesPerMatch < 1)
        {
            await DisplayAlert("Validation Error", "Please enter a valid number of frames per match (minimum 1)", "OK");
            return;
        }

        var format = GetSelectedFormat();

        // Validate group stage settings
        if (format == CompetitionFormat.SinglesGroupStage || format == CompetitionFormat.DoublesGroupStage)
        {
            if (!int.TryParse(_topAdvanceEntry.Text, out int topAdvance) || topAdvance < 1)
            {
                await DisplayAlert("Validation Error", "Please enter a valid number of top players advancing (minimum 1)", "OK");
                return;
            }

            if (!int.TryParse(_lowerPlateEntry.Text, out int lowerPlate) || lowerPlate < 0)
            {
                await DisplayAlert("Validation Error", "Please enter a valid number of lower players for plate (minimum 0)", "OK");
                return;
            }
        }

        // Create competition
        var competition = new Competition
        {
            Name = _nameEntry.Text,
            SeasonId = _seasonId,
            Format = format,
            Status = CompetitionStatus.Draft,
            CreatedDate = DateTime.Now,
            BestOf = framesPerMatch,
            Notes = $"Best of {framesPerMatch} (first to {(framesPerMatch + 1) / 2})\nHome & Away: {(_homeAndAwaySwitch.IsToggled ? "Yes" : "No")}"
        };

        // Add group stage settings if applicable
        if (format == CompetitionFormat.SinglesGroupStage || format == CompetitionFormat.DoublesGroupStage)
        {
            competition.GroupSettings = new GroupStageSettings
            {
                NumberOfGroups = 0,
                TopPlayersAdvance = int.Parse(_topAdvanceEntry.Text),
                LowerPlayersToPlate = int.Parse(_lowerPlateEntry.Text),
                CreatePlateCompetition = _createPlateSwitch.IsToggled,
                PlateNameSuffix = _plateSuffixEntry.Text ?? "Plate"
            };
        }

        _taskCompletionSource.SetResult(competition);
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _taskCompletionSource.SetResult(null);
        await Navigation.PopModalAsync();
    }
}
