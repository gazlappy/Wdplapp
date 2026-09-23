using Microsoft.EntityFrameworkCore;
using Wdpl2.Data;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// The first-run wizard's rules: what is enough to start a league, and the
/// season it saves.
/// </summary>
[Collection(SharedDataStoreCollection.Name)]
public class NewLeagueSetupTests : SharedDataStoreTest
{
    private static NewLeagueSetup Ready()
    {
        var setup = new NewLeagueSetup
        {
            LeagueName = "Riverside Pool League",
            SeasonName = "2026/27",
            StartDate = new DateTime(2026, 9, 1),
            EndDate = new DateTime(2027, 4, 30),
            MatchDay = DayOfWeek.Wednesday,
            FramesPerMatch = 10,
        };
        setup.Divisions.AddRange(["Premier", "Division One"]);
        setup.TeamsByDivision["Premier"] = ["Red Lion A", "Crown", "Anchor"];
        setup.TeamsByDivision["Division One"] = ["Red Lion B", "Plough"];
        setup.Venues.AddRange(["Red Lion", "Crown"]);
        setup.HomeVenues["Red Lion A"] = "Red Lion";
        setup.HomeVenues["Red Lion B"] = "Red Lion";
        setup.HomeVenues["Crown"] = "Crown";
        return setup;
    }

    [Fact]
    public void Names_typed_one_per_line_lose_blanks_repeats_and_spaces()
    {
        var names = NewLeagueSetup.Lines("  Crown \r\n\r\nAnchor\ncrown\n   \nPlough");
        Assert.Equal(["Crown", "Anchor", "Plough"], names);
    }

    [Fact]
    public void A_complete_setup_has_nothing_to_put_right() =>
        Assert.Empty(Ready().Problems());

    [Fact]
    public void An_empty_setup_asks_for_the_essentials()
    {
        var problems = new NewLeagueSetup().Problems();
        Assert.Contains("Give the league a name.", problems);
        Assert.Contains("Give the first season a name.", problems);
        Assert.Contains("Add at least one division.", problems);
    }

    [Fact]
    public void A_season_ending_before_it_starts_is_refused()
    {
        var setup = Ready();
        setup.EndDate = setup.StartDate.AddDays(-1);
        Assert.Contains("The season has to end after it starts.", setup.LeagueProblems());
    }

    [Fact]
    public void A_division_of_one_team_cannot_play()
    {
        var setup = Ready();
        setup.TeamsByDivision["Division One"] = ["Plough"];
        Assert.Contains(setup.TeamProblems(), p => p.StartsWith("Division One has only one team"));
    }

    [Fact]
    public void An_empty_division_is_allowed_for_now()
    {
        var setup = Ready();
        setup.Divisions.Add("Division Two");
        Assert.Empty(setup.Problems());
    }

    [Fact]
    public void A_team_in_two_divisions_is_refused()
    {
        var setup = Ready();
        setup.TeamsByDivision["Division One"] = ["Plough", "crown"];
        Assert.Contains(setup.TeamProblems(), p => p.Contains("more than one division"));
    }

    [Fact]
    public void Divisions_with_no_teams_at_all_ask_for_teams()
    {
        var setup = Ready();
        setup.TeamsByDivision.Clear();
        Assert.Contains("Add the teams.", setup.TeamProblems());
    }

    [Fact]
    public void Building_links_every_team_to_its_division_and_home_venue()
    {
        var (season, divisions, venues, teams) = Ready().Build();

        Assert.True(season.IsActive);
        Assert.Equal(DayOfWeek.Wednesday, season.MatchDayOfWeek);
        Assert.All(divisions, d => Assert.Equal(season.Id, d.SeasonId));
        Assert.All(venues, v => Assert.Single(v.Tables));
        Assert.All(teams, t => Assert.Equal(season.Id, t.SeasonId));

        var premier = divisions.Single(d => d.Name == "Premier");
        var redLion = venues.Single(v => v.Name == "Red Lion");
        var a = teams.Single(t => t.Name == "Red Lion A");
        Assert.Equal(premier.Id, a.DivisionId);
        Assert.Equal(redLion.Id, a.VenueId);
        Assert.Equal(redLion.Tables[0].Id, a.TableId);

        Assert.Null(teams.Single(t => t.Name == "Plough").VenueId);
        Assert.Equal(5, teams.Count);
    }

    [Fact]
    public async Task Saving_stores_the_season_and_everything_in_it()
    {
        var options = new DbContextOptionsBuilder<LeagueContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var context = new LeagueContext(options);
        var store = new SqliteDataStore(context);

        var season = await Ready().SaveAsync(store);

        using var reloaded = new LeagueContext(options);
        Assert.Equal(season.Id, (await reloaded.Seasons.SingleAsync()).Id);
        Assert.Equal(2, await reloaded.Divisions.CountAsync(d => d.SeasonId == season.Id));
        Assert.Equal(2, await reloaded.Venues.CountAsync(v => v.SeasonId == season.Id));
        Assert.Equal(5, await reloaded.Teams.CountAsync(t => t.SeasonId == season.Id));
    }

    [Fact]
    public async Task An_incomplete_setup_saves_nothing()
    {
        var options = new DbContextOptionsBuilder<LeagueContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var context = new LeagueContext(options);
        var setup = Ready();
        setup.LeagueName = "";

        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.SaveAsync(new SqliteDataStore(context)));
        Assert.Empty(await context.Seasons.ToListAsync());
    }
}
