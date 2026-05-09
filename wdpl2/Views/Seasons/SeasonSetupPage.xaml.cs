using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class SeasonSetupPage : ContentPage
{
    private readonly IDataStore _dataStore;
    private SetupMethod _selectedMethod = SetupMethod.None;
    private Season? _newSeason;
    private SeasonTemplate? _selectedTemplate;
    private Season? _sourceSeason;
    
    private enum SetupMethod
    {
        None,
        CopyPrevious,
        UseTemplate,
        ManualSetup
    }

    // Copy options
    private readonly ObservableCollection<Season> _availableSeasons = new();
    private bool _copyDivisions = true;
    private bool _copyVenues = true;
    private DatePicker? _copyStartDatePicker;
    private DatePicker? _copyEndDatePicker;

    // Individual team/player selection
    private List<Team> _sourceTeams = new();
    private List<Player> _sourcePlayers = new();
    private readonly HashSet<Guid> _selectedTeamIds = new();
    private readonly HashSet<Guid> _selectedPlayerIds = new();
    private VerticalStackLayout? _teamsContainer;
    private VerticalStackLayout? _playersContainer;
    private Label? _teamsCountLabel;
    private Label? _playersCountLabel;

    public SeasonSetupPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
        InitializeComponent();
    }

    // ========== METHOD SELECTION ==========

    private void OnCopyPreviousSelected(object? sender, EventArgs e)
    {
        SelectMethod(SetupMethod.CopyPrevious);
    }

    private void OnUseTemplateSelected(object? sender, EventArgs e)
    {
        SelectMethod(SetupMethod.UseTemplate);
    }

    private void OnManualSetupSelected(object? sender, EventArgs e)
    {
        SelectMethod(SetupMethod.ManualSetup);
    }

    private void SelectMethod(SetupMethod method)
    {
        _selectedMethod = method;

        // Reset all borders
        ResetBorderStyles();

        // Highlight selected method
        var selectedBorder = method switch
        {
            SetupMethod.CopyPrevious => CopyPreviousBorder,
            SetupMethod.UseTemplate => UseTemplateBorder,
            SetupMethod.ManualSetup => ManualSetupBorder,
            _ => null
        };

        if (selectedBorder != null)
        {
            selectedBorder.Stroke = Color.FromArgb("#3B82F6");
            selectedBorder.StrokeThickness = 3;
            selectedBorder.BackgroundColor = Color.FromArgb("#EFF6FF");
        }

        // Show appropriate content
        ShowMethodContent();
    }

    private void ResetBorderStyles()
    {
        var defaultStroke = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#475569")
            : Color.FromArgb("#CBD5E1");
        var defaultBg = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#1E293B")
            : Color.FromArgb("#F8FAFC");

        CopyPreviousBorder.Stroke = defaultStroke;
        CopyPreviousBorder.StrokeThickness = 2;
        CopyPreviousBorder.BackgroundColor = defaultBg;

        UseTemplateBorder.Stroke = defaultStroke;
        UseTemplateBorder.StrokeThickness = 2;
        UseTemplateBorder.BackgroundColor = defaultBg;

        ManualSetupBorder.Stroke = defaultStroke;
        ManualSetupBorder.StrokeThickness = 2;
        ManualSetupBorder.BackgroundColor = defaultBg;
    }

    // ========== CONTENT DISPLAY ==========

    private void ShowMethodContent()
    {
        ContentPanel.Content = _selectedMethod switch
        {
            SetupMethod.CopyPrevious => CreateCopyPreviousContent(),
            SetupMethod.UseTemplate => CreateTemplateContent(),
            SetupMethod.ManualSetup => CreateManualSetupContent(),
            _ => null
        };

        MethodSelectionPanel.IsVisible = false;
        ActionButtons.IsVisible = true;
    }

    private View CreateCopyPreviousContent()
    {
        _availableSeasons.Clear();
        var seasons = _dataStore.GetData().Seasons
            .OrderByDescending(s => s.StartDate)
            .Take(10)
            .ToList();

        foreach (var season in seasons)
        {
            _availableSeasons.Add(season);
        }

        var seasonPicker = new Picker
        {
            Title = "Select source season",
            ItemsSource = _availableSeasons.ToList(),
            ItemDisplayBinding = new Binding("Name"),
            Margin = new Thickness(0, 10, 0, 0)
        };
        seasonPicker.SelectedIndexChanged += (s, e) =>
        {
            if (seasonPicker.SelectedItem is Season season)
            {
                _sourceSeason = season;
                if (_copyStartDatePicker != null)
                    _copyStartDatePicker.Date = season.StartDate.AddYears(1);
                if (_copyEndDatePicker != null)
                    _copyEndDatePicker.Date = season.EndDate.AddYears(1);
                PopulateSourceSeasonData(season);
            }
        };

        var nameEntry = new Entry
        {
            Placeholder = "New season name (e.g., Spring 2025)",
            Margin = new Thickness(0, 10, 0, 0)
        };

        _copyStartDatePicker = new DatePicker
        {
            Date = DateTime.Today,
            MinimumDate = DateTime.Today.AddYears(-1),
            MaximumDate = DateTime.Today.AddYears(5)
        };
        _copyEndDatePicker = new DatePicker
        {
            Date = DateTime.Today.AddMonths(3),
            MinimumDate = DateTime.Today.AddYears(-1),
            MaximumDate = DateTime.Today.AddYears(5)
        };

        var copyDivisionsSwitch = new Switch { IsToggled = true };
        copyDivisionsSwitch.Toggled += (s, e) => _copyDivisions = e.Value;

        var copyVenuesSwitch = new Switch { IsToggled = true };
        copyVenuesSwitch.Toggled += (s, e) => _copyVenues = e.Value;

        var optionsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 12,
            Margin = new Thickness(0, 20, 0, 0)
        };

        optionsGrid.Add(new Label { Text = "Copy Divisions", VerticalOptions = LayoutOptions.Center }, 0, 0);
        optionsGrid.Add(copyDivisionsSwitch, 1, 0);

        optionsGrid.Add(new Label { Text = "Copy Venues", VerticalOptions = LayoutOptions.Center }, 0, 1);
        optionsGrid.Add(copyVenuesSwitch, 1, 1);

        // Teams selection section
        _teamsContainer = new VerticalStackLayout { Spacing = 6 };
        _teamsContainer.Children.Add(new Label
        {
            Text = "Select a source season to see available teams",
            FontSize = 13, TextColor = Color.FromArgb("#94A3B8"), FontAttributes = FontAttributes.Italic
        });

        // Players selection section
        _playersContainer = new VerticalStackLayout { Spacing = 6 };
        _playersContainer.Children.Add(new Label
        {
            Text = "Select a source season to see available players",
            FontSize = 13, TextColor = Color.FromArgb("#94A3B8"), FontAttributes = FontAttributes.Italic
        });

        return new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = "\U0001F4CB Copy From Previous Season",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    FontFamily = "Segoe UI Emoji",
                    Margin = new Thickness(0, 0, 0, 10)
                },
                new Label
                {
                    Text = "Select a season to copy and customize what data to include:",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#64748B")
                },
                new Label { Text = "Source Season:", FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 10, 0, 0) },
                seasonPicker,
                new Label { Text = "New Season Name:", FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 10, 0, 0) },
                nameEntry,
                new Label { Text = "Start Date:", FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 10, 0, 0) },
                _copyStartDatePicker,
                new Label { Text = "End Date:", FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 10, 0, 0) },
                _copyEndDatePicker,
                new Label { Text = "What to Copy:", FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 10, 0, 0) },
                optionsGrid,
                new Label { Text = "\U0001F465 Teams", FontAttributes = FontAttributes.Bold, FontSize = 16, FontFamily = "Segoe UI Emoji", Margin = new Thickness(0, 16, 0, 0) },
                _teamsContainer,
                new Label { Text = "\U0001F464 Players", FontAttributes = FontAttributes.Bold, FontSize = 16, FontFamily = "Segoe UI Emoji", Margin = new Thickness(0, 16, 0, 0) },
                _playersContainer,
                new Border
                {
                    Padding = 12,
                    BackgroundColor = Color.FromArgb("#FEF3C7"),
                    Stroke = Color.FromArgb("#F59E0B"),
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Margin = new Thickness(0, 20, 0, 0),
                    Content = new Label
                    {
                        Text = "\u2139\uFE0F Fixtures and results from the previous season will NOT be copied. Only structural data (teams, players, etc.) will be duplicated.",
                        FontSize = 12,
                        FontFamily = "Segoe UI Emoji",
                        LineBreakMode = LineBreakMode.WordWrap
                    }
                }
            }
        };
    }

    private View CreateTemplateContent()
    {
        var templates = SeasonTemplate.GetPredefinedTemplates();
        var templateList = new CollectionView
        {
            ItemsSource = templates,
            SelectionMode = SelectionMode.Single,
            Margin = new Thickness(0, 10, 0, 0)
        };

        templateList.ItemTemplate = new DataTemplate(() =>
        {
            var border = new Border
            {
                Padding = 16,
                Margin = new Thickness(0, 0, 0, 8),
                BackgroundColor = Color.FromArgb("#F8FAFC"),
                Stroke = Color.FromArgb("#CBD5E1"),
                StrokeThickness = 2,
                StrokeShape = new RoundRectangle { CornerRadius = 8 }
            };

            var nameLabel = new Label
            {
                FontSize = 18,
                FontAttributes = FontAttributes.Bold
            };
            nameLabel.SetBinding(Label.TextProperty, "Name");

            var descLabel = new Label
            {
                FontSize = 14,
                TextColor = Color.FromArgb("#64748B"),
                Margin = new Thickness(0, 4, 0, 8)
            };
            descLabel.SetBinding(Label.TextProperty, "Description");

            var detailsLabel = new Label
            {
                FontSize = 12,
                TextColor = Color.FromArgb("#6B7280"),
                FontFamily = "Segoe UI Emoji"
            };
            detailsLabel.SetBinding(Label.TextProperty, new Binding
            {
                StringFormat = "\U0001F4CA {0} division(s) � {1} teams/div � {2} frames/match",
                Path = "."
            });

            var stack = new VerticalStackLayout
            {
                Children = { nameLabel, descLabel, detailsLabel }
            };

            border.Content = stack;
            return border;
        });

        templateList.SelectionChanged += (s, e) =>
        {
            if (e.CurrentSelection?.FirstOrDefault() is SeasonTemplate template)
            {
                _selectedTemplate = template;
                SetStatus($"Selected: {template.Name}");
            }
        };

        var nameEntry = new Entry
        {
            Placeholder = "Season name (e.g., Spring 2025)",
            Margin = new Thickness(0, 10, 0, 0)
        };

        var templateStartDatePicker = new DatePicker
        {
            Date = DateTime.Today,
            MinimumDate = DateTime.Today.AddYears(-1),
            MaximumDate = DateTime.Today.AddYears(5)
        };
        var templateEndDatePicker = new DatePicker
        {
            Date = DateTime.Today.AddMonths(3),
            MinimumDate = DateTime.Today.AddYears(-1),
            MaximumDate = DateTime.Today.AddYears(5)
        };

        return new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = "\U0001F3AF Choose a Template",
                        FontSize = 22,
                        FontAttributes = FontAttributes.Bold,
                        FontFamily = "Segoe UI Emoji",
                        Margin = new Thickness(0, 0, 0, 10)
                    },
                    new Label
                    {
                        Text = "Select a pre-configured template to get started quickly:",
                        FontSize = 14,
                        TextColor = Color.FromArgb("#64748B")
                    },
                    templateList,
                    new Label { Text = "Season Name:", FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 10, 0, 0) },
                    nameEntry,
                    new Label { Text = "Start Date:", FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 10, 0, 0) },
                    templateStartDatePicker,
                    new Label { Text = "End Date:", FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 10, 0, 0) },
                    templateEndDatePicker
                }
            }
        };
    }

    private View CreateManualSetupContent()
    {
        var nameEntry = new Entry { Placeholder = "Season name (e.g., Spring 2025)" };
        var startDatePicker = new DatePicker { Date = DateTime.Today };
        var endDatePicker = new DatePicker { Date = DateTime.Today.AddMonths(3) };
        var matchDayPicker = new Picker
        {
            ItemsSource = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().Select(d => d.ToString()).ToList(),
            SelectedIndex = 2 // Tuesday
        };
        var matchTimePicker = new TimePicker { Time = new TimeSpan(19, 30, 0) };
        var framesEntry = new Entry { Placeholder = "10", Keyboard = Keyboard.Numeric };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            RowSpacing = 12,
            Margin = new Thickness(0, 20, 0, 0)
        };

        grid.Add(new Label { Text = "Season Name:", VerticalOptions = LayoutOptions.Center }, 0, 0);
        grid.Add(nameEntry, 1, 0);

        grid.Add(new Label { Text = "Start Date:", VerticalOptions = LayoutOptions.Center }, 0, 1);
        grid.Add(startDatePicker, 1, 1);

        grid.Add(new Label { Text = "End Date:", VerticalOptions = LayoutOptions.Center }, 0, 2);
        grid.Add(endDatePicker, 1, 2);

        grid.Add(new Label { Text = "Match Day:", VerticalOptions = LayoutOptions.Center }, 0, 3);
        grid.Add(matchDayPicker, 1, 3);

        grid.Add(new Label { Text = "Match Time:", VerticalOptions = LayoutOptions.Center }, 0, 4);
        grid.Add(matchTimePicker, 1, 4);

        grid.Add(new Label { Text = "Frames/Match:", VerticalOptions = LayoutOptions.Center }, 0, 5);
        grid.Add(framesEntry, 1, 5);

        return new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label
                {
                    Text = "\u270F\uFE0F Manual Setup",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    FontFamily = "Segoe UI Emoji",
                    Margin = new Thickness(0, 0, 0, 10)
                },
                new Label
                {
                    Text = "Enter the basic details for your new season:",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#64748B")
                },
                grid,
                new Border
                {
                    Padding = 12,
                    BackgroundColor = Color.FromArgb("#DBEAFE"),
                    Stroke = Color.FromArgb("#3B82F6"),
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Margin = new Thickness(0, 20, 0, 0),
                    Content = new Label
                    {
                        Text = "\u2139\uFE0F After creating the season, you'll need to add divisions, teams, and players manually through their respective pages.",
                        FontSize = 12,
                        FontFamily = "Segoe UI Emoji",
                        LineBreakMode = LineBreakMode.WordWrap
                    }
                }
            }
        };
    }

    // ========== NAVIGATION ==========

    private void OnBackToSelection(object? sender, EventArgs e)
    {
        MethodSelectionPanel.IsVisible = true;
        ContentPanel.Content = null;
        ActionButtons.IsVisible = false;
        _selectedMethod = SetupMethod.None;
        ResetBorderStyles();
        SetStatus("");
    }

    private async void OnContinue(object? sender, EventArgs e)
    {
        try
        {
            switch (_selectedMethod)
            {
                case SetupMethod.CopyPrevious:
                    await ProcessCopyPreviousAsync();
                    break;
                case SetupMethod.UseTemplate:
                    await ProcessTemplateAsync();
                    break;
                case SetupMethod.ManualSetup:
                    await ProcessManualSetupAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to create season: {ex.Message}", "OK");
            SetStatus($"Error: {ex.Message}");
        }
    }

    // ========== PROCESSING ==========

    private async Task ProcessCopyPreviousAsync()
    {
        if (_sourceSeason == null)
        {
            await DisplayAlert("Selection Required", "Please select a source season to copy.", "OK");
            return;
        }

        // Get the new season name from the Entry
        var nameEntry = FindEntryInContent("New season name");
        if (nameEntry == null || string.IsNullOrWhiteSpace(nameEntry.Text))
        {
            await DisplayAlert("Name Required", "Please enter a name for the new season.", "OK");
            return;
        }

        SetStatus("Copying season data...");

        // Create new season with same settings
        _newSeason = new Season
        {
            Name = nameEntry.Text.Trim(),
            StartDate = _copyStartDatePicker?.Date ?? _sourceSeason.StartDate.AddYears(1),
            EndDate = _copyEndDatePicker?.Date ?? _sourceSeason.EndDate.AddYears(1),
            MatchDayOfWeek = _sourceSeason.MatchDayOfWeek,
            MatchStartTime = _sourceSeason.MatchStartTime,
            FramesPerMatch = _sourceSeason.FramesPerMatch,
            IncludeDoubles = _sourceSeason.IncludeDoubles,
            SinglesFrameCount = _sourceSeason.SinglesFrameCount,
            DoublesFrameCount = _sourceSeason.DoublesFrameCount,
            IsActive = false // Don't auto-activate
        };

        _dataStore.GetData().Seasons.Add(_newSeason);

        int copiedCount = 0;

        // Copy data based on selections
        if (_copyDivisions)
        {
            foreach (var div in _dataStore.GetData().Divisions.Where(d => d.SeasonId == _sourceSeason.Id).ToList())
            {
                _dataStore.GetData().Divisions.Add(new Division
                {
                    SeasonId = _newSeason.Id,
                    Name = div.Name,
                    Notes = div.Notes
                });
                copiedCount++;
            }
        }

        if (_copyVenues)
        {
            foreach (var venue in _dataStore.GetData().Venues.Where(v => v.SeasonId == _sourceSeason.Id).ToList())
            {
                _dataStore.GetData().Venues.Add(new Venue
                {
                    SeasonId = _newSeason.Id,
                    Name = venue.Name,
                    Address = venue.Address,
                    Notes = venue.Notes
                });
                copiedCount++;
            }
        }

        // Copy selected teams
        if (_selectedTeamIds.Count > 0)
        {
            foreach (var team in _dataStore.GetData().Teams.Where(t => t.SeasonId == _sourceSeason.Id && _selectedTeamIds.Contains(t.Id)).ToList())
            {
                _dataStore.GetData().Teams.Add(new Team
                {
                    SeasonId = _newSeason.Id,
                    Name = team.Name,
                    Captain = team.Captain,
                    ProvidesFood = team.ProvidesFood,
                    Notes = team.Notes
                });
                copiedCount++;
            }
        }

        // Copy selected players
        if (_selectedPlayerIds.Count > 0)
        {
            foreach (var player in _dataStore.GetData().Players.Where(p => p.SeasonId == _sourceSeason.Id && _selectedPlayerIds.Contains(p.Id)).ToList())
            {
                _dataStore.GetData().Players.Add(new Player
                {
                    SeasonId = _newSeason.Id,
                    FirstName = player.FirstName,
                    LastName = player.LastName,
                    Notes = player.Notes
                });
                copiedCount++;
            }
        }

        await _dataStore.SaveAsync();

        await DisplayAlert("Success", $"\u2705 Season '{_newSeason.Name}' created!\nCopied {copiedCount} items from '{_sourceSeason.Name}'.", "OK");
        await Navigation.PopAsync();
    }

    private async Task ProcessTemplateAsync()
    {
        if (_selectedTemplate == null)
        {
            await DisplayAlert("Selection Required", "Please select a template.", "OK");
            return;
        }

        var nameEntry = FindEntryInContent("Season name");
        if (nameEntry == null || string.IsNullOrWhiteSpace(nameEntry.Text))
        {
            await DisplayAlert("Name Required", "Please enter a name for the season.", "OK");
            return;
        }

        SetStatus("Creating season from template...");

        var startDate = FindDatePickerInContent();
        var endDate = FindDatePickerInContent(1);

        _newSeason = new Season
        {
            Name = nameEntry.Text.Trim(),
            StartDate = startDate?.Date ?? DateTime.Today,
            EndDate = endDate?.Date ?? DateTime.Today.AddMonths(3),
            MatchDayOfWeek = _dataStore.GetData().Settings.DefaultMatchDay,
            MatchStartTime = _dataStore.GetData().Settings.DefaultMatchTime,
            FramesPerMatch = _selectedTemplate.FramesPerMatch,
            IsActive = false
        };

        _dataStore.GetData().Seasons.Add(_newSeason);

        // Create divisions based on template
        for (int i = 1; i <= _selectedTemplate.Divisions; i++)
        {
            _dataStore.GetData().Divisions.Add(new Division
            {
                SeasonId = _newSeason.Id,
                Name = _selectedTemplate.Divisions == 1 ? "Division 1" : $"Division {i}",
                Notes = $"Created from {_selectedTemplate.Name} template"
            });
        }

        await _dataStore.SaveAsync();

        await DisplayAlert("Success", 
            $"\u2705 Season '{_newSeason.Name}' created with {_selectedTemplate.Divisions} division(s)!\n\n" +
            $"Next steps:\n\u2022 Add venues\n\u2022 Add teams ({_selectedTemplate.TeamsPerDivision} per division)\n\u2022 Add players ({_selectedTemplate.PlayersPerTeam} per team)", 
            "OK");
        await Navigation.PopAsync();
    }

    private async Task ProcessManualSetupAsync()
    {
        var nameEntry = FindEntryInContent("Season name");
        if (nameEntry == null || string.IsNullOrWhiteSpace(nameEntry.Text))
        {
            await DisplayAlert("Name Required", "Please enter a name for the season.", "OK");
            return;
        }

        SetStatus("Creating season...");

        var startDate = FindDatePickerInContent();
        var endDate = FindDatePickerInContent(1);
        var matchDay = FindPickerInContent();
        var matchTime = FindTimePickerInContent();
        var framesEntry = FindEntryInContent("10");

        _newSeason = new Season
        {
            Name = nameEntry.Text.Trim(),
            StartDate = startDate?.Date ?? DateTime.Today,
            EndDate = endDate?.Date ?? DateTime.Today.AddMonths(3),
            MatchDayOfWeek = matchDay?.SelectedIndex >= 0 ? (DayOfWeek)matchDay.SelectedIndex : DayOfWeek.Tuesday,
            MatchStartTime = matchTime?.Time ?? new TimeSpan(19, 30, 0),
            FramesPerMatch = int.TryParse(framesEntry?.Text, out var frames) ? frames : 10,
            IsActive = false
        };

        _dataStore.GetData().Seasons.Add(_newSeason);
        await _dataStore.SaveAsync();

        await DisplayAlert("Success", 
            $"\u2705 Season '{_newSeason.Name}' created!\n\n" +
            "Next steps:\n\u2022 Add divisions\n\u2022 Add venues\n\u2022 Add teams\n\u2022 Add players", 
            "OK");
        await Navigation.PopAsync();
    }

    // ========== TEAM/PLAYER SELECTION ==========

    private void PopulateSourceSeasonData(Season source)
    {
        _sourceTeams = _dataStore.GetData().Teams
            .Where(t => t.SeasonId == source.Id)
            .OrderBy(t => t.Name)
            .ToList();

        _sourcePlayers = _dataStore.GetData().Players
            .Where(p => p.SeasonId == source.Id)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToList();

        // Select all by default
        _selectedTeamIds.Clear();
        foreach (var team in _sourceTeams)
            _selectedTeamIds.Add(team.Id);

        _selectedPlayerIds.Clear();
        foreach (var player in _sourcePlayers)
            _selectedPlayerIds.Add(player.Id);

        BuildTeamsList();
        BuildPlayersList();
    }

    private void BuildTeamsList()
    {
        if (_teamsContainer == null) return;
        _teamsContainer.Children.Clear();

        if (_sourceTeams.Count == 0)
        {
            _teamsContainer.Children.Add(new Label
            {
                Text = "No teams in selected season",
                FontSize = 13, TextColor = Color.FromArgb("#94A3B8"), FontAttributes = FontAttributes.Italic
            });
            return;
        }

        // Header with count and Select All / Deselect All
        _teamsCountLabel = new Label
        {
            Text = $"{_selectedTeamIds.Count} of {_sourceTeams.Count} selected",
            FontSize = 13, TextColor = Color.FromArgb("#64748B"), VerticalOptions = LayoutOptions.Center
        };

        var selectAllBtn = new Button
        {
            Text = "Select All", FontSize = 12, Padding = new Thickness(12, 4),
            BackgroundColor = Color.FromArgb("#3B82F6"), HeightRequest = 32
        };
        selectAllBtn.Clicked += (s, e) => ToggleAllTeams(true);

        var deselectAllBtn = new Button
        {
            Text = "Deselect All", FontSize = 12, Padding = new Thickness(12, 4),
            BackgroundColor = Color.FromArgb("#6B7280"), HeightRequest = 32
        };
        deselectAllBtn.Clicked += (s, e) => ToggleAllTeams(false);

        _teamsContainer.Children.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { _teamsCountLabel, selectAllBtn, deselectAllBtn }
        });

        // Individual team checkboxes
        foreach (var team in _sourceTeams)
        {
            var teamId = team.Id;
            var cb = new CheckBox { IsChecked = _selectedTeamIds.Contains(teamId), VerticalOptions = LayoutOptions.Center };
            cb.CheckedChanged += (s, e) =>
            {
                if (e.Value) _selectedTeamIds.Add(teamId); else _selectedTeamIds.Remove(teamId);
                if (_teamsCountLabel != null)
                    _teamsCountLabel.Text = $"{_selectedTeamIds.Count} of {_sourceTeams.Count} selected";
            };

            var divName = team.DivisionId.HasValue
                ? _dataStore.GetData().Divisions.FirstOrDefault(d => d.Id == team.DivisionId.Value)?.Name
                : null;

            var row = new HorizontalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    cb,
                    new Label { Text = team.Name ?? "(unnamed)", FontSize = 14, VerticalOptions = LayoutOptions.Center }
                }
            };

            if (!string.IsNullOrWhiteSpace(divName))
            {
                row.Children.Add(new Label
                {
                    Text = $"({divName})", FontSize = 12,
                    TextColor = Color.FromArgb("#94A3B8"), VerticalOptions = LayoutOptions.Center
                });
            }

            _teamsContainer.Children.Add(row);
        }
    }

    private void BuildPlayersList()
    {
        if (_playersContainer == null) return;
        _playersContainer.Children.Clear();

        if (_sourcePlayers.Count == 0)
        {
            _playersContainer.Children.Add(new Label
            {
                Text = "No players in selected season",
                FontSize = 13, TextColor = Color.FromArgb("#94A3B8"), FontAttributes = FontAttributes.Italic
            });
            return;
        }

        // Header with count and Select All / Deselect All
        _playersCountLabel = new Label
        {
            Text = $"{_selectedPlayerIds.Count} of {_sourcePlayers.Count} selected",
            FontSize = 13, TextColor = Color.FromArgb("#64748B"), VerticalOptions = LayoutOptions.Center
        };

        var selectAllBtn = new Button
        {
            Text = "Select All", FontSize = 12, Padding = new Thickness(12, 4),
            BackgroundColor = Color.FromArgb("#3B82F6"), HeightRequest = 32
        };
        selectAllBtn.Clicked += (s, e) => ToggleAllPlayers(true);

        var deselectAllBtn = new Button
        {
            Text = "Deselect All", FontSize = 12, Padding = new Thickness(12, 4),
            BackgroundColor = Color.FromArgb("#6B7280"), HeightRequest = 32
        };
        deselectAllBtn.Clicked += (s, e) => ToggleAllPlayers(false);

        _playersContainer.Children.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { _playersCountLabel, selectAllBtn, deselectAllBtn }
        });

        // Individual player checkboxes
        foreach (var player in _sourcePlayers)
        {
            var playerId = player.Id;
            var cb = new CheckBox { IsChecked = _selectedPlayerIds.Contains(playerId), VerticalOptions = LayoutOptions.Center };
            cb.CheckedChanged += (s, e) =>
            {
                if (e.Value) _selectedPlayerIds.Add(playerId); else _selectedPlayerIds.Remove(playerId);
                if (_playersCountLabel != null)
                    _playersCountLabel.Text = $"{_selectedPlayerIds.Count} of {_sourcePlayers.Count} selected";
            };

            var teamName = player.TeamId.HasValue
                ? _dataStore.GetData().Teams.FirstOrDefault(t => t.Id == player.TeamId.Value)?.Name
                : null;

            var row = new HorizontalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    cb,
                    new Label { Text = player.FullName, FontSize = 14, VerticalOptions = LayoutOptions.Center }
                }
            };

            if (!string.IsNullOrWhiteSpace(teamName))
            {
                row.Children.Add(new Label
                {
                    Text = $"({teamName})", FontSize = 12,
                    TextColor = Color.FromArgb("#94A3B8"), VerticalOptions = LayoutOptions.Center
                });
            }

            _playersContainer.Children.Add(row);
        }
    }

    private void ToggleAllTeams(bool select)
    {
        _selectedTeamIds.Clear();
        if (select)
        {
            foreach (var team in _sourceTeams)
                _selectedTeamIds.Add(team.Id);
        }
        BuildTeamsList();
    }

    private void ToggleAllPlayers(bool select)
    {
        _selectedPlayerIds.Clear();
        if (select)
        {
            foreach (var player in _sourcePlayers)
                _selectedPlayerIds.Add(player.Id);
        }
        BuildPlayersList();
    }

    // ========== HELPER METHODS ==========

    private Entry? FindEntryInContent(string placeholder)
    {
        if (ContentPanel.Content is VerticalStackLayout vsl)
        {
            return FindEntryRecursive(vsl, placeholder);
        }
        else if (ContentPanel.Content is ScrollView sv && sv.Content is VerticalStackLayout svVsl)
        {
            return FindEntryRecursive(svVsl, placeholder);
        }
        return null;
    }

    private Entry? FindEntryRecursive(Layout layout, string placeholder)
    {
        foreach (var child in layout.Children)
        {
            if (child is Entry entry && entry.Placeholder?.Contains(placeholder) == true)
                return entry;
            if (child is Layout childLayout)
            {
                var result = FindEntryRecursive(childLayout, placeholder);
                if (result != null) return result;
            }
        }
        return null;
    }

    private DatePicker? FindDatePickerInContent(int index = 0)
    {
        if (ContentPanel.Content is VerticalStackLayout vsl)
        {
            var pickers = FindDatePickersRecursive(vsl);
            return pickers.Count > index ? pickers[index] : null;
        }
        else if (ContentPanel.Content is ScrollView sv && sv.Content is VerticalStackLayout svVsl)
        {
            var pickers = FindDatePickersRecursive(svVsl);
            return pickers.Count > index ? pickers[index] : null;
        }
        return null;
    }

    private List<DatePicker> FindDatePickersRecursive(Layout layout)
    {
        var result = new List<DatePicker>();
        foreach (var child in layout.Children)
        {
            if (child is DatePicker dp)
                result.Add(dp);
            if (child is Layout childLayout)
                result.AddRange(FindDatePickersRecursive(childLayout));
        }
        return result;
    }

    private Picker? FindPickerInContent()
    {
        if (ContentPanel.Content is VerticalStackLayout vsl)
        {
            return FindPickerRecursive(vsl);
        }
        return null;
    }

    private Picker? FindPickerRecursive(Layout layout)
    {
        foreach (var child in layout.Children)
        {
            if (child is Picker picker && picker.ItemsSource != null)
                return picker;
            if (child is Layout childLayout)
            {
                var result = FindPickerRecursive(childLayout);
                if (result != null) return result;
            }
        }
        return null;
    }

    private TimePicker? FindTimePickerInContent()
    {
        if (ContentPanel.Content is VerticalStackLayout vsl)
        {
            return FindTimePickerRecursive(vsl);
        }
        return null;
    }

    private TimePicker? FindTimePickerRecursive(Layout layout)
    {
        foreach (var child in layout.Children)
        {
            if (child is TimePicker tp)
                return tp;
            if (child is Layout childLayout)
            {
                var result = FindTimePickerRecursive(childLayout);
                if (result != null) return result;
            }
        }
        return null;
    }

    private void SetStatus(string message)
    {
        StatusLabel.Text = message;
    }
}

