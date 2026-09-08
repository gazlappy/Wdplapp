using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services.Inbox;

namespace wdpl2.Tests;

public class AdminEntryReviewMapperTests
{
    [Theory]
    [InlineData("server", "confirmed")]
    [InlineData("local", "pending")]
    public void Application_RetryPreservesAnswersAndRejectsLaterEdits(string mode, string status)
    {
        var backend = Guid.NewGuid().ToString();
        var form = new EntryForm();
        var entry = new EntryFormSubmission
        {
            SourceBackendId = backend, SourceClientId = "client-1", SourceSubmissionSequence = 1,
            FieldValues = new() { ["Answer"] = "Original" }, Notes = "Local notes"
        };
        form.Submissions.Add(entry);
        var league = new LeagueData();
        league.WebsiteSettings.EntryForms.Add(form);
        var change = new AdminSyncChange(1, "entry_review", "1", 1, null, "web",
            JsonSerializer.SerializeToElement(new { submissionSequence = 1, formId = $"form-{form.Id:N}", clientId = "client-1", status = "confirmed", notes = "Reviewed" }));
        var item = new AdminSyncReviewItem
        {
            Change = change, LocalSnapshot = JsonSerializer.SerializeToElement(entry), ResolutionMode = mode,
            ResolutionPayload = mode == "server" ? AdminEntryReviewMapper.ReviewValues(change.Payload) :
                JsonSerializer.SerializeToElement(new { status = entry.Status, notes = entry.Notes })
        };
        var draft = AdminEntryReviewMapper.PrepareApplication(league, backend, item);
        Assert.Equal(status, draft.Status);
        Assert.Equal("Original", draft.FieldValues["Answer"]);
        Assert.Equal("pending", entry.Status);
        Assert.Equal(item.ResolutionRequestId, draft.SourceReviewRequestId);
        Assert.NotEmpty(draft.SourceReviewIntentHash!);
        form.Submissions[0] = JsonSerializer.Deserialize<EntryFormSubmission>(JsonSerializer.Serialize(draft))!;
        var retry = AdminEntryReviewMapper.PrepareApplication(league, backend, item);
        Assert.Equal(draft.SourceReviewIntentHash, retry.SourceReviewIntentHash);
        form.Submissions[0].FieldValues["Answer"] = "Later edit";
        Assert.Throws<InvalidOperationException>(() => AdminEntryReviewMapper.PrepareApplication(league, backend, item));
        form.Submissions[0] = draft;
        item.Change = change with { Revision = 2 };
        Assert.Throws<InvalidOperationException>(() => AdminEntryReviewMapper.PrepareApplication(league, backend, item));
        item.Change = change;
        item.ResolutionPayload = JsonSerializer.SerializeToElement(new { status = "rejected", notes = "Different decision" });
        Assert.Throws<InvalidOperationException>(() => AdminEntryReviewMapper.PrepareApplication(league, backend, item));
    }

    [Theory]
    [InlineData("approved", "notes")]
    [InlineData("pending", null)]
    public void Review_RejectsInvalidMetadata(string status, string? notes)
    {
        Assert.Throws<InvalidOperationException>(() => AdminEntryReviewMapper.ReviewValues(JsonSerializer.SerializeToElement(new { status, notes })));
    }

    [Fact]
    public void Review_EnforcesUtf8ByteLimit()
    {
        Assert.Throws<InvalidOperationException>(() => AdminEntryReviewMapper.ReviewValues(
            JsonSerializer.SerializeToElement(new { status = "pending", notes = new string('é', 8001) })));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Review_RequiresExplicitMappingAndPreservesAnswers(bool mapped)
    {
        var backend = Guid.NewGuid().ToString();
        var form = new EntryForm();
        var entry = new EntryFormSubmission
        {
            EntryName = "Team", FieldValues = new() { ["Historic field"] = "Original answer" },
            SourceBackendId = mapped ? backend : null, SourceClientId = mapped ? "client-1" : null,
            SourceSubmissionSequence = mapped ? 1 : null
        };
        form.Submissions.Add(entry);
        var league = new LeagueData();
        league.WebsiteSettings.EntryForms.Add(form);
        var change = new AdminSyncChange(1, "entry_review", "1", 1, null, "web",
            JsonSerializer.SerializeToElement(new { submissionSequence = 1, formId = $"form-{form.Id:N}", clientId = "client-1", status = "confirmed", notes = "Reviewed" }));
        var snapshot = JsonSerializer.SerializeToElement(entry);
        if (!mapped)
        {
            Assert.Throws<InvalidOperationException>(() => AdminEntryReviewMapper.CreateReviewedDraft(league, backend, change, snapshot));
            return;
        }
        var draft = AdminEntryReviewMapper.CreateReviewedDraft(league, backend, change, snapshot);
        Assert.Equal("confirmed", draft.Status);
        Assert.Equal("pending", entry.Status);
        Assert.Equal("Original answer", draft.FieldValues["Historic field"]);
        Assert.Null(draft.LinkedTeamId);
        entry.Notes = "Local edit";
        Assert.Throws<InvalidOperationException>(() => AdminEntryReviewMapper.CreateReviewedDraft(league, backend, change, snapshot));
        Assert.Throws<InvalidOperationException>(() => AdminEntryReviewMapper.FindMapped(league, Guid.NewGuid().ToString(), change));
    }
}
