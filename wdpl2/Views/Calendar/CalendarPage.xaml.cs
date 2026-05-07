using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Wdpl2.Models;

namespace Wdpl2.Views;

public partial class CalendarPage : ContentPage
{
    private static LeagueData League => DataStore.Data;
    private static CalendarSettings CalSettings => League.CalendarSettings;

    private readonly ObservableCollection<Season> _seasons = new();
    private readonly ObservableCollection<Division> _divisions = new();
    private List<Fixture> _seasonFixtures = [];
    private List<Team> _seasonTeams = [];
    private List<Venue> _seasonVenues = [];
    private Dictionary<DateTime, string> _blackoutDates = [];
    private List<(DateTime Date, string Name)> _competitionDates = [];
    private HashSet<DateTime> _competitionDateSet = [];
    private Dictionary<DateTime, string> _bankHolidays = [];
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

    // Day-of-week highlight for year view
    private DayOfWeek? _highlightDayOfWeek;

    // Zoom level for year/wall planner view
    private double _yearZoomLevel = 1.0;

    // Colors — loaded from settings, updated by ApplySettings()
    private Color FixtureColor = Color.FromArgb("#3B82F6");
    private Color ResultColor = Color.FromArgb("#10B981");
    private Color BlackoutColor = Color.FromArgb("#EF4444");
    private Color SeasonColor = Color.FromArgb("#8B5CF6");
    private Color TodayBorder = Color.FromArgb("#F59E0B");
    private Color CompetitionColor = Color.FromArgb("#F97316");
    private Color BankHolidayColor = Color.FromArgb("#EC4899");
    private Color TransferWindowColor = Color.FromArgb("#06B6D4");
    private Color CustomEventColor = Color.FromArgb("#14B8A6");

    public CalendarPage()
    {
        InitializeComponent();

        SeasonPicker.ItemsSource = _seasons;
        SeasonPicker.ItemDisplayBinding = new Binding("Name");

        DivisionPicker.ItemsSource = _divisions;
        DivisionPicker.ItemDisplayBinding = new Binding("Name");

        ApplySettings();
        BuildDayHeaders();
        LoadSeasons();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Wdpl2.Services.SeasonService.Current.SeasonChanged += OnGlobalSeasonChanged;

        // Pick up any changes the user made on CalendarOptionsPage (colours, filter
        // defaults, week start, legend visibility, preset events, etc.).
        ApplySettings();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Wdpl2.Services.SeasonService.Current.SeasonChanged -= OnGlobalSeasonChanged;
    }

