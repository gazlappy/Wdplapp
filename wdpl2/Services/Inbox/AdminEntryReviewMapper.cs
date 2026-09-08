using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wdpl2.Models;

namespace Wdpl2.Services.Inbox;

/// <summary>Prepares review metadata only; never changes answers or creates registrations.</summary>
public static class AdminEntryReviewMapper
{
    public static JsonElement ReviewValues(JsonElement payload)
    {
        var status = payload.GetProperty("status").GetString();
        var notes = payload.GetProperty("notes").GetString();
        if (status is not ("pending" or "confirmed" or "rejected") || notes == null || Encoding.UTF8.GetByteCount(notes) > 16000)
            throw new InvalidOperationException("Invalid entry status or review notes.");
        return JsonSerializer.SerializeToElement(new { status, notes });
    }

    public static EntryFormSubmission PrepareApplication(LeagueData league, string backendId, AdminSyncReviewItem item)
    {
        if (item.ResolutionRequestId == Guid.Empty || item.Change.Revision < 1 ||
            item.ResolutionMode is not ("server" or "local") || item.ResolutionPayload is not { } payload ||
            item.LocalSnapshot is not { } snapshot)
            throw new InvalidOperationException("Save an explicit review choice and local snapshot before applying it.");
        var values = ReviewValues(payload);
        var before = JsonSerializer.Deserialize<EntryFormSubmission>(snapshot.GetRawText())
            ?? throw new InvalidOperationException("Missing reviewed local entry.");
        var expectedValues = item.ResolutionMode == "server" ? ReviewValues(item.Change.Payload) :
            JsonSerializer.SerializeToElement(new { status = before.Status, notes = before.Notes });
        if (!JsonElement.DeepEquals(values, expectedValues))
            throw new InvalidOperationException("The saved decision differs from the reviewed values.");
        var current = FindMapped(league, backendId, item.Change);
        var draft = JsonSerializer.Deserialize<EntryFormSubmission>(snapshot.GetRawText())!;
        draft.Status = values.GetProperty("status").GetString()!;
        draft.Notes = values.GetProperty("notes").GetString()!;
        draft.SourceReviewRequestId = item.ResolutionRequestId;
        draft.SourceReviewIntentHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            backendId, item.Change, item.ResolutionMode, values, snapshot
        })));
        var now = JsonSerializer.SerializeToElement(current);
        var retry = current.SourceReviewRequestId == item.ResolutionRequestId;
        if (!JsonElement.DeepEquals(now, retry ? JsonSerializer.SerializeToElement(draft) : snapshot))
            throw new InvalidOperationException("The local entry changed after review or application. Compare it again before continuing.");
        return draft;
    }

    public static EntryFormSubmission FindMapped(LeagueData league, string backendId, AdminSyncChange change)
    {
        if (change.Kind != "entry_review" || !Guid.TryParse(backendId, out _) ||
            !long.TryParse(change.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) || sequence < 1)
            throw new InvalidOperationException("Invalid hosted entry review identity.");
        var payload = change.Payload;
        var formId = payload.GetProperty("formId").GetString();
        if (formId == null || !formId.StartsWith("form-", StringComparison.Ordinal) ||
            !Guid.TryParseExact(formId[5..], "N", out var id) || payload.GetProperty("submissionSequence").GetInt64() != sequence)
            throw new InvalidOperationException("The review does not identify a valid form submission.");
        var clientId = payload.GetProperty("clientId").GetString();
        if (string.IsNullOrEmpty(clientId)) throw new InvalidOperationException("A source client identity is required.");
        var form = league.WebsiteSettings.EntryForms.SingleOrDefault(f => f.Id == id)
            ?? throw new InvalidOperationException("The source form is not present locally.");
        var matches = form.Submissions.Where(s => s.SourceBackendId == backendId && s.SourceClientId == clientId &&
            s.SourceSubmissionSequence == sequence).ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException("Link this server submission to exactly one local entry after review. Names and answers are never used to guess identity.");
        var entry = matches[0];
        if (entry.LinkedTeamId is { } teamId)
        {
            var team = league.Teams.SingleOrDefault(t => t.Id == teamId);
            if (team?.SeasonId is not { } seasonId || !league.Seasons.Any(s => s.Id == seasonId) || league.IsSeasonLocked(seasonId))
                throw new InvalidOperationException("The linked team's season is missing or locked.");
        }
        return entry;
    }

    public static EntryFormSubmission CreateReviewedDraft(LeagueData league, string backendId, AdminSyncChange change, JsonElement expectedLocal)
    {
        var entry = FindMapped(league, backendId, change);
        if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(entry), expectedLocal))
            throw new InvalidOperationException("The local entry changed after review. Compare it again.");
        var status = change.Payload.GetProperty("status").GetString();
        var notes = change.Payload.GetProperty("notes").GetString();
        if (status is not ("pending" or "confirmed" or "rejected") || notes == null || Encoding.UTF8.GetByteCount(notes) > 16000)
            throw new InvalidOperationException("Invalid entry status or review notes.");
        var draft = JsonSerializer.Deserialize<EntryFormSubmission>(expectedLocal.GetRawText())!;
        draft.Status = status;
        draft.Notes = notes;
        return draft;
    }
}
