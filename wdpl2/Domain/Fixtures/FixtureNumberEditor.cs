using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wdpl2.Models;

namespace Wdpl2.Services;

public sealed class FixtureNumberEditor
{
    private readonly LeagueData _data;
    private readonly List<Fixture> _original;
    private readonly string _setupFingerprint;
    private readonly string _savedFingerprint;
    private List<Fixture> _draft;
    private Dictionary<Guid, int> _numbers;
    private readonly Dictionary<Guid, int> _initialNumbers;
    private int _revision;

    public sealed class SwapProposal
    {
        internal FixtureNumberEditor Owner { get; }
        internal int Revision { get; }
        internal Dictionary<Guid, int> Numbers { get; }
        internal List<Fixture> Fixtures { get; }
        public bool IncludesOtherDivisions { get; }
        public string Summary { get; }
        public Dictionary<Guid, int> PreviewNumbers => new(Numbers);
        public List<Fixture> PreviewFixtures => Copy(Fixtures);
        public string FixtureChanges { get; }

        internal SwapProposal(FixtureNumberEditor owner, int revision, Dictionary<Guid, int> numbers,
            List<Fixture> fixtures, bool includesOtherDivisions, string summary)
        {
            Owner = owner;
            Revision = revision;
            Numbers = numbers;
            Fixtures = fixtures;
            IncludesOtherDivisions = includesOtherDivisions;
            Summary = summary;
            FixtureChanges = owner.DescribeFixtureChanges(owner._draft, fixtures);
        }
    }

    public Guid SeasonId { get; }
    public string SeasonName => _data.Seasons.Single().Name;
    public int SlotCount { get; }
    public bool HasChanges => !FixturesFingerprint(_original).Equals(FixturesFingerprint(_draft), StringComparison.Ordinal);
    public List<Division> Divisions => Copy(_data.Divisions);
    public List<Team> Teams => Copy(_data.Teams);
    public List<Venue> Venues => Copy(_data.Venues);
    public Dictionary<Guid, int> Numbers => new(_numbers);
    public List<Fixture> Fixtures => Copy(_draft);
    public DateTime OpeningDate => _original.Min(f => f.Date.Date);
    public IReadOnlyList<Guid> PreviousFixtureIds { get; }

    public FixtureNumberEditor(LeagueData data, Guid seasonId, IReadOnlyList<Fixture> draft, IReadOnlyList<Fixture> savedFixtures)
    {
        SeasonId = seasonId;
        _data = Snapshot(data, seasonId);
        EnsureUnplayed(savedFixtures);
        EnsureUnplayed(draft);
        if (savedFixtures.Any(f => f.SeasonId != seasonId)) throw new InvalidOperationException("The saved fixture baseline contains another season.");
        Validate(_data, draft);
        _original = Copy(draft.ToList());
        _draft = Copy(_original);
        var schedule = SharedFixtureSheetSchedule.Create(_data.Divisions, _data.Teams, _draft);
        SlotCount = schedule.SlotCount;
        _numbers = new(schedule.TeamNumbers);
        _initialNumbers = new(_numbers);
        _setupFingerprint = SetupFingerprint(_data, seasonId);
        _savedFingerprint = FixturesFingerprint(savedFixtures);
        PreviousFixtureIds = Array.AsReadOnly(savedFixtures.Select(f => f.Id).ToArray());
    }

    public void Swap(Guid divisionId, int from, int to)
        => ApplySwap(PrepareSwap(divisionId, from, to, includeLinkedDivisions: false));

    public List<Team> OpeningByeTeams(Guid divisionId)
        => Copy(OpeningByeTeams(divisionId, _draft));

    private List<Team> OpeningByeTeams(Guid divisionId, IReadOnlyList<Fixture> fixtures)
    {
        var playing = fixtures.Where(f => f.Date.Date == OpeningDate)
            .SelectMany(f => new[] { f.HomeTeamId, f.AwayTeamId }).ToHashSet();
        return _data.Teams.Where(t => t.DivisionId == divisionId && !playing.Contains(t.Id))
            .OrderBy(t => t.Name).ToList();
    }

