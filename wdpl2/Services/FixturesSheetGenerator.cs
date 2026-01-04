using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    /// <summary>
    /// Page orientation for fixtures sheet
    /// </summary>
    public enum PageOrientation
    {
        Portrait,
        Landscape
    }
    
    /// <summary>
    /// Settings for the fixtures sheet generator
    /// </summary>
    public sealed class FixturesSheetSettings
    {
        public string LeagueName { get; set; } = "Pool League";
        public string SeasonName { get; set; } = "";
        public string Subtitle { get; set; } = "";
        
        // Page orientation
        public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;
        
        // Colors for month headers
        public string OctoberColor { get; set; } = "#F5DEB3"; // Wheat
        public string NovemberColor { get; set; } = "#87CEEB"; // Sky blue
        public string DecemberColor { get; set; } = "#98FB98"; // Pale green
        public string JanuaryColor { get; set; } = "#DDA0DD"; // Plum
        public string FebruaryColor { get; set; } = "#FFB6C1"; // Light pink
        public string MarchColor { get; set; } = "#F0E68C"; // Khaki
        public string AprilColor { get; set; } = "#E6E6FA"; // Lavender
        public string MayColor { get; set; } = "#90EE90"; // Light green
        
        // Special events
        public List<SpecialEvent> SpecialEvents { get; set; } = new();
        
        // Venue telephone numbers
        public Dictionary<string, string> VenuePhoneNumbers { get; set; } = new();
        
        // Footer notes
        public List<string> FooterNotes { get; set; } = new();
        
        // Contact info
        public string WebsiteUrl { get; set; } = "";
        public string EmailAddress { get; set; } = "";
        public string CancelledMatchContact { get; set; } = "";
        public string CancelledCompetitionContact { get; set; } = "";
        
        // Display options
        public bool ShowTeamNumbers { get; set; } = true;
        public bool ShowVenueInfo { get; set; } = true;
        public bool ShowDivisionTeamLists { get; set; } = true;
        public bool ShowSpecialEvents { get; set; } = true;
        public bool ShowLeagueLogo { get; set; } = false;
        public byte[]? LogoImageData { get; set; }
    }
    
    /// <summary>
    /// Special event like knockout rounds
    /// </summary>
    public sealed class SpecialEvent
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; } = "";
        public string Description { get; set; } = "";
        public string Color { get; set; } = "#FFE4B5"; // Moccasin
    }
    
    /// <summary>
    /// Generates printable fixtures sheets in HTML format
    /// </summary>
    public sealed class FixturesSheetGenerator
    {
        private readonly LeagueData _league;
        private readonly FixturesSheetSettings _settings;
        
        public FixturesSheetGenerator(LeagueData league, FixturesSheetSettings settings)
        {
            _league = league;
            _settings = settings;
        }
        
        /// <summary>
        /// Generate the fixtures sheet HTML for a specific season
        /// </summary>
        public string GenerateFixturesSheet(Guid seasonId, List<Guid>? divisionIds = null)
        {
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(seasonId);
            var season = _league.Seasons.FirstOrDefault(s => s.Id == seasonId);
            
            if (season == null)
                throw new InvalidOperationException("Season not found");
            
            // Filter divisions if specified
            if (divisionIds != null && divisionIds.Count > 0)
            {
                divisions = divisions.Where(d => divisionIds.Contains(d.Id)).ToList();
                teams = teams.Where(t => t.DivisionId.HasValue && divisionIds.Contains(t.DivisionId.Value)).ToList();
                fixtures = fixtures.Where(f => f.DivisionId.HasValue && divisionIds.Contains(f.DivisionId.Value)).ToList();
            }
            
            var html = new StringBuilder();
            
            // Start HTML document
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine($"    <title>{_settings.LeagueName} - Fixtures {season.Name}</title>");
            html.AppendLine("    <style>");
            html.AppendLine(GenerateCSS());
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            // Main content
            html.AppendLine(GenerateSheetContent(divisions, venues, teams, fixtures, season));
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        /// <summary>
        /// Generate just the content (for embedding in website)
        /// </summary>
        public string GenerateEmbeddableContent(Guid seasonId, List<Guid>? divisionIds = null)
        {
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(seasonId);
            var season = _league.Seasons.FirstOrDefault(s => s.Id == seasonId);
            
            if (season == null)
                return "<p>Season not found</p>";
            
            // Filter divisions if specified
            if (divisionIds != null && divisionIds.Count > 0)
            {
                divisions = divisions.Where(d => divisionIds.Contains(d.Id)).ToList();
                teams = teams.Where(t => t.DivisionId.HasValue && divisionIds.Contains(t.DivisionId.Value)).ToList();
                fixtures = fixtures.Where(f => f.DivisionId.HasValue && divisionIds.Contains(f.DivisionId.Value)).ToList();
            }
            
            return GenerateSheetContent(divisions, venues, teams, fixtures, season);
        }
        
        /// <summary>
        /// Get the CSS styles for embedding
        /// </summary>
        public string GetEmbeddableCSS()
        {
            return GenerateCSS();
        }
        
        private string GenerateSheetContent(List<Division> divisions, List<Venue> venues, List<Team> teams, List<Fixture> fixtures, Season season)
        {
            var html = new StringBuilder();
            
            // Header
            html.AppendLine("<div class=\"fixtures-sheet\">");
            html.AppendLine($"    <h1 class=\"sheet-title\">{_settings.LeagueName} {_settings.SeasonName}</h1>");
            html.AppendLine("    <div class=\"sheet-header\">");
            if (_settings.ShowLeagueLogo && _settings.LogoImageData != null)
            {
                var logoBase64 = Convert.ToBase64String(_settings.LogoImageData);
                html.AppendLine($"        <img src=\"data:image/png;base64,{logoBase64}\" class=\"league-logo\" alt=\"{_settings.LeagueName} Logo\">");
            }
            if (!string.IsNullOrWhiteSpace(_settings.Subtitle))
            {
                html.AppendLine($"        <h2 class=\"sheet-subtitle\">{_settings.Subtitle}</h2>");
            }
            else
            {
                var divNames = string.Join(" & ", divisions.Select(d => d.Name).Where(n => !string.IsNullOrEmpty(n)));
                if (!string.IsNullOrWhiteSpace(divNames))
                    html.AppendLine($"        <h2 class=\"sheet-subtitle\">{divNames.ToUpper()} FIXTURES</h2>");
            }
            html.AppendLine("    </div>");
            
            // Generate fixtures grid
            GenerateFixturesGrid(html, fixtures, teams, divisions, season);
            
            // Special events section
            if (_settings.ShowSpecialEvents && _settings.SpecialEvents.Count > 0)
            {
                GenerateSpecialEventsSection(html);
            }
            
            // Division team lists
            if (_settings.ShowDivisionTeamLists)
            {
                GenerateDivisionTeamLists(html, divisions, teams, venues);
            }
            
            // Venue telephone numbers
            if (_settings.ShowVenueInfo && _settings.VenuePhoneNumbers.Count > 0)
            {
                GenerateVenueInfo(html);
            }
            
            // Footer notes
            GenerateFooter(html);
            
            html.AppendLine("</div>");
            
            return html.ToString();
        }
        
        private void GenerateFixturesGrid(StringBuilder html, List<Fixture> fixtures, List<Team> teams, List<Division> divisions, Season season)
        {
            // Group fixtures by week (use the actual match date, not week start for display)
            var fixturesByWeek = fixtures
                .OrderBy(f => f.Date)
                .GroupBy(f => f.Date.Date) // Group by actual date
                .ToList();
            
            if (fixturesByWeek.Count == 0)
            {
                html.AppendLine("<p class=\"no-fixtures\">No fixtures scheduled</p>");
                return;
            }
            
            // Build team number lookup (1-based position in division)
            var teamNumbersByDivision = new Dictionary<Guid, Dictionary<Guid, int>>();
            foreach (var division in divisions)
            {
                var divTeams = teams.Where(t => t.DivisionId == division.Id).OrderBy(t => t.Name).ToList();
                var divTeamNumbers = new Dictionary<Guid, int>();
                for (int i = 0; i < divTeams.Count; i++)
                {
                    divTeamNumbers[divTeams[i].Id] = i + 1;
                }
                teamNumbersByDivision[division.Id] = divTeamNumbers;
            }
            
            // Global team numbers for mixed division sheets
            var globalTeamNumbers = new Dictionary<Guid, int>();
            int globalNum = 1;
            foreach (var division in divisions.OrderBy(d => d.Name))
            {
                var divTeams = teams.Where(t => t.DivisionId == division.Id).OrderBy(t => t.Name).ToList();
                foreach (var team in divTeams)
                {
                    globalTeamNumbers[team.Id] = globalNum++;
                }
            }
            
            // Calculate optimal number of rows based on total weeks
            // Aim for roughly 8-12 columns per row for readability
            var totalWeeks = fixturesByWeek.Count;
            int numRows;
            if (totalWeeks <= 12)
                numRows = 1;
            else if (totalWeeks <= 24)
                numRows = 2;
            else if (totalWeeks <= 36)
                numRows = 3;
            else
                numRows = 4;
            
            var weeksPerRow = (int)Math.Ceiling((double)totalWeeks / numRows);
            
            // Generate fixture tables for each row
            for (int rowIndex = 0; rowIndex < numRows; rowIndex++)
            {
                var startIndex = rowIndex * weeksPerRow;
                var rowWeeks = fixturesByWeek.Skip(startIndex).Take(weeksPerRow).ToList();
                
                if (rowWeeks.Count > 0)
                {
                    GenerateFixturesTable(html, rowWeeks, teams, divisions, teamNumbersByDivision, globalTeamNumbers);
                }
            }
        }
        
        private void GenerateFixturesTable(
            StringBuilder html, 
            List<IGrouping<DateTime, Fixture>> fixturesByWeek, 
            List<Team> teams, 
            List<Division> divisions,
            Dictionary<Guid, Dictionary<Guid, int>> teamNumbersByDivision,
            Dictionary<Guid, int> globalTeamNumbers)
        {
            if (fixturesByWeek.Count == 0) return;
            
            // Group weeks by month for header coloring
            var weeksByMonth = fixturesByWeek
                .GroupBy(g => new { g.Key.Year, g.Key.Month })
                .ToList();
            
            var maxFixturesPerWeek = fixturesByWeek.Max(g => g.Count());
            
            html.AppendLine("<table class=\"fixtures-grid\">");
            
            // Month headers row
            html.AppendLine("<tr class=\"month-row\">");
            foreach (var monthGroup in weeksByMonth)
            {
                var monthName = new DateTime(monthGroup.Key.Year, monthGroup.Key.Month, 1).ToString("MMMM").ToUpper();
                var weekCount = monthGroup.Count();
                var color = GetMonthColor(monthGroup.Key.Month);
                html.AppendLine($"    <th colspan=\"{weekCount}\" style=\"background-color: {color};\">{monthName}</th>");
            }
            html.AppendLine("</tr>");
            
            // Week date headers row
            html.AppendLine("<tr class=\"week-row\">");
            foreach (var weekGroup in fixturesByWeek)
            {
                var weekDate = weekGroup.Key;
                var monthColor = GetMonthColor(weekDate.Month);
                var dayNum = $"{weekDate.Day}{GetDaySuffix(weekDate.Day)}";
                html.AppendLine($"    <th style=\"background-color: {monthColor};\">{dayNum}</th>");
            }
            html.AppendLine("</tr>");
            
            // Fixture rows
            for (int row = 0; row < maxFixturesPerWeek; row++)
            {
                html.AppendLine("<tr class=\"fixture-row\">");
                foreach (var weekGroup in fixturesByWeek)
                {
                    var weekFixtures = weekGroup.ToList();
                    if (row < weekFixtures.Count)
                    {
                        var fixture = weekFixtures[row];
                        var homeTeam = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
                        var awayTeam = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId);
                        
                        // Get team numbers
                        int homeNum = 0, awayNum = 0;
                        if (divisions.Count == 1 && fixture.DivisionId.HasValue && 
                            teamNumbersByDivision.TryGetValue(fixture.DivisionId.Value, out var divNumbers))
                        {
                            homeNum = divNumbers.GetValueOrDefault(fixture.HomeTeamId, 0);
                            awayNum = divNumbers.GetValueOrDefault(fixture.AwayTeamId, 0);
                        }
                        else
                        {
                            homeNum = globalTeamNumbers.GetValueOrDefault(fixture.HomeTeamId, 0);
                            awayNum = globalTeamNumbers.GetValueOrDefault(fixture.AwayTeamId, 0);
                        }
                        
                        var tooltip = $"{homeTeam?.Name ?? "TBD"} vs {awayTeam?.Name ?? "TBD"}";
                        
                        if (_settings.ShowTeamNumbers && homeNum > 0 && awayNum > 0)
                        {
                            // Use numbers - compact format
                            html.AppendLine($"    <td title=\"{tooltip}\">{homeNum} v {awayNum}</td>");
                        }
                        else
                        {
                            // Show abbreviated team names - use very short abbreviations
                            var homeName = GetShortTeamName(homeTeam?.Name, 4);
                            var awayName = GetShortTeamName(awayTeam?.Name, 4);
                            html.AppendLine($"    <td title=\"{tooltip}\">{homeName} v {awayName}</td>");
                        }
                    }
                    else
                    {
                        html.AppendLine("    <td></td>");
                    }
                }
                html.AppendLine("</tr>");
            }
            
            html.AppendLine("</table>");
        }
        
        private string GetShortTeamName(string? name, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            
            // Common abbreviations
            name = name.Replace("THE ", "").Replace("The ", "");
            
            if (name.Length <= maxLength) return name;
            
            // Try first word
            var firstSpace = name.IndexOf(' ');
            if (firstSpace > 0 && firstSpace <= maxLength)
            {
                return name.Substring(0, firstSpace);
            }
            
            // Truncate
            return name.Substring(0, maxLength);
        }
        
        private string GetShortName(string? name)
        {
            return GetShortTeamName(name, 8);
        }
        
        private void GenerateSpecialEventsSection(StringBuilder html)
        {
            html.AppendLine("<div class=\"special-events-section\">");
            html.AppendLine("<table class=\"special-events\">");
            foreach (var evt in _settings.SpecialEvents.OrderBy(e => e.Date))
            {
                html.AppendLine("<tr>");
                html.AppendLine($"    <td class=\"event-day\" style=\"background-color: {evt.Color};\">{evt.DayOfWeek}</td>");
                html.AppendLine($"    <td class=\"event-date\" style=\"background-color: {evt.Color};\">{evt.Date:d MMM}</td>");
                html.AppendLine($"    <td class=\"event-desc\" style=\"background-color: {evt.Color};\">{evt.Description}</td>");
                html.AppendLine("</tr>");
            }
            html.AppendLine("</table>");
            html.AppendLine("</div>");
        }
        
        private void GenerateDivisionTeamLists(StringBuilder html, List<Division> divisions, List<Team> teams, List<Venue> venues)
        {
            html.AppendLine("<div class=\"division-lists\">");
            
            foreach (var division in divisions.OrderBy(d => d.Name))
            {
                var divTeams = teams.Where(t => t.DivisionId == division.Id).OrderBy(t => t.Name).ToList();
                if (divTeams.Count == 0) continue;
                
                html.AppendLine($"<div class=\"division-box\">");
                html.AppendLine($"    <h3>{division.Name}</h3>");
                html.AppendLine("    <table class=\"team-list\">");
                
                int num = 1;
                foreach (var team in divTeams)
                {
                    var venue = team.VenueId.HasValue ? venues.FirstOrDefault(v => v.Id == team.VenueId.Value) : null;
                    var venueName = venue?.Name ?? "";
                    
                    // Try to get table info if available
                    var tableInfo = "";
                    if (venue != null && team.TableId.HasValue)
                    {
                        var table = venue.Tables.FirstOrDefault(t => t.Id == team.TableId.Value);
                        if (table != null)
                        {
                            tableInfo = $" ({table.Label})";
                        }
                    }
                    
                    html.AppendLine("        <tr>");
                    html.AppendLine($"            <td class=\"team-num\">{num}</td>");
                    html.AppendLine($"            <td class=\"team-name\">{team.Name}</td>");
                    html.AppendLine($"            <td class=\"team-venue\">{venueName}{tableInfo}</td>");
                    html.AppendLine("        </tr>");
                    num++;
                }
                
                // Add BYE rows if odd number of teams
                if (divTeams.Count % 2 == 1)
                {
                    html.AppendLine("        <tr>");
                    html.AppendLine($"            <td class=\"team-num\">{num}</td>");
                    html.AppendLine("            <td class=\"team-name\">BYE</td>");
                    html.AppendLine("            <td class=\"team-venue\"></td>");
                    html.AppendLine("        </tr>");
                }
                
                html.AppendLine("    </table>");
                html.AppendLine("</div>");
            }
            
            html.AppendLine("</div>");
        }
        
        private void GenerateVenueInfo(StringBuilder html)
        {
            html.AppendLine("<div class=\"venue-phones\">");
            html.AppendLine("    <h4>VENUE TELEPHONE NUMBERS</h4>");
            html.AppendLine("    <table>");
            
            var phoneList = _settings.VenuePhoneNumbers.ToList();
            var half = (phoneList.Count + 1) / 2;
            
            for (int i = 0; i < half; i++)
            {
                html.AppendLine("    <tr>");
                html.AppendLine($"        <td class=\"venue-name\">{phoneList[i].Key}:</td>");
                html.AppendLine($"        <td class=\"venue-phone\">{phoneList[i].Value}</td>");
                
                if (i + half < phoneList.Count)
                {
                    html.AppendLine($"        <td class=\"venue-name\">{phoneList[i + half].Key}:</td>");
                    html.AppendLine($"        <td class=\"venue-phone\">{phoneList[i + half].Value}</td>");
                }
                else
                {
                    html.AppendLine("        <td></td><td></td>");
                }
                
                html.AppendLine("    </tr>");
            }
            
            html.AppendLine("    </table>");
            html.AppendLine("</div>");
        }
        
        private void GenerateFooter(StringBuilder html)
        {
            if (_settings.FooterNotes.Count == 0 && 
                string.IsNullOrWhiteSpace(_settings.CancelledMatchContact) &&
                string.IsNullOrWhiteSpace(_settings.CancelledCompetitionContact) &&
                string.IsNullOrWhiteSpace(_settings.WebsiteUrl) &&
                string.IsNullOrWhiteSpace(_settings.EmailAddress))
            {
                return;
            }
            
            html.AppendLine("<div class=\"sheet-footer\">");
            
            foreach (var note in _settings.FooterNotes)
            {
                html.AppendLine($"    <p class=\"footer-note\">{note}</p>");
            }
            
            if (!string.IsNullOrWhiteSpace(_settings.CancelledMatchContact))
            {
                html.AppendLine($"    <p>Report Cancelled Matches to {_settings.CancelledMatchContact}</p>");
            }
            
            if (!string.IsNullOrWhiteSpace(_settings.CancelledCompetitionContact))
            {
                html.AppendLine($"    <p>Report Cancelled Competition Fixtures to {_settings.CancelledCompetitionContact}</p>");
            }
            
            var contactLine = new List<string>();
            if (!string.IsNullOrWhiteSpace(_settings.WebsiteUrl))
                contactLine.Add($"Web site: <a href=\"{_settings.WebsiteUrl}\">{_settings.WebsiteUrl}</a>");
            if (!string.IsNullOrWhiteSpace(_settings.EmailAddress))
                contactLine.Add($"Email: <a href=\"mailto:{_settings.EmailAddress}\">{_settings.EmailAddress}</a>");
            
            if (contactLine.Count > 0)
            {
                html.AppendLine($"    <p class=\"contact-line\">{string.Join(" &nbsp;&nbsp; ", contactLine)}</p>");
            }
            
            html.AppendLine("</div>");
        }
        
        private string GenerateCSS()
        {
            var isLandscape = _settings.Orientation == PageOrientation.Landscape;
            var pageOrientation = isLandscape ? "landscape" : "portrait";
            
            // A4 dimensions: 210mm x 297mm - Use full width with minimal margins
            var sheetWidth = isLandscape ? "287mm" : "200mm";
            var sheetMinHeight = isLandscape ? "200mm" : "287mm";
            var printWidth = isLandscape ? "297mm" : "210mm";
            var printHeight = isLandscape ? "210mm" : "297mm";
            
            // Division box sizes - fill available space
            var divBoxMinWidth = isLandscape ? "90mm" : "95mm";
            var divBoxMaxWidth = isLandscape ? "95mm" : "100mm";
            
            // Font sizes - adjust based on orientation
            var titleSize = isLandscape ? "18pt" : "16pt";
            var subtitleSize = isLandscape ? "12pt" : "11pt";
            var gridMonthSize = isLandscape ? "8pt" : "7pt";
            var gridWeekSize = isLandscape ? "7pt" : "6pt";
            var gridFixtureSize = isLandscape ? "8pt" : "7pt";
            var teamSize = isLandscape ? "8pt" : "7.5pt";
            var teamNameSize = isLandscape ? "7.5pt" : "7pt";
            var teamVenueSize = isLandscape ? "6.5pt" : "6pt";
            
            // Print font sizes
            var printTitleSize = isLandscape ? "16pt" : "14pt";
            var printSubtitleSize = isLandscape ? "11pt" : "10pt";
            var printGridMonthSize = isLandscape ? "7pt" : "6pt";
            var printGridWeekSize = isLandscape ? "6pt" : "5.5pt";
            var printGridFixtureSize = isLandscape ? "7pt" : "6pt";
            var printTeamSize = isLandscape ? "7pt" : "6.5pt";
            var printTeamVenueSize = isLandscape ? "6pt" : "5.5pt";

            var css = new StringBuilder();
            
            css.AppendLine($@"
/* A4 Page Setup - {pageOrientation.ToUpper()} */
@page {{ size: A4 {pageOrientation}; margin: 5mm; }}

* {{ box-sizing: border-box; }}

html, body {{
    margin: 0; padding: 0;
    font-family: Arial, Helvetica, sans-serif;
    font-size: 9pt; background: white;
}}

.fixtures-sheet {{
    width: {sheetWidth}; max-width: {sheetWidth}; min-height: {sheetMinHeight};
    margin: 0 auto; padding: 2mm; background: white;
    display: flex; flex-direction: column;
}}

.fixtures-sheet .sheet-header {{ margin-bottom: 1mm; }}

.fixtures-sheet .sheet-title {{
    text-align: center; font-size: {titleSize}; font-weight: bold;
    margin: 0 0 1mm 0; border: 2px solid #000; padding: 4px 8px;
    background: linear-gradient(to bottom, #f5f5f5, #ffffff);
}}

.fixtures-sheet .sheet-subtitle {{
    text-align: center; font-size: {subtitleSize}; font-weight: bold;
    margin: 0 0 2mm 0; background: #333; color: white; padding: 4px 8px;
}}

.league-logo {{ max-height: 40px; max-width: 100px; display: block; margin: 0 auto 1mm auto; }}

/* Fixtures Grid - each table is a portion of the season */
.fixtures-grid {{
    width: 100%;
    border-collapse: collapse;
    margin-bottom: 1.5mm;
    table-layout: fixed;
}}

.fixtures-grid th,
.fixtures-grid td {{
    border: 1px solid #000;
    text-align: center;
    vertical-align: middle;
    overflow: hidden;
    text-overflow: ellipsis;
}}

/* Month header row */
.fixtures-grid .month-row th {{
    font-weight: bold;
    font-size: {gridMonthSize};
    text-transform: uppercase;
    padding: 2px 2px;
    line-height: 1.1;
    white-space: nowrap;
}}

/* Week/date header row */
.fixtures-grid .week-row th {{
    font-weight: bold;
    font-size: {gridWeekSize};
    padding: 2px 2px;
    line-height: 1.1;
    vertical-align: middle;
    white-space: nowrap;
}}

/* Fixture cells - prevent overflow */
.fixtures-grid .fixture-row td {{
    font-size: {gridFixtureSize};
    font-weight: normal;
    padding: 2px 1px;
    line-height: 1.1;
    vertical-align: middle;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}}

.special-events-section {{ margin: 2mm 0; }}
.special-events {{ width: auto; border-collapse: collapse; }}
.special-events td {{ border: 1px solid #000; padding: 2px 6px; font-size: 7pt; }}
.special-events .event-day {{ font-weight: bold; min-width: 50px; }}
.special-events .event-date {{ min-width: 45px; }}

/* Division boxes - flexible layout */
.division-lists {{
    display: flex;
    flex-wrap: wrap;
    gap: 2mm;
    margin: 2mm 0;
    flex: 1;
    align-content: flex-start;
}}

.division-box {{
    flex: 1 1 {divBoxMinWidth};
    max-width: {divBoxMaxWidth};
    border: 1.5px solid #000;
}}

.division-box h3 {{
    background: linear-gradient(to bottom, #444, #333);
    color: white;
    text-align: center;
    padding: 3px 5px;
    font-size: 8pt;
    margin: 0;
}}

.team-list {{ width: 100%; border-collapse: collapse; }}

.team-list td {{
    border: 1px solid #ddd;
    padding: 2px 3px;
    font-size: {teamSize};
    line-height: 1.2;
}}

.team-list .team-num {{
    width: 16px;
    text-align: center;
    font-weight: bold;
    background: #f5f5f5;
}}

.team-list .team-name {{
    font-weight: bold;
    font-size: {teamNameSize};
}}

.team-list .team-venue {{
    color: #333;
    font-size: {teamVenueSize};
    text-align: right;
}}

.venue-phones {{
    margin: 1mm 0;
    border: 1.5px solid #000;
    padding: 1.5mm 2mm;
    background: #fafafa;
}}

.venue-phones h4 {{
    text-align: center;
    margin: 0 0 1mm 0;
    font-size: 8pt;
}}

.venue-phones table {{ width: 100%; }}
.venue-phones td {{ padding: 1px 4px; font-size: 7pt; }}
.venue-phones .venue-name {{ font-weight: bold; }}
.venue-phones .venue-phone {{ font-family: monospace; font-size: 6.5pt; }}

.sheet-footer {{
    margin-top: auto;
    padding: 1.5mm 2mm;
    background: #f0f0f0;
    border: 1px solid #999;
    font-size: 6pt;
}}

.sheet-footer p {{ margin: 1px 0; text-align: center; }}
.sheet-footer .footer-note {{ font-weight: bold; color: #c00; }}
.sheet-footer .contact-line {{ margin-top: 1mm; font-weight: bold; }}
.sheet-footer a {{ color: #0066cc; text-decoration: none; }}

.no-fixtures {{ text-align: center; padding: 10mm; color: #666; font-style: italic; }}

/* Print Styles */
@media print {{
    html, body {{
        width: {printWidth};
        height: {printHeight};
        margin: 0;
        padding: 0;
        -webkit-print-color-adjust: exact !important;
        print-color-adjust: exact !important;
        color-adjust: exact !important;
    }}
    
    .fixtures-sheet {{
        width: 100%;
        max-width: none;
        min-height: 100%;
        padding: 0;
        page-break-after: avoid;
        page-break-inside: avoid;
    }}
    
    .fixtures-sheet .sheet-title {{
        font-size: {printTitleSize};
        padding: 3px 6px;
    }}
    
    .fixtures-sheet .sheet-subtitle {{
        font-size: {printSubtitleSize};
        padding: 3px 6px;
    }}
    
    .fixtures-grid {{
        margin-bottom: 1mm;
    }}
    
    .fixtures-grid .month-row th {{
        font-size: {printGridMonthSize};
        padding: 1px 1px;
    }}
    
    .fixtures-grid .week-row th {{
        font-size: {printGridWeekSize};
        padding: 1px 1px;
    }}
    
    .fixtures-grid .fixture-row td {{
        font-size: {printGridFixtureSize};
        padding: 1px 1px;
    }}
    
    .division-box {{
        page-break-inside: avoid;
    }}
    
    .division-box h3 {{
        font-size: 7pt;
        padding: 2px 4px;
    }}
    
    .team-list td {{
        font-size: {printTeamSize};
        padding: 1px 2px;
    }}
    
    .team-list .team-name {{
        font-size: {printTeamSize};
    }}
    
    .team-list .team-venue {{
        font-size: {printTeamVenueSize};
    }}
    
    .special-events td {{
        font-size: 6pt;
        padding: 1px 4px;
    }}
    
    .venue-phones {{
        padding: 1mm;
    }}
    
    .venue-phones h4 {{
        font-size: 7pt;
        margin-bottom: 1mm;
    }}
    
    .venue-phones td {{
        font-size: 6pt;
        padding: 1px 3px;
    }}
    
    .sheet-footer {{
        font-size: 5.5pt;
        padding: 1mm;
    }}
    
    .sheet-footer a {{
        color: #000;
        text-decoration: none;
    }}
}}

/* Screen Preview */
@media screen {{
    body {{
        background: #d0d0d0;
        padding: 15px;
    }}
    
    .fixtures-sheet {{
        background: white;
        box-shadow: 0 3px 15px rgba(0,0,0,0.25);
        margin: 15px auto;
    }}
}}

/* Responsive */
@media screen and (max-width: 768px) {{
    .fixtures-sheet {{
        width: 100%;
        min-width: auto;
        padding: 5px;
    }}
    
    .fixtures-grid th,
    .fixtures-grid td {{
        font-size: 6pt;
        padding: 1px;
    }}
    
    .division-lists {{
        flex-direction: column;
    }}
    
    .division-box {{
        max-width: none;
        min-width: auto;
    }}
}}
");
            
            return css.ToString();
        }
        
        private string GetMonthColor(int month)
        {
            return month switch
            {
                1 => _settings.JanuaryColor,
                2 => _settings.FebruaryColor,
                3 => _settings.MarchColor,
                4 => _settings.AprilColor,
                5 => _settings.MayColor,
                6 => "#FFE4E1",
                7 => "#ADD8E6",
                8 => "#FAFAD2",
                9 => "#D8BFD8",
                10 => _settings.OctoberColor,
                11 => _settings.NovemberColor,
                12 => _settings.DecemberColor,
                _ => "#FFFFFF"
            };
        }
        
        private string GetDaySuffix(int day)
        {
            if (day >= 11 && day <= 13) return "th";
            return (day % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        }
    }
}
