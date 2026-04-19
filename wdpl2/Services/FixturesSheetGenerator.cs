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
        var logoData = _settings.GetEffectiveLogoData();
        if (!string.IsNullOrWhiteSpace(logoData))
        {
            var src = logoData.StartsWith("data:") ? logoData : $"data:image/png;base64,{logoData}";
            sb.AppendLine($"<div class=\"sheet-title\"><img class=\"sheet-logo\" src=\"{src}\" alt=\"Logo\" /><span>{Esc(_settings.LeagueName)} {Esc(season.Name)} League</span></div>");
        }
        else
        {
            sb.AppendLine($"<h1 class=\"sheet-title\">{Esc(_settings.LeagueName)} {Esc(season.Name)} League</h1>");
        }

        // Subtitle
        var divNames = string.Join(" &amp; ", divisions.Select(d => d.Name.ToUpperInvariant()));
        if (!string.IsNullOrEmpty(divNames))
            sb.AppendLine($"<div class=\"sheet-subtitle\">{divNames} FIXTURES</div>");

        // Fixture grid
        GenerateFixtureGrid(sb, divisions, teams, fixtures, season);

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

    private void GenerateFixtureGrid(StringBuilder sb, List<Division> divisions, List<Team> teams, List<Fixture> fixtures, Season season)
    {
        if (fixtures.Count == 0) { sb.AppendLine("<p>No fixtures scheduled.</p>"); return; }

        // Use fixtures from the first division only — since all divisions share
        // the same round-robin structure with per-division numbering (1..N),
        // one grid serves all divisions. The division team lists act as the key.
        var firstDiv = divisions.FirstOrDefault();
        var gridFixtures = firstDiv != null
            ? fixtures.Where(f => f.DivisionId == firstDiv.Id).ToList()
            : fixtures;

        // Build team number lookup for the first division
        var teamNumbers = new Dictionary<Guid, int>();
        if (firstDiv != null)
        {
            int num = 1;
            foreach (var t in teams.Where(t => t.DivisionId == firstDiv.Id).OrderBy(t => t.Name))
                teamNumbers[t.Id] = num++;
        }

        GenerateFixtureGridRows(sb, gridFixtures, teams, teamNumbers, season);
    }

    private void GenerateFixtureGridRows(StringBuilder sb, List<Fixture> fixtures, List<Team> teams, Dictionary<Guid, int> teamNumbers, Season season)
    {
        // Group fixtures by week date
        var weeks = fixtures
            .GroupBy(f => f.Date.Date)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Build event lookup from manual special events
        var eventsByDate = _settings.SpecialEvents
            .GroupBy(e => e.Date.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Merge season blackout/exclusion dates as event cards
        foreach (var blackout in season.BlackoutDates)
        {
            var d = blackout.Date;
            var title = season.BlackoutDateTitles.TryGetValue(d.ToString("yyyy-MM-dd"), out var t) && !string.IsNullOrWhiteSpace(t)
                ? t
                : "No Fixtures";

            var evt = new SpecialEvent
            {
                Date = d,
                DayOfWeek = d.DayOfWeek.ToString(),
                Description = title,
                Color = "#FECACA"
            };

            if (eventsByDate.TryGetValue(d, out var existing))
                existing.Add(evt);
            else
                eventsByDate[d] = [evt];
        }

        // Merge all dates (fixture weeks + standalone events) into one timeline
        var allDates = weeks.Keys.Union(eventsByDate.Keys).OrderBy(d => d).ToList();

        sb.AppendLine("<div class=\"wk-grid\">");

        foreach (var date in allDates)
        {
            var monthName = date.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
            var day = date.Day;
            var color = GetMonthColor(date.Month);
            var hasFixtures = weeks.TryGetValue(date, out var weekFixtures);
            var hasEvents = eventsByDate.TryGetValue(date, out var events);

            if (hasFixtures)
            {
                // Fixture week card (may also have an event annotation)
                sb.AppendLine("<div class=\"wk-card\">");
                sb.AppendLine($"<div class=\"wk-hdr\" style=\"background:{color};\">");
                sb.AppendLine($"<div class=\"wk-day\">{day}</div>");
                sb.AppendLine($"<div class=\"wk-month\">{monthName}</div>");
                sb.AppendLine("</div>");

                if (hasEvents)
                {
                    foreach (var evt in events!)
                        sb.AppendLine($"<div class=\"wk-event\">{Esc(evt.Description)}</div>");
                }

                sb.AppendLine("<div class=\"wk-fixtures\">");
                foreach (var f in weekFixtures!)
                {
                    int h = teamNumbers.GetValueOrDefault(f.HomeTeamId);
                    int a = teamNumbers.GetValueOrDefault(f.AwayTeamId);
                    if (_settings.ShowTeamNumbers && h > 0 && a > 0)
                        sb.AppendLine($"<div class=\"wk-match\"><span class=\"wk-home\">{h}</span><span class=\"wk-v\">v</span><span class=\"wk-away\">{a}</span></div>");
                    else
                    {
                        var hn = Esc(teams.FirstOrDefault(t => t.Id == f.HomeTeamId)?.Name ?? "?");
                        var an = Esc(teams.FirstOrDefault(t => t.Id == f.AwayTeamId)?.Name ?? "?");
                        sb.AppendLine($"<div class=\"wk-match\"><span class=\"wk-home\">{hn}</span><span class=\"wk-v\">v</span><span class=\"wk-away\">{an}</span></div>");
                    }
                }
                sb.AppendLine("</div>");
                sb.AppendLine("</div>");
            }
            else if (hasEvents)
            {
                // Standalone event card (no fixtures on this date)
                foreach (var evt in events!)
                {
                    var evtColor = !string.IsNullOrWhiteSpace(evt.Color) ? evt.Color : "#FDE68A";
                    sb.AppendLine("<div class=\"wk-card wk-card-event\">");
                    sb.AppendLine($"<div class=\"wk-hdr\" style=\"background:{evtColor};\">");
                    sb.AppendLine($"<div class=\"wk-day\">{day}</div>");
                    sb.AppendLine($"<div class=\"wk-month\">{monthName}</div>");
                    sb.AppendLine("</div>");
                    sb.AppendLine($"<div class=\"wk-event-body\">{Esc(evt.Description)}</div>");
                    sb.AppendLine("</div>");
                }
            }
        }

        sb.AppendLine("</div>");
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
        foreach (var div in divisions)
        {
            int num = 1;
            var divTeams = teams.Where(t => t.DivisionId == div.Id).OrderBy(t => t.Name).ToList();
            sb.AppendLine("<div class=\"div-card\">");
            var divColor = GetDivisionColor(div.Name);
            sb.AppendLine($"<div class=\"div-hdr\" style=\"background:linear-gradient(180deg,{divColor}cc 0%,{divColor} 35%,{divColor}dd 50%,{divColor} 65%,{divColor}88 100%);\">{Esc(div.Name)}</div>");
            sb.AppendLine("<table class=\"div-tbl\">");
            foreach (var t in divTeams)
            {
                var venue = venues.FirstOrDefault(v => v.Id == t.VenueId);
                var venueName = venue?.Name ?? "";
                var table = t.TableId.HasValue && venue != null
                    ? venue.Tables.FirstOrDefault(vt => vt.Id == t.TableId.Value)
                    : null;
                var tableInfo = table != null ? $" ({Esc(table.Label)})" : "";
                sb.AppendLine($"<tr><td class=\"div-num\"><span style=\"background:linear-gradient(180deg,{divColor}cc 0%,{divColor} 40%,{divColor}dd 55%,{divColor}88 100%);\" class=\"div-badge\">{num}</span></td><td class=\"div-name\">{Esc(t.Name)}</td><td class=\"div-venue\">{Esc(venueName)}{tableInfo}</td></tr>");
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
html, body {
    font-family: 'Segoe UI', Inter, -apple-system, BlinkMacSystemFont, sans-serif;
    font-size: 9pt; color: #1E293B; background: #0F172A;
}
.fixtures-sheet { max-width: 100%; padding: 4mm; }

/* ── Title ── */
.sheet-title {
    text-align: center; font-size: 24pt; font-weight: 900;
    margin: 0 0 0; letter-spacing: 2px;
    color: white; text-transform: uppercase;
    background: linear-gradient(180deg, {{accent}}cc 0%, {{accent}} 35%, {{accent}}dd 50%, {{accent}} 65%, {{accent}}88 100%);
    padding: 14px 0 12px;
    border-radius: 14px 14px 0 0;
    text-shadow: 0 1px 0 rgba(255,255,255,0.25), 0 2px 0 rgba(0,0,0,0.3), 0 4px 8px rgba(0,0,0,0.4);
    border-bottom: 3px solid rgba(0,0,0,0.2);
    box-shadow: inset 0 2px 0 rgba(255,255,255,0.4), inset 0 -1px 0 rgba(0,0,0,0.2);
}
.sheet-logo {
    height: 48px; width: auto; vertical-align: middle;
    margin-right: 14px; border-radius: 6px;
    filter: drop-shadow(0 2px 4px rgba(0,0,0,0.4));
}
.sheet-title span { vertical-align: middle; }
.sheet-subtitle {
    text-align: center; font-size: 9pt; font-weight: 700;
    background: linear-gradient(180deg, #2A2A2A 0%, #1A1A1A 40%, #222 50%, #181818 100%);
    color: #A0A0A0; padding: 7px 0; margin-bottom: 16px;
    letter-spacing: 5px; text-transform: uppercase;
    border-radius: 0 0 14px 14px;
    box-shadow: inset 0 1px 0 rgba(255,255,255,0.08), 0 4px 12px rgba(0,0,0,0.3);
    text-shadow: 0 1px 2px rgba(0,0,0,0.5);
}

/* ── Weekly Fixture Cards ── */
.wk-grid {
    display: flex; flex-wrap: wrap; gap: 10px; margin: 10px 0 18px;
    justify-content: center;
    perspective: 1200px;
}
.wk-card {
    border-radius: 12px; overflow: hidden;
    min-width: 88px; width: 88px; flex: 0 0 auto;
    background: linear-gradient(170deg, #F8F8F8 0%, #E8E8E8 20%, #F4F4F4 40%, #D0D0D0 60%, #E0E0E0 80%, #C0C0C0 100%);
    border: 1px solid rgba(255,255,255,0.6);
    box-shadow:
        inset 0 2px 0 rgba(255,255,255,0.8),
        inset 0 -1px 0 rgba(0,0,0,0.1),
        0 6px 0 -2px #A0A0A0,
        0 10px 0 -4px #B8B8B8,
        0 12px 24px rgba(0,0,0,0.22);
    transform: translateY(0);
    transition: transform 0.2s, box-shadow 0.2s;
}
.wk-card:hover {
    transform: translateY(-3px);
    box-shadow:
        inset 0 2px 0 rgba(255,255,255,0.8),
        inset 0 -1px 0 rgba(0,0,0,0.1),
        0 8px 0 -2px #A0A0A0,
        0 12px 0 -4px #B8B8B8,
        0 18px 32px rgba(0,0,0,0.3);
}
.wk-hdr {
    display: flex; flex-direction: column; align-items: center;
    padding: 6px 0 4px; position: relative;
    border-bottom: 1px solid rgba(0,0,0,0.08);
    background-image: linear-gradient(180deg, rgba(255,255,255,0.65) 0%, rgba(255,255,255,0.1) 50%, rgba(255,255,255,0) 100%);
}
.wk-day {
    font-size: 24pt; font-weight: 900; color: #2D2D2D;
    line-height: 1; letter-spacing: -1px;
    background: linear-gradient(180deg, #5A5A5A 0%, #333 40%, #1A1A1A 100%);
    -webkit-background-clip: text; -webkit-text-fill-color: transparent;
    background-clip: text;
    filter: drop-shadow(0 1px 0 rgba(255,255,255,0.9));
}
.wk-month {
    font-size: 6pt; text-transform: uppercase; letter-spacing: 2.5px;
    color: #555; font-weight: 800;
}
.wk-event {
    font-size: 5pt; font-weight: 800; text-align: center;
    padding: 2px 4px; color: white;
    background: linear-gradient(180deg, #F59E0B, #D97706);
    text-transform: uppercase; letter-spacing: 0.3px;
    line-height: 1.2;
    box-shadow: inset 0 1px 0 rgba(255,255,255,0.25);
    text-shadow: 0 1px 1px rgba(0,0,0,0.2);
}
.wk-fixtures { padding: 4px 7px 6px; }
.wk-match {
    display: flex; align-items: center; justify-content: center;
    gap: 3px; font-size: 8pt; padding: 2px 0;
    white-space: nowrap; font-variant-numeric: tabular-nums;
}
.wk-match + .wk-match { border-top: 1px solid #C8C8C8; }
.wk-home {
    font-weight: 900; color: {{accent}};
    min-width: 10px; text-align: right;
    text-shadow: 0 1px 0 rgba(255,255,255,0.6);
}
.wk-v { font-size: 6pt; color: #94A3B8; font-weight: 600; }
.wk-away {
    font-weight: 600; color: #475569;
    min-width: 10px; text-align: left;
}

/* Event-only cards */
.wk-card-event {
    background: linear-gradient(170deg, #FFF0D0 0%, #F0D890 20%, #FFECB0 40%, #E0C878 60%, #F0D890 80%, #D4B868 100%);
    border: 1px solid rgba(255,255,255,0.6);
    box-shadow:
        inset 0 2px 0 rgba(255,255,255,0.7),
        inset 0 -1px 0 rgba(0,0,0,0.06),
        0 6px 0 -2px #B8A060,
        0 10px 0 -4px #C8B070,
        0 12px 24px rgba(120,90,20,0.18);
}
.wk-card-event .wk-day {
    color: #5C3D10;
    background: linear-gradient(180deg, #6B4A18, #3C2508);
    -webkit-background-clip: text; -webkit-text-fill-color: transparent;
    background-clip: text;
    filter: drop-shadow(0 1px 0 rgba(255,255,255,0.5));
}
.wk-card-event .wk-month { color: #78350F; }
.wk-event-body {
    font-size: 6.5pt; font-weight: 800; text-align: center;
    padding: 5px 6px; color: #78350F; line-height: 1.3;
    text-transform: uppercase; letter-spacing: 0.3px;
}

/* ── Key Dates ── */
.kd {
    width: 100%; border-collapse: collapse;
    margin: 10px 0; border-radius: 12px; overflow: hidden;
    background: linear-gradient(170deg, #F0F0F0 0%, #D8D8D8 50%, #C8C8C8 100%);
    box-shadow:
        inset 0 1px 0 rgba(255,255,255,0.5),
        0 4px 0 -1px #A0A0A0,
        0 8px 20px rgba(0,0,0,0.15);
}
.kd td {
    padding: 5px 12px; font-size: 7.5pt;
    border-bottom: 1px solid rgba(0,0,0,0.04);
}
.kd tr:last-child td { border-bottom: none; }
.kd .kd-day { font-weight: 800; width: 70px; text-transform: uppercase; }
.kd .kd-date { width: 60px; font-variant-numeric: tabular-nums; }
.kd .kd-desc { font-weight: 700; }

/* ── Division Lists ── */
.div-lists {
    display: flex; gap: 12px; margin: 14px 0; flex-wrap: wrap;
}
.div-card {
    flex: 1; min-width: 280px;
    border-radius: 10px; overflow: hidden;
    background: linear-gradient(180deg, #F0F0F0 0%, #E0E0E0 50%, #D0D0D0 100%);
    border: 2px solid #999;
    box-shadow:
        inset 0 1px 0 rgba(255,255,255,0.5),
        0 6px 0 -2px #A0A0A0,
        0 10px 0 -4px #B8B8B8,
        0 14px 28px rgba(0,0,0,0.2);
}
.div-hdr {
    color: white; font-weight: 900; font-size: 11pt; text-align: center;
    padding: 10px 12px; text-transform: uppercase; letter-spacing: 6px;
    text-shadow: 0 1px 0 rgba(255,255,255,0.25), 0 2px 4px rgba(0,0,0,0.5);
    box-shadow: inset 0 2px 0 rgba(255,255,255,0.5), inset 0 -2px 0 rgba(0,0,0,0.25);
    border-bottom: 2px solid rgba(0,0,0,0.3);
}
.div-tbl { width: 100%; border-collapse: collapse; }
.div-tbl td {
    padding: 6px 10px; font-size: 8pt;
    border-bottom: 1px solid rgba(0,0,0,0.08);
}
.div-tbl tr:last-child td { border-bottom: none; }
.div-tbl tr:nth-child(odd) { background: #FFFFFF; }
.div-tbl tr:nth-child(even) { background: #D8D8D8; }
.div-tbl tr { transition: background 0.15s; }
.div-tbl tr:hover { background: #C8D0E0; }
.div-tbl .div-num { width: 44px; text-align: center; vertical-align: middle; padding-left: 8px; }
.div-badge {
    font-weight: 900; color: white; font-size: 7.5pt;
    border-radius: 12px; width: 28px; height: 20px;
    line-height: 20px; display: inline-block; text-align: center;
    box-shadow: 0 2px 4px rgba(0,0,0,0.35), inset 0 2px 0 rgba(255,255,255,0.5), inset 0 -1px 0 rgba(0,0,0,0.15);
    text-shadow: 0 1px 0 rgba(255,255,255,0.2), 0 -1px 2px rgba(0,0,0,0.4);
}
.div-tbl .div-name {
    font-weight: 800; text-transform: uppercase; color: #1A1A1A;
    letter-spacing: 0.3px; font-size: 8.5pt;
}
.div-tbl .div-venue {
    color: #777; font-size: 7pt; font-style: italic;
    text-align: right; padding-right: 12px;
    text-transform: uppercase; letter-spacing: 0.3px;
}

/* ── Venue Info ── */
.venue-section {
    margin: 14px 0; border-radius: 12px; overflow: hidden;
    background: linear-gradient(170deg, #F0F0F0 0%, #D8D8D8 25%, #E8E8E8 50%, #C8C8C8 75%, #B8B8B8 100%);
    border: 1px solid rgba(255,255,255,0.4);
    box-shadow:
        inset 0 1px 0 rgba(255,255,255,0.5),
        inset 0 -1px 0 rgba(0,0,0,0.06),
        0 6px 0 -2px #A0A0A0,
        0 10px 0 -4px #B8B8B8,
        0 14px 28px rgba(0,0,0,0.2);
}
.venue-hdr {
    background: linear-gradient(180deg, #3A3A3A 0%, #2A2A2A 40%, #333 50%, #222 100%);
    font-weight: 800; font-size: 7.5pt;
    text-align: center; padding: 8px; letter-spacing: 4px;
    color: #A0A0A0; text-transform: uppercase;
    box-shadow: inset 0 1px 0 rgba(255,255,255,0.1), inset 0 -2px 0 rgba(0,0,0,0.25);
    text-shadow: 0 1px 0 rgba(255,255,255,0.05), 0 -1px 2px rgba(0,0,0,0.5);
}
.venue-grid {
    display: grid; grid-template-columns: 1fr 1fr;
    font-size: 7.5pt; padding: 8px 14px; gap: 4px 20px;
}
.venue-name {
    font-weight: 700; text-transform: uppercase;
    color: {{accent}}; letter-spacing: 0.3px;
}

/* ── Footer ── */
.sheet-footer {
    margin-top: 16px; text-align: center;
    font-size: 7pt; line-height: 1.8;
    padding-top: 10px; color: #94A3B8;
    border-top: 2px solid {{accent}}33;
}
.footer-notes {
    font-weight: 800; color: #EF4444; margin-bottom: 5px;
    text-transform: uppercase; letter-spacing: 1.5px; font-size: 7pt;
    background: linear-gradient(170deg, #F5E0E0 0%, #E8CCCC 50%, #D8B8B8 100%);
    padding: 5px 14px; border-radius: 6px;
    display: inline-block;
    box-shadow: inset 0 1px 0 rgba(255,255,255,0.5), 0 2px 6px rgba(239,68,68,0.15);
    text-shadow: 0 1px 0 rgba(255,255,255,0.6);
}
.footer-contacts { color: #64748B; letter-spacing: 0.3px; }

/* ── Print ── */
@media print {
    html, body {
        margin: 0; padding: 0; background: white;
        -webkit-print-color-adjust: exact !important;
        print-color-adjust: exact !important;
    }
    .fixtures-sheet { padding: 0; background: white; }
    .wk-card { break-inside: avoid; }
    .wk-card:hover { transform: none; }
}
@media screen {
    body { background: #0F172A; padding: 20px; }
    .fixtures-sheet {
        max-width: 1100px; margin: 0 auto;
        background: #F8FAFC; padding: 26px;
        border-radius: 18px;
        box-shadow:
            0 0 0 1px rgba(255,255,255,0.05),
            0 20px 60px rgba(0,0,0,0.5),
            0 0 120px rgba(0,0,0,0.2);
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

    private static string GetDivisionColor(string name)
    {
        var n = name.Trim().ToUpperInvariant();
        if (n.Contains("RED")) return "#DC2626";
        if (n.Contains("BLUE")) return "#2563EB";
        if (n.Contains("GREEN")) return "#16A34A";
        if (n.Contains("YELLOW")) return "#B8860B";
        if (n.Contains("ORANGE")) return "#EA580C";
        if (n.Contains("PURPLE") || n.Contains("VIOLET")) return "#7C3AED";
        if (n.Contains("BLACK")) return "#1C1C1C";
        if (n.Contains("WHITE")) return "#6B7280";
        if (n.Contains("GOLD")) return "#B8860B";
        if (n.Contains("SILVER")) return "#71717A";
        if (n.Contains("PINK")) return "#EC4899";
        if (n.Contains("CYAN") || n.Contains("TEAL")) return "#0891B2";
        if (n.Contains("MAROON")) return "#991B1B";
        if (n.Contains("NAVY")) return "#1E3A8A";
        // Fallback: generate a stable hue from the name
        var hash = 0;
        foreach (var c in n) hash = hash * 31 + c;
        var hue = Math.Abs(hash) % 360;
        return $"hsl({hue}, 60%, 35%)";
    }

    private static string GetDaySuffix(int day) => (day % 100) switch
    {
        11 or 12 or 13 => "th",
        _ => (day % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" }
    };

    private static string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);
}
