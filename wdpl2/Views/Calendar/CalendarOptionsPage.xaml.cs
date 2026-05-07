using System.Collections.ObjectModel;
using Microsoft.Maui.Controls.Shapes;
using Wdpl2.Helpers;
using Wdpl2.Models;
using Wdpl2.Services;
using static Wdpl2.Helpers.PanelBuilder;

namespace Wdpl2.Views;

public partial class CalendarOptionsPage : ContentPage
{
    private readonly IDataStore _dataStore;
    private LeagueData League => _dataStore.GetData();
    private CalendarSettings Settings => League.CalendarSettings;

    // Color swatch ↔ setting pairs
    private readonly record struct ColorBinding(BoxView Swatch, string SettingName);
    private readonly List<ColorBinding> _colorBindings = [];

    // Pre-defined palette for the colour picker
    private static readonly string[] _palette =
    [
        "#EF4444", "#F97316", "#F59E0B", "#EAB308", "#84CC16",
        "#22C55E", "#10B981", "#14B8A6", "#06B6D4", "#0EA5E9",
        "#3B82F6", "#6366F1", "#8B5CF6", "#A855F7", "#D946EF",
        "#EC4899", "#F43F5E", "#78716C", "#6B7280", "#64748B",
    ];

    private readonly ObservableCollection<string> _categories = new()
    {
        "General",
        "Default Filters",
        "Preset Events",
        "Colours",
        "Month View",
        "Year / Wall Planner",
        "Day View",
        "Events"
    };

