using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Wdpl2.Models;

namespace Wdpl2.Views;

/// <summary>
/// Multi-step wizard for creating a new competition.
/// Step 1: Choose format (visual cards)
/// Step 2: Name, date, and format-specific settings
/// Step 3: Review and create
/// </summary>
public class CompetitionWizardPage : ContentPage
{
    private readonly Guid _seasonId;
    private readonly TaskCompletionSource<Competition?> _result = new();

    // Wizard state
    private int _currentStep = 1;
    private const int TotalSteps = 3;

    // Selections
    private CompetitionFormat _selectedFormat = CompetitionFormat.SinglesKnockout;
    private string _competitionName = "New Competition";
    private DateTime _startDate = DateTime.Today;
    private int _bestOf = 7;
    private int _numberOfGroups = 4;
    private int _topAdvance = 2;
    private int _lowerToPlate = 2;
    private bool _allLosersToPlate = true;
    private bool _createPlate = true;
    private string _plateSuffix = "Plate";
    private bool _randomDraw = true;

    // UI refs
    private readonly VerticalStackLayout _contentArea = new() { Spacing = 16 };
    private readonly Label _stepLabel = new() { FontSize = 13, TextColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center };
    private readonly ProgressBar _progressBar = new() { Progress = 0.33 };
    private readonly Button _backBtn = new() { Text = "Back", IsVisible = false, Padding = new Thickness(16, 10) };
    private readonly Button _nextBtn = new() { Text = "Next", BackgroundColor = Color.FromArgb("#3B82F6"), TextColor = Colors.White, Padding = new Thickness(24, 10) };
    private readonly Button _cancelBtn = new() { Text = "Cancel", BackgroundColor = Color.FromArgb("#6B7280"), TextColor = Colors.White, Padding = new Thickness(16, 10) };

    // Format card tracking
    private Border? _selectedFormatCard;
    private readonly Dictionary<CompetitionFormat, Border> _formatCards = new();

    public CompetitionWizardPage(Guid seasonId)
    {
        _seasonId = seasonId;
        Title = "Create Competition";

        _backBtn.Clicked += (_, _) => GoBack();
        _nextBtn.Clicked += (_, _) => GoNext();
        _cancelBtn.Clicked += async (_, _) =>
        {
            await Navigation.PopModalAsync();
            _result.TrySetResult(null);
        };

        BuildLayout();
        ShowStep(1);
    }

    public Task<Competition?> GetResultAsync() => _result.Task;

    private void BuildLayout()
    {
        var mainLayout = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16,
            Children =
            {
                _stepLabel,
                _progressBar,
                new ScrollView
                {
                    Content = _contentArea,
                    VerticalOptions = LayoutOptions.FillAndExpand
                },
                new HorizontalStackLayout
                {
                    Spacing = 12,
                    HorizontalOptions = LayoutOptions.Center,
                    Children = { _cancelBtn, _backBtn, _nextBtn }
                }
            }
        };

