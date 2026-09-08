using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services.Inbox;

namespace wdpl2.Tests;

public class AdminScorecardMapperTests
{
    private static (LeagueData League, Fixture Fixture, AdminSyncChange Change) Setup()
    {
        var season = new Season();
        var home = new Team { SeasonId = season.Id };
        var away = new Team { SeasonId = season.Id };
        var hp = new Player { SeasonId = season.Id, TeamId = home.Id };
        var ap = new Player { SeasonId = season.Id, TeamId = away.Id };
        var fixture = new Fixture { SeasonId = season.Id, HomeTeamId = home.Id, AwayTeamId = away.Id, HomeLatePenalty = 2 };
        var league = new LeagueData { Seasons = [season], Teams = [home, away], Players = [hp, ap], Fixtures = [fixture] };
        var payload = JsonSerializer.SerializeToElement(new { state = new {
            home_team_id = home.Id, away_team_id = away.Id,
            frames = new[] { new { number = 1, is_doubles = false, winner = "home", eight_ball = true,
                home_player_id = hp.Id, away_player_id = ap.Id } }
        } });
        return (league, fixture, new(1, "scorecard", fixture.Id.ToString(), 1, season.Id.ToString(), "web", payload));
    }

    [Fact]
    public void Draft_PreservesFixtureSettingsAndDoesNotMutateLeague()
    {
        var (league, fixture, change) = Setup();
        var snapshot = AdminScorecardMapper.Capture(league, fixture.Id);
        var draft = AdminScorecardMapper.CreateReviewedDraft(league, change, snapshot);
        Assert.Empty(fixture.Frames);
        Assert.Equal(2, draft.HomeLatePenalty);
        Assert.Equal(FrameWinner.Home, Assert.Single(draft.Frames).Winner);
        Assert.Equal(2, league.Players.Count);
    }

    [Fact]
    public void Draft_RejectsLockedSeason()
    {
        var (league, fixture, change) = Setup();
        league.Seasons[0].IsLocked = true;
        Assert.Throws<InvalidOperationException>(() => AdminScorecardMapper.CreateReviewedDraft(league, change, AdminScorecardMapper.Capture(league, fixture.Id)));
    }

    [Fact]
    public void Draft_RejectsChangedLocalFixture()
    {
        var (league, fixture, change) = Setup();
        var snapshot = AdminScorecardMapper.Capture(league, fixture.Id);
        fixture.HomeLatePenalty++;
        Assert.Throws<InvalidOperationException>(() => AdminScorecardMapper.CreateReviewedDraft(league, change, snapshot));
    }

    [Fact]
    public void Draft_RejectsUnknownPlayerWithoutNameMatchingOrCreation()
    {
        var (league, fixture, change) = Setup();
        league.Players.RemoveAt(0);
        Assert.Throws<InvalidOperationException>(() => AdminScorecardMapper.CreateReviewedDraft(league, change, AdminScorecardMapper.Capture(league, fixture.Id)));
        Assert.Single(league.Players);
    }

    [Fact]
    public void Draft_RejectsDifferentSeason()
    {
        var (league, fixture, change) = Setup();
        Assert.Throws<InvalidOperationException>(() => AdminScorecardMapper.CreateReviewedDraft(league,
            change with { SeasonId = Guid.NewGuid().ToString() }, AdminScorecardMapper.Capture(league, fixture.Id)));
    }
}
