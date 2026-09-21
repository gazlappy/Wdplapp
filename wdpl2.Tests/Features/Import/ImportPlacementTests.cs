using Wdpl2.Models;
using Wdpl2.Services.Import;

namespace Wdpl2.Tests;

public class ImportPlacementTests
{
    [Fact]
    public void Validator_RejectsCrossSeasonLinks()
    {
        var first = new Season();
        var second = new Season();
        var division = new Division { SeasonId = second.Id };
        var venue = new Venue { SeasonId = second.Id };
        var team = new Team { SeasonId = first.Id, DivisionId = division.Id, VenueId = venue.Id };
        var player = new Player { SeasonId = second.Id, TeamId = team.Id };
        var data = new LeagueData { Seasons = [first, second], Divisions = [division], Venues = [venue], Teams = [team], Players = [player] };
        Assert.Equal(3, ImportPlacementValidator.Validate(data).Count);
    }

    [Fact]
    public void Validator_ChecksDoublesPlayersAndVenueTable()
    {
        var season = new Season();
        var home = new Team { SeasonId = season.Id };
        var away = new Team { SeasonId = season.Id };
        var fixture = new Fixture
        {
            SeasonId = season.Id, HomeTeamId = home.Id, AwayTeamId = away.Id, TableId = Guid.NewGuid(),
            Frames = [new FrameResult { Number = 1, HomePlayer2Id = Guid.NewGuid() }]
        };
        var data = new LeagueData { Seasons = [season], Teams = [home, away], Fixtures = [fixture] };
        Assert.Equal(2, ImportPlacementValidator.Validate(data).Count);
    }

    [Fact]
    public void Validator_AllowsWalkoversAndHistoricalTeamChanges()
    {
        var season = new Season();
        var home = new Team { SeasonId = season.Id };
        var away = new Team { SeasonId = season.Id };
        var player = new Player { SeasonId = season.Id, TeamId = away.Id };
        var fixture = new Fixture
        {
            SeasonId = season.Id, HomeTeamId = home.Id, AwayTeamId = away.Id,
            Frames = [new FrameResult { Number = 1, HomePlayerId = player.Id, AwayPlayerId = FrameResult.VoidPlayerId }]
        };
        var data = new LeagueData { Seasons = [season], Teams = [home, away], Players = [player], Fixtures = [fixture] };
        Assert.Empty(ImportPlacementValidator.Validate(data));
    }

    [Fact]
    public void Validator_DoesNotBlockUnchangedLegacyIssues()
    {
        var before = new LeagueData { Players = [new Player { FirstName = "Legacy" }] };
        var after = ImportWorkspace.Clone(before);
        after.Seasons.Add(new Season());
        ImportPlacementValidator.ThrowIfNewIssues(before, after);
        after.Players.Add(new Player { FirstName = "New orphan" });
        Assert.Throws<InvalidDataException>(() => ImportPlacementValidator.ThrowIfNewIssues(before, after));
    }
}