        Content = mainLayout;
    }

    private void ShowStep(int step)
    {
        _currentStep = step;
        _contentArea.Children.Clear();

        _stepLabel.Text = step switch
        {
            1 => "Step 1 of 3 — Choose Format",
            2 => "Step 2 of 3 — Competition Details",
            3 => "Step 3 of 3 — Review & Create",
            _ => ""
        };

        _progressBar.Progress = step / (double)TotalSteps;
        _backBtn.IsVisible = step > 1;
        _nextBtn.Text = step == TotalSteps ? "Create Competition" : "Next";
        _nextBtn.BackgroundColor = step == TotalSteps ? Color.FromArgb("#10B981") : Color.FromArgb("#3B82F6");

        switch (step)
        {
            case 1: BuildFormatStep(); break;
            case 2: BuildDetailsStep(); break;
            case 3: BuildReviewStep(); break;
        }
    }

    // ???????? STEP 1: Format Selection ????????

    private void BuildFormatStep()
    {
        _contentArea.Children.Add(new Label
        {
            Text = "What type of competition?",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            RowSpacing = 12,
            ColumnSpacing = 12
        };

        var formats = new (CompetitionFormat format, string icon, string name, string desc)[]
        {
            (CompetitionFormat.SinglesKnockout,    "\U0001F3AF", "Singles Knockout",    "Classic 1v1 elimination bracket"),
            (CompetitionFormat.DoublesKnockout,    "\U0001F91D", "Doubles Knockout",    "Pairs compete in elimination bracket"),
            (CompetitionFormat.TeamKnockout,       "\U0001F465", "Team Knockout",       "Teams battle in elimination format"),
            (CompetitionFormat.RoundRobin,         "\U0001F504", "Round Robin",         "Everyone plays everyone"),
            (CompetitionFormat.SinglesGroupStage,  "\U0001F4CA", "Singles Groups",      "Group stage then knockout rounds"),
            (CompetitionFormat.DoublesGroupStage,  "\U0001F4CA", "Doubles Groups",      "Doubles group stage then knockout"),
        };

        for (int i = 0; i < formats.Length; i++)
        {
            var (format, icon, name, desc) = formats[i];
            var card = CreateFormatCard(format, icon, name, desc);
            grid.Add(card, i % 2, i / 2);
        }

        _contentArea.Children.Add(grid);
    }

    private Border CreateFormatCard(CompetitionFormat format, string icon, string name, string desc)
    {
        bool isSelected = format == _selectedFormat;

        var card = new Border
        {
            Stroke = isSelected ? Color.FromArgb("#3B82F6") : Color.FromArgb("#E5E7EB"),
            StrokeThickness = isSelected ? 3 : 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            BackgroundColor = isSelected ? Color.FromArgb("#EFF6FF") : Colors.White,
            Padding = new Thickness(16),
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Label { Text = icon, FontSize = 32, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = name, FontSize = 16, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = desc, FontSize = 12, TextColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center }
                }
            }
        };

        _formatCards[format] = card;
        if (isSelected) _selectedFormatCard = card;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => SelectFormat(format);
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private void SelectFormat(CompetitionFormat format)
    {
        // Deselect previous
        if (_selectedFormatCard != null)
        {
            _selectedFormatCard.Stroke = Color.FromArgb("#E5E7EB");
            _selectedFormatCard.StrokeThickness = 1;
            _selectedFormatCard.BackgroundColor = Colors.White;
        }

        _selectedFormat = format;

        // Select new
        if (_formatCards.TryGetValue(format, out var card))
        {
            card.Stroke = Color.FromArgb("#3B82F6");
            card.StrokeThickness = 3;
            card.BackgroundColor = Color.FromArgb("#EFF6FF");
            _selectedFormatCard = card;
        }

        // Auto-set a sensible name
        _competitionName = format switch
        {
            CompetitionFormat.SinglesKnockout => "Singles Championship",
            CompetitionFormat.DoublesKnockout => "Doubles Championship",
            CompetitionFormat.TeamKnockout => "Team Championship",
            CompetitionFormat.RoundRobin => "Round Robin",
            CompetitionFormat.SinglesGroupStage => "Singles Cup",
            CompetitionFormat.DoublesGroupStage => "Doubles Cup",
            _ => "New Competition"
        };
    }

    // ???????? STEP 2: Details & Settings ????????

    private Entry? _nameEntry;
    private DatePicker? _datePicker;

    private void BuildDetailsStep()
    {
        _contentArea.Children.Add(new Label
        {
            Text = "Competition Details",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Name
        _nameEntry = new Entry { Text = _competitionName, Placeholder = "Competition Name", FontSize = 16 };
        _nameEntry.TextChanged += (_, e) => _competitionName = e.NewTextValue;
        _contentArea.Children.Add(CreateField("Competition Name", _nameEntry));

        // Start Date
        _datePicker = new DatePicker { Date = _startDate, MinimumDate = DateTime.Today.AddYears(-1) };
        _datePicker.DateSelected += (_, e) => _startDate = e.NewDate;
        _contentArea.Children.Add(CreateField("Start Date", _datePicker));

        // Best Of
        var bestOfPicker = new Picker
        {
            Title = "Best Of",
            ItemsSource = new List<string> { "Best of 3", "Best of 5", "Best of 7", "Best of 9", "Best of 11", "Best of 13", "Best of 15" },
            SelectedIndex = _bestOf switch { 3 => 0, 5 => 1, 7 => 2, 9 => 3, 11 => 4, 13 => 5, 15 => 6, _ => 2 }
        };
        bestOfPicker.SelectedIndexChanged += (_, _) =>
        {
            _bestOf = bestOfPicker.SelectedIndex switch { 0 => 3, 1 => 5, 2 => 7, 3 => 9, 4 => 11, 5 => 13, 6 => 15, _ => 7 };
        };
        _contentArea.Children.Add(CreateField($"Best Of (first to {(_bestOf + 1) / 2} wins)", bestOfPicker));

        // Draw order
        var drawHintLabel = new Label
        {
            Text = _randomDraw
                ? "\U0001F3B2 Participants will be shuffled randomly when the bracket is generated."
                : "\U0001F4CB Participants will be placed in the order they are added.",
            FontSize = 12,
            TextColor = Colors.Gray,
            Margin = new Thickness(0, -4, 0, 0)
        };
        var randomDrawSwitch = new Switch { IsToggled = _randomDraw };
        randomDrawSwitch.Toggled += (_, e) =>
        {
            _randomDraw = e.Value;
            drawHintLabel.Text = _randomDraw
                ? "\U0001F3B2 Participants will be shuffled randomly when the bracket is generated."
                : "\U0001F4CB Participants will be placed in the order they are added.";
        };
        _contentArea.Children.Add(CreateField("Random Draw", randomDrawSwitch));
        _contentArea.Children.Add(drawHintLabel);

        // Group stage settings
        bool isGroupStage = _selectedFormat is CompetitionFormat.SinglesGroupStage or CompetitionFormat.DoublesGroupStage;
        if (isGroupStage)
        {
            _contentArea.Children.Add(new Label
            {
                Text = "Knockout Stage Settings",
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 16, 0, 4)
            });

            _contentArea.Children.Add(new Label
            {
                Text = "Groups and venues are configured in the editor after adding participants.",
                FontSize = 12,
                TextColor = Colors.Gray,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var topEntry = new Entry { Text = _topAdvance.ToString(), Keyboard = Keyboard.Numeric };
            topEntry.TextChanged += (_, e) => { if (int.TryParse(e.NewTextValue, out int v) && v >= 1) _topAdvance = v; };
            _contentArea.Children.Add(CreateField("Top Players Advance", topEntry));

            var plateSwitch = new Switch { IsToggled = _createPlate };
            var plateLosersLayout = new VerticalStackLayout { Spacing = 4, IsVisible = _createPlate };

            plateSwitch.Toggled += (_, e) =>
            {
                _createPlate = e.Value;
                plateLosersLayout.IsVisible = e.Value;
            };
            _contentArea.Children.Add(CreateField("Create Plate Competition", plateSwitch));

            // Plate losers options (only visible when plate is enabled)
            var allLosersSwitch = new Switch { IsToggled = _allLosersToPlate };

            var plateCountEntry = new Entry
            {
                Text = _lowerToPlate.ToString(),
                Keyboard = Keyboard.Numeric,
                IsEnabled = !_allLosersToPlate
            };
            plateCountEntry.TextChanged += (_, e) => { if (int.TryParse(e.NewTextValue, out int v) && v >= 1) _lowerToPlate = v; };

            allLosersSwitch.Toggled += (_, e) =>
            {
                _allLosersToPlate = e.Value;
                plateCountEntry.IsEnabled = !e.Value;
            };

            plateLosersLayout.Children.Add(CreateField("All Losers to Plate", allLosersSwitch));
            plateLosersLayout.Children.Add(new Label
            {
                Text = _allLosersToPlate
                    ? "Everyone who doesn't advance goes into the plate"
                    : "Only a fixed number of losers per group go to the plate",
                FontSize = 11,
                TextColor = Colors.Gray,
                FontAttributes = FontAttributes.Italic
            });
            plateLosersLayout.Children.Add(CreateField("Losers Per Group (if not all)", plateCountEntry));

            _contentArea.Children.Add(plateLosersLayout);
        }
    }

    private static View CreateField(string label, View field)
    {
        return new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = label, FontAttributes = FontAttributes.Bold, FontSize = 13 },
                field
            }
        };
    }

    // ???????? STEP 3: Review ????????

    private void BuildReviewStep()
    {
        _contentArea.Children.Add(new Label
        {
            Text = "Review Your Competition",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        });

        bool isGroupStage = _selectedFormat is CompetitionFormat.SinglesGroupStage or CompetitionFormat.DoublesGroupStage;

        var summary = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Stroke = Color.FromArgb("#E5E7EB"),
            BackgroundColor = Color.FromArgb("#F9FAFB"),
            Padding = new Thickness(20),
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    ReviewRow("Name", _competitionName),
                    ReviewRow("Format", FormatDisplayName(_selectedFormat)),
                    ReviewRow("Start Date", _startDate.ToString("dd MMM yyyy")),
                    ReviewRow("Best Of", $"{_bestOf} (first to {(_bestOf + 1) / 2})"),
                    ReviewRow("Draw Order", _randomDraw ? "Random" : "Manual (as added)"),
                }
            }
        };

        if (isGroupStage && summary.Content is VerticalStackLayout layout)
        {
            layout.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#E5E7EB"), Margin = new Thickness(0, 4) });
            layout.Children.Add(ReviewRow("Top Advance", _topAdvance.ToString()));
            layout.Children.Add(ReviewRow("Plate Competition", _createPlate ? "Yes" : "No"));
            if (_createPlate)
            {
                layout.Children.Add(ReviewRow("Plate Mode", _allLosersToPlate ? "All losers" : $"{_lowerToPlate} per group"));
            }
        }

        _contentArea.Children.Add(summary);

        var hintText = isGroupStage
            ? "After creation, add participants, select venues, and configure groups from the editor."
            : "After creation, you can add participants and generate the bracket from the editor.";

        _contentArea.Children.Add(new Label
        {
            Text = hintText,
            FontSize = 13,
            TextColor = Colors.Gray,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0)
        });
    }

    private static View ReviewRow(string label, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(160)), new ColumnDefinition(GridLength.Star) }
        };
        
        grid.Add(new Label { Text = label, FontAttributes = FontAttributes.Bold, FontSize = 14, VerticalTextAlignment = TextAlignment.Center }, 0, 0);
        grid.Add(new Label { Text = value, FontSize = 14, VerticalTextAlignment = TextAlignment.Center }, 1, 0);
        
        return grid;
    }

    private static string FormatDisplayName(CompetitionFormat format) => format switch
    {
        CompetitionFormat.SinglesKnockout => "Singles Knockout",
        CompetitionFormat.DoublesKnockout => "Doubles Knockout",
        CompetitionFormat.TeamKnockout => "Team Knockout",
        CompetitionFormat.RoundRobin => "Round Robin",
        CompetitionFormat.SinglesGroupStage => "Singles Group Stage",
        CompetitionFormat.DoublesGroupStage => "Doubles Group Stage",
        _ => format.ToString()
    };

    // ???????? Navigation ????????

    private void GoBack()
    {
        if (_currentStep > 1)
            ShowStep(_currentStep - 1);
    }

    private async void GoNext()
    {
        if (_currentStep < TotalSteps)
        {
            // Validate current step
            if (_currentStep == 2 && string.IsNullOrWhiteSpace(_competitionName))
            {
                await DisplayAlert("Validation", "Please enter a competition name.", "OK");
                return;
            }

            ShowStep(_currentStep + 1);
        }
        else
        {
            // Create the competition - pop modal first so parent page is ready
            var competition = BuildCompetition();
            await Navigation.PopModalAsync();
            _result.TrySetResult(competition);
        }
    }

    private Competition BuildCompetition()
    {
        bool isGroupStage = _selectedFormat is CompetitionFormat.SinglesGroupStage or CompetitionFormat.DoublesGroupStage;

        var competition = new Competition
        {
            Name = _competitionName,
            SeasonId = _seasonId,
            Format = _selectedFormat,
            Status = CompetitionStatus.Draft,
            StartDate = _startDate,
            CreatedDate = DateTime.Now,
            BestOf = _bestOf,
            RandomDraw = _randomDraw,
            Notes = $"Best of {_bestOf} (first to {(_bestOf + 1) / 2})"
        };

        if (isGroupStage)
        {
            competition.GroupSettings = new GroupStageSettings
            {
                NumberOfGroups = 0,
                TopPlayersAdvance = _topAdvance,
                LowerPlayersToPlate = _lowerToPlate,
                AllLosersToPlate = _allLosersToPlate,
                CreatePlateCompetition = _createPlate,
                PlateNameSuffix = _plateSuffix
            };
        }

        return competition;
    }
}
