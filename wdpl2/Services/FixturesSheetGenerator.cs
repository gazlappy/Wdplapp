using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Simplified settings for the printable fixtures sheet.
/// </summary>
public class FixturesSheetSettings
{
    public string LeagueName { get; set; } = "";
    public string SeasonName { get; set; } = "";
    public bool ShowTeamNumbers { get; set; } = true;
    public bool ShowDivisionLists { get; set; } = true;
    public bool ShowVenueInfo { get; set; } = true;
    public bool ShowSpecialEvents { get; set; } = true;
    public bool ShowFooterNotes { get; set; } = true;
    public bool IsLandscape { get; set; } = true;
    public string AccentColor { get; set; } = "#1a1a8b";
    [JsonConverter(typeof(LenientStringConverter))]
    public string? FooterNotes { get; set; }
    [JsonConverter(typeof(LenientStringConverter))]
    public string? FooterWebsite { get; set; }
    public string? FooterEmail { get; set; }
    public string? FooterContactName { get; set; }
    public string? FooterContactPhone { get; set; }
    public string? FooterReportName { get; set; }
    public string? FooterReportPhone { get; set; }
    public string? LogoBase64 { get; set; }
    public List<SpecialEvent> SpecialEvents { get; set; } = [];

    /// <summary>Absorbs unknown properties from older JSON schemas so deserialization doesn't fail.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

    /// <summary>Returns the logo data to use (custom or from league).</summary>
    public string? GetEffectiveLogoData() => LogoBase64;
}

/// <summary>
/// Converts JSON values of any type to string, handling old schemas where a property
/// may have been an array, object, number, or bool instead of a string.
/// </summary>
public class LenientStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Null => null,
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            // For arrays/objects, skip past them and return null
            JsonTokenType.StartArray or JsonTokenType.StartObject => SkipAndReturnNull(ref reader),
            _ => null,
        };
    }

    private static string? SkipAndReturnNull(ref Utf8JsonReader reader)
    {
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}

/// <summary>
/// A special event annotation for a fixture week.
/// </summary>
public class SpecialEvent
{
    public DateTime Date { get; set; }
    public string DayOfWeek { get; set; } = "";
    public string Description { get; set; } = "";
    public string Color { get; set; } = "#FDE68A";
}

/// <summary>
/// Generates a printable fixtures sheet as HTML, matching the classic WDPL paper format:
/// bordered tables with month colspan headers, ordinal dates, event annotations,
/// division team lists, venue info, and footer.
/// </summary>
public class FixturesSheetGenerator
{
    private readonly LeagueData _league;
    private readonly FixturesSheetSettings _settings;

    public FixturesSheetGenerator(LeagueData league, FixturesSheetSettings settings)
    {
        _league = league;
        _settings = settings;
    }