/// <summary>
/// Template for pre-configured season structures
/// </summary>
public class SeasonTemplate
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Divisions { get; set; }
    public int TeamsPerDivision { get; set; }
    public int PlayersPerTeam { get; set; }
    public int FramesPerMatch { get; set; }
    public int Rounds { get; set; }
    public bool HasPlayoffs { get; set; }

    public static List<SeasonTemplate> GetPredefinedTemplates() => new()
    {
        new SeasonTemplate
        {
            Name = "Small Local League",
            Description = "1 division, 6-8 teams, perfect for pub leagues",
            Divisions = 1,
            TeamsPerDivision = 8,
            PlayersPerTeam = 8,
            FramesPerMatch = 10,
            Rounds = 2,
            HasPlayoffs = false
        },
        new SeasonTemplate
        {
            Name = "Multi-Division League",
            Description = "3 divisions, competitive structure",
            Divisions = 3,
            TeamsPerDivision = 10,
            PlayersPerTeam = 10,
            FramesPerMatch = 10,
            Rounds = 2,
            HasPlayoffs = true
        },
        new SeasonTemplate
        {
            Name = "Tournament Style",
            Description = "Single division with knockout playoffs",
            Divisions = 1,
            TeamsPerDivision = 16,
            PlayersPerTeam = 6,
            FramesPerMatch = 8,
            Rounds = 1,
            HasPlayoffs = true
        },
        new SeasonTemplate
        {
            Name = "Weekend League",
            Description = "Casual weekend pool league",
            Divisions = 1,
            TeamsPerDivision = 6,
            PlayersPerTeam = 6,
            FramesPerMatch = 8,
            Rounds = 2,
            HasPlayoffs = false
        },
        new SeasonTemplate
        {
            Name = "Custom",
            Description = "Start from scratch with your own settings",
            Divisions = 1,
            TeamsPerDivision = 8,
            PlayersPerTeam = 8,
            FramesPerMatch = 10,
            Rounds = 2,
            HasPlayoffs = false
        }
    };
}
