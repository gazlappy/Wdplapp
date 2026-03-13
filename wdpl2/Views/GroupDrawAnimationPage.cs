using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace Wdpl2.Views;

/// <summary>
/// Animated group draw ceremony — shows a spinning wheel of player names,
/// draws each player one by one and places them into groups with visual flair.
/// Designed to be shown to an audience during a live draw event.
/// </summary>
public class GroupDrawAnimationPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _result = new();
    private readonly List<string> _playerNames;
    private readonly List<List<string>> _groupAssignments;
    private readonly int _numberOfGroups;

    // UI elements
    private readonly VerticalStackLayout _wheelContainer = new() { Spacing = 0 };
    private readonly Grid _groupsGrid = new();
    private readonly Label _statusLabel = new() { FontSize = 18, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, TextColor = Colors.White };
    private readonly Label _currentPickLabel = new() { FontSize = 28, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, TextColor = Color.FromArgb("#FFD700"), Opacity = 0 };
    private readonly Button _drawBtn = new() { Text = "▶ Start Draw", BackgroundColor = Color.FromArgb("#10B981"), TextColor = Colors.White, Padding = new Thickness(24, 12), FontSize = 16, CornerRadius = 8 };
    private readonly Button _skipBtn = new() { Text = "Skip Animation", BackgroundColor = Color.FromArgb("#6B7280"), TextColor = Colors.White, Padding = new Thickness(16, 8), FontSize = 13, CornerRadius = 8 };
    private readonly Button _closeBtn = new() { Text = "✕ Close", BackgroundColor = Color.FromArgb("#EF4444"), TextColor = Colors.White, Padding = new Thickness(16, 8), FontSize = 13, CornerRadius = 8, IsVisible = false };

    // Wheel items for animation
    private readonly List<Label> _wheelLabels = [];
    private readonly List<VerticalStackLayout> _groupColumns = [];
    private ScrollView? _wheelScroll;

    private bool _isAnimating;
    private bool _skipRequested;
    private int _currentGroupIndex;

    // Colors for groups
    private static readonly string[] GroupColors =
    [
        "#3B82F6", "#10B981", "#F59E0B", "#EF4444",
        "#8B5CF6", "#EC4899", "#06B6D4", "#F97316",
        "#84CC16", "#6366F1", "#14B8A6", "#E11D48"
    ];

    public GroupDrawAnimationPage(List<string> playerNames, List<List<string>> groupAssignments, int numberOfGroups)
    {
        _playerNames = playerNames;
        _groupAssignments = groupAssignments;
        _numberOfGroups = numberOfGroups;

        Title = "Group Draw";
        BackgroundColor = Color.FromArgb("#0F172A");
        Shell.SetNavBarIsVisible(this, false);

        _drawBtn.Clicked += async (_, _) => await RunDrawAnimation();
        _skipBtn.Clicked += (_, _) => _skipRequested = true;
        _closeBtn.Clicked += async (_, _) =>
        {
            await Navigation.PopModalAsync();
            _result.TrySetResult(true);
        };

        BuildLayout();
    }

    public Task<bool> GetResultAsync() => _result.Task;

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
                    Text = "🎱 GROUP DRAW",
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

        // ── Middle: Split — Wheel (left) | Groups (right) ──
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

        // Overlay grid: ScrollView behind, highlight + arrows on top
        var wheelOverlay = new Grid { HeightRequest = 360 };

        // Scrollable wheel (user interaction disabled — we control it programmatically)
        _wheelScroll = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = _wheelContainer,
            InputTransparent = true
        };
        _wheelContainer.Padding = new Thickness(8, 0);
        wheelOverlay.Children.Add(_wheelScroll);

        // Selection indicator — fixed center highlight
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

        // Selection arrows
        var leftArrow = new Label { Text = "▶", FontSize = 20, TextColor = Color.FromArgb("#FFD700"), VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Start, Margin = new Thickness(4, 0, 0, 0), InputTransparent = true };
        var rightArrow = new Label { Text = "◀", FontSize = 20, TextColor = Color.FromArgb("#FFD700"), VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 4, 0), InputTransparent = true };
        wheelOverlay.Children.Add(leftArrow);
        wheelOverlay.Children.Add(rightArrow);

        wheelBorder.Content = wheelOverlay;

        // Remaining players label
        var wheelPanel = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Players", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center },
                wheelBorder
            }
        };
        middleGrid.Add(wheelPanel, 0, 0);

        // Groups panel
        BuildGroupsGrid();
        var groupsPanel = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Groups", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center },
                new ScrollView
                {
                    Content = _groupsGrid,
                    Orientation = ScrollOrientation.Both
                }
            }
        };
        middleGrid.Add(groupsPanel, 1, 0);

        root.Add(middleGrid, 0, 1);

        // ── Bottom: Buttons ──
        var buttonBar = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            Children = { _drawBtn, _skipBtn, _closeBtn }
        };
        root.Add(buttonBar, 0, 2);

        // Populate the wheel with shuffled names
        PopulateWheel();

        _statusLabel.Text = $"{_playerNames.Count} players → {_numberOfGroups} groups";

        Content = root;
    }

    private void BuildGroupsGrid()
    {
        int cols = Math.Min(_numberOfGroups, 4);
        int rows = (int)Math.Ceiling(_numberOfGroups / (double)cols);

        for (int c = 0; c < cols; c++)
            _groupsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        for (int r = 0; r < rows; r++)
            _groupsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _groupsGrid.ColumnSpacing = 8;
        _groupsGrid.RowSpacing = 8;

        for (int i = 0; i < _numberOfGroups; i++)
        {
            var colorHex = GroupColors[i % GroupColors.Length];
            var groupStack = new VerticalStackLayout
            {
                Spacing = 4,
                Padding = 8
            };

            var groupBorder = new Border
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
                                Text = $"Group {(char)('A' + i)}",
                                FontSize = 14,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Colors.White,
                                HorizontalTextAlignment = TextAlignment.Center
                            }
                        },
                        groupStack
                    }
                }
            };

            _groupColumns.Add(groupStack);

            int col = i % cols;
            int row = i / cols;
            _groupsGrid.Add(groupBorder, col, row);
        }
    }

    private void PopulateWheel()
    {
        _wheelContainer.Children.Clear();
        _wheelLabels.Clear();

        // Fill wheel with repeated shuffled names for scrolling effect.
        // Keep it to 3 repetitions to avoid layout pressure.
        var wheelItems = new List<string>();
        for (int rep = 0; rep < 3; rep++)
            wheelItems.AddRange(_playerNames.OrderBy(_ => Random.Shared.Next()));

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
        _skipBtn.IsVisible = true;
        _closeBtn.IsVisible = false;

        // Build the draw order: round-robin across groups (group 0 pick 1, group 1 pick 1, ..., group 0 pick 2, ...)
        var drawOrder = new List<(int groupIndex, string playerName)>();
        int maxPerGroup = _groupAssignments.Max(g => g.Count);
        for (int pick = 0; pick < maxPerGroup; pick++)
        {
            for (int g = 0; g < _groupAssignments.Count; g++)
            {
                if (pick < _groupAssignments[g].Count)
                    drawOrder.Add((g, _groupAssignments[g][pick]));
            }
        }

        // Wait for layout to settle so ScrollToAsync has valid positions
        await Task.Delay(100);

        int drawnCount = 0;
        foreach (var (groupIndex, playerName) in drawOrder)
        {
            if (_skipRequested) break;

            drawnCount++;
            _currentGroupIndex = groupIndex;
            var colorHex = GroupColors[groupIndex % GroupColors.Length];

            _statusLabel.Text = $"Drawing player {drawnCount} of {_playerNames.Count}...";

            if (!_skipRequested)
            {
                // Spin the wheel
                await SpinWheelToPlayer(playerName);

                if (!_skipRequested)
                {
                    // Flash the picked name
                    _currentPickLabel.Text = playerName;
                    _currentPickLabel.Opacity = 0;
                    await _currentPickLabel.FadeTo(1, 150);
                    await Task.Delay(300);
                }
            }

            // Add to group
            var playerLabel = new Label
            {
                Text = playerName,
                FontSize = 13,
                TextColor = Colors.White,
                Padding = new Thickness(6, 4),
                BackgroundColor = Color.FromArgb(colorHex),
                Opacity = _skipRequested ? 1 : 0
            };

            var playerBorder = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 4 },
                StrokeThickness = 0,
                Padding = 0,
                Content = playerLabel,
                Opacity = _skipRequested ? 1 : 0
            };

            _groupColumns[groupIndex].Children.Add(playerBorder);

            if (!_skipRequested)
            {
                await playerBorder.FadeTo(1, 200);
                playerLabel.Opacity = 1;
                _currentPickLabel.Opacity = 0;
            }
        }

        // If skipped, fill in any remaining players instantly
        if (_skipRequested)
        {
            for (int i = drawnCount; i < drawOrder.Count; i++)
            {
                var (gIdx, pName) = drawOrder[i];
                var cHex = GroupColors[gIdx % GroupColors.Length];
                _groupColumns[gIdx].Children.Add(new Border
                {
                    StrokeShape = new RoundRectangle { CornerRadius = 4 },
                    StrokeThickness = 0,
                    Padding = 0,
                    Content = new Label
                    {
                        Text = pName,
                        FontSize = 13,
                        TextColor = Colors.White,
                        Padding = new Thickness(6, 4),
                        BackgroundColor = Color.FromArgb(cHex)
                    }
                });
            }
        }

        // Done
        _statusLabel.Text = $"✅ Draw complete! {_playerNames.Count} players in {_numberOfGroups} groups";
        _currentPickLabel.Text = "";
        _currentPickLabel.Opacity = 0;
        _drawBtn.IsVisible = false;
        _skipBtn.IsVisible = false;
        _closeBtn.IsVisible = true;
        _isAnimating = false;
    }

    private async Task SpinWheelToPlayer(string targetPlayer)
    {
        if (_wheelScroll == null || _skipRequested) return;

        // Find the target label in the second third of the wheel so there's scroll distance
        int targetIndex = -1;
        int midStart = _wheelLabels.Count / 3;
        for (int i = midStart; i < _wheelLabels.Count; i++)
        {
            if (_wheelLabels[i].Text == targetPlayer)
            {
                targetIndex = i;
                break;
            }
        }

        if (targetIndex < 0) return;

        var targetLabel = _wheelLabels[targetIndex];

        // Use coordinate-based scrolling for the spin effect.
        // This avoids expensive per-element layout measurements that cause UI lockups.
        double itemHeight = 44;
        double targetScrollY = targetIndex * itemHeight;
        double currentScrollY = _wheelScroll.ScrollY;

        // Spin: quick jumps through 8 intermediate positions, decelerating
        int spinSteps = 8;
        double spinDistance = Math.Max(targetScrollY - currentScrollY, spinSteps * itemHeight * 2);
        double spinStartY = targetScrollY - spinDistance;
        if (spinStartY < 0) spinStartY = 0;

        for (int step = 0; step < spinSteps && !_skipRequested; step++)
        {
            double t = (double)(step + 1) / spinSteps;
            double ease = 1 - Math.Pow(1 - t, 2); // ease-out quadratic
            double scrollY = spinStartY + (targetScrollY - spinStartY) * ease;

            // Clamp to valid range
            scrollY = Math.Max(0, Math.Min(scrollY, _wheelScroll.ContentSize.Height - _wheelScroll.Height));

            await _wheelScroll.ScrollToAsync(0, scrollY, false);
            // Decelerate: fast at start, slow near end
            int delay = step < 3 ? 40 : step < 6 ? 80 : 120;
            await Task.Delay(delay);
        }

        if (_skipRequested) return;

        // Final scroll: use element-based to guarantee alignment, with smooth animation
        await _wheelScroll.ScrollToAsync(targetLabel, ScrollToPosition.Center, true);

        // Highlight — change color only, don't change FontSize (avoids layout invalidation)
        targetLabel.TextColor = Color.FromArgb("#FFD700");
        targetLabel.FontAttributes = FontAttributes.Bold;
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
