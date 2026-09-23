using System.Globalization;
using Wdpl2.Helpers;
using Wdpl2.Models;

namespace Wdpl2.Services.Admin4Pool;

/// <summary>
/// One Admin4Pool season turned into this app's records, ready to review and
/// save as a new season. Builds nothing into the league by itself.
/// </summary>
/// <remarks>
/// Every record is new, with fresh IDs, in a season of its own. Nobody is
/// matched to an existing player, team or season: the same person in two
/// seasons stays two rows until the secretary links them (see
/// <c>PlayerLinks</c>), because a name alone is not identity.
/// <para>
/// Admin4Pool numbered players per season and marked walkovers with a player
/// called "Void Frame" ("Void Player" in the oldest data); those frames get
/// <see cref="FrameResult.VoidPlayerId"/>. Anything that would fail this app's
/// placement checks - a team playing itself, a player in two slots of one
/// frame, a repeated frame number - is repaired or dropped here and reported,
/// rather than left to fail the save.
/// </para>
/// </remarks>
public sealed class Admin4PoolSeasonPlan
{
    public string? SourceName { get; private init; }
    public string LeagueName { get; private init; } = "";
    public string OriginalSeason { get; private init; } = "";
    public Season Season { get; private init; } = new();
    public List<Division> Divisions { get; } = [];
    public List<Venue> Venues { get; } = [];
    public List<Team> Teams { get; } = [];
    public List<Player> Players { get; } = [];
    public List<Fixture> Fixtures { get; } = [];

    /// <summary>Things that were changed or dropped, or that the secretary should know.</summary>
    public List<string> Warnings { get; } = [];

    /// <summary>Teams whose points in this app's table differ from the table Admin4Pool saved.</summary>
    public List<string> TableDifferences { get; } = [];

    /// <summary>Teams checked against the table Admin4Pool saved.</summary>
    public int TeamsChecked { get; private set; }

    public int SinglesFrames => Fixtures.Sum(f => f.Frames.Count(fr => !fr.IsDoubles));
    public int DoublesFrames => Fixtures.Sum(f => f.Frames.Count(fr => fr.IsDoubles));
    public int EightBalls => Fixtures.Sum(f => f.Frames.Count(fr => fr.EightBall));
    public int PlayedFixtures => Fixtures.Count(f => f.Frames.Count > 0);

