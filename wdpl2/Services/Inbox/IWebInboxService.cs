namespace Wdpl2.Services.Inbox;

public interface IWebInboxService
{
    Task<IReadOnlyList<WebSubmission>> GetPendingAsync(CancellationToken ct = default);
    Task MarkProcessedAsync(IEnumerable<long> ids, string? by = null, string? notes = null, CancellationToken ct = default);

    /// <summary>
    /// Clears finalization stamps on the live scorecard for <paramref name="fixtureId"/>
    /// (so it reappears in the captain portal) and marks any currently-pending
    /// match_result submissions for that fixture as processed with a "reopened" note.
    /// </summary>
    Task ReopenFixtureAsync(Guid fixtureId, string? by = null, string? notes = null, CancellationToken ct = default);
}