    public SwapProposal PrepareFirstBye(Guid divisionId, Guid teamId)
    {
        var team = _data.Teams.SingleOrDefault(t => t.Id == teamId && t.DivisionId == divisionId)
            ?? throw new InvalidOperationException("Choose a team in the selected division.");
        if (_data.Teams.Count(t => t.DivisionId == divisionId) == SlotCount)
            throw new InvalidOperationException("This division has no BYE slots. A first bye cannot be assigned without changing the draw size.");
        if (OpeningByeTeams(divisionId).Any(t => t.Id == teamId))
            return new SwapProposal(this, _revision, new(_numbers), Copy(_draft), false,
                $"{team.Name} already has an opening bye on {OpeningDate:dd MMM yyyy}. No changes are needed.");

        var partners = new Dictionary<Guid, Team>();
        foreach (var group in _data.Teams.GroupBy(t => (t.VenueId, t.TableId)).Where(g => g.Count() == 2))
        {
            var pair = group.ToArray();
            partners[pair[0].Id] = pair[1];
            partners[pair[1].Id] = pair[0];
        }
        if (partners.TryGetValue(teamId, out var homePartner) && homePartner.DivisionId == divisionId)
            throw new InvalidOperationException($"{team.Name} shares a table with {homePartner.Name} in this division. Table partners must occupy an odd/even pair, which plays each other in the opening week. Neither can have the first bye without breaking that rule.");

        var proposals = new List<SwapProposal>();
        string? rejection = null;
        for (int destination = 1; destination <= SlotCount; destination++)
        {
            if (destination == _numbers[teamId]) continue;
            var candidate = new Dictionary<Guid, int>(_numbers);
            var fixedNumbers = new Dictionary<Guid, int>();
            var pending = new Queue<Team>();
            void Assign(Team moved, int number)
            {
                if (fixedNumbers.TryGetValue(moved.Id, out int required) && required != number)
                    throw new InvalidOperationException($"Linked table partners require incompatible numbers for {moved.Name}.");
                if (fixedNumbers.TryAdd(moved.Id, number)) pending.Enqueue(moved);
                candidate[moved.Id] = number;
            }
            void Move(Team moved, int number)
            {
                int previous = candidate[moved.Id];
                var displaced = _data.Teams.SingleOrDefault(t => t.DivisionId == moved.DivisionId && candidate[t.Id] == number);
                Assign(moved, number);
                if (displaced != null && displaced.Id != moved.Id) Assign(displaced, previous);
            }
            try
            {
                Move(team, destination);
                while (pending.TryDequeue(out var moved))
                {
                    if (!partners.TryGetValue(moved.Id, out var partner)) continue;
                    int partnerNumber = ((candidate[moved.Id] - 1) ^ 1) + 1;
                    Move(partner, partnerNumber);
                }
                var openingPair = NumberedFixtureDraw.Create(SlotCount, 1)
                    .Single(p => p.Round == 0 && (p.Home == candidate[teamId] || p.Away == candidate[teamId]));
                int opponent = openingPair.Home == candidate[teamId] ? openingPair.Away : openingPair.Home;
                if (_data.Teams.Any(t => t.DivisionId == divisionId && candidate[t.Id] == opponent)) continue;
                var affected = _data.Teams.Where(t => candidate[t.Id] != _numbers[t.Id]).Select(t => t.DivisionId!.Value).ToHashSet();
                var proposal = CreateProposal(divisionId, affected, candidate);
                if (!OpeningByeTeams(divisionId, proposal.Fixtures).Any(t => t.Id == teamId)) continue;
                proposals.Add(new SwapProposal(this, _revision, candidate, proposal.Fixtures, proposal.IncludesOtherDivisions,
                    $"First bye requested for {team.Name} on {OpeningDate:dd MMM yyyy}.\n\n" + proposal.Summary));
            }
            catch (InvalidOperationException ex) { rejection = ex.Message; }
        }
        return proposals.OrderBy(p => _data.Teams.Where(t => p.Numbers[t.Id] != _numbers[t.Id]).Select(t => t.DivisionId).Distinct().Count())
            .ThenBy(p => p.Numbers.Count(n => n.Value != _numbers[n.Key])).FirstOrDefault()
            ?? throw new InvalidOperationException($"No safe first-bye swap was found for {team.Name}. Nothing has changed. {rejection}");
    }

