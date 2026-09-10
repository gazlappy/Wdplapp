using System.Text.Json;
using Wdpl2.Models;

namespace Wdpl2.Services.Inbox;

public static class AdminReviewSnapshot
{
    public static JsonElement Capture(LeagueData league, string backendId, AdminSyncReviewItem item)
    {
        if (item.ResolutionMode != null || item.ResolutionPayload != null)
            throw new InvalidOperationException("A decision already started. Its snapshot cannot be replaced.");
        JsonElement snapshot;
        if (item.Change.Kind == "entry_review")
            snapshot = JsonSerializer.SerializeToElement(AdminEntryReviewMapper.FindMapped(league, backendId, item.Change));
        else if (item.Change.Kind == "scorecard" && Guid.TryParse(item.Change.Id, out var id) &&
            Guid.TryParse(item.Change.SeasonId, out var seasonId))
        {
            var fixture = league.Fixtures.SingleOrDefault(f => f.Id == id && f.SeasonId == seasonId)
                ?? throw new InvalidOperationException("The fixture is missing from the identified season.");
            if (!league.Seasons.Any(s => s.Id == seasonId) || league.IsSeasonLocked(seasonId))
                throw new InvalidOperationException("The fixture season is missing or locked.");
            snapshot = JsonSerializer.SerializeToElement(fixture);
        }
        else throw new InvalidOperationException("The review has no supported explicit local identity.");
        ValidateIdentity(backendId, item, snapshot);
        return snapshot;
    }

    public static void ValidateIdentity(string backendId, AdminSyncReviewItem item, JsonElement snapshot)
    {
        if (!Guid.TryParse(backendId, out _) || snapshot.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("A valid backend and local snapshot are required.");
        if (item.Change.Kind == "entry_review")
        {
            var entry = snapshot.Deserialize<EntryFormSubmission>()!;
            if (entry.Id == Guid.Empty || entry.SourceBackendId != backendId ||
                entry.SourceClientId != item.Change.Payload.GetProperty("clientId").GetString() ||
                entry.SourceSubmissionSequence != item.Change.Payload.GetProperty("submissionSequence").GetInt64() ||
                entry.SourceSubmissionSequence?.ToString(System.Globalization.CultureInfo.InvariantCulture) != item.Change.Id ||
                (item.LocalSnapshot is { } old && old.Deserialize<EntryFormSubmission>()!.Id != entry.Id))
                throw new InvalidOperationException("Refreshing cannot change the linked entry identity.");
        }
        else if (item.Change.Kind == "scorecard")
        {
            var fixture = snapshot.Deserialize<Fixture>()!;
            if (!Guid.TryParse(item.Change.Id, out var id) || fixture.Id != id ||
                !Guid.TryParse(item.Change.SeasonId, out var season) || fixture.SeasonId != season)
                throw new InvalidOperationException("Refreshing cannot change fixture or season identity.");
        }
        else throw new InvalidOperationException("Unsupported review type.");
    }
}
