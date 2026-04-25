using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Tests;

/// <summary>
/// Tests for SearchService — cross-entity search.
/// </summary>
public class SearchServiceTests
{
    private static LeagueData CreateSampleData()
    {
        var seasonId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var divId = Guid.NewGuid();

        return new LeagueData
        {
            Seasons = new List<Season>
            {
                new() { Id = seasonId, Name = "Winter 2025" }
            },
            Divisions = new List<Division>
            {
                new() { Id = divId, SeasonId = seasonId, Name = "Premier Division" }
            },
            Teams = new List<Team>
            {
                new() { Id = teamId, SeasonId = seasonId, DivisionId = divId, Name = "The Red Lions" }
            },
            Players = new List<Player>
            {
                new() { Id = Guid.NewGuid(), SeasonId = seasonId, TeamId = teamId, FirstName = "John", LastName = "Smith" }
            },
            Venues = new List<Venue>
            {
                new() { Id = Guid.NewGuid(), SeasonId = seasonId, Name = "The Crown", Address = "123 High Street" }
            }
        };
    }

    [Fact]
    public void EmptyQuery_ReturnsEmpty()
    {
        var results = SearchService.Search(CreateSampleData(), "");
        Assert.Empty(results);
    }

    [Fact]
    public void WhitespaceQuery_ReturnsEmpty()
    {
        var results = SearchService.Search(CreateSampleData(), "   ");
        Assert.Empty(results);
    }

    [Fact]
    public void SearchByPlayerName_FindsPlayer()
    {
        var results = SearchService.Search(CreateSampleData(), "John");
        Assert.Contains(results, r => r.Type == "Player" && r.Title.Contains("John"));
    }

    [Fact]
    public void SearchByTeamName_FindsTeam()
    {
        var results = SearchService.Search(CreateSampleData(), "Red Lions");
        Assert.Contains(results, r => r.Type == "Team" && r.Title.Contains("Red Lions"));
    }

    [Fact]
    public void SearchByVenueAddress_FindsVenue()
    {
        var results = SearchService.Search(CreateSampleData(), "High Street");
        Assert.Contains(results, r => r.Type == "Venue");
    }

    [Fact]
    public void SearchBySeasonName_FindsSeason()
    {
        var results = SearchService.Search(CreateSampleData(), "Winter");
        Assert.Contains(results, r => r.Type == "Season" && r.Title.Contains("Winter"));
    }

    [Fact]
    public void CaseInsensitive_FindsResults()
    {
        var results = SearchService.Search(CreateSampleData(), "john");
        Assert.Contains(results, r => r.Type == "Player");
    }

    [Fact]
    public void SeasonFilter_LimitsResults()
    {
        var data = CreateSampleData();
        var otherSeason = Guid.NewGuid();
        data.Players.Add(new Player
        {
            Id = Guid.NewGuid(),
            SeasonId = otherSeason,
            FirstName = "John",
            LastName = "Other"
        });

        var seasonId = data.Seasons[0].Id;
        var results = SearchService.Search(data, "John", seasonFilter: seasonId);

        Assert.Single(results, r => r.Type == "Player");
    }

    [Fact]
    public void NoMatch_ReturnsEmpty()
    {
        var results = SearchService.Search(CreateSampleData(), "xyznonexistent");
        Assert.Empty(results);
    }
}
