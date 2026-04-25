using System.Globalization;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Generates a portable SQL script (CREATE TABLE + INSERT) for an entire season's data.
/// The output is compatible with SQLite, SQL Server, PostgreSQL and MySQL.
/// </summary>
public static class SqlExportService
{
    /// <summary>
    /// Generate a full SQL script for the given season, including schema and data.
    /// </summary>
    public static string GenerateSeasonSql(LeagueData data, Guid seasonId)
    {
        var season = data.Seasons.FirstOrDefault(s => s.Id == seasonId);
        if (season == null)
            return "-- Season not found.";

        var (divisions, venues, teams, players, fixtures) = data.GetSeasonData(seasonId);
        var competitions = data.Competitions.Where(c => c.SeasonId == seasonId).ToList();
        var doublesPairings = data.DoublesPairings.Where(dp => dp.SeasonId == seasonId).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("-- ==========================================================");
        sb.AppendLine($"-- SQL Export: {season.Name}");
        sb.AppendLine($"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("-- ==========================================================");
        sb.AppendLine();

        // Schema
        AppendCreateTables(sb);

        // Data
        AppendSeasonInsert(sb, season);
        AppendDivisionInserts(sb, divisions);
        AppendVenueInserts(sb, venues);
        AppendVenueTableInserts(sb, venues);
        AppendTeamInserts(sb, teams);
        AppendPlayerInserts(sb, players);
        AppendPlayerTransferInserts(sb, players);
        AppendFixtureInserts(sb, fixtures);
        AppendFrameResultInserts(sb, fixtures);
        AppendCompetitionInserts(sb, competitions);
        AppendDoublesPairingInserts(sb, doublesPairings);

        sb.AppendLine("-- ==========================================================");
        sb.AppendLine("-- Export complete");
        sb.AppendLine("-- ==========================================================");

        return sb.ToString();
    }

    // --------------------------------------------------------
    //  CREATE TABLE statements
    // --------------------------------------------------------

    private static void AppendCreateTables(StringBuilder sb)
    {
        sb.AppendLine("-- ===================== SCHEMA =====================");
        sb.AppendLine();

        sb.AppendLine("""
            CREATE TABLE IF NOT EXISTS Seasons (
                Id              TEXT PRIMARY KEY,
                Name            TEXT NOT NULL,
                StartDate       TEXT,
                EndDate         TEXT,
                MatchDayOfWeek  INTEGER,
                MatchStartTime  TEXT,
                FramesPerMatch  INTEGER DEFAULT 0,
                IsActive        INTEGER DEFAULT 1,
                IsLocked        INTEGER DEFAULT 0,
                IncludeDoubles  INTEGER DEFAULT 0,
                SinglesFrameCount INTEGER DEFAULT 0,
                DoublesFrameCount INTEGER DEFAULT 0,
                TransferWindowStart TEXT,
                TransferWindowEnd   TEXT,
                CreatedDate     TEXT,
                ModifiedDate    TEXT
            );
            """);

        sb.AppendLine("""
            CREATE TABLE IF NOT EXISTS Divisions (
                Id          TEXT PRIMARY KEY,
                SeasonId    TEXT NOT NULL REFERENCES Seasons(Id),
                Name        TEXT NOT NULL,
                Notes       TEXT,
                CreatedDate TEXT,
                ModifiedDate TEXT
            );
            """);

        sb.AppendLine("""
            CREATE TABLE IF NOT EXISTS Venues (
                Id          TEXT PRIMARY KEY,
                SeasonId    TEXT REFERENCES Seasons(Id),
                Name        TEXT NOT NULL,
                Address     TEXT,
                Notes       TEXT,
                CreatedDate TEXT,
                ModifiedDate TEXT
            );
            """);

        sb.AppendLine("""
            CREATE TABLE IF NOT EXISTS VenueTables (
                Id          TEXT PRIMARY KEY,
                VenueId     TEXT NOT NULL REFERENCES Venues(Id),
                Label       TEXT NOT NULL,
                MaxTeams    INTEGER DEFAULT 2
            );
            """);

        sb.AppendLine("""
            CREATE TABLE IF NOT EXISTS Teams (
                Id              TEXT PRIMARY KEY,
                SeasonId        TEXT REFERENCES Seasons(Id),
                GlobalTeamId    TEXT,
                Name            TEXT,
                DivisionId      TEXT REFERENCES Divisions(Id),
                VenueId         TEXT REFERENCES Venues(Id),
                TableId         TEXT REFERENCES VenueTables(Id),
                ProvidesFood    INTEGER DEFAULT 0,
                CaptainPlayerId TEXT,
                Captain         TEXT,
                CaptainPlayed   INTEGER DEFAULT 0,
                Notes           TEXT,
                CreatedDate     TEXT,
                ModifiedDate    TEXT
            );
            """);

        sb.AppendLine("""
            CREATE TABLE IF NOT EXISTS Players (
                Id                  TEXT PRIMARY KEY,
                SeasonId            TEXT REFERENCES Seasons(Id),
                GlobalPlayerId      TEXT,
                FirstName           TEXT,
                LastName            TEXT,
                TeamId              TEXT REFERENCES Teams(Id),
                IsActive            INTEGER DEFAULT 1,
                DeactivatedDate     TEXT,
                DeactivationReason  TEXT,
                Notes               TEXT,
                CreatedDate         TEXT,
                ModifiedDate        TEXT
            );
            """);

        sb.AppendLine("""
            CREATE TABLE IF NOT EXISTS PlayerTransfers (
                Id                      TEXT PRIMARY KEY,
                PlayerId                TEXT NOT NULL REFERENCES Players(Id),
                FromTeamId              TEXT,
                FromTeamName            TEXT,
                ToTeamId                TEXT,
                ToTeamName              TEXT,
                TransferDate            TEXT,
                RatingAtTransfer        INTEGER DEFAULT 0,
                FramesPlayedAtTransfer  INTEGER DEFAULT 0,
                WinsAtTransfer          INTEGER DEFAULT 0,
                LossesAtTransfer        INTEGER DEFAULT 0,
                Notes                   TEXT
            );
            """);

        sb.AppendLine("""
            CREATE TABLE IF NOT EXISTS Fixtures (
                Id                  TEXT PRIMARY KEY,
                SeasonId            TEXT REFERENCES Seasons(Id),
                DivisionId          TEXT REFERENCES Divisions(Id),
                HomeTeamId          TEXT NOT NULL,
                AwayTeamId          TEXT NOT NULL,
                VenueId             TEXT REFERENCES Venues(Id),
                TableId             TEXT REFERENCES VenueTables(Id),
                Date                TEXT,
                HomeLatePenalty     INTEGER DEFAULT 0,
                AwayLatePenalty     INTEGER DEFAULT 0,
                CancelledByTeam    INTEGER DEFAULT 0,
                CancellationPenalty INTEGER DEFAULT 0,
                CreatedDate         TEXT,
                ModifiedDate        TEXT
            );
            """);

        sb.AppendLine("""
            CREATE TABLE IF NOT EXISTS FrameResults (
                FixtureId       TEXT NOT NULL REFERENCES Fixtures(Id),
                Number          INTEGER NOT NULL,
                HomePlayerId    TEXT,
                AwayPlayerId    TEXT,
                HomePlayer2Id   TEXT,
                AwayPlayer2Id   TEXT,
                Winner          INTEGER DEFAULT 0,
                EightBall       INTEGER DEFAULT 0,
                IsDoubles       INTEGER DEFAULT 0,
                HomeOppRating   INTEGER,
                HomePlayerRating INTEGER,
                AwayOppRating   INTEGER,
                AwayPlayerRating INTEGER,
                WeekNo          INTEGER,
                PRIMARY KEY (FixtureId, Number)
            );
            """);

        sb.AppendLine("""
            CREATE TABLE IF NOT EXISTS Competitions (
                Id                  TEXT PRIMARY KEY,
                SeasonId            TEXT REFERENCES Seasons(Id),
                Name                TEXT NOT NULL,
                Format              INTEGER DEFAULT 0,
                Status              INTEGER DEFAULT 0,
                StartDate           TEXT,
                Notes               TEXT,
                BestOf              INTEGER DEFAULT 0,
                RandomDraw          INTEGER DEFAULT 1,
                CreatedDate         TEXT,
                PlateCompetitionId  TEXT,
                ParentCompetitionId TEXT
            );
            """);

        sb.AppendLine("""
            CREATE TABLE IF NOT EXISTS DoublesPairings (
                Id              TEXT PRIMARY KEY,
                SeasonId        TEXT REFERENCES Seasons(Id),
                DivisionId      TEXT REFERENCES Divisions(Id),
                TeamId          TEXT REFERENCES Teams(Id),
                Player1Id       TEXT,
                Player2Id       TEXT,
                Player1Name     TEXT,
                Player2Name     TEXT,
                TeamName        TEXT,
                Played          INTEGER DEFAULT 0,
                Won             INTEGER DEFAULT 0,
                Lost            INTEGER DEFAULT 0,
                BestRating      INTEGER DEFAULT 0,
                BestRatingDate  TEXT,
                CurrentRating   INTEGER DEFAULT 0
            );
            """);

        sb.AppendLine("-- ===================== DATA =====================");
        sb.AppendLine();
    }

    // --------------------------------------------------------
    //  INSERT helpers
    // --------------------------------------------------------

    private static void AppendSeasonInsert(StringBuilder sb, Season s)
    {
        sb.AppendLine("-- Season");
        sb.AppendLine($"INSERT INTO Seasons (Id, Name, StartDate, EndDate, MatchDayOfWeek, MatchStartTime, FramesPerMatch, IsActive, IsLocked, IncludeDoubles, SinglesFrameCount, DoublesFrameCount, TransferWindowStart, TransferWindowEnd, CreatedDate, ModifiedDate) VALUES ({Esc(s.Id)}, {Esc(s.Name)}, {Esc(s.StartDate)}, {Esc(s.EndDate)}, {(int)s.MatchDayOfWeek}, {Esc(s.MatchStartTime)}, {s.FramesPerMatch}, {Bool(s.IsActive)}, {Bool(s.IsLocked)}, {Bool(s.IncludeDoubles)}, {s.SinglesFrameCount}, {s.DoublesFrameCount}, {Esc(s.TransferWindowStart)}, {Esc(s.TransferWindowEnd)}, {Esc(s.CreatedDate)}, {Esc(s.ModifiedDate)});");
        sb.AppendLine();
    }

    private static void AppendDivisionInserts(StringBuilder sb, List<Division> divisions)
    {
        if (divisions.Count == 0) return;
        sb.AppendLine($"-- Divisions ({divisions.Count})");
        foreach (var d in divisions)
            sb.AppendLine($"INSERT INTO Divisions (Id, SeasonId, Name, Notes, CreatedDate, ModifiedDate) VALUES ({Esc(d.Id)}, {Esc(d.SeasonId)}, {Esc(d.Name)}, {Esc(d.Notes)}, {Esc(d.CreatedDate)}, {Esc(d.ModifiedDate)});");
        sb.AppendLine();
    }

    private static void AppendVenueInserts(StringBuilder sb, List<Venue> venues)
    {
        if (venues.Count == 0) return;
        sb.AppendLine($"-- Venues ({venues.Count})");
        foreach (var v in venues)
            sb.AppendLine($"INSERT INTO Venues (Id, SeasonId, Name, Address, Notes, CreatedDate, ModifiedDate) VALUES ({Esc(v.Id)}, {Esc(v.SeasonId)}, {Esc(v.Name)}, {Esc(v.Address)}, {Esc(v.Notes)}, {Esc(v.CreatedDate)}, {Esc(v.ModifiedDate)});");
        sb.AppendLine();
    }

    private static void AppendVenueTableInserts(StringBuilder sb, List<Venue> venues)
    {
        var allTables = venues.Where(v => v.Tables.Count > 0)
            .SelectMany(v => v.Tables.Select(t => (VenueId: v.Id, Table: t)))
            .ToList();
        if (allTables.Count == 0) return;
        sb.AppendLine($"-- Venue Tables ({allTables.Count})");
        foreach (var (venueId, t) in allTables)
            sb.AppendLine($"INSERT INTO VenueTables (Id, VenueId, Label, MaxTeams) VALUES ({Esc(t.Id)}, {Esc(venueId)}, {Esc(t.Label)}, {t.MaxTeams});");
        sb.AppendLine();
    }

    private static void AppendTeamInserts(StringBuilder sb, List<Team> teams)
    {
        if (teams.Count == 0) return;
        sb.AppendLine($"-- Teams ({teams.Count})");
        foreach (var t in teams)
            sb.AppendLine($"INSERT INTO Teams (Id, SeasonId, GlobalTeamId, Name, DivisionId, VenueId, TableId, ProvidesFood, CaptainPlayerId, Captain, CaptainPlayed, Notes, CreatedDate, ModifiedDate) VALUES ({Esc(t.Id)}, {Esc(t.SeasonId)}, {Esc(t.GlobalTeamId)}, {Esc(t.Name)}, {Esc(t.DivisionId)}, {Esc(t.VenueId)}, {Esc(t.TableId)}, {Bool(t.ProvidesFood)}, {Esc(t.CaptainPlayerId)}, {Esc(t.Captain)}, {Bool(t.CaptainPlayed)}, {Esc(t.Notes)}, {Esc(t.CreatedDate)}, {Esc(t.ModifiedDate)});");
        sb.AppendLine();
    }

    private static void AppendPlayerInserts(StringBuilder sb, List<Player> players)
    {
        if (players.Count == 0) return;
        sb.AppendLine($"-- Players ({players.Count})");
        foreach (var p in players)
            sb.AppendLine($"INSERT INTO Players (Id, SeasonId, GlobalPlayerId, FirstName, LastName, TeamId, IsActive, DeactivatedDate, DeactivationReason, Notes, CreatedDate, ModifiedDate) VALUES ({Esc(p.Id)}, {Esc(p.SeasonId)}, {Esc(p.GlobalPlayerId)}, {Esc(p.FirstName)}, {Esc(p.LastName)}, {Esc(p.TeamId)}, {Bool(p.IsActive)}, {Esc(p.DeactivatedDate)}, {Esc(p.DeactivationReason)}, {Esc(p.Notes)}, {Esc(p.CreatedDate)}, {Esc(p.ModifiedDate)});");
        sb.AppendLine();
    }

    private static void AppendPlayerTransferInserts(StringBuilder sb, List<Player> players)
    {
        var transfers = players.Where(p => p.TransferHistory is { Count: > 0 })
            .SelectMany(p => p.TransferHistory.Select(t => (PlayerId: p.Id, Transfer: t)))
            .ToList();
        if (transfers.Count == 0) return;
        sb.AppendLine($"-- Player Transfers ({transfers.Count})");
        foreach (var (playerId, t) in transfers)
            sb.AppendLine($"INSERT INTO PlayerTransfers (Id, PlayerId, FromTeamId, FromTeamName, ToTeamId, ToTeamName, TransferDate, RatingAtTransfer, FramesPlayedAtTransfer, WinsAtTransfer, LossesAtTransfer, Notes) VALUES ({Esc(t.Id)}, {Esc(playerId)}, {Esc(t.FromTeamId)}, {Esc(t.FromTeamName)}, {Esc(t.ToTeamId)}, {Esc(t.ToTeamName)}, {Esc(t.TransferDate)}, {t.RatingAtTransfer}, {t.FramesPlayedAtTransfer}, {t.WinsAtTransfer}, {t.LossesAtTransfer}, {Esc(t.Notes)});");
        sb.AppendLine();
    }

    private static void AppendFixtureInserts(StringBuilder sb, List<Fixture> fixtures)
    {
        if (fixtures.Count == 0) return;
        sb.AppendLine($"-- Fixtures ({fixtures.Count})");
        foreach (var f in fixtures)
            sb.AppendLine($"INSERT INTO Fixtures (Id, SeasonId, DivisionId, HomeTeamId, AwayTeamId, VenueId, TableId, Date, HomeLatePenalty, AwayLatePenalty, CancelledByTeam, CancellationPenalty, CreatedDate, ModifiedDate) VALUES ({Esc(f.Id)}, {Esc(f.SeasonId)}, {Esc(f.DivisionId)}, {Esc(f.HomeTeamId)}, {Esc(f.AwayTeamId)}, {Esc(f.VenueId)}, {Esc(f.TableId)}, {Esc(f.Date)}, {f.HomeLatePenalty}, {f.AwayLatePenalty}, {(int)f.CancelledByTeam}, {f.CancellationPenalty}, {Esc(f.CreatedDate)}, {Esc(f.ModifiedDate)});");
        sb.AppendLine();
    }

    private static void AppendFrameResultInserts(StringBuilder sb, List<Fixture> fixtures)
    {
        var frames = fixtures.Where(f => f.Frames.Count > 0)
            .SelectMany(f => f.Frames.Select(fr => (FixtureId: f.Id, Frame: fr)))
            .ToList();
        if (frames.Count == 0) return;
        sb.AppendLine($"-- Frame Results ({frames.Count})");
        foreach (var (fixtureId, fr) in frames)
            sb.AppendLine($"INSERT INTO FrameResults (FixtureId, Number, HomePlayerId, AwayPlayerId, HomePlayer2Id, AwayPlayer2Id, Winner, EightBall, IsDoubles, HomeOppRating, HomePlayerRating, AwayOppRating, AwayPlayerRating, WeekNo) VALUES ({Esc(fixtureId)}, {fr.Number}, {Esc(fr.HomePlayerId)}, {Esc(fr.AwayPlayerId)}, {Esc(fr.HomePlayer2Id)}, {Esc(fr.AwayPlayer2Id)}, {(int)fr.Winner}, {Bool(fr.EightBall)}, {Bool(fr.IsDoubles)}, {NullInt(fr.HomeOppRating)}, {NullInt(fr.HomePlayerRating)}, {NullInt(fr.AwayOppRating)}, {NullInt(fr.AwayPlayerRating)}, {NullInt(fr.WeekNo)});");
        sb.AppendLine();
    }

    private static void AppendCompetitionInserts(StringBuilder sb, List<Competition> competitions)
    {
        if (competitions.Count == 0) return;
        sb.AppendLine($"-- Competitions ({competitions.Count})");
        foreach (var c in competitions)
            sb.AppendLine($"INSERT INTO Competitions (Id, SeasonId, Name, Format, Status, StartDate, Notes, BestOf, RandomDraw, CreatedDate, PlateCompetitionId, ParentCompetitionId) VALUES ({Esc(c.Id)}, {Esc(c.SeasonId)}, {Esc(c.Name)}, {(int)c.Format}, {(int)c.Status}, {Esc(c.StartDate)}, {Esc(c.Notes)}, {c.BestOf}, {Bool(c.RandomDraw)}, {Esc(c.CreatedDate)}, {Esc(c.PlateCompetitionId)}, {Esc(c.ParentCompetitionId)});");
        sb.AppendLine();
    }

    private static void AppendDoublesPairingInserts(StringBuilder sb, List<DoublesPairing> pairings)
    {
        if (pairings.Count == 0) return;
        sb.AppendLine($"-- Doubles Pairings ({pairings.Count})");
        foreach (var dp in pairings)
            sb.AppendLine($"INSERT INTO DoublesPairings (Id, SeasonId, DivisionId, TeamId, Player1Id, Player2Id, Player1Name, Player2Name, TeamName, Played, Won, Lost, BestRating, BestRatingDate, CurrentRating) VALUES ({Esc(dp.Id)}, {Esc(dp.SeasonId)}, {Esc(dp.DivisionId)}, {Esc(dp.TeamId)}, {Esc(dp.Player1Id)}, {Esc(dp.Player2Id)}, {Esc(dp.Player1Name)}, {Esc(dp.Player2Name)}, {Esc(dp.TeamName)}, {dp.Played}, {dp.Won}, {dp.Lost}, {dp.BestRating}, {Esc(dp.BestRatingDate)}, {dp.CurrentRating});");
        sb.AppendLine();
    }

    // --------------------------------------------------------
    //  Value formatters
    // --------------------------------------------------------

    private static string Esc(Guid value) => $"'{value}'";
    private static string Esc(Guid? value) => value.HasValue ? $"'{value.Value}'" : "NULL";
    private static string Esc(string? value) => value == null ? "NULL" : $"'{value.Replace("'", "''")}'";
    private static string Esc(DateTime value) => $"'{value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}'";
    private static string Esc(DateTime? value) => value.HasValue ? Esc(value.Value) : "NULL";
    private static string Esc(TimeSpan value) => $"'{value:hh\\:mm\\:ss}'";
    private static string Bool(bool value) => value ? "1" : "0";
    private static string NullInt(int? value) => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "NULL";
}
