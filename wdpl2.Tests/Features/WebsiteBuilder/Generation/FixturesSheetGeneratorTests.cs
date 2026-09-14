using System.Xml.Linq;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

public class FixturesSheetGeneratorTests
{
    [Fact]
    public void DateCards_UseInclusiveSeasonBoundsForEverySourceWithoutBottomDuplicates()
    {
        var (league, season) = CreateLeague();
        season.EndDate = season.StartDate.AddDays(28);
        league.ActiveSeasonId = season.Id;
        season.IsActive = true;
        var settings = new FixturesSheetSettings();
        var dates = new[] { season.StartDate.AddDays(-1), season.StartDate, season.EndDate.AddHours(23), season.EndDate.AddDays(1) };
        for (var i = 0; i < dates.Length; i++)
        {
            var date = dates[i];
            settings.SpecialEvents.Add(new SpecialEvent { Date = date, Description = $"Manual {i}" });
            season.BlackoutDates.Add(date);
            season.BlackoutDateTitles[date.ToString("yyyy-MM-dd")] = $"Exclusion {i}";
            var competition = new Competition
            {
                SeasonId = season.Id, Name = $"Competition {i}", StartDate = date,
                Rounds = [new() { Name = $"Round {i}", Date = date }]
            };
            league.Competitions.Add(competition);
            league.CalendarEvents.Add(new CalendarEvent { Date = date, Title = $"Linked {i}", CompetitionId = competition.Id });
            league.CalendarEvents.Add(new CalendarEvent { Date = date, Title = $"Calendar {i}", Category = CalendarEventCategory.Competition });
        }
        foreach (var fixture in league.Fixtures.ToList())
            league.Fixtures.Add(new Fixture
            {
                SeasonId = season.Id, DivisionId = fixture.DivisionId,
                HomeTeamId = fixture.HomeTeamId, AwayTeamId = fixture.AwayTeamId,
                Date = fixture.Date.AddDays(-14)
            });
        var before = System.Text.Json.JsonSerializer.Serialize(league);
        var settingsBefore = System.Text.Json.JsonSerializer.Serialize(settings);
        var html = Render(league, season, settings);
        var grid = Assert.Single(html.Elements().Where(e => (string?)e.Attribute("class") == "wk-grid"));
        Assert.Equal(new[] { "2026-09-17", "2026-09-24", "2026-10-15" },
            grid.Elements().Select(e => (string?)e.Attribute("data-date")));
        foreach (var source in new[] { "Manual", "Exclusion", "Competition", "Round", "Linked", "Calendar" })
        {
            Assert.DoesNotContain($"{source} 0", html.Value);
            Assert.DoesNotContain($"{source} 3", html.Value);
            Assert.Contains($"{source} 1", grid.Value);
            Assert.Contains($"{source} 2", grid.Value);
        }
        Assert.DoesNotContain(html.Descendants(), e => ((string?)e.Attribute("class"))?.StartsWith("kd-") == true);
        Assert.Single(html.Descendants().Where(e => e.Value == "Manual 1"));
        var generator = new FixturesSheetGenerator(league, settings);
        Assert.Contains("2 event date(s) included", generator.GetEventVisibilitySummary(season.Id));
        Assert.Contains(html.ToString(SaveOptions.DisableFormatting),
            XElement.Parse(generator.GenerateEmbeddableContent(season.Id)).ToString(SaveOptions.DisableFormatting));
        Assert.Contains(generator.GenerateEmbeddableContent(season.Id), generator.GenerateFixturesSheet(season.Id));
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(league));
        Assert.Equal(settingsBefore, System.Text.Json.JsonSerializer.Serialize(settings));
    }

    [Fact]
    public void EventSummary_DistinguishesHiddenMissingAndIncludedDatesWithoutChangingSettings()
    {
        var (league, season) = CreateLeague();
        var settings = new FixturesSheetSettings { ShowSpecialEvents = false };
        var generator = new FixturesSheetGenerator(league, settings);
        league.Competitions.Add(new Competition
        {
            SeasonId = season.Id, Name = "Cup", StartDate = season.StartDate.AddDays(14),
            Rounds = [new() { Name = "First round", Date = season.StartDate.AddDays(14) }]
        });
        var summary = generator.GetEventVisibilitySummary(season.Id);
        Assert.Contains("Winter: 1 event date(s) found but hidden", summary);
        Assert.Contains("Show competition / event date cards", summary);
        Assert.False(settings.ShowSpecialEvents);
        Assert.Empty(settings.SpecialEvents);
        settings.ShowSpecialEvents = true;
        Assert.Contains("1 event date(s) included", generator.GetEventVisibilitySummary(season.Id));
        league.Competitions.Clear();
        Assert.Contains("no competition/event dates found", generator.GetEventVisibilitySummary(season.Id));
    }

    private static (LeagueData league, Season season) CreateLeague()
    {
        var league = new LeagueData();
        var season = new Season { Name = "Winter", StartDate = new DateTime(2026, 9, 17), EndDate = new DateTime(2027, 6, 3) };
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

    [Theory]
    [InlineData(0)]
    [InlineData(14)]
    public void CalendarCompetition_AppearsOnBothSheetsWithoutChangingSavedData(int days)
    {
        var (league, season) = CreateLeague();
        season.EndDate = season.StartDate.AddDays(28);
        var evt = new CalendarEvent { Date = season.StartDate.AddDays(days).AddHours(20), Title = "Singles & Doubles <Final>", Category = CalendarEventCategory.Competition, Notes = "Private details" };
        league.CalendarEvents.Add(evt);
        var settings = new FixturesSheetSettings();
        var originalFixtures = System.Text.Json.JsonSerializer.Serialize(league.Fixtures);
        var html = Render(league, season, settings);
        var card = Assert.Single(html.Descendants().Where(e => (string?)e.Attribute("data-date") == evt.Date.ToString("yyyy-MM-dd")));
        Assert.Contains(evt.Title, card.Value);
        Assert.DoesNotContain(html.Descendants(), e => (string?)e.Attribute("class") == "kd-grid");
        var standalone = new FixturesSheetGenerator(league, settings).GenerateFixturesSheet(season.Id);
        Assert.Contains("Singles &amp; Doubles &lt;Final&gt;", standalone);
        Assert.DoesNotContain("Private details", standalone);
        Assert.Empty(settings.SpecialEvents);
        Assert.Equal(originalFixtures, System.Text.Json.JsonSerializer.Serialize(league.Fixtures));
        evt.Title = "Changed final";
        Assert.Contains("Changed final", Render(league, season, settings).Value);
        league.CalendarEvents.Clear();
        Assert.DoesNotContain("Changed final", Render(league, season, settings).Value);
    }

    [Fact]
    public void CalendarCompetition_RequiresMatchingSeasonAndInRangeDate()
    {
        var (league, season) = CreateLeague();
        season.EndDate = season.StartDate.AddDays(28);
        var own = new Competition { SeasonId = season.Id };
        var other = new Competition { SeasonId = Guid.NewGuid() };
        league.Competitions.AddRange([own, other]);
        league.CalendarEvents.AddRange([
            new() { Date = season.StartDate.AddDays(35), Title = "Linked final", CompetitionId = own.Id },
            new() { Date = season.StartDate, Title = "Other season final", CompetitionId = other.Id, Category = CalendarEventCategory.Competition },
            new() { Date = season.StartDate.AddDays(-1), Title = "Outside date", Category = CalendarEventCategory.Competition },
            new() { Date = season.StartDate, Title = "Missing competition", CompetitionId = Guid.NewGuid(), Category = CalendarEventCategory.Competition },
            new() { Date = season.StartDate, Title = "Private meeting", Category = CalendarEventCategory.Meeting },
            new() { Date = season.StartDate, Title = "Opening competition", Category = CalendarEventCategory.Competition },
            new() { Date = season.EndDate.AddHours(23), Title = "Closing competition", Category = CalendarEventCategory.Competition }
        ]);
        var html = Render(league, season);
        Assert.DoesNotContain("Linked final", html.Value);
        Assert.Contains("Opening competition", html.Value);
        Assert.Contains("Closing competition", html.Value);
        foreach (var title in new[] { "Other season final", "Outside date", "Missing competition", "Private meeting" })
            Assert.DoesNotContain(title, html.Value);
    }

    [Fact]
    public void CalendarCompetition_DeduplicatesAnnotationsAndRespectsHiddenEvents()
    {
        var (league, season) = CreateLeague();
        season.EndDate = season.StartDate.AddDays(28);
        var date = season.StartDate.AddDays(14);
        league.CalendarEvents.AddRange([
            new() { Date = date, Title = "Cup", Category = CalendarEventCategory.Competition },
            new() { Date = date, Title = "Doubles", Category = CalendarEventCategory.Competition }
        ]);
        var settings = new FixturesSheetSettings { SpecialEvents = [new() { Date = date, Description = "Cup" }] };
        var html = Render(league, season, settings);
        var card = Assert.Single(html.Descendants().Where(e => (string?)e.Attribute("data-date") == date.ToString("yyyy-MM-dd")));
        Assert.Equal(2, card.Descendants().Count(e => (string?)e.Attribute("class") == "wk-event-body"));
        Assert.Single(settings.SpecialEvents);
        settings.ShowSpecialEvents = false;
        html = Render(league, season, settings);
        Assert.DoesNotContain("Cup", html.Value);
        Assert.DoesNotContain("Doubles", html.Value);
        Assert.Equal(2, html.Descendants().Count(e => e.Attribute("data-date") != null));
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

    [Fact]
    public void CompetitionDates_RenderAsChronologicalCardsInMainGrid()
    {
        var (league, season) = CreateLeague();
        var roundDate = season.StartDate.AddDays(21);
        league.Competitions.Add(new Competition
        {
            SeasonId = season.Id, Name = "Singles Cup", StartDate = season.StartDate.AddDays(14),
            Rounds = [new() { Name = "Final", Date = roundDate }]
        });
        league.Competitions.Add(new Competition { SeasonId = Guid.NewGuid(), Name = "Other season cup", StartDate = roundDate });
        league.CalendarEvents.Add(new CalendarEvent { Category = CalendarEventCategory.Competition, Date = roundDate, Title = "Singles Cup — Final" });
        season.EndDate = roundDate.AddDays(7);
        var settings = new FixturesSheetSettings();
        var before = System.Text.Json.JsonSerializer.Serialize(league);
        var html = Render(league, season, settings);
        var grid = html.Elements().Single(e => (string?)e.Attribute("class") == "wk-grid");
        var cards = grid.Elements().ToList();
        Assert.Equal(new[] { "2026-09-17", "2026-09-24", "2026-10-01", "2026-10-08" },
            cards.Select(e => (string?)e.Attribute("data-date")));
        foreach (var card in cards.Skip(2))
        {
            Assert.Equal("wk-card wk-card-event", (string?)card.Attribute("class"));
            var header = card.Elements().Single(e => (string?)e.Attribute("class") == "wk-hdr");
            Assert.Contains(header.Elements(), e => (string?)e.Attribute("class") == "wk-day");
            Assert.Contains(header.Elements(), e => (string?)e.Attribute("class") == "wk-month");
        }
        Assert.Equal("Singles Cup — Final", Assert.Single(cards[3].Elements().Where(e => (string?)e.Attribute("class") == "wk-event-body")).Value);
        Assert.DoesNotContain("Other season cup", html.Value);
        Assert.Contains(grid.ToString(SaveOptions.DisableFormatting),
            XElement.Parse(new FixturesSheetGenerator(league, settings).GenerateEmbeddableContent(season.Id)).ToString(SaveOptions.DisableFormatting));
        Assert.Contains("data-date=\"2026-10-08\"", new FixturesSheetGenerator(league, settings).GenerateFixturesSheet(season.Id));
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(league));
        settings.ShowSpecialEvents = false;
        Assert.Equal(2, Render(league, season, settings).Descendants().Count(e => e.Attribute("data-date") != null));
    }

    [Fact]
    public void SeasonCompetitionExclusions_RenderWithoutManualSyncAndKeepLeaguePairings()
    {
        var (league, season) = CreateLeague();
        var date = season.StartDate.AddDays(14);
        season.BlackoutDates.AddRange([season.StartDate, date]);
        season.BlackoutDateTitles[season.StartDate.ToString("yyyy-MM-dd")] = "Cup draw";
        season.BlackoutDateTitles[date.ToString("yyyy-MM-dd")] = "Doubles competition";
        var settings = new FixturesSheetSettings();
        var html = Render(league, season, settings);
        var grid = html.Elements().Single(e => (string?)e.Attribute("class") == "wk-grid");
        Assert.Equal(3, grid.Elements().Count());
        Assert.Equal(2, grid.Descendants().Count(e => e.Attribute("data-home-number") != null));
        Assert.Contains("Cup draw", grid.Elements().First().Value);
        Assert.Equal("wk-card wk-card-event", (string?)grid.Elements().Last().Attribute("class"));
        Assert.Contains("Doubles competition", grid.Elements().Last().Value);
        Assert.Empty(settings.SpecialEvents);
        season.BlackoutDateTitles[date.ToString("yyyy-MM-dd")] = "Revised cup night";
        Assert.Contains("Revised cup night", Render(league, season, settings).Value);
        season.BlackoutDates.Remove(date);
        Assert.DoesNotContain("Revised cup night", Render(league, season, settings).Value);
    }

    [Fact]
    public void SavedHomeTableClash_IsRejectedBeforeRenderingNumbers()
    {
        var (league, season) = CreateLeague();
        var firstNight = league.Fixtures.Where(f => f.Date.Date == season.StartDate.Date).ToList();
        var venue = Guid.NewGuid();
        var table = Guid.NewGuid();
        foreach (var fixture in firstNight)
        {
            var home = league.Teams.Single(t => t.Id == fixture.HomeTeamId);
            home.VenueId = fixture.VenueId = venue;
            home.TableId = fixture.TableId = table;
        }
        var html = Render(league, season);
        Assert.Contains("home table is double-booked", html.Value);
        Assert.Empty(html.Descendants().Where(e => e.Attribute("data-home-number") != null));
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
        var pattern = new[] { (1, 2), (3, 4), (4, 1), (2, 3), (1, 3), (4, 2) };
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
