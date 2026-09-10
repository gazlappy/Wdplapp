using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services.Inbox;

namespace wdpl2.Tests;

public class AdminEntryLinkMapperTests
{
    private readonly string _backend = Guid.NewGuid().ToString();
    private readonly LeagueData _league = new();
    private readonly EntryForm _form = new();
    private readonly EntryFormSubmission _entry = new() { EntryName = "Local entry", Notes = "Local notes", FieldValues = new() { ["Answer"] = "Original" } };
    private AdminSyncChange Change => new(1, "entry_review", "7", 1, null, "web", JsonSerializer.SerializeToElement(new
    {
        formId = $"form-{_form.Id:N}", clientId = "client-1", submissionSequence = 7, status = "confirmed", notes = "Web notes"
    }));
    public AdminEntryLinkMapperTests()
    {
        _form.Submissions.Add(_entry);
        _league.WebsiteSettings.EntryForms.Add(_form);
    }
    private EntryFormSubmission Prepare() => AdminEntryLinkMapper.Prepare(_league, _backend, Change, _entry.Id, JsonSerializer.SerializeToElement(_entry));

    [Fact]
    public void Link_PreservesAnswersAndReviewWithoutMutatingOriginal()
    {
        var linked = Prepare();
        Assert.Equal(_backend, linked.SourceBackendId);
        Assert.Equal(7, linked.SourceSubmissionSequence);
        Assert.Equal("client-1", linked.SourceClientId);
        Assert.Equal("pending", linked.Status);
        Assert.Equal("Local notes", linked.Notes);
        Assert.Equal("Original", linked.FieldValues["Answer"]);
        Assert.Null(_entry.SourceBackendId);
        _form.Submissions[0] = linked;
        var retry = AdminEntryLinkMapper.Prepare(_league, _backend, Change, linked.Id, JsonSerializer.SerializeToElement(linked));
        Assert.Equal(JsonSerializer.Serialize(linked), JsonSerializer.Serialize(retry));
    }

    [Fact]
    public void Link_RejectsChangedCandidate()
    {
        var before = JsonSerializer.SerializeToElement(_entry);
        _entry.Notes = "Changed after comparison";
        Assert.Throws<InvalidOperationException>(() => AdminEntryLinkMapper.Prepare(_league, _backend, Change, _entry.Id, before));
    }

    [Fact]
    public void Link_RejectsReassignmentAndPartialMapping()
    {
        _entry.SourceClientId = "other";
        Assert.Throws<InvalidOperationException>(() => Prepare());
    }

    [Fact]
    public void Link_RejectsDuplicateServerIdentityAcrossForms()
    {
        var other = new EntryForm();
        other.Submissions.Add(new() { SourceBackendId = _backend, SourceClientId = "other", SourceSubmissionSequence = 7 });
        _league.WebsiteSettings.EntryForms.Add(other);
        Assert.Throws<InvalidOperationException>(() => Prepare());
    }

    [Fact]
    public void Link_RejectsCandidateInDifferentForm()
    {
        var other = new EntryForm();
        var candidate = new EntryFormSubmission();
        other.Submissions.Add(candidate);
        _league.WebsiteSettings.EntryForms.Add(other);
        Assert.Throws<InvalidOperationException>(() => AdminEntryLinkMapper.Prepare(_league, _backend, Change, candidate.Id, JsonSerializer.SerializeToElement(candidate)));
    }

    [Fact]
    public void Link_RejectsLockedSeason()
    {
        var season = new Season { IsLocked = true };
        var team = new Team { SeasonId = season.Id };
        _league.Seasons.Add(season);
        _league.Teams.Add(team);
        _entry.LinkedTeamId = team.Id;
        Assert.Throws<InvalidOperationException>(() => Prepare());
    }
}
