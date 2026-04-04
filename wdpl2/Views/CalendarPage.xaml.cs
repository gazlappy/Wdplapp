using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Wdpl2.Models;

namespace Wdpl2.Views;

public partial class CalendarPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private readonly ObservableCollection<Season> _seasons = new();
    private readonly ObservableCollection<Division> _divisions = new();
    private List<Fixture> _seasonFixtures = [];
    private List<Team> _seasonTeams = [];
    private List<Venue> _seasonVenues = [];
    private HashSet<DateTime> _blackoutDates = [];
    private List<(DateTime Date, string Name)> _competitionDates = [];
    private HashSet<DateTime> _competitionDateSet = [];
    private HashSet<DateTime> _bankHolidays = [];
    private List<CalendarEvent> _calendarEvents = [];

    private enum CalendarView { Month, Year, Day }
    private CalendarView _currentView = CalendarView.Month;
    private DateTime _viewDate = DateTime.Today;
    private Guid? _divisionFilter;

    // Filter toggles
    private bool _showLeagueMatches = true;
    private bool _showCompetitions = true;
    private bool _showBankHolidays = true;
    private bool _showBlackouts = true;
    private bool _showTransferWindow = true;
    private bool _showCustomEvents = true;

    // Colors
    private static readonly Color FixtureColor = Color.FromArgb("#3B82F6");
    private static readonly Color ResultColor = Color.FromArgb("#10B981");
    private static readonly Color BlackoutColor = Color.FromArgb("#EF4444");
    private static readonly Color SeasonColor = Color.FromArgb("#8B5CF6");
    private static readonly Color TodayBorder = Color.FromArgb("#F59E0B");
    private static readonly Color CompetitionColor = Color.FromArgb("#F97316");
    private static readonly Color BankHolidayColor = Color.FromArgb("#EC4899");
    private static readonly Color TransferWindowColor = Color.FromArgb("#06B6D4");
    private static readonly Color CustomEventColor = Color.FromArgb("#14B8A6");

    public CalendarPage()
    {
        InitializeComponent();

        SeasonPicker.ItemsSource = _seasons;
        SeasonPicker.ItemDisplayBinding = new Binding("Name");

        DivisionPicker.ItemsSource = _divisions;
        DivisionPicker.ItemDisplayBinding = new Binding("Name");

        BuildDayHeaders();
        LoadSeasons();
    }

    // ────────────────────── Filter Handlers ──────────────────────

    private void OnShowLeagueMatchesChanged(object? sender, CheckedChangedEventArgs e)
    {
        _showLeagueMatches = e.Value;
        Refresh();
    }

    private void OnShowCompetitionsChanged(object? sender, CheckedChangedEventArgs e)
    {
        _showCompetitions = e.Value;
        Refresh();
    }

    private void OnShowBankHolidaysChanged(object? sender, CheckedChangedEventArgs e)
    {
        _showBankHolidays = e.Value;
        Refresh();
    }

    private void OnShowBlackoutsChanged(object? sender, CheckedChangedEventArgs e)
    {
        _showBlackouts = e.Value;
        Refresh();
    }

    private void OnShowTransferWindowChanged(object? sender, CheckedChangedEventArgs e)
    {
        _showTransferWindow = e.Value;
        Refresh();
    }

    private void OnShowCustomEventsChanged(object? sender, CheckedChangedEventArgs e)
    {
        _showCustomEvents = e.Value;
        Refresh();
    }

    // ────────────────────── Add Event ──────────────────────

    private async void OnAddEventClicked(object? sender, EventArgs e)
    {
        var targetDate = _currentView == CalendarView.Day ? _viewDate : DateTime.Today;
        await ShowAddEventDialog(targetDate);
    }

    private async Task ShowAddEventDialog(DateTime defaultDate)
    {
        var title = await DisplayPromptAsync("Add Calendar Event",
            "Event title:", placeholder: "e.g. Committee Meeting", maxLength: 100);
        if (string.IsNullOrWhiteSpace(title)) return;

        var categories = Enum.GetNames<CalendarEventCategory>();
        var category = await DisplayActionSheet("Category", "Cancel", null, categories);
        if (category is null or "Cancel") return;

        var notes = await DisplayPromptAsync("Notes (optional)",
            "Any additional details:", placeholder: "Optional", accept: "Save", cancel: "Skip");

        var evt = new CalendarEvent
        {
            Date = defaultDate.Date,
            Title = title.Trim(),
            Category = Enum.TryParse<CalendarEventCategory>(category, out var cat) ? cat : CalendarEventCategory.General,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        League.CalendarEvents.Add(evt);
        DataStore.Save();
        ReloadCalendarEvents();
        Refresh();
    }

    private async Task ShowEditEventDialog(CalendarEvent evt)
    {
        var action = await DisplayActionSheet($"📌 {evt.Title}", "Cancel", "Delete",
            "Edit Title", "Change Category", "Edit Notes", "Change Date");

        switch (action)
        {
            case "Edit Title":
                var newTitle = await DisplayPromptAsync("Edit Title", "Event title:",
                    initialValue: evt.Title, maxLength: 100);
                if (!string.IsNullOrWhiteSpace(newTitle))
                {
                    evt.Title = newTitle.Trim();
                    DataStore.Save();
                    ReloadCalendarEvents();
                    Refresh();
                }
                break;

            case "Change Category":
                var categories = Enum.GetNames<CalendarEventCategory>();
                var cat = await DisplayActionSheet("Category", "Cancel", null, categories);
                if (cat is not null and not "Cancel" && Enum.TryParse<CalendarEventCategory>(cat, out var parsed))
                {
                    evt.Category = parsed;
                    DataStore.Save();
                    ReloadCalendarEvents();
                    Refresh();
                }
                break;

            case "Edit Notes":
                var newNotes = await DisplayPromptAsync("Edit Notes", "Details:",
                    initialValue: evt.Notes ?? "", accept: "Save", cancel: "Cancel");
                if (newNotes != null)
                {
                    evt.Notes = string.IsNullOrWhiteSpace(newNotes) ? null : newNotes.Trim();
                    DataStore.Save();
                    ReloadCalendarEvents();
                    Refresh();
                }
                break;

            case "Change Date":
                // Use a simple prompt for date since MAUI doesn't have a date picker dialog
                var dateStr = await DisplayPromptAsync("Change Date",
                    "Enter new date (dd/MM/yyyy):",
                    initialValue: evt.Date.ToString("dd/MM/yyyy"), maxLength: 10);
                if (!string.IsNullOrWhiteSpace(dateStr) && DateTime.TryParseExact(dateStr.Trim(),
                    ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd"],
                    null, System.Globalization.DateTimeStyles.None, out var newDate))
                {
                    evt.Date = newDate.Date;
                    DataStore.Save();
                    ReloadCalendarEvents();
                    Refresh();
                }
                break;

            case "Delete":
                bool confirm = await DisplayAlert("Delete Event",
                    $"Delete '{evt.Title}'?", "Delete", "Cancel");
                if (confirm)
                {
                    League.CalendarEvents.Remove(evt);
                    DataStore.Save();
                    ReloadCalendarEvents();
                    Refresh();
                }
                break;
        }
    }

    private void ReloadCalendarEvents()
    {
        _calendarEvents = League.CalendarEvents.OrderBy(e => e.Date).ToList();
    }

    // ────────────────────── Data Loading ──────────────────────

    private void LoadSeasons()
    {
        _seasons.Clear();
        foreach (var s in League.Seasons.OrderByDescending(s => s.StartDate))
            _seasons.Add(s);

        var active = _seasons.FirstOrDefault(s => s.IsActive) ?? _seasons.FirstOrDefault();
        if (active != null)
        {
            SeasonPicker.SelectedItem = active;
            _viewDate = active.StartDate.Date > DateTime.Today ? active.StartDate.Date : DateTime.Today;
        }
    }

    private void OnSeasonChanged(object? sender, EventArgs e)
    {
        if (SeasonPicker.SelectedItem is not Season season) return;

        var id = season.Id;
        _seasonFixtures = League.Fixtures.Where(f => f.SeasonId == id).ToList();
        _seasonTeams = League.Teams.Where(t => t.SeasonId == id).ToList();
        _seasonVenues = League.Venues.Where(v => v.SeasonId == id).ToList();
        _blackoutDates = new HashSet<DateTime>(season.BlackoutDates.Select(d => d.Date));

        // Load competition dates for this season
        _competitionDates = League.Competitions
            .Where(c => c.SeasonId == id)
            .SelectMany(c => c.Rounds
                .Where(r => r.Date.HasValue)
                .Select(r => (Date: r.Date!.Value.Date, Name: $"{c.Name} — {r.Name}")))
            .Concat(League.Competitions
                .Where(c => c.SeasonId == id && c.StartDate.HasValue)
                .Select(c => (Date: c.StartDate!.Value.Date, Name: c.Name)))
            .Distinct()
            .OrderBy(x => x.Date)
            .ToList();
        _competitionDateSet = new HashSet<DateTime>(_competitionDates.Select(c => c.Date));

        // Calculate bank holidays for the season range
        _bankHolidays = GetBankHolidays(season.StartDate.Year, season.EndDate.Year);

        // Load user-created calendar events
        ReloadCalendarEvents();

        _divisions.Clear();
        _divisions.Add(new Division { Name = "All Divisions", Id = Guid.Empty });
        foreach (var d in League.Divisions.Where(d => d.SeasonId == id))
            _divisions.Add(d);
        DivisionPicker.SelectedIndex = 0;
        _divisionFilter = null;

        Refresh();
    }

    private void OnDivisionChanged(object? sender, EventArgs e)
    {
        var div = DivisionPicker.SelectedItem as Division;
        _divisionFilter = div != null && div.Id != Guid.Empty ? div.Id : null;
        Refresh();
    }

    private IEnumerable<Fixture> FilteredFixtures()
    {
        var fixtures = _seasonFixtures.AsEnumerable();
        if (_divisionFilter.HasValue)
            fixtures = fixtures.Where(f => f.DivisionId == _divisionFilter.Value);
        return fixtures;
    }

    // ────────────────────── Navigation ──────────────────────

    private void OnPrevClicked(object? sender, EventArgs e)
    {
        _viewDate = _currentView switch
        {
            CalendarView.Month => _viewDate.AddMonths(-1),
            CalendarView.Year => _viewDate.AddYears(-1),
            CalendarView.Day => _viewDate.AddDays(-1),
            _ => _viewDate
        };
        Refresh();
    }

    private void OnNextClicked(object? sender, EventArgs e)
    {
        _viewDate = _currentView switch
        {
            CalendarView.Month => _viewDate.AddMonths(1),
            CalendarView.Year => _viewDate.AddYears(1),
            CalendarView.Day => _viewDate.AddDays(1),
            _ => _viewDate
        };
        Refresh();
    }

    private void OnTodayClicked(object? sender, EventArgs e)
    {
        _viewDate = DateTime.Today;
        Refresh();
    }

    private void OnMonthViewClicked(object? sender, EventArgs e) => SwitchView(CalendarView.Month);
    private void OnYearViewClicked(object? sender, EventArgs e) => SwitchView(CalendarView.Year);
    private void OnDayViewClicked(object? sender, EventArgs e) => SwitchView(CalendarView.Day);

    private void SwitchView(CalendarView view)
    {
        _currentView = view;
        MonthView.IsVisible = view == CalendarView.Month;
        YearView.IsVisible = view == CalendarView.Year;
        DayView.IsVisible = view == CalendarView.Day;

        SetViewButtonActive(MonthViewBtn, view == CalendarView.Month);
        SetViewButtonActive(YearViewBtn, view == CalendarView.Year);
        SetViewButtonActive(DayViewBtn, view == CalendarView.Day);

        Refresh();
    }

    private static void SetViewButtonActive(Button btn, bool active)
    {
        btn.BackgroundColor = active
            ? Color.FromArgb("#2563EB")
            : Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#374151")
                : Color.FromArgb("#F3F4F6");
        btn.TextColor = active
            ? Colors.White
            : Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#D1D5DB")
                : Color.FromArgb("#374151");
    }

    // ────────────────────── Refresh ──────────────────────

    private void Refresh()
    {
        switch (_currentView)
        {
            case CalendarView.Month:
                RenderMonth();
                break;
            case CalendarView.Year:
                RenderYear();
                break;
            case CalendarView.Day:
                RenderDay();
                break;
        }
    }

    // ────────────────────── MONTH VIEW ──────────────────────

    private void BuildDayHeaders()
    {
        string[] days = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
        for (int i = 0; i < 7; i++)
        {
            var label = new Label
            {
                Text = days[i],
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280")
            };
            Grid.SetColumn(label, i);
            DayHeaders.Children.Add(label);
        }
    }

    private void RenderMonth()
    {
        TitleLabel.Text = _viewDate.ToString("MMMM yyyy");
        MonthGrid.Children.Clear();

        var season = SeasonPicker.SelectedItem as Season;
        var firstOfMonth = new DateTime(_viewDate.Year, _viewDate.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(_viewDate.Year, _viewDate.Month);

        // Monday = 0
        int startCol = ((int)firstOfMonth.DayOfWeek + 6) % 7;

        var fixturesByDate = FilteredFixtures()
            .GroupBy(f => f.Date.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(_viewDate.Year, _viewDate.Month, day);
            int cellIndex = startCol + day - 1;
            int row = cellIndex / 7;
            int col = cellIndex % 7;

            var cell = BuildMonthCell(date, fixturesByDate, season);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            MonthGrid.Children.Add(cell);
        }
    }

    private Border BuildMonthCell(DateTime date, Dictionary<DateTime, List<Fixture>> fixturesByDate, Season? season)
    {
        bool isToday = date == DateTime.Today;
        bool isBlackout = _showBlackouts && _blackoutDates.Contains(date);
        bool isInSeason = season != null && date >= season.StartDate.Date && date <= season.EndDate.Date;
        bool isSeasonBound = season != null && (date == season.StartDate.Date || date == season.EndDate.Date);
        bool isMatchDay = _showLeagueMatches && season != null && date.DayOfWeek == season.MatchDayOfWeek && isInSeason;
        bool isCompetition = _showCompetitions && _competitionDateSet.Contains(date);
        bool isBankHoliday = _showBankHolidays && _bankHolidays.Contains(date);
        bool isTransferWindow = _showTransferWindow && season != null
            && season.TransferWindowStart.HasValue && season.TransferWindowEnd.HasValue
            && date >= season.TransferWindowStart.Value.Date && date <= season.TransferWindowEnd.Value.Date;
        fixturesByDate.TryGetValue(date, out var dayFixtures);

        var stack = new VerticalStackLayout { Spacing = 2, Padding = new Thickness(4, 3) };

        // Day number
        var dayLabel = new Label
        {
            Text = date.Day.ToString(),
            FontSize = 14,
            FontAttributes = isToday ? FontAttributes.Bold : FontAttributes.None,
            TextColor = isToday ? Color.FromArgb("#F59E0B")
                : isBlackout ? Color.FromArgb("#EF4444")
                : date.Month != _viewDate.Month ? Color.FromArgb("#9CA3AF")
                : Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#1F2937")
        };
        stack.Children.Add(dayLabel);

        // Season boundary marker
        if (isSeasonBound)
        {
            stack.Children.Add(new Label
            {
                Text = date == season!.StartDate.Date ? "▶ Start" : "◀ End",
                FontSize = 9,
                TextColor = SeasonColor,
                FontAttributes = FontAttributes.Bold
            });
        }

        // Blackout marker
        if (isBlackout)
        {
            stack.Children.Add(new Label
            {
                Text = "🚫 Blackout",
                FontSize = 9,
                TextColor = BlackoutColor
            });
        }

        // Bank holiday marker
        if (isBankHoliday)
        {
            stack.Children.Add(new Label
            {
                Text = "🏦 Bank Hol",
                FontSize = 9,
                TextColor = BankHolidayColor
            });
        }

        // Competition marker
        if (isCompetition)
        {
            var compName = _competitionDates.FirstOrDefault(c => c.Date == date).Name;
            stack.Children.Add(new Label
            {
                Text = $"🏆 {compName ?? "Comp"}",
                FontSize = 9,
                TextColor = CompetitionColor,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            });
        }

        // Transfer window marker
        if (isTransferWindow && !isBlackout && dayFixtures == null && !isCompetition)
        {
            if (date == season!.TransferWindowStart!.Value.Date)
                stack.Children.Add(new Label { Text = "🔄 Transfer Open", FontSize = 9, TextColor = TransferWindowColor });
            else if (date == season.TransferWindowEnd!.Value.Date)
                stack.Children.Add(new Label { Text = "🔄 Transfer Close", FontSize = 9, TextColor = TransferWindowColor });
        }

        // Custom calendar events
        if (_showCustomEvents)
        {
            var dayEvents = _calendarEvents.Where(e => e.Date == date).ToList();
            foreach (var evt in dayEvents.Take(2))
            {
                stack.Children.Add(new Label
                {
                    Text = $"📌 {evt.Title}",
                    FontSize = 9,
                    TextColor = CustomEventColor,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1
                });
            }
            if (dayEvents.Count > 2)
            {
                stack.Children.Add(new Label
                {
                    Text = $"+{dayEvents.Count - 2} more",
                    FontSize = 9,
                    TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280")
                });
            }
        }

        // Fixtures
        if (_showLeagueMatches && dayFixtures != null)
        {
            int maxShow = 3;
            int shown = 0;
            foreach (var f in dayFixtures.OrderBy(f => f.Date))
            {
                if (shown >= maxShow)
                {
                    stack.Children.Add(new Label
                    {
                        Text = $"+{dayFixtures.Count - maxShow} more",
                        FontSize = 9,
                        TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                            ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280")
                    });
                    break;
                }

                bool hasResult = f.Frames.Count > 0;
                var home = _seasonTeams.FirstOrDefault(t => t.Id == f.HomeTeamId)?.Name ?? "?";
                var away = _seasonTeams.FirstOrDefault(t => t.Id == f.AwayTeamId)?.Name ?? "?";

                var text = hasResult
                    ? $"{Abbreviate(home)} {f.HomeScore}-{f.AwayScore} {Abbreviate(away)}"
                    : $"{Abbreviate(home)} v {Abbreviate(away)}";

                stack.Children.Add(new Label
                {
                    Text = text,
                    FontSize = 9,
                    TextColor = hasResult ? ResultColor : FixtureColor,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1
                });
                shown++;
            }
        }

        // Background
        Color bgColor;
        if (isBlackout)
            bgColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#2D1215" : "#FEF2F2");
        else if (isCompetition)
            bgColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#431407" : "#FFF7ED");
        else if (isBankHoliday)
            bgColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#500724" : "#FDF2F8");
        else if (isMatchDay && dayFixtures == null)
            bgColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#1A1F2E" : "#F0F4FF");
        else if (isTransferWindow)
            bgColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#083344" : "#ECFEFF");
        else
            bgColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#111827") : Color.FromArgb("#FFFFFF");

        var border = new Border
        {
            Content = stack,
            BackgroundColor = bgColor,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Stroke = isToday ? TodayBorder
                : isSeasonBound ? SeasonColor
                : Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#374151") : Color.FromArgb("#E5E7EB"),
            StrokeThickness = isToday ? 2 : 1,
            MinimumHeightRequest = 80
        };

        // Tap to switch to day view
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            _viewDate = date;
            SwitchView(CalendarView.Day);
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }

    // ────────────────────── YEAR VIEW (Wall Planner) ──────────────────────

    private void RenderYear()
    {
        TitleLabel.Text = $"{_viewDate.Year} — Wall Planner";
        YearPlannerContainer.Children.Clear();

        var season = SeasonPicker.SelectedItem as Season;
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        // Ensure bank holidays cover the displayed year
        if (!_bankHolidays.Any(h => h.Year == _viewDate.Year))
        {
            foreach (var h in GetBankHolidays(_viewDate.Year, _viewDate.Year))
                _bankHolidays.Add(h);
        }

        var fixturesByDate = FilteredFixtures()
            .GroupBy(f => f.Date.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ── Build the grid: 13 rows (header + 12 months), 33 cols (month label + day-of-week + days 1-31) ──
        var colDefs = new ColumnDefinitionCollection();
        colDefs.Add(new ColumnDefinition(new GridLength(80)));  // Month name
        colDefs.Add(new ColumnDefinition(new GridLength(24)));  // Day-of-week spacer column
        for (int d = 0; d < 31; d++)
            colDefs.Add(new ColumnDefinition(new GridLength(42))); // Day cells

        var rowDefs = new RowDefinitionCollection();
        rowDefs.Add(new RowDefinition(new GridLength(28))); // Header row
        for (int m = 0; m < 12; m++)
            rowDefs.Add(new RowDefinition(new GridLength(38))); // Month rows

        var grid = new Grid
        {
            ColumnDefinitions = colDefs,
            RowDefinitions = rowDefs,
            ColumnSpacing = 1,
            RowSpacing = 1,
            BackgroundColor = isDark ? Color.FromArgb("#1F2937") : Color.FromArgb("#E5E7EB")
        };

        // ── Header row: day numbers 1–31 ──
        var headerBg = isDark ? Color.FromArgb("#111827") : Color.FromArgb("#F8FAFC");
        var headerFg = isDark ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280");

        var monthHeader = new Label
        {
            Text = _viewDate.Year.ToString(),
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            TextColor = isDark ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#374151"),
            BackgroundColor = headerBg
        };
        Grid.SetRow(monthHeader, 0);
        Grid.SetColumn(monthHeader, 0);
        grid.Children.Add(monthHeader);

        // Spacer header for day-of-week column
        var dowHeader = new Label
        {
            BackgroundColor = headerBg
        };
        Grid.SetRow(dowHeader, 0);
        Grid.SetColumn(dowHeader, 1);
        grid.Children.Add(dowHeader);

        for (int d = 1; d <= 31; d++)
        {
            var dayHeader = new Label
            {
                Text = d.ToString(),
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = headerFg,
                BackgroundColor = headerBg
            };
            Grid.SetRow(dayHeader, 0);
            Grid.SetColumn(dayHeader, d + 1);
            grid.Children.Add(dayHeader);
        }

        // ── Month rows ──
        string[] monthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun",
                                "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

        for (int m = 1; m <= 12; m++)
        {
            int row = m; // row 0 is header
            int daysInMonth = DateTime.DaysInMonth(_viewDate.Year, m);
            bool isCurrentMonth = m == DateTime.Today.Month && _viewDate.Year == DateTime.Today.Year;

            // Month label
            var monthLabel = new Label
            {
                Text = monthNames[m - 1],
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = isCurrentMonth ? TodayBorder
                    : isDark ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#1F2937"),
                BackgroundColor = isDark ? Color.FromArgb("#111827") : Color.FromArgb("#F8FAFC")
            };
            var monthTap = new TapGestureRecognizer();
            int capturedMonth = m;
            monthTap.Tapped += (_, _) =>
            {
                _viewDate = new DateTime(_viewDate.Year, capturedMonth, 1);
                SwitchView(CalendarView.Month);
            };
            monthLabel.GestureRecognizers.Add(monthTap);
            Grid.SetRow(monthLabel, row);
            Grid.SetColumn(monthLabel, 0);
            grid.Children.Add(monthLabel);

            // Day-of-week indicator for the 1st of the month
            var firstDow = new DateTime(_viewDate.Year, m, 1).DayOfWeek;
            string[] dowLetters = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];
            var dowLabel = new Label
            {
                Text = dowLetters[(int)firstDow],
                FontSize = 9,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Color.FromArgb("#9CA3AF"),
                BackgroundColor = isDark ? Color.FromArgb("#111827") : Color.FromArgb("#F8FAFC")
            };
            Grid.SetRow(dowLabel, row);
            Grid.SetColumn(dowLabel, 1);
            grid.Children.Add(dowLabel);

            // Day cells
            for (int d = 1; d <= 31; d++)
            {
                int col = d + 1; // offset by month-name col and dow col
                var cell = BuildWallPlannerCell(_viewDate.Year, m, d, daysInMonth,
                    fixturesByDate, season, isDark);
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, col);
                grid.Children.Add(cell);
            }
        }

        // ── Season span indicator below the grid ──
        if (season != null)
        {
            var seasonInfo = new HorizontalStackLayout { Spacing = 12, Margin = new Thickness(0, 8, 0, 0) };
            seasonInfo.Children.Add(new Label
            {
                Text = $"📅 {season.Name}",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = SeasonColor,
                VerticalOptions = LayoutOptions.Center
            });
            seasonInfo.Children.Add(new Label
            {
                Text = $"{season.StartDate:dd MMM yyyy} → {season.EndDate:dd MMM yyyy}",
                FontSize = 12,
                TextColor = isDark ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280"),
                VerticalOptions = LayoutOptions.Center
            });

            var totalFixtures = FilteredFixtures().Count();
            var playedFixtures = FilteredFixtures().Count(f => f.Frames.Count > 0);
            var blackouts = _blackoutDates.Count;
            var competitions = _competitionDates.Count;
            var bankHols = _bankHolidays.Count(h => h.Year == _viewDate.Year);
            var customEvents = _calendarEvents.Count;
            seasonInfo.Children.Add(new Label
            {
                Text = $"· {totalFixtures} fixtures · {playedFixtures} played · {blackouts} blackouts · {competitions} comp dates · {bankHols} bank hols · {customEvents} events",
                FontSize = 12,
                TextColor = isDark ? Color.FromArgb("#6B7280") : Color.FromArgb("#9CA3AF"),
                VerticalOptions = LayoutOptions.Center
            });

            YearPlannerContainer.Children.Add(seasonInfo);
        }

        YearPlannerContainer.Children.Insert(0, grid);
    }

    private View BuildWallPlannerCell(int year, int month, int day, int daysInMonth,
        Dictionary<DateTime, List<Fixture>> fixturesByDate, Season? season, bool isDark)
    {
        // Days beyond the month's range — empty/disabled cell
        if (day > daysInMonth)
        {
            return new BoxView
            {
                Color = isDark ? Color.FromArgb("#0D1117") : Color.FromArgb("#F1F5F9")
            };
        }

        var date = new DateTime(year, month, day);
        bool isToday = date == DateTime.Today;
        bool isBlackout = _showBlackouts && _blackoutDates.Contains(date);
        bool isInSeason = season != null && date >= season.StartDate.Date && date <= season.EndDate.Date;
        bool isSeasonBound = season != null && (date == season.StartDate.Date || date == season.EndDate.Date);
        bool isMatchDay = _showLeagueMatches && season != null && date.DayOfWeek == season.MatchDayOfWeek && isInSeason;
        bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        bool isCompetition = _showCompetitions && _competitionDateSet.Contains(date);
        bool isBankHoliday = _showBankHolidays && _bankHolidays.Contains(date);
        bool isTransferWindow = _showTransferWindow && season != null
            && season.TransferWindowStart.HasValue && season.TransferWindowEnd.HasValue
            && date >= season.TransferWindowStart.Value.Date && date <= season.TransferWindowEnd.Value.Date;
        bool hasCustomEvent = _showCustomEvents && _calendarEvents.Any(e => e.Date == date);

        fixturesByDate.TryGetValue(date, out var dayFixtures);
        bool hasFixtures = _showLeagueMatches && dayFixtures != null && dayFixtures.Count > 0;
        bool hasResults = hasFixtures && dayFixtures!.Any(f => f.Frames.Count > 0);
        bool allPlayed = hasFixtures && dayFixtures!.All(f => f.Frames.Count > 0);

        // Cell background — priority order
        Color bgColor;
        if (isBlackout)
            bgColor = isDark ? Color.FromArgb("#2D1215") : Color.FromArgb("#FEF2F2");
        else if (hasResults && allPlayed)
            bgColor = isDark ? Color.FromArgb("#052E16") : Color.FromArgb("#ECFDF5");
        else if (hasFixtures)
            bgColor = isDark ? Color.FromArgb("#172554") : Color.FromArgb("#EFF6FF");
        else if (isCompetition)
            bgColor = isDark ? Color.FromArgb("#431407") : Color.FromArgb("#FFF7ED");
        else if (isBankHoliday)
            bgColor = isDark ? Color.FromArgb("#500724") : Color.FromArgb("#FDF2F8");
        else if (isSeasonBound)
            bgColor = isDark ? Color.FromArgb("#1E1B4B") : Color.FromArgb("#F5F3FF");
        else if (isTransferWindow)
            bgColor = isDark ? Color.FromArgb("#083344") : Color.FromArgb("#ECFEFF");
        else if (isMatchDay)
            bgColor = isDark ? Color.FromArgb("#1A1F2E") : Color.FromArgb("#F8FAFF");
        else if (hasCustomEvent)
            bgColor = isDark ? Color.FromArgb("#042F2E") : Color.FromArgb("#F0FDFA");
        else if (!isInSeason && season != null)
            bgColor = isDark ? Color.FromArgb("#0D1117") : Color.FromArgb("#F9FAFB");
        else if (isWeekend)
            bgColor = isDark ? Color.FromArgb("#111419") : Color.FromArgb("#F8F9FA");
        else
            bgColor = isDark ? Color.FromArgb("#111827") : Color.FromArgb("#FFFFFF");

        // Text color
        Color textColor;
        if (isToday)
            textColor = TodayBorder;
        else if (isBlackout)
            textColor = BlackoutColor;
        else if (hasResults)
            textColor = ResultColor;
        else if (hasFixtures)
            textColor = FixtureColor;
        else if (isCompetition)
            textColor = CompetitionColor;
        else if (isBankHoliday)
            textColor = BankHolidayColor;
        else if (hasCustomEvent)
            textColor = CustomEventColor;
        else if (!isInSeason && season != null)
            textColor = Color.FromArgb("#6B7280");
        else
            textColor = isDark ? Color.FromArgb("#D1D5DB") : Color.FromArgb("#374151");

        // Build cell content
        var cellStack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 0
        };

        // Day number (with count if fixtures)
        var dayText = hasFixtures ? $"{dayFixtures!.Count}" : "";
        cellStack.Children.Add(new Label
        {
            Text = dayText,
            FontSize = hasFixtures ? 11 : 0,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = textColor,
            HeightRequest = hasFixtures ? 14 : 0
        });

        // Dot indicators row
        bool hasDots = hasFixtures || isBlackout || isSeasonBound || isCompetition || isBankHoliday || isTransferWindow || hasCustomEvent;
        if (hasDots)
        {
            var dotRow = new HorizontalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center
            };

            if (isSeasonBound)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = SeasonColor,
                    WidthRequest = 6, HeightRequest = 6, CornerRadius = 3
                });
            }

            if (isBlackout)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = BlackoutColor,
                    WidthRequest = 6, HeightRequest = 6, CornerRadius = 3
                });
            }

            if (hasResults)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = ResultColor,
                    WidthRequest = 6, HeightRequest = 6, CornerRadius = 3
                });
            }
            else if (hasFixtures)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = FixtureColor,
                    WidthRequest = 6, HeightRequest = 6, CornerRadius = 3
                });
            }

            if (isCompetition)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = CompetitionColor,
                    WidthRequest = 6, HeightRequest = 6, CornerRadius = 3
                });
            }

            if (isBankHoliday)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = BankHolidayColor,
                    WidthRequest = 6, HeightRequest = 6, CornerRadius = 3
                });
            }

            if (hasCustomEvent)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = CustomEventColor,
                    WidthRequest = 6, HeightRequest = 6, CornerRadius = 3
                });
            }

            if (isTransferWindow && !isCompetition && !hasFixtures && !isBlackout && !isSeasonBound && !hasCustomEvent)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = TransferWindowColor,
                    WidthRequest = 6, HeightRequest = 6, CornerRadius = 3
                });
            }

            cellStack.Children.Add(dotRow);
        }

        var border = new Border
        {
            Content = cellStack,
            BackgroundColor = bgColor,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = isToday ? 4 : 2 },
            Stroke = isToday ? TodayBorder
                : isSeasonBound ? SeasonColor
                : Colors.Transparent,
            StrokeThickness = isToday ? 2 : isSeasonBound ? 1 : 0,
            Padding = new Thickness(0, 2)
        };

        // Tooltip-style: tap to see day details
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            _viewDate = date;
            SwitchView(CalendarView.Day);
        };
        border.GestureRecognizers.Add(tap);

        return border;
    }

    // ────────────────────── DAY VIEW ──────────────────────

    private void RenderDay()
    {
        TitleLabel.Text = _viewDate.ToString("dddd, dd MMMM yyyy");
        DayContent.Children.Clear();

        var season = SeasonPicker.SelectedItem as Season;
        var date = _viewDate.Date;

        // Day status badges
        var badgeRow = new HorizontalStackLayout { Spacing = 8 };

        if (date == DateTime.Today)
            badgeRow.Children.Add(MakeBadge("Today", "#F59E0B", "#FFFBEB"));

        if (season != null)
        {
            bool inSeason = date >= season.StartDate.Date && date <= season.EndDate.Date;
            if (inSeason)
                badgeRow.Children.Add(MakeBadge("In Season", "#8B5CF6", "#F5F3FF"));
            if (date == season.StartDate.Date)
                badgeRow.Children.Add(MakeBadge("Season Start", "#8B5CF6", "#F5F3FF"));
            if (date == season.EndDate.Date)
                badgeRow.Children.Add(MakeBadge("Season End", "#8B5CF6", "#F5F3FF"));
            if (date.DayOfWeek == season.MatchDayOfWeek && inSeason)
                badgeRow.Children.Add(MakeBadge("Match Night", "#3B82F6", "#EFF6FF"));
        }

        if (_showBlackouts && _blackoutDates.Contains(date))
            badgeRow.Children.Add(MakeBadge("Blackout Date", "#EF4444", "#FEF2F2"));

        if (_showBankHolidays && _bankHolidays.Contains(date))
            badgeRow.Children.Add(MakeBadge("Bank Holiday", "#EC4899", "#FDF2F8"));

        if (_showCompetitions && _competitionDateSet.Contains(date))
        {
            var compNames = _competitionDates.Where(c => c.Date == date).Select(c => c.Name);
            foreach (var name in compNames)
                badgeRow.Children.Add(MakeBadge($"🏆 {name}", "#F97316", "#FFF7ED"));
        }

        if (_showTransferWindow && season != null
            && season.TransferWindowStart.HasValue && season.TransferWindowEnd.HasValue
            && date >= season.TransferWindowStart.Value.Date && date <= season.TransferWindowEnd.Value.Date)
        {
            if (date == season.TransferWindowStart.Value.Date)
                badgeRow.Children.Add(MakeBadge("Transfer Window Opens", "#06B6D4", "#ECFEFF"));
            else if (date == season.TransferWindowEnd.Value.Date)
                badgeRow.Children.Add(MakeBadge("Transfer Window Closes", "#06B6D4", "#ECFEFF"));
            else
                badgeRow.Children.Add(MakeBadge("Transfer Window", "#06B6D4", "#ECFEFF"));
        }

        if (badgeRow.Children.Count > 0)
            DayContent.Children.Add(badgeRow);

        // Custom calendar events for this day
        if (_showCustomEvents)
        {
            var dayEvents = _calendarEvents.Where(e => e.Date == date).OrderBy(e => e.Title).ToList();
            if (dayEvents.Count > 0)
            {
                DayContent.Children.Add(new Label
                {
                    Text = $"📌 {dayEvents.Count} event{(dayEvents.Count != 1 ? "s" : "")}",
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = CustomEventColor,
                    Margin = new Thickness(0, 4, 0, 0)
                });

                foreach (var evt in dayEvents)
                {
                    var evtCard = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(16, 12) };
                    var evtTopRow = new HorizontalStackLayout { Spacing = 8 };
                    evtTopRow.Children.Add(MakeBadge(evt.Category.ToString(), "#14B8A6", "#F0FDFA"));
                    evtTopRow.Children.Add(new Label
                    {
                        Text = evt.Title,
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        VerticalOptions = LayoutOptions.Center,
                        TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                            ? Color.FromArgb("#F9FAFB") : Color.FromArgb("#111827")
                    });
                    evtCard.Children.Add(evtTopRow);

                    if (!string.IsNullOrWhiteSpace(evt.Notes))
                    {
                        evtCard.Children.Add(new Label
                        {
                            Text = evt.Notes,
                            FontSize = 13,
                            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                                ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280")
                        });
                    }

                    var evtBorder = new Border
                    {
                        Content = evtCard,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                        Stroke = CustomEventColor,
                        StrokeThickness = 1,
                        BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                            ? Color.FromArgb("#111827") : Color.FromArgb("#FFFFFF")
                    };

                    // Tap to edit/delete
                    var capturedEvt = evt;
                    var evtTap = new TapGestureRecognizer();
                    evtTap.Tapped += async (_, _) => await ShowEditEventDialog(capturedEvt);
                    evtBorder.GestureRecognizers.Add(evtTap);

                    DayContent.Children.Add(evtBorder);
                }
            }
        }

        // Fixtures for this day
        var dayFixtures = FilteredFixtures().Where(f => f.Date.Date == date).OrderBy(f => f.Date).ToList();

        var hasEventsAbove = _showCustomEvents && _calendarEvents.Any(e => e.Date == date);
        if (dayFixtures.Count == 0 && !hasEventsAbove)
        {
            DayContent.Children.Add(new Border
            {
                Content = new VerticalStackLayout
                {
                    Spacing = 4,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Padding = new Thickness(0, 20),
                    Children =
                    {
                        new Label
                        {
                            Text = "📅",
                            FontSize = 36,
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new Label
                        {
                            Text = "No fixtures scheduled",
                            FontSize = 15,
                            HorizontalTextAlignment = TextAlignment.Center,
                            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                                ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280")
                        }
                    }
                },
                Padding = 24,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Stroke = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#374151") : Color.FromArgb("#E5E7EB"),
                StrokeThickness = 1,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#111827") : Color.FromArgb("#FFFFFF")
            });
            return;
        }

        if (dayFixtures.Count == 0)
            return;

        // Header
        DayContent.Children.Add(new Label
        {
            Text = $"{dayFixtures.Count} fixture{(dayFixtures.Count != 1 ? "s" : "")}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#1F2937")
        });

        foreach (var f in dayFixtures)
        {
            var home = _seasonTeams.FirstOrDefault(t => t.Id == f.HomeTeamId);
            var away = _seasonTeams.FirstOrDefault(t => t.Id == f.AwayTeamId);
            var venue = _seasonVenues.FirstOrDefault(v => v.Id == f.VenueId);
            var div = _divisions.FirstOrDefault(d => d.Id == f.DivisionId);
            bool hasResult = f.Frames.Count > 0;

            var card = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 14) };

            // Time & division row
            var topRow = new HorizontalStackLayout { Spacing = 8 };
            topRow.Children.Add(new Label
            {
                Text = f.Date.ToString("h:mm tt"),
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#374151")
            });
            if (div != null && div.Id != Guid.Empty)
            {
                topRow.Children.Add(MakeBadge(div.Name, "#6366F1", "#EEF2FF"));
            }
            if (hasResult)
            {
                topRow.Children.Add(MakeBadge("Played", "#10B981", "#ECFDF5"));
            }
            card.Children.Add(topRow);

            // Teams & score
            var matchRow = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection(
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)),
                ColumnSpacing = 12
            };

            var homeLabel = new Label
            {
                Text = home?.Name ?? "Home",
                FontSize = 18,
                FontAttributes = hasResult && f.HomeScore > f.AwayScore ? FontAttributes.Bold : FontAttributes.None,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#F9FAFB") : Color.FromArgb("#111827")
            };

            var scoreLabel = new Label
            {
                Text = hasResult ? $"{f.HomeScore} - {f.AwayScore}" : "vs",
                FontSize = hasResult ? 22 : 16,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = hasResult ? (Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#F9FAFB") : Color.FromArgb("#111827")) : FixtureColor
            };

            var awayLabel = new Label
            {
                Text = away?.Name ?? "Away",
                FontSize = 18,
                FontAttributes = hasResult && f.AwayScore > f.HomeScore ? FontAttributes.Bold : FontAttributes.None,
                HorizontalTextAlignment = TextAlignment.Start,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#F9FAFB") : Color.FromArgb("#111827")
            };

            Grid.SetColumn(homeLabel, 0);
            Grid.SetColumn(scoreLabel, 1);
            Grid.SetColumn(awayLabel, 2);
            matchRow.Children.Add(homeLabel);
            matchRow.Children.Add(scoreLabel);
            matchRow.Children.Add(awayLabel);
            card.Children.Add(matchRow);

            // Venue
            if (venue != null)
            {
                card.Children.Add(new Label
                {
                    Text = $"📍 {venue.Name}",
                    FontSize = 12,
                    HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280")
                });
            }

            // Frame details if result exists
            if (hasResult && f.Frames.Count > 0)
            {
                card.Children.Add(new Label
                {
                    Text = $"Frames: {f.Frames.Count} played",
                    FontSize = 11,
                    HorizontalTextAlignment = TextAlignment.Center,
                    TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#6B7280") : Color.FromArgb("#9CA3AF")
                });
            }

            var accentColor = hasResult ? ResultColor : FixtureColor;
            DayContent.Children.Add(new Border
            {
                Content = card,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Stroke = accentColor,
                StrokeThickness = 1,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#111827") : Color.FromArgb("#FFFFFF")
            });
        }
    }

    // ────────────────────── Helpers ──────────────────────

    private static Border MakeBadge(string text, string textHex, string bgHex)
    {
        return new Border
        {
            Content = new Label
            {
                Text = text,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(textHex),
                Margin = new Thickness(0)
            },
            Padding = new Thickness(8, 3),
            BackgroundColor = Color.FromArgb(bgHex),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
            StrokeThickness = 0,
            VerticalOptions = LayoutOptions.Center
        };
    }

    private static string Abbreviate(string name)
    {
        if (string.IsNullOrEmpty(name)) return "?";
        // If short enough, return as-is
        if (name.Length <= 12) return name;
        // Try to use initials of words
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
            return string.Concat(words.Select(w => w[0]));
        return name[..10] + "…";
    }

    // ────────────────────── UK Bank Holidays ──────────────────────

    private static HashSet<DateTime> GetBankHolidays(int startYear, int endYear)
    {
        var holidays = new HashSet<DateTime>();
        for (int y = startYear; y <= endYear; y++)
        {
            // New Year's Day (substitute if weekend)
            holidays.Add(SubstituteWeekend(new DateTime(y, 1, 1)));

            // Good Friday & Easter Monday
            var easter = CalculateEaster(y);
            holidays.Add(easter.AddDays(-2)); // Good Friday
            holidays.Add(easter.AddDays(1));  // Easter Monday

            // Early May bank holiday (first Monday of May)
            holidays.Add(FirstMonday(y, 5));

            // Spring bank holiday (last Monday of May)
            holidays.Add(LastMonday(y, 5));

            // Summer bank holiday (last Monday of August)
            holidays.Add(LastMonday(y, 8));

            // Christmas Day (substitute if weekend)
            var xmas = new DateTime(y, 12, 25);
            var boxing = new DateTime(y, 12, 26);
            if (xmas.DayOfWeek == DayOfWeek.Saturday)
            {
                holidays.Add(new DateTime(y, 12, 27)); // Mon
                holidays.Add(new DateTime(y, 12, 28)); // Tue
            }
            else if (xmas.DayOfWeek == DayOfWeek.Sunday)
            {
                holidays.Add(new DateTime(y, 12, 27)); // Tue
                holidays.Add(boxing);
            }
            else
            {
                holidays.Add(xmas);
                holidays.Add(SubstituteWeekend(boxing));
            }
        }
        return holidays;
    }

    private static DateTime SubstituteWeekend(DateTime date) => date.DayOfWeek switch
    {
        DayOfWeek.Saturday => date.AddDays(2),
        DayOfWeek.Sunday => date.AddDays(1),
        _ => date
    };

    private static DateTime FirstMonday(int year, int month)
    {
        var d = new DateTime(year, month, 1);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return d;
    }

    private static DateTime LastMonday(int year, int month)
    {
        var d = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(-1);
        return d;
    }

    /// <summary>Anonymous Gregorian algorithm for Easter Sunday.</summary>
    private static DateTime CalculateEaster(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(year, month, day);
    }
}
