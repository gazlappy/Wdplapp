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
    public void Generate_VenueWithNoTables_StillHostsFixtures()
    {
        var league = BuildLeague(divisions: 1, teamsPerDivision: 2, venues: 0);

        // One venue without any tables defined
        var venue = new Venue { Id = Guid.NewGuid(), SeasonId = SeasonId, Name = "No Tables Inn" };
        league.Venues.Add(venue);
        foreach (var team in league.Teams)
            team.VenueId = venue.Id;

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick);

        Assert.Equal(2, fixtures.Count); // 2 teams, home & away
        Assert.All(fixtures, f =>
        {
            Assert.Equal(venue.Id, f.VenueId);
            Assert.Null(f.TableId); // implicit-table sentinel must not leak out
        });
    }

    [Fact]
    public void Generate_NoVenuesAtAll_StillProducesFixtures()
    {
        var league = BuildLeague(divisions: 1, teamsPerDivision: 4, venues: 0);

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick);

        Assert.Equal(12, fixtures.Count);
        Assert.All(fixtures, f => Assert.Null(f.VenueId));
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
    public void Generate_TeamsWithoutDivisionId_SingleDivision_AutoAssigned()
    {
        var league = BuildLeague(divisions: 1, teamsPerDivision: 4, venues: 2, tablesPerVenue: 2);
        var division = league.Divisions[0];

        // Simulate teams copied without DivisionId set
        foreach (var team in league.Teams)
            team.DivisionId = null;

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick);

        Assert.Equal(12, fixtures.Count);
        Assert.All(league.Teams, t => Assert.Equal(division.Id, t.DivisionId));
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

        var fixtures = FixtureGenerator.Generate(
            league, SeasonId, Start, DayOfWeek.Tuesday, roundsPerOpponent: 2, kickoff: Kick,
            endDate: endDate);

        // Rounds beyond the end date are not scheduled
        Assert.True(fixtures.Count <= 6, $"Expected at most 6 fixtures, got {fixtures.Count}");
    }
}
