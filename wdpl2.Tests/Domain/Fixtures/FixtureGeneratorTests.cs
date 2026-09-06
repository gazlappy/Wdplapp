using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for FixtureGenerator — multi-division, multi-venue round-robin scheduling.
/// </summary>
public class FixtureGeneratorTests
{
    private static readonly Guid SeasonId = Guid.NewGuid();
    private static readonly DateTime Start = new(2025, 9, 2); // a Tuesday
    private static readonly TimeSpan Kick = new(19, 30, 0);

    private static LeagueData BuildLeague(
        int divisions,
        int teamsPerDivision,
        int venues,
        int tablesPerVenue = 1,
        Guid? seasonId = null)
    {
        var sid = seasonId ?? SeasonId;
        var league = new LeagueData();
        league.Seasons.Add(new Season
        {
            Id = sid,
            Name = "Test Season",
            StartDate = Start,
            EndDate = Start.AddMonths(8),
            IsActive = true
        });

        var venueList = new List<Venue>();
        for (int v = 0; v < venues; v++)
        {
            var venue = new Venue
            {
                Id = Guid.NewGuid(),
                SeasonId = sid,
                Name = $"Venue {v + 1}"
            };
            for (int t = 0; t < tablesPerVenue; t++)
                venue.Tables.Add(new VenueTable { Id = Guid.NewGuid(), Label = $"T{t + 1}" });
            venueList.Add(venue);
            league.Venues.Add(venue);
        }

        int teamIndex = 0;
        for (int d = 0; d < divisions; d++)
        {
            var division = new Division
            {
                Id = Guid.NewGuid(),
                SeasonId = sid,
                Name = $"Division {d + 1}"
            };
            league.Divisions.Add(division);

            for (int t = 0; t < teamsPerDivision; t++)
            {
                var venue = venueList.Count > 0 ? venueList[teamIndex % venueList.Count] : null;
                league.Teams.Add(new Team
                {
                    Id = Guid.NewGuid(),
                    SeasonId = sid,
                    DivisionId = division.Id,
                    VenueId = venue?.Id,
                    TableId = venue?.Tables[(teamIndex / venueList.Count) % tablesPerVenue].Id,
                    Name = $"Team {d + 1}.{t + 1}"
                });
                teamIndex++;
            }
        }

        return league;
    }

