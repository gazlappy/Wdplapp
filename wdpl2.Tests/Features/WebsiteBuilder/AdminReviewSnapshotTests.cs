using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services.Inbox;

namespace wdpl2.Tests;

public class AdminReviewSnapshotTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Capture_UsesCurrentFixtureAndRejectsLockedSeason(bool locked)
    {
        var season = new Season { IsLocked = locked };
        var fixture = new Fixture { SeasonId = season.Id };
        var league = new LeagueData { Seasons = [season], Fixtures = [fixture] };
        var item = new AdminSyncReviewItem
        {
            Change = new(1, "scorecard", fixture.Id.ToString(), 1, season.Id.ToString(), "web", JsonSerializer.SerializeToElement(new { })),
            LocalSnapshot = JsonSerializer.SerializeToElement(fixture)
        };
        var backend = Guid.NewGuid().ToString();
        if (locked) Assert.Throws<InvalidOperationException>(() => AdminReviewSnapshot.Capture(league, backend, item));
        else Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(fixture), AdminReviewSnapshot.Capture(league, backend, item)));
        item.ResolutionMode = "server";
        Assert.Throws<InvalidOperationException>(() => AdminReviewSnapshot.Capture(league, backend, item));
    }

    [Fact]
    public void Validate_RejectsChangedEntryIdentity()
    {
        var backend = Guid.NewGuid().ToString();
        var entry = new EntryFormSubmission { SourceBackendId = backend, SourceClientId = "client-1", SourceSubmissionSequence = 7 };
        var item = new AdminSyncReviewItem
        {
            Change = new(1, "entry_review", "7", 1, null, "web", JsonSerializer.SerializeToElement(new { clientId = "client-1", submissionSequence = 7 })),
            LocalSnapshot = JsonSerializer.SerializeToElement(entry)
        };
        entry.Notes = "New local notes";
        AdminReviewSnapshot.ValidateIdentity(backend, item, JsonSerializer.SerializeToElement(entry));
        entry.Id = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => AdminReviewSnapshot.ValidateIdentity(backend, item, JsonSerializer.SerializeToElement(entry)));
    }
}