    /// <summary>Generate a full standalone HTML page.</summary>
    public string GenerateFixturesSheet(Guid seasonId, List<Guid>? divisionIds = null)
    {
        var (divisions, venues, teams, _, fixtures) = _league.GetSeasonData(seasonId);
        var season = _league.Seasons.FirstOrDefault(s => s.Id == seasonId)
            ?? throw new InvalidOperationException("Season not found");

        FilterByDivisions(ref divisions, ref teams, ref fixtures, divisionIds);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine($"    <title>{Esc(_settings.LeagueName)} — Fixtures {Esc(season.Name)}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine(GenerateCSS());
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine(GenerateContent(divisions, venues, teams, fixtures, season));
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    /// <summary>Generate just the inner content (for embedding in the website).</summary>
    public string GenerateEmbeddableContent(Guid seasonId, List<Guid>? divisionIds = null)
    {
        var (divisions, venues, teams, _, fixtures) = _league.GetSeasonData(seasonId);
        var season = _league.Seasons.FirstOrDefault(s => s.Id == seasonId);
        if (season == null) return "<p>Season not found</p>";
        FilterByDivisions(ref divisions, ref teams, ref fixtures, divisionIds);
        return GenerateContent(divisions, venues, teams, fixtures, season);
    }

    /// <summary>Get full CSS for standalone use.</summary>
    public string GetEmbeddableCSS() => GenerateCSS();

    /// <summary>Get CSS scoped for inline embedding (strips @page/@media print).</summary>
    public string GetScopedCSS()
    {
        var css = GenerateCSS();
        var sb = new StringBuilder();
        int braceDepth = 0;
        bool skipping = false;

        foreach (var rawLine in css.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.TrimStart();

            if (!skipping && trimmed.StartsWith("@page")) continue;
            if (!skipping && (trimmed.StartsWith("@media print") || trimmed.StartsWith("@media screen {")))
            {
                skipping = true; braceDepth = 0;
                foreach (var ch in line) { if (ch == '{') braceDepth++; if (ch == '}') braceDepth--; }
                if (braceDepth <= 0) skipping = false;
                continue;
            }
            if (skipping)
            {
                foreach (var ch in line) { if (ch == '{') braceDepth++; if (ch == '}') braceDepth--; }
                if (braceDepth <= 0) skipping = false;
                continue;
            }

            if (trimmed.StartsWith("html, body {") || trimmed.StartsWith("html, body{"))
                sb.AppendLine(line.Replace("html, body", ".fixtures-sheet-wrapper"));
            else if (trimmed.StartsWith("* {") || trimmed.StartsWith("*{"))
                sb.AppendLine(line.Replace("*", ".fixtures-sheet-wrapper *"));
            else
                sb.AppendLine(line);
        }
        return sb.ToString();
    }

    // ── Content ──────────────────────────────────────────────

    private string GenerateContent(List<Division> divisions, List<Venue> venues, List<Team> teams, List<Fixture> fixtures, Season season)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<div class=\"fixtures-sheet\">");

        // Title
        sb.AppendLine($"<h1 class=\"sheet-title\">{Esc(_settings.LeagueName)} {Esc(season.Name)} League</h1>");

        // Subtitle
        var divNames = string.Join(" &amp; ", divisions.Select(d => d.Name.ToUpperInvariant()));
        if (!string.IsNullOrEmpty(divNames))
            sb.AppendLine($"<div class=\"sheet-subtitle\">{divNames} FIXTURES</div>");

        // Fixture grid
        GenerateFixtureGrid(sb, divisions, teams, fixtures);

        // Special events / key dates
        if (_settings.ShowSpecialEvents && _settings.SpecialEvents.Count > 0)
            GenerateKeyDates(sb);

        // Division team lists
        if (_settings.ShowDivisionLists)
            GenerateDivisionLists(sb, divisions, teams, venues);

        // Venue telephone numbers
        if (_settings.ShowVenueInfo)
            GenerateVenueInfo(sb, venues);

        // Footer
        if (_settings.ShowFooterNotes)
            GenerateFooter(sb);

        sb.AppendLine("</div>");
        return sb.ToString();
    }

    // ── Fixture Grid ─────────────────────────────────────────

