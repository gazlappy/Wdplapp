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
    private bool _isTransitioning;

    // Selections
    private CompetitionFormat _selectedFormat = CompetitionFormat.SinglesKnockout;
    private string _competitionName = "Singles Knockout";
    private bool _nameManuallyEdited;
    private DateTime _startDate = DateTime.Today;
    private int _bestOf = 7;
    private int _numberOfGroups = 0;
    private int _topAdvance = 2;
    private int _lowerToPlate = 2;
    private bool _allLosersToPlate = true;
    private bool _createPlate = true;
    private bool _randomDraw = true;

    // UI refs
    private readonly VerticalStackLayout _contentArea = new() { Spacing = 16, Padding = new Thickness(20, 8) };
    private readonly HorizontalStackLayout _stepIndicator = new() { Spacing = 0, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
    private readonly List<(Border circle, Label label, BoxView line)> _stepElements = new();
    private readonly Button _backBtn;
    private readonly Button _nextBtn;
    private readonly Button _cancelBtn;

    // Format card tracking
    private Border? _selectedFormatCard;
    private readonly Dictionary<CompetitionFormat, Border> _formatCards = new();

    // Step 2 refs
    private Entry? _nameEntry;
    private DatePicker? _datePicker;

    // Colors
    private static readonly Color Blue = Color.FromArgb("#3B82F6");
    private static readonly Color BlueDark = Color.FromArgb("#2563EB");
    private static readonly Color Green = Color.FromArgb("#10B981");
    private static readonly Color GrayBorder = Color.FromArgb("#E5E7EB");
    private static readonly Color GrayText = Color.FromArgb("#6B7280");
    private static readonly Color GrayBg = Color.FromArgb("#F3F4F6");
    private static readonly Color Dark = Color.FromArgb("#111827");
    private static readonly Color SelectedBg = Color.FromArgb("#EFF6FF");

    public CompetitionWizardPage(Guid seasonId)
    {
        _seasonId = seasonId;
        Title = "Create Competition";
        BackgroundColor = Colors.White;

        _backBtn = new Button
        {
            Text = "\u2190  Back",
            BackgroundColor = Colors.Transparent,
            TextColor = GrayText,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(16, 10),
            IsVisible = false
        };
        _nextBtn = new Button
        {
            Text = "Next  \u2192",
            BackgroundColor = Blue,
            TextColor = Colors.White,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(28, 12),
            CornerRadius = 12
        };
        _cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.Transparent,
            TextColor = GrayText,
            FontSize = 14,
            Padding = new Thickness(16, 10)
        };

        _backBtn.Clicked += (_, _) => GoBack();
        _nextBtn.Clicked += (_, _) => GoNext();
        _cancelBtn.Clicked += async (_, _) =>
        {
            await Navigation.PopModalAsync();
            _result.TrySetResult(null);
        };

        BuildLayout();
        BuildStepIndicator();
        BuildFormatStep();
    }

    public Task<Competition?> GetResultAsync() => _result.Task;

    private void BuildLayout()
    {
        var rootGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(72, GridUnitType.Absolute)),
                new RowDefinition(GridLength.Star),
                new RowDefinition(new GridLength(68, GridUnitType.Absolute))
            },
            RowSpacing = 0
        };

        // Step indicator row
        var indicatorContainer = new Grid
        {
            Padding = new Thickness(20, 16),
            BackgroundColor = Colors.White
        };
        indicatorContainer.Children.Add(_stepIndicator);
        rootGrid.Add(indicatorContainer, 0, 0);

        // Scrollable content
        var scroll = new ScrollView { Content = _contentArea };
        rootGrid.Add(scroll, 0, 1);

        // Button bar
        var buttonBar = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Padding = new Thickness(20, 10),
            BackgroundColor = Colors.White
        };
        buttonBar.Add(_cancelBtn, 0, 0);
        buttonBar.Add(_backBtn, 1, 0);
        _backBtn.HorizontalOptions = LayoutOptions.Center;
        buttonBar.Add(_nextBtn, 2, 0);
        rootGrid.Add(buttonBar, 0, 2);

        Content = rootGrid;
    }

    private void BuildStepIndicator()
    {
        _stepIndicator.Children.Clear();
        _stepElements.Clear();

        string[] labels = ["Format", "Details", "Review"];

        for (int i = 0; i < TotalSteps; i++)
        {
            // Connecting line before circles 2 and 3
            BoxView? line = null;
            if (i > 0)
            {
                line = new BoxView
                {
                    HeightRequest = 2,
                    WidthRequest = 48,
                    Color = GrayBorder,
                    VerticalOptions = LayoutOptions.Center
                };
                _stepIndicator.Children.Add(line);
            }

            var numberLabel = new Label
            {
                Text = (i + 1).ToString(),
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };

            var circle = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                StrokeThickness = 2,
                Stroke = GrayBorder,
                BackgroundColor = GrayBg,
                WidthRequest = 36,
                HeightRequest = 36,
                Content = numberLabel
            };

            var stepLabel = new Label
            {
                Text = labels[i],
                FontSize = 11,
                TextColor = GrayText,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var stack = new VerticalStackLayout
            {
                Spacing = 0,
                HorizontalOptions = LayoutOptions.Center,
                Children = { circle, stepLabel }
            };

            _stepIndicator.Children.Add(stack);
            _stepElements.Add((circle, stepLabel, line!));
        }

        UpdateStepIndicator();
    }

    private void UpdateStepIndicator()
    {
        for (int i = 0; i < _stepElements.Count; i++)
        {
            var (circle, label, line) = _stepElements[i];
            var numberLabel = (Label)circle.Content;

            if (i + 1 < _currentStep)
            {
                // Completed
                circle.BackgroundColor = Green;
                circle.Stroke = Green;
                numberLabel.Text = "\u2713";
                numberLabel.TextColor = Colors.White;
                label.TextColor = Green;
            }
            else if (i + 1 == _currentStep)
            {
                // Current
                circle.BackgroundColor = Blue;
                circle.Stroke = Blue;
                numberLabel.Text = (i + 1).ToString();
                numberLabel.TextColor = Colors.White;
                label.TextColor = Blue;
                label.FontAttributes = FontAttributes.Bold;
            }
            else
            {
                // Future
                circle.BackgroundColor = GrayBg;
                circle.Stroke = GrayBorder;
                numberLabel.Text = (i + 1).ToString();
                numberLabel.TextColor = GrayText;
                label.TextColor = GrayText;
                label.FontAttributes = FontAttributes.None;
            }

            if (line != null)
                line.Color = (i + 1 <= _currentStep) ? Green : GrayBorder;
        }
    }

    private async void ShowStep(int step)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        try
        {
            await _contentArea.FadeTo(0, 120, Easing.CubicIn);

            _currentStep = step;
            _contentArea.Children.Clear();

            switch (step)
            {
                case 1: BuildFormatStep(); break;
                case 2: BuildDetailsStep(); break;
                case 3: BuildReviewStep(); break;
            }

            UpdateStepIndicator();
            _backBtn.IsVisible = step > 1;
            _nextBtn.Text = step == TotalSteps ? "\u2713  Create" : "Next  \u2192";
            _nextBtn.BackgroundColor = step == TotalSteps ? Green : Blue;

            await _contentArea.FadeTo(1, 180, Easing.CubicOut);
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    // ─── Step 1: Format ──────────────────────────────────────

    private void BuildFormatStep()
    {
        _contentArea.Children.Clear();
        _formatCards.Clear();
        _selectedFormatCard = null;

        _contentArea.Children.Add(new Label
        {
            Text = "Choose a competition format",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Dark,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        });
        _contentArea.Children.Add(new Label
        {
            Text = "Select the type of competition you want to create",
            FontSize = 14,
            TextColor = GrayText,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 12,
            RowSpacing = 12
        };

        var formats = new (CompetitionFormat format, string icon, string name, string desc, string? badge)[]
        {
            (CompetitionFormat.SinglesKnockout, "\U0001F3AF", "Singles Knockout", "1v1 elimination bracket", null),
            (CompetitionFormat.SinglesGroupStage, "\U0001F3C6", "Singles Groups + KO", "Group stage then knockout", "Popular"),
            (CompetitionFormat.DoublesKnockout, "\U0001F91D", "Doubles Knockout", "Pairs elimination bracket", null),
            (CompetitionFormat.DoublesGroupStage, "\U0001F46B", "Doubles Groups + KO", "Doubles with group stage", null),
            (CompetitionFormat.TeamKnockout, "\U0001F3E2", "Team Knockout", "Team vs team elimination", null),
            (CompetitionFormat.RoundRobin, "\U0001F504", "Round Robin", "Everyone plays everyone", null),
            (CompetitionFormat.Swiss, "\U0001F9E9", "Swiss System", "Paired by similar record", "Beta"),
        };

        for (int i = 0; i < formats.Length; i++)
        {
            var (format, icon, name, desc, badge) = formats[i];
            var card = BuildFormatCard(format, icon, name, desc, badge);
            grid.Add(card, i % 2, i / 2);
            _formatCards[format] = card;

            if (format == _selectedFormat)
            {
                _selectedFormatCard = card;
                ApplyCardSelection(card, true);
            }
        }

        _contentArea.Children.Add(grid);
    }

    private Border BuildFormatCard(CompetitionFormat format, string icon, string name, string desc, string? badge)
    {
        var iconLabel = new Label
        {
            Text = icon,
            FontSize = 28,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var nameLabel = new Label
        {
            Text = name,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Dark,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var descLabel = new Label
        {
            Text = desc,
            FontSize = 11,
            TextColor = GrayText,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var stack = new VerticalStackLayout
        {
            Spacing = 4,
            Padding = new Thickness(8, 14),
            HorizontalOptions = LayoutOptions.Center,
            Children = { iconLabel, nameLabel, descLabel }
        };

        // Badge
        if (!string.IsNullOrEmpty(badge))
        {
            var badgeColor = badge == "Popular" ? Green : Color.FromArgb("#F59E0B");
            var badgeLabel = new Label
            {
                Text = badge,
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                BackgroundColor = badgeColor,
                Padding = new Thickness(6, 2),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            // Wrap in a border for rounded corners
            var badgeBorder = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                StrokeThickness = 0,
                BackgroundColor = badgeColor,
                Padding = new Thickness(8, 2),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 2, 0, 0),
                Content = new Label
                {
                    Text = badge,
                    FontSize = 9,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White
                }
            };
            stack.Children.Add(badgeBorder);
        }

        var card = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Stroke = GrayBorder,
            StrokeThickness = 2,
            BackgroundColor = Colors.White,
            Padding = 0,
            Content = stack,
            MinimumHeightRequest = 130,
            Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Offset = new Point(0, 1), Radius = 4, Opacity = 0.05f }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            // Tap feedback
            await card.ScaleTo(0.96, 60, Easing.CubicIn);
            await card.ScaleTo(1.0, 80, Easing.CubicOut);
            SelectFormat(format, card);
        };
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private void SelectFormat(CompetitionFormat format, Border card)
    {
        if (_selectedFormatCard != null)
            ApplyCardSelection(_selectedFormatCard, false);

        _selectedFormat = format;
        _selectedFormatCard = card;
        ApplyCardSelection(card, true);

        if (!_nameManuallyEdited)
            _competitionName = FormatDisplayName(format);
    }

    private static void ApplyCardSelection(Border card, bool selected)
    {
        card.Stroke = selected ? Blue : GrayBorder;
        card.StrokeThickness = selected ? 2.5 : 2;
        card.BackgroundColor = selected ? SelectedBg : Colors.White;
    }

    // ─── Step 2: Details ─────────────────────────────────────

    private void BuildDetailsStep()
    {
        _contentArea.Children.Clear();

        _contentArea.Children.Add(new Label
        {
            Text = "Competition details",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Dark,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 8)
        });

        // Basic Info card
        _nameEntry = new Entry
        {
            Text = _competitionName,
            FontSize = 15,
            Placeholder = "e.g. Summer Singles 2025",
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing
        };
        _nameEntry.TextChanged += (_, e) =>
        {
            _competitionName = e.NewTextValue ?? "";
            _nameManuallyEdited = true;
        };

        _datePicker = new DatePicker
        {
            Date = _startDate,
            MinimumDate = DateTime.Today.AddMonths(-6),
            MaximumDate = DateTime.Today.AddYears(2),
            FontSize = 15
        };
        _datePicker.DateSelected += (_, e) => _startDate = e.NewDate;

        _contentArea.Children.Add(Card("\U0001F4CB  Basic Information", new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                Field("Competition Name", _nameEntry),
                Field("Start Date", _datePicker)
            }
        }));

        // Match Settings card
        var bestOfLayout = BuildBestOfChips();
        var randomSwitch = new Switch { IsToggled = _randomDraw, OnColor = Blue };
        randomSwitch.Toggled += (_, e) => _randomDraw = e.Value;

        _contentArea.Children.Add(Card("\u2699\uFE0F  Match Settings", new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                Field("Best Of (frames per match)", bestOfLayout),
                InlineField("Random Draw", randomSwitch)
            }
        }));

        // Group stage settings (only for group formats)
        if (_selectedFormat is CompetitionFormat.SinglesGroupStage or CompetitionFormat.DoublesGroupStage)
        {
            BuildGroupStageCard();
        }
    }

    private View BuildBestOfChips()
    {
        int[] options = [3, 5, 7, 9, 11, 13, 15];
        var layout = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
            AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center
        };

        foreach (var val in options)
        {
            var chipLabel = new Label
            {
                Text = val.ToString(),
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = val == _bestOf ? Colors.White : Dark,
                WidthRequest = 44,
                HeightRequest = 36
            };

            var chip = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Stroke = val == _bestOf ? Blue : GrayBorder,
                StrokeThickness = val == _bestOf ? 2 : 1,
                BackgroundColor = val == _bestOf ? Blue : Colors.White,
                Padding = 0,
                Margin = new Thickness(0, 0, 8, 8),
                Content = chipLabel
            };

            var capturedVal = val;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                _bestOf = capturedVal;
                // Refresh chips by rebuilding the details step
                BuildDetailsStep();
            };
            chip.GestureRecognizers.Add(tap);
            layout.Children.Add(chip);
        }

        var firstToLabel = new Label
        {
            Text = $"First to {(_bestOf + 1) / 2} wins",
            FontSize = 12,
            TextColor = GrayText,
            Margin = new Thickness(0, 4, 0, 0)
        };

        return new VerticalStackLayout { Spacing = 4, Children = { layout, firstToLabel } };
    }

    private void BuildGroupStageCard()
    {
        var advanceStepper = new Stepper { Minimum = 1, Maximum = 4, Increment = 1, Value = _topAdvance };
        var advanceLabel = new Label { Text = _topAdvance.ToString(), FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Dark, VerticalTextAlignment = TextAlignment.Center };
        advanceStepper.ValueChanged += (_, e) => { _topAdvance = (int)e.NewValue; advanceLabel.Text = _topAdvance.ToString(); };

        var plateSwitch = new Switch { IsToggled = _createPlate, OnColor = Blue };
        var allLosersSwitch = new Switch { IsToggled = _allLosersToPlate, OnColor = Blue };
        plateSwitch.Toggled += (_, e) => _createPlate = e.Value;
        allLosersSwitch.Toggled += (_, e) => _allLosersToPlate = e.Value;

        var groupFields = new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                new Label
                {
                    Text = "\u2139\uFE0F Number of groups will be configured after adding players, with recommended group sizes.",
                    FontSize = 12,
                    TextColor = Blue,
                    Margin = new Thickness(0, 0, 0, 4)
                },
                StepperField("Top players advance (per group)", advanceLabel, advanceStepper),
                InlineField("Create Plate Competition", plateSwitch),
                InlineField("All non-qualifiers to Plate", allLosersSwitch)
            }
        };

        _contentArea.Children.Add(Card("\U0001F3C6  Group Stage Settings", groupFields));
    }

    // ─── Step 3: Review ──────────────────────────────────────

    private void BuildReviewStep()
    {
        _contentArea.Children.Clear();

        _contentArea.Children.Add(new Label
        {
            Text = "Review & Create",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Dark,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 8)
        });

        var summaryContent = new VerticalStackLayout { Spacing = 12 };

        summaryContent.Children.Add(SummaryRow("\U0001F3AF", "Format", FormatDisplayName(_selectedFormat)));
        summaryContent.Children.Add(SummaryRow("\U0001F4DD", "Name", _competitionName));
        summaryContent.Children.Add(SummaryRow("\U0001F4C5", "Start Date", _startDate.ToString("dd MMMM yyyy")));
        summaryContent.Children.Add(SummaryRow("\U0001F3B1", "Best Of", $"{_bestOf} frames (first to {(_bestOf + 1) / 2})"));
        summaryContent.Children.Add(SummaryRow("\U0001F3B2", "Draw", _randomDraw ? "Random" : "Manual order"));

        if (_selectedFormat is CompetitionFormat.SinglesGroupStage or CompetitionFormat.DoublesGroupStage)
        {
            summaryContent.Children.Add(new BoxView { HeightRequest = 1, Color = GrayBorder, Margin = new Thickness(0, 4) });
            summaryContent.Children.Add(SummaryRow("\U0001F4CA", "Groups", "Set after adding players"));
            summaryContent.Children.Add(SummaryRow("\u2B06\uFE0F", "Advance", $"Top {_topAdvance} per group"));
            summaryContent.Children.Add(SummaryRow("\U0001F3C5", "Plate", _createPlate ? "Yes" : "No"));
        }

        _contentArea.Children.Add(Card("\U0001F4CB  Competition Summary", summaryContent));

        // Hint card
        var hintContent = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = "\U0001F4A1  What happens next?", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = BlueDark },
                new Label { Text = "After creating the competition, you'll be taken to the editor where you can:", FontSize = 13, TextColor = Dark },
                new Label { Text = "  \u2022  Add participants (players/teams)", FontSize = 13, TextColor = GrayText },
                new Label { Text = "  \u2022  Generate the draw or brackets", FontSize = 13, TextColor = GrayText },
                new Label { Text = "  \u2022  Manage scores and progression", FontSize = 13, TextColor = GrayText }
            }
        };

        var hintCard = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Stroke = Color.FromArgb("#BFDBFE"),
            StrokeThickness = 1,
            BackgroundColor = Color.FromArgb("#EFF6FF"),
            Padding = new Thickness(18, 16),
            Margin = new Thickness(0, 4, 0, 0),
            Content = hintContent
        };
        _contentArea.Children.Add(hintCard);
    }

    // ─── Navigation ──────────────────────────────────────────

    private void GoBack()
    {
        if (_currentStep > 1)
            ShowStep(_currentStep - 1);
    }

    private async void GoNext()
    {
        if (_isTransitioning) return;

        if (_currentStep == 2)
        {
            // Validate name
            if (string.IsNullOrWhiteSpace(_competitionName))
            {
                await DisplayAlert("Missing Name", "Please enter a competition name.", "OK");
                _nameEntry?.Focus();
                return;
            }
        }

        if (_currentStep < TotalSteps)
        {
            ShowStep(_currentStep + 1);
        }
        else
        {
            // Create competition
            var competition = BuildCompetition();
            await Navigation.PopModalAsync();
            _result.TrySetResult(competition);
        }
    }

    private Competition BuildCompetition()
    {
        var comp = new Competition
        {
            Id = Guid.NewGuid(),
            SeasonId = _seasonId,
            Name = _competitionName.Trim(),
            Format = _selectedFormat,
            Status = CompetitionStatus.Draft,
            StartDate = _startDate,
            BestOf = _bestOf,
            RandomDraw = _randomDraw,
            CreatedDate = DateTime.Now
        };

        if (_selectedFormat is CompetitionFormat.SinglesGroupStage or CompetitionFormat.DoublesGroupStage)
        {
            comp.GroupSettings = new GroupStageSettings
            {
                NumberOfGroups = _numberOfGroups,
                TopPlayersAdvance = _topAdvance,
                LowerPlayersToPlate = _lowerToPlate,
                AllLosersToPlate = _allLosersToPlate,
                CreatePlateCompetition = _createPlate,
                PlateNameSuffix = "Plate"
            };
        }

        return comp;
    }

    // ─── UI Helpers ──────────────────────────────────────────

    private static View Field(string label, View control)
    {
        return new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = label, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = GrayText },
                control
            }
        };
    }

    private static View InlineField(string label, View control)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            VerticalOptions = LayoutOptions.Center
        };
        grid.Add(new Label
        {
            Text = label,
            FontSize = 14,
            TextColor = Dark,
            VerticalTextAlignment = TextAlignment.Center
        }, 0, 0);
        grid.Add(control, 1, 0);
        return grid;
    }

    private static View StepperField(string label, Label valueLabel, Stepper stepper)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            VerticalOptions = LayoutOptions.Center
        };
        row.Add(new Label { Text = label, FontSize = 14, TextColor = Dark, VerticalTextAlignment = TextAlignment.Center }, 0, 0);
        row.Add(valueLabel, 1, 0);
        row.Add(stepper, 2, 0);
        return row;
    }

    private static Border Card(string title, View content)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Dark },
                content
            }
        };

        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Stroke = GrayBorder,
            StrokeThickness = 1,
            BackgroundColor = Colors.White,
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 4),
            Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Offset = new Point(0, 2), Radius = 8, Opacity = 0.04f },
            Content = stack
        };
    }

    private static View SummaryRow(string icon, string label, string value)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(32, GridUnitType.Absolute)),
                new ColumnDefinition(new GridLength(110, GridUnitType.Absolute)),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 4
        };
        grid.Add(new Label { Text = icon, FontSize = 16, VerticalTextAlignment = TextAlignment.Center }, 0, 0);
        grid.Add(new Label { Text = label, FontSize = 14, TextColor = GrayText, VerticalTextAlignment = TextAlignment.Center }, 1, 0);
        grid.Add(new Label { Text = value, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Dark, VerticalTextAlignment = TextAlignment.Center }, 2, 0);
        return grid;
    }

    private static string FormatDisplayName(CompetitionFormat format) => format switch
    {
        CompetitionFormat.SinglesKnockout => "Singles Knockout",
        CompetitionFormat.DoublesKnockout => "Doubles Knockout",
        CompetitionFormat.TeamKnockout => "Team Knockout",
        CompetitionFormat.RoundRobin => "Round Robin",
        CompetitionFormat.Swiss => "Swiss System",
        CompetitionFormat.SinglesGroupStage => "Singles Groups + KO",
        CompetitionFormat.DoublesGroupStage => "Doubles Groups + KO",
        _ => format.ToString()
    };
}