    private void OnGlobalSeasonChanged(object? sender, Wdpl2.Services.SeasonChangedEventArgs e)
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadSeasons();
            Refresh();
        });
    }

    /// <summary>
    /// Reads CalendarSettings and applies colours, filter defaults, and view preferences.
    /// Called on construction and when returning from the CalendarOptionsPage.
    /// </summary>
    private void ApplySettings()
    {
        var s = CalSettings;

        // Colors
        FixtureColor = SafeColor(s.FixtureColor, "#3B82F6");
        ResultColor = SafeColor(s.ResultColor, "#10B981");
        BlackoutColor = SafeColor(s.BlackoutColor, "#EF4444");
        SeasonColor = SafeColor(s.SeasonColor, "#8B5CF6");
        TodayBorder = SafeColor(s.TodayColor, "#F59E0B");
        CompetitionColor = SafeColor(s.CompetitionColor, "#F97316");
        BankHolidayColor = SafeColor(s.BankHolidayColor, "#EC4899");
        TransferWindowColor = SafeColor(s.TransferWindowColor, "#06B6D4");
        CustomEventColor = SafeColor(s.CustomEventColor, "#14B8A6");

        // Filter defaults
        _showLeagueMatches = s.ShowLeagueMatches;
        _showCompetitions = s.ShowCompetitions;
        _showBankHolidays = s.ShowBankHolidays;
        _showBlackouts = s.ShowBlackouts;
        _showTransferWindow = s.ShowTransferWindow;
        _showCustomEvents = s.ShowCustomEvents;

        // Sync filter checkboxes
        foreach (var cb in GetFilterCheckboxes())
            cb.IsChecked = true; // will be overridden below
        SyncFilterCheckboxes();

        // Legend visibility
        if (Content is Grid rootGrid && rootGrid.Children.Count > 2 && rootGrid.Children[^1] is Border legendBorder)
            legendBorder.IsVisible = s.ShowLegend;

        // Rebuild day headers in case week start changed
        BuildDayHeaders();

        // Reload events and refresh
        ReloadCalendarEvents();
        Refresh();
    }

    private void SyncFilterCheckboxes()
    {
        var checkboxes = GetFilterCheckboxes().ToList();
        // Order: League, Competitions, BankHolidays, Blackouts, Transfer, Events
        bool[] values = [_showLeagueMatches, _showCompetitions, _showBankHolidays,
                         _showBlackouts, _showTransferWindow, _showCustomEvents];
        for (int i = 0; i < Math.Min(checkboxes.Count, values.Length); i++)
            checkboxes[i].IsChecked = values[i];
    }

    private static Color SafeColor(string? hex, string fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex))
                return Color.FromArgb(hex);
        }
        catch { /* ignored */ }
        return Color.FromArgb(fallback);
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

    private async void OnCalendarOptionsClicked(object? sender, EventArgs e)
    {
        var action = await DisplayActionSheet("Calendar Options", "Cancel", null,
            "\U0001f4cc Add Event",
            "\U0001f4c5 Jump to Date",
            "\U0001f4cb View All Events",
            _showLeagueMatches && _showCompetitions && _showBankHolidays && _showBlackouts && _showTransferWindow && _showCustomEvents
                ? "\U0001f6ab Hide All Filters"
                : "\u2705 Show All Filters",
            "\u2699\ufe0f Calendar Settings");

        switch (action)
        {
            case "\U0001f4cc Add Event":
                var targetDate = _currentView == CalendarView.Day ? _viewDate : DateTime.Today;
                await ShowAddEventDialog(targetDate);
                break;

            case "\U0001f4c5 Jump to Date":
                var dateStr = await DisplayPromptAsync("Jump to Date",
                    "Enter date (dd/MM/yyyy):",
                    initialValue: _viewDate.ToString("dd/MM/yyyy"), maxLength: 10);
                if (!string.IsNullOrWhiteSpace(dateStr) && DateTime.TryParseExact(dateStr.Trim(),
                    ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd"],
                    null, System.Globalization.DateTimeStyles.None, out var jumpDate))
                {
                    _viewDate = jumpDate.Date;
                    Refresh();
                }
                break;

            case "\U0001f4cb View All Events":
                var events = _calendarEvents.OrderBy(e2 => e2.Date).ToList();
                if (events.Count == 0)
                {
                    await DisplayAlert("Events", "No custom events yet.", "OK");
                    break;
                }
                var eventOptions = events
                    .Select(e2 => $"{e2.Date:dd MMM} — {e2.Title} ({e2.Category})")
                    .ToArray();
                var picked = await DisplayActionSheet(
                    $"{events.Count} event{(events.Count != 1 ? "s" : "")}", "Close", null, eventOptions);
                if (picked != null && picked != "Close")
                {
                    var idx = Array.IndexOf(eventOptions, picked);
                    if (idx >= 0 && idx < events.Count)
                    {
                        var evt = events[idx];
                        var evtAction = await DisplayActionSheet(
                            $"\U0001f4cc {evt.Title}", "Cancel", null,
                            "\U0001f4c5 Go to Date", "\u270f\ufe0f Edit Event");
                        if (evtAction == "\U0001f4c5 Go to Date")
                        {
                            _viewDate = evt.Date;
                            SwitchView(CalendarView.Day);
                        }
                        else if (evtAction == "\u270f\ufe0f Edit Event")
                        {
                            await ShowEditEventDialog(evt);
                        }
                    }
                }
                break;

            case "\U0001f6ab Hide All Filters":
                SetAllFilters(false);
                break;

            case "\u2705 Show All Filters":
                SetAllFilters(true);
                break;

            case "\u2699\ufe0f Calendar Settings":
                var optionsPage = Application.Current?.Handler?.MauiContext?.Services.GetService<CalendarOptionsPage>()
                    ?? throw new InvalidOperationException("CalendarOptionsPage not registered");
                optionsPage.Disappearing += (_, _) => ApplySettings();
                await Navigation.PushModalAsync(new NavigationPage(optionsPage));
                break;
        }
    }

    private void SetAllFilters(bool value)
    {
        _showLeagueMatches = value;
        _showCompetitions = value;
        _showBankHolidays = value;
        _showBlackouts = value;
        _showTransferWindow = value;
        _showCustomEvents = value;

        // Sync the checkboxes — find them in the filter row
        foreach (var child in GetFilterCheckboxes())
            child.IsChecked = value;

        Refresh();
    }

    private IEnumerable<CheckBox> GetFilterCheckboxes()
    {
        // The filter row is a HorizontalStackLayout containing nested HorizontalStackLayouts with CheckBoxes
        // Walk the visual tree to find them
        static IEnumerable<CheckBox> FindCheckBoxes(IView view)
        {
            if (view is CheckBox cb)
            {
                yield return cb;
            }
            else if (view is Layout layout)
            {
                foreach (var child in layout.Children)
                    foreach (var found in FindCheckBoxes(child))
                        yield return found;
            }
        }

        // The toolbar is the first child (Border) in the page's root Grid
        if (Content is Grid rootGrid && rootGrid.Children.Count > 0 && rootGrid.Children[0] is Border toolbarBorder)
        {
            foreach (var cb in FindCheckBoxes(toolbarBorder))
                yield return cb;
        }
    }

    private async Task ShowAddEventDialog(DateTime defaultDate)
    {
        var title = await DisplayPromptAsync("Add Calendar Event",
            $"Event title for {defaultDate:ddd dd MMM yyyy}:",
            placeholder: "e.g. Committee Meeting", maxLength: 100);
        if (string.IsNullOrWhiteSpace(title)) return;

        var categories = Enum.GetNames<CalendarEventCategory>();
        var category = await DisplayActionSheet("Event Type", "Cancel", null, categories);
        if (category is null or "Cancel") return;

        // If Competition category, offer to link a competition
        Guid? competitionId = null;
        if (category == nameof(CalendarEventCategory.Competition))
        {
            var season = SeasonPicker.SelectedItem as Season;
            var comps = League.Competitions
                .Where(c => season == null || c.SeasonId == season.Id)
                .Where(c => c.Status != CompetitionStatus.Draft)
                .OrderByDescending(c => c.StartDate)
                .ToList();

            if (comps.Count > 0)
            {
                var compNames = comps.Select(c => c.Name).ToArray();
                var selected = await DisplayActionSheet("Link to Competition", "Cancel", "None", compNames);
                if (selected is not null and not "Cancel")
                {
                    var comp = selected == "None" ? null : comps.FirstOrDefault(c => c.Name == selected);
                    competitionId = comp?.Id;
                }
            }
        }

        // Offer to change the date
        var changeDateAction = await DisplayActionSheet(
            $"Date: {defaultDate:ddd dd MMM yyyy}", null, null,
            "Keep this date", "Change date");
        var eventDate = defaultDate;
        if (changeDateAction == "Change date")
        {
            var dateStr = await DisplayPromptAsync("Event Date",
                "Enter date (dd/MM/yyyy):",
                initialValue: defaultDate.ToString("dd/MM/yyyy"), maxLength: 10);
            if (!string.IsNullOrWhiteSpace(dateStr) && DateTime.TryParseExact(dateStr.Trim(),
                ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd"],
                null, System.Globalization.DateTimeStyles.None, out var parsed))
            {
                eventDate = parsed.Date;
            }
        }

        var notes = await DisplayPromptAsync("Notes (optional)",
            "Any additional details:", placeholder: "Optional", accept: "Save", cancel: "Skip");

        var evt = new CalendarEvent
        {
            Date = eventDate,
            Title = title.Trim(),
            Category = Enum.TryParse<CalendarEventCategory>(category, out var cat) ? cat : CalendarEventCategory.General,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CompetitionId = competitionId
        };

        League.CalendarEvents.Add(evt);
        DataStore.Save();
        ReloadCalendarEvents();
        Refresh();
    }

    private async Task ShowEditEventDialog(CalendarEvent evt)
    {
        var compName = evt.CompetitionId.HasValue
            ? League.Competitions.FirstOrDefault(c => c.Id == evt.CompetitionId.Value)?.Name
            : null;

        var summary = $"\U0001f4cc {evt.Title}\n{evt.Category} · {evt.Date:ddd dd MMM yyyy}";
        if (!string.IsNullOrEmpty(compName))
            summary += $"\n🏆 {compName}";
        if (!string.IsNullOrWhiteSpace(evt.Notes))
            summary += $"\n{evt.Notes}";

        var options = new List<string>
        {
            $"\u270f\ufe0f Title: {evt.Title}",
            $"\U0001f3f7\ufe0f Type: {evt.Category}",
            $"\U0001f4c5 Date: {evt.Date:dd/MM/yyyy}",
            $"\U0001f3c6 Competition: {(string.IsNullOrEmpty(compName) ? "(none)" : compName)}",
            $"\U0001f4dd Notes: {(string.IsNullOrWhiteSpace(evt.Notes) ? "(none)" : evt.Notes)}"
        };

        var action = await DisplayActionSheet(summary, "Cancel", "\u274c Delete", options.ToArray());

        if (action != null && action.StartsWith("\u270f\ufe0f Title:"))
        {
            var newTitle = await DisplayPromptAsync("Edit Title", "Event title:",
                initialValue: evt.Title, maxLength: 100);
            if (!string.IsNullOrWhiteSpace(newTitle))
            {
                evt.Title = newTitle.Trim();
                DataStore.Save();
                ReloadCalendarEvents();
                Refresh();
            }
        }
        else if (action != null && action.StartsWith("\U0001f3f7\ufe0f Type:"))
        {
            var categories = Enum.GetNames<CalendarEventCategory>();
            var cat = await DisplayActionSheet($"Current: {evt.Category}", "Cancel", null, categories);
            if (cat is not null and not "Cancel" && Enum.TryParse<CalendarEventCategory>(cat, out var parsed))
            {
                evt.Category = parsed;
                DataStore.Save();
                ReloadCalendarEvents();
                Refresh();
            }
        }
        else if (action != null && action.StartsWith("\U0001f4c5 Date:"))
        {
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
        }
        else if (action != null && action.StartsWith("\U0001f3c6 Competition:"))
        {
            var season = SeasonPicker.SelectedItem as Season;
            var comps = League.Competitions
                .Where(c => season == null || c.SeasonId == season.Id)
                .Where(c => c.Status != CompetitionStatus.Draft)
                .OrderByDescending(c => c.StartDate)
                .ToList();

            if (comps.Count > 0)
            {
                var compNames = comps.Select(c => c.Name).ToArray();
                var selected = await DisplayActionSheet(
                    $"Current: {(string.IsNullOrEmpty(compName) ? "None" : compName)}",
                    "Cancel", "Remove Link", compNames);

                if (selected == "Remove Link")
                {
                    evt.CompetitionId = null;
                    DataStore.Save();
                    ReloadCalendarEvents();
                    Refresh();
                }
                else if (selected is not null and not "Cancel")
                {
                    var comp = comps.FirstOrDefault(c => c.Name == selected);
                    if (comp != null)
                    {
                        evt.CompetitionId = comp.Id;
                        DataStore.Save();
                        ReloadCalendarEvents();
                        Refresh();
                    }
                }
            }
            else
            {
                await DisplayAlert("No Competitions",
                    "No competitions found for this season.", "OK");
            }
        }
        else if (action != null && action.StartsWith("\U0001f4dd Notes:"))
        {
            var newNotes = await DisplayPromptAsync("Edit Notes", "Details:",
                initialValue: evt.Notes ?? "", accept: "Save", cancel: "Cancel");
            if (newNotes != null)
            {
                evt.Notes = string.IsNullOrWhiteSpace(newNotes) ? null : newNotes.Trim();
                DataStore.Save();
                ReloadCalendarEvents();
                Refresh();
            }
        }
        else if (action == "\u274c Delete")
        {
            bool confirm = await DisplayAlert("Delete Event",
                $"Delete '{evt.Title}' on {evt.Date:dd MMM yyyy}?", "Delete", "Cancel");
            if (confirm)
            {
                League.CalendarEvents.Remove(evt);
                DataStore.Save();
                ReloadCalendarEvents();
                Refresh();
            }
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
        _blackoutDates = season.BlackoutDates
            .Select(d => d.Date)
            .Distinct()
            .ToDictionary(
                d => d,
                d => season.BlackoutDateTitles?.GetValueOrDefault(d.ToString("yyyy-MM-dd"), "") ?? "");

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
        _bankHolidays = GetPresetHolidays(season.StartDate.Year, season.EndDate.Year);

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
        DayHeaders.Children.Clear();
        string[] days = CalSettings.WeekStartDay == 0
            ? ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"]
            : ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
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

        // Calculate start column based on week start preference
        int startCol = CalSettings.WeekStartDay == 0
            ? (int)firstOfMonth.DayOfWeek                   // Sunday = 0
            : ((int)firstOfMonth.DayOfWeek + 6) % 7;        // Monday = 0

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
        bool isBlackout = _showBlackouts && _blackoutDates.ContainsKey(date);
        bool isInSeason = season != null && date >= season.StartDate.Date && date <= season.EndDate.Date;
        bool isSeasonBound = season != null && (date == season.StartDate.Date || date == season.EndDate.Date);
        bool isMatchDay = _showLeagueMatches && season != null && date.DayOfWeek == season.MatchDayOfWeek && isInSeason;
        bool isCompetition = _showCompetitions && _competitionDateSet.Contains(date);
        bool isBankHoliday = _showBankHolidays && _bankHolidays.ContainsKey(date);
        bool isTransferWindow = _showTransferWindow && season != null
            && season.TransferWindowStart.HasValue && season.TransferWindowEnd.HasValue
            && date >= season.TransferWindowStart.Value.Date && date <= season.TransferWindowEnd.Value.Date;
        fixturesByDate.TryGetValue(date, out var dayFixtures);

        var stack = new VerticalStackLayout { Spacing = 2, Padding = new Thickness(4, 3) };

        // Day number
        var dayLabel = new Label
        {
            Text = date.Day.ToString(),
            FontSize = CalSettings.MonthDayFontSize,
            FontAttributes = isToday ? FontAttributes.Bold : FontAttributes.None,
            TextColor = isToday ? TodayBorder
                : isBlackout ? BlackoutColor
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
            var boTitle = _blackoutDates.TryGetValue(date, out var bt) && !string.IsNullOrWhiteSpace(bt)
                ? $"🚫 {bt}"
                : "🚫 Blackout";
            stack.Children.Add(new Label
            {
                Text = boTitle,
                FontSize = 9,
                TextColor = BlackoutColor,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
            });
        }

        // Bank holiday marker
        if (isBankHoliday)
        {
            var holName = _bankHolidays.TryGetValue(date, out var hName) ? hName : "Bank Hol";
            var shortName = holName.Length > 12 ? holName[..12] + "…" : holName;
            stack.Children.Add(new Label
            {
                Text = $"🏦 {shortName}",
                FontSize = 9,
                TextColor = BankHolidayColor,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1
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
            var maxEvents = CalSettings.MonthMaxEventsPerCell;
            var dayEvents = _calendarEvents.Where(e => e.Date == date).ToList();
            foreach (var evt in dayEvents.Take(maxEvents))
            {
                var isCompEvt = evt.Category == CalendarEventCategory.Competition;
                stack.Children.Add(new Label
                {
                    Text = $"{(isCompEvt ? "🏆" : "📌")} {evt.Title}",
                    FontSize = CalSettings.MonthLabelFontSize,
                    TextColor = isCompEvt ? CompetitionColor : CustomEventColor,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1
                });
            }
            if (dayEvents.Count > maxEvents)
            {
                stack.Children.Add(new Label
                {
                    Text = $"+{dayEvents.Count - maxEvents} more",
                    FontSize = CalSettings.MonthLabelFontSize,
                    TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                        ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280")
                });
            }
        }

        // Fixtures
        if (_showLeagueMatches && dayFixtures != null)
        {
            int maxShow = CalSettings.MonthMaxFixturesPerCell;
            int shown = 0;
            foreach (var f in dayFixtures.OrderBy(f => f.Date))
            {
                if (shown >= maxShow)
                {
                    stack.Children.Add(new Label
                    {
                        Text = $"+{dayFixtures.Count - maxShow} more",
                        FontSize = CalSettings.MonthLabelFontSize,
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
                    FontSize = CalSettings.MonthLabelFontSize,
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
            MinimumHeightRequest = CalSettings.MonthCellMinHeight
        };

        // Hover tooltip preview
        var tooltip = BuildTooltipText(date, fixturesByDate, season);
        if (!string.IsNullOrEmpty(tooltip))
            ToolTipProperties.SetText(border, tooltip);

        // Tap to show day options
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            var dayEvents = _calendarEvents.Where(e => e.Date == date).ToList();
            var options = new List<string> { "\U0001f4c5 View Day", "\U0001f4cc Add Event" };
            foreach (var evt in dayEvents.Take(3))
                options.Add($"\u270f\ufe0f {evt.Title}");

            var action = await DisplayActionSheet(
                date.ToString("ddd dd MMM yyyy"), "Cancel", null, options.ToArray());

            if (action == "\U0001f4c5 View Day")
            {
                _viewDate = date;
                SwitchView(CalendarView.Day);
            }
            else if (action == "\U0001f4cc Add Event")
            {
                await ShowAddEventDialog(date);
            }
            else if (action != null && action.StartsWith("\u270f\ufe0f "))
            {
                var title = action[3..];
                var evt = dayEvents.FirstOrDefault(e => e.Title == title);
                if (evt != null) await ShowEditEventDialog(evt);
            }
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

        // ── Day-of-week highlight selector ──
        var highlightBar = new HorizontalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };
        highlightBar.Children.Add(new Label
        {
            Text = "Highlight:",
            FontSize = 11,
            TextColor = isDark ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280"),
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 4, 0)
        });

        string[] dowNames = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
        DayOfWeek[] dowValues = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                                  DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

        for (int i = 0; i < 7; i++)
        {
            var dow = dowValues[i];
            bool isActive = _highlightDayOfWeek == dow;
            var btn = new Button
            {
                Text = dowNames[i],
                FontSize = 10,
                Padding = new Thickness(8, 4),
                CornerRadius = 6,
                MinimumHeightRequest = 28,
                MinimumWidthRequest = 40,
                BackgroundColor = isActive
                    ? Color.FromArgb("#3B82F6")
                    : isDark ? Color.FromArgb("#1F2937") : Color.FromArgb("#F1F5F9"),
                TextColor = isActive
                    ? Colors.White
                    : isDark ? Color.FromArgb("#D1D5DB") : Color.FromArgb("#374151"),
                BorderWidth = isActive ? 0 : 1,
                BorderColor = isDark ? Color.FromArgb("#374151") : Color.FromArgb("#E2E8F0")
            };
            var capturedDow = dow;
            btn.Clicked += (_, _) =>
            {
                _highlightDayOfWeek = _highlightDayOfWeek == capturedDow ? null : capturedDow;
                Refresh();
            };
            highlightBar.Children.Add(btn);
        }

        // "Clear" button
        if (_highlightDayOfWeek.HasValue)
        {
            var clearBtn = new Button
            {
                Text = "✕",
                FontSize = 10,
                Padding = new Thickness(6, 4),
                CornerRadius = 6,
                MinimumHeightRequest = 28,
                MinimumWidthRequest = 28,
                BackgroundColor = isDark ? Color.FromArgb("#374151") : Color.FromArgb("#E2E8F0"),
                TextColor = isDark ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280")
            };
            clearBtn.Clicked += (_, _) =>
            {
                _highlightDayOfWeek = null;
                Refresh();
            };
            highlightBar.Children.Add(clearBtn);
        }

        // ── Zoom controls ──
        var zoomBar = new HorizontalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };
        zoomBar.Children.Add(new Label
        {
            Text = "Zoom:",
            FontSize = 11,
            TextColor = isDark ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280"),
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 4, 0)
        });

        var zoomOutBtn = new Button
        {
            Text = "−",
            FontSize = 14,
            Padding = new Thickness(8, 2),
            CornerRadius = 6,
            MinimumHeightRequest = 28,
            MinimumWidthRequest = 32,
            BackgroundColor = isDark ? Color.FromArgb("#1F2937") : Color.FromArgb("#F1F5F9"),
            TextColor = isDark ? Color.FromArgb("#D1D5DB") : Color.FromArgb("#374151"),
            BorderWidth = 1,
            BorderColor = isDark ? Color.FromArgb("#374151") : Color.FromArgb("#E2E8F0")
        };
        zoomOutBtn.Clicked += (_, _) =>
        {
            if (_yearZoomLevel > 0.5)
            {
                _yearZoomLevel = Math.Round(_yearZoomLevel - 0.1, 1);
                Refresh();
            }
        };
        zoomBar.Children.Add(zoomOutBtn);

        var zoomLabel = new Label
        {
            Text = $"{(int)(_yearZoomLevel * 100)}%",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            WidthRequest = 40,
            TextColor = isDark ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#374151")
        };
        zoomBar.Children.Add(zoomLabel);

        var zoomInBtn = new Button
        {
            Text = "+",
            FontSize = 14,
            Padding = new Thickness(8, 2),
            CornerRadius = 6,
            MinimumHeightRequest = 28,
            MinimumWidthRequest = 32,
            BackgroundColor = isDark ? Color.FromArgb("#1F2937") : Color.FromArgb("#F1F5F9"),
            TextColor = isDark ? Color.FromArgb("#D1D5DB") : Color.FromArgb("#374151"),
            BorderWidth = 1,
            BorderColor = isDark ? Color.FromArgb("#374151") : Color.FromArgb("#E2E8F0")
        };
        zoomInBtn.Clicked += (_, _) =>
        {
            if (_yearZoomLevel < 2.5)
            {
                _yearZoomLevel = Math.Round(_yearZoomLevel + 0.1, 1);
                Refresh();
            }
        };
        zoomBar.Children.Add(zoomInBtn);

        if (Math.Abs(_yearZoomLevel - 1.0) > 0.01)
        {
            var resetBtn = new Button
            {
                Text = "Reset",
                FontSize = 10,
                Padding = new Thickness(8, 4),
                CornerRadius = 6,
                MinimumHeightRequest = 28,
                BackgroundColor = isDark ? Color.FromArgb("#374151") : Color.FromArgb("#E2E8F0"),
                TextColor = isDark ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280")
            };
            resetBtn.Clicked += (_, _) =>
            {
                _yearZoomLevel = 1.0;
                Refresh();
            };
            zoomBar.Children.Add(resetBtn);
        }

        YearPlannerContainer.Children.Add(highlightBar);
        YearPlannerContainer.Children.Add(zoomBar);

        // Ensure bank holidays cover the displayed year
        if (!_bankHolidays.Keys.Any(h => h.Year == _viewDate.Year))
        {
            foreach (var kv in GetPresetHolidays(_viewDate.Year, _viewDate.Year))
                _bankHolidays.TryAdd(kv.Key, kv.Value);
        }

        var fixturesByDate = FilteredFixtures()
            .GroupBy(f => f.Date.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        // ── Compute zoomed dimensions ──
        double zCellW = CalSettings.YearCellWidth * _yearZoomLevel;
        double zRowH = CalSettings.YearRowHeight * _yearZoomLevel;
        double zMonthLabelW = CalSettings.YearMonthLabelWidth * _yearZoomLevel;
        double zHeaderH = 28 * _yearZoomLevel;
        double zDowColW = 24 * _yearZoomLevel;

        // ── Build the grid: 13 rows (header + 12 months), 33 cols (month label + day-of-week + days 1-31) ──
        var colDefs = new ColumnDefinitionCollection();
        colDefs.Add(new ColumnDefinition(new GridLength(zMonthLabelW)));  // Month name
        colDefs.Add(new ColumnDefinition(new GridLength(zDowColW)));  // Day-of-week spacer column
        for (int d = 0; d < 31; d++)
            colDefs.Add(new ColumnDefinition(new GridLength(zCellW))); // Day cells

        var rowDefs = new RowDefinitionCollection();
        rowDefs.Add(new RowDefinition(new GridLength(zHeaderH))); // Header row
        for (int m = 0; m < 12; m++)
            rowDefs.Add(new RowDefinition(new GridLength(zRowH))); // Month rows

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
            FontSize = 11 * _yearZoomLevel,
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
                FontSize = 10 * _yearZoomLevel,
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
                FontSize = 12 * _yearZoomLevel,
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
                FontSize = 9 * _yearZoomLevel,
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
                    fixturesByDate, season, isDark, _yearZoomLevel);
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
            var bankHols = _bankHolidays.Keys.Count(h => h.Year == _viewDate.Year);
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

        YearPlannerContainer.Children.Insert(2, grid);
    }

    private View BuildWallPlannerCell(int year, int month, int day, int daysInMonth,
        Dictionary<DateTime, List<Fixture>> fixturesByDate, Season? season, bool isDark, double zoom = 1.0)
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
        bool isBlackout = _showBlackouts && _blackoutDates.ContainsKey(date);
        bool isInSeason = season != null && date >= season.StartDate.Date && date <= season.EndDate.Date;
        bool isSeasonBound = season != null && (date == season.StartDate.Date || date == season.EndDate.Date);
        bool isMatchDay = _showLeagueMatches && season != null && date.DayOfWeek == season.MatchDayOfWeek && isInSeason;
        bool isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        bool isCompetition = _showCompetitions && _competitionDateSet.Contains(date);
        bool isBankHoliday = _showBankHolidays && _bankHolidays.ContainsKey(date);
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
        var showCount = CalSettings.YearShowFixtureCount && hasFixtures;
        var dayText = showCount ? $"{dayFixtures!.Count}" : "";
        cellStack.Children.Add(new Label
        {
            Text = dayText,
            FontSize = showCount ? 11 * zoom : 0,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            TextColor = textColor,
            HeightRequest = showCount ? 14 * zoom : 0
        });

        // Dot indicators row
        double dotSize = 6 * zoom;
        double dotRadius = 3 * zoom;
        bool hasDots = CalSettings.YearShowDots && (hasFixtures || isBlackout || isSeasonBound || isCompetition || isBankHoliday || isTransferWindow || hasCustomEvent);
        if (hasDots)
        {
            var dotRow = new HorizontalStackLayout
            {
                Spacing = 2 * zoom,
                HorizontalOptions = LayoutOptions.Center
            };

            if (isSeasonBound)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = SeasonColor,
                    WidthRequest = dotSize, HeightRequest = dotSize, CornerRadius = dotRadius
                });
            }

            if (isBlackout)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = BlackoutColor,
                    WidthRequest = dotSize, HeightRequest = dotSize, CornerRadius = dotRadius
                });
            }

            if (hasResults)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = ResultColor,
                    WidthRequest = dotSize, HeightRequest = dotSize, CornerRadius = dotRadius
                });
            }
            else if (hasFixtures)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = FixtureColor,
                    WidthRequest = dotSize, HeightRequest = dotSize, CornerRadius = dotRadius
                });
            }

            if (isCompetition)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = CompetitionColor,
                    WidthRequest = dotSize, HeightRequest = dotSize, CornerRadius = dotRadius
                });
            }

            if (isBankHoliday)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = BankHolidayColor,
                    WidthRequest = dotSize, HeightRequest = dotSize, CornerRadius = dotRadius
                });
            }

            if (hasCustomEvent)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = CustomEventColor,
                    WidthRequest = dotSize, HeightRequest = dotSize, CornerRadius = dotRadius
                });
            }

            if (isTransferWindow && !isCompetition && !hasFixtures && !isBlackout && !isSeasonBound && !hasCustomEvent)
            {
                dotRow.Children.Add(new BoxView
                {
                    Color = TransferWindowColor,
                    WidthRequest = dotSize, HeightRequest = dotSize, CornerRadius = dotRadius
                });
            }

            cellStack.Children.Add(dotRow);
        }

        bool isHighlighted = _highlightDayOfWeek.HasValue && date.DayOfWeek == _highlightDayOfWeek.Value;

        // Highlighted cells get a subtle tinted background boost
        var finalBg = isHighlighted && !isToday
            ? BlendHighlight(bgColor, isDark)
            : bgColor;

        var border = new Border
        {
            Content = cellStack,
            BackgroundColor = finalBg,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = isToday ? 4 : isHighlighted ? 3 : 2 },
            Stroke = isToday ? TodayBorder
                : isHighlighted ? Color.FromArgb(isDark ? "#60A5FA" : "#93C5FD")
                : isSeasonBound ? SeasonColor
                : Colors.Transparent,
            StrokeThickness = isToday ? 2 : isHighlighted ? 1.5 : isSeasonBound ? 1 : 0,
            Padding = new Thickness(0, 2 * zoom),
            Shadow = isHighlighted && !isToday
                ? new Shadow
                {
                    Brush = new SolidColorBrush(Color.FromArgb(isDark ? "#3B82F6" : "#60A5FA")),
                    Offset = new Point(0, 0),
                    Radius = 6,
                    Opacity = isDark ? 0.5f : 0.35f
                }
                : null
        };

        // Hover tooltip preview
        var tooltip = BuildTooltipText(date, fixturesByDate, season);
        if (!string.IsNullOrEmpty(tooltip))
            ToolTipProperties.SetText(border, tooltip);

        // Tap to show day options
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            var dayEvts = _calendarEvents.Where(e => e.Date == date).ToList();
            var options = new List<string> { "\U0001f4c5 View Day", "\U0001f4cc Add Event" };
            foreach (var evt in dayEvts.Take(3))
                options.Add($"\u270f\ufe0f {evt.Title}");

            var action = await DisplayActionSheet(
                date.ToString("ddd dd MMM yyyy"), "Cancel", null, options.ToArray());

            if (action == "\U0001f4c5 View Day")
            {
                _viewDate = date;
                SwitchView(CalendarView.Day);
            }
            else if (action == "\U0001f4cc Add Event")
            {
                await ShowAddEventDialog(date);
            }
            else if (action != null && action.StartsWith("\u270f\ufe0f "))
            {
                var title = action[3..];
                var evt = dayEvts.FirstOrDefault(e => e.Title == title);
                if (evt != null) await ShowEditEventDialog(evt);
            }
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

        if (_showBlackouts && _blackoutDates.ContainsKey(date))
        {
            var boTitle = _blackoutDates.TryGetValue(date, out var bt) && !string.IsNullOrWhiteSpace(bt)
                ? $"🚫 {bt}"
                : "Blackout Date";
            badgeRow.Children.Add(MakeBadge(boTitle, "#EF4444", "#FEF2F2"));
        }

        if (_showBankHolidays && _bankHolidays.ContainsKey(date))
        {
            var holName = _bankHolidays.TryGetValue(date, out var hName) ? hName : "Bank Holiday";
            badgeRow.Children.Add(MakeBadge($"🏦 {holName}", "#EC4899", "#FDF2F8"));
        }

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

        // "Add Event" button for this day
        if (CalSettings.DayShowAddEventButton)
        {
            var addEventBtn = new Button
            {
                Text = "\U0001f4cc Add Event for " + date.ToString("dd MMM"),
                BackgroundColor = CustomEventColor,
                TextColor = Colors.White,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                Padding = new Thickness(14, 6),
                CornerRadius = 8,
                HorizontalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 4, 0, 4)
            };
            addEventBtn.Clicked += async (_, _) => await ShowAddEventDialog(date);
            DayContent.Children.Add(addEventBtn);
        }

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
                    var isCompEvt = evt.Category == CalendarEventCategory.Competition;
                    var evtCard = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(16, 12) };
                    var evtTopRow = new HorizontalStackLayout { Spacing = 8 };
                    evtTopRow.Children.Add(MakeBadge(evt.Category.ToString(),
                        isCompEvt ? "#F97316" : "#14B8A6",
                        isCompEvt ? "#FFF7ED" : "#F0FDFA"));
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

                    // Show linked competition name
                    if (evt.CompetitionId.HasValue)
                    {
                        var linkedComp = League.Competitions.FirstOrDefault(c => c.Id == evt.CompetitionId.Value);
                        if (linkedComp != null)
                        {
                            evtCard.Children.Add(new Label
                            {
                                Text = $"🏆 {linkedComp.Name}",
                                FontSize = 13,
                                TextColor = CompetitionColor
                            });
                        }
                    }

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
                        Stroke = isCompEvt ? CompetitionColor : CustomEventColor,
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
            if (CalSettings.DayShowDivision && div != null && div.Id != Guid.Empty)
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
            if (CalSettings.DayShowVenue && venue != null)
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
            if (CalSettings.DayShowFrameCount && hasResult && f.Frames.Count > 0)
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

    private string BuildTooltipText(DateTime date, Dictionary<DateTime, List<Fixture>> fixturesByDate, Season? season)
    {
        var lines = new List<string> { date.ToString("dddd, dd MMMM yyyy") };

        // Season info
        if (season != null)
        {
            if (date == season.StartDate.Date)
                lines.Add("▶ Season Start");
            else if (date == season.EndDate.Date)
                lines.Add("◀ Season End");
        }

        // Blackout
        if (_showBlackouts && _blackoutDates.TryGetValue(date, out var boTitle))
            lines.Add(!string.IsNullOrWhiteSpace(boTitle) ? $"🚫 {boTitle}" : "🚫 Blackout");

        // Bank holiday
        if (_showBankHolidays && _bankHolidays.TryGetValue(date, out var holName))
            lines.Add($"🏦 {holName}");

        // Competition
        if (_showCompetitions && _competitionDateSet.Contains(date))
        {
            foreach (var comp in _competitionDates.Where(c => c.Date == date))
                lines.Add($"🏆 {comp.Name}");
        }

        // Transfer window
        if (_showTransferWindow && season != null
            && season.TransferWindowStart.HasValue && season.TransferWindowEnd.HasValue
            && date >= season.TransferWindowStart.Value.Date && date <= season.TransferWindowEnd.Value.Date)
        {
            if (date == season.TransferWindowStart.Value.Date)
                lines.Add("🔄 Transfer Window Opens");
            else if (date == season.TransferWindowEnd.Value.Date)
                lines.Add("🔄 Transfer Window Closes");
            else
                lines.Add("🔄 Transfer Window");
        }

        // Custom events
        if (_showCustomEvents)
        {
            foreach (var evt in _calendarEvents.Where(e => e.Date == date))
                lines.Add($"📌 {evt.Title}");
        }

        // Fixtures
        if (_showLeagueMatches && fixturesByDate.TryGetValue(date, out var dayFixtures))
        {
            if (dayFixtures.Count > 0)
                lines.Add($"— {dayFixtures.Count} fixture{(dayFixtures.Count != 1 ? "s" : "")} —");

            foreach (var f in dayFixtures.OrderBy(f => f.Date).Take(6))
            {
                var home = _seasonTeams.FirstOrDefault(t => t.Id == f.HomeTeamId)?.Name ?? "?";
                var away = _seasonTeams.FirstOrDefault(t => t.Id == f.AwayTeamId)?.Name ?? "?";
                bool hasResult = f.Frames.Count > 0;
                var matchLine = hasResult
                    ? $"{home} {f.HomeScore}-{f.AwayScore} {away}"
                    : $"{home} v {away}";

                var venue = _seasonVenues.FirstOrDefault(v => v.Id == f.VenueId);
                if (venue != null)
                    matchLine += $"  📍 {venue.Name}";

                lines.Add(matchLine);
            }

            if (dayFixtures.Count > 6)
                lines.Add($"+{dayFixtures.Count - 6} more...");
        }

        // Only return tooltip if there's something beyond the date header
        return lines.Count > 1 ? string.Join("\n", lines) : string.Empty;
    }

    // ────────────────────── Preset Holidays ──────────────────────

    private Dictionary<DateTime, string> GetPresetHolidays(int startYear, int endYear)
    {
        var holidays = new Dictionary<DateTime, string>();
        var presets = CalSettings.PresetHolidays;

        // Seed defaults if the list is empty (first launch or after clearing)
        if (presets.Count == 0)
        {
            presets.AddRange(PresetHoliday.CreateDefaults());
            DataStore.Save();
        }

        for (int y = startYear; y <= endYear; y++)
        {
            foreach (var p in presets.Where(h => h.IsEnabled))
            {
                var date = ResolveHolidayDate(p, y);
                if (date.HasValue)
                    holidays.TryAdd(date.Value, p.Name);
            }
        }
        return holidays;
    }

    /// <summary>Resolves the actual date of a preset holiday for a given year.</summary>
    private static DateTime? ResolveHolidayDate(PresetHoliday holiday, int year)
    {
        if (holiday.IsBuiltIn)
        {
            return holiday.Rule switch
            {
                "new-year" => SubstituteWeekend(new DateTime(year, 1, 1)),
                "good-friday" => CalculateEaster(year).AddDays(-2),
                "easter-monday" => CalculateEaster(year).AddDays(1),
                "early-may" => FirstMonday(year, 5),
                "spring-bank" => LastMonday(year, 5),
                "summer-bank" => LastMonday(year, 8),
                "christmas" => ResolveChristmas(year),
                "boxing-day" => ResolveBoxingDay(year),
                _ => null
            };
        }

        // Custom fixed-date holiday
        if (holiday.FixedMonth >= 1 && holiday.FixedMonth <= 12
            && holiday.FixedDay >= 1 && holiday.FixedDay <= DateTime.DaysInMonth(year, holiday.FixedMonth))
        {
            return new DateTime(year, holiday.FixedMonth, holiday.FixedDay);
        }

        return null;
    }

    private static DateTime ResolveChristmas(int year)
    {
        var xmas = new DateTime(year, 12, 25);
        return xmas.DayOfWeek switch
        {
            DayOfWeek.Saturday => new DateTime(year, 12, 27),
            DayOfWeek.Sunday => new DateTime(year, 12, 27),
            _ => xmas
        };
    }

    private static DateTime ResolveBoxingDay(int year)
    {
        var xmas = new DateTime(year, 12, 25);
        var boxing = new DateTime(year, 12, 26);
        return xmas.DayOfWeek switch
        {
            DayOfWeek.Saturday => new DateTime(year, 12, 28),
            DayOfWeek.Sunday => boxing,
            _ => SubstituteWeekend(boxing)
        };
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

    /// <summary>
    /// Blends a subtle blue highlight tint into the given background colour
    /// to create the "glow" effect for day-of-week highlighting.
    /// </summary>
    private static Color BlendHighlight(Color bg, bool isDark)
    {
        // Mix ~15% blue highlight into the existing background
        const float factor = 0.15f;
        var highlight = isDark
            ? Color.FromArgb("#1E3A5F")  // dark blue tint
            : Color.FromArgb("#DBEAFE"); // light blue tint

        float r = bg.Red + (highlight.Red - bg.Red) * factor;
        float g = bg.Green + (highlight.Green - bg.Green) * factor;
        float b = bg.Blue + (highlight.Blue - bg.Blue) * factor;

        return new Color(
            Math.Clamp(r, 0f, 1f),
            Math.Clamp(g, 0f, 1f),
            Math.Clamp(b, 0f, 1f));
    }
}