    private void GenerateFixtureGrid(StringBuilder sb, List<Division> divisions, List<Team> teams, List<Fixture> fixtures)
    {
        if (fixtures.Count == 0) { sb.AppendLine("<p>No fixtures scheduled.</p>"); return; }

        // Build team number lookup
        var teamNumbers = new Dictionary<Guid, int>();
        int num = 1;
        foreach (var div in divisions)
        {
            var divTeams = teams.Where(t => t.DivisionId == div.Id).OrderBy(t => t.Name).ToList();
            foreach (var t in divTeams)
                teamNumbers[t.Id] = num++;
        }

        // Group fixtures by week date
        var weeks = fixtures
            .GroupBy(f => f.Date.Date)
            .OrderBy(g => g.Key)
            .ToList();

        int maxFixturesPerWeek = weeks.Max(g => g.Count());

        // Group weeks by month
        var monthGroups = weeks
            .GroupBy(g => (g.Key.Year, g.Key.Month))
            .ToList();

        // Split into two half-season rows
        int splitAt = (monthGroups.Count + 1) / 2;
        var rows = new[] { monthGroups.Take(splitAt).ToList(), monthGroups.Skip(splitAt).ToList() }
            .Where(r => r.Count > 0).ToList();

        // Build event lookup
        var eventsByDate = _settings.SpecialEvents.ToDictionary(e => e.Date.Date, e => e.Description);

        foreach (var row in rows)
        {
            var rowWeeks = row.SelectMany(m => m).ToList();

            sb.AppendLine("<table class=\"cg\">");

            // Month header row
            sb.AppendLine("<tr class=\"cg-mh\">");
            sb.AppendLine("<th class=\"cg-ev\"></th>");
            foreach (var month in row)
            {
                var monthDate = new DateTime(month.Key.Year, month.Key.Month, 1);
                var name = monthDate.ToString("MMMM", CultureInfo.InvariantCulture).ToUpperInvariant();
                var color = GetMonthColor(month.Key.Month);
                sb.AppendLine($"<th colspan=\"{month.Count()}\" style=\"background:{color};\">{name}</th>");
            }
            sb.AppendLine("</tr>");

            // Date header row
            sb.AppendLine("<tr class=\"cg-dh\">");
            sb.AppendLine("<th class=\"cg-ev\"></th>");
            foreach (var week in rowWeeks)
            {
                var d = week.Key.Day;
                sb.AppendLine($"<th>{d}{GetDaySuffix(d)}</th>");
            }
            sb.AppendLine("</tr>");

            // Fixture rows
            for (int r = 0; r < maxFixturesPerWeek; r++)
            {
                sb.AppendLine("<tr class=\"cg-fr\">");

                // Event annotation column (first row only, spans all fixture rows)
                if (r == 0)
                {
                    var evts = rowWeeks
                        .Where(w => eventsByDate.ContainsKey(w.Key))
                        .Select(w => eventsByDate[w.Key])
                        .ToList();
                    var evtText = evts.Count > 0 ? string.Join("<br>", evts.Select(Esc)) : "";
                    sb.AppendLine($"<td class=\"cg-ev\" rowspan=\"{maxFixturesPerWeek}\">{evtText}</td>");
                }

                foreach (var week in rowWeeks)
                {
                    var weekFixtures = week.ToList();
                    if (r < weekFixtures.Count)
                    {
                        var f = weekFixtures[r];
                        int h = teamNumbers.GetValueOrDefault(f.HomeTeamId);
                        int a = teamNumbers.GetValueOrDefault(f.AwayTeamId);
                        if (_settings.ShowTeamNumbers && h > 0 && a > 0)
                            sb.AppendLine($"<td><strong>{h}</strong> v {a}</td>");
                        else
                        {
                            var hn = Esc(teams.FirstOrDefault(t => t.Id == f.HomeTeamId)?.Name ?? "?");
                            var an = Esc(teams.FirstOrDefault(t => t.Id == f.AwayTeamId)?.Name ?? "?");
                            sb.AppendLine($"<td><strong>{hn}</strong> v {an}</td>");
                        }
                    }
                    else
                        sb.AppendLine("<td></td>");
                }
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table>");
        }
    }

    // ── Key Dates ────────────────────────────────────────────

    private void GenerateKeyDates(StringBuilder sb)
    {
        sb.AppendLine("<table class=\"kd\">");
        foreach (var evt in _settings.SpecialEvents.OrderBy(e => e.Date))
        {
            sb.AppendLine($"<tr style=\"background:{evt.Color};\">");
            sb.AppendLine($"<td class=\"kd-day\">{evt.DayOfWeek}</td>");
            sb.AppendLine($"<td class=\"kd-date\">{evt.Date:dd-MMM}</td>");
            sb.AppendLine($"<td class=\"kd-desc\">{Esc(evt.Description)}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</table>");
    }

    // ── Division Lists ───────────────────────────────────────

    private void GenerateDivisionLists(StringBuilder sb, List<Division> divisions, List<Team> teams, List<Venue> venues)
    {
        sb.AppendLine("<div class=\"div-lists\">");
        int num = 1;
        foreach (var div in divisions)
        {
            var divTeams = teams.Where(t => t.DivisionId == div.Id).OrderBy(t => t.Name).ToList();
            sb.AppendLine("<div class=\"div-card\">");
            sb.AppendLine($"<div class=\"div-hdr\">{Esc(div.Name)}</div>");
            sb.AppendLine("<table class=\"div-tbl\">");
            foreach (var t in divTeams)
            {
                var venue = venues.FirstOrDefault(v => v.Id == t.VenueId);
                var venueName = venue?.Name ?? "";
                var table = t.TableId.HasValue && venue != null
                    ? venue.Tables.FirstOrDefault(vt => vt.Id == t.TableId.Value)
                    : null;
                var tableInfo = table != null ? $" ({Esc(table.Label)})" : "";
                sb.AppendLine($"<tr><td class=\"div-num\">{num}</td><td class=\"div-name\">{Esc(t.Name)}</td><td class=\"div-venue\">{Esc(venueName)}{tableInfo}</td></tr>");
                num++;
            }
            sb.AppendLine("</table></div>");
        }
        sb.AppendLine("</div>");
    }

    // ── Venue Info ───────────────────────────────────────────

    private void GenerateVenueInfo(StringBuilder sb, List<Venue> venues)
    {
        var withAddress = venues.Where(v => !string.IsNullOrWhiteSpace(v.Address)).ToList();
        if (withAddress.Count == 0) return;

        sb.AppendLine("<div class=\"venue-section\">");
        sb.AppendLine("<div class=\"venue-hdr\">VENUE INFORMATION</div>");
        sb.AppendLine("<div class=\"venue-grid\">");
        foreach (var v in withAddress)
            sb.AppendLine($"<div class=\"venue-item\"><span class=\"venue-name\">{Esc(v.Name)}:</span> {Esc(v.Address!)}</div>");
        sb.AppendLine("</div></div>");
    }

    // ── Footer ───────────────────────────────────────────────

    private void GenerateFooter(StringBuilder sb)
    {
        sb.AppendLine("<div class=\"sheet-footer\">");
        if (!string.IsNullOrWhiteSpace(_settings.FooterNotes))
            sb.AppendLine($"<div class=\"footer-notes\">{Esc(_settings.FooterNotes)}</div>");

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(_settings.FooterContactName) && !string.IsNullOrWhiteSpace(_settings.FooterContactPhone))
            lines.Add($"Report Cancelled Matches to {Esc(_settings.FooterContactName)} on {Esc(_settings.FooterContactPhone)}");
        if (!string.IsNullOrWhiteSpace(_settings.FooterReportName) && !string.IsNullOrWhiteSpace(_settings.FooterReportPhone))
            lines.Add($"Report Cancelled Competition Fixtures to {Esc(_settings.FooterReportName)} on {Esc(_settings.FooterReportPhone)}");
        if (!string.IsNullOrWhiteSpace(_settings.FooterWebsite))
            lines.Add($"Web site: {Esc(_settings.FooterWebsite)}");
        if (!string.IsNullOrWhiteSpace(_settings.FooterEmail))
            lines.Add($"Email address: {Esc(_settings.FooterEmail)}");

        if (lines.Count > 0)
            sb.AppendLine($"<div class=\"footer-contacts\">{string.Join(" &nbsp;&nbsp;&nbsp; ", lines)}</div>");
        sb.AppendLine("</div>");
    }

    // ── CSS ──────────────────────────────────────────────────

    private string GenerateCSS()
    {
        var orientation = _settings.IsLandscape ? "landscape" : "portrait";
        var accent = _settings.AccentColor;

        return $$"""
@page { size: A4 {{orientation}}; margin: 8mm; }
* { box-sizing: border-box; margin: 0; padding: 0; }
html, body { font-family: Arial, Helvetica, sans-serif; font-size: 9pt; color: #1a1a1a; background: white; }

.fixtures-sheet { max-width: 100%; padding: 4mm; }

/* ── Title ── */
.sheet-title {
    text-align: center; font-size: 16pt; font-weight: 700;
    margin-bottom: 4px; font-style: italic;
}
.sheet-subtitle {
    text-align: center; font-size: 12pt; font-weight: 700;
    background: {{accent}}; color: white;
    padding: 4px 0; margin-bottom: 8px;
    letter-spacing: 2px; text-transform: uppercase;
}

/* ── Classic Grid ── */
.cg {
    width: 100%; border-collapse: collapse; table-layout: fixed;
    margin-bottom: 6px; border: 2px solid #222;
}
.cg th, .cg td {
    border: 1px solid #888; text-align: center;
    vertical-align: middle; padding: 2px 3px;
    font-size: 8pt; line-height: 1.3;
}
.cg .cg-mh th {
    font-weight: 700; font-size: 9pt; text-transform: uppercase;
    letter-spacing: 1.5px; padding: 3px 6px;
    border: 2px solid #222; color: #1a1a1a;
}
.cg .cg-dh th {
    font-weight: 600; font-size: 7.5pt; padding: 2px;
    background: #f0f0f0; border-bottom: 2px solid #555;
    white-space: nowrap;
}
.cg .cg-ev {
    width: 75px; min-width: 60px; max-width: 90px;
    font-size: 6.5pt; font-weight: 600; text-align: left;
    padding: 2px 4px; line-height: 1.2; vertical-align: top;
    color: #334155; background: #fafafa;
    border-right: 2px solid #555;
}
.cg .cg-fr td { font-weight: 400; color: #334155; white-space: nowrap; }
.cg .cg-fr td strong { font-weight: 800; }

/* ── Key Dates ── */
.kd {
    width: 100%; border-collapse: collapse;
    margin: 8px 0; border: 2px solid #222;
}
.kd td { border: 1px solid #888; padding: 3px 8px; font-size: 8pt; }
.kd .kd-day { font-weight: 700; width: 70px; }
.kd .kd-date { width: 60px; }
.kd .kd-desc { font-weight: 600; }

/* ── Division Lists ── */
.div-lists {
    display: flex; gap: 10px; margin: 8px 0; flex-wrap: wrap;
}
.div-card { flex: 1; min-width: 250px; }
.div-hdr {
    background: {{accent}}; color: white;
    font-weight: 700; font-size: 9pt; text-align: center;
    padding: 4px; text-transform: uppercase; letter-spacing: 1px;
}
.div-tbl { width: 100%; border-collapse: collapse; border: 2px solid #222; }
.div-tbl td { border: 1px solid #888; padding: 2px 6px; font-size: 8pt; }
.div-tbl .div-num { width: 24px; text-align: center; font-weight: 700; }
.div-tbl .div-name { font-weight: 600; text-transform: uppercase; }
.div-tbl .div-venue { color: #555; }

/* ── Venue Info ── */
.venue-section { margin: 8px 0; border: 2px solid #222; }
.venue-hdr {
    background: #f0f0f0; font-weight: 700; font-size: 8pt;
    text-align: center; padding: 4px; letter-spacing: 1px;
    border-bottom: 1px solid #888;
}
.venue-grid {
    display: grid; grid-template-columns: 1fr 1fr;
    font-size: 8pt; padding: 4px 8px; gap: 2px 16px;
}
.venue-name { font-weight: 600; text-transform: uppercase; }

/* ── Footer ── */
.sheet-footer {
    margin-top: 8px; text-align: center;
    font-size: 7.5pt; line-height: 1.5;
    border-top: 1px solid #ccc; padding-top: 4px;
}
.footer-notes { font-weight: 700; color: #B91C1C; margin-bottom: 4px; }
.footer-contacts { color: #334155; }

/* ── Print ── */
@media print {
    html, body { margin: 0; padding: 0; -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }
    .fixtures-sheet { padding: 0; }
}
@media screen {
    body { background: #e2e8f0; padding: 16px; }
    .fixtures-sheet {
        max-width: 1100px; margin: 0 auto;
        background: white; padding: 16px;
        box-shadow: 0 4px 20px rgba(0,0,0,0.12);
    }
}
""";
    }

    // ── Helpers ───────────────────────────────────────────────

    private static void FilterByDivisions(ref List<Division> divisions, ref List<Team> teams, ref List<Fixture> fixtures, List<Guid>? divisionIds)
    {
        if (divisionIds is not { Count: > 0 }) return;
        divisions = divisions.Where(d => divisionIds.Contains(d.Id)).ToList();
        teams = teams.Where(t => t.DivisionId.HasValue && divisionIds.Contains(t.DivisionId.Value)).ToList();
        fixtures = fixtures.Where(f => f.DivisionId.HasValue && divisionIds.Contains(f.DivisionId.Value)).ToList();
    }

    private static string GetMonthColor(int month) => month switch
    {
        1  => "#C4B5FD",  // purple
        2  => "#86EFAC",  // green
        3  => "#FDBA74",  // orange
        4  => "#93C5FD",  // blue
        5  => "#FCA5A5",  // red
        6  => "#A5F3FC",  // cyan
        7  => "#FDE68A",  // yellow
        8  => "#C4B5FD",  // purple
        9  => "#86EFAC",  // green
        10 => "#FDBA74",  // orange
        11 => "#86EFAC",  // green
        12 => "#67E8F9",  // teal
        _  => "#E2E8F0",
    };

    private static string GetDaySuffix(int day) => (day % 100) switch
    {
        11 or 12 or 13 => "th",
        _ => (day % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" }
    };

    private static string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);
}
