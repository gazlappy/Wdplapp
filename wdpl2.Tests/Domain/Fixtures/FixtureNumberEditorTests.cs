using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

public class FixtureNumberEditorTests
{
    internal static (LeagueData data, Guid seasonId, List<Fixture> fixtures) Setup(bool crossDivision = false, int secondCount = 4)
    {
        var data = new LeagueData();
        var season = new Season { Name = "Number review", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 5, 1) };
        data.Seasons.Add(season);
        var first = new Division { SeasonId = season.Id, Name = "First" };
        var second = new Division { SeasonId = season.Id, Name = "Second" };
        data.Divisions.AddRange(new[] { first, second });
        foreach (var division in data.Divisions)
        {
            int count = division.Id == first.Id ? 4 : secondCount;
            for (int i = 0; i < count; i++)
            {
                var venue = crossDivision && division.Id == second.Id ? data.Venues[i]
                    : new Venue { SeasonId = season.Id, Name = $"{division.Name} Venue {i}", Tables = new() { new VenueTable { Label = "1" } } };
                if (!data.Venues.Contains(venue)) data.Venues.Add(venue);
                data.Teams.Add(new Team { SeasonId = season.Id, DivisionId = division.Id, Name = $"{division.Name} Team {i}", VenueId = venue.Id, TableId = venue.Tables[0].Id });
            }
        }
        var fixtures = FixtureGenerator.Generate(data, season.Id, season.StartDate, DayOfWeek.Tuesday);
        return (data, season.Id, fixtures);
    }

    [Fact]
    public void Swap_MovesLocalBlocks_PreservesOtherDivisionAndRoundTrips()
    {
        var (data, season, fixtures) = Setup();
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        var original = JsonSerializer.Serialize(fixtures);
        var before = editor.Numbers;
        var division = data.Divisions[0].Id;
        editor.Swap(division, 1, 3);
        foreach (var team in data.Teams)
            Assert.Equal(team.DivisionId == division ? (before[team.Id] <= 2 ? before[team.Id] + 2 : before[team.Id] - 2) : before[team.Id], editor.Numbers[team.Id]);
        Assert.Equal(original, JsonSerializer.Serialize(fixtures));
        foreach (var fixture in fixtures.Where(f => f.DivisionId != division))
            Assert.Equal(JsonSerializer.Serialize(fixture), JsonSerializer.Serialize(editor.Fixtures.Single(f => f.Id == fixture.Id)));
        Assert.Equal(fixtures.Select(f => f.Id).OrderBy(id => id), editor.Fixtures.Select(f => f.Id).OrderBy(id => id));
        Assert.True(editor.HasChanges);
        Assert.Contains("→", editor.Review());
        Assert.Equal(editor.Numbers.OrderBy(p => p.Key), SharedFixtureSheetSchedule.Create(data.Divisions, data.Teams, editor.Fixtures).TeamNumbers.OrderBy(p => p.Key));
        Assert.NotEmpty(editor.ValidateForSave(data, fixtures));
        editor.Reset();
        Assert.False(editor.HasChanges);
        Assert.Equal(before.OrderBy(p => p.Key), editor.Numbers.OrderBy(p => p.Key));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstBye_PreviewsAndAppliesEveryTeamChoiceWithoutClashes(bool linked)
    {
        var (data, season, fixtures) = Setup(linked, secondCount: 3);
        var division = data.Divisions[1].Id;
        foreach (var team in data.Teams.Where(t => t.DivisionId == division))
        {
            var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
            bool alreadyBye = editor.OpeningByeTeams(division).Any(t => t.Id == team.Id);
            var before = JsonSerializer.Serialize(editor.Fixtures);
            var proposal = editor.PrepareFirstBye(division, team.Id);
            Assert.Contains(team.Name, proposal.Summary);
            Assert.Equal(before, JsonSerializer.Serialize(editor.Fixtures));
            Assert.False(editor.HasChanges);
            Assert.Equal(linked && !alreadyBye, proposal.IncludesOtherDivisions);
            Assert.DoesNotContain(proposal.PreviewFixtures, f => f.Date.Date == editor.OpeningDate && (f.HomeTeamId == team.Id || f.AwayTeamId == team.Id));
            editor.ApplySwap(proposal);
            Assert.Contains(editor.OpeningByeTeams(division), t => t.Id == team.Id);
            Assert.Equal(fixtures.Count, editor.Fixtures.Count);
            Assert.NotEmpty(editor.ValidateForSave(data, fixtures));
            Assert.Equal(editor.Numbers.OrderBy(p => p.Key), SharedFixtureSheetSchedule.Create(data.Divisions, data.Teams, editor.Fixtures).TeamNumbers.OrderBy(p => p.Key));
            foreach (var night in editor.Fixtures.GroupBy(f => f.Date.Date))
            {
                Assert.Equal(night.Count(), night.Select(f => (f.VenueId, f.TableId)).Distinct().Count());
                Assert.Equal(night.Count() * 2, night.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().Count());
            }
            if (!linked)
                foreach (var fixture in fixtures.Where(f => f.DivisionId != division))
                    Assert.Equal(JsonSerializer.Serialize(fixture), JsonSerializer.Serialize(editor.Fixtures.Single(f => f.Id == fixture.Id)));
            editor.Reset();
            Assert.Equal(before, JsonSerializer.Serialize(editor.Fixtures));
            Assert.Throws<InvalidOperationException>(() => editor.ApplySwap(proposal));
        }
    }

    [Fact]
    public void FirstBye_FullDivisionAndWrongTeamAreRejectedWithoutMutation()
    {
        var (data, season, fixtures) = Setup(secondCount: 3);
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        Assert.Contains("no BYE slots", Assert.Throws<InvalidOperationException>(() => editor.PrepareFirstBye(data.Divisions[0].Id, data.Teams[0].Id)).Message);
        Assert.Throws<InvalidOperationException>(() => editor.PrepareFirstBye(data.Divisions[1].Id, data.Teams[0].Id));
        Assert.Throws<InvalidOperationException>(() => editor.PrepareFirstBye(Guid.NewGuid(), Guid.NewGuid()));
        Assert.False(editor.HasChanges);
    }

    [Fact]
    public void FirstBye_LocalTablePartnersCannotBeSeparated()
    {
        var (data, season, fixtures) = Setup(secondCount: 3);
        var division = data.Divisions[1].Id;
        var opening = fixtures.Where(f => f.DivisionId == division).OrderBy(f => f.Date).First();
        var home = data.Teams.Single(t => t.Id == opening.HomeTeamId);
        var away = data.Teams.Single(t => t.Id == opening.AwayTeamId);
        away.VenueId = home.VenueId;
        away.TableId = home.TableId;
        foreach (var fixture in fixtures.Where(f => f.HomeTeamId == away.Id))
        {
            fixture.VenueId = home.VenueId;
            fixture.TableId = home.TableId;
        }
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        var before = JsonSerializer.Serialize(editor.Fixtures);
        foreach (var team in new[] { home, away })
            Assert.Contains("shares a table", Assert.Throws<InvalidOperationException>(() => editor.PrepareFirstBye(division, team.Id)).Message);
        Assert.Equal(before, JsonSerializer.Serialize(editor.Fixtures));
    }

    [Fact]
    public void FirstBye_UsesCurrentDraftAfterEarlierNumberChanges()
    {
        var (data, season, fixtures) = Setup(true, secondCount: 3);
        var division = data.Divisions[1].Id;
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        editor.ApplySwap(editor.PrepareSwap(division, 1, 4));
        var bye = editor.OpeningByeTeams(division).Single();
        var selected = data.Teams.First(t => t.DivisionId == division && t.Id != bye.Id);
        var baseline = JsonSerializer.Serialize(editor.Fixtures);
        var proposal = editor.PrepareFirstBye(division, selected.Id);
        Assert.Equal(baseline, JsonSerializer.Serialize(editor.Fixtures));
        editor.ApplySwap(proposal);
        Assert.Equal(selected.Id, editor.OpeningByeTeams(division).Single().Id);
        Assert.NotEmpty(editor.ValidateForSave(data, fixtures));
    }

    [Fact]
    public void CrossDivisionPartner_MoveRejectedWithoutChangingEitherDivision()
    {
        var (data, season, fixtures) = Setup(true);
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        var before = JsonSerializer.Serialize(editor.Fixtures);
        var error = Assert.Throws<InvalidOperationException>(() => editor.Swap(data.Divisions[0].Id, 1, 3));
        Assert.Contains("Other divisions will not be changed", error.Message);
        Assert.Equal(before, JsonSerializer.Serialize(editor.Fixtures));
        Assert.False(editor.HasChanges);
    }

    [Theory]
    [InlineData(false, 1, 2)]
    [InlineData(false, 2, 1)]
    [InlineData(false, 3, 4)]
    [InlineData(false, 1, 4)]
    [InlineData(false, 2, 3)]
    [InlineData(true, 1, 2)]
    [InlineData(true, 1, 4)]
    public void ReversalProposal_PreservesPartnersAndRoundTrips(bool linked, int from, int to)
    {
        var (data, season, fixtures) = Setup(linked);
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        var division = data.Divisions[0].Id;
        var before = editor.Numbers;
        var baseline = JsonSerializer.Serialize(editor.Fixtures);
        var proposal = editor.PrepareSwap(division, from, to);
        Assert.Equal(linked, proposal.IncludesOtherDivisions);
        Assert.Equal(baseline, JsonSerializer.Serialize(editor.Fixtures));
        Assert.NotEmpty(proposal.FixtureChanges);
        var expected = proposal.PreviewNumbers;
        var expectedFixtures = JsonSerializer.Serialize(proposal.PreviewFixtures);
        proposal.PreviewNumbers.Clear();
        proposal.PreviewFixtures.Clear();
        Assert.Equal(expected.Count, proposal.PreviewNumbers.Count);
        Assert.Equal(expectedFixtures, JsonSerializer.Serialize(proposal.PreviewFixtures));
        editor.ApplySwap(proposal);
        Assert.Equal(expectedFixtures, JsonSerializer.Serialize(editor.Fixtures));
        static int Partner(int number) => number % 2 == 1 ? number + 1 : number - 1;
        foreach (var team in data.Teams)
        {
            int number = before[team.Id];
            int destination = number == from ? to : number == to ? from
                : number == Partner(from) ? Partner(to) : number == Partner(to) ? Partner(from) : number;
            Assert.Equal(linked || team.DivisionId == division ? destination : number, editor.Numbers[team.Id]);
        }
        if (!linked)
            foreach (var fixture in fixtures.Where(f => f.DivisionId != division))
                Assert.Equal(JsonSerializer.Serialize(fixture), JsonSerializer.Serialize(editor.Fixtures.Single(f => f.Id == fixture.Id)));
        foreach (var night in editor.Fixtures.GroupBy(f => f.Date.Date))
        {
            Assert.Equal(night.Count(), night.Select(f => (f.VenueId, f.TableId)).Distinct().Count());
            Assert.Equal(night.Count() * 2, night.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().Count());
        }
        Assert.NotEmpty(editor.ValidateForSave(data, fixtures));
        Assert.Equal(expected.OrderBy(p => p.Key), SharedFixtureSheetSchedule.Create(data.Divisions, data.Teams, editor.Fixtures).TeamNumbers.OrderBy(p => p.Key));
        editor.ApplySwap(editor.PrepareSwap(division, from, to));
        Assert.False(editor.HasChanges);
        Assert.Equal(before.OrderBy(p => p.Key), editor.Numbers.OrderBy(p => p.Key));
    }

    [Fact]
    public void LinkedReversal_RequiresConsent_AndResetInvalidatesPreview()
    {
        var (data, season, fixtures) = Setup(true);
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        Assert.Throws<InvalidOperationException>(() => editor.Swap(data.Divisions[0].Id, 1, 2));
        var proposal = editor.PrepareSwap(data.Divisions[0].Id, 1, 2);
        Assert.False(editor.HasChanges);
        editor.Reset();
        Assert.Throws<InvalidOperationException>(() => editor.ApplySwap(proposal));
        Assert.False(editor.HasChanges);
    }

    [Fact]
    public void ByePair_CanReverseWithoutLosingFixtures()
    {
        var (data, season, fixtures) = Setup(secondCount: 3);
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        var division = data.Divisions[1].Id;
        var used = data.Teams.Where(t => t.DivisionId == division).Select(t => editor.Numbers[t.Id]).ToHashSet();
        int bye = Enumerable.Range(1, editor.SlotCount).Single(n => !used.Contains(n));
        int partner = bye % 2 == 1 ? bye + 1 : bye - 1;
        var team = data.Teams.Single(t => t.DivisionId == division && editor.Numbers[t.Id] == partner);
        editor.Swap(division, partner, bye);
        Assert.Equal(bye, editor.Numbers[team.Id]);
        Assert.Equal(fixtures.Count, editor.Fixtures.Count);
        Assert.NotEmpty(editor.ValidateForSave(data, fixtures));
    }

    [Fact]
    public void CombinedProposal_DoesNotChangeDraftUntilAccepted_AndResetsTogether()
    {
        var (data, season, fixtures) = Setup(true);
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        var before = JsonSerializer.Serialize(editor.Fixtures);
        var numbers = editor.Numbers;
        var proposal = editor.PrepareSwap(data.Divisions[0].Id, 1, 3);
        Assert.True(proposal.IncludesOtherDivisions);
        foreach (var division in data.Divisions) Assert.Contains(division.Name, proposal.Summary);
        foreach (var team in data.Teams) Assert.Contains(team.Name, proposal.Summary);
        Assert.Equal(before, JsonSerializer.Serialize(editor.Fixtures));
        Assert.Equal(numbers.OrderBy(p => p.Key), editor.Numbers.OrderBy(p => p.Key));
        Assert.False(editor.HasChanges);
        editor.ApplySwap(proposal);
        foreach (var team in data.Teams)
            Assert.Equal(numbers[team.Id] <= 2 ? numbers[team.Id] + 2 : numbers[team.Id] - 2, editor.Numbers[team.Id]);
        Assert.NotEmpty(editor.ValidateForSave(data, fixtures));
        Assert.Equal(editor.Numbers.OrderBy(p => p.Key), SharedFixtureSheetSchedule.Create(data.Divisions, data.Teams, editor.Fixtures).TeamNumbers.OrderBy(p => p.Key));
        editor.Reset();
        Assert.Equal(before, JsonSerializer.Serialize(editor.Fixtures));
        Assert.False(editor.HasChanges);
    }

    [Fact]
    public void Proposal_RejectsStaleOrForeignApplicationWithoutMutation()
    {
        var (data, season, fixtures) = Setup(true);
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        var proposal = editor.PrepareSwap(data.Divisions[0].Id, 1, 3);
        var other = new FixtureNumberEditor(data, season, fixtures, fixtures);
        Assert.Throws<InvalidOperationException>(() => other.ApplySwap(proposal));
        Assert.False(other.HasChanges);
        editor.Reset();
        Assert.Throws<InvalidOperationException>(() => editor.ApplySwap(proposal));
        Assert.False(editor.HasChanges);
        var current = editor.PrepareSwap(data.Divisions[0].Id, 1, 3);
        editor.ApplySwap(current);
        var before = JsonSerializer.Serialize(editor.Fixtures);
        Assert.Throws<InvalidOperationException>(() => editor.ApplySwap(current));
        Assert.Equal(before, JsonSerializer.Serialize(editor.Fixtures));
    }

    [Fact]
    public void LocalProposal_DoesNotRequireOtherDivisionConsent()
    {
        var (data, season, fixtures) = Setup();
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        var proposal = editor.PrepareSwap(data.Divisions[0].Id, 1, 3);
        Assert.False(proposal.IncludesOtherDivisions);
        editor.ApplySwap(proposal);
        foreach (var fixture in fixtures.Where(f => f.DivisionId == data.Divisions[1].Id))
            Assert.Equal(JsonSerializer.Serialize(fixture), JsonSerializer.Serialize(editor.Fixtures.Single(f => f.Id == fixture.Id)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CombinedProposal_FollowsCascadingPartnersAndCycles_LeavingUnrelatedDivisionUnchanged(bool cycle)
    {
        var (data, season, fixtures) = Setup();
        for (int index = 2; index < 4; index++)
        {
            var division = new Division { SeasonId = season, Name = $"Division {index}" };
            data.Divisions.Add(division);
            for (int i = 0; i < 4; i++)
            {
                var venue = new Venue { SeasonId = season, Name = $"Venue {index}-{i}", Tables = new() { new VenueTable { Label = "1" } } };
                data.Venues.Add(venue);
                data.Teams.Add(new Team { SeasonId = season, DivisionId = division.Id, Name = $"Team {index}-{i}", VenueId = venue.Id, TableId = venue.Tables[0].Id });
            }
        }
        fixtures = FixtureGenerator.Generate(data, season, data.Seasons[0].StartDate, DayOfWeek.Tuesday);
        var numbers = SharedFixtureSheetSchedule.Create(data.Divisions, data.Teams, fixtures).TeamNumbers;
        void Share(int firstDivision, int firstNumber, int secondDivision, int secondNumber)
        {
            var first = data.Teams.Single(t => t.DivisionId == data.Divisions[firstDivision].Id && numbers[t.Id] == firstNumber);
            var second = data.Teams.Single(t => t.DivisionId == data.Divisions[secondDivision].Id && numbers[t.Id] == secondNumber);
            second.VenueId = first.VenueId;
            second.TableId = first.TableId;
            foreach (var fixture in fixtures.Where(f => f.HomeTeamId == second.Id))
            {
                fixture.VenueId = second.VenueId;
                fixture.TableId = second.TableId;
            }
        }
        Share(0, 1, 1, 2);
        Share(1, 3, 2, 4);
        if (cycle) Share(2, 1, 0, 2);
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        var before = editor.Numbers;
        var proposal = editor.PrepareSwap(data.Divisions[0].Id, 1, 3);
        Assert.True(proposal.IncludesOtherDivisions);
        Assert.DoesNotContain(data.Divisions[3].Name, proposal.Summary);
        editor.ApplySwap(proposal);
        foreach (var team in data.Teams)
            Assert.Equal(team.DivisionId == data.Divisions[3].Id ? before[team.Id] : before[team.Id] <= 2 ? before[team.Id] + 2 : before[team.Id] - 2, editor.Numbers[team.Id]);
        foreach (var fixture in fixtures.Where(f => f.DivisionId == data.Divisions[3].Id))
            Assert.Equal(JsonSerializer.Serialize(fixture), JsonSerializer.Serialize(editor.Fixtures.Single(f => f.Id == fixture.Id)));
        Assert.NotEmpty(editor.ValidateForSave(data, fixtures));
        foreach (var night in editor.Fixtures.GroupBy(f => f.Date.Date))
        {
            Assert.Equal(night.Count(), night.Select(f => (f.VenueId, f.TableId)).Distinct().Count());
            Assert.Equal(night.Count() * 2, night.SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).Distinct().Count());
        }
    }

    [Fact]
    public void SameTablePartner_FollowsFromOneTwoToThreeFour()
    {
        var (data, season, fixtures) = Setup();
        var numbers = SharedFixtureSheetSchedule.Create(data.Divisions, data.Teams, fixtures).TeamNumbers;
        var division = data.Divisions[0].Id;
        var one = data.Teams.Single(t => t.DivisionId == division && numbers[t.Id] == 1);
        var two = data.Teams.Single(t => t.DivisionId == division && numbers[t.Id] == 2);
        two.VenueId = one.VenueId;
        two.TableId = one.TableId;
        foreach (var fixture in fixtures.Where(f => f.HomeTeamId == two.Id))
        {
            fixture.VenueId = one.VenueId;
            fixture.TableId = one.TableId;
        }
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        editor.Swap(division, 1, 2);
        Assert.Equal(2, editor.Numbers[one.Id]);
        Assert.Equal(1, editor.Numbers[two.Id]);
        Assert.NotEmpty(editor.ValidateForSave(data, fixtures));
        editor.Swap(division, 2, 1);
        Assert.False(editor.HasChanges);
        editor.Swap(division, 1, 3);
        Assert.Equal(3, editor.Numbers[one.Id]);
        Assert.Equal(4, editor.Numbers[two.Id]);
        foreach (var night in editor.Fixtures.GroupBy(f => f.Date.Date))
            Assert.Equal(night.Count(), night.Select(f => (f.VenueId, f.TableId)).Distinct().Count());
        editor.Swap(division, 4, 2);
        Assert.Equal(1, editor.Numbers[one.Id]);
        Assert.Equal(2, editor.Numbers[two.Id]);
        Assert.False(editor.HasChanges);
    }

    [Fact]
    public void ByeBlockMove_RemainsCompleteAndOtherDivisionUnchanged()
    {
        var (data, season, fixtures) = Setup(secondCount: 3);
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        editor.Swap(data.Divisions[1].Id, 1, 3);
        Assert.Contains("BYE slots", editor.Review());
        Assert.Equal(fixtures.Count, editor.Fixtures.Count);
        Assert.NotEmpty(editor.ValidateForSave(data, fixtures));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 5)]
    [InlineData(1, 0)]
    public void InvalidMove_DoesNotMutateDraft(int from, int to)
    {
        var (data, season, fixtures) = Setup();
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        Assert.Throws<InvalidOperationException>(() => editor.Swap(data.Divisions[0].Id, from, to));
        Assert.False(editor.HasChanges);
    }

    [Theory]
    [InlineData("frames")]
    [InlineData("penalty")]
    [InlineData("canceled")]
    public void PlayedSeason_RejectsEditor(string state)
    {
        var (data, season, fixtures) = Setup();
        if (state == "frames") fixtures[0].Frames.Add(new FrameResult());
        if (state == "penalty") fixtures[0].HomeLatePenalty = 1;
        if (state == "canceled") fixtures[0].CancelledByTeam = FrameWinner.Home;
        Assert.Throws<InvalidOperationException>(() => new FixtureNumberEditor(data, season, fixtures, fixtures));
    }

    [Fact]
    public void ReturningDraftCopies_CannotBypassValidation()
    {
        var (data, season, fixtures) = Setup();
        var editor = new FixtureNumberEditor(data, season, fixtures, fixtures);
        editor.Fixtures.Clear();
        editor.Numbers.Clear();
        Assert.Equal(fixtures.Count, editor.Fixtures.Count);
        Assert.Equal(data.Teams.Count, editor.Numbers.Count);
    }
}
