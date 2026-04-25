namespace Wdpl2.Models
{
    /// <summary>
    /// Tracks a player's availability for a specific match date.
    /// </summary>
    public sealed class PlayerAvailability
    {
        public DateTime Date { get; set; }
        public AvailabilityStatus Status { get; set; } = AvailabilityStatus.Unknown;
        public string? Reason { get; set; }
    }

    public enum AvailabilityStatus
    {
        Unknown,
        Available,
        Unavailable,
        Maybe
    }
}
