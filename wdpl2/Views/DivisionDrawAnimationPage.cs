using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
#if WINDOWS
using Microsoft.UI.Xaml.Input;
#endif

namespace Wdpl2.Views;

/// <summary>
/// Animated division draw — shows a spinning wheel of team names,
/// draws each team one by one and places them into divisions with visual flair.
/// Modelled on <see cref="GroupDrawAnimationPage"/>.
/// </summary>
public class DivisionDrawAnimationPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _result = new();
    private readonly List<string> _teamNames;
    private readonly List<List<string>> _divisionAssignments;
    private readonly List<string> _divisionNames;

    // UI elements
    private readonly VerticalStackLayout _wheelContainer = new() { Spacing = 0 };
    private readonly Grid _divisionsGrid = new();
    private readonly Label _statusLabel = new() { FontSize = 18, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, TextColor = Colors.White };
    private readonly Label _currentPickLabel = new() { FontSize = 28, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, TextColor = Color.FromArgb("#FFD700"), Opacity = 0 };
    private readonly Button _drawBtn = new() { Text = "▶ Start Draw", BackgroundColor = Color.FromArgb("#10B981"), TextColor = Colors.White, Padding = new Thickness(24, 12), FontSize = 16, CornerRadius = 8 };
    private readonly Button _closeBtn = new() { Text = "✕ Close", BackgroundColor = Color.FromArgb("#EF4444"), TextColor = Colors.White, Padding = new Thickness(16, 8), FontSize = 13, CornerRadius = 8, IsVisible = false };

    private readonly List<Label> _wheelLabels = [];
    private readonly List<VerticalStackLayout> _divisionColumns = [];
    private ScrollView? _wheelScroll;

    private bool _isAnimating;
    private bool _skipRequested;

    private static readonly string[] DivisionColors =
    [
        "#3B82F6", "#10B981", "#F59E0B", "#EF4444",
        "#8B5CF6", "#EC4899", "#06B6D4", "#F97316",
        "#84CC16", "#6366F1", "#14B8A6", "#E11D48"
    ];

    private static readonly Dictionary<string, string> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["red"] = "#EF4444",
        ["blue"] = "#3B82F6",
        ["green"] = "#10B981",
        ["yellow"] = "#EAB308",
        ["orange"] = "#F97316",
        ["purple"] = "#8B5CF6",
        ["pink"] = "#EC4899",
        ["teal"] = "#14B8A6",
        ["cyan"] = "#06B6D4",
        ["gold"] = "#F59E0B",
        ["silver"] = "#94A3B8",
        ["black"] = "#334155",
        ["white"] = "#E2E8F0",
        ["maroon"] = "#991B1B",
        ["navy"] = "#1E3A5F",
        ["lime"] = "#84CC16",
        ["indigo"] = "#6366F1",
        ["violet"] = "#7C3AED",
        ["crimson"] = "#DC2626",
        ["coral"] = "#FB923C",
        ["brown"] = "#92400E",
        ["scarlet"] = "#DC2626",
        ["amber"] = "#F59E0B",
    };

    public DivisionDrawAnimationPage(
        List<string> teamNames,
        List<string> divisionNames,
        List<List<string>> divisionAssignments)
    {
        _teamNames = teamNames;
        _divisionNames = divisionNames;
        _divisionAssignments = divisionAssignments;

        Title = "Division Draw";
        BackgroundColor = Color.FromArgb("#0F172A");
        Shell.SetNavBarIsVisible(this, false);

        _drawBtn.Clicked += async (_, _) => await RunDrawAnimation();
        _closeBtn.Clicked += async (_, _) =>
        {
            await Navigation.PopModalAsync();
            _result.TrySetResult(true);
        };

        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
        BuildLayout();
    }

    public Task<bool> GetResultAsync() => _result.Task;

    private void OnPageLoaded(object? sender, EventArgs e)
    {
#if WINDOWS
        if (Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            nativeWindow.Content.KeyDown += OnNativeKeyDown;
#endif
    }

    private void OnPageUnloaded(object? sender, EventArgs e)
    {
#if WINDOWS
        if (Window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window nativeWindow)
            nativeWindow.Content.KeyDown -= OnNativeKeyDown;
#endif
    }

#if WINDOWS
    private void OnNativeKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (_isAnimating)
            {
                _skipRequested = true;
            }
            else if (_closeBtn.IsVisible)
            {
                Dispatcher.Dispatch(async () =>
                {
                    await Navigation.PopModalAsync();
                    _result.TrySetResult(true);
                });
            }
            e.Handled = true;
        }
    }
