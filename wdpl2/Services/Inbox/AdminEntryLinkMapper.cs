using System.Text.Json;
using Wdpl2.Models;

namespace Wdpl2.Services.Inbox;

public static class AdminEntryLinkMapper
{
    public static EntryForm SourceForm(LeagueData league, AdminSyncChange change)
    {
        var formId = change.Payload.GetProperty("formId").GetString();
        if (change.Kind != "entry_review" || formId == null || !formId.StartsWith("form-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(formId[5..], "N", out var id))
            throw new InvalidOperationException("The review must identify a local form explicitly.");
        return league.WebsiteSettings.EntryForms.SingleOrDefault(f => f.Id == id)
            ?? throw new InvalidOperationException("The source form is missing locally. Restore it before linking.");
    }

    public static EntryFormSubmission Prepare(LeagueData league, string backend, AdminSyncChange change, Guid localId, JsonElement expected)
    {
        var copy = JsonSerializer.Deserialize<LeagueData>(JsonSerializer.Serialize(league))!;
        var form = SourceForm(copy, change);
        var entry = form.Submissions.SingleOrDefault(s => s.Id == localId)
            ?? throw new InvalidOperationException("Choose an existing entry in the identified form.");
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(entry), expected))
            throw new InvalidOperationException("The selected entry changed. Compare it again before linking.");
        var client = change.Payload.GetProperty("clientId").GetString();
        var sequence = change.Payload.GetProperty("submissionSequence").GetInt64();
        var already = entry.SourceBackendId == backend && entry.SourceClientId == client && entry.SourceSubmissionSequence == sequence;
        if (!already && (entry.SourceBackendId != null || entry.SourceClientId != null || entry.SourceSubmissionSequence != null ||
            entry.SourceReviewRequestId != null || entry.SourceReviewIntentHash != null))
            throw new InvalidOperationException("This entry already has source metadata. It cannot be reassigned by linking.");
        if (copy.WebsiteSettings.EntryForms.SelectMany(f => f.Submissions).Any(s => !ReferenceEquals(s, entry) &&
            s.SourceBackendId == backend && (s.SourceSubmissionSequence == sequence ||
                (form.Submissions.Contains(s) && s.SourceClientId == client))))
            throw new InvalidOperationException("Another entry already uses this hosted submission identity.");
        entry.SourceBackendId = backend;
        entry.SourceClientId = client;
        entry.SourceSubmissionSequence = sequence;
        return AdminEntryReviewMapper.FindMapped(copy, backend, change);
    }
}
