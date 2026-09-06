using System.Xml.Linq;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

public class FixturesSheetGeneratorTests
{
    private static (LeagueData league, Season season) CreateLeague()
    {
        var league = new LeagueData();
        var season = new Season { Name = "Winter", StartDate = new DateTime(2026, 9, 17) };
        league.Seasons.Add(season);
        foreach (var name in new[] { "1st", "2nd & reserves" })
        {
            var division = new Division { SeasonId = season.Id, Name = name };
            league.Divisions.Add(division);
            var first = new Team { SeasonId = season.Id, DivisionId = division.Id, Name = "Alpha & Sons" };
            var second = new Team { SeasonId = season.Id, DivisionId = division.Id, Name = "Zulu" };
            // Deliberately reverse input order: numbering must not depend on insertion order.
            league.Teams.AddRange(new[] { second, first });
            bool reverse = league.Divisions.Count == 2;
            league.Fixtures.Add(new Fixture
            {
                SeasonId = season.Id, DivisionId = division.Id, Date = season.StartDate.AddHours(19),
                HomeTeamId = reverse ? second.Id : first.Id, AwayTeamId = reverse ? first.Id : second.Id
            });
            league.Fixtures.Add(new Fixture
            {
                SeasonId = season.Id, DivisionId = division.Id, Date = season.StartDate.AddDays(7).AddHours(19),
                HomeTeamId = reverse ? first.Id : second.Id, AwayTeamId = reverse ? second.Id : first.Id
            });
        }
        return (league, season);
    }

    private static XElement Render(LeagueData league, Season season, FixturesSheetSettings? settings = null, List<Guid>? divisions = null) =>
        XElement.Parse(new FixturesSheetGenerator(league, settings ?? new FixturesSheetSettings()).GenerateEmbeddableContent(season.Id, divisions));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SharedDates_RenderOnce_PreserveEveryDivisionPairing(bool samePairings)
    {
        var (league, season) = CreateLeague();
        if (samePairings)
            foreach (var fixture in league.Fixtures.Where(f => f.DivisionId == league.Divisions[1].Id))
                (fixture.HomeTeamId, fixture.AwayTeamId) = (fixture.AwayTeamId, fixture.HomeTeamId);
        var html = Render(league, season);
        Assert.Equal(2, html.Descendants().Count(e => e.Attribute("data-date") != null));
        Assert.Equal(2, html.Descendants().Count(e => e.Attribute("data-home-number") != null));
        Assert.DoesNotContain(html.Descendants(), e => (string?)e.Attribute("class") == "wk-division");
        foreach (var fixture in league.Fixtures)
        {
            var date = html.Descendants().Single(e => (string?)e.Attribute("data-date") == fixture.Date.ToString("yyyy-MM-dd"));
            var match = Assert.Single(date.Descendants().Where(e => e.Attribute("data-home-number") != null));
            foreach (var (id, css) in new[] { (fixture.HomeTeamId, "wk-home"), (fixture.AwayTeamId, "wk-away") })
            {
                var number = Assert.Single(match.Elements("span").Where(e => (string?)e.Attribute("class") == css));
                var key = Assert.Single(html.Descendants("tr").Where(e => (string?)e.Attribute("id") == $"fixture-team-{id}"));
                Assert.Equal(key.Descendants("span").Single().Value, number.Value);
                var team = league.Teams.Single(t => t.Id == id);
                Assert.Equal(team.Name, key.Elements("td").Single(e => (string?)e.Attribute("class") == "div-name").Value);
            }
        }
    }

    [Fact]
    public void DivisionFilter_UsesOnlySelectedFixturesAndKeys()
    {
        var (league, season) = CreateLeague();
        var division = league.Divisions[1];
        var html = Render(league, season, divisions: new() { division.Id });
        Assert.Equal(2, html.Descendants().Count(e => e.Attribute("data-home-number") != null));
        Assert.Equal(2, html.Descendants("tr").Count());
        Assert.All(html.Descendants().Where(e => e.Attribute("data-division-id") != null),
            e => Assert.Equal(division.Id.ToString(), (string?)e.Attribute("data-division-id")));
    }

    [Fact]
    public void HiddenNumbers_RenderNamesWithoutBrokenLinks()
    {
        var (league, season) = CreateLeague();
        var html = Render(league, season, new FixturesSheetSettings { ShowTeamNumbers = false });
        var matches = html.Descendants().Where(e => e.Attribute("data-fixture-id") != null).ToList();
        Assert.All(matches, e =>
        {
            Assert.Empty(e.Elements("a"));
            Assert.Contains("Alpha & Sons", e.Value);
            Assert.Contains("Zulu", e.Value);
        });
    }

    [Fact]
    public void UnequalDivisionDates_AndMultipleEvents_PreserveOneCardPerDate()
    {
        var (league, season) = CreateLeague();
        var settings = new FixturesSheetSettings
        {
            SpecialEvents = new()
            {
                new SpecialEvent { Date = season.StartDate.AddDays(14), Description = "Cup" },
                new SpecialEvent { Date = season.StartDate.AddDays(14), Description = "Meeting" }
            }
        };
        var html = Render(league, season, settings);
        Assert.Equal(3, html.Descendants().Count(e => e.Attribute("data-date") != null));
        Assert.Equal(2, html.Descendants().Count(e => e.Attribute("data-home-number") != null));
        var events = Assert.Single(html.Descendants().Where(e => (string?)e.Attribute("data-date") == "2026-10-01"));
        Assert.Contains("Cup", events.Value);
        Assert.Contains("Meeting", events.Value);
        settings.ShowSpecialEvents = false;
        Assert.Equal(2, Render(league, season, settings).Descendants().Count(e => e.Attribute("data-date") != null));
    }

