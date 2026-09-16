using System.Net;
using System.Text;
using Wdpl2.Models;
using Wdpl2.Services.Web;

namespace wdpl2.Tests;

/// <summary>
/// The whole way home: the JSON the server sends, through the parse, into the
/// app's own competition.
/// </summary>
/// <remarks>
/// Apply is covered on its own, and so is what the server returns. The join
/// between them was not, which is exactly where a night can be collected
/// successfully and still leave the competition untouched.
/// <para>
/// The payloads here are the shape <c>comps/collect</c> actually produces -
/// snake_case, numbers as strings the way PDO hands them back - rather than a
/// tidied version that would not catch a mismatch.
/// </para>
/// </remarks>
public class CompetitionCollectRoundTripTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _body;

        public FakeHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
    }

    private static WebConnection Connection() => new()
    {
        BaseUrl = "https://wdpl.uk/api/",
        AdminUser = "admin",
        AdminPassword = "pw",
    };

    private static readonly Guid GroupId = Guid.NewGuid();
    private static readonly Guid Ann = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();
    private static readonly Guid Cal = Guid.NewGuid();

    private static readonly Guid Tie1 = Guid.NewGuid();
    private static readonly Guid Tie2 = Guid.NewGuid();
    private static readonly Guid Final = Guid.NewGuid();

    /// <summary>
    /// What the server sends back, MySQL's stringly-typed numbers and all.
    /// </summary>
    private static string CollectBody() => $$"""
    {
      "ok": true,
      "data": {
        "id": "{{Guid.NewGuid()}}",
        "competition_id": "{{Guid.NewGuid()}}",
        "competition": "Singles Cup",
        "name": "Group A",
        "kind": "group",
        "ref_id": "{{GroupId}}",
        "venue_name": "The Bell",
        "best_of": "3",
        "frames_to_win": "2",
        "state": "closed",
        "version": "9",
        "drawn": "1",
        "bracket_size": "4",
        "players": [
          { "participant_id": "{{Ann}}", "name": "Ann Reid", "sort_order": "0", "present": "1", "draw_no": "1" },
          { "participant_id": "{{Bob}}", "name": "Bob Crane", "sort_order": "1", "present": "1", "draw_no": "3" },
          { "participant_id": "{{Cal}}", "name": "Cal Dow", "sort_order": "2", "present": "1", "draw_no": "2" }
        ],
        "matches": [
          { "match_id": "{{Tie1}}", "round_no": "1", "slot": "0",
            "p1_id": "{{Ann}}", "p2_id": "{{Bob}}",
            "p1_score": "2", "p2_score": "1", "winner_id": "{{Ann}}", "is_complete": "1" },
          { "match_id": "{{Tie2}}", "round_no": "1", "slot": "1",
            "p1_id": "{{Cal}}", "p2_id": null,
            "p1_score": "0", "p2_score": "0", "winner_id": "{{Cal}}", "is_complete": "1" },
          { "match_id": "{{Final}}", "round_no": "2", "slot": "0",
            "p1_id": "{{Ann}}", "p2_id": "{{Cal}}",
            "p1_score": "2", "p2_score": "0", "winner_id": "{{Ann}}", "is_complete": "1" }
        ]
      }
    }
    """;

    private static Competition Competition()
    {
        var group = new CompetitionGroup
        {
            Id = GroupId,
            Name = "Group A",
            ParticipantIds = { Ann, Bob, Cal },
        };

        // What the app had pencilled in before the night.
        group.Matches.Add(new CompetitionMatch { Participant1Id = Ann, Participant2Id = Bob });

        var competition = new Competition
        {
            Id = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            Name = "Singles Cup",
            Format = CompetitionFormat.SinglesGroupStage,
            BestOf = 3,
        };
        competition.Groups.Add(group);
        return competition;
    }

    private static async Task<CompetitionNightService.CollectedSession> CollectAsync()
    {
        using var client = new WebApiClient(Connection(), new FakeHandler(CollectBody()));
        return await CompetitionNightService.CollectAsync(client, Guid.NewGuid());
    }

    [Fact]
    public async Task TheReplyIsReadIntoASessionTheAppCanUse()
    {
        var collected = await CollectAsync();

        Assert.Equal(GroupId, collected.RefId);
        Assert.Equal("group", collected.Kind);
        Assert.Equal(3, collected.Matches.Count);
    }

    [Fact]
    public async Task TheDrawComesBackInTheOrderTheyWereDrawn()
    {
        var collected = await CollectAsync();

        // Sent in squad order, but drawn 1, 3, 2 - so Cal is second out.
        Assert.Equal(new[] { Ann, Cal, Bob }, collected.DrawOrder);
    }

    [Fact]
    public async Task NumbersSurviveArrivingAsStrings()
    {
        var collected = await CollectAsync();

        var final = collected.Matches.Single(m => m.RoundNumber == 2);

        Assert.Equal(0, final.Slot);
        Assert.Equal(2, final.P1Score);
        Assert.Equal(0, final.P2Score);
        Assert.True(final.IsComplete);
    }

    [Fact]
    public async Task AByeComesBackWithOneSideEmpty()
    {
        var collected = await CollectAsync();

        var bye = collected.Matches.Single(m => m.P2Id is null);

        Assert.Equal(Cal, bye.P1Id);
        Assert.Equal(Cal, bye.WinnerId);
        Assert.True(bye.IsComplete);
    }

    [Fact]
    public async Task CollectingWritesTheNightIntoTheCompetition()
    {
        var competition = Competition();
        var collected = await CollectAsync();

        var applied = CompetitionNightService.Apply(competition, collected);

        Assert.Equal(3, applied);

        var group = competition.Groups[0];
        Assert.Equal(3, group.Matches.Count);
        Assert.Equal(new[] { Ann, Cal, Bob }, group.DrawOrder);

        var final = group.Matches.Single(m => m.RoundNumber == 2);
        Assert.Equal(Ann, final.WinnerId);
        Assert.Equal(2, final.Participant1Score);
    }

    [Fact]
    public async Task TheMatchIdsAreTheOnesTheWebsitePlayed()
    {
        var competition = Competition();
        CompetitionNightService.Apply(competition, await CollectAsync());

        var ids = competition.Groups[0].Matches.Select(m => m.Id).ToList();

        // Collecting twice must land on the same rows rather than doubling them.
        Assert.Contains(Tie1, ids);
        Assert.Contains(Tie2, ids);
        Assert.Contains(Final, ids);
    }

    [Fact]
    public async Task CollectingTwiceLeavesOneCopyOfTheNight()
    {
        var competition = Competition();

        CompetitionNightService.Apply(competition, await CollectAsync());
        CompetitionNightService.Apply(competition, await CollectAsync());

        Assert.Equal(3, competition.Groups[0].Matches.Count);
    }
}
