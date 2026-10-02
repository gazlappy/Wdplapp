using Wdpl2.Domain.Fixtures;
using Wdpl2.Models;
using Wdpl2.Services.Web;
using Xunit;
using static Wdpl2.Services.Web.ScorecardService;

namespace wdpl2.Tests.Features.WebPlatform;

/// <summary>
/// The message centre's own copy of captains' notes: nothing is lost when the
/// website moves on, and nothing is kept twice.
/// </summary>
public class MessageArchiveTests
{
    private static readonly Guid Tie = Guid.NewGuid();

    private static CardMessage Note(string text, Guid? fixture = null, DateTime? date = null) =>
        new(fixture ?? Tie, "LEGENDS", "CHANGING LANES", true, date ?? new DateTime(2026, 10, 1),
            text, new DateTime(2026, 10, 1, 20, 15, 0), Unread: true);

    [Fact]
    public void A_message_read_again_is_kept_once()
    {
        var league = new LeagueData();

        Assert.Equal(1, MessageArchive.Keep(league, new[] { Note("Started late.") }));
        Assert.Equal(0, MessageArchive.Keep(league, new[] { Note("Started late.") }));

        Assert.Single(league.CardMessages);
    }

    [Fact]
    public void A_rewritten_note_keeps_both_versions()
    {
        var league = new LeagueData();

        MessageArchive.Keep(league, new[] { Note("Started late.") });
        MessageArchive.Keep(league, new[] { Note("Started late. Their no.3 left early.") });

        Assert.Equal(2, league.CardMessages.Count);
    }

    [Fact]
    public void A_message_the_website_no_longer_has_stays()
    {
        var league = new LeagueData();
        MessageArchive.Keep(league, new[] { Note("Started late.") });

        MessageArchive.Keep(league, Array.Empty<CardMessage>());

        Assert.Single(league.CardMessages);
    }

    [Fact]
    public void A_collected_card_keeps_its_note_without_the_solo_stamp()
    {
        var league = new LeagueData();
        var card = new ScorecardState
        {
            FixtureId = Tie, HomeTeam = "LEGENDS", AwayTeam = "CHANGING LANES",
            Notes = "Their phone died.\n[Submitted from one device by the home captain.]",
        };

        Assert.Equal(1, MessageArchive.Keep(league, card));
        Assert.Equal("Their phone died.", league.CardMessages[0].Text);

        // The same note read later from Messages is not kept again.
        Assert.Equal(0, MessageArchive.Keep(league, new[] { Note("Their phone died.") }));
    }

    [Fact]
    public void A_card_with_only_the_solo_stamp_has_no_message()
    {
        var league = new LeagueData();
        var card = new ScorecardState { FixtureId = Tie, Notes = "[Submitted from one device by the away captain.]" };

        Assert.Equal(0, MessageArchive.Keep(league, card));
    }

    [Fact]
    public void A_message_is_placed_in_its_season()
    {
        var league = new LeagueData();
        var autumn = new Season { Name = "Autumn 2026", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2026, 12, 20) };
        var spring = new Season { Name = "Spring 2026", StartDate = new DateTime(2026, 1, 10), EndDate = new DateTime(2026, 5, 1) };
        league.Seasons.AddRange(new[] { autumn, spring });

        var fixture = new Fixture { SeasonId = spring.Id };
        league.Fixtures.Add(fixture);

        // A league fixture by its own season; a cup tie by its date.
        MessageArchive.Keep(league, new[] { Note("League night", fixture.Id), Note("Cup tie") });

        Assert.Equal(spring.Id, league.CardMessages.Single(m => m.Text == "League night").SeasonId);
        Assert.Equal(autumn.Id, league.CardMessages.Single(m => m.Text == "Cup tie").SeasonId);
    }

    [Fact]
    public void Newest_written_comes_first()
    {
        var league = new LeagueData();
        league.CardMessages.Add(new CardMessageRecord { Text = "old", WrittenAt = new DateTime(2026, 9, 1) });
        league.CardMessages.Add(new CardMessageRecord { Text = "new", WrittenAt = new DateTime(2026, 10, 1) });

        Assert.Equal(new[] { "new", "old" }, MessageArchive.Newest(league).Select(m => m.Text));
    }
}
