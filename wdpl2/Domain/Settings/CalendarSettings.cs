namespace Wdpl2.Models;

/// <summary>
/// Persisted settings that control the appearance and behaviour of the Calendar page.
/// Stored on <see cref="LeagueData.CalendarSettings"/>.
/// </summary>
public sealed class CalendarSettings
{
    // ── Default View ──
    /// <summary>Which view to show when the calendar first opens (Month, Year, Day).</summary>
    public string DefaultView { get; set; } = "Month";

    /// <summary>First day of the week: 0 = Sunday, 1 = Monday.</summary>
    public int WeekStartDay { get; set; } = 1; // Monday

    // ── Default Filter States ──
    public bool ShowLeagueMatches { get; set; } = true;
    public bool ShowCompetitions { get; set; } = true;
    public bool ShowBankHolidays { get; set; } = true;
    public bool ShowBlackouts { get; set; } = true;
    public bool ShowTransferWindow { get; set; } = true;
    public bool ShowCustomEvents { get; set; } = true;

    // ── Colors (hex) ──
    public string FixtureColor { get; set; } = "#3B82F6";
    public string ResultColor { get; set; } = "#10B981";
    public string BlackoutColor { get; set; } = "#EF4444";
    public string SeasonColor { get; set; } = "#8B5CF6";
    public string TodayColor { get; set; } = "#F59E0B";
    public string CompetitionColor { get; set; } = "#F97316";
    public string BankHolidayColor { get; set; } = "#EC4899";
    public string TransferWindowColor { get; set; } = "#06B6D4";
    public string CustomEventColor { get; set; } = "#14B8A6";

    // ── Month View ──
    /// <summary>Minimum cell height in the month grid (pixels).</summary>
    public int MonthCellMinHeight { get; set; } = 80;

    /// <summary>Maximum fixture labels to show per month cell.</summary>
    public int MonthMaxFixturesPerCell { get; set; } = 3;

    /// <summary>Maximum event labels to show per month cell.</summary>
    public int MonthMaxEventsPerCell { get; set; } = 2;

    /// <summary>Font size for day numbers in month view.</summary>
    public int MonthDayFontSize { get; set; } = 14;

    /// <summary>Font size for fixture/event labels in month cells.</summary>
    public int MonthLabelFontSize { get; set; } = 9;

    // ── Year / Wall Planner View ──
    /// <summary>Width of each day cell in the wall planner (pixels).</summary>
    public int YearCellWidth { get; set; } = 42;

    /// <summary>Height of each month row in the wall planner (pixels).</summary>
    public int YearRowHeight { get; set; } = 38;

    /// <summary>Width of the month-name column (pixels).</summary>
    public int YearMonthLabelWidth { get; set; } = 80;

    /// <summary>Show dot indicators on wall planner cells.</summary>
    public bool YearShowDots { get; set; } = true;

    /// <summary>Show fixture count number on wall planner cells.</summary>
    public bool YearShowFixtureCount { get; set; } = true;

    // ── Day View ──
    /// <summary>Show venue on fixture cards in day view.</summary>
    public bool DayShowVenue { get; set; } = true;

    /// <summary>Show frame count on played fixture cards.</summary>
    public bool DayShowFrameCount { get; set; } = true;

    /// <summary>Show division badge on fixture cards.</summary>
    public bool DayShowDivision { get; set; } = true;

    /// <summary>Show the "Add Event" button in day view.</summary>
    public bool DayShowAddEventButton { get; set; } = true;

    // ── Legend ──
    /// <summary>Show the colour legend overlay at the bottom of the calendar.</summary>
    public bool ShowLegend { get; set; } = true;

    // ── Events ──
    /// <summary>Default category for new events.</summary>
    public string DefaultEventCategory { get; set; } = "General";

    // ── Preset Holidays ──
    /// <summary>Customisable list of preset holidays (bank holidays, etc.).</summary>
    public List<PresetHoliday> PresetHolidays { get; set; } = PresetHoliday.CreateDefaults();
}