    [Fact]
    public void OrphanFixture_BlocksSharedGrid()
    {
        var (league, season) = CreateLeague();
        league.Fixtures[0].DivisionId = null;
        var html = Render(league, season);
        Assert.Contains("invalid division placement", html.Value);
        Assert.Empty(html.Descendants().Where(e => e.Attribute("data-home-number") != null));
    }

    [Fact]
    public void StandaloneSheet_ContainsSameTimelineAsEmbeddedSheet()
    {
        var (league, season) = CreateLeague();
        var generator = new FixturesSheetGenerator(league, new FixturesSheetSettings());
        Assert.Contains(generator.GenerateEmbeddableContent(season.Id), generator.GenerateFixturesSheet(season.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IncompatibleDatesOrHomeAway_BlocksWithoutChangingFixtures(bool changeDate)
    {
        var (league, season) = CreateLeague();
        var fixture = league.Fixtures[3];
        if (changeDate) fixture.Date = fixture.Date.AddDays(1);
        else (fixture.HomeTeamId, fixture.AwayTeamId) = (fixture.AwayTeamId, fixture.HomeTeamId);
        var before = league.Fixtures.Select(f => (f.Id, f.Date, f.HomeTeamId, f.AwayTeamId)).ToList();
        var html = Render(league, season);
        Assert.Single(html.Descendants().Where(e => (string?)e.Attribute("role") == "alert"));
        Assert.Empty(html.Descendants().Where(e => e.Attribute("data-home-number") != null));
        Assert.Equal(before, league.Fixtures.Select(f => (f.Id, f.Date, f.HomeTeamId, f.AwayTeamId)).ToList());
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(4, 3)]
    [InlineData(4, 2)]
    [InlineData(3, 3)]
    public void FullDraw_PermutationsAndByes_ExpandToExactlySavedFixtures(int firstCount, int secondCount)
    {
        var league = new LeagueData();
        var season = new Season { Name = "Winter", StartDate = new DateTime(2026, 9, 17) };
        league.Seasons.Add(season);
        var pattern = new[] { (1, 2), (3, 4), (1, 3), (4, 2), (1, 4), (2, 3) };
        for (int d = 0; d < 2; d++)
        {
            var division = new Division { SeasonId = season.Id, Name = $"Division {d}" };
            league.Divisions.Add(division);
            int count = d == 0 ? firstCount : secondCount;
            var slots = Enumerable.Range(1, count).ToDictionary(n => n, n => new Team
            {
                SeasonId = season.Id, DivisionId = division.Id,
                Name = d == 0 ? $"Team {n}" : $"Team {5 - n}"
            });
            league.Teams.AddRange(slots.Values.Reverse());
            for (int leg = 0; leg < 2; leg++)
                for (int p = 0; p < pattern.Length; p++)
                {
                    var (h, a) = pattern[p];
                    if (leg == 1) (h, a) = (a, h);
                    if (!slots.ContainsKey(h) || !slots.ContainsKey(a)) continue;
                    league.Fixtures.Add(new Fixture
                    {
                        SeasonId = season.Id, DivisionId = division.Id,
                        Date = season.StartDate.AddDays((p / 2 + leg * 3) * 7),
                        HomeTeamId = slots[h].Id, AwayTeamId = slots[a].Id
                    });
                }
        }
        var html = Render(league, season);
        Assert.DoesNotContain(html.Descendants(), e => (string?)e.Attribute("role") == "alert");
        Assert.Equal(6, html.Descendants().Count(e => e.Attribute("data-date") != null));
        Assert.Equal(12, html.Descendants().Count(e => e.Attribute("data-home-number") != null));
        Assert.Equal(8 - firstCount - secondCount, html.Descendants("tr").Count(e => e.Value.Contains("BYE")));
        foreach (var division in league.Divisions)
        {
            var key = html.Descendants("table").Single(e => (string?)e.Attribute("data-division-id") == division.Id.ToString())
                .Elements("tr").Where(e => e.Attribute("id") != null)
                .ToDictionary(e => (int)e.Attribute("data-team-number")!, e => Guid.Parse(((string)e.Attribute("id")!)["fixture-team-".Length..]));
            var expanded = html.Descendants().Where(e => e.Attribute("data-home-number") != null)
                .Select(e => (Date: DateTime.Parse((string)e.Ancestors().First(a => a.Attribute("data-date") != null).Attribute("data-date")!),
                    Home: (int)e.Attribute("data-home-number")!, Away: (int)e.Attribute("data-away-number")!))
                .Where(p => key.ContainsKey(p.Home) && key.ContainsKey(p.Away))
                .Select(p => (p.Date, Home: key[p.Home], Away: key[p.Away])).ToHashSet();
            var saved = league.Fixtures.Where(f => f.DivisionId == division.Id)
                .Select(f => (f.Date.Date, Home: f.HomeTeamId, Away: f.AwayTeamId)).ToHashSet();
            Assert.True(saved.SetEquals(expanded));
        }
    }
}