    [Fact]
    public void Generate_NullLeague_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FixtureGenerator.Generate(null!, SeasonId, Start, DayOfWeek.Tuesday));
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    [InlineData(8, 7)]
    [InlineData(8, 6)]
    [InlineData(5, 3)]
    public void Generate_SharedTables_AlwaysProducesSharedSheet(int largest, int smaller)
    {
        var league = BuildLeague(2, largest, largest);
        var second = league.Divisions[1].Id;
        foreach (var team in league.Teams.Where(t => t.DivisionId == second).Skip(smaller).ToList())
            league.Teams.Remove(team);
        var fixtures = FixtureGenerator.Generate(league, SeasonId, Start, DayOfWeek.Tuesday);
        Assert.Equal(largest * (largest - 1) + smaller * (smaller - 1), fixtures.Count);
        league.Fixtures.AddRange(fixtures);
        var shared = SharedFixtureSheetSchedule.Create(league.Divisions, league.Teams, fixtures);
        Assert.Equal(league.Teams.Count, shared.TeamNumbers.Count);
        var html = new FixturesSheetGenerator(league, new FixturesSheetSettings()).GenerateEmbeddableContent(SeasonId);
        Assert.DoesNotContain("sheet-error", html);
        foreach (var night in fixtures.GroupBy(f => f.Date.Date))
        {
            Assert.Equal(night.Count() * 2, night.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().Count());
            Assert.Equal(night.Count(), night.Select(f => (f.VenueId, f.TableId)).Distinct().Count());
        }
    }

    [Fact]
    public void Generate_SingleDivision_ProducesFullDoubleRoundRobin()
    {
        var league = BuildLeague(divisions: 1, teamsPerDivision: 4, venues: 4, tablesPerVenue: 2);

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick);

        // 4 teams, double round robin: 4 * 3 = 12 fixtures
        Assert.Equal(12, fixtures.Count);

        // Every team plays every other team exactly twice (home and away once each)
        var teams = league.Teams.Select(t => t.Id).ToList();
        foreach (var a in teams)
            foreach (var b in teams.Where(b => b != a))
                Assert.Equal(1, fixtures.Count(f => f.HomeTeamId == a && f.AwayTeamId == b));
    }

    [Fact]
    public void Generate_MultiDivision_AllDivisionsGetFixtures()
    {
        var league = BuildLeague(divisions: 3, teamsPerDivision: 4, venues: 6, tablesPerVenue: 2);

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick);

        // Each division: 4 teams double round-robin = 12 fixtures, 3 divisions = 36
        Assert.Equal(36, fixtures.Count);

        foreach (var division in league.Divisions)
        {
            var divFixtures = fixtures.Where(f => f.DivisionId == division.Id).ToList();
            Assert.Equal(12, divFixtures.Count);

            // Fixtures only pair teams within the same division
            var divTeamIds = league.Teams.Where(t => t.DivisionId == division.Id).Select(t => t.Id).ToHashSet();
            Assert.All(divFixtures, f =>
            {
                Assert.Contains(f.HomeTeamId, divTeamIds);
                Assert.Contains(f.AwayTeamId, divTeamIds);
            });
        }
    }

    [Fact]
    public void Generate_AllFixturesStampedWithSeasonId()
    {
        var league = BuildLeague(divisions: 2, teamsPerDivision: 4, venues: 4);

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick);

        Assert.NotEmpty(fixtures);
        Assert.All(fixtures, f => Assert.Equal(SeasonId, f.SeasonId));
    }

    [Fact]
    public void Generate_NoTeamDoubleBookedOnSameNight()
    {
        var league = BuildLeague(divisions: 3, teamsPerDivision: 6, venues: 9, tablesPerVenue: 1);

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick);

        foreach (var nightGroup in fixtures.GroupBy(f => f.Date.Date))
        {
            var seen = new HashSet<Guid>();
            foreach (var fx in nightGroup)
            {
                Assert.True(seen.Add(fx.HomeTeamId),
                    $"Home team double-booked on {nightGroup.Key:yyyy-MM-dd}");
                Assert.True(seen.Add(fx.AwayTeamId),
                    $"Away team double-booked on {nightGroup.Key:yyyy-MM-dd}");
            }
        }
    }

    [Fact]
    public void Generate_NoVenueTableDoubleBookedOnSameNight()
    {
        var league = BuildLeague(divisions: 2, teamsPerDivision: 4, venues: 4, tablesPerVenue: 1);

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick);

        foreach (var nightGroup in fixtures.GroupBy(f => f.Date.Date))
        {
            var slots = nightGroup
                .Where(f => f.VenueId.HasValue)
                .Select(f => (f.VenueId!.Value, f.TableId))
                .ToList();
            Assert.Equal(slots.Count, slots.Distinct().Count());
        }
    }

    [Fact]
    public void Generate_IgnoresVenuesFromOtherSeasons()
    {
        var league = BuildLeague(divisions: 1, teamsPerDivision: 4, venues: 2, tablesPerVenue: 2);

        // Add a venue belonging to a different season
        var otherSeasonVenue = new Venue
        {
            Id = Guid.NewGuid(),
            SeasonId = Guid.NewGuid(),
            Name = "AAA Other Season Venue" // sorts first alphabetically
        };
        otherSeasonVenue.Tables.Add(new VenueTable { Id = Guid.NewGuid(), Label = "T1" });
        league.Venues.Add(otherSeasonVenue);

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick);

        Assert.NotEmpty(fixtures);
        Assert.All(fixtures, f => Assert.NotEqual(otherSeasonVenue.Id, f.VenueId));
    }

    [Fact]
    public void Generate_VenueWithNoTables_BlocksGeneration()
    {
        var league = BuildLeague(divisions: 1, teamsPerDivision: 2, venues: 0);

        // One venue without any tables defined
        var venue = new Venue { Id = Guid.NewGuid(), SeasonId = SeasonId, Name = "No Tables Inn" };
        league.Venues.Add(venue);
        foreach (var team in league.Teams)
            team.VenueId = venue.Id;

        Assert.Throws<InvalidOperationException>(() => FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick));
    }

    [Fact]
    public void Generate_NoVenuesAtAll_BlocksGeneration()
    {
        var league = BuildLeague(divisions: 1, teamsPerDivision: 4, venues: 0);

        Assert.Throws<InvalidOperationException>(() => FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick));
    }

    [Fact]
    public void Generate_RespectsBlackoutDates()
    {
        var league = BuildLeague(divisions: 1, teamsPerDivision: 4, venues: 4, tablesPerVenue: 2);
        var blackout = Start; // first match night is blacked out

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick,
            blackoutDates: new[] { blackout });

        Assert.NotEmpty(fixtures);
        Assert.All(fixtures, f => Assert.NotEqual(blackout.Date, f.Date.Date));
    }

    [Fact]
    public void Generate_AllFixturesLandOnMatchNight()
    {
        var league = BuildLeague(divisions: 2, teamsPerDivision: 4, venues: 4, tablesPerVenue: 2);

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Thursday, roundsPerOpponent: 2, kickoff: Kick);

        Assert.NotEmpty(fixtures);
        Assert.All(fixtures, f => Assert.Equal(DayOfWeek.Thursday, f.Date.DayOfWeek));
    }

    [Fact]
    public void Generate_OddTeamCount_ByeRotatesAndAllPairingsPlayed()
    {
        var league = BuildLeague(divisions: 1, teamsPerDivision: 5, venues: 5, tablesPerVenue: 2);

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick);

        // 5 teams double round-robin: 5 * 4 = 20 fixtures
        Assert.Equal(20, fixtures.Count);

        // No fixture may involve the bye placeholder (Guid.Empty)
        Assert.All(fixtures, f =>
        {
            Assert.NotEqual(Guid.Empty, f.HomeTeamId);
            Assert.NotEqual(Guid.Empty, f.AwayTeamId);
        });
    }

    [Fact]
    public void Generate_SingleRound_ProducesSingleRoundRobin()
    {
        var league = BuildLeague(divisions: 1, teamsPerDivision: 4, venues: 4, tablesPerVenue: 2);

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 1, kickoff: Kick);

        // 4 teams single round robin: 6 fixtures
        Assert.Equal(6, fixtures.Count);
    }

    [Fact]
    public void Generate_TeamsWithoutDivisionId_BlocksWithoutMutatingTeams()
    {
        var league = BuildLeague(divisions: 1, teamsPerDivision: 4, venues: 2, tablesPerVenue: 2);
        var division = league.Divisions[0];

        // Simulate teams copied without DivisionId set
        foreach (var team in league.Teams)
            team.DivisionId = null;

        Assert.Throws<InvalidOperationException>(() => FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick));
        Assert.All(league.Teams, t => Assert.Null(t.DivisionId));
    }

    [Fact]
    public void Generate_SharedVenue_MultipleTablesUsedConcurrently()
    {
        // 4 teams all share one venue with 2 tables: both round-robin games
        // per night need both tables.
        var league = BuildLeague(divisions: 1, teamsPerDivision: 4, venues: 1, tablesPerVenue: 2);
        var venue = league.Venues[0];

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick);

        Assert.Equal(12, fixtures.Count);
        Assert.All(fixtures, f => Assert.Equal(venue.Id, f.VenueId));

        // At least one night must use both tables simultaneously
        var anyNightWithBothTables = fixtures
            .GroupBy(f => f.Date.Date)
            .Any(g => g.Select(f => f.TableId).Distinct().Count() == 2);
        Assert.True(anyNightWithBothTables, "Expected both tables in use on the same night");
    }

    [Fact]
    public void Generate_RespectsEndDate()
    {
        var league = BuildLeague(divisions: 1, teamsPerDivision: 4, venues: 4, tablesPerVenue: 2);
        var endDate = Start.AddDays(14); // only 3 match nights available

        var error = Assert.Throws<InvalidOperationException>(() => FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick,
            endDate: endDate));
        Assert.Contains("6 match nights", error.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Generate_SharedTablesAcrossDivisions_CompleteNightsAndExactHomeTables(int legs)
    {
        var league = BuildLeague(2, 4, 4);
        var fixtures = FixtureGenerator.Generate(league, SeasonId, Start, DayOfWeek.Tuesday, legs, Kick);
        Assert.Equal(12 * legs, fixtures.Count);
        Assert.Equal(3 * legs, fixtures.Select(f => f.Date.Date).Distinct().Count());
        foreach (var night in fixtures.GroupBy(f => f.Date.Date))
        {
            Assert.Equal(4, night.Count());
            Assert.Equal(8, night.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().Count());
            Assert.Equal(4, night.Select(f => (f.VenueId, f.TableId)).Distinct().Count());
        }
        Assert.All(fixtures, f =>
        {
            var home = league.Teams.Single(t => t.Id == f.HomeTeamId);
            Assert.Equal(home.VenueId, f.VenueId);
            Assert.Equal(home.TableId, f.TableId);
        });
    }

    [Fact]
    public void Generate_InsufficientHomeTableCapacity_RejectsWithoutUsingSpareVenue()
    {
        var league = BuildLeague(1, 4, 1);
        league.Venues.Add(new Venue { SeasonId = SeasonId, Name = "Spare", Tables = new() { new VenueTable() } });
        var existing = new Fixture { SeasonId = SeasonId };
        league.Fixtures.Add(existing);
        Assert.Throws<InvalidOperationException>(() => FixtureGenerator.Generate(league, SeasonId, Start, DayOfWeek.Tuesday));
        Assert.Same(existing, Assert.Single(league.Fixtures));
    }

    [Theory]
    [InlineData("table")]
    [InlineData("venue")]
    [InlineData("division")]
    [InlineData("identity")]
    [InlineData("locked")]
    public void Generate_InvalidSetup_Blocks(string problem)
    {
        var league = BuildLeague(1, 4, 4);
        switch (problem)
        {
            case "table": league.Teams[0].TableId = league.Teams[1].TableId; break;
            case "venue": league.Venues[0].SeasonId = Guid.NewGuid(); break;
            case "division": league.Divisions[0].SeasonId = Guid.NewGuid(); break;
            case "identity": league.Teams[0].GlobalTeamId = league.Teams[1].GlobalTeamId = Guid.NewGuid(); break;
            case "locked": league.Seasons[0].IsLocked = true; break;
        }
        Assert.Throws<InvalidOperationException>(() => FixtureGenerator.Generate(league, SeasonId, Start, DayOfWeek.Tuesday));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("wrongTable")]
    [InlineData("wrongNight")]
    [InlineData("overflow")]
    [InlineData("splitRound")]
    public void Validator_RejectsCorruptedSchedule(string problem)
    {
        var league = BuildLeague(1, 4, 4);
        var fixtures = FixtureGenerator.Generate(league, SeasonId, Start, DayOfWeek.Tuesday);
        switch (problem)
        {
            case "duplicate": fixtures.Add(fixtures[0]); break;
            case "missing": fixtures.RemoveAt(0); break;
            case "wrongTable": fixtures[0].TableId = fixtures[1].TableId; break;
            case "wrongNight": fixtures[0].Date = fixtures[0].Date.AddDays(1); break;
            case "overflow": fixtures[0].Date = fixtures[0].Date.AddYears(1); break;
            case "splitRound": fixtures[0].Date = fixtures[0].Date.AddDays(42); break;
        }
        Assert.Throws<InvalidOperationException>(() => GeneratedScheduleValidator.Validate(league, SeasonId,
            fixtures, Start, league.Seasons[0].EndDate, DayOfWeek.Tuesday, Kick, 2, Array.Empty<DateTime>()));
    }

    [Fact]
    public void Generate_MoreThan52Blackouts_NeverUsesExcludedDate()
    {
        var league = BuildLeague(1, 2, 2);
        var excluded = Enumerable.Range(0, 60).Select(i => Start.AddDays(i * 7)).ToList();
        var fixtures = FixtureGenerator.Generate(league, SeasonId, Start, DayOfWeek.Tuesday,
            endDate: Start.AddDays(63 * 7), blackoutDates: excluded);
        Assert.Equal(2, fixtures.Count);
        Assert.Equal(Start.AddDays(60 * 7), fixtures[0].Date.Date);
    }
}