    public static Admin4PoolSeasonPlan Build(Admin4PoolSqlFile file, AppSettings leagueSettings)
    {
        var league = file.Rows("League").FirstOrDefault()
            ?? throw new InvalidDataException("The export has no League row.");
        var plan = new Admin4PoolSeasonPlan
        {
            SourceName = file.SourceName,
            LeagueName = Str(league, "LeagueName"),
            OriginalSeason = Str(league, "Season"),
        };
        var seasonId = plan.Season.Id;

        var divisions = new Dictionary<long, Division>();
        foreach (var row in file.Rows("Division"))
        {
            var name = Str(row, "FullDivisionName") is { Length: > 0 } full ? full : Str(row, "Abbreviated");
            var division = new Division { SeasonId = seasonId, Name = name };
            divisions[Long(row, "Item_id")] = division;
            plan.Divisions.Add(division);
        }

        var venues = new Dictionary<long, Venue>();
        foreach (var row in file.Rows("Venue"))
        {
            var address = Join(Str(row, "AddressLine1"), Str(row, "AddressLine2"), Str(row, "AddressLine3"), Str(row, "AddressLine4"));
            var venue = new Venue { SeasonId = seasonId, Name = Str(row, "Venue"), Address = address.Length > 0 ? address : null };
            venues[Long(row, "Item_id")] = venue;
            plan.Venues.Add(venue);
        }

        var extras = file.Rows("Team_1").ToDictionary(r => Long(r, "Item_id"));
        var teams = new Dictionary<long, Team>();
        var resultsRemoved = new HashSet<long>();
        var deductions = new Dictionary<Guid, int>();
        foreach (var row in file.Rows("Team"))
        {
            var id = Long(row, "Item_id");
            var notes = new List<string>();
            var contactAddress = Join(Str(row, "ContactAddress1"), Str(row, "ContactAddress2"), Str(row, "ContactAddress3"), Str(row, "ContactAddress4"));
            if (contactAddress.Length > 0) notes.Add("Contact address: " + contactAddress);
            if (Bool(row, "Withdrawn")) notes.Add("Withdrew from the league.");
            if (Bool(row, "RemoveResults"))
            {
                notes.Add("Admin4Pool left this team's matches out of the league table.");
                resultsRemoved.Add(id);
            }
            var team = new Team
            {
                SeasonId = seasonId,
                Name = Str(row, "TeamName"),
                DivisionId = divisions.TryGetValue(Long(row, "Division"), out var d) ? d.Id : null,
                VenueId = venues.TryGetValue(Long(row, "Venue"), out var v) ? v.Id : null,
                Captain = NullIfEmpty(Str(row, "Contact")),
            };
            if (extras.TryGetValue(id, out var extra))
            {
                var deduction = (int)Long(extra, "Deduction");
                if (deduction != 0)
                {
                    notes.Add($"Points deducted: {deduction}.");
                    deductions[team.Id] = deduction;
                }
                var fined = Money(extra, "AmtFined");
                var paid = Money(extra, "FinesPaid");
                var due = Money(extra, "FinesDue");
                if (fined != 0 || paid != 0 || due != 0)
                    notes.Add(string.Create(CultureInfo.GetCultureInfo("en-GB"), $"Fines: {fined:C} fined, {paid:C} paid, {due:C} due."));
            }
            team.Notes = notes.Count > 0 ? string.Join(Environment.NewLine, notes) : null;
            teams[id] = team;
            plan.Teams.Add(team);
        }
        var removedNames = resultsRemoved.Select(id => teams[id].Name).ToList();
        if (removedNames.Count > 0)
            plan.Warnings.Add($"Admin4Pool left {string.Join(", ", removedNames)} out of its table. Their matches are imported and this app will count them.");
        if (deductions.Count > 0)
            plan.Warnings.Add($"{deductions.Count} team(s) had points deducted. This app has no season deduction, so it's recorded in the team's notes and its table will show those points.");

        var players = new Dictionary<long, Guid>();
        var voidNumbers = new HashSet<long>();
        foreach (var row in file.Rows("Player"))
        {
            var number = Long(row, "PlayerNo");
            var name = Str(row, "PlayerName");
            if (name.StartsWith("Void", StringComparison.OrdinalIgnoreCase))
            {
                voidNumbers.Add(number);
                players[number] = FrameResult.VoidPlayerId;
                continue;
            }
            var (first, last) = CsvRows.SplitName(name);
            var player = new Player
            {
                SeasonId = seasonId,
                FirstName = first,
                LastName = last,
                TeamId = teams.TryGetValue(Long(row, "PlayerTeam"), out var t) ? t.Id : null,
            };
            players[number] = player.Id;
            plan.Players.Add(player);
        }
        foreach (var team in plan.Teams.Where(t => t.Captain != null))
        {
            var matches = plan.Players.Where(p => p.TeamId == team.Id &&
                string.Equals(p.FullName, team.Captain, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 1) team.CaptainPlayerId = matches[0].Id;
        }

        var singles = file.Rows("Single").ToLookup(r => Long(r, "MatchNo"));
        var doubles = file.Rows("Dbls").ToLookup(r => Long(r, "MatchNo"));
        int skipped = 0, unknownPlayers = 0, clearedSlots = 0, renumbered = 0, scoreMismatches = 0;
        var storedScores = new Dictionary<Guid, (long Home, long Away)>();
        foreach (var row in file.Rows("Match").OrderBy(r => Str(r, "MatchDate")).ThenBy(r => Long(r, "MatchNo")))
        {
            if (!teams.TryGetValue(Long(row, "HomeTeam"), out var home) ||
                !teams.TryGetValue(Long(row, "AwayTeam"), out var away) || home.Id == away.Id ||
                Date(row, "MatchDate") is not DateTime date)
            {
                skipped++;
                continue;
            }
            var fixture = new Fixture
            {
                SeasonId = seasonId,
                HomeTeamId = home.Id,
                AwayTeamId = away.Id,
                DivisionId = home.DivisionId,
                VenueId = home.VenueId,
                Date = date,
            };
            var matchNo = Long(row, "MatchNo");

            Guid? Map(object? number)
            {
                if (number is null) return null;
                var key = Convert.ToInt64(number, CultureInfo.InvariantCulture);
                if (players.TryGetValue(key, out var id)) return id;
                unknownPlayers++;
                return null;
            }

            foreach (var s in singles[matchNo].OrderBy(r => Long(r, "SingleNo")))
                fixture.Frames.Add(new FrameResult
                {
                    Number = (int)Long(s, "SingleNo"),
                    HomePlayerId = Map(s.GetValueOrDefault("HomePlayerNo")),
                    AwayPlayerId = Map(s.GetValueOrDefault("AwayPlayerNo")),
                    Winner = Winner(s),
                    EightBall = Bool(s, "EightBall"),
                });
            var afterSingles = fixture.Frames.Count == 0 ? 0 : fixture.Frames.Max(f => f.Number);
            foreach (var d in doubles[matchNo].OrderBy(r => Long(r, "DoubleNo")))
                fixture.Frames.Add(new FrameResult
                {
                    Number = afterSingles + (int)Long(d, "DoubleNo"),
                    IsDoubles = true,
                    HomePlayerId = Map(d.GetValueOrDefault("HomePlayerNo1")),
                    HomePlayer2Id = Map(d.GetValueOrDefault("HomePlayerNo2")),
                    AwayPlayerId = Map(d.GetValueOrDefault("AwayPlayerNo1")),
                    AwayPlayer2Id = Map(d.GetValueOrDefault("AwayPlayerNo2")),
                    Winner = Winner(d),
                    EightBall = Bool(d, "EightBall1") || Bool(d, "EightBall2"),
                });

            if (fixture.Frames.Any(f => f.Number < 1) ||
                fixture.Frames.Select(f => f.Number).Distinct().Count() != fixture.Frames.Count)
            {
                renumbered++;
                var n = 1;
                foreach (var f in fixture.Frames) f.Number = n++;
            }
            foreach (var f in fixture.Frames) clearedSlots += ClearRepeatedPlayers(f);

            var storedHome = Long(row, "HSWins") + Long(row, "HDWins");
            var storedAway = Long(row, "ASWins") + Long(row, "ADWins");
            if (fixture.Frames.Count > 0 && (storedHome != fixture.HomeScore || storedAway != fixture.AwayScore))
                scoreMismatches++;
            else if (fixture.Frames.Count == 0 && storedHome + storedAway > 0)
                scoreMismatches++;
            storedScores[fixture.Id] = (storedHome, storedAway);
            plan.Fixtures.Add(fixture);
        }
        if (skipped > 0) plan.Warnings.Add($"{skipped} match(es) left out: a team was missing, a team played itself, or there was no date.");
        if (unknownPlayers > 0) plan.Warnings.Add($"{unknownPlayers} frame slot(s) named a player who isn't in the export; left blank.");
        if (clearedSlots > 0) plan.Warnings.Add($"{clearedSlots} frame slot(s) repeated a player already in that frame; the repeat was left blank.");
        if (renumbered > 0) plan.Warnings.Add($"{renumbered} match(es) had missing or repeated frame numbers and were numbered 1, 2, 3...");
        if (scoreMismatches > 0) plan.Warnings.Add($"{scoreMismatches} match(es) have a saved score that doesn't agree with their frames. The frames are imported; the score comes from them.");

        FillSeason(plan, league, leagueSettings);
        CheckTable(plan, file, teams, resultsRemoved);
        if (Bool(league, "UpdateRequired"))
            plan.Warnings.Add("Admin4Pool had results waiting for its Update step, so its saved table may be out of date.");
        return plan;
    }

    /// <summary>The name this app's seasons use: "Summer 2011", "Winter 2011-12".</summary>
    public static string SuggestName(DateTime firstMatch)
    {
        if (firstMatch.Month is >= 3 and <= 8) return $"Summer {firstMatch.Year}";
        var start = firstMatch.Month >= 9 ? firstMatch.Year : firstMatch.Year - 1;
        return $"Winter {start}-{(start + 1) % 100:00}";
    }

    private static void FillSeason(Admin4PoolSeasonPlan plan, Dictionary<string, object?> league, AppSettings leagueSettings)
    {
        var season = plan.Season;
        var dates = plan.Fixtures.Select(f => f.Date).OrderBy(d => d).ToList();
        if (dates.Count > 0)
        {
            season.StartDate = dates[0];
            season.EndDate = dates[^1];
            season.MatchDayOfWeek = dates.GroupBy(d => d.DayOfWeek).OrderByDescending(g => g.Count()).First().Key;
            for (var i = 1; i < dates.Count; i++)
                if ((dates[i] - dates[i - 1]).TotalDays > 56)
                {
                    plan.Warnings.Add($"{dates.Count - i} match(es) are dated after a {(dates[i] - dates[i - 1]).TotalDays / 7:0}-week gap, " +
                        $"from {dates[i]:d MMM yyyy} (the season ends {dates[^1]:d MMM yyyy}). Worth checking for a mistyped date.");
                    break;
                }
        }
        season.Name = dates.Count > 0 ? SuggestName(dates[0]) : plan.OriginalSeason;
        var singles = (int)Long(league, "NoSingles");
        var doubles = (int)Long(league, "NoDoubles");
        season.SinglesFrameCount = singles;
        season.DoublesFrameCount = doubles;
        season.IncludeDoubles = doubles > 0;
        season.FramesPerMatch = singles + doubles;

        var settings = leagueSettings.Clone();
        settings.MatchWinBonus = (int)Long(league, "WinBonus");
        settings.MatchDrawBonus = (int)Long(league, "DrawBonus");
        settings.DefaultFramesPerMatch = singles + doubles;
        if (Long(league, "MaxSingles") > 0) settings.MaxFramesPerPlayer = (int)Long(league, "MaxSingles");
        if (Double(league, "WinFactor") is > 0 and var win) settings.WinFactor = win;
        if (Double(league, "LossFactor") is > 0 and var loss) settings.LossFactor = loss;
        if (Double(league, "EightBallFactor") is > 0 and var eight) settings.EightBallFactor = eight;
        season.Settings = settings;

        if (Long(league, "SinglesBonus") != 1 || doubles > 0 && Long(league, "DoublesBonus") != 1)
            plan.Warnings.Add("Admin4Pool scored frames at other than 1 point each; this app counts 1 per frame.");
        if (Long(league, "LossBonus") != 0)
            plan.Warnings.Add($"Admin4Pool gave {Long(league, "LossBonus")} point(s) for a match loss; this app has no loss bonus.");
    }

    /// <summary>
    /// Compares this app's table for the imported season with the one Admin4Pool
    /// saved, team by team, on the points each would show.
    /// </summary>
    private static void CheckTable(Admin4PoolSeasonPlan plan, Admin4PoolSqlFile file,
        Dictionary<long, Team> teams, HashSet<long> resultsRemoved)
    {
        // Admin4Pool left a removed team's matches out for both sides, and saved
        // points before any deduction, which is also how this app adds them up.
        var removed = resultsRemoved.Select(id => teams[id].Id).ToHashSet();
        var counted = plan.Fixtures.Where(f => !removed.Contains(f.HomeTeamId) && !removed.Contains(f.AwayTeamId));
        var standings = StandingsCalculator.Calculate(plan.Teams, counted, plan.Season.Settings!)
            .ToDictionary(s => s.TeamId);
        foreach (var row in file.Rows("Team"))
        {
            var id = Long(row, "Item_id");
            if (resultsRemoved.Contains(id) || !teams.TryGetValue(id, out var team)) continue;
            plan.TeamsChecked++;
            var ours = standings[team.Id];
            var saved = Long(row, "Points");
            if (ours.Points != saved || ours.Played != Long(row, "Played"))
                plan.TableDifferences.Add($"{team.Name}: {ours.Points} pts from {ours.Played} played here, {saved} pts from {Long(row, "Played")} in Admin4Pool");
        }
    }

    private static int ClearRepeatedPlayers(FrameResult frame)
    {
        var seen = new HashSet<Guid>();
        var cleared = 0;
        Guid? Keep(Guid? id)
        {
            if (id is not Guid g || FrameResult.IsVoidPlayer(g)) return id;
            if (seen.Add(g)) return id;
            cleared++;
            return null;
        }
        frame.HomePlayerId = Keep(frame.HomePlayerId);
        frame.HomePlayer2Id = Keep(frame.HomePlayer2Id);
        frame.AwayPlayerId = Keep(frame.AwayPlayerId);
        frame.AwayPlayer2Id = Keep(frame.AwayPlayer2Id);
        return cleared;
    }

    private static FrameWinner Winner(Dictionary<string, object?> row) =>
        Str(row, "Winner").ToUpperInvariant() switch
        {
            "HOME" => FrameWinner.Home,
            "AWAY" => FrameWinner.Away,
            _ => FrameWinner.None,
        };

    private static string Str(Dictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture)!.Trim() : "";

    private static string? NullIfEmpty(string value) => value.Length > 0 ? value : null;

    private static long Long(Dictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var v) && v != null ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : 0;

    private static double Double(Dictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var v) && v != null ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : 0;

    private static decimal Money(Dictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var v) && v != null ? Convert.ToDecimal(v, CultureInfo.InvariantCulture) : 0;

    private static bool Bool(Dictionary<string, object?> row, string key) => Long(row, key) != 0;

    private static DateTime? Date(Dictionary<string, object?> row, string key) =>
        DateTime.TryParseExact(Str(row, key), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static string Join(params string[] parts) =>
        string.Join(", ", parts.Where(p => p.Length > 0));
}
