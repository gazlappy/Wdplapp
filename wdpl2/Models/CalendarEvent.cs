namespace Wdpl2.Models;

/// <summary>
/// A user-created calendar event (meeting, social, deadline, etc.).
/// </summary>
public sealed class CalendarEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The date of the event (date-only).</summary>
    public DateTime Date { get; set; } = DateTime.Today;

    /// <summary>Short title shown on the calendar.</summary>
    public string Title { get; set; } = "";

    /// <summary>Optional longer description.</summary>
    public string? Notes { get; set; }

    /// <summary>Category for colour-coding and filtering.</summary>
    public CalendarEventCategory Category { get; set; } = CalendarEventCategory.General;

    /// <summary>When this record was created.</summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Pre-defined categories for calendar events.</summary>
public enum CalendarEventCategory
{
    General,
    Meeting,
    Social,
    Deadline,
    Training,
    Other
}