    public SwapProposal PrepareSwap(Guid divisionId, int from, int to, bool includeLinkedDivisions = true)
    {
        if (!_data.Divisions.Any(d => d.Id == divisionId)) throw new InvalidOperationException("Choose a division in this season.");
        if (from < 1 || from > SlotCount || to < 1 || to > SlotCount)
            throw new InvalidOperationException($"Numbers must be between 1 and {SlotCount}.");
        static int Partner(int number) => number % 2 == 1 ? number + 1 : number - 1;
        var moves = new Dictionary<int, int>
        {
            [from] = to,
            [to] = from
        };
        moves[Partner(from)] = Partner(to);
        moves[Partner(to)] = Partner(from);
        var candidate = new Dictionary<Guid, int>(_numbers);
        var affected = new HashSet<Guid>();
        void SwapDivision(Guid id)
        {
            if (!affected.Add(id)) return;
            foreach (var team in _data.Teams.Where(t => t.DivisionId == id))
            {
                int number = candidate[team.Id];
                if (moves.TryGetValue(number, out var destination)) candidate[team.Id] = destination;
            }
        }
        SwapDivision(divisionId);
        var partners = _data.Teams.GroupBy(t => (t.VenueId, t.TableId)).Where(g => g.Count() == 2).ToList();
        bool expanded;
        do
        {
            expanded = false;
            foreach (var group in partners)
            {
                var first = group.First();
                var second = group.Last();
                if (NumberedFixtureDraw.AreTablePartners(candidate[first.Id], candidate[second.Id])) continue;
                if (!includeLinkedDivisions)
                    throw new InvalidOperationException($"Cannot move this block: {first.Name} ({candidate[first.Id]}) and {second.Name} ({candidate[second.Id]}) share a table and must keep consecutive odd/even numbers. Other divisions will not be changed without accepting a combined swap.");
                var linked = new[] { first.DivisionId!.Value, second.DivisionId!.Value }.Where(id => !affected.Contains(id)).ToList();
                if (linked.Count == 0)
                    throw new InvalidOperationException($"The combined swap cannot preserve table partners {first.Name} and {second.Name}. Nothing has changed.");
                foreach (var id in linked) SwapDivision(id);
                expanded = true;
            }
        } while (expanded);
        return CreateProposal(divisionId, affected, candidate);
    }

    private SwapProposal CreateProposal(Guid divisionId, HashSet<Guid> affected, Dictionary<Guid, int> candidate)
    {
        var rebuilt = Rebuild(affected, candidate);
        Validate(_data, rebuilt);
        var reconstructed = SharedFixtureSheetSchedule.Create(_data.Divisions, _data.Teams, rebuilt).TeamNumbers;
        if (candidate.Any(p => reconstructed[p.Key] != p.Value))
            throw new InvalidOperationException("This move has ambiguous numbering in the shared draw. The sheet cannot preserve the exact requested numbers, so nothing has changed.");
        var summary = new List<string>();
        foreach (var division in _data.Divisions.Where(d => affected.Contains(d.Id)))
        {
            summary.Add(division.Name);
            summary.AddRange(_data.Teams.Where(t => t.DivisionId == division.Id && candidate[t.Id] != _numbers[t.Id])
                .OrderBy(t => candidate[t.Id]).Select(t => $"{t.Name}: {_numbers[t.Id]} → {candidate[t.Id]}"));
            var members = _data.Teams.Where(t => t.DivisionId == division.Id).ToList();
            var beforeByes = Enumerable.Range(1, SlotCount).Where(n => members.All(t => _numbers[t.Id] != n));
            var afterByes = Enumerable.Range(1, SlotCount).Where(n => members.All(t => candidate[t.Id] != n));
            summary.Add($"BYE slots: {string.Join(", ", beforeByes)} → {string.Join(", ", afterByes)}");
            string ByeNames(IReadOnlyList<Fixture> fixtures)
            {
                var names = OpeningByeTeams(division.Id, fixtures).Select(t => t.Name).ToList();
                return names.Count == 0 ? "None" : string.Join(", ", names);
            }
            summary.Add($"Opening bye ({OpeningDate:dd MMM yyyy}): {ByeNames(_draft)} → {ByeNames(rebuilt)}");
        }
        var before = _draft.ToDictionary(f => f.Id);
        int changed = rebuilt.Count(f => !before.TryGetValue(f.Id, out var old) || old.Date != f.Date);
        summary.Add($"{changed} fixture(s) have new dates or pairings. All affected divisions are validated together. Unrelated divisions stay unchanged. Review full fixture changes before saving.");
        return new SwapProposal(this, _revision, candidate, rebuilt, affected.Any(id => id != divisionId), string.Join("\n", summary));
    }