    public CalendarOptionsPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
        InitializeComponent();
        CategoriesList.ItemsSource = _categories;
        CategoriesList.SelectedItem = _categories.First();
    }

    // ────────────────────── Navigation ──────────────────────

    private void OnCategorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        FlushPendingSave();
        var selected = e.CurrentSelection?.FirstOrDefault() as string;
        ShowCategory(selected);
    }

    private void ShowCategory(string? category)
    {
        View? content = category switch
        {
            "General" => CreateGeneralPanel(),
            "Default Filters" => CreateDefaultFiltersPanel(),
            "Preset Events" => CreatePresetEventsPanel(),
            "Colours" => CreateColoursPanel(),
            "Month View" => CreateMonthViewPanel(),
            "Year / Wall Planner" => CreateYearViewPanel(),
            "Day View" => CreateDayViewPanel(),
            "Events" => CreateEventsPanel(),
            _ => null
        };
        ContentPanel.Content = content;
    }

    // ═══════════════════════════════════════════════════════════
    //  PANELS
    // ═══════════════════════════════════════════════════════════

    // ────────────────────── General ──────────────────────

    private View CreateGeneralPanel()
    {
        var s = Settings;
        var root = new VerticalStackLayout { Spacing = 0 };

        root.Children.Add(SectionHeader(Emojis.Settings, "General", "Default view, week start, and display preferences"));

        var viewPicker = new Picker { WidthRequest = 160, FontSize = 14 };
        viewPicker.Items.Add("Month");
        viewPicker.Items.Add("Year");
        viewPicker.Items.Add("Day");
        viewPicker.SelectedItem = s.DefaultView;
        viewPicker.SelectedIndexChanged += (_, _) => { s.DefaultView = viewPicker.SelectedItem?.ToString() ?? "Month"; SaveSettings(); };

        var weekPicker = new Picker { WidthRequest = 160, FontSize = 14 };
        weekPicker.Items.Add("Monday");
        weekPicker.Items.Add("Sunday");
        weekPicker.SelectedIndex = s.WeekStartDay == 0 ? 1 : 0;
        weekPicker.SelectedIndexChanged += (_, _) => { s.WeekStartDay = weekPicker.SelectedIndex == 1 ? 0 : 1; SaveSettings(); };

        root.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                SettingRow("Default calendar view", viewPicker, "Which view opens when you go to Calendar"),
                SettingRow("Week starts on", weekPicker),
                SwitchRow("Show colour legend", s.ShowLegend, v => s.ShowLegend = v, "Colour key overlay at the bottom of the calendar"),
            }
        }));

        return root;
    }

    // ────────────────────── Default Filters ──────────────────────

    private View CreateDefaultFiltersPanel()
    {
        var s = Settings;
        var root = new VerticalStackLayout { Spacing = 0 };

        root.Children.Add(SectionHeader("\U0001F50D", "Default Filters", "Filters applied when the calendar first opens"));

        root.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                SwitchRow("League matches", s.ShowLeagueMatches, v => s.ShowLeagueMatches = v),
                SwitchRow("Competitions", s.ShowCompetitions, v => s.ShowCompetitions = v),
                SwitchRow("Bank holidays", s.ShowBankHolidays, v => s.ShowBankHolidays = v),
                SwitchRow("Blackout dates", s.ShowBlackouts, v => s.ShowBlackouts = v),
                SwitchRow("Transfer window", s.ShowTransferWindow, v => s.ShowTransferWindow = v),
                SwitchRow("Custom events", s.ShowCustomEvents, v => s.ShowCustomEvents = v),
            }
        }));

        root.Children.Add(InfoBanner(
            "These settings only affect the initial state. You can still toggle filters on/off from the calendar toolbar at any time."));

        return root;
    }

    // ────────────────────── Colours ──────────────────────

    private View CreateColoursPanel()
    {
        var s = Settings;
        _colorBindings.Clear();
        var root = new VerticalStackLayout { Spacing = 0 };

        root.Children.Add(SectionHeader("\U0001F3A8", "Colours", "Tap any swatch to pick a new colour"));

        var colors = new (string Label, string SettingName)[]
        {
            ("Fixture", nameof(CalendarSettings.FixtureColor)),
            ("Result", nameof(CalendarSettings.ResultColor)),
            ("Blackout", nameof(CalendarSettings.BlackoutColor)),
            ("Season", nameof(CalendarSettings.SeasonColor)),
            ("Today highlight", nameof(CalendarSettings.TodayColor)),
            ("Competition", nameof(CalendarSettings.CompetitionColor)),
            ("Bank holiday", nameof(CalendarSettings.BankHolidayColor)),
            ("Transfer window", nameof(CalendarSettings.TransferWindowColor)),
            ("Custom event", nameof(CalendarSettings.CustomEventColor)),
        };

        var rows = new VerticalStackLayout { Spacing = 0 };
        foreach (var (label, settingName) in colors)
        {
            var hex = (string?)typeof(CalendarSettings).GetProperty(settingName)?.GetValue(s) ?? "#888888";

            var swatch = new Border
            {
                WidthRequest = 36,
                HeightRequest = 36,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                StrokeThickness = 2,
                Stroke = CardStroke,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                Content = new BoxView { CornerRadius = 6 }
            };
            var box = (BoxView)swatch.Content;
            TrySetSwatchColor(box, hex);

            var hexLabel = new Label
            {
                Text = hex.ToUpperInvariant(),
                FontSize = 11,
                TextColor = SubtleText,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalTextAlignment = TextAlignment.Center,
                WidthRequest = 70
            };

            var controlStack = new HorizontalStackLayout
            {
                Spacing = 10,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                Children = { hexLabel, swatch }
            };

            var localSettingName = settingName;
            var localBox = box;
            var localHexLabel = hexLabel;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await ShowColourPicker(localBox, localHexLabel, localSettingName);
            swatch.GestureRecognizers.Add(tap);

            _colorBindings.Add(new ColorBinding(box, settingName));

            rows.Children.Add(SettingRow(label, controlStack));
        }

        root.Children.Add(Card(rows));
        return root;
    }

    private async Task ShowColourPicker(BoxView swatch, Label hexLabel, string settingName)
    {
        var currentHex = (string?)typeof(CalendarSettings).GetProperty(settingName)?.GetValue(Settings) ?? "#888888";

        var popup = new ContentPage { BackgroundColor = Color.FromArgb("#80000000") };

        var card = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            StrokeThickness = 1,
            Padding = 24,
            MaximumWidthRequest = 380,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            BackgroundColor = CardBg,
            Stroke = CardStroke
        };

        var titleLabel = new Label { Text = "Choose a colour", FontSize = 18, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, TextColor = TitleText };

        var preview = new Border
        {
            WidthRequest = 48,
            HeightRequest = 48,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            StrokeThickness = 2,
            Stroke = CardStroke,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 8, 0, 4),
            Content = new BoxView { Color = Color.FromArgb(currentHex), CornerRadius = 10 }
        };

        var paletteGrid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 12, 0, 0)
        };

        const int cols = 5;
        int gridRows = (_palette.Length + cols - 1) / cols;
        for (int c = 0; c < cols; c++)
            paletteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = 52 });
        for (int r = 0; r < gridRows; r++)
            paletteGrid.RowDefinitions.Add(new RowDefinition { Height = 52 });

        string? selectedHex = null;

        for (int i = 0; i < _palette.Length; i++)
        {
            var hex = _palette[i];
            var isSelected = hex.Equals(currentHex, StringComparison.OrdinalIgnoreCase);

            var cell = new Border
            {
                BackgroundColor = Color.FromArgb(hex),
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                StrokeThickness = isSelected ? 3 : 1,
                Stroke = isSelected ? TitleText : CardStroke,
                WidthRequest = 46,
                HeightRequest = 46,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };

            if (isSelected)
            {
                cell.Content = new Label
                {
                    Text = Emojis.Check,
                    FontSize = 18,
                    TextColor = Colors.White,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                };
            }

            var localHex = hex;
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (_, _) =>
            {
                selectedHex = localHex;
                await popup.Navigation.PopModalAsync();
            };
            cell.GestureRecognizers.Add(tapGesture);

            paletteGrid.Add(cell, i % cols, i / cols);
        }

        var cancelBtn = new Button
        {
            Text = "Cancel",
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(0, 16, 0, 0)
        };
        cancelBtn.SetDynamicResource(Button.StyleProperty, "SecondaryButtonStyle");
        cancelBtn.Clicked += async (_, _) => await popup.Navigation.PopModalAsync();

        card.Content = new VerticalStackLayout
        {
            Spacing = 0,
            Children = { titleLabel, preview, paletteGrid, cancelBtn }
        };

        popup.Content = new Grid { Children = { card } };

        var tcs = new TaskCompletionSource();
        popup.Disappearing += (_, _) => tcs.TrySetResult();
        await Navigation.PushModalAsync(popup, animated: true);
        await tcs.Task;

        if (selectedHex != null)
        {
            typeof(CalendarSettings).GetProperty(settingName)?.SetValue(Settings, selectedHex);
            TrySetSwatchColor(swatch, selectedHex);
            hexLabel.Text = selectedHex.ToUpperInvariant();
            SaveSettings();
        }
    }

    // ────────────────────── Month View ──────────────────────

    private View CreateMonthViewPanel()
    {
        var s = Settings;
        var root = new VerticalStackLayout { Spacing = 0 };

        root.Children.Add(SectionHeader(Emojis.Calendar, "Month View", "Control cell sizing and label limits for month grid"));

        var (r1, cellH) = NumericRow("Minimum cell height", s.MonthCellMinHeight, "Pixels");
        var (r2, maxFix) = NumericRow("Max fixtures per cell", s.MonthMaxFixturesPerCell);
        var (r3, maxEvt) = NumericRow("Max events per cell", s.MonthMaxEventsPerCell);
        var (r4, dayFont) = NumericRow("Day number font size", s.MonthDayFontSize);
        var (r5, lblFont) = NumericRow("Label font size", s.MonthLabelFontSize);

        root.Children.Add(Card(new VerticalStackLayout { Spacing = 0, Children = { r1, r2, r3, r4, r5 } }));

        void SaveMonth()
        {
            s.MonthCellMinHeight = PanelBuilder.ParseInt(cellH.Text, 80, 40, 200);
            s.MonthMaxFixturesPerCell = PanelBuilder.ParseInt(maxFix.Text, 3, 1, 10);
            s.MonthMaxEventsPerCell = PanelBuilder.ParseInt(maxEvt.Text, 2, 1, 10);
            s.MonthDayFontSize = PanelBuilder.ParseInt(dayFont.Text, 14, 8, 24);
            s.MonthLabelFontSize = PanelBuilder.ParseInt(lblFont.Text, 9, 6, 18);
            SaveSettings();
        }

        cellH.Unfocused += (_, _) => SaveMonth();
        maxFix.Unfocused += (_, _) => SaveMonth();
        maxEvt.Unfocused += (_, _) => SaveMonth();
        dayFont.Unfocused += (_, _) => SaveMonth();
        lblFont.Unfocused += (_, _) => SaveMonth();

        return root;
    }

    // ────────────────────── Year / Wall Planner ──────────────────────

    private View CreateYearViewPanel()
    {
        var s = Settings;
        var root = new VerticalStackLayout { Spacing = 0 };

        root.Children.Add(SectionHeader(Emojis.Chart, "Year / Wall Planner", "Dimensions and indicators for the wall planner grid"));

        var (r1, cellW) = NumericRow("Day cell width", s.YearCellWidth, "Pixels");
        var (r2, rowH) = NumericRow("Month row height", s.YearRowHeight, "Pixels");
        var (r3, monthW) = NumericRow("Month label width", s.YearMonthLabelWidth, "Pixels");

        root.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                r1, r2, r3,
                SwitchRow("Show dot indicators", s.YearShowDots, v => s.YearShowDots = v, "Coloured dots on cells with events"),
                SwitchRow("Show fixture count", s.YearShowFixtureCount, v => s.YearShowFixtureCount = v, "Number badge on cells"),
            }
        }));

        void SaveYear()
        {
            s.YearCellWidth = PanelBuilder.ParseInt(cellW.Text, 42, 24, 80);
            s.YearRowHeight = PanelBuilder.ParseInt(rowH.Text, 38, 24, 80);
            s.YearMonthLabelWidth = PanelBuilder.ParseInt(monthW.Text, 80, 40, 160);
            SaveSettings();
        }

        cellW.Unfocused += (_, _) => SaveYear();
        rowH.Unfocused += (_, _) => SaveYear();
        monthW.Unfocused += (_, _) => SaveYear();

        return root;
    }

    // ────────────────────── Day View ──────────────────────

    private View CreateDayViewPanel()
    {
        var s = Settings;
        var root = new VerticalStackLayout { Spacing = 0 };

        root.Children.Add(SectionHeader("\U0001F4CB", "Day View", "Control which details appear on fixture and event cards"));

        root.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                SwitchRow("Show venue", s.DayShowVenue, v => s.DayShowVenue = v, "Display venue name on fixture cards"),
                SwitchRow("Show frame count", s.DayShowFrameCount, v => s.DayShowFrameCount = v, "Frame tally for played matches"),
                SwitchRow("Show division", s.DayShowDivision, v => s.DayShowDivision = v, "Division badge on cards"),
                SwitchRow("Show 'Add Event' button", s.DayShowAddEventButton, v => s.DayShowAddEventButton = v, "Quick-add button in day view header"),
            }
        }));

        return root;
    }

    // ────────────────────── Preset Events ──────────────────────

    private View CreatePresetEventsPanel()
    {
        var presets = Settings.PresetHolidays;
        if (presets.Count == 0)
        {
            presets.AddRange(PresetHoliday.CreateDefaults());
            SaveSettings();
        }

        var builtIn = presets.Where(h => h.IsBuiltIn).ToList();
        var custom = presets.Where(h => h.IsCustom).ToList();
        var root = new VerticalStackLayout { Spacing = 0 };

        root.Children.Add(SectionHeader("\ud83c\udfe6", "Preset Events",
            $"{presets.Count(h => h.IsEnabled)} of {presets.Count} enabled"));

        root.Children.Add(InfoBanner(
            "Customise bank holidays and add your own recurring events. " +
            "Disabled holidays won't appear on the calendar."));

        // ── Built-in bank holidays ──
        root.Children.Add(new Label
        {
            Text = "Bank Holidays",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = TitleText,
            Margin = new Thickness(0, 8, 0, 8)
        });

        foreach (var h in builtIn)
            root.Children.Add(CreatePresetHolidayCard(h, isBuiltIn: true));

        // ── Custom holidays ──
        root.Children.Add(new Label
        {
            Text = "Custom Holidays",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = TitleText,
            Margin = new Thickness(0, 16, 0, 8)
        });

        if (custom.Count == 0)
        {
            root.Children.Add(new Label
            {
                Text = "No custom holidays yet — add your own below.",
                FontSize = 12,
                TextColor = SubtleText,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }
        else
        {
            foreach (var h in custom)
                root.Children.Add(CreatePresetHolidayCard(h, isBuiltIn: false));
        }

        // Add custom holiday button
        var addBtn = new Button
        {
            Text = $"{Emojis.Add}  Add Custom Holiday",
            Margin = new Thickness(0, 8, 0, 8)
        };
        addBtn.SetDynamicResource(Button.StyleProperty, "PrimaryButtonStyle");
        addBtn.Clicked += OnAddCustomHolidayClicked;
        root.Children.Add(addBtn);

        // Reset to defaults button
        var resetBtn = new Button
        {
            Text = $"{Emojis.Reload}  Reset to Default Bank Holidays",
            Margin = new Thickness(0, 4, 0, 0)
        };
        resetBtn.SetDynamicResource(Button.StyleProperty, "DangerButtonStyle");
        resetBtn.Clicked += OnResetPresetHolidaysClicked;
        root.Children.Add(resetBtn);

        return root;
    }

    private View CreatePresetHolidayCard(PresetHoliday holiday, bool isBuiltIn)
    {
        var card = new Border
        {
            Padding = 12,
            Margin = new Thickness(0, 0, 0, 6),
            BackgroundColor = CardBg,
            Stroke = CardStroke,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 }
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 44 },
                new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto }
            },
            RowSpacing = 4,
            ColumnSpacing = 10
        };

        // Enable/Disable toggle
        var toggle = new Switch
        {
            IsToggled = holiday.IsEnabled,
            VerticalOptions = LayoutOptions.Center
        };
        toggle.Toggled += (_, _) =>
        {
            holiday.IsEnabled = toggle.IsToggled;
            SaveSettings();
        };
        grid.Add(toggle, 0, 0);

        // Name (editable via Entry for both built-in and custom)
        var nameEntry = new Entry
        {
            Text = holiday.Name,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            Placeholder = "Holiday name"
        };
        nameEntry.SetAppThemeColor(Entry.TextColorProperty, Color.FromArgb("#111827"), Colors.White);
        nameEntry.SetAppThemeColor(Entry.BackgroundColorProperty, Colors.Transparent, Colors.Transparent);
        nameEntry.Unfocused += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(nameEntry.Text))
            {
                holiday.Name = nameEntry.Text.Trim();
                SaveSettings();
            }
        };
        grid.Add(nameEntry, 1, 0);

        // Description / date info
        var description = isBuiltIn
            ? GetBuiltInRuleDescription(holiday.Rule)
            : $"Fixed: {holiday.FixedDay:00}/{holiday.FixedMonth:00} every year";

        var descLabel = new Label
        {
            Text = description,
            FontSize = 11,
            TextColor = SubtleText
        };
        grid.Add(descLabel, 1, 1);

        // For custom holidays: month/day pickers
        if (!isBuiltIn)
        {
            var dateRow = new HorizontalStackLayout { Spacing = 8 };

            var monthPicker = new Picker { WidthRequest = 90, FontSize = 12 };
            string[] months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun",
                               "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
            foreach (var m in months) monthPicker.Items.Add(m);
            monthPicker.SelectedIndex = Math.Clamp(holiday.FixedMonth - 1, 0, 11);
            monthPicker.SelectedIndexChanged += (_, _) =>
            {
                holiday.FixedMonth = monthPicker.SelectedIndex + 1;
                SaveSettings();
                descLabel.Text = $"Fixed: {holiday.FixedDay:00}/{holiday.FixedMonth:00} every year";
            };
            dateRow.Children.Add(monthPicker);

            var dayEntry = new Entry
            {
                Text = holiday.FixedDay.ToString(),
                Keyboard = Keyboard.Numeric,
                WidthRequest = 50,
                FontSize = 12,
                Placeholder = "Day"
            };
            dayEntry.Unfocused += (_, _) =>
            {
                if (int.TryParse(dayEntry.Text, out var d) && d >= 1 && d <= 31)
                {
                    holiday.FixedDay = d;
                    SaveSettings();
                    descLabel.Text = $"Fixed: {holiday.FixedDay:00}/{holiday.FixedMonth:00} every year";
                }
            };
            dateRow.Children.Add(dayEntry);

            grid.Add(dateRow, 1, 2);

            // Delete button for custom holidays
            var deleteBtn = new Button
            {
                Text = Emojis.Delete,
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#EF4444"),
                FontSize = 16,
                WidthRequest = 36,
                HeightRequest = 36,
                CornerRadius = 8,
                Padding = 0,
                VerticalOptions = LayoutOptions.Center
            };
            deleteBtn.Clicked += async (_, _) =>
            {
                bool confirm = await DisplayAlert("Delete Holiday",
                    $"Delete \"{holiday.Name}\"?", "Delete", "Cancel");
                if (!confirm) return;
                Settings.PresetHolidays.Remove(holiday);
                SaveSettings();
                ShowCategory("Preset Events");
            };
            grid.Add(deleteBtn, 2, 0);
            Grid.SetRowSpan(deleteBtn, 3);
        }

        card.Content = grid;
        return card;
    }

    private static string GetBuiltInRuleDescription(string? rule) => rule switch
    {
        "new-year" => "1st January (substituted if weekend)",
        "good-friday" => "Friday before Easter Sunday",
        "easter-monday" => "Monday after Easter Sunday",
        "early-may" => "First Monday of May",
        "spring-bank" => "Last Monday of May",
        "summer-bank" => "Last Monday of August",
        "christmas" => "25th December (substituted if weekend)",
        "boxing-day" => "26th December (substituted if weekend)",
        _ => "Built-in holiday"
    };

    private async void OnAddCustomHolidayClicked(object? sender, EventArgs e)
    {
        var name = await DisplayPromptAsync("Add Custom Holiday",
            "Holiday name:", placeholder: "e.g. League Anniversary", maxLength: 60);
        if (string.IsNullOrWhiteSpace(name)) return;

        var monthStr = await DisplayPromptAsync("Month",
            "Which month? (1–12):", initialValue: "1",
            keyboard: Keyboard.Numeric, maxLength: 2);
        if (!int.TryParse(monthStr, out var month) || month < 1 || month > 12) return;

        var dayStr = await DisplayPromptAsync("Day",
            $"Which day of month {month}? (1–{DateTime.DaysInMonth(DateTime.Today.Year, month)}):",
            initialValue: "1", keyboard: Keyboard.Numeric, maxLength: 2);
        if (!int.TryParse(dayStr, out var day) || day < 1 || day > 31) return;

        Settings.PresetHolidays.Add(new PresetHoliday
        {
            Name = name.Trim(),
            FixedMonth = month,
            FixedDay = day,
            IsCustom = true,
            IsEnabled = true
        });
        SaveSettings();
        ShowCategory("Preset Events");
    }

    private async void OnResetPresetHolidaysClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Reset Preset Holidays",
            "This will reset all bank holidays to defaults and remove any custom holidays. Continue?",
            "Reset", "Cancel");
        if (!confirm) return;

        Settings.PresetHolidays.Clear();
        Settings.PresetHolidays.AddRange(PresetHoliday.CreateDefaults());
        SaveSettings();
        ShowCategory("Preset Events");
    }

    // ────────────────────── Events ──────────────────────

    private View CreateEventsPanel()
    {
        var s = Settings;
        var events = League.CalendarEvents.OrderBy(e => e.Date).ToList();
        var root = new VerticalStackLayout { Spacing = 0 };

        root.Children.Add(SectionHeader(Emojis.MapPin, "Events", $"{events.Count} event{(events.Count != 1 ? "s" : "")} total"));

        var catPicker = new Picker { WidthRequest = 160, FontSize = 14 };
        foreach (var cat in Enum.GetNames<CalendarEventCategory>())
            catPicker.Items.Add(cat);
        catPicker.SelectedItem = s.DefaultEventCategory;
        catPicker.SelectedIndexChanged += (_, _) =>
        {
            s.DefaultEventCategory = catPicker.SelectedItem?.ToString() ?? "General";
            SaveSettings();
        };

        root.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 0,
            Children = { SettingRow("Default category for new events", catPicker) }
        }));

        if (events.Count > 0)
        {
            var deleteAllBtn = new Button
            {
                Text = $"{Emojis.Delete}  Delete All Events ({events.Count})",
                Margin = new Thickness(0, 0, 0, 16)
            };
            deleteAllBtn.SetDynamicResource(Button.StyleProperty, "DangerButtonStyle");
            deleteAllBtn.Clicked += OnDeleteAllEventsClicked;
            root.Children.Add(deleteAllBtn);
        }

        if (events.Count == 0)
        {
            root.Children.Add(EmptyState(Emojis.Calendar, "No events yet", "Use the calendar to add events — tap a date cell or use the options menu."));
        }
        else
        {
            foreach (var evt in events)
                root.Children.Add(CreateEventCard(evt));
        }

        return root;
    }

    private View CreateEventCard(CalendarEvent evt)
    {
        var card = new Border
        {
            Padding = 14,
            Margin = new Thickness(0, 0, 0, 8),
            BackgroundColor = CardBg,
            Stroke = CardStroke,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 10 }
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(),
                new ColumnDefinition { Width = 44 }
            },
            RowSpacing = 8
        };

        var titleEntry = new Entry
        {
            Text = evt.Title,
            Placeholder = "Event title",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold
        };
        titleEntry.SetAppThemeColor(Entry.TextColorProperty, Color.FromArgb("#111827"), Colors.White);
        titleEntry.SetAppThemeColor(Entry.BackgroundColorProperty, Colors.Transparent, Colors.Transparent);
        titleEntry.Unfocused += (_, _) => { evt.Title = titleEntry.Text?.Trim() ?? ""; SaveSettings(); };
        grid.Add(titleEntry, 0, 0);

        var infoRow = new HorizontalStackLayout { Spacing = 12 };

        var datePicker = new DatePicker
        {
            Date = evt.Date,
            FontSize = 13,
            Format = "ddd dd MMM yyyy"
        };
        datePicker.DateSelected += (_, _) => { evt.Date = datePicker.Date; SaveSettings(); };
        infoRow.Children.Add(datePicker);

        var evtCatPicker = new Picker { FontSize = 13, WidthRequest = 120 };
        foreach (var cat in Enum.GetNames<CalendarEventCategory>())
            evtCatPicker.Items.Add(cat);
        evtCatPicker.SelectedItem = evt.Category.ToString();
        evtCatPicker.SelectedIndexChanged += (_, _) =>
        {
            if (Enum.TryParse<CalendarEventCategory>(evtCatPicker.SelectedItem?.ToString(), out var c))
                evt.Category = c;
            SaveSettings();
        };
        infoRow.Children.Add(evtCatPicker);

        grid.Add(infoRow, 0, 1);

        // Competition link picker
        var compRow = new HorizontalStackLayout { Spacing = 8 };
        var compLabel = new Label
        {
            Text = "🏆",
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center
        };
        compRow.Children.Add(compLabel);

        var compPicker = new Picker { FontSize = 13, WidthRequest = 200 };
        var comps = League.Competitions
            .Where(c => c.Status != CompetitionStatus.Draft)
            .OrderByDescending(c => c.StartDate)
            .ToList();
        compPicker.Items.Add("(None)");
        foreach (var comp in comps)
            compPicker.Items.Add(comp.Name);
        compPicker.SelectedItem = evt.CompetitionId.HasValue
            ? comps.FirstOrDefault(c => c.Id == evt.CompetitionId.Value)?.Name ?? "(None)"
            : "(None)";
        compPicker.SelectedIndexChanged += (_, _) =>
        {
            var selected = compPicker.SelectedItem?.ToString();
            if (selected is null or "(None)")
                evt.CompetitionId = null;
            else
            {
                var comp = comps.FirstOrDefault(c => c.Name == selected);
                evt.CompetitionId = comp?.Id;
            }
            SaveSettings();
        };
        compRow.Children.Add(compPicker);
        grid.Add(compRow, 0, 2);

        var notesEntry = new Entry
        {
            Text = evt.Notes ?? "",
            Placeholder = "Notes (optional)",
            FontSize = 13
        };
        notesEntry.SetAppThemeColor(Entry.TextColorProperty, Color.FromArgb("#6B7280"), Color.FromArgb("#9CA3AF"));
        notesEntry.SetAppThemeColor(Entry.BackgroundColorProperty, Colors.Transparent, Colors.Transparent);
        notesEntry.Unfocused += (_, _) =>
        {
            evt.Notes = string.IsNullOrWhiteSpace(notesEntry.Text) ? null : notesEntry.Text.Trim();
            SaveSettings();
        };
        grid.Add(notesEntry, 0, 3);

        var deleteBtn = new Button
        {
            Text = Emojis.Delete,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#EF4444"),
            FontSize = 18,
            WidthRequest = 40,
            HeightRequest = 40,
            CornerRadius = 8,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };
        deleteBtn.Clicked += async (_, _) =>
        {
            bool confirm = await DisplayAlert("Delete Event",
                $"Delete \"{evt.Title}\"?", "Delete", "Cancel");
            if (!confirm) return;
            League.CalendarEvents.Remove(evt);
            SaveSettings();
            ShowCategory("Events");
        };
        grid.Add(deleteBtn, 1, 0);
        Grid.SetRowSpan(deleteBtn, 4);

        card.Content = grid;
        return card;
    }

    // ═══════════════════════════════════════════════════════════
    //  SAVE / HANDLERS
    // ═══════════════════════════════════════════════════════════

    // SaveSettings is intentionally cheap: it just flags the page as dirty.
    // The actual DataStore.Save() happens on category change, page disappear,
    // explicit save/reset, or delete-all — see FlushPendingSave().
    private bool _isDirty;
    private void SaveSettings() => _isDirty = true;

    private void FlushPendingSave()
    {
        if (!_isDirty) return;
        _isDirty = false;
        _ = _dataStore.SaveAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        FlushPendingSave();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        FlushPendingSave();
        await Navigation.PopModalAsync();
    }

    private async void OnResetDefaultsClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Reset Defaults",
            "Reset all calendar options to their default values?", "Reset", "Cancel");
        if (!confirm) return;

        League.CalendarSettings = new CalendarSettings();
        SaveSettings();
        FlushPendingSave();
        ShowCategory(CategoriesList.SelectedItem as string);
    }

    private async void OnDeleteAllEventsClicked(object? sender, EventArgs e)
    {
        var count = League.CalendarEvents.Count;
        if (count == 0) { await DisplayAlert("Events", "No events to delete.", "OK"); return; }

        bool confirm = await DisplayAlert("Delete All Events",
            $"This will permanently delete all {count} calendar event{(count != 1 ? "s" : "")}. Continue?",
            "Delete All", "Cancel");
        if (!confirm) return;

        League.CalendarEvents.Clear();
        SaveSettings();
        FlushPendingSave();
        ShowCategory("Events");
    }

    // ═══════════════════════════════════════════════════════════
    //  COLOUR HELPERS
    // ═══════════════════════════════════════════════════════════

    private static void TrySetSwatchColor(BoxView swatch, string? hex)
    {
        hex = NormalizeHex(hex);
        try { swatch.Color = Color.FromArgb(hex); } catch { }
    }

    private static string NormalizeHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return "#888888";
        hex = hex.Trim();
        if (!hex.StartsWith('#')) hex = "#" + hex;
        return hex.Length == 7 ? hex : "#888888";
    }
}
