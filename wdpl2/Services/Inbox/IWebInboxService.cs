namespace Wdpl2.Services.Inbox;

public interface IWebInboxService
{
    Task<IReadOnlyList<WebSubmission>> GetPendingAsync(CancellationToken ct = default);
    Task MarkProcessedAsync(IEnumerable<long> ids, string? by = null, string? notes = null, CancellationToken ct = default);
}
