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
    /// Logo position options for fixtures sheet
    /// </summary>
    public enum LogoPosition
    {
        AboveTitle,
        BelowTitle,
        LeftOfTitle,
        RightOfTitle,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public enum TitleStyle
    {
        DoubleRule,
        SingleRule,
        BoxBorder,
        None
    }

    public enum GridBorderWeight
    {
        Fine,
        Medium,
        Bold
    }

    public enum HomeBadgeStyle
    {
        Pill,
        BoldOnly
    }

    public enum SheetFontFamily
    {
        Modern,
        Classic,
        Mono
    }

    public enum MonthPalette
    {
        Muted,
        Vibrant,
        Monochrome,
        Earth,
        Ocean
    }

    public enum ColumnBanding
    {
        None,
        Subtle,
        Strong
    }

    public enum SubtitleStyle
    {
        FilledBar,
        Outline,
        TextOnly
    }

    public enum DivisionLayout
    {
        Auto,
        Stacked,
        Compact
    }

    public enum TextDensity
    {
        Compact,
        Normal,
        Spacious
    }

    public enum CardStyle
    {
        Solid,
        Frosted,
        Translucent
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

        // Colors for month headers — muted, print-friendly tones
        public string OctoberColor { get; set; } = "#E8D5B7";   // Warm sand
        public string NovemberColor { get; set; } = "#B8D4E3";  // Dusty blue
        public string DecemberColor { get; set; } = "#C8DEC8";  // Sage
        public string JanuaryColor { get; set; } = "#CBC3E3";   // Soft lavender
        public string FebruaryColor { get; set; } = "#E8C8D4";  // Dusty rose
        public string MarchColor { get; set; } = "#E3DDB8";     // Warm wheat
        public string AprilColor { get; set; } = "#D4C8E8";     // Light mauve
        public string MayColor { get; set; } = "#C3DEB8";       // Soft green

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

        // Logo options — LogoImageData is resolved by the caller (from upload or shared catalog)
        public bool ShowLeagueLogo { get; set; } = false;
        public byte[]? LogoImageData { get; set; }
        public LogoPosition LogoPosition { get; set; } = LogoPosition.AboveTitle;
        public int LogoWidth { get; set; } = 100;
        public int LogoHeight { get; set; } = 60;
        public bool LogoMaintainAspectRatio { get; set; } = true;

        // ── Design & Styling ──

        /// <summary>Accent colour used for subtitle bar, home badges, and division headers</summary>
        public string AccentColor { get; set; } = "#1a1a1a";

        /// <summary>Title frame style: DoubleRule, SingleRule, BoxBorder, None</summary>
        public TitleStyle TitleStyle { get; set; } = TitleStyle.DoubleRule;

        /// <summary>Grid border weight: Fine, Medium, Bold</summary>
        public GridBorderWeight GridBorders { get; set; } = GridBorderWeight.Medium;

        /// <summary>Home team indicator style: Pill, BoldOnly</summary>
        public HomeBadgeStyle HomeBadge { get; set; } = HomeBadgeStyle.Pill;

        /// <summary>Font family: Modern (Segoe UI/Calibri), Classic (Georgia/Times), Mono (Consolas/Courier)</summary>
        public SheetFontFamily FontFamily { get; set; } = SheetFontFamily.Modern;

        /// <summary>Show the match-night auto-detected banner</summary>
        public bool ShowMatchNight { get; set; } = true;

        /// <summary>Month header colour palette</summary>
        public MonthPalette MonthColors { get; set; } = MonthPalette.Muted;

        /// <summary>Column colour banding intensity: None, Subtle, Strong</summary>
        public ColumnBanding ColumnBanding { get; set; } = ColumnBanding.Subtle;

        /// <summary>Subtitle bar style: FilledBar, Outline, TextOnly</summary>
        public SubtitleStyle SubtitleStyle { get; set; } = SubtitleStyle.FilledBar;

        /// <summary>Division list layout: Auto (side-by-side), Stacked, Compact (3-col)</summary>
        public DivisionLayout DivisionLayout { get; set; } = DivisionLayout.Auto;

        /// <summary>Text density: Compact, Normal, Spacious — scales all font sizes</summary>
        public TextDensity TextDensity { get; set; } = TextDensity.Normal;

        /// <summary>Uppercase the title text</summary>
        public bool TitleUppercase { get; set; } = true;

        /// <summary>Uppercase the month headers</summary>
        public bool MonthUppercase { get; set; } = true;

        /// <summary>Show grid legend (Home = bold, Away = regular)</summary>
        public bool ShowGridLegend { get; set; } = true;

        /// <summary>Card surface style: Solid (opaque white), Frosted (glass blur), Translucent (semi-transparent)</summary>
        public CardStyle CardStyle { get; set; } = CardStyle.Solid;

        /// <summary>
        /// Get the effective logo data
        /// </summary>
        public byte[]? GetEffectiveLogoData()
        {
            if (LogoImageData != null && LogoImageData.Length > 0)
                return LogoImageData;
            return null;
        }
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
            var effectiveLogoData = _settings.GetEffectiveLogoData();
            var hasLogo = _settings.ShowLeagueLogo && effectiveLogoData != null;
            
            // Header
            html.AppendLine("<div class=\"fixtures-sheet\">");
            
            // Generate header based on logo position
            GenerateHeader(html, divisions, hasLogo, effectiveLogoData);

            // Detect common match night
            if (_settings.ShowMatchNight)
            {
                var matchDays = fixtures.Select(f => f.Date.DayOfWeek).Distinct().OrderBy(d => d).ToList();
                if (matchDays.Count is >= 1 and <= 2)
                {
                    var dayText = string.Join(" &amp; ", matchDays.Select(d => d.ToString().ToUpper()));
                    var label = matchDays.Count == 1 ? "MATCH NIGHT" : "MATCH NIGHTS";
                    html.AppendLine($"<div class=\"match-day-strip\">{label} &mdash; {dayText}</div>");
                }
            }

            // Generate fixtures grid
            GenerateFixturesGrid(html, fixtures, teams, divisions, season);

            // Special events section
            if (_settings.ShowSpecialEvents && _settings.SpecialEvents.Count > 0)
            {
                html.AppendLine("<hr class=\"section-divider\">");
                GenerateSpecialEventsSection(html);
            }

            // Division team lists
            if (_settings.ShowDivisionTeamLists)
            {
                html.AppendLine("<hr class=\"section-divider\">");
                GenerateDivisionTeamLists(html, divisions, teams, venues);
            }

            // Venue telephone numbers
            if (_settings.ShowVenueInfo && _settings.VenuePhoneNumbers.Count > 0)
            {
                html.AppendLine("<hr class=\"section-divider\">");
                GenerateVenueInfo(html);
            }

            // Footer notes
            GenerateFooter(html);
            
            // Corner positioned logos
            if (hasLogo && (_settings.LogoPosition == LogoPosition.BottomLeft || _settings.LogoPosition == LogoPosition.BottomRight))
            {
                GenerateCornerLogo(html, effectiveLogoData!, _settings.LogoPosition);
            }
            
            html.AppendLine("</div>");
            
            return html.ToString();
        }
        
        private void GenerateHeader(StringBuilder html, List<Division> divisions, bool hasLogo, byte[]? logoData)
        {
            var logoBase64 = hasLogo && logoData != null ? Convert.ToBase64String(logoData) : "";
            var logoHtml = hasLogo ? GenerateLogoImgTag(logoBase64) : "";
            
            // Determine layout class based on logo position
            var headerClass = "sheet-header";
            if (hasLogo)
            {
                headerClass += _settings.LogoPosition switch
                {
                    LogoPosition.LeftOfTitle => " header-logo-left",
                    LogoPosition.RightOfTitle => " header-logo-right",
                    LogoPosition.TopLeft or LogoPosition.TopRight => " header-logo-corner",
                    _ => ""
                };
            }
            
            // Top corner logos
            if (hasLogo && (_settings.LogoPosition == LogoPosition.TopLeft || _settings.LogoPosition == LogoPosition.TopRight))
            {
                var cornerClass = _settings.LogoPosition == LogoPosition.TopLeft ? "corner-logo-top-left" : "corner-logo-top-right";
                html.AppendLine($"    <div class=\"{cornerClass}\">{logoHtml}</div>");
            }
            
            html.AppendLine($"    <div class=\"{headerClass}\">");
            
            // Logo above title
            if (hasLogo && _settings.LogoPosition == LogoPosition.AboveTitle)
            {
                html.AppendLine($"        <div class=\"logo-container logo-above\">{logoHtml}</div>");
            }
            
            // Logo left of title
            if (hasLogo && _settings.LogoPosition == LogoPosition.LeftOfTitle)
            {
                html.AppendLine($"        <div class=\"logo-container logo-left\">{logoHtml}</div>");
            }
            
            // Title section
            html.AppendLine("        <div class=\"title-section\">");
            html.AppendLine($"            <h1 class=\"sheet-title\">{_settings.LeagueName} {_settings.SeasonName}</h1>");
            
            if (!string.IsNullOrWhiteSpace(_settings.Subtitle))
            {
                html.AppendLine($"            <h2 class=\"sheet-subtitle\">{_settings.Subtitle}</h2>");
            }
            else
            {
                var divNames = string.Join(" & ", divisions.Select(d => d.Name).Where(n => !string.IsNullOrEmpty(n)));
                if (!string.IsNullOrWhiteSpace(divNames))
                    html.AppendLine($"            <h2 class=\"sheet-subtitle\">{divNames.ToUpper()} FIXTURES</h2>");
            }
            html.AppendLine("        </div>");
            
            // Logo right of title
            if (hasLogo && _settings.LogoPosition == LogoPosition.RightOfTitle)
            {
                html.AppendLine($"        <div class=\"logo-container logo-right\">{logoHtml}</div>");
            }
            
            // Logo below title
            if (hasLogo && _settings.LogoPosition == LogoPosition.BelowTitle)
            {
                html.AppendLine($"        <div class=\"logo-container logo-below\">{logoHtml}</div>");
            }
            
            html.AppendLine("    </div>");
        }
        
        private string GenerateLogoImgTag(string base64Data)
        {
            var widthStyle = $"width: {_settings.LogoWidth}px;";
            var heightStyle = _settings.LogoMaintainAspectRatio 
                ? "height: auto;" 
                : $"height: {_settings.LogoHeight}px;";
            var maxHeightStyle = _settings.LogoMaintainAspectRatio && _settings.LogoHeight > 0
                ? $"max-height: {_settings.LogoHeight}px;"
                : "";
            
            return $"<img src=\"data:image/png;base64,{base64Data}\" class=\"league-logo\" alt=\"{_settings.LeagueName} Logo\" style=\"{widthStyle} {heightStyle} {maxHeightStyle}\">";
        }
        
        private void GenerateCornerLogo(StringBuilder html, byte[] logoData, LogoPosition position)
        {
            var logoBase64 = Convert.ToBase64String(logoData);
            var logoHtml = GenerateLogoImgTag(logoBase64);
            var cornerClass = position == LogoPosition.BottomLeft ? "corner-logo-bottom-left" : "corner-logo-bottom-right";
            html.AppendLine($"    <div class=\"{cornerClass}\">{logoHtml}</div>");
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

            // Group weeks by month and generate one card per month
            var weeksByMonth = fixturesByWeek
                .GroupBy(g => new { g.Key.Year, g.Key.Month })
                .ToList();

            // Layout months in rows, fitting as many as possible per row
            var monthIndex = 0;
            while (monthIndex < weeksByMonth.Count)
            {
                html.AppendLine("<div class=\"month-cards-row\">");
                int colsInRow = 0;
                while (monthIndex < weeksByMonth.Count && colsInRow + weeksByMonth[monthIndex].Count() <= weeksPerRow)
                {
                    var monthGroup = weeksByMonth[monthIndex];
                    var monthWeeks = monthGroup.ToList();
                    var monthName = new DateTime(monthGroup.Key.Year, monthGroup.Key.Month, 1).ToString("MMMM");

                    html.AppendLine($"<div class=\"month-card\">");
                    html.AppendLine($"<div class=\"month-card-header\">{monthName}</div>");
                    GenerateMonthTable(html, monthWeeks, teams, divisions, teamNumbersByDivision, globalTeamNumbers);
                    html.AppendLine("</div>");

                    colsInRow += monthWeeks.Count;
                    monthIndex++;
                }
                // If a month alone exceeds weeksPerRow, emit it solo
                if (colsInRow == 0 && monthIndex < weeksByMonth.Count)
                {
                    var monthGroup = weeksByMonth[monthIndex];
                    var monthWeeks = monthGroup.ToList();
                    var monthName = new DateTime(monthGroup.Key.Year, monthGroup.Key.Month, 1).ToString("MMMM");

                    html.AppendLine($"<div class=\"month-card\">");
                    html.AppendLine($"<div class=\"month-card-header\">{monthName}</div>");
                    GenerateMonthTable(html, monthWeeks, teams, divisions, teamNumbersByDivision, globalTeamNumbers);
                    html.AppendLine("</div>");
                    monthIndex++;
                }
                html.AppendLine("</div>");
            }

            if (_settings.ShowGridLegend)
                html.AppendLine("<div class=\"grid-legend\"><strong>Bold</strong> = Home team &nbsp;·&nbsp; Regular = Away team</div>");
        }

        private void GenerateMonthTable(
            StringBuilder html,
            List<IGrouping<DateTime, Fixture>> fixturesByWeek,
            List<Team> teams,
            List<Division> divisions,
            Dictionary<Guid, Dictionary<Guid, int>> teamNumbersByDivision,
            Dictionary<Guid, int> globalTeamNumbers)
        {
            if (fixturesByWeek.Count == 0) return;

            var maxFixturesPerWeek = fixturesByWeek.Max(g => g.Count());

            // Today highlight
            var today = DateTime.Today;
            int todayColIndex = -1;
            double minDist = double.MaxValue;
            for (int ci = 0; ci < fixturesByWeek.Count; ci++)
            {
                var dist = Math.Abs((fixturesByWeek[ci].Key - today).TotalDays);
                if (dist < minDist) { minDist = dist; todayColIndex = ci; }
            }
            if (minDist > 7) todayColIndex = -1;

            var baseBanding = _settings.ColumnBanding switch
            {
                ColumnBanding.None => 0.0,
                ColumnBanding.Strong => 0.22,
                _ => 0.12
            };

            html.AppendLine("<table class=\"fixtures-grid\">");

            // Column colour banding
            html.AppendLine("<colgroup>");
            for (int ci = 0; ci < fixturesByWeek.Count; ci++)
            {
                var opacity = ci == todayColIndex ? Math.Max(baseBanding + 0.16, 0.20) : baseBanding;
                var colColor = HexToRgba(GetMonthColor(fixturesByWeek[ci].Key.Month), opacity);
                html.AppendLine($"    <col style=\"background-color: {colColor};\">");
            }
            html.AppendLine("</colgroup>");

            // Week date headers row
            html.AppendLine("<tr class=\"week-row\">");
            for (int wi = 0; wi < fixturesByWeek.Count; wi++)
            {
                var weekDate = fixturesByWeek[wi].Key;
                var dayAbbrev = weekDate.ToString("ddd");
                var dayNum = $"{weekDate.Day}{GetDaySuffix(weekDate.Day)}";

                var eventOnDate = _settings.SpecialEvents.FirstOrDefault(e => e.Date.Date == weekDate);
                var eventMarker = eventOnDate != null
                    ? $"<span class=\"event-marker\" style=\"background: {eventOnDate.Color};\"></span>"
                    : "";

                var thClass = wi == todayColIndex ? " class=\"today-week\"" : "";
                html.AppendLine($"    <th{thClass}>{eventMarker}<span class=\"week-day\">{dayAbbrev}</span><br>{dayNum}</th>");
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
                            html.AppendLine($"    <td title=\"{tooltip}\"><strong>{homeNum}</strong><span class=\"vs\">v</span>{awayNum}</td>");
                        }
                        else
                        {
                            var homeName = GetShortTeamName(homeTeam?.Name, 4);
                            var awayName = GetShortTeamName(awayTeam?.Name, 4);
                            html.AppendLine($"    <td title=\"{tooltip}\"><strong>{homeName}</strong><span class=\"vs\">v</span>{awayName}</td>");
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
            html.AppendLine("<div class=\"events-legend\">");
            html.AppendLine("    <span class=\"events-legend-title\">SPECIAL EVENTS</span>");
            foreach (var evt in _settings.SpecialEvents.OrderBy(e => e.Date))
            {
                html.AppendLine($"    <span class=\"event-tag\" style=\"border-left: 3px solid {evt.Color};\">");
                html.AppendLine($"        <strong>{evt.Date:ddd d MMM}</strong> {evt.Description}");
                html.AppendLine("    </span>");
            }
            html.AppendLine("</div>");
        }
        
        private void GenerateDivisionTeamLists(StringBuilder html, List<Division> divisions, List<Team> teams, List<Venue> venues)
        {
            html.AppendLine("<div class=\"division-lists\">");

            int divIndex = 0;
            foreach (var division in divisions.OrderBy(d => d.Name))
            {
                var divTeams = teams.Where(t => t.DivisionId == division.Id).OrderBy(t => t.Name).ToList();
                if (divTeams.Count == 0) continue;

                var accent = DivisionAccentColors[divIndex % DivisionAccentColors.Length];
                var headerBg = DivisionHeaderColors[divIndex % DivisionHeaderColors.Length];
                html.AppendLine($"<div class=\"division-box\" style=\"border-left: 3px solid {accent};\">");
                divIndex++;
                html.AppendLine($"    <h3 style=\"background: {headerBg};\">{division.Name}</h3>");
                html.AppendLine("    <table class=\"team-list\">");
                html.AppendLine("        <tr class=\"team-list-header\"><td></td><td>Team</td><td>Plays at</td></tr>");
                
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
            html.AppendLine("    <div class=\"phone-tags\">");
            foreach (var phone in _settings.VenuePhoneNumbers)
            {
                html.AppendLine($"        <span class=\"phone-tag\"><strong>{phone.Key}</strong> {phone.Value}</span>");
            }
            html.AppendLine("    </div>");
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

            // Text density scale factor
            var scale = _settings.TextDensity switch
            {
                TextDensity.Compact => -1,
                TextDensity.Spacious => 1,
                _ => 0
            };

            string Sz(double basePt) => $"{basePt + scale}pt";

            // Font sizes - adjust based on orientation and density
            var titleSize = Sz(isLandscape ? 18 : 16);
            var subtitleSize = Sz(isLandscape ? 12 : 11);
            var gridMonthSize = Sz(isLandscape ? 8 : 7);
            var gridWeekSize = Sz(isLandscape ? 7 : 6);
            var gridFixtureSize = Sz(isLandscape ? 8 : 7);
            var teamSize = Sz(isLandscape ? 8 : 7.5);
            var teamNameSize = Sz(isLandscape ? 7.5 : 7);
            var teamVenueSize = Sz(isLandscape ? 6.5 : 6);

            // Print font sizes
            var printTitleSize = Sz(isLandscape ? 16 : 14);
            var printSubtitleSize = Sz(isLandscape ? 11 : 10);
            var printGridMonthSize = Sz(isLandscape ? 7 : 6);
            var printGridWeekSize = Sz(isLandscape ? 6 : 5.5);
            var printGridFixtureSize = Sz(isLandscape ? 7 : 6);
            var printTeamSize = Sz(isLandscape ? 7 : 6.5);
            var printTeamVenueSize = Sz(isLandscape ? 6 : 5.5);

            // Text casing
            var titleCase = _settings.TitleUppercase ? "uppercase" : "none";
            var monthCase = _settings.MonthUppercase ? "uppercase" : "none";
            
            // Logo dimensions from settings
            var logoWidth = $"{_settings.LogoWidth}px";
            var logoMaxHeight = _settings.LogoHeight > 0 ? $"{_settings.LogoHeight}px" : "80px";

            // Design settings
            var accent = _settings.AccentColor;
            var accentLight = HexToRgba(accent, 0.08);

            var fontStack = _settings.FontFamily switch
            {
                SheetFontFamily.Classic => "'Georgia', 'Times New Roman', serif",
                SheetFontFamily.Mono => "'Consolas', 'Courier New', monospace",
                _ => "'Segoe UI', Calibri, Arial, sans-serif"
            };

            var (gridOuterBorder, gridInnerBorder, gridHeaderBorder) = _settings.GridBorders switch
            {
                GridBorderWeight.Fine => ("1px solid #ccc", "0.5px solid #ddd", "1px solid #aaa"),
                GridBorderWeight.Bold => ("2.5px solid #222", "1.5px solid #999", "2px solid #444"),
                _ => ("1.5px solid #333", "1px solid #bbb", "1.5px solid #555")
            };

            // Title style
            var titleBorderTop = _settings.TitleStyle switch
            {
                TitleStyle.DoubleRule => $"4px solid {accent}",
                TitleStyle.SingleRule => $"2px solid {accent}",
                TitleStyle.BoxBorder => $"2px solid {accent}",
                _ => "none"
            };
            var titleBorderBottom = _settings.TitleStyle switch
            {
                TitleStyle.DoubleRule => $"1.5px solid {accent}",
                TitleStyle.BoxBorder => $"2px solid {accent}",
                _ => "none"
            };
            var titleBorderSides = _settings.TitleStyle == TitleStyle.BoxBorder ? $"2px solid {accent}" : "none";

            // Home badge
            var homeBadgeCss = _settings.HomeBadge switch
            {
                HomeBadgeStyle.BoldOnly => @"
.fixtures-grid .fixture-row td strong {
    font-weight: 900;
}",
                _ => $@"
.fixtures-grid .fixture-row td strong {{
    display: inline-block;
    background: {accent};
    color: white;
    min-width: 12px;
    text-align: center;
    border-radius: 7px;
    font-size: 80%;
    font-weight: 800;
    padding: 0 3px;
    line-height: 14px;
    height: 14px;
}}"
            };

            var gridLegendBadgeCss = _settings.HomeBadge switch
            {
                HomeBadgeStyle.BoldOnly => @"
.grid-legend strong {
    font-weight: 900;
}",
                _ => $@"
.grid-legend strong {{
    display: inline-block;
    background: {accent};
    color: white;
    border-radius: 4px;
    font-size: 90%;
    padding: 0 3px;
    line-height: 11px;
    height: 11px;
    vertical-align: middle;
}}"
            };

            // Compute a darker shade for gradients
            var accentDark = DarkenHex(accent, 0.25);

            // Card style surfaces
            var (cardBg, cardBorder, cardBlur, gridBg) = _settings.CardStyle switch
            {
                CardStyle.Frosted => (
                    "rgba(255,255,255,0.55)",
                    $"1px solid rgba(255,255,255,0.35)",
                    "backdrop-filter: blur(12px); -webkit-backdrop-filter: blur(12px);",
                    "rgba(255,255,255,0.45)"
                ),
                CardStyle.Translucent => (
                    "rgba(255,255,255,0.72)",
                    $"1px solid rgba(255,255,255,0.5)",
                    "",
                    "rgba(255,255,255,0.65)"
                ),
                _ => (
                    "white",
                    "none",
                    "",
                    "white"
                )
            };

            var cardShadow = _settings.CardStyle == CardStyle.Solid
                ? "0 1px 4px rgba(0,0,0,0.08)"
                : "0 2px 8px rgba(0,0,0,0.06)";

            // Sheet background — adds a subtle tint for translucent/frosted so cards show through
            var sheetBg = _settings.CardStyle switch
            {
                CardStyle.Frosted => $"linear-gradient(135deg, {HexToRgba(accent, 0.08)} 0%, #f0f4f8 40%, {HexToRgba(accent, 0.05)} 100%)",
                CardStyle.Translucent => $"linear-gradient(160deg, {HexToRgba(accent, 0.06)} 0%, #f4f6f8 50%, {HexToRgba(accent, 0.04)} 100%)",
                _ => "#f8fafc"
            };

            var css = new StringBuilder();

            css.AppendLine($@"
/* A4 Page Setup - {pageOrientation.ToUpper()} */
@page {{ size: A4 {pageOrientation}; margin: 5mm; }}

* {{ box-sizing: border-box; margin: 0; padding: 0; }}

html, body {{
    margin: 0; padding: 0;
    font-family: {fontStack};
    font-size: 9pt;
    background: #f1f5f9;
    color: #0f172a;
    line-height: 1.5;
}}

.fixtures-sheet {{
    width: {sheetWidth}; max-width: {sheetWidth}; min-height: {sheetMinHeight};
    margin: 0 auto; padding: 4mm;
    background: {sheetBg};
    display: flex; flex-direction: column;
    gap: 2.5mm;
    position: relative;
}}

/* ══════════ Header Card ══════════ */
.sheet-header {{
    background: linear-gradient(135deg, {accent} 0%, {accentDark} 100%);
    border-radius: 6px;
    padding: 5mm 4mm;
    color: white;
    display: flex;
    flex-direction: column;
    align-items: center;
    position: relative;
    overflow: hidden;
}}

/* Subtle pattern overlay on header */
.sheet-header::before {{
    content: '';
    position: absolute;
    inset: 0;
    background-image: url(""data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='none' fill-rule='evenodd'%3E%3Cg fill='%23ffffff' fill-opacity='0.04'%3E%3Cpath d='M36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E"");
    pointer-events: none;
}}

.sheet-header.header-logo-left {{
    flex-direction: row;
    align-items: center;
    justify-content: flex-start;
    gap: 12px;
}}

.sheet-header.header-logo-right {{
    flex-direction: row;
    align-items: center;
    justify-content: flex-start;
    gap: 12px;
}}

.sheet-header.header-logo-right .title-section {{ order: 1; flex: 1; }}
.sheet-header.header-logo-right .logo-container {{ order: 2; }}

.title-section {{
    text-align: center;
    flex: 1;
    position: relative;
    z-index: 1;
}}

/* Logo */
.logo-container {{
    display: flex;
    justify-content: center;
    align-items: center;
    position: relative;
    z-index: 1;
}}
.logo-container.logo-above {{ margin-bottom: 2mm; }}
.logo-container.logo-below {{ margin-top: 2mm; }}
.logo-container.logo-left {{ margin-right: 12px; }}
.logo-container.logo-right {{ margin-left: 12px; }}

.league-logo {{
    max-width: {logoWidth};
    max-height: {logoMaxHeight};
    object-fit: contain;
    border-radius: 4px;
}}

/* Corner Logos */
.corner-logo-top-left,
.corner-logo-top-right,
.corner-logo-bottom-left,
.corner-logo-bottom-right {{
    position: absolute;
    z-index: 10;
}}
.corner-logo-top-left    {{ top: 2mm; left: 2mm; }}
.corner-logo-top-right   {{ top: 2mm; right: 2mm; }}
.corner-logo-bottom-left {{ bottom: 2mm; left: 2mm; }}
.corner-logo-bottom-right{{ bottom: 2mm; right: 2mm; }}

.corner-logo-top-left .league-logo,
.corner-logo-top-right .league-logo,
.corner-logo-bottom-left .league-logo,
.corner-logo-bottom-right .league-logo {{
    max-width: {logoWidth};
    max-height: {logoMaxHeight};
}}

/* ── Title ── */
.fixtures-sheet .sheet-title {{
    text-align: center;
    font-size: {titleSize};
    font-weight: 800;
    margin: 0;
    padding: 0;
    letter-spacing: 1px;
    text-transform: {titleCase};
    color: white;
    text-shadow: 0 1px 3px rgba(0,0,0,0.15);
    border: none;
    background: none;
}}

/* ── Subtitle ── */
.fixtures-sheet .sheet-subtitle {{
    text-align: center;
    font-size: {subtitleSize};
    font-weight: 500;
    margin: 1mm 0 0 0;
    padding: 0;
    letter-spacing: 1.5px;
    text-transform: uppercase;
    color: rgba(255,255,255,0.8);
    border: none;
    background: none;
}}

/* ══════════ Match-Day Strip ══════════ */
.match-day-strip {{
    text-align: center;
    font-size: 7.5pt;
    font-weight: 700;
    letter-spacing: 2.5px;
    text-transform: uppercase;
    color: {accent};
    padding: 2.5px 14px;
    margin: 0 auto;
    width: fit-content;
    background: {cardBg};
    border-radius: 20px;
    border: {(_settings.CardStyle == CardStyle.Solid ? $"1px solid {HexToRgba(accent, 0.2)}" : cardBorder)};
    box-shadow: {cardShadow};
    {cardBlur}
}}

/* ══════════ Month Cards Layout ══════════ */
.month-cards-row {{
    display: flex;
    gap: 2.5mm;
    align-items: flex-start;
}}

.month-card {{
    flex: 1;
    background: {cardBg};
    border: {cardBorder};
    border-radius: 6px;
    overflow: hidden;
    box-shadow: {cardShadow};
    {cardBlur}
}}

.month-card-header {{
    background: linear-gradient(135deg, {accent} 0%, {accentDark} 100%);
    color: white;
    text-align: center;
    font-weight: 700;
    font-size: {gridMonthSize};
    text-transform: {monthCase};
    padding: 4px 6px;
    letter-spacing: 1.5px;
}}

/* ══════════ Fixtures Grid ══════════ */
.fixtures-grid {{
    width: 100%;
    border-collapse: separate;
    border-spacing: 0;
    margin: 0;
    table-layout: fixed;
    border: none;
    background: {gridBg};
}}

.fixtures-grid th,
.fixtures-grid td {{
    border: none;
    border-right: 1px solid rgba(0,0,0,0.06);
    border-bottom: 1px solid rgba(0,0,0,0.06);
    text-align: center;
    vertical-align: middle;
    overflow: hidden;
    text-overflow: ellipsis;
}}

.fixtures-grid th:last-child,
.fixtures-grid td:last-child {{
    border-right: none;
}}

.fixtures-grid tr:last-child td {{
    border-bottom: none;
}}

/* Week/date header row */
.fixtures-grid .week-row th {{
    font-weight: 600;
    font-size: {gridWeekSize};
    padding: 2px 1px;
    line-height: 1.15;
    vertical-align: middle;
    white-space: nowrap;
    background: {HexToRgba(accent, 0.06)};
    border-bottom: 1px solid rgba(0,0,0,0.08);
}}

.fixtures-grid .week-row .week-day {{
    font-size: 80%;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.3px;
    color: {accent};
    display: block;
    line-height: 1;
}}

/* Fixture cells */
.fixtures-grid .fixture-row td {{
    font-size: {gridFixtureSize};
    font-weight: 400;
    padding: 2.5px 1.5px;
    line-height: 1.25;
    vertical-align: middle;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
    background-color: transparent;
    color: #334155;
}}

/* Home team indicator */
{homeBadgeCss}

.fixtures-grid .fixture-row td .vs {{
    display: inline-block;
    font-size: 65%;
    color: #94a3b8;
    font-weight: 400;
    margin: 0 0.5px;
    vertical-align: middle;
}}

/* Empty cells — subtle dot pattern */
.fixtures-grid .fixture-row td:empty {{
    background-image: radial-gradient(circle, #e2e8f0 0.5px, transparent 0.5px) !important;
    background-size: 4px 4px;
}}

/* Row hover-style striping */
.fixtures-grid .fixture-row:nth-child(even) td {{
    background-color: rgba(0,0,0,0.015);
}}

/* Grid legend */
.grid-legend {{
    font-size: 5.5pt;
    color: #94a3b8;
    text-align: right;
    padding: 1mm 1mm 0 0;
    letter-spacing: 0.2px;
}}

{gridLegendBadgeCss}

/* ══════════ Section Divider ══════════ */
.section-divider {{
    border: none;
    height: 0;
    margin: 1mm 0;
}}

/* ══════════ Event Markers & Legend ══════════ */
.event-marker {{
    display: block;
    width: 5px;
    height: 5px;
    border-radius: 50%;
    margin: 0 auto 1px;
}}

.today-week {{
    box-shadow: inset 0 -2px 0 {accent};
}}

.events-legend {{
    display: flex;
    flex-wrap: wrap;
    gap: 3px;
    align-items: center;
    padding: 2mm 3mm;
    background: {cardBg};
    border: {cardBorder};
    border-radius: 6px;
    box-shadow: {cardShadow};
    {cardBlur}
}}

.events-legend-title {{
    font-size: 6pt;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 1px;
    color: {accent};
    margin-right: 4px;
}}

.event-tag {{
    display: inline-flex;
    align-items: center;
    gap: 3px;
    padding: 2px 8px;
    font-size: 6.5pt;
    background: {HexToRgba(accent, 0.04)};
    border: 1px solid {HexToRgba(accent, 0.1)};
    border-radius: 20px;
}}

.event-tag strong {{
    font-weight: 700;
    color: {accent};
}}

/* ══════════ Division Cards ══════════ */
.division-lists {{
    display: flex;
    {(_settings.DivisionLayout switch
    {
        DivisionLayout.Stacked => "flex-direction: column;",
        _ => "flex-wrap: wrap;"
    })}
    gap: 2.5mm;
    flex: 1;
    align-content: flex-start;
}}

.division-box {{
    {(_settings.DivisionLayout switch
    {
        DivisionLayout.Stacked => "width: 100%; max-width: none;",
        DivisionLayout.Compact => $"flex: 1 1 calc(33% - 2mm); max-width: calc(33.33% - 2mm);",
        _ => $"flex: 1 1 {divBoxMinWidth}; max-width: {divBoxMaxWidth};"
    })}
    background: {cardBg};
    border: {cardBorder};
    border-radius: 6px;
    overflow: hidden;
    box-shadow: {cardShadow};
    {cardBlur}
}}

.division-box h3 {{
    background: linear-gradient(135deg, {accent} 0%, {accentDark} 100%);
    color: white;
    text-align: center;
    padding: 3.5px 8px;
    font-size: 7.5pt;
    font-weight: 700;
    margin: 0;
    letter-spacing: 1px;
    text-transform: uppercase;
}}

.team-list {{ width: 100%; border-collapse: separate; border-spacing: 0; }}

.team-list .team-list-header td {{
    font-size: 5pt;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    color: #94a3b8;
    font-weight: 600;
    padding: 2px 5px 1px;
    border-bottom: 1px solid #f1f5f9;
    background: transparent;
}}

.team-list td {{
    padding: 2.5px 5px;
    font-size: {teamSize};
    line-height: 1.4;
    border-bottom: 1px solid #f1f5f9;
}}

.team-list tr:last-child td {{
    border-bottom: none;
}}

.team-list .team-num {{
    width: 18px;
    text-align: center;
    font-weight: 800;
    font-size: 85%;
    color: white;
    background: linear-gradient(135deg, {accent}, {accentDark});
    border-radius: 4px;
    border-right: none;
    padding: 1.5px 0;
}}

.team-list .team-name {{
    font-weight: 600;
    font-size: {teamNameSize};
    color: #1e293b;
}}

.team-list .team-venue {{
    color: #94a3b8;
    font-size: {teamVenueSize};
    text-align: right;
}}

/* ══════════ Venue Phones Card ══════════ */
.venue-phones {{
    background: {cardBg};
    border: {cardBorder};
    border-radius: 6px;
    padding: 2.5mm 3mm;
    box-shadow: {cardShadow};
    {cardBlur}
}}

.venue-phones h4 {{
    text-align: center;
    margin: 0 0 1.5mm 0;
    font-size: 7pt;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 1px;
    color: {accent};
}}

.phone-tags {{
    display: flex;
    flex-wrap: wrap;
    gap: 3px;
    justify-content: center;
}}

.phone-tag {{
    display: inline-flex;
    align-items: center;
    background: {HexToRgba(accent, 0.05)};
    border: 1px solid {HexToRgba(accent, 0.12)};
    border-radius: 20px;
    padding: 2px 9px;
    font-size: 6.5pt;
    white-space: nowrap;
    color: #334155;
}}

.phone-tag strong {{
    font-weight: 700;
    margin-right: 4px;
    color: {accent};
}}

/* ══════════ Footer Card ══════════ */
.sheet-footer {{
    margin-top: auto;
    padding: 2.5mm 3mm;
    background: {cardBg};
    border: {cardBorder};
    border-radius: 6px;
    box-shadow: {cardShadow};
    font-size: 6pt;
    border-top: 3px solid {accent};
    {cardBlur}
}}

.sheet-footer p {{ margin: 1.5px 0; text-align: center; color: #64748b; }}
.sheet-footer .footer-note {{ font-weight: 700; color: #dc2626; }}
.sheet-footer .contact-line {{ margin-top: 1.5mm; font-weight: 600; color: #1e293b; }}
.sheet-footer a {{ color: {accent}; text-decoration: none; font-weight: 600; }}

.no-fixtures {{ text-align: center; padding: 10mm; color: #94a3b8; font-style: italic; }}

/* ══════════ Print Styles ══════════ */
@media print {{
    html, body {{
        width: {printWidth};
        height: {printHeight};
        margin: 0;
        padding: 0;
        background: white;
        -webkit-print-color-adjust: exact !important;
        print-color-adjust: exact !important;
        color-adjust: exact !important;
    }}

    .fixtures-sheet {{
        width: 100%;
        max-width: none;
        min-height: 100%;
        padding: 2mm;
        background: white;
        gap: 1.5mm;
        page-break-after: avoid;
        page-break-inside: avoid;
    }}

    .sheet-header {{
        padding: 3mm;
        border-radius: 4px;
    }}

    .fixtures-sheet .sheet-title {{
        font-size: {printTitleSize};
    }}

    .fixtures-sheet .sheet-subtitle {{
        font-size: {printSubtitleSize};
    }}

    .fixtures-grid {{
        box-shadow: none;
    }}

    .month-card {{
        box-shadow: none;
        border: 1px solid #e2e8f0;
        border-radius: 4px;
        background: white;
        backdrop-filter: none;
        -webkit-backdrop-filter: none;
    }}

    .month-card-header {{
        font-size: {printGridMonthSize};
        padding: 2px 4px;
    }}

    .month-cards-row {{
        gap: 1.5mm;
    }}

    .fixtures-grid .week-row th {{
        font-size: {printGridWeekSize};
        padding: 1.5px 1px;
    }}

    .fixtures-grid .fixture-row td {{
        font-size: {printGridFixtureSize};
        padding: 1.5px 1px;
    }}

    .grid-legend {{
        font-size: 5pt;
        padding: 0.3mm 1mm 0 0;
    }}

    .grid-legend strong {{
        font-size: 85%;
        line-height: 9px;
        height: 9px;
    }}

    .division-box h3 {{
        font-size: 7pt;
        padding: 2px 4px;
    }}

    .team-list .team-list-header td {{
        font-size: 4.5pt;
    }}

    .team-list td {{
        font-size: {printTeamSize};
        padding: 1px 3px;
    }}

    .team-list .team-name {{
        font-size: {printTeamSize};
    }}

    .team-list .team-venue {{
        font-size: {printTeamVenueSize};
    }}

    .match-day-strip {{
        font-size: 7pt;
        box-shadow: none;
        background: white;
        backdrop-filter: none;
        -webkit-backdrop-filter: none;
    }}

    .events-legend {{
        gap: 2px;
        box-shadow: none;
        border: 1px solid #e2e8f0;
        padding: 1.5mm 2mm;
        background: white;
        backdrop-filter: none;
        -webkit-backdrop-filter: none;
    }}

    .event-tag {{
        font-size: 5.5pt;
        padding: 1px 5px;
    }}

    .today-week {{
        box-shadow: none;
    }}

    .venue-phones {{
        box-shadow: none;
        border: 1px solid #e2e8f0;
        background: white;
        backdrop-filter: none;
        -webkit-backdrop-filter: none;
    }}

    .phone-tag {{
        font-size: 5.5pt;
        padding: 1px 5px;
    }}

    .sheet-footer {{
        font-size: 5.5pt;
        padding: 1.5mm;
        box-shadow: none;
        border: 1px solid #e2e8f0;
        background: white;
        backdrop-filter: none;
        -webkit-backdrop-filter: none;
    }}

    .division-box {{
        page-break-inside: avoid;
        border-radius: 4px;
        box-shadow: none;
        border: 1px solid #e2e8f0;
        background: white;
        backdrop-filter: none;
        -webkit-backdrop-filter: none;
    }}

    .sheet-footer a {{
        color: #000;
        text-decoration: none;
    }}

    .corner-logo-top-left,
    .corner-logo-top-right,
    .corner-logo-bottom-left,
    .corner-logo-bottom-right {{
        position: absolute;
    }}
}}

/* ══════════ Screen Preview ══════════ */
@media screen {{
    body {{
        background: #e2e8f0;
        padding: 20px;
    }}

    .fixtures-sheet {{
        box-shadow: 0 20px 60px rgba(0,0,0,0.12), 0 4px 12px rgba(0,0,0,0.06);
        margin: 20px auto;
        border-radius: 8px;
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

    .month-cards-row {{
        flex-direction: column;
    }}

    .sheet-header.header-logo-left,
    .sheet-header.header-logo-right {{
        flex-direction: column;
    }}
}}
");
            
            return css.ToString();
        }
        
        private string GetMonthColor(int month)
        {
            return _settings.MonthColors switch
            {
                MonthPalette.Vibrant => month switch
                {
                    1 => "#A78BFA", 2 => "#F472B6", 3 => "#FBBF24",
                    4 => "#C084FC", 5 => "#34D399", 6 => "#FB7185",
                    7 => "#38BDF8", 8 => "#FCD34D", 9 => "#A78BFA",
                    10 => "#F59E0B", 11 => "#3B82F6", 12 => "#10B981",
                    _ => "#E5E7EB"
                },
                MonthPalette.Monochrome => month switch
                {
                    1 => "#E5E5E5", 2 => "#D4D4D4", 3 => "#E5E5E5",
                    4 => "#D4D4D4", 5 => "#E5E5E5", 6 => "#D4D4D4",
                    7 => "#E5E5E5", 8 => "#D4D4D4", 9 => "#E5E5E5",
                    10 => "#D4D4D4", 11 => "#E5E5E5", 12 => "#D4D4D4",
                    _ => "#EEEEEE"
                },
                MonthPalette.Earth => month switch
                {
                    1 => "#D4B896", 2 => "#C4A882", 3 => "#D9C4A0",
                    4 => "#C9B48E", 5 => "#BFCFA0", 6 => "#D4B896",
                    7 => "#C9BFA0", 8 => "#D9CFA5", 9 => "#CCBF99",
                    10 => "#D4C4A0", 11 => "#C4B48C", 12 => "#BFAF8A",
                    _ => "#DDD5C0"
                },
                MonthPalette.Ocean => month switch
                {
                    1 => "#BAD6E8", 2 => "#A8CCD8", 3 => "#B8D8D0",
                    4 => "#A0D0D0", 5 => "#B0D8C8", 6 => "#C0D8E0",
                    7 => "#A8D0E0", 8 => "#98C8D8", 9 => "#B0D0D8",
                    10 => "#A0C8D0", 11 => "#B8D0E0", 12 => "#98C0D0",
                    _ => "#C8D8E0"
                },
                _ => month switch // Muted (default)
                {
                    1 => _settings.JanuaryColor, 2 => _settings.FebruaryColor,
                    3 => _settings.MarchColor, 4 => _settings.AprilColor,
                    5 => _settings.MayColor,
                    6 => "#E8D0C8", 7 => "#C8D8E8", 8 => "#E3E0C8",
                    9 => "#D4C8D8",
                    10 => _settings.OctoberColor, 11 => _settings.NovemberColor,
                    12 => _settings.DecemberColor,
                    _ => "#F0F0F0"
                }
            };
        }
        
        private string GetDaySuffix(int day)
        {
            if (day >= 11 && day <= 13) return "th";
            return (day % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        }

        private static string HexToRgba(string hex, double alpha)
        {
            hex = hex.TrimStart('#');
            if (hex.Length < 6) return $"rgba(200,200,200,{alpha})";
            int r = Convert.ToInt32(hex[..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..6], 16);
            return $"rgba({r},{g},{b},{alpha})";
        }

        private static string DarkenHex(string hex, double amount)
        {
            hex = hex.TrimStart('#');
            if (hex.Length < 6) return "#333333";
            int r = Math.Max(0, (int)(Convert.ToInt32(hex[..2], 16) * (1 - amount)));
            int g = Math.Max(0, (int)(Convert.ToInt32(hex[2..4], 16) * (1 - amount)));
            int b = Math.Max(0, (int)(Convert.ToInt32(hex[4..6], 16) * (1 - amount)));
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static readonly string[] DivisionAccentColors =
        [
            "#3B82F6", "#10B981", "#F59E0B", "#8B5CF6",
            "#EF4444", "#EC4899", "#06B6D4", "#84CC16"
        ];

        private static readonly string[] DivisionHeaderColors =
        [
            "#1D4ED8", "#047857", "#B45309", "#6D28D9",
            "#B91C1C", "#BE185D", "#0E7490", "#4D7C0F"
        ];
    }
}
