namespace Wdpl2.Models;

/// <summary>
/// A customisable preset holiday definition used by the calendar.
/// Built-in entries (UK bank holidays) use a <see cref="Rule"/> to calculate
/// the date each year. Custom entries use <see cref="FixedMonth"/>/<see cref="FixedDay"/>.
/// </summary>
public sealed class PresetHoliday
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Display name shown on the calendar.</summary>
    public string Name { get; set; } = "";

    /// <summary>Whether this holiday is shown on the calendar.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Rule identifier for built-in holidays. Null or empty for custom fixed-date holidays.
    /// Built-in values: "new-year", "good-friday", "easter-monday", "early-may",
    /// "spring-bank", "summer-bank", "christmas", "boxing-day".
    /// </summary>
    public string? Rule { get; set; }

    /// <summary>For custom holidays: month (1–12).</summary>
    public int FixedMonth { get; set; }

    /// <summary>For custom holidays: day (1–31).</summary>
    public int FixedDay { get; set; }

    /// <summary>Whether this was added by the user (vs. built-in).</summary>
    public bool IsCustom { get; set; }

    /// <summary>True when this is a built-in holiday (has a <see cref="Rule"/>).</summary>
    public bool IsBuiltIn => !string.IsNullOrEmpty(Rule);

    /// <summary>
    /// Returns the default UK bank holiday and common observance preset list.
    /// Bank holidays are enabled by default; other observances start disabled.
    /// </summary>
    public static List<PresetHoliday> CreateDefaults() =>
    [
        // England & Wales bank holidays
        new() { Name = "New Year's Day", Rule = "new-year" },
        new() { Name = "Good Friday", Rule = "good-friday" },
        new() { Name = "Easter Monday", Rule = "easter-monday" },
        new() { Name = "Early May Bank Holiday", Rule = "early-may" },
        new() { Name = "Spring Bank Holiday", Rule = "spring-bank" },
        new() { Name = "Summer Bank Holiday", Rule = "summer-bank" },
        new() { Name = "Christmas Day", Rule = "christmas" },
        new() { Name = "Boxing Day", Rule = "boxing-day" },
        // Common observances (off by default — enable as needed)
        new() { Name = "Christmas Eve", Rule = "christmas-eve", IsEnabled = false },
        new() { Name = "New Year's Eve", Rule = "new-years-eve", IsEnabled = false },
        new() { Name = "Easter Sunday", Rule = "easter-sunday", IsEnabled = false },
        new() { Name = "Valentine's Day", Rule = "valentines", IsEnabled = false },
        new() { Name = "Halloween", Rule = "halloween", IsEnabled = false },
        new() { Name = "Bonfire Night", Rule = "bonfire-night", IsEnabled = false },
        new() { Name = "Remembrance Sunday", Rule = "remembrance-sunday", IsEnabled = false },
        new() { Name = "Mother's Day", Rule = "mothers-day", IsEnabled = false },
        new() { Name = "Father's Day", Rule = "fathers-day", IsEnabled = false },
        new() { Name = "St David's Day", Rule = "st-david", IsEnabled = false },
        new() { Name = "St Patrick's Day", Rule = "st-patrick", IsEnabled = false },
        new() { Name = "St George's Day", Rule = "st-george", IsEnabled = false },
        new() { Name = "St Andrew's Day", Rule = "st-andrew", IsEnabled = false },
        new() { Name = "Burns Night", Rule = "burns-night", IsEnabled = false },
    ];

    /// <summary>
    /// Adds any built-in presets that are missing from the supplied list
    /// (e.g. after an app update introduces new rules). Preserves user
    /// enable/disable choices for existing entries. Returns true if anything was added.
    /// </summary>
    public static bool EnsureBuiltIns(List<PresetHoliday> presets)
    {
        bool added = false;
        foreach (var def in CreateDefaults())
        {
            if (!presets.Any(p => string.Equals(p.Rule, def.Rule, StringComparison.OrdinalIgnoreCase)))
            {
                presets.Add(def);
                added = true;
            }
        }
        return added;
    }
}