    public void ApplySwap(SwapProposal proposal)
    {
        if (!ReferenceEquals(proposal.Owner, this) || proposal.Revision != _revision)
            throw new InvalidOperationException("The draft changed after this proposal was prepared. Preview the swap again. Nothing has been applied.");
        _numbers = new(proposal.Numbers);
        _draft = Copy(proposal.Fixtures);
        _revision++;
    }

    public void Reset()
    {
        _draft = Copy(_original);
        _numbers = new(_initialNumbers);
        _revision++;
    }

    public string Review()
    {
        var lines = new List<string>();
        foreach (var division in _data.Divisions)
        {
            var changes = _data.Teams.Where(t => t.DivisionId == division.Id && _initialNumbers[t.Id] != _numbers[t.Id]).ToList();
            if (changes.Count == 0) continue;
            lines.Add(division.Name);
            lines.AddRange(changes.OrderBy(t => _numbers[t.Id]).Select(t => $"{t.Name}: {_initialNumbers[t.Id]} → {_numbers[t.Id]}"));
            var usedBefore = _data.Teams.Where(t => t.DivisionId == division.Id).Select(t => _initialNumbers[t.Id]).ToHashSet();
            var usedAfter = _data.Teams.Where(t => t.DivisionId == division.Id).Select(t => _numbers[t.Id]).ToHashSet();
            lines.Add($"BYE slots: {string.Join(", ", Enumerable.Range(1, SlotCount).Where(n => !usedBefore.Contains(n)))} → {string.Join(", ", Enumerable.Range(1, SlotCount).Where(n => !usedAfter.Contains(n)))}");
        }
        var fixtureChanges = DescribeFixtureChanges(_original, _draft);
        if (fixtureChanges.Length > 0) lines.Add(fixtureChanges);
        return lines.Count == 0 ? "No number changes. The validated draw is ready to save." : string.Join("\n", lines);
    }

    private string DescribeFixtureChanges(IReadOnlyList<Fixture> baseline, IReadOnlyList<Fixture> proposed)
    {
        var lines = new List<string>();
        var before = baseline.ToDictionary(f => f.Id);
        foreach (var fixture in proposed)
        {
            if (before.TryGetValue(fixture.Id, out var old) && old.Date == fixture.Date) continue;
            var home = _data.Teams.Single(t => t.Id == fixture.HomeTeamId).Name;
            var away = _data.Teams.Single(t => t.Id == fixture.AwayTeamId).Name;
            lines.Add($"{home} v {away}: {(old == null ? "new pairing" : old.Date.ToString("dd MMM yyyy HH:mm"))} → {fixture.Date:dd MMM yyyy HH:mm}");
        }
        foreach (var removed in baseline.Where(f => !proposed.Any(d => d.Id == f.Id)))
            lines.Add($"Removed: {_data.Teams.Single(t => t.Id == removed.HomeTeamId).Name} v {_data.Teams.Single(t => t.Id == removed.AwayTeamId).Name}, {removed.Date:dd MMM yyyy HH:mm}");
        return string.Join("\n", lines);
    }

    public List<Fixture> ValidateForSave(LeagueData current, IReadOnlyList<Fixture> savedFixtures)
    {
        GeneratedScheduleValidator.ValidateSetup(current, SeasonId);
        EnsureUnplayed(savedFixtures);
        if (FixturesFingerprint(savedFixtures) != _savedFingerprint || SetupFingerprint(current, SeasonId) != _setupFingerprint)
            throw new InvalidOperationException("The season setup or saved fixtures changed while this review was open. Close it and reopen to review the current schedule. Nothing was saved.");
        Validate(current, _draft);
        var teams = current.Teams.Where(t => t.SeasonId == SeasonId).ToList();
        var reconstructed = SharedFixtureSheetSchedule.Create(current.Divisions.Where(d => d.SeasonId == SeasonId).ToList(), teams, _draft).TeamNumbers;
        if (_numbers.Any(p => reconstructed[p.Key] != p.Value)) throw new InvalidOperationException("The requested numbers no longer match the shared sheet.");
        return Copy(_draft);
    }

    public static void EnsureUnplayed(IEnumerable<Fixture> fixtures)
    {
        if (fixtures.Any(f => f.Frames.Count > 0 || f.HomeLatePenalty != 0 || f.AwayLatePenalty != 0
            || f.CancellationPenalty != 0 || f.CancelledByTeam != FrameWinner.None))
            throw new InvalidOperationException("Team numbers can only be changed for a completely unplayed season with no recorded frames, penalties or cancellations.");
    }