#endif

    private void BuildLayout()
    {
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            },
            Padding = 16,
            RowSpacing = 12,
            BackgroundColor = Color.FromArgb("#0F172A")
        };

        // ── Top: Title + Status ──
        var header = new VerticalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "🎱 DIVISION DRAW",
                    FontSize = 28,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                _statusLabel,
                _currentPickLabel
            }
        };
        root.Add(header, 0, 0);

        // ── Middle: Wheel (left) | Divisions (right) ──
        var middleGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }
            },
            ColumnSpacing = 16
        };

        // Wheel panel
        var wheelBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Stroke = Color.FromArgb("#334155"),
            BackgroundColor = Color.FromArgb("#1E293B"),
            Padding = 0,
            HeightRequest = 360
        };

        var wheelOverlay = new Grid { HeightRequest = 360 };

        _wheelScroll = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = _wheelContainer,
            InputTransparent = true
        };
        _wheelContainer.Padding = new Thickness(8, 0);
        wheelOverlay.Children.Add(_wheelScroll);

        // Selection highlight
        var selectionHighlight = new Border
        {
            HeightRequest = 44,
            BackgroundColor = Color.FromArgb("#3B82F6"),
            Opacity = 0.3,
            VerticalOptions = LayoutOptions.Center,
            StrokeThickness = 0,
            InputTransparent = true
        };
        wheelOverlay.Children.Add(selectionHighlight);

        // Arrows
        wheelOverlay.Children.Add(new Label { Text = "▶", FontSize = 20, TextColor = Color.FromArgb("#FFD700"), VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Start, Margin = new Thickness(4, 0, 0, 0), InputTransparent = true });
        wheelOverlay.Children.Add(new Label { Text = "◀", FontSize = 20, TextColor = Color.FromArgb("#FFD700"), VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 4, 0), InputTransparent = true });

        wheelBorder.Content = wheelOverlay;

        var wheelPanel = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Teams", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center },
                wheelBorder
            }
        };
        middleGrid.Add(wheelPanel, 0, 0);

        // Divisions panel
        BuildDivisionsGrid();
        var divisionsPanel = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Divisions", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center },
                new ScrollView
                {
                    Content = _divisionsGrid,
                    Orientation = ScrollOrientation.Both
                }
            }
        };
        middleGrid.Add(divisionsPanel, 1, 0);

        root.Add(middleGrid, 0, 1);

        // ── Bottom: Buttons ──
        var buttonBar = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            Children = { _drawBtn, _closeBtn }
        };
        root.Add(buttonBar, 0, 2);

        PopulateWheel();
        _statusLabel.Text = $"{_teamNames.Count} teams → {_divisionNames.Count} divisions";

        Content = root;
    }

    private void BuildDivisionsGrid()
    {
        int cols = Math.Min(_divisionNames.Count, 4);
        int rows = (int)Math.Ceiling(_divisionNames.Count / (double)cols);

        for (int c = 0; c < cols; c++)
            _divisionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        for (int r = 0; r < rows; r++)
            _divisionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _divisionsGrid.ColumnSpacing = 8;
        _divisionsGrid.RowSpacing = 8;

        for (int i = 0; i < _divisionNames.Count; i++)
        {
            var colorHex = GetDivisionColor(_divisionNames[i], i);
            var divStack = new VerticalStackLayout
            {
                Spacing = 4,
                Padding = 8
            };

            var divBorder = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Stroke = Color.FromArgb(colorHex),
                BackgroundColor = Color.FromArgb("#1E293B"),
                Padding = 0,
                Content = new VerticalStackLayout
                {
                    Children =
                    {
                        new Border
                        {
                            BackgroundColor = Color.FromArgb(colorHex),
                            Padding = new Thickness(8, 6),
                            StrokeThickness = 0,
                            Content = new Label
                            {
                                Text = _divisionNames[i],
                                FontSize = 14,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Colors.White,
                                HorizontalTextAlignment = TextAlignment.Center
                            }
                        },
                        divStack
                    }
                }
            };

            _divisionColumns.Add(divStack);

            int col = i % cols;
            int row = i / cols;
            _divisionsGrid.Add(divBorder, col, row);
        }
    }

    private void PopulateWheel()
    {
        _wheelContainer.Children.Clear();
        _wheelLabels.Clear();

        var wheelItems = new List<string>();
        for (int rep = 0; rep < 3; rep++)
            wheelItems.AddRange(_teamNames.OrderBy(_ => Random.Shared.Next()));

        foreach (var name in wheelItems)
        {
            var lbl = new Label
            {
                Text = name,
                FontSize = 18,
                TextColor = Color.FromArgb("#94A3B8"),
                HorizontalTextAlignment = TextAlignment.Center,
                Padding = new Thickness(12, 10),
                HeightRequest = 44
            };
            _wheelLabels.Add(lbl);
            _wheelContainer.Children.Add(lbl);
        }
    }

    private async Task RunDrawAnimation()
    {
        if (_isAnimating) return;
        _isAnimating = true;
        _skipRequested = false;

        _drawBtn.IsEnabled = false;
        _drawBtn.Text = "Drawing...";
        _closeBtn.IsVisible = false;

        // Build draw order: round-robin across divisions
        var drawOrder = new List<(int divIndex, string teamName)>();
        int maxPerDiv = _divisionAssignments.Max(g => g.Count);
        for (int pick = 0; pick < maxPerDiv; pick++)
        {
            for (int d = 0; d < _divisionAssignments.Count; d++)
            {
                if (pick < _divisionAssignments[d].Count)
                    drawOrder.Add((d, _divisionAssignments[d][pick]));
            }
        }

        await Task.Delay(100);

        int drawnCount = 0;
        foreach (var (divIndex, teamName) in drawOrder)
        {
            if (_skipRequested) break;

            drawnCount++;
            var colorHex = GetDivisionColor(_divisionNames[divIndex], divIndex);

            _statusLabel.Text = $"Drawing team {drawnCount} of {_teamNames.Count}...";

            if (!_skipRequested)
            {
                await SpinWheelToTeam(teamName);

                if (!_skipRequested)
                {
                    _currentPickLabel.Text = teamName;
                    _currentPickLabel.Opacity = 0;
                    await _currentPickLabel.FadeTo(1, 150);
                    await Task.Delay(300);
                }
            }

            // Add to division column
            var teamLabel = new Label
            {
                Text = teamName,
                FontSize = 13,
                TextColor = Colors.White,
                Padding = new Thickness(6, 4),
                BackgroundColor = Color.FromArgb(colorHex),
                Opacity = _skipRequested ? 1 : 0
            };

            var teamBorder = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 4 },
                StrokeThickness = 0,
                Padding = 0,
                Content = teamLabel,
                Opacity = _skipRequested ? 1 : 0
            };

            _divisionColumns[divIndex].Children.Add(teamBorder);

            if (!_skipRequested)
            {
                await teamBorder.FadeTo(1, 200);
                teamLabel.Opacity = 1;
                _currentPickLabel.Opacity = 0;
            }
        }

        // If skipped, fill remaining instantly
        if (_skipRequested)
        {
            for (int i = drawnCount; i < drawOrder.Count; i++)
            {
                var (dIdx, tName) = drawOrder[i];
                var cHex = GetDivisionColor(_divisionNames[dIdx], dIdx);
                _divisionColumns[dIdx].Children.Add(new Border
                {
                    StrokeShape = new RoundRectangle { CornerRadius = 4 },
                    StrokeThickness = 0,
                    Padding = 0,
                    Content = new Label
                    {
                        Text = tName,
                        FontSize = 13,
                        TextColor = Colors.White,
                        Padding = new Thickness(6, 4),
                        BackgroundColor = Color.FromArgb(cHex)
                    }
                });
            }
        }

        _statusLabel.Text = $"✅ Draw complete! {_teamNames.Count} teams in {_divisionNames.Count} divisions";
        _currentPickLabel.Text = "";
        _currentPickLabel.Opacity = 0;
        _drawBtn.IsVisible = false;
        _closeBtn.IsVisible = true;
        _isAnimating = false;
    }

    private async Task SpinWheelToTeam(string targetTeam)
    {
        if (_wheelScroll == null || _skipRequested) return;

        int targetIndex = -1;
        int midStart = _wheelLabels.Count / 3;
        for (int i = midStart; i < _wheelLabels.Count; i++)
        {
            if (_wheelLabels[i].Text == targetTeam)
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex < 0) return;

        var targetLabel = _wheelLabels[targetIndex];

        double itemHeight = 44;
        double targetScrollY = targetIndex * itemHeight;
        double currentScrollY = _wheelScroll.ScrollY;

        int spinSteps = 8;
        double spinDistance = Math.Max(targetScrollY - currentScrollY, spinSteps * itemHeight * 2);
        double spinStartY = targetScrollY - spinDistance;
        if (spinStartY < 0) spinStartY = 0;

        for (int step = 0; step < spinSteps && !_skipRequested; step++)
        {
            double t = (double)(step + 1) / spinSteps;
            double ease = 1 - Math.Pow(1 - t, 2);
            double scrollY = spinStartY + (targetScrollY - spinStartY) * ease;

            scrollY = Math.Max(0, Math.Min(scrollY, _wheelScroll.ContentSize.Height - _wheelScroll.Height));

            await _wheelScroll.ScrollToAsync(0, scrollY, false);
            int delay = step < 3 ? 40 : step < 6 ? 80 : 120;
            await Task.Delay(delay);
        }

        if (_skipRequested) return;

        await _wheelScroll.ScrollToAsync(targetLabel, ScrollToPosition.Center, true);

        targetLabel.TextColor = Color.FromArgb("#FFD700");
        targetLabel.FontAttributes = FontAttributes.Bold;
    }

    private static string GetDivisionColor(string divisionName, int index)
    {
        foreach (var (colorName, hex) in NamedColors)
        {
            if (divisionName.Contains(colorName, StringComparison.OrdinalIgnoreCase))
                return hex;
        }
        return DivisionColors[index % DivisionColors.Length];
    }

    protected override bool OnBackButtonPressed()
    {
        if (_isAnimating)
        {
            _skipRequested = true;
            return true;
        }
        _result.TrySetResult(false);
        return base.OnBackButtonPressed();
    }
}
