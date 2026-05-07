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
using static Wdpl2.Helpers.PanelBuilder;

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

        private readonly ObservableCollection<string> _categories = new()
        {
            "Appearance",
            "Player Ratings",
            "Ratings Guide",
            "Match Scoring",
            "Fixture Defaults",
            "Notifications",
            "Division Management",
            "Data Management",
            "Manual",
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
                "Ratings Guide" => CreateRatingsGuidePanel(),
                "Match Scoring" => CreateMatchScoringPanel(),
                "Fixture Defaults" => CreateFixtureDefaultsPanel(),
                "Notifications" => CreateNotificationsPanel(),
                "Division Management" => CreateDivisionManagementPanel(),
                "Data Management" => CreateDataManagementPanel(),
                "Manual" => CreateManualPanel(),
                "About" => CreateAboutPanel(),
                _ => null
            };

            ContentPanel.Content = content;
        }

        // ═══════════════════════════════════════════════════════════
        //  APPEARANCE
        // ═══════════════════════════════════════════════════════════

        private View CreateAppearancePanel()
        {
            var useSystemThemeSwitch = new Switch { IsToggled = Settings.UseSystemTheme };
            var darkModeSwitch = new Switch { IsToggled = Settings.DarkModeEnabled, IsEnabled = !Settings.UseSystemTheme };
            var statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            var currentThemeLabel = new Label
            {
                Text = GetCurrentThemeText(),
                FontSize = 13,
                TextColor = SubtleText,
                Margin = new Thickness(0, 4, 0, 0)
            };

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

            var root = new VerticalStackLayout { Spacing = 0 };

            root.Children.Add(SectionHeader(Emojis.Settings, "Theme Settings", "Control light and dark mode preferences"));

            root.Children.Add(Card(new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    SettingRow("Follow system theme", useSystemThemeSwitch, "Automatically switches based on your device settings"),
                    SettingRow("Dark mode", darkModeSwitch, "Manual control when system theme is disabled"),
                }
            }));

            root.Children.Add(currentThemeLabel);

            root.Children.Add(InfoPanel("Theme Options",
                "• Follow system theme: Automatically switches between light and dark based on your device settings\n" +
                "• Dark mode: Manual control when system theme is disabled\n\n" +
                "The pool game will also update to match your theme preference."));

            root.Children.Add(statusLabel);

            return root;
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

        // ═══════════════════════════════════════════════════════════
        //  PLAYER RATINGS
        // ═══════════════════════════════════════════════════════════

        private View CreatePlayerRatingsPanel()
        {
            var (r1, startEntry) = NumericRow("Starting rating", Settings.RatingStartValue, "Initial rating for new players");
            var (r2, weightEntry) = NumericRow("Rating weighting", Settings.RatingWeighting, "Base weight applied to each frame");
            var (r3, biasEntry) = NumericRow("Ratings bias (decay)", Settings.RatingsBias, "Progressive weighting increase for later frames");
            var (r4, winEntry) = DecimalRow("Win factor", Settings.WinFactor, "0.00", "Multiplier for winning frames");
            var (r5, lossEntry) = DecimalRow("Loss factor", Settings.LossFactor, "0.00", "Multiplier for losing frames");
            var (r6, eightEntry) = DecimalRow("8-ball factor", Settings.EightBallFactor, "0.00", "Bonus multiplier for 8-ball wins");
            var (r7, minEntry) = NumericRow("Min frames % for table", Settings.MinFramesPercentage, "Percentage of max frames needed to appear in table");

            _startingRatingEntry = startEntry;
            _ratingWeightingEntry = weightEntry;
            _ratingsBiasEntry = biasEntry;
            _winFactorEntry = winEntry;
            _lossFactorEntry = lossEntry;
            _eightBallFactorEntry = eightEntry;
            _minFramesEntry = minEntry;

            _useEightBallSwitch = new Switch { IsToggled = Settings.UseEightBallFactor };
            _useEightBallSwitch.Toggled += (s, e) => _eightBallFactorEntry.IsEnabled = e.Value;
            _eightBallFactorEntry.IsEnabled = Settings.UseEightBallFactor;

            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            var recalcBtn = new Button
            {
                Text = "\U0001F504 Recalculate All Ratings",
                Margin = new Thickness(0, 4, 0, 0)
            };
            recalcBtn.SetDynamicResource(Button.StyleProperty, "WarningButtonStyle");
            recalcBtn.Command = new Command(async () => await OnRecalculateAllRatingsAsync());

            var saveBtn = new Button { Text = "Save Settings" };
            saveBtn.SetDynamicResource(Button.StyleProperty, "PrimaryButtonStyle");
            saveBtn.Command = new Command(OnSaveClicked);

            var resetBtn = new Button { Text = "Reset to Defaults" };
            resetBtn.SetDynamicResource(Button.StyleProperty, "DangerButtonStyle");
            resetBtn.Command = new Command(OnResetClicked);

            var buttons = new HorizontalStackLayout
            {
                Spacing = 12,
                Margin = new Thickness(0, 16, 0, 0),
                Children = { saveBtn, resetBtn }
            };

            var root = new VerticalStackLayout { Spacing = 0 };

            root.Children.Add(SectionHeader(Emojis.Chart, "Player Rating System", "VBA-style opponent-based cumulative weighted rating system"));

            root.Children.Add(BuildSeasonScopeSelector("Player Ratings"));

            root.Children.Add(Card(new VerticalStackLayout
            {
                Spacing = 0,
                Children = { r1, r2, r3, r4, r5, SettingRow("Use 8-ball factor", _useEightBallSwitch), r6, r7 }
            }));

            root.Children.Add(InfoPanel("VBA-Style Cumulative Weighted Rating",
                "• Earlier frames have lower weight (Weighting − Bias × frames)\n" +
                "• Later frames have higher weight (progressive bias increase)\n" +
                "• Rating based on opponent strength at time of match\n" +
                "• Win against stronger opponent = higher rating gain\n\n" +
                "Min Frames %:\n" +
                "Percentage of maximum available frames needed to appear in ratings table.\n" +
                "Example: If max is 30 frames and you set 60%, players need 18 frames.\n" +
                "All players still have ratings calculated.\n\n" +
                "Formula:\n" +
                "Rating = Σ(OpponentRating × Factor × Weight) / ΣWeight"));

            root.Children.Add(buttons);

            root.Children.Add(WarningPanel("Recalculate Ratings",
                "Imported data from VBA/SQL may contain pre-calculated rating values.\n" +
                "Use the button below to clear those and recalculate all ratings from scratch using the current settings."));

            root.Children.Add(recalcBtn);
            root.Children.Add(_statusLabel);

            return root;
        }

        // ═══════════════════════════════════════════════════════════
        //  MATCH SCORING
        // ═══════════════════════════════════════════════════════════

        private View CreateMatchScoringPanel()
        {
            var (r1, winEntry) = NumericRow("Match win bonus", Settings.MatchWinBonus, "Bonus points for winning the match");
            var (r2, drawEntry) = NumericRow("Match draw bonus", Settings.MatchDrawBonus, "Bonus points for a drawn match");

            _pointsForWinEntry = winEntry;
            _pointsForDrawEntry = drawEntry;
            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            var saveBtn = new Button { Text = "Save Settings" };
            saveBtn.SetDynamicResource(Button.StyleProperty, "PrimaryButtonStyle");
            saveBtn.Command = new Command(OnSaveClicked);

            var resetBtn = new Button { Text = "Reset to Defaults" };
            resetBtn.SetDynamicResource(Button.StyleProperty, "DangerButtonStyle");
            resetBtn.Command = new Command(OnResetClicked);

            var buttons = new HorizontalStackLayout
            {
                Spacing = 12,
                Margin = new Thickness(0, 16, 0, 0),
                Children = { saveBtn, resetBtn }
            };

            // Tiebreaker section
            var tiebreakerSection = BuildTiebreakerSection();

            var root = new VerticalStackLayout { Spacing = 0 };

            root.Children.Add(SectionHeader(Emojis.Trophy, "Match Scoring", "Configure how team match points are awarded"));

            root.Children.Add(BuildSeasonScopeSelector("Match Scoring"));

            root.Children.Add(Card(new VerticalStackLayout { Spacing = 0, Children = { r1, r2 } }));

            root.Children.Add(InfoPanel("Points System",
                "Team points = Frames Won + Bonus\n\n" +
                "• Win: Frames Won + Match Win Bonus\n" +
                "• Draw: Frames Won + Match Draw Bonus\n" +
                "• Loss: Frames Won (no bonus)\n\n" +
                "Example: Team wins 6-4 with Win Bonus=2:\n" +
                "  Winner gets 6+2=8 points\n" +
                "  Loser gets 4 points"));

            root.Children.Add(tiebreakerSection);
            root.Children.Add(buttons);
            root.Children.Add(_statusLabel);

            return root;
        }

        /// <summary>
        /// Builds the tiebreaker configuration section with move-up / move-down / add / remove controls.
        /// </summary>
        private View BuildTiebreakerSection()
        {
            var root = new VerticalStackLayout { Spacing = 4, Margin = new Thickness(0, 16, 0, 0) };

            root.Children.Add(new Label { Text = "Tiebreaker Order", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = TitleText });
            var subtitle = new Label { Text = "When teams are level on points, these criteria are applied in order (top = highest priority)", FontSize = 13, TextColor = SubtleText, Margin = new Thickness(0, 0, 0, 8) };
            root.Children.Add(subtitle);

            _tiebreakerListLayout = new VerticalStackLayout { Spacing = 6 };
            RebuildTiebreakerRows();
            root.Children.Add(_tiebreakerListLayout);

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

            root.Children.Add(addPicker);
            return root;
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

                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = 30 },
                        new ColumnDefinition(),
                        new ColumnDefinition { Width = GridLength.Auto },
                    },
                    Padding = new Thickness(12, 8),
                    BackgroundColor = FieldBg,
                    MinimumHeightRequest = 40
                };

                var posLabel = new Label
                {
                    Text = $"{index + 1}.",
                    FontAttributes = FontAttributes.Bold,
                    VerticalTextAlignment = TextAlignment.Center,
                    TextColor = BodyText
                };

                var nameLabel = new Label
                {
                    Text = FormatCriterionName(criterion),
                    VerticalTextAlignment = TextAlignment.Center,
                    TextColor = BodyText
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

                var btnRow = new HorizontalStackLayout { Spacing = 4, Children = { upBtn, downBtn, removeBtn } };

                row.Add(posLabel, 0, 0);
                row.Add(nameLabel, 1, 0);
                row.Add(btnRow, 2, 0);

                _tiebreakerListLayout.Children.Add(row);
            }

            if (order.Count == 0)
            {
                _tiebreakerListLayout.Children.Add(new Label
                {
                    Text = "No tiebreakers configured — tied teams will share the same position.",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Italic,
                    TextColor = SubtleText
                });
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
        /// </summary>
        private View BuildSeasonScopeSelector(string categoryToRefresh)
        {
            var seasons = DataStore.Data.Seasons.OrderByDescending(s => s.StartDate).ToList();

            var seasonPicker = new Picker { Title = "Settings scope", HorizontalOptions = LayoutOptions.Fill };
            seasonPicker.Items.Add("Global Defaults");
            foreach (var s in seasons)
                seasonPicker.Items.Add(s.Name ?? $"Season {s.StartDate.Year}");

            if (_editingSeasonId.HasValue)
            {
                var idx = seasons.FindIndex(s => s.Id == _editingSeasonId.Value);
                seasonPicker.SelectedIndex = idx >= 0 ? idx + 1 : 0;
            }
            else
            {
                seasonPicker.SelectedIndex = 0;
            }

            var customToggle = new Switch { IsToggled = IsEditingSeasonOverride, HorizontalOptions = LayoutOptions.Start };
            var customLabel = new Label { Text = "Use custom settings for this season", VerticalTextAlignment = TextAlignment.Center, FontSize = 13, TextColor = BodyText };
            var toggleRow = new HorizontalStackLayout { Spacing = 8, Children = { customToggle, customLabel } };
            toggleRow.IsVisible = _editingSeasonId.HasValue;

            var scopeInfo = new Label { FontSize = 12, TextColor = SubtleText, Margin = new Thickness(0, 4, 0, 0) };

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
                    season.Settings ??= DataStore.Data.Settings.Clone();
                else
                    season.Settings = null;

                DataStore.Save();
                ShowCategory(categoryToRefresh);
            };

            return Card(new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label { Text = $"{Emojis.Settings} Settings Scope", FontAttributes = FontAttributes.Bold, FontSize = 14, TextColor = TitleText },
                    seasonPicker,
                    toggleRow,
                    scopeInfo
                }
            });
        }

        // ═══════════════════════════════════════════════════════════
        //  FIXTURE DEFAULTS
        // ═══════════════════════════════════════════════════════════

        private View CreateFixtureDefaultsPanel()
        {
            var (r1, framesEntry) = NumericRow("Frames per match", Settings.DefaultFramesPerMatch);

            _matchDayPicker = new Picker
            {
                ItemsSource = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().Select(d => d.ToString()).ToList(),
                SelectedIndex = (int)Settings.DefaultMatchDay,
                WidthRequest = 160,
                FontSize = 14
            };

            _matchTimePicker = new TimePicker { Format = "HH:mm", Time = Settings.DefaultMatchTime };

            var (r4, roundsEntry) = NumericRow("Rounds per opponent", Settings.DefaultRoundsPerOpponent);

            _framesPerMatchEntry = framesEntry;
            _roundsPerOpponentEntry = roundsEntry;
            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            var saveBtn = new Button { Text = "Save Settings" };
            saveBtn.SetDynamicResource(Button.StyleProperty, "PrimaryButtonStyle");
            saveBtn.Command = new Command(OnSaveClicked);

            var resetBtn = new Button { Text = "Reset to Defaults" };
            resetBtn.SetDynamicResource(Button.StyleProperty, "DangerButtonStyle");
            resetBtn.Command = new Command(OnResetClicked);

            var buttons = new HorizontalStackLayout
            {
                Spacing = 12,
                Margin = new Thickness(0, 16, 0, 0),
                Children = { saveBtn, resetBtn }
            };

            var root = new VerticalStackLayout { Spacing = 0 };

            root.Children.Add(SectionHeader(Emojis.Fixture, "Fixture Generation Defaults", "Default values used when generating new fixtures"));

            root.Children.Add(BuildSeasonScopeSelector("Fixture Defaults"));

            root.Children.Add(Card(new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    r1,
                    SettingRow("Default match day", _matchDayPicker),
                    SettingRow("Default match time", _matchTimePicker),
                    r4
                }
            }));

            root.Children.Add(InfoBanner("These defaults are used when generating fixtures for a season. You can override them when creating a specific season."));

            root.Children.Add(buttons);
            root.Children.Add(_statusLabel);

            return root;
        }

        // ═══════════════════════════════════════════════════════════
        //  NOTIFICATIONS
        // ═══════════════════════════════════════════════════════════

        private View CreateNotificationsPanel()
        {
            // Resolve services once — these can be null on platforms without registration.
            var services = Handler?.MauiContext?.Services;
            var notificationService = services?.GetService<INotificationService>();
            var reminderService = services?.GetService<MatchReminderService>();

            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            // ── User Preferences ──
            var matchRemindersSwitch = new Switch { IsToggled = Settings.MatchRemindersEnabled };
            var resultNotificationsSwitch = new Switch { IsToggled = Settings.ResultNotificationsEnabled };
            var weeklyFixtureSwitch = new Switch { IsToggled = Settings.WeeklyFixtureListEnabled };

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
                    _ => 1
                },
                WidthRequest = 140,
                FontSize = 14
            };

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
                    0 => 1, 1 => 2, 2 => 4, 3 => 6, 4 => 12, 5 => 24, _ => 2
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

            // ── Buttons ──
            var requestPermissionsBtn = new Button { Text = $"{Emojis.Bell} Request Notification Permissions" };
            requestPermissionsBtn.SetDynamicResource(Button.StyleProperty, "PrimaryButtonStyle");

            var testNotificationBtn = new Button { Text = $"{Emojis.Bell} Send Test Notification", Margin = new Thickness(0, 4) };
            testNotificationBtn.SetDynamicResource(Button.StyleProperty, "SecondaryButtonStyle");

            var cancelAllBtn = new Button { Text = $"{Emojis.Error} Cancel All Notifications", Margin = new Thickness(0, 4) };
            cancelAllBtn.SetDynamicResource(Button.StyleProperty, "DangerButtonStyle");

            var pendingLabel = new Label { Text = "Pending notifications: Checking...", FontSize = 12, TextColor = SubtleText, Margin = new Thickness(0, 8, 0, 0) };

            // ── Event Handlers ──
            requestPermissionsBtn.Clicked += async (s, e) =>
            {
                try
                {
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

            testNotificationBtn.Clicked += async (s, e) =>
            {
                try
                {
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

            cancelAllBtn.Clicked += async (s, e) =>
            {
                try
                {
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

            // ── Build layout ──
            var root = new VerticalStackLayout { Spacing = 0 };

            root.Children.Add(SectionHeader(Emojis.Bell, "Match Notifications", "Customize your notification preferences"));

            root.Children.Add(Card(new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    SettingRow($"{Emojis.Bell} Match reminders", matchRemindersSwitch, "Get notified before your matches"),
                    SettingRow("  Remind me", reminderHoursPicker, "How long before the match"),
                    SettingRow($"{Emojis.Success} Result notifications", resultNotificationsSwitch, "Instant alerts when results are posted"),
                    SettingRow($"{Emojis.Calendar} Weekly fixture list", weeklyFixtureSwitch, "Monday morning fixture summary"),
                }
            }));

            root.Children.Add(Card(new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label { Text = "Setup & Testing", FontAttributes = FontAttributes.Bold, FontSize = 16, TextColor = TitleText },
                    requestPermissionsBtn,
                    testNotificationBtn,
                    pendingLabel,
                    cancelAllBtn,
                }
            }, new Thickness(0, 12, 0, 0)));

            root.Children.Add(InfoPanel("About Notifications",
                "Notification Types:\n" +
                $"{Emojis.Bell} Match Reminders — Get notified before your matches\n" +
                $"{Emojis.Success} Result Alerts — Instant notifications when results are posted\n" +
                $"{Emojis.Calendar} Weekly Summary — Monday morning fixture list\n\n" +
                "Customization:\n" +
                "• Choose reminder timing (1-24 hours before match)\n" +
                "• Enable/disable each notification type independently\n" +
                "• Settings saved automatically when changed\n\n" +
                "How It Works:\n" +
                "• Reminders scheduled automatically when fixtures are generated or saved\n" +
                "• Past matches don't get reminders\n" +
                "• Settings apply to all future notifications\n" +
                "• Works on iOS, Android, and Windows"));

            root.Children.Add(WarningPanel("Important",
                $"{Emojis.Info} You must grant notification permissions first\n" +
                $"{Emojis.Info} Changing reminder time affects new notifications only\n" +
                $"{Emojis.Info} Battery saver mode may delay notifications\n" +
                $"{Emojis.Info} Test notifications to ensure they're working"));

            root.Children.Add(_statusLabel);

            // Check pending notifications after a short delay
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), async () =>
            {
                try
                {
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

            return root;
        }

        // ═══════════════════════════════════════════════════════════
        //  DIVISION MANAGEMENT
        // ═══════════════════════════════════════════════════════════

        private View CreateDivisionManagementPanel()
        {
            var data = DataStore.Data;
            var seasons = data.Seasons.OrderByDescending(s => s.StartDate).ToList();

            var seasonPicker = new Picker { Title = "Select Season", HorizontalOptions = LayoutOptions.Fill };
            foreach (var s in seasons)
                seasonPicker.Items.Add(s.Name ?? $"Season {s.StartDate.Year}");
            if (seasons.Count > 0)
                seasonPicker.SelectedIndex = 0;

            var divisionListContainer = new VerticalStackLayout { Spacing = 8 };
            var statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 4, 0, 0) };

            var selectedDivisionIds = new HashSet<Guid>();

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

            var selectionLabel = new Label { FontSize = 12, TextColor = SubtleText, IsVisible = false };

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
                    divisionListContainer.Children.Add(EmptyState(Emojis.Division, "No divisions", "No divisions in this season."));
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
                        VerticalTextAlignment = TextAlignment.Center,
                        TextColor = TitleText
                    };

                    var statsLabel = new Label
                    {
                        Text = $"{teamCount} teams · {playerCount} players · {fixtureCount} fixtures",
                        FontSize = 12,
                        TextColor = SubtleText
                    };

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

                    var headerRow = new HorizontalStackLayout { Spacing = 8, Children = { selectBox, nameLabel } };
                    var btnRow = new HorizontalStackLayout { Spacing = 6, Children = { renameBtn, mergeBtn, deleteBtn } };

                    divisionListContainer.Children.Add(Card(new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children = { headerRow, statsLabel, btnRow }
                    }));
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

                seasons.Clear();
                seasons.AddRange(data.Seasons.OrderByDescending(ss => ss.StartDate));
                seasonPicker.Items.Clear();
                foreach (var ss in seasons)
                    seasonPicker.Items.Add(ss.Name ?? $"Season {ss.StartDate.Year}");

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
                Padding = new Thickness(24, 14),
                HorizontalOptions = LayoutOptions.Start
            };
            autoCleanBtn.SetDynamicResource(Button.StyleProperty, "WarningButtonStyle");

            autoCleanBtn.Clicked += async (s, e) =>
            {
                if (seasonPicker.SelectedIndex < 0 || seasonPicker.SelectedIndex >= seasons.Count)
                    return;

                var seasonId = seasons[seasonPicker.SelectedIndex].Id;
                var seasonDivisions = data.Divisions.Where(d => d.SeasonId == seasonId).ToList();

                int mergedCount = 0;

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

            // Initial load
            RefreshDivisionList();

            var root = new VerticalStackLayout { Spacing = 0 };

            root.Children.Add(SectionHeader(Emojis.Division, "Division Management", "View, rename, merge, move, and split divisions across seasons"));

            root.Children.Add(Card(new VerticalStackLayout
            {
                Spacing = 8,
                Children = { SettingRow("Season", seasonPicker), autoCleanBtn }
            }));

            root.Children.Add(selectionLabel);

            var bulkRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 8, 0, 0), Children = { splitBtn, moveBtn } };
            root.Children.Add(bulkRow);
            root.Children.Add(statusLabel);
            root.Children.Add(divisionListContainer);

            root.Children.Add(InfoPanel("Division Management",
                "• Rename: Change a division's display name\n" +
                "• Merge: Combine two divisions (moves all teams & fixtures)\n" +
                "• Delete: Remove empty divisions (no teams/fixtures)\n" +
                "• Auto-Clean: Automatically merge duplicates like \"1st\" & \"First\",\n" +
                "  \"Red\" & \"Red Division\", normalize names, and remove empties\n\n" +
                "Select divisions with checkboxes to:\n" +
                "• Split to New Season: Create a new season from selected divisions\n" +
                "• Move to Season: Move selected divisions to an existing season\n" +
                "  (Teams, players, and fixtures move with the division)"));

            return new ScrollView { Content = root };
        }

        /// <summary>
        /// Counts of records whose SeasonId references a non-existent season.
        /// </summary>
        private readonly record struct OrphanCounts(
            int Fixtures, int Players, int Teams, int Venues, int Divisions, int Competitions)
        {
            public int Total => Fixtures + Players + Teams + Venues + Divisions + Competitions;
        }

        /// <summary>
        /// Count records that reference a missing season — used by the Data Management scan & clean handlers.
        /// </summary>
        private static OrphanCounts CountOrphans(LeagueData data)
        {
            var validSeasonIds = new HashSet<Guid>(data.Seasons.Select(s => s.Id));
            return new OrphanCounts(
                Fixtures: data.Fixtures.Count(f => f.SeasonId == null || !validSeasonIds.Contains(f.SeasonId.Value)),
                Players: data.Players.Count(p => p.SeasonId == null || !validSeasonIds.Contains(p.SeasonId.Value)),
                Teams: data.Teams.Count(t => t.SeasonId == null || !validSeasonIds.Contains(t.SeasonId.Value)),
                Venues: data.Venues.Count(v => v.SeasonId == null || !validSeasonIds.Contains(v.SeasonId.Value)),
                Divisions: data.Divisions.Count(d => d.SeasonId == null || !validSeasonIds.Contains(d.SeasonId.Value)),
                Competitions: data.Competitions.Count(c => c.SeasonId == null || !validSeasonIds.Contains(c.SeasonId.Value))
            );
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
        /// Move selected divisions and all associated data from one season to another.
        /// </summary>
        private static void MoveDivisionsToSeason(
            LeagueData data, List<Guid> divisionIds, Guid sourceSeasonId, Guid targetSeasonId)
        {
            var teamIds = new HashSet<Guid>(
                data.Teams
                    .Where(t => t.DivisionId.HasValue && divisionIds.Contains(t.DivisionId.Value))
                    .Select(t => t.Id));

            foreach (var div in data.Divisions.Where(d => divisionIds.Contains(d.Id)))
            {
                div.SeasonId = targetSeasonId;
                div.ModifiedDate = DateTime.UtcNow;
            }

            foreach (var team in data.Teams.Where(t => teamIds.Contains(t.Id)))
                team.SeasonId = targetSeasonId;

            foreach (var player in data.Players.Where(p =>
                         p.SeasonId == sourceSeasonId &&
                         p.TeamId.HasValue &&
                         teamIds.Contains(p.TeamId.Value)))
            {
                player.SeasonId = targetSeasonId;
                player.ModifiedDate = DateTime.UtcNow;
            }

            foreach (var fixture in data.Fixtures.Where(f =>
                         f.DivisionId.HasValue && divisionIds.Contains(f.DivisionId.Value)))
            {
                fixture.SeasonId = targetSeasonId;
            }

            foreach (var fixture in data.Fixtures.Where(f =>
                         f.SeasonId == sourceSeasonId &&
                         !f.DivisionId.HasValue &&
                         (teamIds.Contains(f.HomeTeamId) || teamIds.Contains(f.AwayTeamId))))
            {
                fixture.SeasonId = targetSeasonId;
            }

            var movedVenueIds = data.Teams
                .Where(t => teamIds.Contains(t.Id) && t.VenueId.HasValue)
                .Select(t => t.VenueId!.Value)
                .Distinct()
                .ToList();

            foreach (var venueId in movedVenueIds)
            {
                var venue = data.Venues.FirstOrDefault(v => v.Id == venueId);
                if (venue == null) continue;

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

        // ═══════════════════════════════════════════════════════════
        //  DATA MANAGEMENT
        // ═══════════════════════════════════════════════════════════

        private View CreateDataManagementPanel()
        {
            var statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            // Orphan scan results
            var orphanResultsLabel = new Label { Text = "", FontSize = 12, LineHeight = 1.4, TextColor = BodyText };
            var orphanResultsBorder = new Border
            {
                IsVisible = false,
                Padding = 12,
                Margin = new Thickness(0, 12, 0, 0),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                BackgroundColor = CardBg,
                Stroke = CardStroke,
                Content = orphanResultsLabel
            };

            var cleanButton = new Button
            {
                Text = $"{Emojis.Delete} Remove Orphaned Data",
                IsVisible = false,
                Margin = new Thickness(0, 8, 0, 0)
            };
            cleanButton.SetDynamicResource(Button.StyleProperty, "DangerButtonStyle");

            var scanButton = new Button
            {
                Text = $"{Emojis.Wrench} Scan for Orphaned Data",
                HorizontalOptions = LayoutOptions.Start
            };
            scanButton.SetDynamicResource(Button.StyleProperty, "WarningButtonStyle");

            scanButton.Clicked += (s, e) =>
            {
                var data = DataStore.Data;
                var counts = CountOrphans(data);
                int orphanFixtures = counts.Fixtures;
                int orphanPlayers = counts.Players;
                int orphanTeams = counts.Teams;
                int orphanVenues = counts.Venues;
                int orphanDivisions = counts.Divisions;
                int orphanCompetitions = counts.Competitions;
                int totalOrphans = counts.Total;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Seasons in database: {data.Seasons.Count}");
                sb.AppendLine();
                sb.AppendLine("Orphaned records (no valid season):");
                sb.AppendLine($"  • Divisions: {orphanDivisions}");
                sb.AppendLine($"  • Venues: {orphanVenues}");
                sb.AppendLine($"  • Teams: {orphanTeams}");
                sb.AppendLine($"  • Players: {orphanPlayers}");
                sb.AppendLine($"  • Fixtures: {orphanFixtures}");
                sb.AppendLine($"  • Competitions: {orphanCompetitions}");
                sb.AppendLine();
                sb.AppendLine($"Total orphaned records: {totalOrphans}");

                if (totalOrphans == 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("✅ Storage is clean — no orphaned data found.");
                }

                orphanResultsLabel.Text = sb.ToString();
                orphanResultsBorder.IsVisible = true;
                cleanButton.IsVisible = totalOrphans > 0;

                if (totalOrphans > 0)
                {
                    orphanResultsBorder.Stroke = Color.FromArgb("#F59E0B");
                    statusLabel.Text = $"{Emojis.Warning} Found {totalOrphans} orphaned records.";
                    statusLabel.TextColor = Color.FromArgb("#F59E0B");
                }
                else
                {
                    orphanResultsBorder.Stroke = Color.FromArgb("#10B981");
                    statusLabel.Text = "✅ No orphaned data found.";
                    statusLabel.TextColor = Color.FromArgb("#10B981");
                }
            };

            cleanButton.Clicked += async (s, e) =>
            {
                var data = DataStore.Data;
                var counts = CountOrphans(data);
                int orphanFixtures = counts.Fixtures;
                int orphanPlayers = counts.Players;
                int orphanTeams = counts.Teams;
                int orphanVenues = counts.Venues;
                int orphanDivisions = counts.Divisions;
                int orphanCompetitions = counts.Competitions;
                int total = counts.Total;

                var confirm = await DisplayAlert(
                    "Clean Storage",
                    $"This will permanently remove {total} orphaned records:\n\n" +
                    $"• {orphanDivisions} Division(s)\n" +
                    $"• {orphanVenues} Venue(s)\n" +
                    $"• {orphanTeams} Team(s)\n" +
                    $"• {orphanPlayers} Player(s)\n" +
                    $"• {orphanFixtures} Fixture(s)\n" +
                    $"• {orphanCompetitions} Competition(s)\n\n" +
                    "This cannot be undone!",
                    "Yes, Clean Storage",
                    "Cancel");

                if (!confirm) return;

                data.CleanupOrphans();
                DataStore.Save();

                orphanResultsLabel.Text = $"✅ Removed {total} orphaned records.\n\nStorage is now clean.";
                orphanResultsBorder.Stroke = Color.FromArgb("#10B981");
                cleanButton.IsVisible = false;
                statusLabel.Text = $"✅ Cleaned {total} orphaned records.";
                statusLabel.TextColor = Color.FromArgb("#10B981");
            };

            // Delete All Seasons
            var deleteAllSeasonsBtn = new Button
            {
                Text = $"{Emojis.Delete} Delete All Seasons",
                HorizontalOptions = LayoutOptions.Start
            };
            deleteAllSeasonsBtn.SetDynamicResource(Button.StyleProperty, "DangerButtonStyle");

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

                var lockedSeasons = data.Seasons.Where(season => season.IsLocked).ToList();
                if (lockedSeasons.Count > 0)
                {
                    await DisplayAlert($"{Helpers.Emojis.Lock} Locked Seasons",
                        $"{lockedSeasons.Count} season(s) are locked and will be skipped:\n" +
                        string.Join("\n", lockedSeasons.Select(ls => $"• {ls.Name}")),
                        "OK");
                }

                var seasonIds = data.Seasons.Where(season => !season.IsLocked).Select(season => season.Id).ToList();
                foreach (var id in seasonIds)
                    data.DeleteSeasonCascade(id);

                data.CleanupOrphans();
                DataStore.Save();

                var deletedCount = seasonIds.Count;
                statusLabel.Text = $"✅ Deleted {deletedCount} season{(deletedCount != 1 ? "s" : "")} and all associated data." +
                    (lockedSeasons.Count > 0 ? $" ({lockedSeasons.Count} locked season(s) kept.)" : "");
                statusLabel.TextColor = Color.FromArgb("#10B981");

                ShowCategory("Data Management");
            };

            // ── Build layout ──
            var root = new VerticalStackLayout { Spacing = 0 };

            root.Children.Add(SectionHeader(Emojis.Wrench, "Data Management", "Scan and remove orphaned data, or reset all seasons"));

            root.Children.Add(Card(new VerticalStackLayout
            {
                Spacing = 8,
                Children = { scanButton, orphanResultsBorder, cleanButton }
            }));

            // Danger zone card
            var dangerCard = new Border
            {
                Padding = 16,
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#DC2626"),
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Margin = new Thickness(0, 24, 0, 0),
                BackgroundColor = PanelBuilder.IsDark ? Color.FromArgb("#450A0A") : Color.FromArgb("#FEF2F2"),
                Content = new VerticalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new Label { Text = "⚠️ Danger Zone", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#DC2626") },
                        new Label
                        {
                            Text = "Delete all seasons and their associated data (divisions, teams, players, fixtures, venues, competitions). " +
                                   "This is useful after a bad import that created many unwanted seasons. A backup file (.bak) is kept automatically.",
                            FontSize = 13,
                            LineHeight = 1.4,
                            TextColor = BodyText
                        },
                        deleteAllSeasonsBtn
                    }
                }
            };

            root.Children.Add(dangerCard);
            root.Children.Add(statusLabel);

            return root;
        }

        // ═══════════════════════════════════════════════════════════
        //  RATINGS GUIDE  ("Ratings for Dummies")
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// A friendly, plain-English walkthrough of how WDPL2 calculates player ratings.
        /// Reuses the collapsible <see cref="ManualSectionView"/> for a consistent look
        /// with the in-app Manual.
        /// </summary>
        private View CreateRatingsGuidePanel()
        {
            var root = new VerticalStackLayout { Spacing = 0 };

            root.Children.Add(SectionHeader(Emojis.Chart, "Ratings for Dummies",
                "Plain-English guide to how player ratings work — tap any chapter to expand it"));

            // Track sections so the expand/collapse-all buttons can drive them.
            var sections = new List<ManualSectionView>();

            View Add(string icon, string title, string body, bool startExpanded = false)
            {
                var s = ManualSection(icon, title, body, startExpanded);
                sections.Add(s);
                return s;
            }

            // ---- Expand / Collapse all ----
            var expandAllBtn = new Button
            {
                Text = $"{Emojis.Down}  Expand all",
                FontSize = 12,
                Padding = new Thickness(10, 4),
                HeightRequest = 32
            };
            expandAllBtn.SetDynamicResource(Button.StyleProperty, "SecondaryButtonStyle");
            expandAllBtn.Clicked += (_, _) => { foreach (var s in sections) s.SetExpanded(true); };

            var collapseAllBtn = new Button
            {
                Text = $"{Emojis.Up}  Collapse all",
                FontSize = 12,
                Padding = new Thickness(10, 4),
                HeightRequest = 32
            };
            collapseAllBtn.SetDynamicResource(Button.StyleProperty, "SecondaryButtonStyle");
            collapseAllBtn.Clicked += (_, _) => { foreach (var s in sections) s.SetExpanded(false); };

            root.Children.Add(new HorizontalStackLayout
            {
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 8),
                Children = { expandAllBtn, collapseAllBtn }
            });

            // ---- Foreword (always visible) ----
            root.Children.Add(InfoPanel("Foreword: what is a rating?",
                "A rating is just a number that describes how strong a player is. Higher = better. " +
                "Everyone starts at the same value (1000 by default), and the number goes up or down based on:\n\n" +
                "  • whether you won or lost each frame, and\n" +
                "  • how strong your opponent was at the time.\n\n" +
                "Beating a strong player gains you more than beating a weak player. " +
                "Losing to a weak player costs you more than losing to a strong one. " +
                "That\u2019s the whole idea — the rest is just maths."));

            // ---- Chapter 1 ----
            root.Children.Add(Add(Emojis.Star, "Chapter 1 — The big picture (in 30 seconds)",
                "Every Tuesday night you play a few frames. For each frame WDPL2 asks two questions:\n\n" +
                $"  1. Did you win or lose?\n" +
                $"  2. How strong was your opponent at the start of this week?\n\n" +
                "It turns each frame into a number called the \u201Cframe value\u201D, then mixes all your " +
                "frame values together using a weighted average. Recent frames count more than ancient ones.\n\n" +
                "That weighted average is your rating. Done.\n\n" +
                "Everything in the rest of this guide is just explaining the dials you can turn.",
                startExpanded: true));

            // ---- Chapter 2 ----
            root.Children.Add(Add(Emojis.Sparkles, "Chapter 2 — Where everyone starts",
                "Every player starts at the Starting Rating (default 1000) the very first week of their first season.\n\n" +
                $"{Emojis.Bullet} 1000 is just a number — it has no real-world meaning. It could be 100 or 500.\n" +
                $"{Emojis.Bullet} The only thing that matters is the difference between players. A 1100 player " +
                "is stronger than a 950 player, that\u2019s all.\n" +
                $"{Emojis.Bullet} New players added mid-season also start at 1000 in their first week.\n\n" +
                "You can change the starting value under Settings → Player Ratings, but unless you have a " +
                "reason to, leave it at 1000."));

            // ---- Chapter 3 ----
            root.Children.Add(Add(Emojis.Target, "Chapter 3 — What one frame is worth (the factors)",
                "For every frame you play, WDPL2 first works out a \u201Cframe value\u201D. " +
                "It does this by taking your opponent\u2019s rating going into this week and multiplying it by one of three factors:\n\n" +
                $"  • You WON the frame                  →  opp rating  ×  Win Factor      (default 1.25)\n" +
                $"  • You won on the 8-BALL              →  opp rating  ×  8-Ball Factor   (default 1.35)\n" +
                $"  • You LOST the frame                 →  opp rating  ×  Loss Factor     (default 0.75)\n\n" +
                "Worked example (against an average 1000-rated opponent):\n\n" +
                $"  Win:        1000 × 1.25 = 1250  → a frame valued at 1250\n" +
                $"  8-ball win: 1000 × 1.35 = 1350  → a frame valued at 1350\n" +
                $"  Loss:       1000 × 0.75 =  750  → a frame valued at  750\n\n" +
                "Now play a stronger opponent (rated 1200):\n\n" +
                $"  Win:        1200 × 1.25 = 1500  (worth more than beating a 1000)\n" +
                $"  Loss:       1200 × 0.75 =  900  (still adds to your average!)\n\n" +
                "That\u2019s the key insight: “losing\u201D to a strong opponent can still be a frame value of 900, " +
                "which keeps your rating high. Losing to a 600-rated opponent only gives you 600 × 0.75 = 450, " +
                "which drags it down.\n\n" +
                $"{Emojis.Bullet} Turn the 8-ball bonus on or off with the \u201CUse 8-ball factor\u201D switch.\n" +
                $"{Emojis.Bullet} If you want results to swing harder, raise the Win Factor and lower the Loss Factor."));

            // ---- Chapter 4 ----
            root.Children.Add(Add(Emojis.Clock, "Chapter 4 — Why recent frames matter more (weighting & bias)",
                "Once every one of your frames has a value, WDPL2 doesn\u2019t just average them. " +
                "It does a WEIGHTED average, where newer frames count more than old ones. This is what makes the " +
                "rating feel like \u201Cform\u201D rather than just a season average.\n\n" +
                "Two settings control the weights:\n\n" +
                $"  • Rating Weighting  (default 220) — the weight given to your NEWEST frame.\n" +
                $"  • Ratings Bias      (default   4) — how much LESS each older frame is worth.\n\n" +
                "So if you have played 5 frames in your career, the weights look like:\n\n" +
                $"  Frame 1 (oldest):  220 − 4×4 = 204\n" +
                $"  Frame 2:           220 − 4×3 = 208\n" +
                $"  Frame 3:           220 − 4×2 = 212\n" +
                $"  Frame 4:           220 − 4×1 = 216\n" +
                $"  Frame 5 (newest):  220        = 220\n\n" +
                "Your rating = (sum of frame value × weight) ÷ (sum of weights).\n\n" +
                "What this means in practice:\n\n" +
                $"  • A great night this week shifts your rating more than a great night last March.\n" +
                $"  • Old bad results slowly fade as you play more.\n" +
                $"  • Players who play more frames have more stable ratings (one fluke matters less).\n\n" +
                $"{Emojis.Bullet} If you want NEW frames to dominate even more, increase the Bias.\n" +
                $"{Emojis.Bullet} If you want a smoother, season-long average, decrease the Bias.\n" +
                $"{Emojis.Bullet} (Floor: weight is never allowed to drop below 1, even if the formula goes negative.)"));

            // ---- Chapter 5 ----
            root.Children.Add(Add(Emojis.Wrench, "Chapter 5 — A full worked example",
                "Let\u2019s rate \u201CAlex\u201D after a single night of pool. Alex plays 4 frames " +
                "against opponents who are all rated 1000 going into this week.\n\n" +
                "Defaults: Start 1000, Win 1.25, Loss 0.75, 8-ball 1.35, Weighting 220, Bias 4.\n\n" +
                $"Frame 1: WIN          → value = 1000 × 1.25 = 1250\n" +
                $"Frame 2: LOSS         → value = 1000 × 0.75 =  750\n" +
                $"Frame 3: WIN          → value = 1000 × 1.25 = 1250\n" +
                $"Frame 4: WIN (8-ball) → value = 1000 × 1.35 = 1350\n\n" +
                "With 4 total frames the weights are:\n\n" +
                $"  Frame 1 weight: 220 − 4×3 = 208\n" +
                $"  Frame 2 weight: 220 − 4×2 = 212\n" +
                $"  Frame 3 weight: 220 − 4×1 = 216\n" +
                $"  Frame 4 weight: 220        = 220\n\n" +
                "Numerator   = 1250×208 + 750×212 + 1250×216 + 1350×220\n" +
                "            = 260\u202F000 + 159\u202F000 + 270\u202F000 + 297\u202F000\n" +
                "            = 986\u202F000\n\n" +
                "Denominator = 208 + 212 + 216 + 220 = 856\n\n" +
                "Rating      = 986\u202F000 ÷ 856 ≈ 1151\n\n" +
                "So after one productive night against average opposition, Alex\u2019s rating is about 1151. " +
                "Beating a few stronger players, or winning more on the 8-ball, would push it higher."));

            // ---- Chapter 6 ----
            root.Children.Add(Add(Emojis.Calendar, "Chapter 6 — How weeks work",
                "WDPL2 calculates ratings WEEK BY WEEK, in chronological order, using the season\u2019s " +
                "start date as week 1.\n\n" +
                $"{Emojis.Bullet} At the start of week 1, EVERY player has the starting rating (1000).\n" +
                $"{Emojis.Bullet} All of week 1\u2019s frames are processed using those week-1 ratings.\n" +
                $"{Emojis.Bullet} At the end of the week, each player\u2019s rating is recalculated. That new " +
                "number is the rating they CARRY INTO week 2.\n" +
                $"{Emojis.Bullet} Repeat for every week of the season.\n\n" +
                "Why does this matter?\n\n" +
                $"  • The opponent\u2019s rating used in your frame value is the rating they had GOING INTO " +
                "this week, not their final season rating. So if a player gets hot late in the season, " +
                "early opponents don\u2019t suddenly get retroactive credit for beating them.\n" +
                $"  • Your weekly rating history is stored, which is what powers the rating sparkline " +
                "(▁▂▃▅▆▇█) and the rating-progression chart on player profiles."));

            // ---- Chapter 7 ----
            root.Children.Add(Add(Emojis.Player, "Chapter 7 — The minimum-frames rule",
                "Open the league\u2019s player ratings table and you\u2019ll only see the players who\u2019ve " +
                "played enough frames. That\u2019s the Min Frames % setting (default 60).\n\n" +
                $"{Emojis.Bullet} 60% means a player needs to have played at least 60% of the maximum " +
                "number of frames anyone in their division has played, before they show up.\n" +
                $"{Emojis.Bullet} Example: if the most-played player has 30 frames, others need at least " +
                "30 × 60% = 18 frames to appear.\n" +
                $"{Emojis.Bullet} The threshold ONLY hides players from the table — their rating is still " +
                "calculated and used wherever they show up (their profile, opponent\u2019s frame values, etc.).\n\n" +
                "Why have this rule?\n\n" +
                $"  • With only a handful of frames, a single big win or loss can swing a rating wildly. " +
                "Filtering them out keeps the headline table fair.\n" +
                $"  • Lower the % to be more inclusive (e.g. 40%), raise it to be stricter (e.g. 75%)."));

            // ---- Chapter 8 ----
            root.Children.Add(Add(Emojis.Reload, "Chapter 8 — The Recalculate button",
                "Under Settings → Player Ratings there\u2019s a big yellow button: " +
                "\u201C\U0001F504 Recalculate All Ratings\u201D. What does it actually do?\n\n" +
                $"{Emojis.Bullet} Imported data (especially from the old VBA / Access database) often " +
                "includes pre-baked rating numbers stored on each frame. Normally WDPL2 will TRUST those " +
                "and use them as-is, so historical ratings match what the old system showed.\n" +
                $"{Emojis.Bullet} But that means changing your rating settings does nothing for old data — " +
                "the imported numbers override the formula.\n" +
                $"{Emojis.Bullet} Recalculate All Ratings WIPES those imported per-frame rating values, " +
                "forcing the algorithm to rebuild every player\u2019s rating from scratch using the current " +
                "settings.\n\n" +
                "When to use it:\n\n" +
                $"  • Right after importing legacy data and you want consistent results going forward.\n" +
                $"  • After tweaking Win/Loss/8-ball factors or the Weighting/Bias and you want the " +
                "changes applied to past frames too.\n\n" +
                $"{Emojis.Warning} Take a backup first (Settings → Data Management → Backup). It\u2019s a " +
                "one-way operation — the imported numbers can\u2019t be brought back without restoring."));

            // ---- Chapter 9 ----
            root.Children.Add(Add(Emojis.Settings, "Chapter 9 — Tuning the dials safely",
                "Most leagues never need to change these. But if you do, here\u2019s a cheat sheet.\n\n" +
                $"{Emojis.Bullet} Want WINS to count for more?  → raise Win Factor (e.g. 1.25 → 1.40).\n" +
                $"{Emojis.Bullet} Want LOSSES to hurt more?     → lower Loss Factor (e.g. 0.75 → 0.60).\n" +
                $"{Emojis.Bullet} Want 8-balls to feel special? → raise 8-Ball Factor or keep it at 1.35.\n" +
                $"{Emojis.Bullet} Want NEW form to dominate?    → raise Bias (e.g. 4 → 6 or 8).\n" +
                $"{Emojis.Bullet} Want a SMOOTHER season-long view? → lower Bias toward 0 (≈ simple average).\n" +
                $"{Emojis.Bullet} Want more players in the table? → lower Min Frames % (e.g. 60 → 40).\n\n" +
                "Recommended workflow when experimenting:\n\n" +
                "  1. Take a backup (Data Management → Backup).\n" +
                "  2. Tweak ONE setting at a time.\n" +
                "  3. Tap Recalculate All Ratings.\n" +
                "  4. Open League Tables and see what changed.\n" +
                "  5. If it\u2019s wrong, restore the backup and try a smaller change.\n\n" +
                $"{Emojis.Bullet} Use the Settings Scope selector at the top of the Player Ratings panel " +
                "to keep the global defaults but try different values for ONE season only."));

            // ---- Chapter 10 ----
            root.Children.Add(Add(Emojis.Info, "Chapter 10 — The formula in one line (for the curious)",
                "For each player, sort their frames in chronological order. For frame number i out of N total " +
                "frames, define:\n\n" +
                "  weight_i = max(1,  Weighting − Bias × (N − i))\n" +
                "  value_i  = OpponentRating(at start of that week) × Factor\n\n" +
                "where Factor is:\n\n" +
                "  Win Factor      if you won (and the win wasn\u2019t on the 8-ball, or 8-ball factor is off)\n" +
                "  8-Ball Factor   if you won on the 8-ball and the 8-ball switch is on\n" +
                "  Loss Factor     if you lost\n\n" +
                "Then:\n\n" +
                "  Rating = Σ(value_i × weight_i)  ÷  Σ(weight_i)\n\n" +
                "That\u2019s it. Everything else in this guide is just explaining each piece of that one line."));

            // ---- FAQ ----
            root.Children.Add(Add(Emojis.Note, "Frequently asked questions",
                "Q. Why didn\u2019t my rating go up after I won 3-0?\n" +
                "A. Your opponents may have been low-rated, so each frame value was small. Or the win was " +
                "only a few frames against a much larger career history — the weighted average barely moved.\n\n" +
                "Q. Why does my rating change every week even though I didn\u2019t play?\n" +
                "A. It shouldn\u2019t. If you didn\u2019t play this week your rating carries over unchanged. If " +
                "you\u2019re seeing changes, check that no walkover frames were attributed to you in the " +
                "fixture editor.\n\n" +
                "Q. Two players have the same record but different ratings — why?\n" +
                "A. They played different opponents. Beating stronger opponents earns a higher rating, " +
                "and the timing matters too (recent results count more).\n\n" +
                "Q. Can a rating go below the starting value?\n" +
                "A. Yes. If you lose most of your frames against weak opponents your weighted average " +
                "will sit below 1000.\n\n" +
                "Q. Are ratings comparable across seasons?\n" +
                "A. Roughly, yes — the formula is the same, but each season starts everyone fresh at 1000. " +
                "Use the Career Stats page for a longer-term view.\n\n" +
                "Q. Are ratings comparable across leagues?\n" +
                "A. No — only against players within your league, because everyone started at 1000 together."));

            return root;
        }

        // ═══════════════════════════════════════════════════════════
        //  MANUAL (in-app user guide)
        // ═══════════════════════════════════════════════════════════

        private View CreateManualPanel()
        {
            var root = new VerticalStackLayout { Spacing = 0 };

            root.Children.Add(SectionHeader(Emojis.Note, "User Manual",
                "How to use the Wellington District Pool League app — tap any heading to expand it"));

            // Track every section we add so the expand/collapse-all buttons can drive them.
            var sections = new List<ManualSectionView>();

            View Add(string icon, string title, string body, bool startExpanded = false)
            {
                var s = ManualSection(icon, title, body, startExpanded);
                sections.Add(s);
                return s;
            }

            // ---- Expand / Collapse all controls ----
            var expandAllBtn = new Button
            {
                Text = $"{Emojis.Down}  Expand all",
                FontSize = 12,
                Padding = new Thickness(10, 4),
                HeightRequest = 32
            };
            expandAllBtn.SetDynamicResource(Button.StyleProperty, "SecondaryButtonStyle");
            expandAllBtn.Clicked += (_, _) => { foreach (var s in sections) s.SetExpanded(true); };

            var collapseAllBtn = new Button
            {
                Text = $"{Emojis.Up}  Collapse all",
                FontSize = 12,
                Padding = new Thickness(10, 4),
                HeightRequest = 32
            };
            collapseAllBtn.SetDynamicResource(Button.StyleProperty, "SecondaryButtonStyle");
            collapseAllBtn.Clicked += (_, _) => { foreach (var s in sections) s.SetExpanded(false); };

            root.Children.Add(new HorizontalStackLayout
            {
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 8),
                Children = { expandAllBtn, collapseAllBtn }
            });

            // ---- Welcome (always expanded) ----
            root.Children.Add(InfoPanel("Welcome",
                "WDPL2 is the management app for the Wellington District Pool League. It tracks seasons, " +
                "divisions, teams, players, venues, fixtures, results, ratings, cup competitions and even " +
                "generates the league website.\n\n" +
                "Navigation: use the Shell flyout / tab bar on the left (or top on phones) to move between " +
                "areas. Most pages let you add, edit and delete inline; changes are saved automatically to " +
                "the SQLite database in your app data folder, with a JSON backup for settings.\n\n" +
                "This page is the in-app reference. Tap a heading below to expand its detail."));

            // ---- Getting Started (expanded by default) ----
            root.Children.Add(Add(Emojis.Rocket, "Getting Started — first-time setup",
                "Follow this order the first time you set up a league. Each step links to the page of the same name in the side menu.\n\n" +
                $"1. {Emojis.Season} Seasons → New Season. Enter a name (e.g. \u201C2025 Winter\u201D), the start " +
                "and end dates and the default match day/time. Mark it Active so it shows up everywhere.\n\n" +
                $"2. {Emojis.Building} Venues. Add every pub or club that hosts matches. For each one, " +
                "set the number of tables — the scheduler uses this to avoid double-booking a venue on the same night.\n\n" +
                $"3. {Emojis.Division} Divisions. Create the divisions for the season (Premier, Division 1, etc.). " +
                "You can drag teams between divisions later, or use the Division Draw animation to randomise.\n\n" +
                $"4. {Emojis.Team} Teams. Add each team, assign it to a division and choose its home venue and home night. " +
                "Set a captain (any player on that team) so reminder notifications go to the right person.\n\n" +
                $"5. {Emojis.Player} Players. Add the squad for each team. Players can be moved between teams during " +
                "the season — the Transfer History keeps an audit trail.\n\n" +
                $"6. {Emojis.Fixture} Fixtures → Generate. The scheduler creates a balanced round-robin (or " +
                "multi-round) fixture list using your Fixture Defaults and venue availability. Review it, " +
                "resolve any clashes the validator flags, and publish.\n\n" +
                $"7. {Emojis.Chart} Enter results week by week. League Tables, ratings and achievements update automatically.",
                startExpanded: true));

            // ---- Seasons ----
            root.Children.Add(Add(Emojis.Season, "Seasons",
                "Seasons are the top-level container for everything else. Each division, team, player, fixture and competition belongs to exactly one season.\n\n" +
                $"{Emojis.Bullet} New Season: name, start/end dates, default match day, default match time, " +
                "frames per match, rounds per opponent.\n" +
                $"{Emojis.Bullet} Active Season: only one season is \u201Ccurrent\u201D at a time. Most pages " +
                "filter to it automatically. Switch the active season from Seasons → Set Active.\n" +
                $"{Emojis.Bullet} Season Setup: a guided wizard for blackout dates (public holidays, school " +
                "holidays, etc.), preset holidays from the country pack, and frame counts.\n" +
                $"{Emojis.Bullet} Season Comparison: pick two seasons and compare standings, top scorers, " +
                "win percentages and rating distributions side-by-side.\n" +
                $"{Emojis.Bullet} Season Copy: clone divisions, teams and players from a previous season into a " +
                "new one so you don\u2019t have to re-enter everything every year.\n" +
                $"{Emojis.Bullet} Settings Scope: most settings panels show a Settings Scope selector at the top. " +
                "Choose \u201CGlobal Defaults\u201D to edit values used by every season, or pick a specific " +
                "season and toggle \u201CUse custom settings for this season\u201D to override."));

            // ---- Divisions ----
            root.Children.Add(Add(Emojis.Division, "Divisions",
                "Divisions group teams of similar standard within a season.\n\n" +
                $"{Emojis.Bullet} Add a division with a name (e.g. \u201CPremier\u201D), an optional colour and " +
                "sort order — the sort order controls the display order on the dashboard and league tables.\n" +
                $"{Emojis.Bullet} Group Stage Settings: configure how groups are drawn and seeded for divisions " +
                "that use a group stage.\n" +
                $"{Emojis.Bullet} Division Draw / Group Draw animations: a randomiser with on-screen animation " +
                "— useful for AGM nights when you want to do the draw in front of the captains.\n" +
                $"{Emojis.Bullet} Promotion / Relegation: rules for moving teams between divisions at season " +
                "end are configured under Settings → Division Management."));

            // ---- Teams ----
            root.Children.Add(Add(Emojis.Team, "Teams",
                $"{Emojis.Bullet} Each team has a name, division, home venue, home night, captain and squad of players.\n" +
                $"{Emojis.Bullet} Captain: drives the captain-only website (entry forms, availability) and is the " +
                "recipient of weekly notifications.\n" +
                $"{Emojis.Bullet} Home venue: the scheduler will book home fixtures here on the team\u2019s home night.\n" +
                $"{Emojis.Bullet} Players list: drag players between teams; transfers are recorded in the player\u2019s " +
                "Transfer History.\n" +
                $"{Emojis.Bullet} Team Analytics: per-team page showing form, rating progression, top performers " +
                "and head-to-head records."));

            // ---- Players ----
            root.Children.Add(Add(Emojis.Player, "Players",
                "Players are the heart of the data — every frame they play feeds into ratings and stats.\n\n" +
                $"{Emojis.Bullet} Add Player: first name, last name, date of birth (optional), team, contact details.\n" +
                $"{Emojis.Bullet} Player Profile: tap a player to see profile, current rating, total frames, win " +
                "percentage and a timeline of every fixture they\u2019ve played.\n" +
                $"{Emojis.Bullet} Career Stats: combined stats across every season they\u2019ve appeared in, " +
                "including imported historical data.\n" +
                $"{Emojis.Bullet} Frame Stats: break-by-break detail — 8-ball wins, deciding-frame record, " +
                "longest winning streak, etc.\n" +
                $"{Emojis.Bullet} Player Results: chronological list of every result with opponent and venue.\n" +
                $"{Emojis.Bullet} Availability: mark dates a player is unavailable so the scheduler / captain " +
                "knows in advance.\n" +
                $"{Emojis.Bullet} Transfers: when you move a player to another team, the move and date are " +
                "recorded so historical fixtures still show the correct team."));

            // ---- Venues ----
            root.Children.Add(Add(Emojis.Building, "Venues",
                $"{Emojis.Bullet} Each venue has a name, address, optional contact details and one or more tables. " +
                "Tables are stored as JSON on the venue and have their own labels (e.g. \u201CFront\u201D, \u201CBack\u201D).\n" +
                $"{Emojis.Bullet} Number of tables limits how many home fixtures can be played at the venue on the " +
                "same night — useful when one club hosts several teams.\n" +
                $"{Emojis.Bullet} The Venues page also shows which teams call this venue home.\n" +
                $"{Emojis.Bullet} VenueAssign tab on the Competitions page lets you assign venues to knockout / " +
                "round-robin matches in cup competitions."));

            // ---- Fixtures ----
            root.Children.Add(Add(Emojis.Fixture, "Fixtures, Results & Match Day",
                "Fixtures are league matches between two teams on a given date.\n\n" +
                $"{Emojis.Bullet} Generate Fixtures: from the Fixtures page, click Generate. The Season Scheduler " +
                "produces a balanced round-robin using your Fixture Defaults (match day, time, rounds per " +
                "opponent, frames per match) and respects blackout dates.\n" +
                $"{Emojis.Bullet} Fixture Validator: warns about clashes — same team booked twice, venue capacity " +
                "exceeded, fixture on a blackout date, etc. Use the clash resolver to swap dates automatically.\n" +
                $"{Emojis.Bullet} Enter Results: tap a fixture to open the result editor. Enter each frame, mark " +
                "the winner, optionally flag 8-ball wins. Team score and player ratings update instantly.\n" +
                $"{Emojis.Bullet} Match Day Dashboard: a single-screen overview of every fixture being played " +
                "tonight — ideal to leave running on a tablet at the venue. Auto-refreshes as results come in.\n" +
                $"{Emojis.Bullet} Calendar: month view of every fixture, blackout date and event. Tap a day to " +
                "see what\u2019s on. Use Calendar Options to change the first day of the week and add personal events.\n" +
                $"{Emojis.Bullet} Snapshots: the Schedule Snapshot service saves the schedule before any " +
                "regeneration so you can roll back if needed."));

            // ---- Competitions ----
            root.Children.Add(Add(Emojis.Competition, "Competitions — Cups, Knockouts, Singles & Doubles",
                "Competitions are tournaments that sit alongside the league — cups, plate competitions, " +
                "singles and doubles championships.\n\n" +
                $"{Emojis.Bullet} Competition Wizard: walks you through creating a competition. Choose format " +
                "(knockout, double-elimination, round-robin, group stage + knockout, singles, doubles), entry " +
                "type (teams or players) and seeding rules.\n" +
                $"{Emojis.Bullet} Participants tab: add or remove entries. The system can auto-seed by current rating.\n" +
                $"{Emojis.Bullet} Bracket / Groups tabs: enter results and the next round is filled in for you. " +
                "The bracket renders a printable single-elimination chart.\n" +
                $"{Emojis.Bullet} VenueAssign tab: assign each match to a venue and time so participants know where to play.\n" +
                $"{Emojis.Bullet} Editor tab: rename the competition, change format mid-stream (with a warning), " +
                "or add custom rounds.\n" +
                $"{Emojis.Bullet} Doubles: pairings are stored as DoublesPairings on a DoublesTeam, so the same " +
                "player can partner different people across seasons."));

            // ---- League Tables & ratings ----
            root.Children.Add(Add(Emojis.Chart, "League Tables & Player Ratings",
                "League standings and player ratings are computed live from results.\n\n" +
                $"{Emojis.Bullet} League Tables: choose a division to see the standings. Columns: Played, Won, " +
                "Drawn, Lost, Frames For, Frames Against, Frame Difference, Points.\n" +
                $"{Emojis.Bullet} Points: Frames Won + Match Win Bonus on a win, + Match Draw Bonus on a draw, " +
                "none on a loss. Configure the bonuses under Settings → Match Scoring.\n" +
                $"{Emojis.Bullet} Tiebreakers: when teams are level on points, the Tiebreaker Order from Match " +
                "Scoring decides the order — Frame Difference, Frames For, Head-to-Head, Matches Won. Drag the " +
                "order to reflect your league\u2019s rules.\n" +
                $"{Emojis.Bullet} Player Ratings: VBA-style cumulative weighted formula — " +
                "Rating = Σ(OpponentRating × Factor × Weight) / ΣWeight. Tune Starting Rating, Weighting, Bias, " +
                "Win/Loss/8-ball factors and Min Frames % under Settings → Player Ratings.\n" +
                $"{Emojis.Bullet} Recalculate All Ratings: imported VBA/SQL data may contain pre-baked ratings. " +
                "Use this button to wipe and recompute everything from frame data using current settings."));

            // ---- Achievements ----
            root.Children.Add(Add(Emojis.Trophy, "Achievements & Season Awards",
                $"{Emojis.Bullet} Achievements unlock automatically as players reach milestones — first 8-ball, " +
                "100 frames played, 10-game winning streak, etc.\n" +
                $"{Emojis.Bullet} Each achievement shows when it was earned and against whom.\n" +
                $"{Emojis.Bullet} Season Awards page collates end-of-season honours — Player of the Year, Most " +
                "Improved, Highest Win %, Most 8-balls, etc.\n" +
                $"{Emojis.Bullet} Award winners feed into the Website Builder\u2019s History page."));

            // ---- Analytics ----
            root.Children.Add(Add(Emojis.Chart, "Analytics",
                $"{Emojis.Bullet} Analytics Hub: entry point to all charts and reports.\n" +
                $"{Emojis.Bullet} Team Analytics: per-team form line, rating curve, top frame-winners, " +
                "home-vs-away record, head-to-head matrix.\n" +
                $"{Emojis.Bullet} What-If Simulator: pick remaining fixtures and assign hypothetical scores to see " +
                "how the league table would look. Useful for run-in planning.\n" +
                $"{Emojis.Bullet} Career Stats: long-term player view across every season they\u2019ve appeared in."));

            // ---- Import ----
            root.Children.Add(Add(Emojis.Import, "Importing Data",
                "WDPL2 can import legacy data so you don\u2019t have to type history in by hand.\n\n" +
                $"{Emojis.Bullet} Smart Import: drag a folder or single file in and the app sniffs the format — " +
                "CSV, HTML, Word (.docx), Excel (.xlsx), Access (.mdb/.accdb), SQL dumps and Paradox (.db) tables " +
                "are all supported.\n" +
                $"{Emojis.Bullet} Import Preview: every importer produces a preview screen first. You can deselect " +
                "individual rows (e.g. a player you don\u2019t want to import) before confirming.\n" +
                $"{Emojis.Bullet} Batch HTML Import: point at a folder of historical league HTML pages and the " +
                "discovery service indexes them all into one preview.\n" +
                $"{Emojis.Bullet} Paradox Pipeline: dedicated importers for Paradox players, teams, divisions, " +
                "venues, matches, singles, doubles — orchestrated so foreign keys are resolved correctly.\n" +
                $"{Emojis.Bullet} Honours Excel Importer: bulk-import end-of-season award winners.\n" +
                $"{Emojis.Bullet} Score Card Recognition: snap a photo of a paper score card and the OCR service " +
                "(Plugin.Maui.OCR + Azure Vision fallback) extracts the frame results.\n" +
                $"{Emojis.Bullet} Always take a backup from Data Management before a large import."));

            // ---- Website Builder ----
            root.Children.Add(Add(Emojis.Building, "Website Builder",
                "The Website Builder turns your league data into a public static website ready to host anywhere.\n\n" +
                $"{Emojis.Bullet} Hub: pick which pages to publish (Home, Fixtures, Results, Standings, Players, " +
                "Divisions, Competitions, Rules, History, Gallery, Contact, Entry Forms, Captains-only area).\n" +
                $"{Emojis.Bullet} Branding & Colours: set the league name, tagline, primary/accent colours and " +
                "upload a logo (or design one in the Logo Designer).\n" +
                $"{Emojis.Bullet} Layout: drag-and-drop the order of sections on each page, choose between " +
                "single-column / sidebar / hero layouts.\n" +
                $"{Emojis.Bullet} Logo Designer: SkiaSharp-powered canvas with shape and icon catalogues. Save " +
                "a design recipe so you can re-render at any size.\n" +
                $"{Emojis.Bullet} Fixtures Sheet: printable PDF-style fixture sheet for handing out at the AGM.\n" +
                $"{Emojis.Bullet} Generate: builds a static folder of HTML, CSS, JSON data and images.\n" +
                $"{Emojis.Bullet} Deploy: upload via FTP (FtpUploadService) or push to GitHub Pages " +
                "(GitHubPagesService) directly from the Deployment Settings page.\n" +
                $"{Emojis.Bullet} SEO & Social Card: configure meta tags, Open Graph image and a generated " +
                "social-share card for the league."));

            // ---- Notifications ----
            root.Children.Add(Add(Emojis.Bell, "Notifications",
                $"{Emojis.Bullet} On first use, open Settings → Notifications and tap Request Notification " +
                "Permissions. The app uses Plugin.LocalNotification under the hood.\n" +
                $"{Emojis.Bullet} Match Reminders: scheduled per fixture for the captain. Choose how many hours " +
                "before (1, 2, 4, 6, 12 or 24).\n" +
                $"{Emojis.Bullet} Result Notifications: instant alert when a result is posted to a fixture you\u2019re part of.\n" +
                $"{Emojis.Bullet} Weekly Fixture List: Monday morning summary of the week\u2019s fixtures.\n" +
                $"{Emojis.Bullet} Test Notification button sends a sample so you can confirm permissions are working.\n" +
                $"{Emojis.Bullet} Cancel All Notifications wipes scheduled reminders — useful after regenerating fixtures."));

            // ---- Games ----
            root.Children.Add(Add(Emojis.EightBall, "Games Library",
                "A bonus collection of mini-games — mostly built for fun and to show off MAUI graphics.\n\n" +
                $"{Emojis.Bullet} Pool: full 8-ball physics simulator (PoolPhysicsModule, PoolRenderingModule, " +
                "PoolAiModule). Configure AI difficulty, audio, visual effects and physics quality from in-game settings.\n" +
                $"{Emojis.Bullet} Replay: every shot is recorded by PoolReplayModule so you can rewind and watch.\n" +
                $"{Emojis.Bullet} Snake, Memory, Breakout: classic time-killers.\n" +
                $"{Emojis.Bullet} RetroFps: experimental SkiaSharp first-person prototype."));

            // ---- Search ----
            root.Children.Add(Add(Emojis.Target, "Search",
                "The global Search page (⌘/Ctrl+F equivalent) jumps straight to any record.\n\n" +
                $"{Emojis.Bullet} Searches across players, teams, venues, divisions, fixtures and competitions.\n" +
                $"{Emojis.Bullet} Type any part of a name; results group by type with the most relevant hit at the top.\n" +
                $"{Emojis.Bullet} Tap a result to navigate to its detail page."));

            // ---- Settings overview ----
            root.Children.Add(Add(Emojis.Settings, "Settings (this page) — every panel explained",
                $"{Emojis.Bullet} Appearance — light / dark / follow system. Theme is applied immediately to all " +
                "open pages including the Pool game.\n" +
                $"{Emojis.Bullet} Player Ratings — Starting rating, weighting, bias, win/loss factor, 8-ball factor, " +
                "min frames %. Recalculate All Ratings re-runs the formula across every historical frame.\n" +
                $"{Emojis.Bullet} Match Scoring — win bonus, draw bonus, and the drag-and-drop Tiebreaker Order " +
                "used by League Tables.\n" +
                $"{Emojis.Bullet} Fixture Defaults — default match day, default match time, frames per match, rounds " +
                "per opponent. Used by the Season Scheduler when generating new fixtures.\n" +
                $"{Emojis.Bullet} Notifications — enable/disable each notification type, set reminder lead time, " +
                "send a test notification, cancel all pending.\n" +
                $"{Emojis.Bullet} Division Management — promotion / relegation rules between seasons.\n" +
                $"{Emojis.Bullet} Data Management — backup, restore, export (Local / SQL / JSON), import, clear all " +
                "data, and integrity validation.\n" +
                $"{Emojis.Bullet} Manual — you are here.\n" +
                $"{Emojis.Bullet} About — version, technology stack and credits."));

            // ---- Data Management deep-dive ----
            root.Children.Add(Add(Emojis.Database, "Backups, Export & Data Safety",
                $"{Emojis.Bullet} Backup: the BackupService writes a timestamped copy of the SQLite database and " +
                "settings JSON to your app data folder. Take one before any large change (import, recalc, season copy, clear).\n" +
                $"{Emojis.Bullet} Restore: pick a backup from the list to replace the current database. The app " +
                "restarts so EF Core can re-open the file.\n" +
                $"{Emojis.Bullet} Export: ExportService produces a portable archive; LocalExportService writes to " +
                "the local file system; SqlExportService dumps SQL for use in other tools.\n" +
                $"{Emojis.Bullet} Cloud Sync: optional CloudSyncService can push backups to a configured location.\n" +
                $"{Emojis.Bullet} Data Integrity: the DataIntegrityValidator checks for orphaned records, missing " +
                "references and inconsistent ratings, and reports issues you can fix with one click."));

            // ---- Tips ----
            root.Children.Add(Add(Emojis.Sparkles, "Tips & Tricks",
                $"{Emojis.Bullet} Most settings panels show a Settings Scope selector — keep global defaults but " +
                "override values for one season only.\n" +
                $"{Emojis.Bullet} Pull down on any list to refresh.\n" +
                $"{Emojis.Bullet} The dashboard cards are tap-targets — they jump to the matching detail page.\n" +
                $"{Emojis.Bullet} On Windows the app respects the system accent colour for selected list items.\n" +
                $"{Emojis.Bullet} If a page looks wrong after changing the theme, navigate away and back — " +
                "theme-aware colours rebuild on next render.\n" +
                $"{Emojis.Bullet} Keep the database tidy: delete test seasons before importing real history, " +
                "and run the Data Integrity Validator after every major import."));

            // ---- Troubleshooting ----
            root.Children.Add(Add(Emojis.Wrench, "Troubleshooting",
                $"{Emojis.Bullet} App won\u2019t start / crashes on launch: the database may be from an older " +
                "schema. Delete league.db from the app data folder — the app rebuilds it on next launch and runs " +
                "DataMigrationService.\n" +
                $"{Emojis.Bullet} Notifications not firing: re-open Settings → Notifications and tap Request " +
                "Permissions. On Android 13+ check the system app-info screen too.\n" +
                $"{Emojis.Bullet} Ratings look wrong after import: tap Recalculate All Ratings under Settings → " +
                "Player Ratings. Imported VBA values are wiped and recomputed from frames.\n" +
                $"{Emojis.Bullet} Website Builder deploy fails: check FTP credentials / GitHub token under " +
                "Deployment Settings, and that the generated output folder isn\u2019t open elsewhere.\n" +
                $"{Emojis.Bullet} Fixture clashes after generation: open the Fixture Validator on the Fixtures page " +
                "and use the suggested swaps to resolve them."));

            return root;
        }

        /// <summary>
        /// Helper that builds one collapsible manual section: a tappable header that toggles
        /// a body card. Used so the manual stays tidy and users can drill in only where needed.
        /// </summary>
        private ManualSectionView ManualSection(string icon, string title, string body, bool startExpanded = false)
        {
            return new ManualSectionView(icon, title, body, startExpanded);
        }

        /// <summary>
        /// View used by the user manual: a tappable header (with chevron + icon + title) that
        /// expands or collapses an info card containing the section body text.
        /// </summary>
        private sealed class ManualSectionView : VerticalStackLayout
        {
            private readonly Label _chevron;
            private readonly Border _bodyContainer;
            private bool _expanded;

            public ManualSectionView(string icon, string title, string body, bool startExpanded)
            {
                Spacing = 6;
                Margin = new Thickness(0, 8, 0, 0);

                _chevron = new Label
                {
                    Text = startExpanded ? "\u25BC" : "\u25B6",
                    FontSize = 12,
                    TextColor = SubtleText,
                    VerticalTextAlignment = TextAlignment.Center,
                    WidthRequest = 16
                };

                var titleLabel = new Label
                {
                    Text = $"{icon}  {title}",
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TitleText,
                    VerticalTextAlignment = TextAlignment.Center
                };

                var headerStack = new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children = { _chevron, titleLabel }
                };

                var headerBorder = new Border
                {
                    Padding = new Thickness(10, 8),
                    BackgroundColor = FieldBg,
                    Stroke = CardStroke,
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Content = headerStack
                };

                var tap = new TapGestureRecognizer();
                tap.Tapped += (_, _) => SetExpanded(!_expanded);
                headerBorder.GestureRecognizers.Add(tap);

                var bodyLabel = new Label
                {
                    Text = body,
                    FontSize = 13,
                    LineHeight = 1.4,
                    TextColor = BodyText
                };

                _bodyContainer = Card(bodyLabel);
                _bodyContainer.IsVisible = startExpanded;
                _expanded = startExpanded;

                Children.Add(headerBorder);
                Children.Add(_bodyContainer);
            }

            public void SetExpanded(bool expanded)
            {
                if (_expanded == expanded) return;
                _expanded = expanded;
                _bodyContainer.IsVisible = expanded;
                _chevron.Text = expanded ? "\u25BC" : "\u25B6";
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  ABOUT
        // ═══════════════════════════════════════════════════════════

        private View CreateAboutPanel()
        {
            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

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
                TextColor = SubtleText,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };

            var hintLabel = new Label
            {
                Text = $"{Emojis.Sparkles} Tip: Some secrets are hidden in plain sight...",
                FontSize = 11,
                TextColor = SubtleText,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 24, 0, 0),
                FontAttributes = FontAttributes.Italic
            };

            var root = new VerticalStackLayout { Spacing = 0 };

            root.Children.Add(versionLabel);
            root.Children.Add(subtitleLabel);

            root.Children.Add(SectionHeader(Emojis.Info, "About This App", "Information about league settings and data management"));

            root.Children.Add(InfoPanel("How Settings Work",
                "• Settings are saved with your league data\n" +
                "• Player ratings use VBA-style cumulative weighted calculation\n" +
                "• Rating changes based on opponent strength at time of match\n" +
                "• Changes to rating settings require refreshing the Tables page\n" +
                "• Fixture defaults only apply to newly generated fixtures\n" +
                "• Use 'Reset to Defaults' to restore original values"));

            root.Children.Add(SuccessBanner(
                ".NET 9 MAUI (Multi-platform App UI) · C# 13 · Cross-platform: Windows, macOS, iOS, Android · JSON local storage · VBA-compatible rating algorithm"));

            root.Children.Add(hintLabel);
            root.Children.Add(_statusLabel);

            return root;
        }

        // ═══════════════════════════════════════════════════════════
        //  SAVE / RESET / RECALCULATE
        // ═══════════════════════════════════════════════════════════

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
