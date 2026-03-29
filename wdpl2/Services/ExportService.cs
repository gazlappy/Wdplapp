using System.Text;
using Wdpl2.Helpers;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Generates exportable reports as HTML (can be printed to PDF via system print).
/// Also supports CSV export for spreadsheet use.
/// </summary>
public static class ExportService
{
    /// <summary>
    /// Export league table to CSV for the given division.
    /// </summary>
    public static string GenerateLeagueTableCsv(
        List<Fixture> fixtures, List<Team> teams, Division division, AppSettings settings)
    {
        var divTeams = teams.Where(t => t.DivisionId == division.Id).ToList();
        var divFixtures = fixtures.Where(f => f.DivisionId == division.Id && f.Frames.Count > 0).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Position,Team,Played,Won,Drawn,Lost,Frames For,Frames Against,Frame Diff,Points");

        var standings = CalculateStandings(divFixtures, divTeams, settings);

        int pos = 1;
        foreach (var s in standings)
        {
            sb.AppendLine($"{pos},{EscapeCsv(s.TeamName)},{s.Played},{s.Won},{s.Drawn},{s.Lost},{s.FramesFor},{s.FramesAgainst},{s.FrameDiff},{s.Points}");
            pos++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Export fixtures to CSV.
    /// </summary>
    public static string GenerateFixturesCsv(
        List<Fixture> fixtures, List<Team> teams, List<Venue> venues, List<Division> divisions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Division,Home Team,Away Team,Home Score,Away Score,Venue,Status");

        var teamLookup = teams.ToDictionary(t => t.Id, t => t.Name ?? "");
        var venueLookup = venues.ToDictionary(v => v.Id, v => v.Name ?? "");
        var divLookup = divisions.ToDictionary(d => d.Id, d => d.Name ?? "");

        foreach (var f in fixtures.OrderBy(f => f.Date))
        {
            var div = f.DivisionId.HasValue && divLookup.TryGetValue(f.DivisionId.Value, out var dn) ? dn : "";
            var home = teamLookup.TryGetValue(f.HomeTeamId, out var hn) ? hn : "?";
            var away = teamLookup.TryGetValue(f.AwayTeamId, out var an) ? an : "?";
            var venue = f.VenueId.HasValue && venueLookup.TryGetValue(f.VenueId.Value, out var vn) ? vn : "";
            var status = f.Frames.Count > 0 ? "Completed" : "Scheduled";

            sb.AppendLine($"{f.Date:yyyy-MM-dd HH:mm},{EscapeCsv(div)},{EscapeCsv(home)},{EscapeCsv(away)},{f.HomeScore},{f.AwayScore},{EscapeCsv(venue)},{status}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Export player ratings to CSV.
    /// </summary>
    public static string GeneratePlayerRatingsCsv(
        List<Fixture> fixtures, List<Player> players, List<Team> teams,
        AppSettings settings, DateTime seasonStartDate)
    {
        var ratings = RatingCalculator.CalculateAllRatings(
            fixtures.Where(f => f.Frames.Count > 0).ToList(),
            players, teams, settings, seasonStartDate);

        var sb = new StringBuilder();
        sb.AppendLine("Position,Player,Team,Played,Won,Lost,Win%,8-Balls,Rating");

        int pos = 1;
        foreach (var r in ratings.Values.OrderByDescending(r => r.Rating))
        {
            sb.AppendLine($"{pos},{EscapeCsv(r.PlayerName)},{EscapeCsv(r.TeamName)},{r.Played},{r.Wins},{r.Losses},{r.WinPercentage:F1},{r.EightBalls},{r.Rating}");
            pos++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Export league table as shareable HTML with inline styles.
    /// </summary>
    public static string GenerateLeagueTableHtml(
        List<Fixture> fixtures, List<Team> teams, Division division, AppSettings settings)
    {
        var standings = CalculateStandings(
            fixtures.Where(f => f.DivisionId == division.Id && f.Frames.Count > 0).ToList(),
            teams.Where(t => t.DivisionId == division.Id).ToList(),
            settings);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
        sb.AppendLine("<style>");
        sb.AppendLine("body{font-family:Arial,sans-serif;margin:20px}");
        sb.AppendLine("h1{color:#0284C7}");
        sb.AppendLine("table{border-collapse:collapse;width:100%}");
        sb.AppendLine("th,td{border:1px solid #ddd;padding:8px;text-align:center}");
        sb.AppendLine("th{background:#0284C7;color:white}");
        sb.AppendLine("tr:nth-child(even){background:#f8f9fa}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<h1>{division.Name} - League Table</h1>");
        sb.AppendLine("<table><tr><th>#</th><th>Team</th><th>P</th><th>W</th><th>D</th><th>L</th><th>FF</th><th>FA</th><th>FD</th><th>Pts</th></tr>");

        int pos = 1;
        foreach (var s in standings)
        {
            sb.AppendLine($"<tr><td>{pos}</td><td style='text-align:left'>{s.TeamName}</td><td>{s.Played}</td><td>{s.Won}</td><td>{s.Drawn}</td><td>{s.Lost}</td><td>{s.FramesFor}</td><td>{s.FramesAgainst}</td><td>{s.FrameDiff}</td><td><b>{s.Points}</b></td></tr>");
            pos++;
        }

        sb.AppendLine("</table>");
        sb.AppendLine($"<p style='color:#888;font-size:12px'>Generated {DateTime.Now:dd MMM yyyy HH:mm}</p>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }

    /// <summary>
    /// Save content to a file and share it via the system share sheet.
    /// </summary>
    public static async Task ShareFileAsync(string content, string filename, string title = "Export")
    {
        var filePath = Path.Combine(FileSystem.CacheDirectory, filename);
        await File.WriteAllTextAsync(filePath, content);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(filePath)
        });
    }

    private sealed class TeamStanding
    {
        public string TeamName { get; set; } = "";
        public Guid TeamId { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int FramesFor { get; set; }
        public int FramesAgainst { get; set; }
        public int FrameDiff => FramesFor - FramesAgainst;
        public int Points { get; set; }
    }

    private static List<TeamStanding> CalculateStandings(
        List<Fixture> fixtures, List<Team> teams, AppSettings settings)
    {
        var standings = teams.ToDictionary(t => t.Id, t => new TeamStanding
        {
            TeamId = t.Id,
            TeamName = t.Name ?? ""
        });

        var pointsForWin = settings?.MatchWinBonus ?? 3;
        var pointsForDraw = settings?.MatchDrawBonus ?? 1;

        foreach (var f in fixtures)
        {
            if (!standings.TryGetValue(f.HomeTeamId, out var home)) continue;
            if (!standings.TryGetValue(f.AwayTeamId, out var away)) continue;

            home.Played++;
            away.Played++;
            home.FramesFor += f.HomeScore;
            home.FramesAgainst += f.AwayScore;
            away.FramesFor += f.AwayScore;
            away.FramesAgainst += f.HomeScore;

            if (f.HomeScore > f.AwayScore)
            {
                home.Won++;
                away.Lost++;
                home.Points += pointsForWin;
            }
            else if (f.AwayScore > f.HomeScore)
            {
                away.Won++;
                home.Lost++;
                away.Points += pointsForWin;
            }
            else
            {
                home.Drawn++;
                away.Drawn++;
                home.Points += pointsForDraw;
                away.Points += pointsForDraw;
            }
        }

        return StandingsSorter.Sort(
            standings.Values,
            settings,
            s => s.Points,
            s => s.FramesFor,
            s => s.FramesAgainst,
            s => s.Won,
            s => s.TeamId,
            fixtures);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    /// <summary>
    /// Generate a printable blank scorecard HTML for a fixture.
    /// </summary>
    public static string GenerateBlankScorecardHtml(
        Fixture fixture, List<Team> teams, List<Player> players, List<Venue> venues, int framesPerMatch)
    {
        var home = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId)?.Name ?? "Home";
        var away = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId)?.Name ?? "Away";
        var venue = fixture.VenueId.HasValue
            ? venues.FirstOrDefault(v => v.Id == fixture.VenueId.Value)?.Name ?? ""
            : "";

        var homePlayers = players.Where(p => p.TeamId == fixture.HomeTeamId && p.IsActive).OrderBy(p => p.FullName).ToList();
        var awayPlayers = players.Where(p => p.TeamId == fixture.AwayTeamId && p.IsActive).OrderBy(p => p.FullName).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/>");
        sb.AppendLine("<style>");
        sb.AppendLine("@page{size:landscape;margin:10mm}");
        sb.AppendLine("body{font-family:Arial,sans-serif;margin:10px;font-size:11px}");
        sb.AppendLine("h2{text-align:center;margin:4px 0}");
        sb.AppendLine(".info{text-align:center;color:#666;margin-bottom:8px}");
        sb.AppendLine("table{border-collapse:collapse;width:100%;margin-top:6px}");
        sb.AppendLine("th,td{border:1px solid #333;padding:6px 10px;text-align:center}");
        sb.AppendLine("th{background:#0284C7;color:white;font-size:10px}");
        sb.AppendLine(".frame-cell{min-width:60px;height:24px}");
        sb.AppendLine(".roster{display:flex;gap:20px;margin-top:8px}");
        sb.AppendLine(".roster div{flex:1}");
        sb.AppendLine(".roster ul{list-style:none;padding:0;margin:0}");
        sb.AppendLine(".roster li{padding:2px 0;border-bottom:1px dotted #ccc}");
        sb.AppendLine("</style></head><body>");

        sb.AppendLine($"<h2>{home} vs {away}</h2>");
        sb.AppendLine($"<div class='info'>{fixture.Date:dddd dd MMMM yyyy, HH:mm} | {venue}</div>");

        sb.AppendLine("<table><tr><th>#</th><th>Home Player</th><th>Score</th><th>Score</th><th>Away Player</th><th>8B</th></tr>");
        for (int i = 1; i <= framesPerMatch; i++)
            sb.AppendLine($"<tr><td>{i}</td><td class='frame-cell'></td><td class='frame-cell'></td><td class='frame-cell'></td><td class='frame-cell'></td><td class='frame-cell'></td></tr>");
        sb.AppendLine("<tr><td colspan='2'><b>TOTAL</b></td><td class='frame-cell'></td><td class='frame-cell'></td><td colspan='2'></td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<div class='roster'>");
        sb.AppendLine($"<div><strong>{home} Roster</strong><ul>");
        foreach (var p in homePlayers) sb.AppendLine($"<li>{p.FullName}</li>");
        sb.AppendLine("</ul></div>");
        sb.AppendLine($"<div><strong>{away} Roster</strong><ul>");
        foreach (var p in awayPlayers) sb.AppendLine($"<li>{p.FullName}</li>");
        sb.AppendLine("</ul></div></div>");

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    /// <summary>
    /// Get recent results as a feed of text lines for dashboard display.
    /// </summary>
    public static List<string> GetRecentResultsFeed(
        List<Fixture> fixtures, List<Team> teams, int count = 10)
    {
        var teamLookup = teams.ToDictionary(t => t.Id, t => t.Name ?? "?");
        return fixtures
            .Where(f => f.Frames.Count > 0)
            .OrderByDescending(f => f.ModifiedDate ?? f.Date)
            .Take(count)
            .Select(f =>
            {
                var home = teamLookup.GetValueOrDefault(f.HomeTeamId, "?");
                var away = teamLookup.GetValueOrDefault(f.AwayTeamId, "?");
                return $"{home} {f.HomeScore}-{f.AwayScore} {away} ({f.Date:ddd dd MMM})";
            })
            .ToList();
    }
}