    private List<Fixture> Rebuild(HashSet<Guid> divisionIds, Dictionary<Guid, int> numbers)
    {
        var settings = _data.GetSettingsForSeason(SeasonId);
        var dates = _original.Select(f => f.Date.Date).Distinct().OrderBy(d => d).ToList();
        var result = Copy(_draft.Where(f => !divisionIds.Contains(f.DivisionId!.Value)).ToList());
        foreach (var divisionId in divisionIds)
        {
            var teams = _data.Teams.Where(t => t.DivisionId == divisionId).ToDictionary(t => numbers[t.Id]);
            var available = _original.Where(f => f.DivisionId == divisionId).GroupBy(f => (f.HomeTeamId, f.AwayTeamId))
                .ToDictionary(g => g.Key, g => new Queue<Fixture>(g.OrderBy(f => f.Date)));
            foreach (var pairing in NumberedFixtureDraw.Create(SlotCount, settings.DefaultRoundsPerOpponent))
            {
                if (!teams.TryGetValue(pairing.Home, out var home) || !teams.TryGetValue(pairing.Away, out var away)) continue;
                Fixture fixture = available.TryGetValue((home.Id, away.Id), out var queue) && queue.Count > 0
                    ? Copy(queue.Dequeue()) : new Fixture { SeasonId = SeasonId, DivisionId = divisionId, HomeTeamId = home.Id, AwayTeamId = away.Id };
                fixture.Date = dates[pairing.Round].Add(settings.DefaultMatchTime);
                fixture.VenueId = home.VenueId;
                fixture.TableId = home.TableId;
                result.Add(fixture);
            }
        }
        return result.OrderBy(f => f.Date).ThenBy(f => f.DivisionId).ThenBy(f => f.Id).ToList();
    }

    private void Validate(LeagueData data, IReadOnlyList<Fixture> fixtures)
    {
        var season = data.Seasons.Single(s => s.Id == SeasonId);
        var settings = data.GetSettingsForSeason(SeasonId);
        EnsureUnplayed(fixtures);
        GeneratedScheduleValidator.Validate(data, SeasonId, fixtures, season.StartDate, season.EndDate,
            settings.DefaultMatchDay, settings.DefaultMatchTime, settings.DefaultRoundsPerOpponent, season.BlackoutDates);
    }

    private static LeagueData Snapshot(LeagueData data, Guid seasonId) => new()
    {
        Seasons = Copy(data.Seasons.Where(s => s.Id == seasonId).ToList()),
        Teams = Copy(data.Teams.Where(t => t.SeasonId == seasonId).ToList()),
        Divisions = Copy(data.Divisions.Where(d => d.SeasonId == seasonId).ToList()),
        Venues = Copy(data.Venues.Where(v => v.SeasonId == seasonId).ToList()),
        Settings = Copy(data.GetSettingsForSeason(seasonId))
    };

    private static string SetupFingerprint(LeagueData data, Guid seasonId)
    {
        var season = data.Seasons.Single(s => s.Id == seasonId);
        var settings = data.GetSettingsForSeason(seasonId);
        return Hash(new
        {
            season.Id, season.StartDate, season.EndDate, season.IsLocked,
            Blackouts = season.BlackoutDates.OrderBy(d => d).ToList(),
            settings.DefaultMatchDay, settings.DefaultMatchTime, settings.DefaultRoundsPerOpponent,
            Teams = data.Teams.Where(t => t.SeasonId == seasonId).OrderBy(t => t.Id).Select(t => new { t.Id, t.Name, t.DivisionId, t.VenueId, t.TableId, t.GlobalTeamId }),
            Divisions = data.Divisions.Where(d => d.SeasonId == seasonId).OrderBy(d => d.Id).Select(d => new { d.Id, d.Name }),
            Venues = data.Venues.Where(v => v.SeasonId == seasonId).OrderBy(v => v.Id).Select(v => new { v.Id, Tables = v.Tables.Select(t => t.Id).OrderBy(id => id).ToList() })
        });
    }

    private static string FixturesFingerprint(IEnumerable<Fixture> fixtures) => Hash(fixtures.OrderBy(f => f.Id).ToList());
    private static string Hash<T>(T value)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new TimestampFingerprintConverter());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, options))));
    }

    private sealed class TimestampFingerprintConverter : System.Text.Json.Serialization.JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetInt64());
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) => writer.WriteNumberValue(value.Ticks);
    }
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
