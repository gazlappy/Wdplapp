using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using Wdpl2.Helpers;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views
{
    public partial class SettingsPage : ContentPage
    {
        private Guid? _editingSeasonId;
        private AppSettings Settings => _editingSeasonId.HasValue
            ? DataStore.Data.GetSettingsForSeason(_editingSeasonId)
            : DataStore.Data.Settings;

        /// <summary>
        /// Returns true when we are editing a season-specific override (not the global defaults).
        /// </summary>
        private bool IsEditingSeasonOverride =>
            _editingSeasonId.HasValue &&
            DataStore.Data.Seasons.FirstOrDefault(s => s.Id == _editingSeasonId.Value)?.Settings != null;

        // Theme-aware colors
        private static Color InfoBoxBackground => ThemeService.Current.IsDarkModeActive 
            ? Color.FromArgb("#1E3A5F") 
            : Color.FromArgb("#F0F9FF");
        
        private static Color InfoBoxText => ThemeService.Current.IsDarkModeActive 
            ? Colors.White 
            : Colors.Black;
            
        private static Color SubtitleText => ThemeService.Current.IsDarkModeActive 
            ? Color.FromArgb("#9CA3AF") 
            : Color.FromArgb("#666666");
            
        private static Color CardBackground => ThemeService.Current.IsDarkModeActive 
            ? Color.FromArgb("#1F2937") 
            : Color.FromArgb("#FAFAFA");
            
        private static Color CardBorder => ThemeService.Current.IsDarkModeActive 
            ? Color.FromArgb("#374151") 
            : Color.FromArgb("#E5E7EB");
            
        private static Color WarningBoxBackground => ThemeService.Current.IsDarkModeActive 
            ? Color.FromArgb("#422006") 
            : Color.FromArgb("#FFFBEB");
            
        private static Color SuccessBoxBackground => ThemeService.Current.IsDarkModeActive 
            ? Color.FromArgb("#052E16") 
            : Color.FromArgb("#F0FDF4");
            
        private static Color ErrorBoxBackground => ThemeService.Current.IsDarkModeActive 
            ? Color.FromArgb("#450A0A") 
            : Color.FromArgb("#FEF2F2");

        private readonly ObservableCollection<string> _categories = new()
        {
            "Appearance",
            "Player Ratings",
            "Match Scoring",
            "Fixture Defaults",
            "Notifications",
            "Division Management",
            "Data Management",
            "About"
        };

        // UI Elements (created programmatically)
        private Entry? _startingRatingEntry;
        private Entry? _ratingWeightingEntry;
        private Entry? _ratingsBiasEntry;
        private Entry? _winFactorEntry;
        private Entry? _lossFactorEntry;
        private Entry? _eightBallFactorEntry;
        private Switch? _useEightBallSwitch;
        private Entry? _minFramesEntry;
        private Entry? _pointsForWinEntry;
        private Entry? _pointsForDrawEntry;
        private Entry? _framesPerMatchEntry;
        private Picker? _matchDayPicker;
        private TimePicker? _matchTimePicker;
        private Entry? _roundsPerOpponentEntry;
        private VerticalStackLayout? _tiebreakerListLayout;
        private Label? _statusLabel;

        public SettingsPage()
        {
            InitializeComponent();

            CategoriesList.ItemsSource = _categories;

            // Select first category by default
            CategoriesList.SelectedItem = _categories.First();

            // Don't use the responsive layout handler since XAML defines fixed columns
            // SizeChanged += (_, __) => ApplyResponsiveLayout(Width);
        }

        private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = e.CurrentSelection?.FirstOrDefault() as string;
            ShowCategory(selected);
        }

        private void ShowCategory(string? category)
        {
            View? content = category switch
            {
                "Appearance" => CreateAppearancePanel(),
                "Player Ratings" => CreatePlayerRatingsPanel(),
                "Match Scoring" => CreateMatchScoringPanel(),
                "Fixture Defaults" => CreateFixtureDefaultsPanel(),
                "Notifications" => CreateNotificationsPanel(),
                "Division Management" => CreateDivisionManagementPanel(),
                "Data Management" => CreateDataManagementPanel(),
                "About" => CreateAboutPanel(),
                _ => null
            };

            ContentPanel.Content = content;
        }

        private View CreateAppearancePanel()
        {
            var useSystemThemeSwitch = new Switch { IsToggled = Settings.UseSystemTheme };
            var darkModeSwitch = new Switch { IsToggled = Settings.DarkModeEnabled, IsEnabled = !Settings.UseSystemTheme };
            var statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            var grid = new Grid 
            { 
                ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = 80 } }, 
                RowSpacing = 16 
            };

            // Theme Icon/Header
            var headerStack = new HorizontalStackLayout { Spacing = 8 };
            headerStack.Children.Add(new Label { Text = Emojis.Settings, FontSize = 24, VerticalTextAlignment = TextAlignment.Center });
            headerStack.Children.Add(new Label { Text = "Theme Settings", FontSize = 18, FontAttributes = FontAttributes.Bold, VerticalTextAlignment = TextAlignment.Center });
            grid.Add(headerStack, 0, 0);
            Grid.SetColumnSpan(headerStack, 2);

            // Use System Theme
            grid.Add(new Label { Text = "Follow system theme:", VerticalTextAlignment = TextAlignment.Center }, 0, 1);
            grid.Add(useSystemThemeSwitch, 1, 1);

            // Dark Mode
            var darkModeLabel = new Label { Text = "Dark mode:", VerticalTextAlignment = TextAlignment.Center };
            grid.Add(darkModeLabel, 0, 2);
            grid.Add(darkModeSwitch, 1, 2);

            // Current theme indicator
            var currentThemeLabel = new Label 
            { 
                Text = GetCurrentThemeText(),
                FontSize = 14,
                Margin = new Thickness(0, 8, 0, 0)
            };
            currentThemeLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6B7280"), Color.FromArgb("#9CA3AF"));
            grid.Add(currentThemeLabel, 0, 3);
            Grid.SetColumnSpan(currentThemeLabel, 2);

            // Status label
            grid.Add(statusLabel, 0, 4);
            Grid.SetColumnSpan(statusLabel, 2);

            // Event handlers
            useSystemThemeSwitch.Toggled += (s, e) =>
            {
                darkModeSwitch.IsEnabled = !e.Value;
                Settings.UseSystemTheme = e.Value;
                
                if (e.Value)
                {
                    ThemeService.Current.UseSystemTheme();
                    statusLabel.Text = $"{Emojis.Settings} Following system theme";
                }
                else
                {
                    ThemeService.Current.SetDarkMode(Settings.DarkModeEnabled);
                    statusLabel.Text = Settings.DarkModeEnabled ? "\U0001F319 Dark mode enabled" : "\u2600\uFE0F Light mode enabled";
                }
                
                currentThemeLabel.Text = GetCurrentThemeText();
                DataStore.Save();
                
                // Refresh the panel after a short delay to allow theme to apply
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () => ShowCategory("Appearance"));
            };

            darkModeSwitch.Toggled += (s, e) =>
            {
                Settings.DarkModeEnabled = e.Value;
                ThemeService.Current.SetDarkMode(e.Value);
                statusLabel.Text = e.Value ? "\U0001F319 Dark mode enabled" : "\u2600\uFE0F Light mode enabled";
                currentThemeLabel.Text = GetCurrentThemeText();
                DataStore.Save();
                
                // Refresh the panel after a short delay to allow theme to apply
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () => ShowCategory("Appearance"));
            };

            // Info panel
            var infoLabel = new Label
            {
                FontSize = 12,
                LineHeight = 1.4,
                Text = $"{Emojis.Info} Theme Options:\n\n" +
                       "� Follow system theme: Automatically switches between light and dark based on your device settings\n" +
                       "� Dark mode: Manual control when system theme is disabled\n\n" +
                       "The pool game will also update to match your theme preference."
            };
            infoLabel.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            
            var infoFrame = new Border
            {
                Padding = 12,
                Stroke = Color.FromArgb("#3B82F6"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 16, 0, 0),
                Content = infoLabel
            };
            infoFrame.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#F0F9FF"), Color.FromArgb("#1E3A5F"));

            var stack = new VerticalStackLayout { Spacing = 8 };
            stack.Children.Add(grid);
            stack.Children.Add(infoFrame);

            return stack;
        }

        private string GetCurrentThemeText()
        {
            var isDark = ThemeService.Current.IsDarkModeActive;
            var isSystem = Settings.UseSystemTheme;
            
            if (isSystem)
            {
                return isDark ? "\U0001F319 System theme: Dark" : "\u2600\uFE0F System theme: Light";
            }
            return isDark ? "\U0001F319 Current: Dark mode" : "\u2600\uFE0F Current: Light mode";
        }

        private View CreatePlayerRatingsPanel()
        {
            _startingRatingEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "1500", Text = Settings.RatingStartValue.ToString() };
            _ratingWeightingEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "100", Text = Settings.RatingWeighting.ToString() };
            _ratingsBiasEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "5", Text = Settings.RatingsBias.ToString() };
            _winFactorEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "1.0", Text = Settings.WinFactor.ToString("0.00") };
            _lossFactorEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "1.0", Text = Settings.LossFactor.ToString("0.00") };
            _eightBallFactorEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "1.5", Text = Settings.EightBallFactor.ToString("0.00") };
            _useEightBallSwitch = new Switch { IsToggled = Settings.UseEightBallFactor };
            _minFramesEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "60", Text = Settings.MinFramesPercentage.ToString() };
            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = 140 } }, RowSpacing = 12 };

            grid.Add(new Label { Text = "Starting rating:", VerticalTextAlignment = TextAlignment.Center }, 0, 0);
            grid.Add(_startingRatingEntry, 1, 0);

            grid.Add(new Label { Text = "Rating weighting:", VerticalTextAlignment = TextAlignment.Center }, 0, 1);
            grid.Add(_ratingWeightingEntry, 1, 1);

            grid.Add(new Label { Text = "Ratings bias (decay):", VerticalTextAlignment = TextAlignment.Center }, 0, 2);
            grid.Add(_ratingsBiasEntry, 1, 2);

            grid.Add(new Label { Text = "Win factor:", VerticalTextAlignment = TextAlignment.Center }, 0, 3);
            grid.Add(_winFactorEntry, 1, 3);

            grid.Add(new Label { Text = "Loss factor:", VerticalTextAlignment = TextAlignment.Center }, 0, 4);
            grid.Add(_lossFactorEntry, 1, 4);

            grid.Add(new Label { Text = "Use 8-ball factor:", VerticalTextAlignment = TextAlignment.Center }, 0, 5);
            grid.Add(_useEightBallSwitch, 1, 5);

            grid.Add(new Label { Text = "8-ball factor:", VerticalTextAlignment = TextAlignment.Center }, 0, 6);
            grid.Add(_eightBallFactorEntry, 1, 6);

            grid.Add(new Label { Text = "Min frames % for table:", VerticalTextAlignment = TextAlignment.Center }, 0, 7);
            grid.Add(_minFramesEntry, 1, 7);

            _useEightBallSwitch.Toggled += (s, e) => _eightBallFactorEntry.IsEnabled = e.Value;
            _eightBallFactorEntry.IsEnabled = Settings.UseEightBallFactor;

            var infoContent = new Label
            {
                FontSize = 12,
                LineHeight = 1.4,
                Text = "VBA-Style Cumulative Weighted Rating:\n\n" +
                       "� Earlier frames have lower weight (Weighting - Bias � frames)\n" +
                       "� Later frames have higher weight (progressive bias increase)\n" +
                       "� Rating based on opponent strength at time of match\n" +
                       "� Win against stronger opponent = higher rating gain\n\n" +
                       "Min Frames %:\n" +
                       "Percentage of maximum available frames needed to appear in ratings table.\n" +
                       "Example: If max is 30 frames and you set 60%, players need 18 frames.\n" +
                       "All players still have ratings calculated.\n\n" +
                       "Formula:\n" +
                       "Rating = ?(OpponentRating � Factor � Weight) / ?Weight"
            };
            infoContent.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            
            var infoFrame = new Border
            {
                Padding = 12,
                Stroke = Color.FromArgb("#3B82F6"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 8, 0, 0),
                Content = infoContent
            };
            infoFrame.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#F0F9FF"), Color.FromArgb("#1E3A5F"));

            var recalcBtn = new Button
            {
                Text = "\U0001F504 Recalculate All Ratings",
                BackgroundColor = Color.FromArgb("#D97706"),
                TextColor = Colors.White,
                Command = new Command(async () => await OnRecalculateAllRatingsAsync())
            };

            var buttons = new HorizontalStackLayout
            {
                Spacing = 12,
                Margin = new Thickness(0, 16, 0, 0),
                Children =
                {
                    new Button { Text = "Save Settings", Command = new Command(OnSaveClicked) },
                    new Button { Text = "Reset to Defaults", BackgroundColor = Color.FromArgb("#FF6B6B"), TextColor = Colors.White, Command = new Command(OnResetClicked) }
                }
            };

            var recalcInfoContent = new Label
            {
                FontSize = 12,
                LineHeight = 1.4,
                Text = "Imported data from VBA/SQL may contain pre-calculated rating values.\n" +
                       "Use this button to clear those and recalculate all ratings from scratch\n" +
                       "using the current settings above."
            };
            recalcInfoContent.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);

            var recalcInfoFrame = new Border
            {
                Padding = 12,
                Stroke = Color.FromArgb("#D97706"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 8, 0, 0),
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children = { recalcInfoContent, recalcBtn }
                }
            };
            recalcInfoFrame.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#FFFBEB"), Color.FromArgb("#422006"));

            var subtitleLabel = new Label { Text = "VBA-style opponent-based cumulative weighted rating system", FontSize = 14, Margin = new Thickness(0, 0, 0, 8) };
            subtitleLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#666666"), Color.FromArgb("#9CA3AF"));

            return new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Player Rating System", FontSize = 20, FontAttributes = FontAttributes.Bold },
                    subtitleLabel,
                    BuildSeasonScopeSelector("Player Ratings"),
                    grid,
                    infoFrame,
                    buttons,
                    recalcInfoFrame,
                    _statusLabel
                }
            };
        }

        private View CreateMatchScoringPanel()
        {
            _pointsForWinEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "2", Text = Settings.MatchWinBonus.ToString() };
            _pointsForDrawEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "1", Text = Settings.MatchDrawBonus.ToString() };
            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = 140 } }, RowSpacing = 12 };

            grid.Add(new Label { Text = "Match win bonus:", VerticalTextAlignment = TextAlignment.Center }, 0, 0);
            grid.Add(_pointsForWinEntry, 1, 0);

            grid.Add(new Label { Text = "Match draw bonus:", VerticalTextAlignment = TextAlignment.Center }, 0, 1);
            grid.Add(_pointsForDrawEntry, 1, 1);

            var infoContent = new Label
            {
                FontSize = 12,
                LineHeight = 1.4,
                Text = "New Points System:\n\n" +
                       "Team points = Frames Won + Bonus\n\n" +
                       "· Win: Frames Won + Match Win Bonus\n" +
                       "· Draw: Frames Won + Match Draw Bonus\n" +
                       "· Loss: Frames Won (no bonus)\n\n" +
                       "Example: Team wins 6-4 with Win Bonus=2:\n" +
                       "  Winner gets 6+2=8 points\n" +
                       "  Loser gets 4 points"
            };
            infoContent.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);

            var infoFrame = new Border
            {
                Padding = 12,
                Stroke = Color.FromArgb("#3B82F6"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 8, 0, 0),
                Content = infoContent
            };
            infoFrame.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#F0F9FF"), Color.FromArgb("#1E3A5F"));

            // ── Tiebreaker Configuration ──
            var tiebreakerSection = BuildTiebreakerSection();

            var buttons = new HorizontalStackLayout
            {
                Spacing = 12,
                Margin = new Thickness(0, 16, 0, 0),
                Children =
                {
                    new Button { Text = "Save Settings", Command = new Command(OnSaveClicked) },
                    new Button { Text = "Reset to Defaults", BackgroundColor = Color.FromArgb("#FF6B6B"), TextColor = Colors.White, Command = new Command(OnResetClicked) }
                }
            };

            var subtitleLabel = new Label { Text = "Configure how team match points are awarded", FontSize = 14, Margin = new Thickness(0, 0, 0, 8) };
            subtitleLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#666666"), Color.FromArgb("#9CA3AF"));

            return new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Match Scoring", FontSize = 20, FontAttributes = FontAttributes.Bold },
                    subtitleLabel,
                    BuildSeasonScopeSelector("Match Scoring"),
                    grid,
                    infoFrame,
                    tiebreakerSection,
                    buttons,
                    _statusLabel
                }
            };
        }

        /// <summary>
        /// Builds the tiebreaker configuration section with move-up / move-down / add / remove controls.
        /// </summary>
        private View BuildTiebreakerSection()
        {
            var header = new Label { Text = "Tiebreaker Order", FontSize = 18, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 16, 0, 0) };
            var subtitle = new Label { Text = "When teams are level on points, these criteria are applied in order (top = highest priority)", FontSize = 13, Margin = new Thickness(0, 0, 0, 8) };
            subtitle.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#666666"), Color.FromArgb("#9CA3AF"));

            _tiebreakerListLayout = new VerticalStackLayout { Spacing = 6 };
            RebuildTiebreakerRows();

            // "Add criterion" picker
            var allCriteria = Enum.GetValues<TiebreakerCriterion>();
            var addPicker = new Picker { Title = "Add tiebreaker...", HorizontalOptions = LayoutOptions.Start, WidthRequest = 200 };
            addPicker.ItemsSource = allCriteria.Select(FormatCriterionName).ToList();
            addPicker.SelectedIndexChanged += (s, e) =>
            {
                if (addPicker.SelectedIndex < 0) return;
                var criterion = allCriteria[addPicker.SelectedIndex];
                if (!Settings.TiebreakerOrder.Contains(criterion))
                {
                    Settings.TiebreakerOrder.Add(criterion);
                    DataStore.Save();
                    RebuildTiebreakerRows();
                }
                addPicker.SelectedIndex = -1;
            };

            return new VerticalStackLayout
            {
                Spacing = 4,
                Children = { header, subtitle, _tiebreakerListLayout, addPicker }
            };
        }

        private void RebuildTiebreakerRows()
        {
            if (_tiebreakerListLayout == null) return;
            _tiebreakerListLayout.Children.Clear();

            var order = Settings.TiebreakerOrder;
            for (int i = 0; i < order.Count; i++)
            {
                int index = i; // capture
                var criterion = order[index];

                var row = new HorizontalStackLayout { Spacing = 8 };

                var posLabel = new Label
                {
                    Text = $"{index + 1}.",
                    FontAttributes = FontAttributes.Bold,
                    WidthRequest = 24,
                    VerticalTextAlignment = TextAlignment.Center
                };

                var nameLabel = new Label
                {
                    Text = FormatCriterionName(criterion),
                    VerticalTextAlignment = TextAlignment.Center,
                    WidthRequest = 160
                };

                var upBtn = new Button { Text = "▲", FontSize = 11, WidthRequest = 36, HeightRequest = 32, Padding = 0, IsEnabled = index > 0 };
                upBtn.Clicked += (s, e) =>
                {
                    (order[index - 1], order[index]) = (order[index], order[index - 1]);
                    DataStore.Save();
                    RebuildTiebreakerRows();
                };

                var downBtn = new Button { Text = "▼", FontSize = 11, WidthRequest = 36, HeightRequest = 32, Padding = 0, IsEnabled = index < order.Count - 1 };
                downBtn.Clicked += (s, e) =>
                {
                    (order[index + 1], order[index]) = (order[index], order[index + 1]);
                    DataStore.Save();
                    RebuildTiebreakerRows();
                };

                var removeBtn = new Button { Text = "✕", FontSize = 11, WidthRequest = 36, HeightRequest = 32, Padding = 0, BackgroundColor = Color.FromArgb("#EF4444"), TextColor = Colors.White };
                removeBtn.Clicked += (s, e) =>
                {
                    order.RemoveAt(index);
                    DataStore.Save();
                    RebuildTiebreakerRows();
                };

                row.Children.Add(posLabel);
                row.Children.Add(nameLabel);
                row.Children.Add(upBtn);
                row.Children.Add(downBtn);
                row.Children.Add(removeBtn);

                _tiebreakerListLayout.Children.Add(row);
            }

            if (order.Count == 0)
            {
                var emptyLabel = new Label { Text = "No tiebreakers configured — tied teams will share the same position.", FontSize = 12, FontAttributes = FontAttributes.Italic };
                emptyLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6B7280"), Color.FromArgb("#9CA3AF"));
                _tiebreakerListLayout.Children.Add(emptyLabel);
            }
        }

        private static string FormatCriterionName(TiebreakerCriterion criterion) => criterion switch
        {
            TiebreakerCriterion.FrameDifference => "Frame Difference",
            TiebreakerCriterion.FramesFor => "Frames For",
            TiebreakerCriterion.HeadToHead => "Head-to-Head",
            TiebreakerCriterion.Wins => "Matches Won",
            _ => criterion.ToString()
        };

        /// <summary>
        /// Builds a season scope selector that appears at the top of per-season settings panels.
        /// Allows the user to choose "Global Defaults" or a specific season, and toggle custom overrides.
        /// </summary>
        private View BuildSeasonScopeSelector(string categoryToRefresh)
        {
            var seasons = DataStore.Data.Seasons.OrderByDescending(s => s.StartDate).ToList();

            // Season picker: "Global Defaults" + each season
            var seasonPicker = new Picker { Title = "Settings scope", HorizontalOptions = LayoutOptions.Fill };
            seasonPicker.Items.Add("Global Defaults");
            foreach (var s in seasons)
                seasonPicker.Items.Add(s.Name ?? $"Season {s.StartDate.Year}");

            // Set current selection
            if (_editingSeasonId.HasValue)
            {
                var idx = seasons.FindIndex(s => s.Id == _editingSeasonId.Value);
                seasonPicker.SelectedIndex = idx >= 0 ? idx + 1 : 0;
            }
            else
            {
                seasonPicker.SelectedIndex = 0;
            }

            // "Use custom settings" toggle (only visible when a season is selected)
            var customToggle = new Switch { IsToggled = IsEditingSeasonOverride, HorizontalOptions = LayoutOptions.Start };
            var customLabel = new Label { Text = "Use custom settings for this season", VerticalTextAlignment = TextAlignment.Center, FontSize = 13 };
            var toggleRow = new HorizontalStackLayout { Spacing = 8, Children = { customToggle, customLabel } };
            toggleRow.IsVisible = _editingSeasonId.HasValue;

            // Scope info label
            var scopeInfo = new Label { FontSize = 12, Margin = new Thickness(0, 4, 0, 0) };
            scopeInfo.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6B7280"), Color.FromArgb("#9CA3AF"));

            if (!_editingSeasonId.HasValue)
                scopeInfo.Text = $"{Emojis.Info} Editing global defaults — applies to all seasons without custom settings.";
            else if (IsEditingSeasonOverride)
                scopeInfo.Text = $"{Emojis.Info} Editing custom settings for this season only.";
            else
                scopeInfo.Text = $"{Emojis.Info} This season uses global defaults. Toggle on to customise.";

            seasonPicker.SelectedIndexChanged += (s, e) =>
            {
                if (seasonPicker.SelectedIndex <= 0)
                    _editingSeasonId = null;
                else
                    _editingSeasonId = seasons[seasonPicker.SelectedIndex - 1].Id;

                ShowCategory(categoryToRefresh);
            };

            customToggle.Toggled += (s, e) =>
            {
                if (!_editingSeasonId.HasValue) return;
                var season = DataStore.Data.Seasons.FirstOrDefault(se => se.Id == _editingSeasonId.Value);
                if (season == null) return;

                if (e.Value)
                {
                    // Create a season override by cloning global settings
                    season.Settings ??= DataStore.Data.Settings.Clone();
                }
                else
                {
                    // Remove the override — revert to global
                    season.Settings = null;
                }

                DataStore.Save();
                ShowCategory(categoryToRefresh);
            };

            var border = new Border
            {
                Padding = 12,
                StrokeThickness = 1,
                Margin = new Thickness(0, 0, 0, 12),
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label { Text = $"{Emojis.Settings} Settings Scope", FontAttributes = FontAttributes.Bold, FontSize = 14 },
                        seasonPicker,
                        toggleRow,
                        scopeInfo
                    }
                }
            };
            border.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#F8FAFC"), Color.FromArgb("#1F2937"));
            border.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#E5E7EB"), Color.FromArgb("#374151"));

            return border;
        }

        private View CreateFixtureDefaultsPanel()
        {
            _framesPerMatchEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "10", Text = Settings.DefaultFramesPerMatch.ToString() };
            _matchDayPicker = new Picker
            {
                ItemsSource = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().Select(d => d.ToString()).ToList(),
                SelectedIndex = (int)Settings.DefaultMatchDay
            };
            _matchTimePicker = new TimePicker { Format = "HH:mm", Time = Settings.DefaultMatchTime };
            _roundsPerOpponentEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "2", Text = Settings.DefaultRoundsPerOpponent.ToString() };
            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = 140 } }, RowSpacing = 12 };

            grid.Add(new Label { Text = "Frames per match:", VerticalTextAlignment = TextAlignment.Center }, 0, 0);
            grid.Add(_framesPerMatchEntry, 1, 0);

            grid.Add(new Label { Text = "Default match day:", VerticalTextAlignment = TextAlignment.Center }, 0, 1);
            grid.Add(_matchDayPicker, 1, 1);

            grid.Add(new Label { Text = "Default match time:", VerticalTextAlignment = TextAlignment.Center }, 0, 2);
            grid.Add(_matchTimePicker, 1, 2);

            grid.Add(new Label { Text = "Rounds per opponent:", VerticalTextAlignment = TextAlignment.Center }, 0, 3);
            grid.Add(_roundsPerOpponentEntry, 1, 3);

            var infoContent = new Label
            {
                FontSize = 12,
                LineHeight = 1.4,
                Text = "These defaults are used when generating fixtures for a season. \nYou can override them when creating a specific season."
            };
            infoContent.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            
            var infoFrame = new Border
            {
                Padding = 12,
                Stroke = Color.FromArgb("#3B82F6"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 8, 0, 0),
                Content = infoContent
            };
            infoFrame.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#F0F9FF"), Color.FromArgb("#1E3A5F"));

            var buttons = new HorizontalStackLayout
            {
                Spacing = 12,
                Margin = new Thickness(0, 16, 0, 0),
                Children =
                {
                    new Button { Text = "Save Settings", Command = new Command(OnSaveClicked) },
                    new Button { Text = "Reset to Defaults", BackgroundColor = Color.FromArgb("#FF6B6B"), TextColor = Colors.White, Command = new Command(OnResetClicked) }
                }
            };

            var subtitleLabel = new Label { Text = "Default values used when generating new fixtures", FontSize = 14, Margin = new Thickness(0, 0, 0, 8) };
            subtitleLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#666666"), Color.FromArgb("#9CA3AF"));

            return new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Fixture Generation Defaults", FontSize = 20, FontAttributes = FontAttributes.Bold },
                    subtitleLabel,
                    BuildSeasonScopeSelector("Fixture Defaults"),
                    grid,
                    infoFrame,
                    buttons,
                    _statusLabel
                }
            };
        }

        private View CreateNotificationsPanel()
        {
            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            // ========== PHASE 3: USER PREFERENCES ==========
            
            // Match Reminders Enable/Disable
            var matchRemindersSwitch = new Switch 
            { 
                IsToggled = Settings.MatchRemindersEnabled,
                HorizontalOptions = LayoutOptions.Start
            };

            // Reminder Hours Picker
            var reminderHoursPicker = new Picker
            {
                Title = "Select hours",
                ItemsSource = new List<string> { "1 hour", "2 hours", "4 hours", "6 hours", "12 hours", "24 hours" },
                SelectedIndex = Settings.ReminderHoursBefore switch
                {
                    1 => 0,
                    2 => 1,
                    4 => 2,
                    6 => 3,
                    12 => 4,
                    24 => 5,
                    _ => 1 // Default to 2 hours
                },
                HorizontalOptions = LayoutOptions.FillAndExpand
            };

            // Result Notifications Enable/Disable
            var resultNotificationsSwitch = new Switch 
            { 
                IsToggled = Settings.ResultNotificationsEnabled,
                HorizontalOptions = LayoutOptions.Start
            };

            // Weekly Fixture List Enable/Disable
            var weeklyFixtureSwitch = new Switch 
            { 
                IsToggled = Settings.WeeklyFixtureListEnabled,
                HorizontalOptions = LayoutOptions.Start
            };

            // Event Handlers for Phase 3 Settings
            matchRemindersSwitch.Toggled += (s, e) =>
            {
                Settings.MatchRemindersEnabled = e.Value;
                DataStore.Save();
                if (_statusLabel != null)
                    _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {Emojis.Success} Match reminders {(e.Value ? "enabled" : "disabled")}";
            };

            reminderHoursPicker.SelectedIndexChanged += (s, e) =>
            {
                Settings.ReminderHoursBefore = reminderHoursPicker.SelectedIndex switch
                {
                    0 => 1,
                    1 => 2,
                    2 => 4,
                    3 => 6,
                    4 => 12,
                    5 => 24,
                    _ => 2
                };
                DataStore.Save();
                if (_statusLabel != null)
                    _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {Emojis.Success} Reminder time set to {Settings.ReminderHoursBefore} hour(s) before match";
            };

            resultNotificationsSwitch.Toggled += (s, e) =>
            {
                Settings.ResultNotificationsEnabled = e.Value;
                DataStore.Save();
                if (_statusLabel != null)
                    _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {Emojis.Success} Result notifications {(e.Value ? "enabled" : "disabled")}";
            };

            weeklyFixtureSwitch.Toggled += (s, e) =>
            {
                Settings.WeeklyFixtureListEnabled = e.Value;
                DataStore.Save();
                if (_statusLabel != null)
                    _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {Emojis.Success} Weekly fixture list {(e.Value ? "enabled" : "disabled")}";
            };

            // Preferences Grid
            var preferencesGrid = new Grid 
            { 
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, 
                RowSpacing = 16,
                Margin = new Thickness(0, 12, 0, 0)
            };

            // Row 0: Match Reminders
            preferencesGrid.Add(new Label 
            { 
                Text = $"{Emojis.Bell} Match Reminders:", 
                VerticalTextAlignment = TextAlignment.Center,
                FontAttributes = FontAttributes.Bold,
                FontFamily = Emojis.GetFontFamily()
            }, 0, 0);
            preferencesGrid.Add(matchRemindersSwitch, 1, 0);

            // Row 1: Reminder Hours (only if enabled)
            preferencesGrid.Add(new Label 
            { 
                Text = "  Remind me:",
                VerticalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(20, 0, 0, 0)
            }, 0, 1);
            preferencesGrid.Add(reminderHoursPicker, 1, 1);

            // Row 2: Result Notifications
            preferencesGrid.Add(new Label 
            { 
                Text = $"{Emojis.Success} Result Notifications:", 
                VerticalTextAlignment = TextAlignment.Center,
                FontAttributes = FontAttributes.Bold,
                FontFamily = Emojis.GetFontFamily()
            }, 0, 2);
            preferencesGrid.Add(resultNotificationsSwitch, 1, 2);

            // Row 3: Weekly Fixture List
            preferencesGrid.Add(new Label 
            { 
                Text = $"{Emojis.Calendar} Weekly Fixture List:", 
                VerticalTextAlignment = TextAlignment.Center,
                FontAttributes = FontAttributes.Bold,
                FontFamily = Emojis.GetFontFamily()
            }, 0, 3);
            preferencesGrid.Add(weeklyFixtureSwitch, 1, 3);

            // Request Permissions Button
            var requestPermissionsBtn = new Button
            {
                Text = $"{Emojis.Bell} Request Notification Permissions",
                BackgroundColor = Color.FromArgb("#3B82F6"),
                TextColor = Colors.White,
                Margin = new Thickness(0, 16, 0, 8),
                FontFamily = Emojis.GetFontFamily()
            };

            // Test Notification Button
            var testNotificationBtn = new Button
            {
                Text = $"{Emojis.Bell} Send Test Notification",
                BackgroundColor = Color.FromArgb("#10B981"),
                TextColor = Colors.White,
                Margin = new Thickness(0, 4),
                FontFamily = Emojis.GetFontFamily()
            };

            // Pending Notifications Label
            var pendingLabel = new Label
            {
                Text = "Pending notifications: Checking...",
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0)
            };
            pendingLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#666666"), Color.FromArgb("#9CA3AF"));

            // Cancel All Button
            var cancelAllBtn = new Button
            {
                Text = $"{Emojis.Error} Cancel All Notifications",
                BackgroundColor = Color.FromArgb("#EF4444"),
                TextColor = Colors.White,
                Margin = new Thickness(0, 4),
                FontFamily = Emojis.GetFontFamily()
            };

            // Request Permissions Event Handler
            requestPermissionsBtn.Clicked += async (s, e) =>
            {
                try
                {
                    var notificationService = Handler?.MauiContext?.Services.GetService<INotificationService>();
                    if (notificationService != null)
                    {
                        var granted = await notificationService.RequestPermissionsAsync();
                        if (granted)
                        {
                            await DisplayAlert($"{Emojis.Success} Success", "Notifications enabled! You can now receive match reminders.", "OK");
                            if (_statusLabel != null)
                                _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {Emojis.Success} Notifications enabled";
                        }
                        else
                        {
                            await DisplayAlert($"{Emojis.Error} Permission Denied", "Please enable notifications in your device settings.", "OK");
                            if (_statusLabel != null)
                                _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {Emojis.Error} Permission denied";
                        }
                    }
                    else
                    {
                        await DisplayAlert("Error", "Notification service not available.", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to request permissions: {ex.Message}", "OK");
                }
            };

            // Test Notification Event Handler
            testNotificationBtn.Clicked += async (s, e) =>
            {
                try
                {
                    var notificationService = Handler?.MauiContext?.Services.GetService<INotificationService>();
                    if (notificationService != null)
                    {
                        await notificationService.ShowNotificationAsync(
                            id: 99999,
                            title: $"{Emojis.EightBall} Test Notification",
                            message: "Notifications are working! You'll get match reminders."
                        );
                        await DisplayAlert($"{Emojis.Success} Sent", "Check your notification panel!", "OK");
                        if (_statusLabel != null)
                            _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {Emojis.Success} Test notification sent";
                    }
                    else
                    {
                        await DisplayAlert("Error", "Notification service not available.", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to send notification: {ex.Message}", "OK");
                }
            };

            // Cancel All Event Handler
            cancelAllBtn.Clicked += async (s, e) =>
            {
                try
                {
                    var reminderService = Handler?.MauiContext?.Services.GetService<MatchReminderService>();
                    if (reminderService != null)
                    {
                        await reminderService.CancelAllMatchRemindersAsync();
                        pendingLabel.Text = "Pending notifications: 0";
                        await DisplayAlert($"{Emojis.Success} Cancelled", "All scheduled notifications have been cancelled.", "OK");
                        if (_statusLabel != null)
                            _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {Emojis.Success} All notifications cancelled";
                    }
                    else
                    {
                        await DisplayAlert("Error", "Reminder service not available.", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to cancel notifications: {ex.Message}", "OK");
                }
            };

            // Info Frame - Updated for Phase 3
            var infoHeaderLabel = new Label 
            { 
                Text = $"{Emojis.Info} About Notifications", 
                FontAttributes = FontAttributes.Bold, 
                FontSize = 14,
                FontFamily = Emojis.GetFontFamily()
            };
            infoHeaderLabel.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            
            var infoBodyLabel = new Label
            {
                FontSize = 12,
                LineHeight = 1.4,
                FormattedText = new FormattedString
                {
                    Spans =
                    {
                        new Span { Text = "Notification Types:\n", FontAttributes = FontAttributes.Bold },
                        new Span { Text = $"{Emojis.Bell} Match Reminders - Get notified before your matches\n" },
                        new Span { Text = $"{Emojis.Success} Result Alerts - Instant notifications when results are posted\n" },
                        new Span { Text = $"{Emojis.Calendar} Weekly Summary - Monday morning fixture list\n\n" },
                        new Span { Text = "Customization:\n", FontAttributes = FontAttributes.Bold },
                        new Span { Text = $"{Emojis.Success} Choose reminder timing (1-24 hours before match)\n" },
                        new Span { Text = $"{Emojis.Success} Enable/disable each notification type independently\n" },
                        new Span { Text = $"{Emojis.Success} Settings saved automatically when changed\n\n" },
                        new Span { Text = "How It Works:\n", FontAttributes = FontAttributes.Bold },
                        new Span { Text = "� Reminders scheduled automatically when fixtures are generated or saved\n" },
                        new Span { Text = "� Past matches don't get reminders\n" },
                        new Span { Text = "� Settings apply to all future notifications\n" },
                        new Span { Text = "� Works on iOS, Android, and Windows" }
                    }
                }
            };
            infoBodyLabel.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            
            var infoFrame = new Border
            {
                Padding = 12,
                Stroke = Color.FromArgb("#3B82F6"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 16, 0, 0),
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children = { infoHeaderLabel, infoBodyLabel }
                }
            };
            infoFrame.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#F0F9FF"), Color.FromArgb("#1E3A5F"));

            var warningHeaderLabel = new Label 
            { 
                Text = $"{Emojis.Warning} Important", 
                FontAttributes = FontAttributes.Bold, 
                FontSize = 14, 
                FontFamily = Emojis.GetFontFamily()
            };
            warningHeaderLabel.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            
            var warningBodyLabel = new Label
            {
                FontSize = 12,
                LineHeight = 1.4,
                FormattedText = new FormattedString
                {
                    Spans =
                    {
                        new Span { Text = $"{Emojis.Info} You must grant notification permissions first\n" },
                        new Span { Text = $"{Emojis.Info} Changing reminder time affects new notifications only\n" },
                        new Span { Text = $"{Emojis.Info} Battery saver mode may delay notifications\n" },
                        new Span { Text = $"{Emojis.Info} Test notifications to ensure they're working" }
                    }
                }
            };
            warningBodyLabel.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            
            var warningFrame = new Border
            {
                Padding = 12,
                Stroke = Color.FromArgb("#F59E0B"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 8, 0, 0),
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children = { warningHeaderLabel, warningBodyLabel }
                }
            };
            warningFrame.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#FFFBEB"), Color.FromArgb("#422006"));

            var subtitleLabel = new Label { Text = "Customize your notification preferences", FontSize = 14, Margin = new Thickness(0, 0, 0, 8) };
            subtitleLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#666666"), Color.FromArgb("#9CA3AF"));
            
            var preferencesCard = new Border
            {
                Padding = 16,
                StrokeThickness = 1,
                Margin = new Thickness(0, 8, 0, 0),
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label { Text = $"{Emojis.Bell} Notification Preferences", FontAttributes = FontAttributes.Bold, FontSize = 16 },
                        preferencesGrid
                    }
                }
            };
            preferencesCard.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#FAFAFA"), Color.FromArgb("#1F2937"));
            preferencesCard.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#E5E7EB"), Color.FromArgb("#374151"));

            var scrollView = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new Label { Text = "Match Notifications", FontSize = 20, FontAttributes = FontAttributes.Bold },
                        subtitleLabel,
                        
                        // Phase 3: User Preferences Section
                        preferencesCard,
                        
                        // Permissions & Management Section
                        new Label { Text = "Setup & Testing", FontAttributes = FontAttributes.Bold, FontSize = 16, Margin = new Thickness(0, 16, 0, 0) },
                        requestPermissionsBtn,
                        testNotificationBtn,
                        pendingLabel,
                        cancelAllBtn,
                        
                        infoFrame,
                        warningFrame,
                        _statusLabel
                    }
                }
            };

            // Check pending notifications after a short delay to ensure Handler is ready
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), async () =>
            {
                try
                {
                    var notificationService = Handler?.MauiContext?.Services.GetService<INotificationService>();
                    if (notificationService != null)
                    {
                        var count = await notificationService.GetPendingNotificationCountAsync();
                        pendingLabel.Text = $"Pending notifications: {count}";
                    }
                    else
                    {
                        pendingLabel.Text = "Pending notifications: Service not available";
                    }
                }
                catch (Exception ex)
                {
                    pendingLabel.Text = $"Pending notifications: Error - {ex.Message}";
                }
            });

            return scrollView;
        }

        private View CreateDivisionManagementPanel()
        {
            var data = DataStore.Data;
            var seasons = data.Seasons.OrderByDescending(s => s.StartDate).ToList();

            // ── Season picker ──
            var seasonPicker = new Picker
            {
                Title = "Select Season",
                HorizontalOptions = LayoutOptions.Fill
            };
            foreach (var s in seasons)
                seasonPicker.Items.Add(s.Name ?? $"Season {s.StartDate.Year}");

            if (seasons.Count > 0)
                seasonPicker.SelectedIndex = 0;

            // ── Container for division cards ──
            var divisionListContainer = new VerticalStackLayout { Spacing = 8 };
            var statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 4, 0, 0) };

            // Track selected divisions for bulk operations
            var selectedDivisionIds = new HashSet<Guid>();

            // Bulk action buttons (hidden until selections made)
            var splitBtn = new Button
            {
                Text = $"{Emojis.Season} Split Selected to New Season",
                FontSize = 13,
                Padding = new Thickness(16, 10),
                BackgroundColor = Color.FromArgb("#059669"),
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Start,
                IsVisible = false
            };

            var moveBtn = new Button
            {
                Text = $"{Emojis.Forward} Move Selected to Season",
                FontSize = 13,
                Padding = new Thickness(16, 10),
                BackgroundColor = Color.FromArgb("#7C3AED"),
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Start,
                IsVisible = false
            };

            var selectionLabel = new Label
            {
                FontSize = 12,
                IsVisible = false
            };
            selectionLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#666666"), Color.FromArgb("#9CA3AF"));

            void UpdateBulkButtons()
            {
                var count = selectedDivisionIds.Count;
                var hasSelection = count > 0;
                splitBtn.IsVisible = hasSelection;
                moveBtn.IsVisible = hasSelection;
                selectionLabel.IsVisible = hasSelection;
                selectionLabel.Text = $"{count} division(s) selected";
            }

            void RefreshDivisionList()
            {
                divisionListContainer.Children.Clear();
                selectedDivisionIds.Clear();
                UpdateBulkButtons();
                statusLabel.Text = "";

                if (seasonPicker.SelectedIndex < 0 || seasonPicker.SelectedIndex >= seasons.Count)
                    return;

                var seasonId = seasons[seasonPicker.SelectedIndex].Id;
                var divisions = data.Divisions
                    .Where(d => d.SeasonId == seasonId)
                    .OrderBy(d => d.Name)
                    .ToList();

                if (divisions.Count == 0)
                {
                    divisionListContainer.Children.Add(new Label
                    {
                        Text = "No divisions in this season.",
                        FontSize = 14,
                        TextColor = SubtitleText
                    });
                    return;
                }

                foreach (var div in divisions)
                {
                    var teamCount = data.Teams.Count(t => t.DivisionId == div.Id);
                    var fixtureCount = data.Fixtures.Count(f => f.DivisionId == div.Id);
                    var playerCount = data.Players.Count(p =>
                        p.SeasonId == seasonId &&
                        p.TeamId.HasValue &&
                        data.Teams.Any(t => t.Id == p.TeamId && t.DivisionId == div.Id));

                    var selectBox = new CheckBox { IsChecked = false, VerticalOptions = LayoutOptions.Center };
                    var capturedDiv = div;

                    selectBox.CheckedChanged += (s, e) =>
                    {
                        if (e.Value)
                            selectedDivisionIds.Add(capturedDiv.Id);
                        else
                            selectedDivisionIds.Remove(capturedDiv.Id);
                        UpdateBulkButtons();
                    };

                    var nameLabel = new Label
                    {
                        Text = div.Name ?? "(unnamed)",
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        VerticalTextAlignment = TextAlignment.Center
                    };

                    var statsLabel = new Label
                    {
                        Text = $"{teamCount} teams · {playerCount} players · {fixtureCount} fixtures",
                        FontSize = 12
                    };
                    statsLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#666666"), Color.FromArgb("#9CA3AF"));

                    var renameBtn = new Button
                    {
                        Text = $"{Emojis.Edit} Rename",
                        FontSize = 12,
                        Padding = new Thickness(10, 6),
                        BackgroundColor = Color.FromArgb("#3B82F6"),
                        TextColor = Colors.White,
                        HeightRequest = 34
                    };

                    var mergeBtn = new Button
                    {
                        Text = $"{Emojis.Forward} Merge",
                        FontSize = 12,
                        Padding = new Thickness(10, 6),
                        BackgroundColor = Color.FromArgb("#8B5CF6"),
                        TextColor = Colors.White,
                        HeightRequest = 34
                    };

                    var deleteBtn = new Button
                    {
                        Text = $"{Emojis.Delete}",
                        FontSize = 12,
                        Padding = new Thickness(10, 6),
                        BackgroundColor = Color.FromArgb("#EF4444"),
                        TextColor = Colors.White,
                        HeightRequest = 34,
                        IsEnabled = teamCount == 0 && fixtureCount == 0
                    };

                    renameBtn.Clicked += async (s, e) =>
                    {
                        var newName = await DisplayPromptAsync(
                            "Rename Division",
                            $"Current name: {capturedDiv.Name}",
                            initialValue: capturedDiv.Name ?? "",
                            maxLength: 100,
                            keyboard: Keyboard.Text);

                        if (!string.IsNullOrWhiteSpace(newName))
                        {
                            capturedDiv.Name = DivisionHelper.NormalizeDivisionName(newName);
                            capturedDiv.ModifiedDate = DateTime.UtcNow;
                            DataStore.Save();
                            RefreshDivisionList();
                            statusLabel.Text = $"{Emojis.Success} Renamed to \"{capturedDiv.Name}\"";
                        }
                    };

                    mergeBtn.Clicked += async (s, e) =>
                    {
                        var otherDivisions = divisions
                            .Where(d => d.Id != capturedDiv.Id)
                            .Select(d => d.Name ?? "(unnamed)")
                            .ToArray();

                        if (otherDivisions.Length == 0)
                        {
                            await DisplayAlert("Merge", "No other divisions to merge with.", "OK");
                            return;
                        }

                        var target = await DisplayActionSheet(
                            $"Merge \"{capturedDiv.Name}\" into:",
                            "Cancel", null, otherDivisions);

                        if (string.IsNullOrEmpty(target) || target == "Cancel") return;

                        var targetDiv = divisions.FirstOrDefault(d =>
                            (d.Name ?? "(unnamed)") == target && d.Id != capturedDiv.Id);

                        if (targetDiv == null) return;

                        var confirm = await DisplayAlert(
                            "Confirm Merge",
                            $"Move all teams and fixtures from \"{capturedDiv.Name}\" into \"{targetDiv.Name}\" and delete \"{capturedDiv.Name}\"?",
                            "Merge", "Cancel");

                        if (!confirm) return;

                        foreach (var team in data.Teams.Where(t => t.DivisionId == capturedDiv.Id))
                            team.DivisionId = targetDiv.Id;
                        foreach (var fixture in data.Fixtures.Where(f => f.DivisionId == capturedDiv.Id))
                            fixture.DivisionId = targetDiv.Id;

                        data.Divisions.Remove(capturedDiv);
                        DataStore.Save();
                        RefreshDivisionList();
                        statusLabel.Text = $"{Emojis.Success} Merged into \"{targetDiv.Name}\"";
                    };

                    deleteBtn.Clicked += async (s, e) =>
                    {
                        var confirm = await DisplayAlert(
                            "Delete Division",
                            $"Delete \"{capturedDiv.Name}\"? This division has no teams or fixtures.",
                            "Delete", "Cancel");

                        if (!confirm) return;

                        data.Divisions.Remove(capturedDiv);
                        DataStore.Save();
                        RefreshDivisionList();
                        statusLabel.Text = $"{Emojis.Success} Deleted \"{capturedDiv.Name}\"";
                    };

                    var headerRow = new HorizontalStackLayout { Spacing = 8 };
                    headerRow.Children.Add(selectBox);
                    headerRow.Children.Add(nameLabel);

                    var btnRow = new HorizontalStackLayout { Spacing = 6 };
                    btnRow.Children.Add(renameBtn);
                    btnRow.Children.Add(mergeBtn);
                    btnRow.Children.Add(deleteBtn);

                    var cardContent = new VerticalStackLayout { Spacing = 4 };
                    cardContent.Children.Add(headerRow);
                    cardContent.Children.Add(statsLabel);
                    cardContent.Children.Add(btnRow);

                    var card = new Border
                    {
                        Padding = 12,
                        StrokeThickness = 1,
                        StrokeShape = new RoundRectangle { CornerRadius = 8 },
                        Content = cardContent
                    };
                    card.SetAppThemeColor(Border.StrokeProperty, CardBorder, CardBorder);
                    card.SetAppThemeColor(Border.BackgroundColorProperty, CardBackground, CardBackground);

                    divisionListContainer.Children.Add(card);
                }
            }

            seasonPicker.SelectedIndexChanged += (s, e) => RefreshDivisionList();

            // ── Split to new season ──
            splitBtn.Clicked += async (s, e) =>
            {
                if (seasonPicker.SelectedIndex < 0 || seasonPicker.SelectedIndex >= seasons.Count)
                    return;

                var sourceSeasonId = seasons[seasonPicker.SelectedIndex].Id;
                var sourceSeason = seasons[seasonPicker.SelectedIndex];
                var divIds = selectedDivisionIds.ToList();
                var divNames = data.Divisions
                    .Where(d => divIds.Contains(d.Id))
                    .Select(d => d.Name ?? "(unnamed)")
                    .ToList();

                var summary = BuildMoveSummary(data, divIds, sourceSeasonId);

                var seasonName = await DisplayPromptAsync(
                    "Split to New Season",
                    $"Moving {divNames.Count} division(s) and associated data:\n{summary}\n\nEnter name for the new season:",
                    initialValue: $"{sourceSeason.Name} (Split)",
                    maxLength: 100,
                    keyboard: Keyboard.Text);

                if (string.IsNullOrWhiteSpace(seasonName)) return;

                var confirm = await DisplayAlert(
                    "Confirm Split",
                    $"Create season \"{seasonName}\" and move:\n{summary}\n\nfrom \"{sourceSeason.Name}\"?",
                    "Split", "Cancel");

                if (!confirm) return;

                // Create the new season
                var newSeason = new Season
                {
                    Id = Guid.NewGuid(),
                    Name = seasonName,
                    StartDate = sourceSeason.StartDate,
                    EndDate = sourceSeason.EndDate,
                    MatchDayOfWeek = sourceSeason.MatchDayOfWeek,
                    MatchStartTime = sourceSeason.MatchStartTime,
                    FramesPerMatch = sourceSeason.FramesPerMatch
                };

                data.Seasons.Add(newSeason);
                MoveDivisionsToSeason(data, divIds, sourceSeasonId, newSeason.Id);
                UpdateSeasonDatesFromFixtures(data, newSeason.Id);
                UpdateSeasonDatesFromFixtures(data, sourceSeasonId);
                DataStore.Save();

                // Refresh seasons list in picker
                seasons.Clear();
                seasons.AddRange(data.Seasons.OrderByDescending(ss => ss.StartDate));
                seasonPicker.Items.Clear();
                foreach (var ss in seasons)
                    seasonPicker.Items.Add(ss.Name ?? $"Season {ss.StartDate.Year}");

                // Select the source season
                var idx = seasons.FindIndex(ss => ss.Id == sourceSeasonId);
                seasonPicker.SelectedIndex = idx >= 0 ? idx : 0;
                RefreshDivisionList();
                statusLabel.Text = $"{Emojis.Success} Split {divNames.Count} division(s) into new season \"{seasonName}\"";
            };

            // ── Move to existing season ──
            moveBtn.Clicked += async (s, e) =>
            {
                if (seasonPicker.SelectedIndex < 0 || seasonPicker.SelectedIndex >= seasons.Count)
                    return;

                var sourceSeasonId = seasons[seasonPicker.SelectedIndex].Id;
                var divIds = selectedDivisionIds.ToList();
                var divNames = data.Divisions
                    .Where(d => divIds.Contains(d.Id))
                    .Select(d => d.Name ?? "(unnamed)")
                    .ToList();

                var otherSeasons = seasons
                    .Where(ss => ss.Id != sourceSeasonId)
                    .Select(ss => ss.Name ?? $"Season {ss.StartDate.Year}")
                    .ToArray();

                if (otherSeasons.Length == 0)
                {
                    await DisplayAlert("Move", "No other seasons to move to. Use 'Split to New Season' instead.", "OK");
                    return;
                }

                var targetName = await DisplayActionSheet(
                    $"Move {divNames.Count} division(s) to:",
                    "Cancel", null, otherSeasons);

                if (string.IsNullOrEmpty(targetName) || targetName == "Cancel") return;

                var targetSeason = seasons.FirstOrDefault(ss =>
                    (ss.Name ?? $"Season {ss.StartDate.Year}") == targetName && ss.Id != sourceSeasonId);

                if (targetSeason == null) return;

                var summary = BuildMoveSummary(data, divIds, sourceSeasonId);

                var confirm = await DisplayAlert(
                    "Confirm Move",
                    $"Move from \"{seasons[seasonPicker.SelectedIndex].Name}\" to \"{targetSeason.Name}\":\n{summary}",
                    "Move", "Cancel");

                if (!confirm) return;

                MoveDivisionsToSeason(data, divIds, sourceSeasonId, targetSeason.Id);
                UpdateSeasonDatesFromFixtures(data, targetSeason.Id);
                UpdateSeasonDatesFromFixtures(data, sourceSeasonId);
                DataStore.Save();

                RefreshDivisionList();
                statusLabel.Text = $"{Emojis.Success} Moved {divNames.Count} division(s) to \"{targetSeason.Name}\"";
            };

            // ── Auto-Clean button ──
            var autoCleanBtn = new Button
            {
                Text = $"{Emojis.Wrench} Auto-Clean Duplicates",
                BackgroundColor = Color.FromArgb("#F59E0B"),
                TextColor = Colors.White,
                Padding = new Thickness(24, 14),
                HorizontalOptions = LayoutOptions.Start
            };

            autoCleanBtn.Clicked += async (s, e) =>
            {
                if (seasonPicker.SelectedIndex < 0 || seasonPicker.SelectedIndex >= seasons.Count)
                    return;

                var seasonId = seasons[seasonPicker.SelectedIndex].Id;
                var seasonDivisions = data.Divisions.Where(d => d.SeasonId == seasonId).ToList();

                int mergedCount = 0;

                // Group by normalized name (handles "Red" vs "Red Division", "1st" vs "First")
                var groups = seasonDivisions
                    .GroupBy(d => DivisionHelper.NormalizeDivisionName(d.Name ?? ""), StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1);

                foreach (var group in groups)
                {
                    var canonical = group.OrderByDescending(d => (d.Name ?? "").Length).First();
                    foreach (var dup in group.Where(d => d.Id != canonical.Id))
                    {
                        foreach (var team in data.Teams.Where(t => t.DivisionId == dup.Id))
                            team.DivisionId = canonical.Id;
                        foreach (var fixture in data.Fixtures.Where(f => f.DivisionId == dup.Id))
                            fixture.DivisionId = canonical.Id;
                        data.Divisions.Remove(dup);
                        mergedCount++;
                    }
                }

                // Also merge ordinal equivalents that didn't group above
                var remaining = data.Divisions.Where(d => d.SeasonId == seasonId).ToList();
                var alreadyMerged = new HashSet<Guid>();
                for (int i = 0; i < remaining.Count; i++)
                {
                    if (alreadyMerged.Contains(remaining[i].Id)) continue;
                    for (int j = i + 1; j < remaining.Count; j++)
                    {
                        if (alreadyMerged.Contains(remaining[j].Id)) continue;
                        if (DivisionHelper.AreSameDivision(remaining[i].Name ?? "", remaining[j].Name ?? ""))
                        {
                            var (keep, remove) = (remaining[i].Name ?? "").Length >= (remaining[j].Name ?? "").Length
                                ? (remaining[i], remaining[j])
                                : (remaining[j], remaining[i]);

                            foreach (var team in data.Teams.Where(t => t.DivisionId == remove.Id))
                                team.DivisionId = keep.Id;
                            foreach (var fixture in data.Fixtures.Where(f => f.DivisionId == remove.Id))
                                fixture.DivisionId = keep.Id;
                            data.Divisions.Remove(remove);
                            alreadyMerged.Add(remove.Id);
                            mergedCount++;
                        }
                    }
                }

                // Normalize remaining division names
                int renamedCount = 0;
                foreach (var div in data.Divisions.Where(d => d.SeasonId == seasonId))
                {
                    var normalized = DivisionHelper.NormalizeDivisionName(div.Name ?? "");
                    if (!string.IsNullOrEmpty(normalized) && normalized != div.Name)
                    {
                        div.Name = normalized;
                        div.ModifiedDate = DateTime.UtcNow;
                        renamedCount++;
                    }
                }

                // Delete empty divisions
                int deletedCount = 0;
                var emptyDivisions = data.Divisions
                    .Where(d => d.SeasonId == seasonId)
                    .Where(d => !data.Teams.Any(t => t.DivisionId == d.Id) &&
                                !data.Fixtures.Any(f => f.DivisionId == d.Id))
                    .ToList();
                foreach (var empty in emptyDivisions)
                {
                    data.Divisions.Remove(empty);
                    deletedCount++;
                }

                if (mergedCount > 0 || renamedCount > 0 || deletedCount > 0)
                {
                    DataStore.Save();
                    RefreshDivisionList();
                    statusLabel.Text = $"{Emojis.Success} Merged {mergedCount} duplicates, normalized {renamedCount} names, deleted {deletedCount} empty divisions";
                }
                else
                {
                    statusLabel.Text = $"{Emojis.Info} No duplicates or issues found";
                }
            };

            // ── Info panel ──
            var infoLabel = new Label
            {
                FontSize = 12,
                LineHeight = 1.4,
                Text = $"{Emojis.Info} Division Management:\n\n" +
                       "• Rename: Change a division's display name\n" +
                       "• Merge: Combine two divisions (moves all teams & fixtures)\n" +
                       "• Delete: Remove empty divisions (no teams/fixtures)\n" +
                       "• Auto-Clean: Automatically merge duplicates like \"1st\" & \"First\",\n" +
                       "  \"Red\" & \"Red Division\", normalize names, and remove empties\n\n" +
                       "Select divisions with checkboxes to:\n" +
                       "• Split to New Season: Create a new season from selected divisions\n" +
                       "• Move to Season: Move selected divisions to an existing season\n" +
                       "  (Teams, players, and fixtures move with the division)"
            };
            infoLabel.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);

            var infoFrame = new Border
            {
                Padding = 12,
                Stroke = Color.FromArgb("#3B82F6"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 8, 0, 0),
                Content = infoLabel
            };
            infoFrame.SetAppThemeColor(Border.BackgroundColorProperty, InfoBoxBackground, InfoBoxBackground);

            // Initial load
            RefreshDivisionList();

            var mainStack = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = $"{Emojis.Division} Division Management", FontSize = 20, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "View, rename, merge, move, and split divisions across seasons", FontSize = 14, TextColor = SubtitleText, Margin = new Thickness(0, 0, 0, 8) },
                    seasonPicker,
                    autoCleanBtn,
                    selectionLabel,
                    new HorizontalStackLayout { Spacing = 8, Children = { splitBtn, moveBtn } },
                    statusLabel,
                    divisionListContainer,
                    infoFrame
                }
            };

            var scrollView = new ScrollView { Content = mainStack };
            return scrollView;
        }

        /// <summary>
        /// Build a human-readable summary of what will be moved with the selected divisions.
        /// </summary>
        private static string BuildMoveSummary(LeagueData data, List<Guid> divisionIds, Guid sourceSeasonId)
        {
            var divNames = data.Divisions
                .Where(d => divisionIds.Contains(d.Id))
                .Select(d => d.Name ?? "(unnamed)")
                .ToList();

            var teamIds = new HashSet<Guid>(
                data.Teams
                    .Where(t => t.DivisionId.HasValue && divisionIds.Contains(t.DivisionId.Value))
                    .Select(t => t.Id));

            var playerCount = data.Players.Count(p =>
                p.SeasonId == sourceSeasonId &&
                p.TeamId.HasValue &&
                teamIds.Contains(p.TeamId.Value));

            var fixtureCount = data.Fixtures.Count(f =>
                f.DivisionId.HasValue && divisionIds.Contains(f.DivisionId.Value));

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"  {Emojis.Division} {divNames.Count} division(s): {string.Join(", ", divNames)}");
            sb.AppendLine($"  {Emojis.Team} {teamIds.Count} team(s)");
            sb.AppendLine($"  {Emojis.Player} {playerCount} player(s)");
            sb.Append($"  {Emojis.Fixture} {fixtureCount} fixture(s)");
            return sb.ToString();
        }

        /// <summary>
        /// Move selected divisions and all associated data (teams, players, fixtures)
        /// from one season to another.
        /// </summary>
        private static void MoveDivisionsToSeason(
            LeagueData data, List<Guid> divisionIds, Guid sourceSeasonId, Guid targetSeasonId)
        {
            // Collect team IDs belonging to these divisions
            var teamIds = new HashSet<Guid>(
                data.Teams
                    .Where(t => t.DivisionId.HasValue && divisionIds.Contains(t.DivisionId.Value))
                    .Select(t => t.Id));

            // Move divisions
            foreach (var div in data.Divisions.Where(d => divisionIds.Contains(d.Id)))
            {
                div.SeasonId = targetSeasonId;
                div.ModifiedDate = DateTime.UtcNow;
            }

            // Move teams
            foreach (var team in data.Teams.Where(t => teamIds.Contains(t.Id)))
            {
                team.SeasonId = targetSeasonId;
            }

            // Move players on those teams
            foreach (var player in data.Players.Where(p =>
                         p.SeasonId == sourceSeasonId &&
                         p.TeamId.HasValue &&
                         teamIds.Contains(p.TeamId.Value)))
            {
                player.SeasonId = targetSeasonId;
                player.ModifiedDate = DateTime.UtcNow;
            }

            // Move fixtures in these divisions
            foreach (var fixture in data.Fixtures.Where(f =>
                         f.DivisionId.HasValue && divisionIds.Contains(f.DivisionId.Value)))
            {
                fixture.SeasonId = targetSeasonId;
            }

            // Also move fixtures that reference these teams but have no division set
            foreach (var fixture in data.Fixtures.Where(f =>
                         f.SeasonId == sourceSeasonId &&
                         !f.DivisionId.HasValue &&
                         (teamIds.Contains(f.HomeTeamId) || teamIds.Contains(f.AwayTeamId))))
            {
                fixture.SeasonId = targetSeasonId;
            }

            // Move venues that are only used by teams in the moved set
            var movedVenueIds = data.Teams
                .Where(t => teamIds.Contains(t.Id) && t.VenueId.HasValue)
                .Select(t => t.VenueId!.Value)
                .Distinct()
                .ToList();

            foreach (var venueId in movedVenueIds)
            {
                var venue = data.Venues.FirstOrDefault(v => v.Id == venueId);
                if (venue == null) continue;

                // Only move the venue if no other teams in the source season reference it
                var otherTeamsUsingVenue = data.Teams.Any(t =>
                    t.SeasonId == sourceSeasonId &&
                    !teamIds.Contains(t.Id) &&
                    t.VenueId == venueId);

                if (!otherTeamsUsingVenue)
                    venue.SeasonId = targetSeasonId;
            }
        }

        /// <summary>
        /// Update a season's start/end dates to match the actual fixture date range.
        /// </summary>
        private static void UpdateSeasonDatesFromFixtures(LeagueData data, Guid seasonId)
        {
            var season = data.Seasons.FirstOrDefault(s => s.Id == seasonId);
            if (season == null) return;

            var dates = data.Fixtures
                .Where(f => f.SeasonId == seasonId && f.Date > DateTime.MinValue)
                .Select(f => f.Date.Date)
                .ToList();

            if (dates.Count == 0) return;

            season.StartDate = dates.Min();
            season.EndDate = dates.Max();
            season.ModifiedDate = DateTime.UtcNow;
        }

        private View CreateDataManagementPanel()
        {
            var statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            // ========== ORPHAN SCAN RESULTS ==========
            var orphanResultsLabel = new Label { Text = "", FontSize = 12, LineHeight = 1.4 };
            var orphanResultsBorder = new Border
            {
                IsVisible = false,
                Padding = 12,
                Margin = new Thickness(0, 12, 0, 0),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 8 }
            };
            orphanResultsBorder.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#E5E7EB"), Color.FromArgb("#374151"));
            orphanResultsBorder.SetAppThemeColor(Border.BackgroundColorProperty, CardBackground, CardBackground);
            orphanResultsBorder.Content = orphanResultsLabel;

            var cleanButton = new Button
            {
                Text = $"{Emojis.Delete} Remove Orphaned Data",
                BackgroundColor = Color.FromArgb("#EF4444"),
                TextColor = Colors.White,
                Padding = new Thickness(24, 14),
                HorizontalOptions = LayoutOptions.Start,
                IsVisible = false,
                Margin = new Thickness(0, 8, 0, 0)
            };

            // ========== SCAN BUTTON ==========
            var scanButton = new Button
            {
                Text = $"{Emojis.Wrench} Scan for Orphaned Data",
                BackgroundColor = Color.FromArgb("#F59E0B"),
                TextColor = Colors.White,
                Padding = new Thickness(24, 14),
                HorizontalOptions = LayoutOptions.Start
            };

            scanButton.Clicked += (s, e) =>
            {
                var data = DataStore.Data;
                var validSeasonIds = new HashSet<Guid>(data.Seasons.Select(s2 => s2.Id));

                int orphanFixtures = data.Fixtures.Count(f => f.SeasonId == null || !validSeasonIds.Contains(f.SeasonId.Value));
                int orphanPlayers = data.Players.Count(p => p.SeasonId == null || !validSeasonIds.Contains(p.SeasonId.Value));
                int orphanTeams = data.Teams.Count(t => t.SeasonId == null || !validSeasonIds.Contains(t.SeasonId.Value));
                int orphanVenues = data.Venues.Count(v => v.SeasonId == null || !validSeasonIds.Contains(v.SeasonId.Value));
                int orphanDivisions = data.Divisions.Count(d => d.SeasonId == null || !validSeasonIds.Contains(d.SeasonId.Value));
                int orphanCompetitions = data.Competitions.Count(c => c.SeasonId == null || !validSeasonIds.Contains(c.SeasonId.Value));

                int totalOrphans = orphanFixtures + orphanPlayers + orphanTeams + orphanVenues + orphanDivisions + orphanCompetitions;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Seasons in database: {data.Seasons.Count}");
                sb.AppendLine();
                sb.AppendLine("Orphaned records (no valid season):");
                sb.AppendLine($"  � Divisions: {orphanDivisions}");
                sb.AppendLine($"  � Venues: {orphanVenues}");
                sb.AppendLine($"  � Teams: {orphanTeams}");
                sb.AppendLine($"  � Players: {orphanPlayers}");
                sb.AppendLine($"  � Fixtures: {orphanFixtures}");
                sb.AppendLine($"  � Competitions: {orphanCompetitions}");
                sb.AppendLine();
                sb.AppendLine($"Total orphaned records: {totalOrphans}");

                if (totalOrphans == 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("✅ Storage is clean � no orphaned data found.");
                }

                orphanResultsLabel.Text = sb.ToString();
                orphanResultsBorder.IsVisible = true;
                cleanButton.IsVisible = totalOrphans > 0;

                if (totalOrphans > 0)
                {
                    orphanResultsBorder.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#F59E0B"), Color.FromArgb("#F59E0B"));
                    statusLabel.Text = $"{Emojis.Warning} Found {totalOrphans} orphaned records.";
                    statusLabel.TextColor = Color.FromArgb("#F59E0B");
                }
                else
                {
                    orphanResultsBorder.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#10B981"), Color.FromArgb("#10B981"));
                    statusLabel.Text = "✅ No orphaned data found.";
                    statusLabel.TextColor = Color.FromArgb("#10B981");
                }
            };

            cleanButton.Clicked += async (s, e) =>
            {
                var data = DataStore.Data;
                var validSeasonIds = new HashSet<Guid>(data.Seasons.Select(s2 => s2.Id));

                int orphanFixtures = data.Fixtures.Count(f => f.SeasonId == null || !validSeasonIds.Contains(f.SeasonId.Value));
                int orphanPlayers = data.Players.Count(p => p.SeasonId == null || !validSeasonIds.Contains(p.SeasonId.Value));
                int orphanTeams = data.Teams.Count(t => t.SeasonId == null || !validSeasonIds.Contains(t.SeasonId.Value));
                int orphanVenues = data.Venues.Count(v => v.SeasonId == null || !validSeasonIds.Contains(v.SeasonId.Value));
                int orphanDivisions = data.Divisions.Count(d => d.SeasonId == null || !validSeasonIds.Contains(d.SeasonId.Value));
                int orphanCompetitions = data.Competitions.Count(c => c.SeasonId == null || !validSeasonIds.Contains(c.SeasonId.Value));
                int total = orphanFixtures + orphanPlayers + orphanTeams + orphanVenues + orphanDivisions + orphanCompetitions;

                var confirm = await DisplayAlert(
                    "Clean Storage",
                    $"This will permanently remove {total} orphaned records:\n\n" +
                    $"� {orphanDivisions} Division(s)\n" +
                    $"� {orphanVenues} Venue(s)\n" +
                    $"� {orphanTeams} Team(s)\n" +
                    $"� {orphanPlayers} Player(s)\n" +
                    $"� {orphanFixtures} Fixture(s)\n" +
                    $"� {orphanCompetitions} Competition(s)\n\n" +
                    "This cannot be undone!",
                    "Yes, Clean Storage",
                    "Cancel");

                if (!confirm) return;

                data.CleanupOrphans();
                DataStore.Save();

                orphanResultsLabel.Text = $"✅ Removed {total} orphaned records.\n\nStorage is now clean.";
                orphanResultsBorder.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#10B981"), Color.FromArgb("#10B981"));
                cleanButton.IsVisible = false;
                statusLabel.Text = $"✅ Cleaned {total} orphaned records.";
                statusLabel.TextColor = Color.FromArgb("#10B981");
            };

            // ========== DELETE ALL SEASONS ==========
            var deleteAllSeasonsBtn = new Button
            {
                Text = $"{Emojis.Delete} Delete All Seasons",
                BackgroundColor = Color.FromArgb("#DC2626"),
                TextColor = Colors.White,
                Padding = new Thickness(24, 14),
                HorizontalOptions = LayoutOptions.Start
            };

            deleteAllSeasonsBtn.Clicked += async (s, e) =>
            {
                var data = DataStore.Data;
                var seasonCount = data.Seasons.Count;

                if (seasonCount == 0)
                {
                    await DisplayAlert("No Seasons", "There are no seasons to delete.", "OK");
                    return;
                }

                var totalDivisions = data.Divisions.Count;
                var totalTeams = data.Teams.Count;
                var totalPlayers = data.Players.Count;
                var totalFixtures = data.Fixtures.Count;
                var totalVenues = data.Venues.Count;
                var totalCompetitions = data.Competitions.Count;

                var firstConfirm = await DisplayAlert(
                    "⚠️ Delete All Seasons",
                    $"This will permanently delete ALL {seasonCount} season{(seasonCount != 1 ? "s" : "")} and all associated data:\n\n" +
                    $"• {totalDivisions} Division{(totalDivisions != 1 ? "s" : "")}\n" +
                    $"• {totalTeams} Team{(totalTeams != 1 ? "s" : "")}\n" +
                    $"• {totalPlayers} Player{(totalPlayers != 1 ? "s" : "")}\n" +
                    $"• {totalFixtures} Fixture{(totalFixtures != 1 ? "s" : "")}\n" +
                    $"• {totalVenues} Venue{(totalVenues != 1 ? "s" : "")}\n" +
                    $"• {totalCompetitions} Competition{(totalCompetitions != 1 ? "s" : "")}\n\n" +
                    "This cannot be undone!",
                    "Continue",
                    "Cancel");

                if (!firstConfirm) return;

                var finalConfirm = await DisplayAlert(
                    "🛑 Final Confirmation",
                    $"Are you absolutely sure?\n\nYou are about to delete ALL {seasonCount} seasons and every piece of league data.\n\nType-safety note: a backup (.bak) will be kept.",
                    $"Yes, Delete All {seasonCount} Seasons",
                    "Cancel");

                if (!finalConfirm) return;

                // Check for locked seasons
                var lockedSeasons = data.Seasons.Where(season => season.IsLocked).ToList();
                if (lockedSeasons.Count > 0)
                {
                    await DisplayAlert($"{Helpers.Emojis.Lock} Locked Seasons",
                        $"{lockedSeasons.Count} season(s) are locked and will be skipped:\n" +
                        string.Join("\n", lockedSeasons.Select(ls => $"• {ls.Name}")),
                        "OK");
                }

                // Perform deletion (skip locked seasons)
                var seasonIds = data.Seasons.Where(season => !season.IsLocked).Select(season => season.Id).ToList();
                foreach (var id in seasonIds)
                {
                    data.DeleteSeasonCascade(id);
                }

                // Also clean up any orphaned data
                data.CleanupOrphans();

                DataStore.Save();

                var deletedCount = seasonIds.Count;
                statusLabel.Text = $"✅ Deleted {deletedCount} season{(deletedCount != 1 ? "s" : "")} and all associated data." +
                    (lockedSeasons.Count > 0 ? $" ({lockedSeasons.Count} locked season(s) kept.)" : "");
                statusLabel.TextColor = Color.FromArgb("#10B981");

                // Refresh the panel to update counts
                ShowCategory("Data Management");
            };

            var deleteAllSeasonsBorder = new Border
            {
                Padding = 16,
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#DC2626"),
                Margin = new Thickness(0, 24, 0, 0),
                Content = new VerticalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new Label
                        {
                            Text = "⚠️ Danger Zone",
                            FontSize = 16,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#DC2626")
                        },
                        new Label
                        {
                            Text = "Delete all seasons and their associated data (divisions, teams, players, fixtures, venues, competitions). " +
                                   "This is useful after a bad import that created many unwanted seasons. A backup file (.bak) is kept automatically.",
                            FontSize = 13,
                            LineHeight = 1.4
                        },
                        deleteAllSeasonsBtn
                    }
                }
            };
            deleteAllSeasonsBorder.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#FEF2F2"), Color.FromArgb("#450A0A"));

            return new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Data Management", FontSize = 20, FontAttributes = FontAttributes.Bold },
                    new Label
                    {
                        Text = "Scan and remove data that is not associated with any season. " +
                               "This can happen when seasons are deleted but some records were left behind.",
                        FontSize = 14,
                        TextColor = SubtitleText,
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    scanButton,
                    orphanResultsBorder,
                    cleanButton,
                    deleteAllSeasonsBorder,
                    statusLabel
                }
            };
        }

        private View CreateAboutPanel()
        {
            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            // Version label with easter egg
            var versionLabel = new Label
            {
                Text = "WDPL2 v2.0.0",
                FontSize = 24,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#3B82F6"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var subtitleLabel = new Label
            {
                Text = "The Next Generation Pool League Manager",
                FontSize = 14,
                TextColor = Color.FromArgb("#666"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };

            var infoFrame = new Border
            {
                Padding = 12,
                BackgroundColor = Color.FromArgb("#F0F9FF"),
                Stroke = Color.FromArgb("#3B82F6"),
                StrokeThickness = 1,
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label { Text = "How Settings Work", FontAttributes = FontAttributes.Bold, FontSize = 14, TextColor = Colors.Black },
                        new Label
                        {
                            FontSize = 12,
                            LineHeight = 1.4,
                            TextColor = Colors.Black,
                            Text = "� Settings are saved with your league data\n" +
                                   "� Player ratings use VBA-style cumulative weighted calculation\n" +
                                   "� Rating changes based on opponent strength at time of match\n" +
                                   "� Changes to rating settings require refreshing the Tables page\n" +
                                   "� Fixture defaults only apply to newly generated fixtures\n" +
                                   "� Use 'Reset to Defaults' to restore original values"
                        }
                    }
                }
            };

            var techStack = new Border
            {
                Padding = 12,
                BackgroundColor = Color.FromArgb("#F0FDF4"),
                Stroke = Color.FromArgb("#10B981"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 12, 0, 0),
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label { Text = "Technology Stack", FontAttributes = FontAttributes.Bold, FontSize = 14, TextColor = Colors.Black },
                        new Label
                        {
                            FontSize = 12,
                            LineHeight = 1.4,
                            TextColor = Colors.Black,
                            Text = "� .NET 9 MAUI (Multi-platform App UI)\n" +
                                   "� C# 13\n" +
                                   "� Cross-platform: Windows, macOS, iOS, Android\n" +
                                   "� JSON local storage\n" +
                                   "� VBA-compatible rating algorithm"
                        }
                    }
                }
            };

            // Hidden hint for easter egg
            var hintLabel = new Label
            {
                Text = $"{Emojis.Sparkles} Tip: Some secrets are hidden in plain sight...",
                FontSize = 11,
                TextColor = Color.FromArgb("#999"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 24, 0, 0),
                FontAttributes = FontAttributes.Italic
            };

            return new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    versionLabel,
                    subtitleLabel,
                    new Label { Text = "About This App", FontSize = 20, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Information about league settings and data management", FontSize = 14, TextColor = Color.FromArgb("#666"), Margin = new Thickness(0, 0, 0, 8) },
                    infoFrame,
                    techStack,
                    hintLabel,
                    _statusLabel
                }
            };
        }

        private async void OnSaveClicked()
        {
            try
            {
                // Validate and save Player Rating settings
                if (_startingRatingEntry != null)
                {
                    if (!int.TryParse(_startingRatingEntry.Text, out var startingRating) || startingRating < 0)
                    {
                        await DisplayAlert("Invalid Input", "Starting rating must be a positive number.", "OK");
                        return;
                    }
                    Settings.RatingStartValue = startingRating;
                }

                if (_ratingWeightingEntry != null)
                {
                    if (!int.TryParse(_ratingWeightingEntry.Text, out var weighting) || weighting < 1)
                    {
                        await DisplayAlert("Invalid Input", "Rating weighting must be at least 1.", "OK");
                        return;
                    }
                    Settings.RatingWeighting = weighting;
                }

                if (_ratingsBiasEntry != null)
                {
                    if (!int.TryParse(_ratingsBiasEntry.Text, out var bias) || bias < 0)
                    {
                        await DisplayAlert("Invalid Input", "Ratings bias must be 0 or higher.", "OK");
                        return;
                    }
                    Settings.RatingsBias = bias;
                }

                if (_winFactorEntry != null)
                {
                    if (!double.TryParse(_winFactorEntry.Text, out var winFactor) || winFactor < 0 || winFactor > 10)
                    {
                        await DisplayAlert("Invalid Input", "Win factor must be between 0 and 10.", "OK");
                        return;
                    }
                    Settings.WinFactor = winFactor;
                }

                if (_lossFactorEntry != null)
                {
                    if (!double.TryParse(_lossFactorEntry.Text, out var lossFactor) || lossFactor < 0 || lossFactor > 10)
                    {
                        await DisplayAlert("Invalid Input", "Loss factor must be between 0 and 10.", "OK");
                        return;
                    }
                    Settings.LossFactor = lossFactor;
                }

                if (_eightBallFactorEntry != null)
                {
                    if (!double.TryParse(_eightBallFactorEntry.Text, out var eightBallFactor) || eightBallFactor < 0 || eightBallFactor > 10)
                    {
                        await DisplayAlert("Invalid Input", "8-ball factor must be between 0 and 10.", "OK");
                        return;
                    }
                    Settings.EightBallFactor = eightBallFactor;
                }

                if (_useEightBallSwitch != null)
                    Settings.UseEightBallFactor = _useEightBallSwitch.IsToggled;

                if (_minFramesEntry != null)
                {
                    if (!int.TryParse(_minFramesEntry.Text, out var minFramesPct) || minFramesPct < 0 || minFramesPct > 100)
                    {
                        await DisplayAlert("Invalid Input", "Min frames percentage must be between 0 and 100.", "OK");
                        return;
                    }
                    Settings.MinFramesPercentage = minFramesPct;
                }

                // Save Match Scoring settings
                if (_pointsForWinEntry != null && int.TryParse(_pointsForWinEntry.Text, out var ptsWin) && ptsWin >= 0)
                    Settings.MatchWinBonus = ptsWin;

                if (_pointsForDrawEntry != null && int.TryParse(_pointsForDrawEntry.Text, out var ptsDraw) && ptsDraw >= 0)
                    Settings.MatchDrawBonus = ptsDraw;

                // Save Fixture Defaults
                if (_framesPerMatchEntry != null && int.TryParse(_framesPerMatchEntry.Text, out var frames) && frames >= 1)
                    Settings.DefaultFramesPerMatch = frames;

                if (_matchDayPicker != null)
                    Settings.DefaultMatchDay = (DayOfWeek)_matchDayPicker.SelectedIndex;

                if (_matchTimePicker != null)
                    Settings.DefaultMatchTime = _matchTimePicker.Time;

                if (_roundsPerOpponentEntry != null && int.TryParse(_roundsPerOpponentEntry.Text, out var rounds) && rounds >= 1)
                    Settings.DefaultRoundsPerOpponent = rounds;

                DataStore.Save();

                if (_statusLabel != null)
                    _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  Settings saved successfully.";

                await DisplayAlert("Settings Saved", "Your settings have been saved.", "OK");
            }
            catch (Exception ex)
            {
                if (_statusLabel != null)
                    _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  Error: {ex.Message}";
                await DisplayAlert("Error", $"Failed to save settings: {ex.Message}", "OK");
            }
        }

        private async void OnResetClicked()
        {
            var scope = IsEditingSeasonOverride ? "this season's settings" : "the global default settings";
            var confirm = await DisplayAlert(
                "Reset Settings",
                $"Are you sure you want to reset {scope} to their default values?",
                "Reset",
                "Cancel");

            if (!confirm) return;

            Settings.ResetToDefaults();
            DataStore.Save();

            var selected = CategoriesList.SelectedItem as string;
            ShowCategory(selected);

            if (_statusLabel != null)
                _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  Settings reset to defaults.";
        }

        private async System.Threading.Tasks.Task OnRecalculateAllRatingsAsync()
        {
            var data = DataStore.Data;

            // Count frames with VBA data to show the user what will be affected
            int vbaFrameCount = 0;
            int totalFrames = 0;
            foreach (var fixture in data.Fixtures)
            {
                foreach (var frame in fixture.Frames)
                {
                    totalFrames++;
                    if (frame.HomePlayerRating.HasValue || frame.AwayPlayerRating.HasValue ||
                        frame.HomeOppRating.HasValue || frame.AwayOppRating.HasValue)
                    {
                        vbaFrameCount++;
                    }
                }
            }

            if (vbaFrameCount == 0)
            {
                await DisplayAlert("No Imported Data",
                    $"None of the {totalFrames} frames have VBA pre-calculated rating data.\n\n" +
                    "All ratings are already being calculated using your current settings.",
                    "OK");
                return;
            }

            var confirm = await DisplayAlert(
                "\U0001F504 Recalculate All Ratings",
                $"This will clear VBA pre-calculated rating data from {vbaFrameCount} frame(s) " +
                $"(out of {totalFrames} total).\n\n" +
                "All ratings will then be recalculated from scratch using your current settings:\n" +
                $"  • Start Value: {Settings.RatingStartValue}\n" +
                $"  • Win Factor: {Settings.WinFactor:0.00}\n" +
                $"  • Loss Factor: {Settings.LossFactor:0.00}\n" +
                $"  • 8-Ball Factor: {(Settings.UseEightBallFactor ? Settings.EightBallFactor.ToString("0.00") : "disabled")}\n\n" +
                "This cannot be undone. Continue?",
                "Recalculate", "Cancel");

            if (!confirm) return;

            int cleared = RatingCalculator.ClearVbaRatingData(data.Fixtures);
            DataStore.Save();

            if (_statusLabel != null)
                _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  Cleared VBA data from {cleared} frame(s). Ratings will now use current settings.";

            await DisplayAlert($"{Emojis.Success} Ratings Recalculated",
                $"Cleared imported rating data from {cleared} frame(s).\n\n" +
                "All player ratings will now be calculated using your current settings " +
                "the next time league tables or rating views are loaded.",
                "OK");
        }

        private void ApplyResponsiveLayout(double width)
        {
            var left = RootGrid.Children[0];
            var right = RootGrid.Children[1];

            RootGrid.ColumnDefinitions.Clear();
            RootGrid.RowDefinitions.Clear();

            if (width >= 800)
            {
                RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                RootGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));

                Grid.SetRow((BindableObject)left, 0);
                Grid.SetRow((BindableObject)right, 0);
                Grid.SetColumn((BindableObject)left, 0);
                Grid.SetColumn((BindableObject)right, 1);
            }
            else
            {
                RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

                Grid.SetColumn((BindableObject)left, 0);
                Grid.SetColumn((BindableObject)right, 0);
                Grid.SetRow((BindableObject)left, 0);
                Grid.SetRow((BindableObject)right, 1);
            }
        }
    }
}
