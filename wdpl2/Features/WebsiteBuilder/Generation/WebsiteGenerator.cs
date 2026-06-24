using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Wdpl2.Helpers;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    /// <summary>
    /// Generates static HTML websites from league data.
    /// 
    /// This is a partial class to enable future modularization:
    /// - WebsiteGenerator.cs: Core orchestration and page generation
    /// - WebsiteGenerator.CSS.cs: CSS/stylesheet generation (future)
    /// - WebsiteGenerator.Components.cs: Shared HTML components (future)
    /// </summary>
    public sealed partial class WebsiteGenerator
    {
        private readonly LeagueData _league;
        private readonly WebsiteSettings _settings;
        private List<Division> _navDivisions = new();

        /// <summary>Effective league settings resolved for the website's selected season.</summary>
        private AppSettings _leagueSettings = null!;

        /// <summary>Cache-busting version string derived from build time (UTC ticks). Appended to CSS/JSON URLs so browsers fetch fresh files after each website regeneration.</summary>
        private readonly string _cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        public WebsiteGenerator(LeagueData league, WebsiteSettings settings)
        {
            _league = league;
            _settings = settings;
        }

        /// <summary>
        /// Produces a filename-safe slug from a title or user-supplied slug:
        /// lowercased, alphanumerics + hyphens only, collapsed runs.
        /// </summary>
        private static string SanitizeSlug(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var sb = new StringBuilder(raw.Length);
            foreach (var ch in raw.Trim().ToLowerInvariant())
            {
                if (ch is >= 'a' and <= 'z' or >= '0' and <= '9') sb.Append(ch);
                else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
            }
            while (sb.Length > 0 && sb[^1] == '-') sb.Length--;
            return sb.ToString();
        }
        
        /// <summary>
        /// Generate all HTML files for the website
        /// </summary>
        public Dictionary<string, string> GenerateWebsite()
        {
            var files = new Dictionary<string, string>();
            
            var season = _settings.SelectedSeasonId.HasValue
                ? _league.Seasons.FirstOrDefault(s => s.Id == _settings.SelectedSeasonId.Value)
                : _league.Seasons.FirstOrDefault(s => s.IsActive);
            
            if (season == null)
            {
                throw new InvalidOperationException("No season selected for website generation");
            }

            _leagueSettings = _league.GetSettingsForSeason(season.Id);

            // Cache divisions for the active season so shared components (e.g. nav) can read them
            var (_navDivs, _, _, _, _) = _league.GetSeasonData(season.Id);
            _navDivisions = _navDivs.OrderBy(d => d.Name).ToList();

            // Generate files based on template
            var template = WebsiteTemplate.GetTemplateById(_settings.SelectedTemplate) ?? WebsiteTemplate.Modern;

            // Hoist season data + sub-generators (used by Players/Divisions/Captains branches below)
            var (seasonDivisions, seasonVenues, seasonTeams, seasonPlayers, seasonFixtures) = _league.GetSeasonData(season.Id);
            var jsonGenerator = new WebsiteJsonDataGenerator(_league, _settings, _leagueSettings);
            var templateGenerator = new WebsiteTemplatePageGenerator(_settings);

            // Core files
            files["home.html"] = GenerateIndexPage(season, template);
            files["style.css"] = GenerateStylesheet(template);

            // Optional pages
            if (_settings.ShowStandings)
            {
                files["standings.html"] = GenerateStandingsPage(season, template);
                foreach (var d in _navDivisions)
                {
                    var slug = StandingsDivisionSlug(d);
                    files[$"standings-{slug}.html"] = GenerateStandingsPage(season, template, d);
                }
            }
            
            if (_settings.ShowFixtures)
                files["fixtures.html"] = GenerateFixturesPage(season, template);
            
            if (_settings.ShowResults)
            {
                files["results.html"] = GenerateResultsPage(season, template);

                if (_settings.ResultsShowFrameDetails)
                    files["scorecards.html"] = GenerateScorecardsPage(season, template);
            }

            if (_settings.ShowPlayerStats)
            {
                files["players.html"] = GeneratePlayersPage(season, template);

                foreach (var d in _navDivisions)
                {
                    var slug = StandingsDivisionSlug(d);
                    files[$"players-{slug}.html"] = GeneratePlayersPage(season, template, d);
                }

                // Generate JSON data file and single template page (instead of individual HTML files per player)
                files["players-data.json"] = jsonGenerator.GeneratePlayersJson(seasonPlayers, seasonTeams, seasonFixtures);
                files["player.html"] = templateGenerator.GeneratePlayerTemplatePage(
                    season,
                    _cacheBuster,
                    AppendDocumentHead,
                    AppendHeader,
                    AppendNavigation,
                    AppendFooter,
                    GetTableClasses());
            }
            
            if (_settings.ShowDivisions)
            {
                files["divisions.html"] = GenerateDivisionsPage(season, template);
                
                // Generate JSON data file and single template page for teams
                files["teams-data.json"] = jsonGenerator.GenerateTeamsJson(seasonTeams, seasonDivisions, seasonVenues, seasonPlayers, seasonFixtures);
                files["team.html"] = templateGenerator.GenerateTeamTemplatePage(
                    season,
                    _cacheBuster,
                    AppendDocumentHead,
                    AppendHeader,
                    AppendNavigation,
                    AppendFooter,
                    GetTableClasses());
            }
            
            if (_settings.ShowCompetitions)
                files["competitions.html"] = GenerateCompetitionsPage(season, template);
            
            if (_settings.ShowGallery && _settings.GalleryImages.Count > 0)
                files["gallery.html"] = GenerateGalleryPage(season, template);
            
            if (_settings.ShowRules && _settings.HasAnyRulesContent)
                files["rules.html"] = GenerateRulesPage(season, template);
            
            if (_settings.ShowContactPage && _settings.HasContactInfo)
                files["contact.html"] = GenerateContactPage(season, template);
            
            if (_settings.ShowSponsors && _settings.Sponsors.Count > 0)
                files["sponsors.html"] = GenerateSponsorsPage(season, template);
            
            if (_settings.ShowNews && _settings.NewsItems.Count > 0)
                files["news.html"] = GenerateNewsPage(season, template);

            if (_settings.ShowRowsReports && _settings.RowsReports.Count > 0)
                files["rows-reports.html"] = GenerateRowsReportsPage(season, template);

            if (_settings.ShowEntryForms && _settings.EntryForms.Any(f => f.IsPublished))
            {
                files["entry-forms.html"] = GenerateEntryFormsPage(season, template);
                files["_submissions.html"] = GenerateSubmissionsAdminPage(season, template);
            }

            // Add UK 8-Ball Pool Game
            if (_settings.ShowPoolGame)
                files["pool-game.html"] = PoolGameGenerator.GeneratePoolGameHtml(_settings.LeagueName);

            if (_settings.ShowHistory && _settings.HistoricHonours.Count > 0)
                files["history.html"] = GenerateHistoryPage(season, template);

            // Captains Area - now just a static landing page that links to the
            // server-backed Captain Portal (deployed separately by BackendDeployService).
            // The old PIN-gated dashboard and captains-data.json have been retired.
            if (_settings.EnableCaptainsArea)
            {
                files["captains.html"] = templateGenerator.GenerateCaptainsLoginPage(
                    season, _cacheBuster,
                    AppendDocumentHead, AppendHeader, AppendNavigation, AppendFooter);
            }

            // Custom pages
            foreach (var page in _settings.CustomPages.Where(p => p.IsPublished))
            {
                var slug = string.IsNullOrWhiteSpace(page.Slug) ? SanitizeSlug(page.Title) : SanitizeSlug(page.Slug);
                if (string.IsNullOrWhiteSpace(slug)) continue;
                files[$"{slug}.html"] = GenerateCustomPage(season, template, page);
            }
            
            // Generate sitemap if enabled
            if (_settings.GenerateSitemap)
                files["sitemap.xml"] = GenerateSitemap(files.Keys.ToList());
            
            return files;
        }
        
        private string GenerateIndexPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            
            AppendDocumentHead(html, $"{_settings.HomeWelcomeTitle.Replace("{season}", season.Name)} - {_settings.LeagueName}", season);
            html.AppendLine("<body>");

            // Inline mobile responsive styles for the homepage canvas layout
            html.AppendLine(@"<style>
@media (max-width: 768px) {
    .page-canvas { gap: 8px !important; padding: 8px 0 !important; }
    .page-canvas .container { padding: 0 clamp(16px, 5vw, 24px) !important; }
    .page-canvas .section { padding: clamp(20px, 5vw, 28px) !important; }
    .page-canvas .leader-item { padding: 16px 18px !important; gap: 6px 14px !important; }
    .page-canvas .result-item,
    .page-canvas .fixture-item { padding: 16px 18px !important; }
    .page-canvas .results-list,
    .page-canvas .fixtures-list { gap: 14px !important; }
    .page-canvas .leaders-list { gap: 14px !important; }
}
@media (max-width: 480px) {
    .page-canvas .container { padding: 0 clamp(14px, 4vw, 20px) !important; }
    .page-canvas .section { padding: clamp(18px, 5vw, 24px) !important; }
    .page-canvas .leader-item { padding: 14px 16px !important; gap: 4px 12px !important; }
    .page-canvas .result-item,
    .page-canvas .fixture-item { padding: 14px 16px !important; }
}
</style>");

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);

            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            var completedFixtures = fixtures.Count(f => f.Frames.Any(fr => fr.Winner != FrameWinner.None));

            var blocks = _settings.GetEffectiveLayoutBlocks()
                .Where(b => b.IsEnabled)
                .OrderBy(b => b.Order)
                .ToList();

            LayoutBlock.AutoPositionBlocks(blocks);

            // Separate footer from canvas blocks so it flows naturally at the bottom
            var canvasBlocks = blocks.Where(b => b.BlockType != "footer").ToList();
            var footerBlock = blocks.FirstOrDefault(b => b.BlockType == "footer");

            var canvasHeight = canvasBlocks.Count != 0
                ? canvasBlocks.Max(b => b.TopPx + (b.HeightPx > 0 ? b.HeightPx : 350)) + 100
                : 800;

            html.AppendLine($"    <div class=\"page-canvas\" style=\"min-height:{canvasHeight.ToString("F0", inv)}px;\">");

            foreach (var block in canvasBlocks)
            {
                var left = block.LeftPercent.ToString("F1", inv);
                var top = block.TopPx.ToString("F0", inv);
                var width = block.WidthPercent.ToString("F1", inv);
                var posStyle = $"position:absolute; left:{left}%; top:{top}px; width:{width}%; max-width:{width}%; z-index:{block.ZIndex};";
                if (block.HeightPx > 0)
                    posStyle += $" height:{block.HeightPx.ToString("F0", inv)}px; overflow:auto;";

                var dataAttrs = $"data-block-id=\"{block.BlockType}\" data-block-name=\"{block.DisplayName}\" data-structural=\"{(block.IsStructural ? "true" : "false")}\"";
                var allAttrs = $"{dataAttrs} style=\"{posStyle}\"";

                switch (block.BlockType)
                {
                    case "header":
                        AppendHeaderBlock(html, season, allAttrs);
                        break;
                    case "nav":
                        AppendNavBlock(html, "Home", allAttrs);
                        break;
                    default:
                        html.AppendLine($"        <div {allAttrs}>");
                        html.AppendLine("            <div class=\"container\">");
                        AppendHomeSection(html, block.BlockType, season, divisions, venues, teams, players, fixtures, completedFixtures);
                        html.AppendLine("            </div>");
                        html.AppendLine("        </div>");
                        break;
                }
            }

            html.AppendLine("    </div>");

            // Footer rendered outside canvas in normal document flow for sticky footer
            if (footerBlock != null)
            {
                var footerAttrs = $"data-block-id=\"footer\" data-block-name=\"{footerBlock.DisplayName}\" data-structural=\"true\"";
                AppendFooterBlock(html, footerAttrs);
            }

            // Script to adjust canvas height based on actual rendered block sizes
            html.AppendLine(@"<script>
(function(){
    function adjustCanvas(){
        var c=document.querySelector('.page-canvas');
        if(!c)return;
        var max=0;
        for(var i=0;i<c.children.length;i++){
            var el=c.children[i];
            var b=el.offsetTop+el.offsetHeight;
            if(b>max)max=b;
        }
        if(max>0)c.style.minHeight=(max+60)+'px';
    }
    window.addEventListener('load',adjustCanvas);
    window.addEventListener('resize',adjustCanvas);
})();
</script>");

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);

            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        /// <summary>
        /// Renders a non-home page using the user's structural block order (header/nav/footer positions)
        /// </summary>
        private void RenderPageStructure(StringBuilder html, Season season, string activePage, Action<StringBuilder> renderContent)
        {
            var blocks = _settings.GetEffectiveLayoutBlocks()
                .Where(b => b.IsEnabled)
                .OrderBy(b => b.Order)
                .ToList();
            
            // Structural blocks + a "content" placeholder
            var structuralBlocks = blocks.Where(b => b.IsStructural).ToList();
            int contentOrder = blocks.Where(b => !b.IsStructural && b.IsEnabled).Select(b => b.Order).DefaultIfEmpty(2).Min();
            
            var pageElements = structuralBlocks
                .Select(b => (Order: b.Order, BlockType: b.BlockType, IsContent: false))
                .Append((Order: contentOrder, BlockType: "page-content", IsContent: true))
                .OrderBy(e => e.Order)
                .ToList();
            
            foreach (var elem in pageElements)
            {
                if (elem.IsContent)
                {
                    var dataAttrs = "data-block-id=\"page-content\" data-block-name=\"Page Content\" data-block-span=\"2\" data-structural=\"false\"";
                    html.AppendLine($"    <div class=\"content-area\" {dataAttrs}>");
                    html.AppendLine("        <div class=\"container\">");
                    renderContent(html);
                    html.AppendLine("        </div>");
                    html.AppendLine("    </div>");
                }
                else
                {
                    var dataAttrs = $"data-block-id=\"{elem.BlockType}\" data-block-name=\"{elem.BlockType}\" data-block-span=\"2\" data-structural=\"true\"";
                    switch (elem.BlockType)
                    {
                        case "header": AppendHeaderBlock(html, season, dataAttrs); break;
                        case "nav": AppendNavBlock(html, activePage, dataAttrs); break;
                        case "footer": AppendFooterBlock(html, dataAttrs); break;
                    }
                }
            }
        }
        
        /// <summary>
        /// Generates a complete page using the structural block ordering.
        /// </summary>
        private string GenerateFullPage(string title, Season season, string activePage, Action<StringBuilder> renderContent)
        {
            var html = new StringBuilder();
            AppendDocumentHead(html, title, season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            RenderPageStructure(html, season, activePage, renderContent);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            return html.ToString();
        }
        
        private void AppendHomeSection(StringBuilder html, string blockType, Season season, 
            List<Division> divisions, List<Venue> venues, List<Team> teams, 
            List<Player> players, List<Fixture> fixtures, int completedFixtures)
        {
            switch (blockType)
            {
                case "welcome":
                    if (_settings.HomeShowWelcomeSection)
                        AppendHomeWelcomeSection(html, season);
                    break;
                case "quick-stats":
                    if (_settings.HomeShowQuickStats)
                        AppendHomeQuickStatsSection(html, teams, players, divisions, completedFixtures);
                    break;
                case "league-leaders":
                    if (_settings.HomeShowLeagueLeaders)
                        AppendHomeLeagueLeadersSection(html, players, teams, fixtures);
                    break;
                case "recent-results":
                    if (_settings.HomeShowRecentResults)
                        AppendHomeRecentResultsSection(html, teams, fixtures, completedFixtures);
                    break;
                case "upcoming-fixtures":
                    if (_settings.HomeShowUpcomingFixtures)
                        AppendHomeUpcomingFixturesSection(html, teams, venues, fixtures);
                    break;
                case "latest-news":
                    if (_settings.ShowNews && _settings.HomeShowLatestNews)
                        AppendHomeLatestNewsSection(html);
                    break;
                case "sponsors":
                    if (_settings.ShowSponsors && _settings.HomeShowSponsors && _settings.Sponsors.Any(s => s.IsActive))
                        AppendSponsorsSection(html);
                    break;
                case "featured-pages":
                    if (_settings.HomeFeaturedPages.Count > 0)
                        AppendHomeFeaturedPages(html, season, divisions, venues, teams, players, fixtures);
                    break;
            }
        }
        
        private void AppendHomeWelcomeSection(StringBuilder html, Season season)
        {
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>{Esc(_settings.HomeWelcomeTitle.Replace("{season}", season.Name))}</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{season.StartDate:MMMM d, yyyy} - {season.EndDate:MMMM d, yyyy}</p>");
            if (!string.IsNullOrWhiteSpace(_settings.WelcomeMessage))
            {
                html.AppendLine($"                <p class=\"welcome-text\">{_settings.WelcomeMessage}</p>");
            }
            html.AppendLine("            </div>");
        }
        
        private void AppendHomeQuickStatsSection(StringBuilder html, List<Team> teams, List<Player> players, List<Division> divisions, int completedFixtures)
        {
            var statColumns = _settings.StatsColumns;
            html.AppendLine($"            <div class=\"stats-grid\" style=\"grid-template-columns: repeat(auto-fit, minmax(min({(statColumns == 2 ? "280px" : statColumns == 3 ? "200px" : "180px")}, 100%), 1fr));\">");
            html.AppendLine("                <div class=\"stat-card\">");
            html.AppendLine($"                    <div class=\"stat-number\">{teams.Count}</div>");
            html.AppendLine("                    <div class=\"stat-label\">Teams</div>");
            html.AppendLine("                </div>");
            var activePlayers = players.Count(p => p.IsActive && p.TeamId != null);
            html.AppendLine("                <div class=\"stat-card\">");
            html.AppendLine($"                    <div class=\"stat-number\">{activePlayers}</div>");
            html.AppendLine("                    <div class=\"stat-label\">Players</div>");
            html.AppendLine("                </div>");
            html.AppendLine("                <div class=\"stat-card\">");
            html.AppendLine($"                    <div class=\"stat-number\">{completedFixtures}</div>");
            html.AppendLine("                    <div class=\"stat-label\">Matches Played</div>");
            html.AppendLine("                </div>");
            html.AppendLine("                <div class=\"stat-card\">");
            html.AppendLine($"                    <div class=\"stat-number\">{divisions.Count}</div>");
            html.AppendLine("                    <div class=\"stat-label\">Divisions</div>");
            html.AppendLine("                </div>");
            html.AppendLine("            </div>");
        }
        
        private void AppendHomeLeagueLeadersSection(StringBuilder html, List<Player> players, List<Team> teams, List<Fixture> fixtures)
        {
            if (!_settings.ShowTopScorers) return;
            
            var playerStats = CalculatePlayerStats(players, teams, fixtures);
            
            int minFramesRequired = 0;
            if (_settings.PlayersUsePercentageFilter && _settings.PlayersMinFramesPercentage > 0)
            {
                var maxFrames = playerStats.Count != 0 ? playerStats.Max(p => p.Played) : 0;
                minFramesRequired = (int)Math.Ceiling(maxFrames * (_settings.PlayersMinFramesPercentage / 100.0));
            }
            else if (_settings.PlayersMinGames > 0)
            {
                minFramesRequired = _settings.PlayersMinGames;
            }
            else if (_leagueSettings.MinFramesPercentage > 0)
            {
                var maxFrames = playerStats.Count != 0 ? playerStats.Max(p => p.Played) : 0;
                minFramesRequired = (int)Math.Ceiling(maxFrames * (_leagueSettings.MinFramesPercentage / 100.0));
            }
            
            var topPlayers = playerStats
                .Where(p => p.Played >= minFramesRequired)
                .OrderByDescending(s => s.Rating)
                .ThenByDescending(s => s.WinPercentage)
                .ThenByDescending(s => s.Won)
                .Take(_settings.HomeLeagueLeadersCount)
                .ToList();
            
            if (topPlayers.Count == 0) return;
            
            html.AppendLine("            <section class=\"section\">");
            html.AppendLine("                <h3>&#127942; League Leaders</h3>");
            html.AppendLine("                <div class=\"leaders-list\">");
            var rank = 1;
            for (var i = 0; i < topPlayers.Count; i++)
            {
                var player = topPlayers[i];
                if (i > 0 && topPlayers[i - 1].Rating == player.Rating)
                    rank = rank; // joint — keep same rank
                else
                    rank = i + 1;
                var isJoint = (i > 0 && topPlayers[i - 1].Rating == player.Rating)
                           || (i < topPlayers.Count - 1 && topPlayers[i + 1].Rating == player.Rating);
                var posDisplay = isJoint ? $"{rank}=" : rank.ToString();
                html.AppendLine("                    <div class=\"leader-item\">");
                html.AppendLine($"                        <span class=\"rank\">{posDisplay}</span>");
                html.AppendLine($"                        <span class=\"player-name\">{player.PlayerName}</span>");
                html.AppendLine($"                        <span class=\"player-team\">{player.TeamName}</span>");
                html.AppendLine($"                        <span class=\"player-stat\">{player.Rating}</span>");
                html.AppendLine("                    </div>");
            }
            html.AppendLine("                </div>");
            if (_settings.ShowPlayerStats)
                html.AppendLine("                <p class=\"view-all\"><a href=\"players.html\">View All Players &#8594;</a></p>");
            html.AppendLine("            </section>");
        }
        
        private void AppendHomeRecentResultsSection(StringBuilder html, List<Team> teams, List<Fixture> fixtures, int completedFixtures)
        {
            if (!(_settings.ShowResults && completedFixtures > 0)) return;
            
            html.AppendLine("            <section class=\"section\">");
            html.AppendLine("                <h3>&#127937; Recent Results</h3>");
            
            var recentResults = fixtures
                .Where(f => f.Frames.Any(fr => fr.Winner != FrameWinner.None))
                .OrderByDescending(f => f.Date)
                .Take(_settings.HomeRecentResultsCount)
                .ToList();
            
            html.AppendLine("                <div class=\"results-list\">");
            foreach (var fixture in recentResults)
            {
                var homeTeam = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
                var awayTeam = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId);
                var isHomeWin = fixture.HomeScore > fixture.AwayScore;
                var isDraw = fixture.HomeScore == fixture.AwayScore;
                
                html.AppendLine("                    <div class=\"result-item\">");
                if (_settings.ResultsShowDate)
                    html.AppendLine($"                        <span class=\"date\">{fixture.Date.ToString(_settings.ResultsDateFormat)}</span>");
                html.AppendLine($"                        <span class=\"team{(_settings.ResultsHighlightWinner && isHomeWin ? " winner" : "")}\">{homeTeam?.Name ?? "Home"}</span>");
                html.AppendLine($"                        <span class=\"score\">{fixture.HomeScore} - {fixture.AwayScore}</span>");
                html.AppendLine($"                        <span class=\"team{(_settings.ResultsHighlightWinner && !isHomeWin && !isDraw ? " winner" : "")}\">{awayTeam?.Name ?? "Away"}</span>");
                html.AppendLine("                    </div>");
            }
            html.AppendLine("                </div>");
            html.AppendLine("                <p class=\"view-all\"><a href=\"results.html\">View All Results &#8594;</a></p>");
            html.AppendLine("            </section>");
        }
        
        private void AppendHomeUpcomingFixturesSection(StringBuilder html, List<Team> teams, List<Venue> venues, List<Fixture> fixtures)
        {
            if (!_settings.ShowFixtures) return;
            
            var upcomingFixtures = fixtures
                .Where(f => f.Date > DateTime.Now && !f.Frames.Any(fr => fr.Winner != FrameWinner.None))
                .OrderBy(f => f.Date)
                .Take(_settings.HomeUpcomingFixturesCount)
                .ToList();
            
            if (upcomingFixtures.Count == 0) return;
            
            html.AppendLine("            <section class=\"section\">");
            html.AppendLine("                <h3>&#128197; Upcoming Fixtures</h3>");
            html.AppendLine("                <div class=\"fixtures-list\">");
            
            
            foreach (var fixture in upcomingFixtures)
            {
                var homeTeam = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
                var awayTeam = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId);
                var venue = fixture.VenueId.HasValue ? venues.FirstOrDefault(v => v.Id == fixture.VenueId.Value) : null;
                
                html.AppendLine("                    <div class=\"fixture-item\">");
                if (_settings.FixturesShowDate)
                {
                    var dateStr = _settings.FixturesShowTime 
                        ? fixture.Date.ToString($"{_settings.FixturesDateFormat} HH:mm")
                        : fixture.Date.ToString(_settings.FixturesDateFormat);
                    html.AppendLine($"                        <span class=\"date\">{dateStr}</span>");
                }
                html.AppendLine($"                        <span class=\"team\">{homeTeam?.Name ?? "Home"}</span>");
                html.AppendLine("                        <span class=\"vs\">vs</span>");
                html.AppendLine($"                        <span class=\"team\">{awayTeam?.Name ?? "Away"}</span>");
                if (_settings.FixturesShowVenue && venue != null)
                    html.AppendLine($"                        <span class=\"venue\">{venue.Name}</span>");
                html.AppendLine("                    </div>");
            }
            
            html.AppendLine("                </div>");
            html.AppendLine("                <p class=\"view-all\"><a href=\"fixtures.html\">View All Fixtures &#8594;</a></p>");
            html.AppendLine("            </section>");
        }

        private void AppendHomeLatestNewsSection(StringBuilder html)
        {
            var latestNews = _settings.NewsItems
                .Where(n => n.IsPublished)
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.DatePublished)
                .Take(3)
                .ToList();

            if (latestNews.Count == 0) return;

            html.AppendLine("            <section class=\"section\">");
            html.AppendLine("                <h3>&#128240; Latest News</h3>");
            html.AppendLine("                <div class=\"news-list\">");
            foreach (var news in latestNews)
            {
                html.AppendLine("                    <div class=\"news-item\">");
                if (news.IsPinned)
                    html.AppendLine("                        <span class=\"pinned-badge\">&#128204;</span>");
                html.AppendLine($"                        <div class=\"news-title\">{news.Title}</div>");
                html.AppendLine($"                        <div class=\"news-date\">{news.DatePublished:dd MMM yyyy}</div>");
                if (!string.IsNullOrWhiteSpace(news.Content))
                {
                    var preview = news.Content.Length > 120 ? news.Content[..120] + "..." : news.Content;
                    html.AppendLine($"                        <div class=\"news-preview\">{preview}</div>");
                }
                html.AppendLine("                    </div>");
            }
            html.AppendLine("                </div>");
            if (_settings.ShowNews)
                html.AppendLine("                <p class=\"view-all\"><a href=\"news.html\">View All News &#8594;</a></p>");
            html.AppendLine("            </section>");
        }

        private void AppendHomeFeaturedPages(StringBuilder html, Season season,
            List<Division> divisions, List<Venue> venues, List<Team> teams,
            List<Player> players, List<Fixture> fixtures)
        {
            foreach (var page in _settings.HomeFeaturedPages)
            {
                switch (page)
                {
                    case "entry-forms":
                        if (_settings.ShowEntryForms)
                            AppendHomeActiveEntryForm(html);
                        break;
                    case "standings":
                        if (_settings.ShowStandings)
                            AppendHomeStandingsSection(html, divisions, teams, fixtures);
                        break;
                    case "competitions":
                        if (_settings.ShowCompetitions)
                            AppendHomeFeaturedPageCard(html, "\U0001F3C6", "Competitions", "View cup draws, knockout brackets and competition results.", "competitions.html");
                        break;
                    case "gallery":
                        if (_settings.ShowGallery && _settings.GalleryImages.Count > 0)
                            AppendHomeFeaturedPageCard(html, "\U0001F4F7", "Photo Gallery", $"{_settings.GalleryImages.Count} photo{(_settings.GalleryImages.Count == 1 ? "" : "s")} from the season.", "gallery.html");
                        break;
                    case "rules":
                        if (_settings.ShowRules && _settings.HasAnyRulesContent)
                            AppendHomeFeaturedPageCard(html, "\U0001F4D6", "League Rules", "View the official league rules and regulations.", "rules.html");
                        break;
                    case "contact":
                        if (_settings.ShowContactPage && _settings.HasContactInfo)
                            AppendHomeFeaturedPageCard(html, "\U0001F4E7", "Contact Us", "Get in touch with the league committee.", "contact.html");
                        break;
                    case "rows-reports":
                        if (_settings.ShowRowsReports && _settings.RowsReports.Count > 0)
                            AppendHomeFeaturedPageCard(html, "\U0001F4CA", "Rows Reports", $"{_settings.RowsReports.Count} report{(_settings.RowsReports.Count == 1 ? "" : "s")} available.", "rows-reports.html");
                        break;
                }
            }
        }

        private void AppendHomeActiveEntryForm(StringBuilder html)
        {
            var activeForm = _settings.EntryForms
                .Where(f => f.IsPublished && !f.IsClosed && (!f.ClosingDate.HasValue || f.ClosingDate.Value >= DateTime.Now))
                .OrderBy(f => f.SortOrder)
                .ThenByDescending(f => f.DateCreated)
                .FirstOrDefault();

            if (activeForm == null) return;

            var formId = $"form-{activeForm.Id:N}";

            html.AppendLine("            <section class=\"section entry-form-card\">");

            if (activeForm.LogoImageData != null && activeForm.LogoImageData.Length > 0)
            {
                var imgOpt = new ImageOptimizationService();
                var dataUrl = imgOpt.ToDataUrl(activeForm.LogoImageData, imgOpt.GetMimeType("logo.png"));
                html.AppendLine($"                <div class=\"entry-form-logo\"><img src=\"{dataUrl}\" alt=\"{Esc(activeForm.Title)}\"></div>");
            }

            html.AppendLine("                <div class=\"entry-form-header\">");
            html.AppendLine($"                    <h3>&#128203; {Esc(activeForm.Title)}</h3>");
            html.AppendLine("                    <span class=\"entry-form-badge open\">Open</span>");
            html.AppendLine("                </div>");

            if (!string.IsNullOrWhiteSpace(activeForm.Description))
                html.AppendLine($"                <p class=\"entry-form-desc\">{Esc(activeForm.Description)}</p>");

            if (activeForm.ClosingDate.HasValue)
                html.AppendLine($"                <p class=\"entry-form-deadline\">&#9200; Closing date: <strong>{activeForm.ClosingDate.Value:dddd dd MMMM yyyy}</strong></p>");

            if (activeForm.Fields.Count != 0)
            {
                html.AppendLine($"                <form class=\"entry-form\" id=\"{formId}-form\" onsubmit=\"return handleEntrySubmit(this)\">");
                html.AppendLine("                    <p style=\"margin-bottom:1rem;color:var(--text-secondary, #64748B);\"><em>Fill in the form below and click Submit to log your entry.</em></p>");
                html.AppendLine($"                    <input type=\"hidden\" name=\"_formId\" value=\"{formId}\">");

                foreach (var field in activeForm.Fields.OrderBy(f => f.SortOrder))
                {
                    var fieldId = $"field-{field.Id:N}";
                    var fieldName = Esc(field.Label);
                    var requiredAttr = field.IsRequired ? " required" : "";
                    var requiredStar = field.IsRequired ? " <span class=\"required\">*</span>" : "";
                    var placeholder = !string.IsNullOrWhiteSpace(field.Placeholder) ? $" placeholder=\"{Esc(field.Placeholder)}\"" : "";

                    html.AppendLine("                    <div class=\"form-group\">");

                    switch (field.FieldType)
                    {
                        case "textarea":
                            html.AppendLine($"                        <label for=\"{fieldId}\">{Esc(field.Label)}{requiredStar}</label>");
                            html.AppendLine($"                        <textarea id=\"{fieldId}\" name=\"{fieldName}\" rows=\"4\"{placeholder}{requiredAttr}></textarea>");
                            break;
                        case "select":
                            html.AppendLine($"                        <label for=\"{fieldId}\">{Esc(field.Label)}{requiredStar}</label>");
                            html.AppendLine($"                        <select id=\"{fieldId}\" name=\"{fieldName}\"{requiredAttr}>");
                            html.AppendLine("                            <option value=\"\">-- Select --</option>");
                            if (!string.IsNullOrWhiteSpace(field.Options))
                            {
                                foreach (var opt in field.Options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                    html.AppendLine($"                            <option value=\"{Esc(opt)}\">{Esc(opt)}</option>");
                            }
                            html.AppendLine("                        </select>");
                            break;
                        case "checkbox":
                            html.AppendLine($"                        <label class=\"checkbox-label\"><input type=\"checkbox\" id=\"{fieldId}\" name=\"{fieldName}\"{requiredAttr}> {Esc(field.Label)}{requiredStar}</label>");
                            break;
                        default:
                            var inputType = field.FieldType switch
                            {
                                "email" => "email",
                                "phone" => "tel",
                                "number" => "number",
                                "date" => "date",
                                _ => "text"
                            };
                            html.AppendLine($"                        <label for=\"{fieldId}\">{Esc(field.Label)}{requiredStar}</label>");
                            html.AppendLine($"                        <input type=\"{inputType}\" id=\"{fieldId}\" name=\"{fieldName}\"{placeholder}{requiredAttr}>");
                            break;
                    }

                    html.AppendLine("                    </div>");
                }

                var submitText = !string.IsNullOrWhiteSpace(activeForm.SubmitButtonText) ? Esc(activeForm.SubmitButtonText) : "Submit Entry";
                html.AppendLine($"                    <button type=\"submit\" class=\"entry-form-submit\">{submitText}</button>");
                html.AppendLine("                </form>");
                html.AppendLine($"                <div class=\"entry-form-confirmation\" id=\"{formId}-confirm\" style=\"display:none;\">");
                html.AppendLine("                    <p>&#9989; Your entry has been submitted successfully! The league secretary will review it shortly.</p>");
                html.AppendLine("                </div>");

                var fieldLabels = activeForm.Fields.OrderBy(f => f.SortOrder)
                    .Select(f => $"{{\"id\":\"field-{f.Id:N}\",\"label\":\"{Esc(f.Label).Replace("\"", "\\\"")}\",\"type\":\"{f.FieldType}\"}}")
                    .ToList();
                html.AppendLine($"                <script>window.formFields = window.formFields || {{}}; window.formFields['{formId}'] = [{string.Join(",", fieldLabels)}];</script>");

                // Include form submission JS
                AppendEntryFormSubmitScript(html, "                ");
            }

            html.AppendLine("                <p class=\"view-all\"><a href=\"entry-forms.html\">View All Entry Forms &#8594;</a></p>");
            html.AppendLine("            </section>");
        }

        private void AppendHomeStandingsSection(StringBuilder html, List<Division> divisions, List<Team> teams, List<Fixture> fixtures)
        {
            var firstDiv = divisions.OrderBy(d => d.Name).FirstOrDefault();
            if (firstDiv == null) return;

            var divTeams = teams.Where(t => t.DivisionId == firstDiv.Id).ToList();
            if (divTeams.Count == 0) return;

            var standings = StandingsCalculator.Calculate(divTeams, fixtures, _leagueSettings, trackForm: true);
            var sorted = StandingsSorter.Sort(standings, _leagueSettings,
                s => s.Points, s => s.FramesFor, s => s.FramesAgainst, s => s.Won, s => s.TeamId, fixtures);

            html.AppendLine("            <section class=\"section\">");
            html.AppendLine($"                <h3>&#127942; {firstDiv.Name} Standings</h3>");
            html.AppendLine("                <div class=\"table-responsive\">");
            html.AppendLine($"                <table class=\"{GetTableClasses()}\">");
            html.AppendLine("                    <thead><tr>");
            html.AppendLine("                        <th>Pos</th><th>Team</th><th>P</th><th>W</th><th>L</th><th>Pts</th>");
            html.AppendLine("                    </tr></thead>");
            html.AppendLine("                    <tbody>");

            var topSorted = sorted.Take(8).ToList();
            var pos = 1;
            for (var i = 0; i < topSorted.Count; i++)
            {
                var s = topSorted[i];
                if (i > 0 && topSorted[i - 1].Points == s.Points)
                    pos = pos; // joint
                else
                    pos = i + 1;
                var isJoint = (i > 0 && topSorted[i - 1].Points == s.Points)
                           || (i < topSorted.Count - 1 && topSorted[i + 1].Points == s.Points);
                var posDisplay = isJoint ? $"{pos}=" : pos.ToString();
                html.AppendLine($"                        <tr><td>{posDisplay}</td><td><strong>{s.TeamName}</strong></td><td>{s.Played}</td><td>{s.Won}</td><td>{s.Lost}</td><td><strong>{s.Points}</strong></td></tr>");
            }

            html.AppendLine("                    </tbody>");
            html.AppendLine("                </table>");
            html.AppendLine("                </div>");
            html.AppendLine("                <p class=\"view-all\"><a href=\"standings.html\">View Full Standings &#8594;</a></p>");
            html.AppendLine("            </section>");
        }

        private static void AppendHomeFeaturedPageCard(StringBuilder html, string icon, string title, string description, string href)
        {
            html.AppendLine("            <section class=\"section featured-page-card\">");
            html.AppendLine($"                <a href=\"{href}\" class=\"featured-page-link\">");
            html.AppendLine($"                    <span class=\"featured-page-icon\">{icon}</span>");
            html.AppendLine($"                    <div class=\"featured-page-info\">");
            html.AppendLine($"                        <h3>{title}</h3>");
            html.AppendLine($"                        <p>{description}</p>");
            html.AppendLine($"                    </div>");
            html.AppendLine("                    <span class=\"featured-page-arrow\">&#8594;</span>");
            html.AppendLine("                </a>");
            html.AppendLine("            </section>");
        }

        private string GenerateStandingsPage(Season season, WebsiteTemplate template, Division? singleDivision = null)
        {
            var html = new StringBuilder();
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);

            var pageTitle = _settings.StandingsPageTitle;
            var docTitle = singleDivision != null
                ? $"{singleDivision.Name} - {pageTitle} - {_settings.LeagueName}"
                : $"{pageTitle} - {_settings.LeagueName}";
            AppendDocumentHead(html, docTitle, season);
            html.AppendLine("<body>");

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);

            AppendHeader(html, season);
            AppendNavigation(html, "Standings");

            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>{Esc(pageTitle)}</h2>");
            var heroSub = singleDivision != null
                ? $"{Esc(singleDivision.Name)} &middot; {Esc(season.Name)}"
                : Esc(season.Name);
            html.AppendLine($"                <p class=\"hero-dates\">{heroSub}</p>");
            html.AppendLine("            </div>");

            var divisionsToRender = singleDivision != null
                ? new[] { singleDivision }.AsEnumerable()
                : divisions.OrderBy(d => d.Name);

            foreach (var division in divisionsToRender)
            {
                var divisionTeams = teams.Where(t => t.DivisionId == division.Id).ToList();
                if (divisionTeams.Count == 0) continue;
                
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine($"                <h3>{division.Name}</h3>");
                html.AppendLine("                <div class=\"table-responsive\">");
                html.AppendLine($"                <table class=\"{GetTableClasses()}\">");
                html.AppendLine("                    <thead>");
                html.AppendLine("                        <tr>");
                if (_settings.StandingsShowPosition) html.AppendLine("                            <th>Pos</th>");
                html.AppendLine("                            <th>Team</th>");
                if (_settings.StandingsShowPlayed) html.AppendLine("                            <th>P</th>");
                if (_settings.StandingsShowWon) html.AppendLine("                            <th>W</th>");
                if (_settings.StandingsShowLost) html.AppendLine("                            <th>L</th>");
                if (_settings.StandingsShowFramesFor) html.AppendLine("                            <th>F</th>");
                if (_settings.StandingsShowFramesAgainst) html.AppendLine("                            <th>A</th>");
                if (_settings.StandingsShowFramesDiff) html.AppendLine("                            <th>Diff</th>");
                if (_settings.StandingsShowForm) html.AppendLine("                            <th>Form</th>");
                if (_settings.StandingsShowDeducted) html.AppendLine("                            <th>Ded</th>");
                if (_settings.StandingsShowPoints) html.AppendLine("                            <th>Pts</th>");
                html.AppendLine("                        </tr>");
                html.AppendLine("                    </thead>");
                html.AppendLine("                    <tbody>");
                
                var standings = StandingsCalculator.Calculate(divisionTeams, fixtures, _leagueSettings, trackForm: true);
                var sortedStandings = StandingsSorter.Sort(
                    standings,
                    _leagueSettings,
                    s => s.Points,
                    s => s.FramesFor,
                    s => s.FramesAgainst,
                    s => s.Won,
                    s => s.TeamId,
                    fixtures);
                var position = 1;
                var totalTeams = sortedStandings.Count;

                for (var idx = 0; idx < sortedStandings.Count; idx++)
                {
                    var standing = sortedStandings[idx];
                    if (idx > 0 && sortedStandings[idx - 1].Points == standing.Points)
                        position = position; // joint
                    else
                        position = idx + 1;

                    var rowClass = "";
                    if (_settings.StandingsHighlightTop && position <= _settings.StandingsHighlightTopCount)
                        rowClass = "highlight-top";
                    else if (_settings.StandingsHighlightBottom && position > totalTeams - _settings.StandingsHighlightBottomCount)
                        rowClass = "highlight-bottom";

                    html.AppendLine($"                        <tr{(string.IsNullOrEmpty(rowClass) ? "" : $" class=\"{rowClass}\"")} >");

                    if (_settings.StandingsShowPosition)
                    {
                        var isJoint = (idx > 0 && sortedStandings[idx - 1].Points == standing.Points)
                                   || (idx < sortedStandings.Count - 1 && sortedStandings[idx + 1].Points == standing.Points);
                        var posDisplay = isJoint ? $"{position}=" : position.ToString();
                        html.AppendLine($"                            <td>{posDisplay}</td>");
                    }
                    
                    html.AppendLine($"                            <td><strong><a href=\"team.html?id={standing.TeamId:N}\" class=\"team-link\">{standing.TeamName}</a></strong></td>");
                    if (_settings.StandingsShowPlayed) html.AppendLine($"                            <td>{standing.Played}</td>");
                    if (_settings.StandingsShowWon) html.AppendLine($"                            <td>{standing.Won}</td>");
                    if (_settings.StandingsShowLost) html.AppendLine($"                            <td>{standing.Lost}</td>");
                    if (_settings.StandingsShowFramesFor) html.AppendLine($"                            <td>{standing.FramesFor}</td>");
                    if (_settings.StandingsShowFramesAgainst) html.AppendLine($"                            <td>{standing.FramesAgainst}</td>");
                    if (_settings.StandingsShowFramesDiff) html.AppendLine($"                            <td class=\"{(standing.FrameDifference > 0 ? "text-positive" : standing.FrameDifference < 0 ? "text-negative" : "")}\">{standing.FrameDifference:+0;-0;0}</td>");
                    if (_settings.StandingsShowForm) html.AppendLine($"                            <td class=\"form\">{string.Join("", standing.RecentForm.Take(5).Select(f => f switch { 'W' => "&#128994;", 'L' => "&#128308;", _ => "&#9898;" }))}</td>");
                    if (_settings.StandingsShowDeducted) html.AppendLine($"                            <td{(standing.Deducted > 0 ? " class=\"text-negative\"" : "")}>{standing.Deducted}</td>");
                    if (_settings.StandingsShowPoints) html.AppendLine($"                            <td><strong>{standing.Points}</strong></td>");
                    html.AppendLine("                        </tr>");
                }

                html.AppendLine("                    </tbody>");
                html.AppendLine("                </table>");
                html.AppendLine("                </div>");
            html.AppendLine("            </div>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private string GenerateFixturesPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            
            AppendDocumentHead(html, $"{_settings.FixturesPageTitle} - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Fixtures");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>&#128197; {Esc(_settings.FixturesPageTitle)}</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">Upcoming Matches</p>");
            html.AppendLine("            </div>");

            // Fixtures sheet (always shown on fixtures page, not collapsible)
            AppendFixturesSheetSection(html, season, divisions, venues, teams, fixtures, collapsible: false);

            // Team calendar downloads
            if (_settings.FixturesShowCalendarDownload)
            {
                AppendTeamCalendarSection(html, season, teams, fixtures, venues);
            }

            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private void AppendFixturesSheetSection(StringBuilder html, Season season, List<Division> divisions, List<Venue> venues, List<Team> teams, List<Fixture> fixtures, bool collapsible = true)
        {
            // Create a fixtures sheet generator with settings from the league
            var fixturesSheetSettings = _league.FixturesSheetSettings ?? new FixturesSheetSettings
            {
                LeagueName = _settings.LeagueName,
                SeasonName = season.Name
            };
            
            // Update league/season name from website settings
            fixturesSheetSettings.LeagueName = _settings.LeagueName;
            fixturesSheetSettings.SeasonName = season.Name;
            
            var fixturesSheetGenerator = new FixturesSheetGenerator(_league, fixturesSheetSettings);
            
            // Get embeddable content, CSS, and tilt script
            var sheetContent = fixturesSheetGenerator.GenerateEmbeddableContent(season.Id);
            var sheetCss = fixturesSheetGenerator.GetEmbeddableCSS();
            var scopedCss = fixturesSheetGenerator.GetScopedCSS();
            var sheetTiltScript = fixturesSheetGenerator.GetEmbeddableTiltScript();

            var expandedClass = _settings.FixturesSheetDefaultExpanded ? " expanded" : "";
            var sheetTitle = string.IsNullOrWhiteSpace(_settings.FixturesSheetTitle) 
                ? "Printable Fixtures Sheet" 
                : _settings.FixturesSheetTitle;

            html.AppendLine("            <!-- Printable Fixtures Sheet Section -->");
            html.AppendLine("            <style>");
            html.AppendLine("            .fixtures-sheet-section { position: relative; }");
            html.AppendLine("            .fixtures-sheet-section .fixtures-sheet-header { cursor: pointer; display: flex; justify-content: space-between; align-items: center; user-select: none; }");
            html.AppendLine("            .fixtures-sheet-section .fixtures-sheet-header h3 { margin-bottom: 0; }");
            html.AppendLine("            .fixtures-sheet-section .toggle-icon { font-size: 0.8rem; transition: transform 0.25s; color: var(--text-secondary, #64748B); }");
            html.AppendLine("            .fixtures-sheet-section:not(.expanded) .toggle-icon { transform: rotate(-90deg); }");
            html.AppendLine("            .fixtures-sheet-section .fixtures-sheet-content { max-height: 0; overflow: hidden; transition: max-height 0.4s ease-out, padding 0.3s ease-out; padding-top: 0; }");
            html.AppendLine("            .fixtures-sheet-section.expanded .fixtures-sheet-content { max-height: 5000px; padding-top: 24px; }");
            html.AppendLine("            .fixtures-sheet-actions { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; }");
            html.AppendLine("            .fixtures-sheet-actions button { display: inline-flex; align-items: center; gap: 8px; padding: 10px 20px; border: none; border-radius: 10px; font-size: 0.9rem; font-weight: 600; cursor: pointer; transition: all 0.15s; }");
            html.AppendLine("            .fixtures-sheet-actions .btn-download { background: var(--primary-color, #3B82F6); color: white; }");
            html.AppendLine("            .fixtures-sheet-actions .btn-download:hover { background: var(--secondary-color, #1D4ED8); transform: translateY(-2px); }");
            html.AppendLine("            .fixtures-sheet-actions .btn-print { background: var(--bg-alt, #F1F5F9); color: var(--text-color, #0F172A); border: 1px solid var(--border-color, #E2E8F0); }");
            html.AppendLine("            .fixtures-sheet-actions .btn-print:hover { background: var(--card-bg, white); border-color: var(--primary-color, #3B82F6); color: var(--primary-color, #3B82F6); }");
            html.AppendLine("            .fixtures-sheet-wrapper { overflow-x: auto; background: white; border: 1px solid var(--border-color, #E2E8F0); border-radius: 10px; padding: 16px; max-width: 100%; }");
            html.AppendLine("            .fixtures-sheet-wrapper .fixtures-sheet { transform-origin: top left; }");
            html.AppendLine("            @media (max-width: 768px) { .fixtures-sheet-actions { flex-direction: column; } .fixtures-sheet-actions button { width: 100%; justify-content: center; } }");
            // Include scoped sheet CSS so inline preview matches the designed sheet
            html.AppendLine(scopedCss);
            // Override the dark background from the standalone sheet — use transparent on website
            html.AppendLine("            .fixtures-sheet-wrapper { background: transparent !important; }");
            html.AppendLine("            </style>");

            if (collapsible)
            {
                html.AppendLine($"            <div class=\"section fixtures-sheet-section{expandedClass}\">");
                html.AppendLine("                <div class=\"fixtures-sheet-header\" onclick=\"toggleFixturesSheet()\">");
                html.AppendLine($"                    <h3>&#128197; {sheetTitle}</h3>");
                html.AppendLine("                    <div class=\"fixtures-sheet-controls\">");
                html.AppendLine("                        <span class=\"toggle-icon\">&#9660;</span>");
                html.AppendLine("                    </div>");
                html.AppendLine("                </div>");
                html.AppendLine("                <div class=\"fixtures-sheet-content\">");
            }
            else
            {
                html.AppendLine("            <div class=\"section fixtures-sheet-section expanded\">");
            }

            html.AppendLine("                    <div class=\"fixtures-sheet-actions\">");
            html.AppendLine("                        <button class=\"btn-download\" onclick=\"downloadFixturesSheet()\">&#128229; Download HTML</button>");
            html.AppendLine("                        <button class=\"btn-print\" onclick=\"printFixturesSheet()\">&#128424; Print</button>");
            html.AppendLine("                    </div>");
            html.AppendLine("                    <div class=\"fixtures-sheet-wrapper\" id=\"fixtures-sheet-container\">");
            html.AppendLine(sheetContent);
            html.AppendLine("                    </div>");

            if (collapsible)
            {
                html.AppendLine("                </div>");
            }
            html.AppendLine("            </div>");
            
            // Add JavaScript for toggle, print and download functionality
            var escapedCss = EscapeJsString(sheetCss);
            html.AppendLine("            <script>");
            html.AppendLine("            function toggleFixturesSheet() {");
            html.AppendLine("                const section = document.querySelector('.fixtures-sheet-section');");
            html.AppendLine("                section.classList.toggle('expanded');");
            html.AppendLine("            }");
            html.AppendLine("            ");
            html.AppendLine("            function printFixturesSheet() {");
            html.AppendLine("                const content = document.getElementById('fixtures-sheet-container').innerHTML;");
            html.AppendLine("                const printWindow = window.open('', '_blank');");
            html.AppendLine("                printWindow.document.write('<html><head><title>Fixtures Sheet</title>');");
            html.AppendLine($"                printWindow.document.write('<style>{escapedCss}</style>');");
            html.AppendLine("                printWindow.document.write('</head><body>');");
            html.AppendLine("                printWindow.document.write(content);");
            html.AppendLine("                printWindow.document.write('</body></html>');");
            html.AppendLine("                printWindow.document.close();");
            html.AppendLine("                printWindow.focus();");
            html.AppendLine("                setTimeout(() => { printWindow.print(); printWindow.close(); }, 250);");
            html.AppendLine("            }");
            html.AppendLine("            ");
            html.AppendLine("            function downloadFixturesSheet() {");
            html.AppendLine("                const content = document.getElementById('fixtures-sheet-container').innerHTML;");
            html.AppendLine($"                const css = `{escapedCss}`;");
            html.AppendLine("                const fullHtml = `<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><title>Fixtures Sheet</title><style>${css}</style></head><body>${content}</body></html>`;");
            html.AppendLine("                const blob = new Blob([fullHtml], { type: 'text/html' });");
            html.AppendLine("                const url = URL.createObjectURL(blob);");
            html.AppendLine("                const a = document.createElement('a');");
            html.AppendLine("                a.href = url;");
            html.AppendLine("                a.download = 'fixtures-sheet.html';");
            html.AppendLine("                document.body.appendChild(a);");
            html.AppendLine("                a.click();");
            html.AppendLine("                document.body.removeChild(a);");
            html.AppendLine("                URL.revokeObjectURL(url);");
            html.AppendLine("            }");
            html.AppendLine("            </script>");

            // Include card/logo tilt script from fixtures sheet
            if (!string.IsNullOrEmpty(sheetTiltScript))
                html.AppendLine(sheetTiltScript);
        }

        private void AppendTeamCalendarSection(StringBuilder html, Season season, List<Team> teams, List<Fixture> fixtures, List<Venue> venues)
        {
            var leagueName = EscapeJsString(_settings.LeagueName);

            // Build fixture data as JSON for client-side filtering
            var allFixtures = fixtures
                .Where(f => !f.Frames.Any(fr => fr.Winner != FrameWinner.None))
                .OrderBy(f => f.Date)
                .ToList();

            var teamsOrdered = teams.OrderBy(t => t.Name).ToList();

            var fixtureJsonItems = new List<string>();
            foreach (var f in allFixtures)
            {
                var homeName = EscapeJsString(teams.FirstOrDefault(t => t.Id == f.HomeTeamId)?.Name ?? "Home");
                var awayName = EscapeJsString(teams.FirstOrDefault(t => t.Id == f.AwayTeamId)?.Name ?? "Away");
                var venueName = f.VenueId.HasValue
                    ? EscapeJsString(venues.FirstOrDefault(v => v.Id == f.VenueId.Value)?.Name ?? "")
                    : "";
                var dateIso = f.Date.ToString("yyyy-MM-ddTHH:mm:ss");
                fixtureJsonItems.Add(
                    $"{{\"homeId\":\"{f.HomeTeamId}\",\"awayId\":\"{f.AwayTeamId}\",\"home\":\"{homeName}\",\"away\":\"{awayName}\",\"date\":\"{dateIso}\",\"venue\":\"{venueName}\"}}");
            }
            var fixturesJson = "[" + string.Join(",", fixtureJsonItems) + "]";

            var teamJsonItems = new List<string>();
            foreach (var t in teamsOrdered)
            {
                teamJsonItems.Add($"{{\"id\":\"{t.Id}\",\"name\":\"{EscapeJsString(t.Name)}\"}}");
            }
            var teamsJson = "[" + string.Join(",", teamJsonItems) + "]";

            html.AppendLine("            <!-- Team Calendar Download Section -->");
            html.AppendLine("            <style>");
            html.AppendLine("            .calendar-section { margin-top: 32px; }");
            html.AppendLine("            .calendar-section h3 { margin-bottom: 16px; }");
            html.AppendLine("            .calendar-controls { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; margin-bottom: 16px; }");
            html.AppendLine("            .calendar-controls select { padding: 10px 16px; border-radius: 10px; border: 1px solid var(--border-color, #E2E8F0); background: var(--card-bg, white); color: var(--text-color, #0F172A); font-size: 0.95rem; min-width: 220px; cursor: pointer; }");
            html.AppendLine("            .calendar-controls select:focus { outline: none; border-color: var(--primary-color, #3B82F6); box-shadow: 0 0 0 3px rgba(59,130,246,0.15); }");
            html.AppendLine("            .calendar-buttons { display: flex; gap: 10px; flex-wrap: wrap; }");
            html.AppendLine("            .calendar-buttons button { display: inline-flex; align-items: center; gap: 8px; padding: 10px 20px; border: none; border-radius: 10px; font-size: 0.9rem; font-weight: 600; cursor: pointer; transition: all 0.15s; }");
            html.AppendLine("            .calendar-buttons .btn-ical { background: var(--primary-color, #3B82F6); color: white; }");
            html.AppendLine("            .calendar-buttons .btn-ical:hover { filter: brightness(1.1); transform: translateY(-2px); }");
            html.AppendLine("            .calendar-buttons .btn-gcal { background: var(--bg-alt, #F1F5F9); color: var(--text-color, #0F172A); border: 1px solid var(--border-color, #E2E8F0); }");
            html.AppendLine("            .calendar-buttons .btn-gcal:hover { background: var(--card-bg, white); border-color: var(--primary-color, #3B82F6); color: var(--primary-color, #3B82F6); }");
            html.AppendLine("            .calendar-buttons button:disabled { opacity: 0.5; cursor: not-allowed; transform: none; }");
            html.AppendLine("            .calendar-fixture-count { font-size: 0.85rem; color: var(--text-secondary, #64748B); margin-top: 4px; }");
            html.AppendLine("            @media (max-width: 768px) { .calendar-controls { flex-direction: column; } .calendar-controls select { width: 100%; } .calendar-buttons { flex-direction: column; width: 100%; } .calendar-buttons button { width: 100%; justify-content: center; } }");
            html.AppendLine("            </style>");

            html.AppendLine("            <div class=\"section calendar-section\">");
            html.AppendLine("                <h3>&#128197; Team Fixture Calendar</h3>");
            html.AppendLine("                <p style=\"color: var(--text-secondary, #64748B); margin-bottom: 16px; font-size: 0.95rem;\">Select a team to download their fixtures as a calendar file or add them to Google Calendar.</p>");
            html.AppendLine("                <div class=\"calendar-controls\">");
            html.AppendLine("                    <select id=\"calTeamSelect\" onchange=\"onCalTeamChanged()\">");
            html.AppendLine("                        <option value=\"\">-- Select a team --</option>");

            foreach (var t in teamsOrdered)
            {
                html.AppendLine($"                        <option value=\"{t.Id}\">{System.Net.WebUtility.HtmlEncode(t.Name)}</option>");
            }

            html.AppendLine("                    </select>");
            html.AppendLine("                    <div class=\"calendar-buttons\">");
            html.AppendLine("                        <button class=\"btn-ical\" id=\"btnIcal\" onclick=\"downloadIcal()\" disabled>&#128229; Download iCal (.ics)</button>");
            html.AppendLine("                        <button class=\"btn-gcal\" id=\"btnGcal\" onclick=\"openGoogleCalendar()\" disabled>&#128197; Add to Google Calendar</button>");
            html.AppendLine("                    </div>");
            html.AppendLine("                </div>");
            html.AppendLine("                <div id=\"calFixtureCount\" class=\"calendar-fixture-count\"></div>");
            html.AppendLine("            </div>");

            // JavaScript for calendar generation
            html.AppendLine("            <script>");
            html.AppendLine($"            var _calFixtures = {fixturesJson};");
            html.AppendLine($"            var _calTeams = {teamsJson};");
            html.AppendLine($"            var _calLeagueName = '{leagueName}';");
            html.AppendLine($"            var _calSeasonName = '{EscapeJsString(season.Name)}';");
            html.AppendLine("            ");
            html.AppendLine("            function onCalTeamChanged() {");
            html.AppendLine("                var sel = document.getElementById('calTeamSelect').value;");
            html.AppendLine("                var hasTeam = sel !== '';");
            html.AppendLine("                document.getElementById('btnIcal').disabled = !hasTeam;");
            html.AppendLine("                document.getElementById('btnGcal').disabled = !hasTeam;");
            html.AppendLine("                var ct = document.getElementById('calFixtureCount');");
            html.AppendLine("                if (hasTeam) {");
            html.AppendLine("                    var fxs = getTeamFixtures(sel);");
            html.AppendLine("                    var tn = _calTeams.find(function(t){return t.id===sel;});");
            html.AppendLine("                    ct.textContent = fxs.length + ' upcoming fixture' + (fxs.length!==1?'s':'') + ' for ' + (tn?tn.name:'this team');");
            html.AppendLine("                } else { ct.textContent = ''; }");
            html.AppendLine("            }");
            html.AppendLine("            ");
            html.AppendLine("            function getTeamFixtures(teamId) {");
            html.AppendLine("                return _calFixtures.filter(function(f){ return f.homeId===teamId || f.awayId===teamId; });");
            html.AppendLine("            }");
            html.AppendLine("            ");
            html.AppendLine("            function pad2(n) { return n < 10 ? '0'+n : ''+n; }");
            html.AppendLine("            ");
            html.AppendLine("            function toIcsDate(d) {");
            html.AppendLine("                return d.getFullYear() + pad2(d.getMonth()+1) + pad2(d.getDate()) + 'T' + pad2(d.getHours()) + pad2(d.getMinutes()) + '00';");
            html.AppendLine("            }");
            html.AppendLine("            ");
            html.AppendLine("            function toGcalDate(d) {");
            html.AppendLine("                return d.getFullYear() + pad2(d.getMonth()+1) + pad2(d.getDate()) + 'T' + pad2(d.getHours()) + pad2(d.getMinutes()) + '00';");
            html.AppendLine("            }");
            html.AppendLine("            ");
            html.AppendLine("            function downloadIcal() {");
            html.AppendLine("                var teamId = document.getElementById('calTeamSelect').value;");
            html.AppendLine("                if (!teamId) return;");
            html.AppendLine("                var fxs = getTeamFixtures(teamId);");
            html.AppendLine("                if (fxs.length === 0) { alert('No upcoming fixtures for this team.'); return; }");
            html.AppendLine("                var tn = _calTeams.find(function(t){return t.id===teamId;});");
            html.AppendLine("                var teamName = tn ? tn.name : 'Team';");
            html.AppendLine("                var lines = ['BEGIN:VCALENDAR','VERSION:2.0','PRODID:-//'+_calLeagueName+'//Fixtures//EN','CALSCALE:GREGORIAN','METHOD:PUBLISH','X-WR-CALNAME:'+teamName+' - '+_calSeasonName];");
            html.AppendLine("                fxs.forEach(function(f) {");
            html.AppendLine("                    var d = new Date(f.date);");
            html.AppendLine("                    var end = new Date(d.getTime() + 3*60*60*1000);");
            html.AppendLine("                    var title = f.home + ' vs ' + f.away;");
            html.AppendLine("                    var uid = f.homeId + '-' + f.awayId + '-' + toIcsDate(d) + '@' + _calLeagueName.replace(/\\s+/g,'');");
            html.AppendLine("                    lines.push('BEGIN:VEVENT');");
            html.AppendLine("                    lines.push('DTSTART:' + toIcsDate(d));");
            html.AppendLine("                    lines.push('DTEND:' + toIcsDate(end));");
            html.AppendLine("                    lines.push('SUMMARY:' + title);");
            html.AppendLine("                    if (f.venue) lines.push('LOCATION:' + f.venue);");
            html.AppendLine("                    lines.push('DESCRIPTION:' + _calLeagueName + ' - ' + _calSeasonName);");
            html.AppendLine("                    lines.push('UID:' + uid);");
            html.AppendLine("                    lines.push('END:VEVENT');");
            html.AppendLine("                });");
            html.AppendLine("                lines.push('END:VCALENDAR');");
            html.AppendLine("                var blob = new Blob([lines.join('\\r\\n')], {type:'text/calendar;charset=utf-8'});");
            html.AppendLine("                var url = URL.createObjectURL(blob);");
            html.AppendLine("                var a = document.createElement('a');");
            html.AppendLine("                a.href = url;");
            html.AppendLine("                a.download = teamName.replace(/[^a-zA-Z0-9]/g,'_') + '_fixtures.ics';");
            html.AppendLine("                document.body.appendChild(a);");
            html.AppendLine("                a.click();");
            html.AppendLine("                document.body.removeChild(a);");
            html.AppendLine("                URL.revokeObjectURL(url);");
            html.AppendLine("            }");
            html.AppendLine("            ");
            html.AppendLine("            function openGoogleCalendar() {");
            html.AppendLine("                var teamId = document.getElementById('calTeamSelect').value;");
            html.AppendLine("                if (!teamId) return;");
            html.AppendLine("                var fxs = getTeamFixtures(teamId);");
            html.AppendLine("                if (fxs.length === 0) { alert('No upcoming fixtures for this team.'); return; }");
            html.AppendLine("                if (fxs.length > 1) {");
            html.AppendLine("                    alert('Google Calendar links work one event at a time. The first fixture will be opened — for all fixtures use the iCal download instead.');");
            html.AppendLine("                }");
            html.AppendLine("                var f = fxs[0];");
            html.AppendLine("                var d = new Date(f.date);");
            html.AppendLine("                var end = new Date(d.getTime() + 3*60*60*1000);");
            html.AppendLine("                var title = encodeURIComponent(f.home + ' vs ' + f.away);");
            html.AppendLine("                var details = encodeURIComponent(_calLeagueName + ' - ' + _calSeasonName);");
            html.AppendLine("                var location = encodeURIComponent(f.venue || '');");
            html.AppendLine("                var dates = toGcalDate(d) + '/' + toGcalDate(end);");
            html.AppendLine("                var url = 'https://calendar.google.com/calendar/render?action=TEMPLATE&text=' + title + '&dates=' + dates + '&details=' + details + '&location=' + location;");
            html.AppendLine("                window.open(url, '_blank');");
            html.AppendLine("            }");
            html.AppendLine("            </script>");
        }

        private static string StandingsDivisionSlug(Division d)
        {
            var s = (d.Name ?? "division").ToLowerInvariant();
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (ch == ' ' || ch == '-' || ch == '_') sb.Append('-');
            }
            var slug = sb.ToString().Trim('-');
            return string.IsNullOrEmpty(slug) ? d.Id.ToString("N") : slug;
        }

        private static string EscapeJsString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("`", "\\`");
        }

        private static string Esc(string? input) =>
            System.Net.WebUtility.HtmlEncode(input ?? "");

        private string GenerateResultsPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            
            AppendDocumentHead(html, $"{_settings.ResultsPageTitle} - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Results");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>&#127937; {Esc(_settings.ResultsPageTitle)}</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">Latest Results</p>");
            html.AppendLine("            </div>");

            if (_settings.ResultsShowFrameDetails)
            {
                html.AppendLine("            <p class=\"view-all\"><a href=\"scorecards.html\">&#128203; View Full Season Scorecards &#8594;</a></p>");
            }

            var completedFixtures = fixtures
                .Where(f => f.Frames.Any(fr => fr.Winner != FrameWinner.None))
                .OrderByDescending(f => f.Date)
                .Take(_settings.ResultsPerPage);
            
            var groupedResults = _settings.ResultsGroupByWeek
                ? completedFixtures.GroupBy(f => GetWeekStart(f.Date)).ToList()
                : (_settings.ResultsGroupByDate
                    ? completedFixtures.GroupBy(f => f.Date.Date).ToList()
                    : new List<IGrouping<DateTime, Fixture>> { new SingleGrouping<DateTime, Fixture>(DateTime.Today, completedFixtures.ToList()) });
            
            if (groupedResults.Any(g => g.Any()))
            {
                foreach (var dateGroup in groupedResults)
                {
                    if (!dateGroup.Any()) continue;
                    
                    html.AppendLine("            <div class=\"section\">");
                    
                    if (_settings.ResultsGroupByWeek)
                        html.AppendLine($"                <h3>Week of {dateGroup.Key:dd MMMM yyyy}</h3>");
                    else if (_settings.ResultsGroupByDate)
                        html.AppendLine($"                <h3>{dateGroup.Key:dddd, dd MMMM yyyy}</h3>");
                    
                    html.AppendLine("                <div class=\"results-list\">");
                    
                    foreach (var fixture in dateGroup.OrderByDescending(f => f.Date))
                    {
                        var homeTeam = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
                        var awayTeam = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId);
                        var venue = fixture.VenueId.HasValue ? venues.FirstOrDefault(v => v.Id == fixture.VenueId.Value) : null;
                        var division = fixture.DivisionId.HasValue ? divisions.FirstOrDefault(d => d.Id == fixture.DivisionId.Value) : null;
                        
                        var isHomeWin = fixture.HomeScore > fixture.AwayScore;
                        var isDraw = fixture.HomeScore == fixture.AwayScore;
                        
                        html.AppendLine("                    <div class=\"result-item\">");
                        
                        if (_settings.ResultsShowDate || _settings.ResultsShowTime)
                        {
                            var dateStr = "";
                            if (_settings.ResultsShowDate && !_settings.ResultsGroupByDate)
                                dateStr = fixture.Date.ToString(_settings.ResultsDateFormat);
                            if (_settings.ResultsShowTime)
                                dateStr += (dateStr.Length > 0 ? " " : "") + fixture.Date.ToString("HH:mm");
                            if (!string.IsNullOrEmpty(dateStr))
                                html.AppendLine($"                        <span class=\"date\">{dateStr.Trim()}</span>");
                        }
                        
                        html.AppendLine($"                        <span class=\"team{(_settings.ResultsHighlightWinner && isHomeWin ? " winner" : "")}\">{homeTeam?.Name ?? "Home"}</span>");
                        
                        if (_settings.ResultsShowScore)
                            html.AppendLine($"                        <span class=\"score\">{fixture.HomeScore} - {fixture.AwayScore}</span>");
                        
                        html.AppendLine($"                        <span class=\"team{(_settings.ResultsHighlightWinner && !isHomeWin && !isDraw ? " winner" : "")}\">{awayTeam?.Name ?? "Away"}</span>");
                        
                        if (_settings.ResultsShowVenue && venue != null)
                            html.AppendLine($"                        <span class=\"venue\">{venue.Name}</span>");
                        
                        if (_settings.ResultsShowDivision && division != null)
                            html.AppendLine($"                        <span class=\"division-badge\">{division.Name}</span>");
                        
                        html.AppendLine("                    </div>");
                    }
                    
                    html.AppendLine("                </div>");
                    html.AppendLine("            </div>");
                }
            }
            else
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <p class=\"empty-message\">No results available yet.</p>");
                html.AppendLine("            </div>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private string GenerateScorecardsPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            var playerById = players.ToDictionary(p => p.Id, p => p);

            AppendDocumentHead(html, $"Scorecards - {_settings.LeagueName}", season);
            html.AppendLine("<body>");

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);

            AppendHeader(html, season);
            AppendNavigation(html, "Results");

            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>&#128203; Season Scorecards</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">Frame-by-frame results for every completed match</p>");
            html.AppendLine("            </div>");

            html.AppendLine("            <div class=\"section\" style=\"text-align: center;\">");
            html.AppendLine("                <a href=\"results.html\" class=\"back-link\">&#8592; Back to Results</a>");
            html.AppendLine("            </div>");

            var completedFixtures = fixtures
                .Where(f => f.Frames.Any(fr => fr.Winner != FrameWinner.None))
                .OrderByDescending(f => f.Date)
                .ToList();

            var groupedScorecards = _settings.ResultsGroupByWeek
                ? completedFixtures.GroupBy(f => GetWeekStart(f.Date)).ToList()
                : (_settings.ResultsGroupByDate
                    ? completedFixtures.GroupBy(f => f.Date.Date).ToList()
                    : new List<IGrouping<DateTime, Fixture>> { new SingleGrouping<DateTime, Fixture>(DateTime.Today, completedFixtures) });

            if (completedFixtures.Count != 0)
            {
                foreach (var dateGroup in groupedScorecards)
                {
                    if (!dateGroup.Any()) continue;

                    html.AppendLine("            <div class=\"section\">");

                    if (_settings.ResultsGroupByWeek)
                        html.AppendLine($"                <h3>Week of {dateGroup.Key:dd MMMM yyyy}</h3>");
                    else if (_settings.ResultsGroupByDate)
                        html.AppendLine($"                <h3>{dateGroup.Key:dddd, dd MMMM yyyy}</h3>");

                    foreach (var fixture in dateGroup.OrderByDescending(f => f.Date))
                    {
                        var homeTeam = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
                        var awayTeam = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId);
                        var division = fixture.DivisionId.HasValue ? divisions.FirstOrDefault(d => d.Id == fixture.DivisionId.Value) : null;

                        var homeName = Esc(homeTeam?.Name ?? "Home");
                        var awayName = Esc(awayTeam?.Name ?? "Away");

                        html.AppendLine("                <details class=\"scorecard\">");
                        html.AppendLine("                    <summary class=\"scorecard-summary\">");
                        html.AppendLine($"                        <span class=\"scorecard-date\">{fixture.Date.ToString(_settings.ResultsDateFormat)}</span>");
                        html.AppendLine($"                        <span class=\"scorecard-teams\">{homeName} <strong>{fixture.HomeScore} - {fixture.AwayScore}</strong> {awayName}</span>");
                        if (division != null)
                            html.AppendLine($"                        <span class=\"division-badge\">{Esc(division.Name)}</span>");
                        html.AppendLine("                    </summary>");

                        html.AppendLine("                    <div class=\"table-responsive\">");
                        html.AppendLine($"                    <table class=\"{GetTableClasses()} scorecard-table\">");
                        html.AppendLine("                        <thead>");
                        html.AppendLine("                            <tr>");
                        html.AppendLine("                                <th>#</th>");
                        html.AppendLine($"                                <th>{homeName}</th>");
                        html.AppendLine("                                <th>Result</th>");
                        html.AppendLine($"                                <th>{awayName}</th>");
                        html.AppendLine("                            </tr>");
                        html.AppendLine("                        </thead>");
                        html.AppendLine("                        <tbody>");

                        foreach (var frame in fixture.Frames.OrderBy(fr => fr.Number))
                        {
                            var homePlayer = FormatFramePlayer(frame.HomePlayerId, frame.HomePlayer2Id, playerById);
                            var awayPlayer = FormatFramePlayer(frame.AwayPlayerId, frame.AwayPlayer2Id, playerById);
                            var homeWon = frame.Winner == FrameWinner.Home;
                            var awayWon = frame.Winner == FrameWinner.Away;
                            var eight = frame.EightBall ? " &#127921;" : "";

                            html.AppendLine("                            <tr>");
                            html.AppendLine($"                                <td>{frame.Number}</td>");
                            html.AppendLine($"                                <td class=\"{(homeWon ? "frame-winner" : "")}\">{homePlayer}{(homeWon ? eight : "")}</td>");
                            html.AppendLine($"                                <td class=\"frame-result\">{(homeWon ? "&#9664;" : awayWon ? "&#9654;" : "&#8211;")}</td>");
                            html.AppendLine($"                                <td class=\"{(awayWon ? "frame-winner" : "")}\">{awayPlayer}{(awayWon ? eight : "")}</td>");
                            html.AppendLine("                            </tr>");
                        }

                        html.AppendLine("                        </tbody>");
                        html.AppendLine("                    </table>");
                        html.AppendLine("                    </div>");
                        html.AppendLine("                </details>");
                    }

                    html.AppendLine("            </div>");
                }
            }
            else
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <p class=\"empty-message\">No scorecards available yet.</p>");
                html.AppendLine("            </div>");
            }

            html.AppendLine("        </div>");
            html.AppendLine("    </div>");

            AppendFooter(html);

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private static string FormatFramePlayer(Guid? playerId, Guid? player2Id, Dictionary<Guid, Player> playerById)
        {
            string NameFor(Guid? id)
            {
                if (!id.HasValue) return "";
                if (FrameResult.IsVoidPlayer(id)) return "Walkover";
                return playerById.TryGetValue(id.Value, out var p)
                    ? Esc(p.Name)
                    : "Unknown";
            }

            var first = NameFor(playerId);
            var second = NameFor(player2Id);

            if (!string.IsNullOrEmpty(second))
                return string.IsNullOrEmpty(first) ? second : $"{first} &amp; {second}";

            return string.IsNullOrEmpty(first) ? "&#8211;" : first;
        }

        private string GeneratePlayersPage(Season season, WebsiteTemplate template, Division? singleDivision = null)
        {
            var html = new StringBuilder();
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            var appSettings = _leagueSettings;

            var pageTitle = _settings.PlayersPageTitle;
            var docTitle = singleDivision != null
                ? $"{singleDivision.Name} - {pageTitle} - {_settings.LeagueName}"
                : $"{pageTitle} - {_settings.LeagueName}";
            AppendDocumentHead(html, docTitle, season);
            html.AppendLine("<body>");

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);

            AppendHeader(html, season);
            AppendNavigation(html, "Players");

            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>&#127942; {Esc(pageTitle)}</h2>");
            // Calculate all player stats (across all divisions, ratings need cross-division data)
            var allPlayerStats = CalculatePlayerStats(players, teams, fixtures);

            var heroSub = singleDivision != null
                ? $"{Esc(singleDivision.Name)} &middot; {allPlayerStats.Count(p => p.DivisionId == singleDivision.Id)} Players"
                : $"{players.Count} Players";
            html.AppendLine($"                <p class=\"hero-dates\">{heroSub}</p>");
            html.AppendLine("            </div>");

            // Generate one table per division — mirrors the app's LeagueTablesPage exactly
            var orderedDivisions = singleDivision != null
                ? new List<Division> { singleDivision }
                : divisions.OrderBy(d => d.Name).ToList();

            if (orderedDivisions.Count == 0)
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <p class=\"empty-message\">No divisions found.</p>");
                html.AppendLine("            </div>");
            }

            foreach (var division in orderedDivisions)
            {
                // Filter to players in this division only
                var divisionStats = allPlayerStats
                    .Where(p => p.DivisionId == division.Id)
                    .ToList();

                if (divisionStats.Count == 0) continue;

                // Apply minimum frames filter using the app's settings (same as LeagueTablesPage)
                int maxFramesInDivision = divisionStats.Max(p => p.Played);
                int minFramesRequired = appSettings.CalculateMinimumFrames(maxFramesInDivision);

                var filteredStats = divisionStats
                    .Where(p => p.Played >= minFramesRequired)
                    .ToList();

                // Sort — use website sort setting but default to rating (matching app default)
                filteredStats = _settings.PlayersSortBy switch
                {
                    "won" => filteredStats.OrderByDescending(s => s.Won).ThenByDescending(s => s.WinPercentage).ToList(),
                    "played" => filteredStats.OrderByDescending(s => s.Played).ThenByDescending(s => s.WinPercentage).ToList(),
                    "eightballs" => filteredStats.OrderByDescending(s => s.EightBalls).ThenByDescending(s => s.WinPercentage).ToList(),
                    "winpct" => filteredStats.OrderByDescending(s => s.WinPercentage).ThenByDescending(s => s.Won).ToList(),
                    _ => filteredStats.OrderByDescending(s => s.Rating).ThenByDescending(s => s.WinPercentage).ToList()
                };

                if (_settings.PlayersPerPage > 0)
                    filteredStats = filteredStats.Take(_settings.PlayersPerPage).ToList();

                html.AppendLine("            <div class=\"section\">");
                html.AppendLine($"                <h3>{division.Name ?? "Division"}</h3>");

                if (filteredStats.Count != 0)
                {
                    html.AppendLine("                <div class=\"table-responsive\">");
                    html.AppendLine($"                <table class=\"{GetTableClasses()}\">");
                    html.AppendLine("                    <thead>");
                    html.AppendLine("                        <tr>");
                    if (_settings.PlayersShowPosition) html.AppendLine("                            <th>Pos</th>");
                    html.AppendLine("                            <th>Player</th>");
                    if (_settings.PlayersShowTeam) html.AppendLine("                            <th>Team</th>");
                    if (_settings.PlayersShowPlayed) html.AppendLine("                            <th>Played</th>");
                    if (_settings.PlayersShowWon) html.AppendLine("                            <th>Won</th>");
                    if (_settings.PlayersShowLost) html.AppendLine("                            <th>Lost</th>");
                    if (_settings.PlayersShowWinPercentage) html.AppendLine("                            <th>Win %</th>");
                    if (_settings.PlayersShowEightBalls) html.AppendLine("                            <th>8-Balls</th>");
                    if (_settings.PlayersShowRating) html.AppendLine("                            <th>Rating</th>");
                    html.AppendLine("                            <th>Form</th>");
                    html.AppendLine("                        </tr>");
                    html.AppendLine("                    </thead>");
                    html.AppendLine("                    <tbody>");

                    var position = 1;
                    for (var idx = 0; idx < filteredStats.Count; idx++)
                    {
                        var stat = filteredStats[idx];
                        if (idx > 0 && filteredStats[idx - 1].Rating == stat.Rating)
                            position = position; // joint
                        else
                            position = idx + 1;

                        html.AppendLine("                        <tr>");
                        if (_settings.PlayersShowPosition)
                        {
                            var isJoint = (idx > 0 && filteredStats[idx - 1].Rating == stat.Rating)
                                       || (idx < filteredStats.Count - 1 && filteredStats[idx + 1].Rating == stat.Rating);
                            var posDisplay = isJoint ? $"{position}=" : position.ToString();
                            html.AppendLine($"                            <td>{posDisplay}</td>");
                        }
                        html.AppendLine($"                            <td><strong><a href=\"player.html?id={stat.PlayerId:N}\" class=\"player-link\">{stat.PlayerName}</a></strong></td>");
                        if (_settings.PlayersShowTeam) html.AppendLine($"                            <td>{stat.TeamName}</td>");
                        if (_settings.PlayersShowPlayed) html.AppendLine($"                            <td>{stat.Played}</td>");
                        if (_settings.PlayersShowWon) html.AppendLine($"                            <td>{stat.Won}</td>");
                        if (_settings.PlayersShowLost) html.AppendLine($"                            <td>{stat.Lost}</td>");
                        if (_settings.PlayersShowWinPercentage) html.AppendLine($"                            <td><strong>{stat.WinPercentage:F1}%</strong></td>");
                        if (_settings.PlayersShowEightBalls) html.AppendLine($"                            <td>{stat.EightBalls}</td>");
                        if (_settings.PlayersShowRating) html.AppendLine($"                            <td>{stat.Rating}</td>");
                        var form = GetLeaguePlayerForm(stat.PlayerId, fixtures, 5);
                        var formHtml = string.Concat(form.Select(c => c == 'W' ? "<span style='color:#10B981;'>&#9679;</span>" : "<span style='color:#EF4444;'>&#9679;</span>"));
                        html.AppendLine($"                            <td>{formHtml}</td>");
                        html.AppendLine("                        </tr>");
                    }

                    html.AppendLine("                    </tbody>");
                    html.AppendLine("                </table>");
                    html.AppendLine("                </div>");

                    if (minFramesRequired > 0)
                        html.AppendLine($"                <p class=\"table-note\">* Minimum {appSettings.MinFramesPercentage}% of available frames required to qualify ({minFramesRequired} frames)</p>");
                }
                else
                {
                    html.AppendLine("                <p class=\"empty-message\">No players qualify yet.</p>");
                }

                html.AppendLine("            </div>");
            }

            html.AppendLine("        </div>");
            html.AppendLine("    </div>");

            AppendFooter(html);

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }
        
        private string GenerateDivisionsPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            
            AppendDocumentHead(html, $"{_settings.DivisionsPageTitle} - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Divisions");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>&#127941; {Esc(_settings.DivisionsPageTitle)}</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{divisions.Count} Division(s)</p>");
            html.AppendLine("            </div>");
            
            var layoutClass = _settings.DivisionsLayout switch
            {
                "grid" => "divisions-grid",
                "list" => "divisions-list",
                _ => "divisions-cards"
            };
            
            html.AppendLine($"            <div class=\"{layoutClass}\">");
            
            foreach (var division in divisions.OrderBy(d => d.Name))
            {
                var divisionTeams = teams.Where(t => t.DivisionId == division.Id).ToList();
                var divisionPlayers = players.Where(p => divisionTeams.Any(t => t.Id == p.TeamId)).ToList();
                
                html.AppendLine("            <div class=\"section division-card\">");
                html.AppendLine($"                <h3>{division.Name}</h3>");
                
                if (_settings.DivisionsShowDescription && !string.IsNullOrWhiteSpace(division.Notes))
                {
                    html.AppendLine($"                <p class=\"division-notes\">{division.Notes}</p>");
                }
                
                if (_settings.DivisionsShowTeamCount || _settings.DivisionsShowPlayerCount)
                {
                    html.AppendLine("                <div class=\"stats-grid mini-stats\">");
                    if (_settings.DivisionsShowTeamCount)
                    {
                        html.AppendLine("                    <div class=\"stat-card\">");
                        html.AppendLine($"                        <div class=\"stat-number\">{divisionTeams.Count}</div>");
                        html.AppendLine("                        <div class=\"stat-label\">Teams</div>");
                        html.AppendLine("                    </div>");
                    }
                    if (_settings.DivisionsShowPlayerCount)
                    {
                        html.AppendLine("                    <div class=\"stat-card\">");
                        html.AppendLine($"                        <div class=\"stat-number\">{divisionPlayers.Count}</div>");
                        html.AppendLine("                        <div class=\"stat-label\">Players</div>");
                        html.AppendLine("                    </div>");
                    }
                    html.AppendLine("                </div>");
                }
                
                if (_settings.DivisionsShowMiniStandings && divisionTeams.Count != 0)
                {
                    var standings = StandingsCalculator.Calculate(divisionTeams, fixtures, _leagueSettings)
                        .OrderByDescending(s => s.Points)
                        .Take(5)
                        .ToList();
                    
                    if (standings.Count != 0)
                    {
                        html.AppendLine("                <h4>Current Standings</h4>");
                        html.AppendLine("                <div class=\"mini-standings\">");
                        var pos = 1;
                        foreach (var standing in standings)
                        {
                            html.AppendLine($"                    <div class=\"mini-standing-row\"><span class=\"pos\">{pos++}</span> <a href=\"team.html?id={standing.TeamId:N}\" class=\"team-link\"><span class=\"team-name\">{standing.TeamName}</span></a> <span class=\"pts\">{standing.Points} pts</span></div>");
                        }
                        html.AppendLine("                </div>");
                    }
                }
                
                if (_settings.DivisionsShowTeamList && divisionTeams.Count != 0)
                {
                    html.AppendLine("                <h4>Teams</h4>");
                    html.AppendLine("                <ul class=\"team-list\">");
                    foreach (var team in divisionTeams.OrderBy(t => t.Name))
                    {
                        var teamPlayers = players.Where(p => p.TeamId == team.Id).ToList();
                        html.AppendLine($"                    <li><strong><a href=\"team.html?id={team.Id:N}\" class=\"player-link\">{team.Name}</a></strong> <span class=\"player-count\">({teamPlayers.Count} players)</span></li>");
                    }
                    html.AppendLine("                </ul>");
                }
                
                html.AppendLine("            </div>");
            }
            
            html.AppendLine("            </div>");
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private string GenerateRulesPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();

            AppendDocumentHead(html, $"{_settings.RulesPageTitle} - {_settings.LeagueName}", season);
            html.AppendLine("<body>");

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);

            AppendHeader(html, season);
            AppendNavigation(html, "Rules");

            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>&#128214; {Esc(_settings.RulesPageTitle)}</h2>");
            html.AppendLine("            </div>");

            // Build the list of sections that have content
            var sections = new List<(string id, string label, string plainText)>();

            if (!string.IsNullOrWhiteSpace(_settings.ConstitutionContent))
                sections.Add(("constitution", "Constitution", _settings.ConstitutionContent));
            if (!string.IsNullOrWhiteSpace(_settings.MatchRulesContent))
                sections.Add(("match-rules", "League Match Rules", _settings.MatchRulesContent));
            if (!string.IsNullOrWhiteSpace(_settings.EpaRulesContent))
                sections.Add(("epa-rules", "EPA Rules", _settings.EpaRulesContent));

            if (sections.Count > 0)
            {
                // Parse all sections into headings for the table of contents
                var allParsed = sections.Select(s => (s.id, s.label, headings: ParseRulesHeadings(s.plainText))).ToList();

                // Search box (filters TOC + highlights matches across all rule tabs)
                html.AppendLine("            <div class=\"rules-search\">");
                html.AppendLine("                <input type=\"search\" id=\"rules-search-input\" class=\"rules-search-input\" placeholder=\"Search rules...\" autocomplete=\"off\" />");
                html.AppendLine("                <span class=\"rules-search-summary\" id=\"rules-search-summary\"></span>");
                html.AppendLine("            </div>");

                // Tab buttons
                html.AppendLine("            <div class=\"rules-tabs\">");
                html.AppendLine("                <div class=\"rules-tab-buttons\">");
                for (int i = 0; i < sections.Count; i++)
                {
                    var active = i == 0 ? " active" : "";
                    html.AppendLine($"                    <button class=\"rules-tab-btn{active}\" onclick=\"openRulesTab(event, '{sections[i].id}')\">{sections[i].label}</button>");
                }
                html.AppendLine("                </div>");

                // Each section: sidebar TOC + content
                for (int i = 0; i < sections.Count; i++)
                {
                    var display = i == 0 ? "block" : "none";
                    var (id, label, headings) = allParsed[i];
                    html.AppendLine($"                <div id=\"{id}\" class=\"rules-tab-content\" style=\"display:{display}\">");
                    html.AppendLine("                    <div class=\"rules-layout\">");

                    // Table of contents sidebar
                    if (headings.Count > 0)
                    {
                        html.AppendLine("                        <nav class=\"rules-toc\">");
                        html.AppendLine("                            <h4>Contents</h4>");
                        html.AppendLine("                            <ul>");
                        foreach (var (anchor, title) in headings)
                            html.AppendLine($"                                <li><a href=\"#{anchor}\">{System.Net.WebUtility.HtmlEncode(title)}</a></li>");
                        html.AppendLine("                            </ul>");
                        html.AppendLine("                        </nav>");
                    }

                    // Content body
                    html.AppendLine("                        <div class=\"rules-body section content-section\">");
                    var afterHeading = id == "epa-rules"
                        ? new Dictionary<string, string> { ["rule-the-rack"] = GenerateRackDiagramSvg() }
                        : null;
                    html.AppendLine(PlainTextToRulesHtml(sections[i].plainText, id, afterHeading));
                    html.AppendLine("                        </div>");

                    html.AppendLine("                    </div>");
                    html.AppendLine($"                </div>");
                }

                html.AppendLine("            </div>");

                // Tab switching + smooth scroll + active TOC highlight
                html.AppendLine("            <script>");
                html.AppendLine("            function openRulesTab(evt, tabId) {");
                html.AppendLine("                document.querySelectorAll('.rules-tab-content').forEach(c => c.style.display = 'none');");
                html.AppendLine("                document.querySelectorAll('.rules-tab-btn').forEach(b => b.classList.remove('active'));");
                html.AppendLine("                document.getElementById(tabId).style.display = 'block';");
                html.AppendLine("                evt.currentTarget.classList.add('active');");
                html.AppendLine("            }");
                html.AppendLine("            document.querySelectorAll('.rules-toc a').forEach(a => {");
                html.AppendLine("                a.addEventListener('click', function(e) {");
                html.AppendLine("                    e.preventDefault();");
                html.AppendLine("                    var target = document.querySelector(this.getAttribute('href'));");
                html.AppendLine("                    if (target) target.scrollIntoView({behavior:'smooth', block:'start'});");
                html.AppendLine("                });");
                html.AppendLine("            });");
                html.AppendLine();
                html.AppendLine("            (function() {");
                html.AppendLine("                var input = document.getElementById('rules-search-input');");
                html.AppendLine("                var summary = document.getElementById('rules-search-summary');");
                html.AppendLine("                if (!input) return;");
                html.AppendLine("                // Cache original HTML for each searchable block so we can rebuild on each keystroke");
                html.AppendLine("                var blocks = [];");
                html.AppendLine("                document.querySelectorAll('.rules-tab-content').forEach(function(tab) {");
                html.AppendLine("                    tab.querySelectorAll('.rules-body h3, .rules-body h4, .rules-body p, .rules-body li').forEach(function(el) {");
                html.AppendLine("                        blocks.push({ tabId: tab.id, el: el, original: el.innerHTML });");
                html.AppendLine("                    });");
                html.AppendLine("                });");
                html.AppendLine("                var tocItems = [];");
                html.AppendLine("                document.querySelectorAll('.rules-tab-content').forEach(function(tab) {");
                html.AppendLine("                    tab.querySelectorAll('.rules-toc li').forEach(function(li) {");
                html.AppendLine("                        tocItems.push({ tabId: tab.id, li: li, text: li.textContent.toLowerCase() });");
                html.AppendLine("                    });");
                html.AppendLine("                });");
                html.AppendLine("                function escapeRegex(s) { return s.replace(/[.*+?^${}()|[\\]\\\\]/g, '\\\\$&'); }");
                html.AppendLine("                function clearHighlights() {");
                html.AppendLine("                    blocks.forEach(function(b) { b.el.innerHTML = b.original; b.el.classList.remove('rules-search-hidden', 'rules-search-hit'); });");
                html.AppendLine("                    tocItems.forEach(function(t) { t.li.classList.remove('rules-search-hidden', 'rules-search-hit'); });");
                html.AppendLine("                    document.querySelectorAll('.rules-tab-btn').forEach(function(btn) { btn.classList.remove('rules-search-has-hits'); });");
                html.AppendLine("                    summary.textContent = '';");
                html.AppendLine("                }");
                html.AppendLine("                function applySearch(query) {");
                html.AppendLine("                    if (!query) { clearHighlights(); return; }");
                html.AppendLine("                    var q = query.toLowerCase();");
                html.AppendLine("                    var rx = new RegExp('(' + escapeRegex(query) + ')', 'gi');");
                html.AppendLine("                    var hitsByTab = {};");
                html.AppendLine("                    blocks.forEach(function(b) {");
                html.AppendLine("                        var text = b.original.replace(/<[^>]+>/g, '');");
                html.AppendLine("                        if (text.toLowerCase().indexOf(q) !== -1) {");
                html.AppendLine("                            b.el.innerHTML = b.original.replace(rx, '<mark class=\"rules-mark\">$1</mark>');");
                html.AppendLine("                            b.el.classList.add('rules-search-hit');");
                html.AppendLine("                            b.el.classList.remove('rules-search-hidden');");
                html.AppendLine("                            hitsByTab[b.tabId] = (hitsByTab[b.tabId] || 0) + 1;");
                html.AppendLine("                        } else {");
                html.AppendLine("                            b.el.innerHTML = b.original;");
                html.AppendLine("                            b.el.classList.remove('rules-search-hit');");
                html.AppendLine("                            b.el.classList.add('rules-search-hidden');");
                html.AppendLine("                        }");
                html.AppendLine("                    });");
                html.AppendLine("                    tocItems.forEach(function(t) {");
                html.AppendLine("                        if (t.text.indexOf(q) !== -1) { t.li.classList.add('rules-search-hit'); t.li.classList.remove('rules-search-hidden'); }");
                html.AppendLine("                        else { t.li.classList.remove('rules-search-hit'); t.li.classList.add('rules-search-hidden'); }");
                html.AppendLine("                    });");
                html.AppendLine("                    document.querySelectorAll('.rules-tab-btn').forEach(function(btn) {");
                html.AppendLine("                        var tabId = (btn.getAttribute('onclick') || '').match(/'([^']+)'/);");
                html.AppendLine("                        if (tabId && hitsByTab[tabId[1]]) btn.classList.add('rules-search-has-hits');");
                html.AppendLine("                        else btn.classList.remove('rules-search-has-hits');");
                html.AppendLine("                    });");
                html.AppendLine("                    var totalHits = Object.keys(hitsByTab).reduce(function(sum, k) { return sum + hitsByTab[k]; }, 0);");
                html.AppendLine("                    summary.textContent = totalHits === 0 ? 'No matches' : totalHits + ' match' + (totalHits === 1 ? '' : 'es');");
                html.AppendLine("                    // If active tab has no hits but another does, switch to it");
                html.AppendLine("                    var visible = Array.prototype.find.call(document.querySelectorAll('.rules-tab-content'), function(c) { return c.style.display !== 'none'; });");
                html.AppendLine("                    if (visible && !hitsByTab[visible.id]) {");
                html.AppendLine("                        var firstHitTab = Object.keys(hitsByTab)[0];");
                html.AppendLine("                        if (firstHitTab) {");
                html.AppendLine("                            document.querySelectorAll('.rules-tab-content').forEach(function(c) { c.style.display = 'none'; });");
                html.AppendLine("                            document.querySelectorAll('.rules-tab-btn').forEach(function(b) { b.classList.remove('active'); });");
                html.AppendLine("                            document.getElementById(firstHitTab).style.display = 'block';");
                html.AppendLine("                            document.querySelectorAll('.rules-tab-btn').forEach(function(btn) {");
                html.AppendLine("                                var t = (btn.getAttribute('onclick') || '').match(/'([^']+)'/);");
                html.AppendLine("                                if (t && t[1] === firstHitTab) btn.classList.add('active');");
                html.AppendLine("                            });");
                html.AppendLine("                        }");
                html.AppendLine("                    }");
                html.AppendLine("                    // Scroll to first highlighted match in the visible tab");
                html.AppendLine("                    var nowVisible = Array.prototype.find.call(document.querySelectorAll('.rules-tab-content'), function(c) { return c.style.display !== 'none'; });");
                html.AppendLine("                    if (nowVisible) {");
                html.AppendLine("                        var firstMark = nowVisible.querySelector('.rules-mark');");
                html.AppendLine("                        if (firstMark) firstMark.scrollIntoView({behavior:'smooth', block:'center'});");
                html.AppendLine("                    }");
                html.AppendLine("                }");
                html.AppendLine("                var debounce;");
                html.AppendLine("                input.addEventListener('input', function() {");
                html.AppendLine("                    clearTimeout(debounce);");
                html.AppendLine("                    debounce = setTimeout(function() { applySearch(input.value.trim()); }, 120);");
                html.AppendLine("                });");
                html.AppendLine("                input.addEventListener('keydown', function(e) {");
                html.AppendLine("                    if (e.key === 'Escape') { input.value = ''; clearHighlights(); }");
                html.AppendLine("                });");
                html.AppendLine("            })();");
                html.AppendLine("            </script>");
            }

            html.AppendLine("        </div>");
            html.AppendLine("    </div>");

            AppendFooter(html);

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        /// <summary>
        /// Extract section headings from plain-text rules.
        /// Lines matching "N. Title" are treated as headings.
        /// </summary>
        private static List<(string anchor, string title)> ParseRulesHeadings(string text)
        {
            var headings = new List<(string, string)>();
            if (string.IsNullOrWhiteSpace(text)) return headings;

            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(\d+[a-z]?)\.\s+(.+)$");
                    if (match.Success)
                    {
                        var title = match.Groups[2].Value.Trim();
                        var anchor = "rule-" + System.Text.RegularExpressions.Regex.Replace(
                            title.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
                        headings.Add((anchor, $"{match.Groups[1].Value}. {title}"));
                }
            }
            return headings;
        }

        /// <summary>
        /// Convert plain-text rules to structured HTML.
        /// "N. Title" lines become h3 headings with anchors.
        /// Lines starting with "- " become bullet lists.
        /// Blank lines separate paragraphs.
        /// </summary>
        private static string PlainTextToRulesHtml(string text, string sectionId, Dictionary<string, string>? afterHeadingContent = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            var sb = new StringBuilder();
            var lines = text.Split('\n');
            bool inList = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();

                if (string.IsNullOrEmpty(trimmed))
                {
                    if (inList) { sb.AppendLine("</ul>"); inList = false; }
                    continue;
                }

                // Numbered heading: "1. Title"
                var headingMatch = System.Text.RegularExpressions.Regex.Match(trimmed, @"^(\d+[a-z]?)\.\s+(.+)$");
                if (headingMatch.Success)
                {
                    if (inList) { sb.AppendLine("</ul>"); inList = false; }
                    var title = headingMatch.Groups[2].Value.Trim();
                    var anchor = "rule-" + System.Text.RegularExpressions.Regex.Replace(
                        title.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
                    sb.AppendLine($"<h3 id=\"{anchor}\">{System.Net.WebUtility.HtmlEncode($"{headingMatch.Groups[1].Value}. {title}")}</h3>");

                    // Inject extra content after specific headings (e.g. rack diagram)
                    if (afterHeadingContent != null && afterHeadingContent.TryGetValue(anchor, out var extra))
                        sb.AppendLine(extra);

                    continue;
                }

                // Bullet line: "- text"
                if (trimmed.StartsWith("- "))
                {
                    if (!inList) { sb.AppendLine("<ul>"); inList = true; }
                    sb.AppendLine($"<li>{System.Net.WebUtility.HtmlEncode(trimmed[2..])}</li>");
                    continue;
                }

                // Regular paragraph text
                if (inList) { sb.AppendLine("</ul>"); inList = false; }
                sb.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(trimmed)}</p>");
            }

            if (inList) sb.AppendLine("</ul>");
            return sb.ToString();
        }

        /// <summary>
        /// Returns HTML for the official EPA rack diagram image.
        /// Uses the SVG hosted on the EPA website.
        /// </summary>
        private static string GenerateRackDiagramSvg()
        {
            var sb = new StringBuilder();
            sb.AppendLine("<div class=\"rack-diagram\">");
            sb.AppendLine("  <img src=\"https://www.epa.org.uk/Pictures/Rules/The-Rack-With-Line.svg\" alt=\"EPA 8-ball rack diagram showing head ball, 8-ball marker, rack line and ball positions\" />");
            sb.AppendLine("</div>");
            return sb.ToString();
        }

        private string GenerateContactPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            
            AppendDocumentHead(html, $"{_settings.ContactPageTitle} - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Contact");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>&#128231; {Esc(_settings.ContactPageTitle)}</h2>");
            html.AppendLine("            </div>");
            html.AppendLine("            <div class=\"section\">");
            html.AppendLine("                <div class=\"contact-grid\">");
            
            if (!string.IsNullOrWhiteSpace(_settings.ContactEmail))
            {
                html.AppendLine("                    <div class=\"contact-item\">");
                html.AppendLine("                        <h4>&#128231; Email</h4>");
                html.AppendLine($"                        <a href=\"mailto:{_settings.ContactEmail}\">{_settings.ContactEmail}</a>");
                html.AppendLine("                    </div>");
            }
            
            if (!string.IsNullOrWhiteSpace(_settings.ContactPhone))
            {
                html.AppendLine("                    <div class=\"contact-item\">");
                html.AppendLine("                        <h4>&#128222; Phone</h4>");
                html.AppendLine($"                        <a href=\"tel:{_settings.ContactPhone}\">{_settings.ContactPhone}</a>");
                html.AppendLine("                    </div>");
            }
            
            if (!string.IsNullOrWhiteSpace(_settings.ContactAddress))
            {
                html.AppendLine("                    <div class=\"contact-item\">");
                html.AppendLine("                        <h4>&#128205; Address</h4>");
                html.AppendLine($"                        <p>{_settings.ContactAddress}</p>");
                html.AppendLine("                    </div>");
            }
            
            html.AppendLine("                </div>");
            
            if (_settings.HasSocialLinks)
            {
                html.AppendLine("                <div class=\"social-links-section\">");
                html.AppendLine("                    <h4>Follow Us</h4>");
                html.AppendLine("                    <div class=\"social-links\">");
                if (!string.IsNullOrWhiteSpace(_settings.FacebookUrl))
                    html.AppendLine($"                        <a href=\"{_settings.FacebookUrl}\" target=\"_blank\" class=\"social-link facebook\">Facebook</a>");
                if (!string.IsNullOrWhiteSpace(_settings.TwitterUrl))
                    html.AppendLine($"                        <a href=\"{_settings.TwitterUrl}\" target=\"_blank\" class=\"social-link twitter\">Twitter</a>");
                if (!string.IsNullOrWhiteSpace(_settings.InstagramUrl))
                    html.AppendLine($"                        <a href=\"{_settings.InstagramUrl}\" target=\"_blank\" class=\"social-link instagram\">Instagram</a>");
                if (!string.IsNullOrWhiteSpace(_settings.YouTubeUrl))
                    html.AppendLine($"                        <a href=\"{_settings.YouTubeUrl}\" target=\"_blank\" class=\"social-link youtube\">YouTube</a>");
                if (!string.IsNullOrWhiteSpace(_settings.TikTokUrl))
                    html.AppendLine($"                        <a href=\"{_settings.TikTokUrl}\" target=\"_blank\" class=\"social-link tiktok\">TikTok</a>");
                html.AppendLine("                    </div>");
                html.AppendLine("                </div>");
            }
            
            html.AppendLine("            </div>");
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private string GenerateSponsorsPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var imageOptimizer = new ImageOptimizationService();
            
            AppendDocumentHead(html, $"{_settings.SponsorsPageTitle} - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Sponsors");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>&#129309; {Esc(_settings.SponsorsPageTitle)}</h2>");
            html.AppendLine("                <p class=\"hero-dates\">Thank you to our supporters</p>");
            html.AppendLine("            </div>");
            
            var activeSponsors = _settings.Sponsors.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToList();
            var tiers = activeSponsors.Select(s => s.Tier).Distinct().ToList();
            
            foreach (var tier in tiers)
            {
                var tierSponsors = activeSponsors.Where(s => s.Tier == tier).ToList();
                if (tierSponsors.Count == 0) continue;
                
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine($"                <h3>{tier} Sponsors</h3>");
                html.AppendLine($"                <div class=\"sponsors-{_settings.SponsorLayout}\">");
                
                foreach (var sponsor in tierSponsors)
                {
                    html.AppendLine("                    <div class=\"sponsor-card\">");
                    if (sponsor.LogoData.Length > 0)
                    {
                        var mimeType = imageOptimizer.GetMimeType(sponsor.LogoFileName);
                        var dataUrl = imageOptimizer.ToDataUrl(sponsor.LogoData, mimeType);
                        if (!string.IsNullOrWhiteSpace(sponsor.WebsiteUrl))
                            html.AppendLine($"                        <a href=\"{sponsor.WebsiteUrl}\" target=\"_blank\"><img src=\"{dataUrl}\" alt=\"{sponsor.Name}\" style=\"max-height: {_settings.SponsorLogoMaxHeight}px;\"></a>");
                        else
                            html.AppendLine($"                        <img src=\"{dataUrl}\" alt=\"{sponsor.Name}\" style=\"max-height: {_settings.SponsorLogoMaxHeight}px;\"></a>");
                    }
                    html.AppendLine($"                        <h4>{sponsor.Name}</h4>");
                    if (!string.IsNullOrWhiteSpace(sponsor.Description))
                        html.AppendLine($"                        <p>{sponsor.Description}</p>");
                    if (!string.IsNullOrWhiteSpace(sponsor.WebsiteUrl))
                        html.AppendLine($"                        <a href=\"{sponsor.WebsiteUrl}\" target=\"_blank\" class=\"sponsor-link\">Visit Website &#8594;</a>");
                    html.AppendLine("                    </div>");
                }
                
                html.AppendLine("                </div>");
                html.AppendLine("            </div>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private string GenerateNewsPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            
            AppendDocumentHead(html, $"{_settings.NewsPageTitle} - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "News");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>&#128240; {Esc(_settings.NewsPageTitle)}</h2>");
            html.AppendLine("            </div>");

            var publishedNews = _settings.NewsItems
                .Where(n => n.IsPublished)
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.DatePublished)
                .Take(_settings.NewsItemsToShow)
                .ToList();
            
            if (publishedNews.Count != 0)
            {
                foreach (var news in publishedNews)
                {
                    html.AppendLine("            <article class=\"section news-article\">");
                    if (news.IsPinned)
                        html.AppendLine("                <span class=\"pinned-badge\">&#128204; Pinned</span>");
                    html.AppendLine($"                <h3>{news.Title}</h3>");
                    html.AppendLine($"                <p class=\"news-meta\"><span class=\"date\">{news.DatePublished:dd MMMM yyyy}</span>");
                    if (!string.IsNullOrWhiteSpace(news.Category))
                        html.AppendLine($"                <span class=\"category-badge\">{news.Category}</span>");
                    html.AppendLine("                </p>");
                    html.AppendLine($"                <div class=\"news-content\">{news.Content}</div>");
                    html.AppendLine("            </article>");
                }
            }
            else
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <p class=\"empty-message\">No news articles available.</p>");
                html.AppendLine("            </div>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }

        private string GenerateRowsReportsPage(Season season, WebsiteTemplate template)
        {
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);

            return GenerateFullPage($"{_settings.RowsReportsPageTitle} - {_settings.LeagueName}", season, "Rows Reports", html =>
            {
                html.AppendLine("            <div class=\"hero\">");
                html.AppendLine($"                <h2>&#128221; {Esc(_settings.RowsReportsPageTitle)}</h2>");
                html.AppendLine("                <p class=\"hero-dates\">Weekly match reports and round-ups</p>");
                html.AppendLine("            </div>");

                var publishedReports = _settings.RowsReports
                    .Where(r => r.IsPublished)
                    .OrderByDescending(r => r.WeekNumber)
                    .ThenByDescending(r => r.MatchDate)
                    .Take(_settings.RowsReportsPerPage)
                    .ToList();

                if (publishedReports.Count != 0)
                {
                    foreach (var report in publishedReports)
                    {
                        html.AppendLine("            <article class=\"section rows-report\">");
                        html.AppendLine($"                <div class=\"report-header\">");
                        html.AppendLine($"                    <span class=\"week-badge\">Week {report.WeekNumber}</span>");
                        if (!string.IsNullOrWhiteSpace(report.Author))
                            html.AppendLine($"                    <span class=\"report-author\">By {report.Author}</span>");
                        html.AppendLine($"                </div>");
                        html.AppendLine($"                <h3>{report.Title}</h3>");
                        html.AppendLine($"                <p class=\"report-meta\">");
                        html.AppendLine($"                    <span class=\"date\">&#128197; {report.MatchDate:dddd dd MMMM yyyy}</span>");
                        html.AppendLine($"                </p>");

                        if (!string.IsNullOrWhiteSpace(report.Summary))
                        {
                            html.AppendLine($"                <p class=\"report-summary\">{report.Summary}</p>");
                        }

                        // Show results for that week if we can match fixtures by date
                        var weekFixtures = fixtures
                            .Where(f => f.Date.Date == report.MatchDate.Date && f.Frames.Any(fr => fr.Winner != FrameWinner.None))
                            .OrderBy(f => f.Date)
                            .ToList();

                        if (weekFixtures.Count > 0)
                        {
                            html.AppendLine("                <div class=\"report-results\">");
                            html.AppendLine("                    <h4>Results</h4>");
                            foreach (var fixture in weekFixtures)
                            {
                                var homeTeam = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
                                var awayTeam = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId);
                                var homeName = homeTeam?.Name ?? "TBC";
                                var awayName = awayTeam?.Name ?? "TBC";
                                var homeWon = fixture.HomeScore > fixture.AwayScore;
                                var awayWon = fixture.AwayScore > fixture.HomeScore;

                                html.AppendLine("                    <div class=\"report-result-row\">");
                                html.AppendLine($"                        <span class=\"team-name{(homeWon ? " winner" : "")}\">{homeName}</span>");
                                html.AppendLine($"                        <span class=\"result-score\">{fixture.HomeScore} - {fixture.AwayScore}</span>");
                                html.AppendLine($"                        <span class=\"team-name{(awayWon ? " winner" : "")}\">{awayName}</span>");
                                html.AppendLine("                    </div>");
                            }
                            html.AppendLine("                </div>");
                        }

                        html.AppendLine($"                <div class=\"report-content\">{report.Content}</div>");

                        if (report.Tags.Count > 0)
                        {
                            html.AppendLine("                <div class=\"report-tags\">");
                            foreach (var tag in report.Tags)
                                html.AppendLine($"                    <span class=\"report-tag\">{tag}</span>");
                            html.AppendLine("                </div>");
                        }

                        html.AppendLine("            </article>");
                    }
                }
                else
                {
                    html.AppendLine("            <div class=\"section\">");
                    html.AppendLine("                <p class=\"empty-message\">No match reports available yet. Check back after the next round of matches!</p>");
                    html.AppendLine("            </div>");
                }
            });
        }

        private string GenerateCompetitionsPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();

            AppendDocumentHead(html, $"{_settings.CompetitionsPageTitle} - {_settings.LeagueName}", season);
            html.AppendLine("<body>");

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);

            AppendHeader(html, season);
            AppendNavigation(html, "Competitions");

            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>&#127942; {Esc(_settings.CompetitionsPageTitle)}</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{_settings.LeagueName}</p>");
            html.AppendLine("            </div>");

            // Show ALL competitions across all seasons (tournaments are standalone events).
            // Honour user-defined display order first, then fall back to Status/CreatedDate.
            var orderMap = new Dictionary<Guid, int>();
            for (int oi = 0; oi < _settings.CompetitionDisplayOrder.Count; oi++)
                orderMap[_settings.CompetitionDisplayOrder[oi]] = oi;

            var competitions = _league.Competitions
                .Where(c => c.Status != CompetitionStatus.Draft)
                .Where(c => c.ShowOnWebsite)
                .OrderBy(c => orderMap.TryGetValue(c.Id, out var idx) ? idx : int.MaxValue)
                .ThenByDescending(c => c.Status == CompetitionStatus.InProgress)
                .ThenByDescending(c => c.CreatedDate)
                .ToList();

            // Gather players and teams from ALL seasons so name lookups work
            var players = _league.Players.ToList();
            var teams = _league.Teams.ToList();

            if (competitions.Count > 0)
            {
                // ── Competition selector tabs ─────────────────────────────
                html.AppendLine("            <div class=\"comp-tabs\">");
                for (int i = 0; i < competitions.Count; i++)
                {
                    var comp = competitions[i];
                    var statusClass = comp.Status switch
                    {
                        CompetitionStatus.Completed => "status-completed",
                        CompetitionStatus.InProgress => "status-active",
                        _ => "status-draft"
                    };
                    var statusIcon = comp.Status switch
                    {
                        CompetitionStatus.Completed => "&#9989;",
                        CompetitionStatus.InProgress => "&#9889;",
                        _ => "&#128221;"
                    };
                    var activeClass = i == 0 ? " active" : "";

                    html.AppendLine($"                <button class=\"comp-tab{activeClass}\" onclick=\"showComp({i})\">");
                    html.AppendLine($"                    <span class=\"comp-tab-name\">{comp.Name}</span>");
                    html.AppendLine($"                    <span class=\"badge {statusClass}\">{statusIcon} {comp.Status}</span>");
                    html.AppendLine($"                </button>");
                }
                html.AppendLine("            </div>");

                // ── Competition panels ────────────────────────────────────
                // Uniform polished layout applied to ALL competition formats (originally piloted on doubles).
                html.AppendLine("            <style>");
                // Compact venue chip styling (used on per-match cards on the draw)
                html.AppendLine("            .comp-panel .venue-chip { display: inline-flex; align-items: center; gap: 4px; padding: 3px 9px; background: var(--bg-alt, #F1F5F9); border: 1px solid var(--border-color, #E2E8F0); border-radius: 999px; font-size: 0.72rem; font-weight: 500; color: var(--text-secondary, #64748B); line-height: 1.4; white-space: nowrap; max-width: 100%; }");
                html.AppendLine("            .comp-panel .venue-chip .vc-icon { opacity: 0.7; font-size: 0.7rem; }");
                html.AppendLine("            .comp-panel .venue-chip .vc-name { overflow: hidden; text-overflow: ellipsis; }");
                html.AppendLine("            .comp-panel .venue-chip .vc-tables { background: var(--card-bg, #fff); border: 1px solid var(--border-color, #E2E8F0); border-radius: 999px; padding: 1px 7px; font-size: 0.65rem; color: var(--text-color, #0F172A); font-weight: 700; line-height: 1.3; }");
                html.AppendLine("            .comp-panel .comp-header { display: flex; flex-wrap: wrap; align-items: baseline; gap: 8px 18px; padding: 4px 0 14px 0; border-bottom: 1px solid var(--border-color, #E2E8F0); margin-bottom: 18px; }");
                html.AppendLine("            .comp-panel .comp-header .ch-chip { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; background: var(--bg-alt, #F1F5F9); border-radius: 999px; font-size: 0.8rem; font-weight: 600; color: var(--text-color, #0F172A); }");
                html.AppendLine("            .comp-panel .comp-header .ch-meta { color: var(--text-secondary, #64748B); font-size: 0.8rem; }");
                html.AppendLine("            .comp-panel .comp-header .ch-meta a { color: var(--primary-color, #3B82F6); text-decoration: none; }");
                html.AppendLine("            .comp-panel .comp-header .ch-meta a:hover { text-decoration: underline; }");
                html.AppendLine("            .comp-panel .comp-notes { font-size: 0.85rem; color: var(--text-secondary, #64748B); font-style: italic; margin: -8px 0 16px 0; padding-left: 10px; border-left: 3px solid var(--border-color, #E2E8F0); }");
                html.AppendLine("            .comp-panel .bk-card { padding: 8px 10px; }");
                html.AppendLine("            .comp-panel .bk-card .bk-name { font-size: 0.85rem; line-height: 1.25; }");
                html.AppendLine("            .comp-panel .bk-card .bk-venue { font-size: 0.7rem; color: var(--text-secondary, #64748B); margin-top: 4px; }");
                html.AppendLine("            .comp-panel .group-matches .match-venue { font-size: 0.75rem; color: var(--text-secondary, #64748B); margin-top: 2px; }");
                html.AppendLine("            </style>");

                for (int i = 0; i < competitions.Count; i++)
                {
                    var comp = competitions[i];
                    var display = i == 0 ? "block" : "none";

                    html.AppendLine($"            <div class=\"comp-panel\" id=\"comp-{i}\" style=\"display:{display}\">");

                    var formatLabel = comp.Format.ToString().Replace("Knockout", " Knockout").Replace("GroupStage", " Group Stage").Replace("RoundRobin", "Round Robin");
                    var compSeasonName = _league.Seasons.FirstOrDefault(s => s.Id == comp.SeasonId)?.Name;

                    // Format-aware participant label
                    var participantWord = comp.Format switch
                    {
                        CompetitionFormat.DoublesKnockout or CompetitionFormat.DoublesGroupStage => "pairs",
                        CompetitionFormat.TeamKnockout => "teams",
                        _ => "players"
                    };

                    // Uniform polished header — primary chips + a single quiet meta line (applies to all formats)
                    html.AppendLine("                <div class=\"comp-header\">");
                    html.AppendLine($"                    <span class=\"ch-chip\">&#128101; {GetParticipantCount(comp)} {participantWord}</span>");
                    html.AppendLine($"                    <span class=\"ch-chip\">&#127919; {formatLabel}</span>");
                    // Show competition-level Best Of as a default chip when all rounds use it (or none override).
                    // If individual rounds override, the chip is hidden — round headers will show their own.
                    var anyRoundOverrides = comp.Rounds.Any(r => r.BestOf.HasValue && r.BestOf != comp.BestOf);
                    if (comp.BestOf > 0 && !anyRoundOverrides)
                        html.AppendLine($"                    <span class=\"ch-chip\">&#127932; Best of {comp.BestOf}</span>");

                    var metaParts = new List<string>();
                    if (comp.StartDate.HasValue) metaParts.Add($"&#128197; {comp.StartDate.Value:dd MMM yyyy}");
                    if (!string.IsNullOrWhiteSpace(compSeasonName)) metaParts.Add(Esc(compSeasonName));

                    if (comp.PlateCompetitionId.HasValue)
                    {
                        var plate = _league.Competitions.FirstOrDefault(c => c.Id == comp.PlateCompetitionId.Value);
                        var plateIdx = plate != null ? competitions.FindIndex(c => c.Id == plate.Id) : -1;
                        if (plate != null && plateIdx >= 0)
                            metaParts.Add($"Plate: <a href=\"#\" onclick=\"showComp({plateIdx});return false;\">{Esc(plate.Name)}</a>");
                        else if (plate != null)
                            metaParts.Add($"Plate: {Esc(plate.Name)}");
                    }
                    if (comp.ParentCompetitionId.HasValue)
                    {
                        var parent = _league.Competitions.FirstOrDefault(c => c.Id == comp.ParentCompetitionId.Value);
                        var parentIdx = parent != null ? competitions.FindIndex(c => c.Id == parent.Id) : -1;
                        if (parent != null && parentIdx >= 0)
                            metaParts.Add($"Plate of: <a href=\"#\" onclick=\"showComp({parentIdx});return false;\">{Esc(parent.Name)}</a>");
                        else if (parent != null)
                            metaParts.Add($"Plate of: {Esc(parent.Name)}");
                    }

                    if (metaParts.Count > 0)
                        html.AppendLine($"                    <span class=\"ch-meta\">{string.Join(" &middot; ", metaParts)}</span>");
                    html.AppendLine("                </div>");

                    if (!string.IsNullOrWhiteSpace(comp.Notes))
                        html.AppendLine($"                <p class=\"comp-notes\">{Esc(comp.Notes)}</p>");

                    // Round Robin → standings + results
                    if (comp.Format == CompetitionFormat.RoundRobin && comp.Rounds.Count > 0)
                    {
                        AppendRoundRobinStandings(html, comp, players, teams);
                        AppendMatchResults(html, comp, players, teams);
                    }
                    // Knockout → bracket
                    else if (comp.Rounds.Count > 0 && comp.Groups.Count == 0)
                    {
                        AppendKnockoutBracket(html, comp, players, teams);
                    }
                    // Group stage → group tables + knockout if present
                    else if (comp.Groups.Count > 0)
                    {
                        AppendGroupStage(html, comp, players, teams);
                        if (comp.Rounds.Count > 0)
                        {
                            html.AppendLine("                <h4 style=\"margin-top:1.5rem\">Knockout Stage</h4>");
                            AppendKnockoutBracket(html, comp, players, teams);
                        }
                    }
                    else
                    {
                        html.AppendLine("                <p class=\"empty-message\">Draw not yet made. Check back soon!</p>");
                    }

                    html.AppendLine($"            </div>");
                }
            }
            else
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <p class=\"empty-message\">No competitions available yet.</p>");
                html.AppendLine("            </div>");
            }

            html.AppendLine("        </div>");
            html.AppendLine("    </div>");

            AppendFooter(html);

            // ── Tab switching JS ──────────────────────────────────────────
            html.AppendLine("<script>");
            html.AppendLine("function showComp(idx){");
            html.AppendLine("  document.querySelectorAll('.comp-panel').forEach(p=>p.style.display='none');");
            html.AppendLine("  document.querySelectorAll('.comp-tab').forEach(t=>t.classList.remove('active'));");
            html.AppendLine("  var panel=document.getElementById('comp-'+idx);");
            html.AppendLine("  if(panel)panel.style.display='block';");
            html.AppendLine("  document.querySelectorAll('.comp-tab')[idx].classList.add('active');");
            html.AppendLine("}");
            html.AppendLine("</script>");

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        // ── Competition sub-section builders ─────────────────────────────

        private void AppendRoundRobinStandings(StringBuilder html, Competition comp, List<Player> players, List<Team> teams)
        {
            var standings = CalculateRoundRobinStandings(comp, players, teams);
            if (standings.Count == 0) return;

            html.AppendLine("                <div class=\"section comp-standings\">");
            html.AppendLine("                    <h4>Standings</h4>");
            html.AppendLine($"                    <table class=\"{GetTableClasses()}\">");
            html.AppendLine("                        <thead><tr><th>#</th><th>Player</th><th>P</th><th>W</th><th>D</th><th>L</th><th>FD</th><th>Pts</th></tr></thead>");
            html.AppendLine("                        <tbody>");
            foreach (var s in standings)
            {
                html.AppendLine($"                        <tr><td>{s.pos}</td><td>{s.name}</td><td>{s.played}</td><td>{s.won}</td><td>{s.drawn}</td><td>{s.lost}</td><td>{(s.fd >= 0 ? "+" : "")}{s.fd}</td><td><strong>{s.pts}</strong></td></tr>");
            }
            html.AppendLine("                        </tbody>");
            html.AppendLine($"                    </table>");
            html.AppendLine("                </div>");
        }

        private void AppendMatchResults(StringBuilder html, Competition comp, List<Player> players, List<Team> teams)
        {
            html.AppendLine("                <div class=\"comp-results\">");
            foreach (var round in comp.Rounds.OrderBy(r => r.RoundNumber))
            {
                var rrDateHtml = round.Date.HasValue
                    ? $" <span class=\"round-date\">{round.Date.Value:dd MMM yyyy}</span>"
                    : "";
                var rrBestOf = round.GetEffectiveBestOf(comp);
                var rrBoHtml = rrBestOf > 0
                    ? $" <span class=\"round-bo\">&#127932; Best of {rrBestOf}</span>"
                    : "";
                html.AppendLine($"                    <div class=\"round-section\">");
                html.AppendLine($"                        <h4>{round.Name}{rrDateHtml}{rrBoHtml}</h4>");
                foreach (var match in round.Matches)
                    AppendMatchRow(html, match, comp, players, teams);
                html.AppendLine($"                    </div>");
            }
            html.AppendLine("                </div>");
        }

        private void AppendKnockoutBracket(StringBuilder html, Competition comp, List<Player> players, List<Team> teams)
        {
            var orderedRounds = comp.Rounds.OrderBy(r => r.RoundNumber).ToList();
            int totalRounds = orderedRounds.Count;
            if (totalRounds == 0) return;

            // Build grid column widths: 220px per round, 28px per connector
            var colWidths = new List<string>();
            for (int ri = 0; ri < totalRounds; ri++)
            {
                colWidths.Add("220px");
                if (ri < totalRounds - 1)
                    colWidths.Add("28px");
            }

            html.AppendLine("                <div class=\"bk-scroll\">");
            html.AppendLine($"                <div class=\"bk-grid\" style=\"grid-template-columns:{string.Join(" ", colWidths)}\">");

            // ── Row 1: Round headers ────────────────────────────────────
            for (int ri = 0; ri < totalRounds; ri++)
            {
                var round = orderedRounds[ri];
                int gridCol = 2 * ri + 1;
                string label = ri == totalRounds - 1 ? "Final"
                    : ri == totalRounds - 2 ? "Semi-Finals"
                    : (round.Name ?? $"Round {round.RoundNumber}");
                var roundDateHtml = round.Date.HasValue
                    ? $"<div class=\"round-date\">{round.Date.Value:dd MMM yyyy}</div>"
                    : "<div class=\"round-date\">Date TBC</div>";
                var koBestOf = round.GetEffectiveBestOf(comp);
                var koBoHtml = koBestOf > 0
                    ? $"<div class=\"round-bo\">&#127932; Best of {koBestOf}</div>"
                    : "";

                html.AppendLine($"                    <div class=\"bk-hdr\" style=\"grid-column:{gridCol};grid-row:1\">");
                html.AppendLine($"                        <div class=\"bk-rn\">{label}</div>{roundDateHtml}{koBoHtml}");
                html.AppendLine($"                    </div>");
            }

            // ── Row 2: Match bodies + connector columns ─────────────────
            for (int ri = 0; ri < totalRounds; ri++)
            {
                var round = orderedRounds[ri];
                int gridCol = 2 * ri + 1;

                html.AppendLine($"                    <div class=\"bk-body\" style=\"grid-column:{gridCol};grid-row:2\">");
                foreach (var match in round.Matches)
                    AppendBracketCard(html, match, comp, players, teams);
                html.AppendLine("                    </div>");

                // Connector column between rounds (row 2 only — aligned with match bodies)
                if (ri < totalRounds - 1)
                {
                    int connCol = 2 * ri + 2;
                    int pairs = orderedRounds[ri + 1].Matches.Count;
                    html.AppendLine($"                    <div class=\"bk-conn\" style=\"grid-column:{connCol};grid-row:2\">");
                    for (int p = 0; p < pairs; p++)
                        html.AppendLine("                        <div class=\"bk-cg\"><span class=\"bk-hl\" style=\"top:25%\"></span><span class=\"bk-hl\" style=\"top:75%\"></span><span class=\"bk-vl\"></span><span class=\"bk-rl\"></span></div>");
                    html.AppendLine("                    </div>");
                }
            }

            html.AppendLine("                </div>");
            html.AppendLine("                </div>");
        }

        private void AppendBracketCard(StringBuilder html, CompetitionMatch match, Competition comp, List<Player> players, List<Team> teams)
        {
            var p1Name = GetWebParticipantName(match.Participant1Id, comp, players, teams);
            var p2Name = GetWebParticipantName(match.Participant2Id, comp, players, teams);
            bool p1Bye = p1Name == null && match.IsComplete;
            bool p2Bye = p2Name == null && match.IsComplete;
            var p1 = p1Name ?? (p1Bye ? "BYE" : "TBD");
            var p2 = p2Name ?? (p2Bye ? "BYE" : "TBD");
            bool p1w = match.WinnerId.HasValue && match.WinnerId == match.Participant1Id;
            bool p2w = match.WinnerId.HasValue && match.WinnerId == match.Participant2Id;

            // Winner checkmark to match app display
            var p1Display = p1w ? $"&#10004; {p1}" : p1;
            var p2Display = p2w ? $"&#10004; {p2}" : p2;

            var cardClass = match.IsComplete ? "bk-card bk-done" : "bk-card";
            html.AppendLine($"                        <div class=\"{cardClass}\">");
            html.AppendLine($"                            <div class=\"bk-player{(p1w ? " bk-w" : "")}{(p1Bye ? " bk-bye" : p1Name == null ? " bk-tbd" : "")}\">");
            html.AppendLine($"                                <span class=\"bk-name\">{p1Display}</span><span class=\"bk-sc{(p1w ? " bk-sw" : "")}\">{match.Participant1Score}</span>");
            html.AppendLine($"                            </div>");
            html.AppendLine($"                            <div class=\"bk-dv\"></div>");
            html.AppendLine($"                            <div class=\"bk-player{(p2w ? " bk-w" : "")}{(p2Bye ? " bk-bye" : p2Name == null ? " bk-tbd" : "")}\">");
            html.AppendLine($"                                <span class=\"bk-name\">{p2Display}</span><span class=\"bk-sc{(p2w ? " bk-sw" : "")}\">{match.Participant2Score}</span>");
            html.AppendLine($"                            </div>");
            if (match.ScheduledDate.HasValue)
                html.AppendLine($"                            <div class=\"bk-date\" style=\"font-size:0.75rem;color:var(--text-secondary,#64748B);margin-top:4px;\">&#128197; {match.ScheduledDate.Value:dd MMM HH:mm}</div>");
            if (!string.IsNullOrEmpty(match.VenueDisplay))
                html.AppendLine($"                            <div class=\"bk-venue\">&#128205; {System.Net.WebUtility.HtmlEncode(match.VenueDisplay)}</div>");
            html.AppendLine($"                        </div>");
        }

        private void AppendGroupStage(StringBuilder html, Competition comp, List<Player> players, List<Team> teams)
        {
            int topAdvance = comp.GroupSettings?.TopPlayersAdvance ?? 2;
            int currentRound = comp.Groups.Count > 0 ? comp.Groups.Max(g => g.GroupRound) : 1;
            bool currentHasSelections = comp.Groups.Any(g => g.Standings.Any(s => s.Position > 0));

            // Build list of all rounds (previous + current) so we can render as tabs
            var rounds = new List<(int roundNumber, bool isCurrent, DateTime? date, int advance, bool hasSelections, List<CompetitionGroup> groups)>();

            if (comp.PreviousGroups.Count > 0)
            {
                foreach (var prev in comp.PreviousGroups.GroupBy(g => g.GroupRound).OrderBy(r => r.Key))
                {
                    int roundAdvance = prev.Max(g => g.Standings.Count(s => s.Position > 0));
                    if (roundAdvance < 1) roundAdvance = topAdvance;
                    DateTime? roundDate = null;
                    rounds.Add((prev.Key, false, roundDate, roundAdvance, true, prev.OrderBy(g => g.GroupNumber).ToList()));
                }
            }

            if (comp.Groups.Count > 0)
            {
                rounds.Add((currentRound, true, comp.GroupSettings?.GroupDate, topAdvance, currentHasSelections,
                    comp.Groups.OrderBy(g => g.GroupNumber).ToList()));
            }

            if (rounds.Count == 0) return;

            // Inline styles: responsive grid for groups + tab strip for rounds
            html.AppendLine("                <style>");
            html.AppendLine("                .gs-wrap .comp-groups { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 14px; align-items: start; }");
            html.AppendLine("                .gs-wrap .group-section { background: var(--card-bg, #fff); border: 1px solid var(--border-color, #E2E8F0); border-radius: 10px; padding: 14px 16px; }");
            html.AppendLine("                .gs-wrap .group-section h4 { margin: 0 0 8px 0; font-size: 1rem; }");
            html.AppendLine("                .gs-wrap .group-count { color: var(--text-secondary, #64748B); font-weight: 400; font-size: 0.85rem; }");
            html.AppendLine("                .gs-wrap .group-venue { font-size: 0.8rem; color: var(--text-secondary, #64748B); margin: 0 0 8px 0; }");
            html.AppendLine("                .gs-wrap .group-players { display: flex; flex-direction: column; gap: 4px; }");
            html.AppendLine("                .gs-wrap .group-player { padding: 6px 10px; border-radius: 6px; font-size: 0.9rem; display: flex; justify-content: space-between; align-items: center; gap: 8px; background: var(--bg-alt, #F8FAFC); }");
            html.AppendLine("                .gs-wrap .group-player.gp-winner { background: #ECFDF5; }");
            html.AppendLine("                .gs-wrap .group-player.gp-loser { background: #FEF2F2; opacity: 0.7; }");
            html.AppendLine("                .gs-wrap .group-player.gp-noshow { background: #F3F4F6; opacity: 0.6; text-decoration: line-through; }");
            html.AppendLine("                .gs-wrap .group-player.gp-organiser { background: #FEF3C7; border: 1px solid #FCD34D; }");
            html.AppendLine("                .gs-wrap .group-player.gp-organiser.gp-winner { background: #FEF3C7; }");
            html.AppendLine("                .gs-wrap .gp-organiser-star { color: #B45309; font-weight: 700; margin-left: 4px; }");
            html.AppendLine("                .gs-wrap .gp-organiser-note { font-size: 0.78rem; color: var(--text-secondary, #64748B); font-style: italic; margin: 8px 0 0 0; }"); 
            html.AppendLine("                .gs-wrap .gp-badge { font-size: 0.7rem; padding: 2px 6px; border-radius: 4px; font-weight: 600; white-space: nowrap; }");
            html.AppendLine("                .gs-wrap .gp-badge-w { background: #10B981; color: #fff; }");
            html.AppendLine("                .gs-wrap .gp-badge-l { background: #EF4444; color: #fff; }");
            html.AppendLine("                .gs-wrap .gp-badge-ns { background: #94A3B8; color: #fff; }");
            html.AppendLine("                .gs-tabs { display: flex; flex-wrap: wrap; gap: 6px; margin: 12px 0 16px 0; border-bottom: 1px solid var(--border-color, #E2E8F0); padding-bottom: 8px; }");
            html.AppendLine("                .gs-tab { background: var(--bg-alt, #F1F5F9); color: var(--text-color, #0F172A); border: 1px solid var(--border-color, #E2E8F0); border-radius: 8px; padding: 6px 14px; font-size: 0.85rem; font-weight: 600; cursor: pointer; transition: all 0.15s; }");
            html.AppendLine("                .gs-tab:hover { background: var(--card-bg, #fff); border-color: var(--primary-color, #3B82F6); }");
            html.AppendLine("                .gs-tab.active { background: var(--primary-color, #3B82F6); color: #fff; border-color: var(--primary-color, #3B82F6); }");
            html.AppendLine("                .gs-tab .gs-tab-date { font-weight: 400; opacity: 0.85; margin-left: 6px; font-size: 0.75rem; }");
            html.AppendLine("                .gs-panel { display: none; }");
            html.AppendLine("                .gs-panel.active { display: block; }");
            html.AppendLine("                .gs-round-meta { font-size: 0.85rem; color: var(--text-secondary, #64748B); margin: 0 0 10px 0; }");
            html.AppendLine("                </style>");

            html.AppendLine("                <div class=\"gs-wrap\">");

            // Tab strip — only when more than one round to switch between
            int defaultIdx = rounds.Count - 1; // default to most recent (current) round
            if (rounds.Count > 1)
            {
                html.AppendLine("                <div class=\"gs-tabs\">");
                for (int i = 0; i < rounds.Count; i++)
                {
                    var r = rounds[i];
                    var label = r.isCurrent && comp.PreviousGroups.Count > 0
                        ? $"Current Round ({r.roundNumber})"
                        : $"Round {r.roundNumber}";
                    var dateBit = r.date.HasValue
                        ? $"<span class=\"gs-tab-date\">{r.date.Value:dd MMM}</span>"
                        : "";
                    var active = i == defaultIdx ? " active" : "";
                    html.AppendLine($"                    <button type=\"button\" class=\"gs-tab{active}\" onclick=\"gsShow(this,{i})\">{label}{dateBit}</button>");
                }
                html.AppendLine("                </div>");
            }

            // Round panels
            for (int i = 0; i < rounds.Count; i++)
            {
                var r = rounds[i];
                var active = i == defaultIdx ? " active" : "";
                html.AppendLine($"                <div class=\"gs-panel{active}\" data-gs-idx=\"{i}\">");

                if (r.date.HasValue)
                    html.AppendLine($"                    <p class=\"gs-round-meta\">&#128197; {r.date.Value:dddd dd MMM yyyy} &middot; Top {r.advance} advance per group</p>");
                else
                    html.AppendLine($"                    <p class=\"gs-round-meta\">Top {r.advance} advance per group</p>");

                html.AppendLine("                    <div class=\"comp-groups\">");
                foreach (var group in r.groups)
                    AppendGroupSection(html, group, comp, players, teams, r.advance, r.hasSelections);
                html.AppendLine("                    </div>");

                // Organiser footnote — shown when at least one group in this
                // round has nominated an organiser to run the draw on the night.
                if (r.groups.Any(g => g.OrganiserParticipantId.HasValue))
                {
                    html.AppendLine("                    <p class=\"gp-organiser-note\">* Group organiser — runs the draw on the night.</p>");
                }

                html.AppendLine("                </div>");
            }

            html.AppendLine("                </div>");

            // Tab switching script (only needed when there's more than one round)
            if (rounds.Count > 1)
            {
                html.AppendLine("                <script>");
                html.AppendLine("                function gsShow(btn, idx) {");
                html.AppendLine("                    var wrap = btn.closest('.gs-wrap'); if (!wrap) return;");
                html.AppendLine("                    wrap.querySelectorAll('.gs-tab').forEach(function(t){ t.classList.remove('active'); });");
                html.AppendLine("                    wrap.querySelectorAll('.gs-panel').forEach(function(p){ p.classList.remove('active'); });");
                html.AppendLine("                    btn.classList.add('active');");
                html.AppendLine("                    var panel = wrap.querySelector('.gs-panel[data-gs-idx=\"' + idx + '\"]');");
                html.AppendLine("                    if (panel) panel.classList.add('active');");
                html.AppendLine("                }");
                html.AppendLine("                </script>");
            }
        }

        private void AppendGroupSection(StringBuilder html, CompetitionGroup group, Competition comp,
            List<Player> players, List<Team> teams, int topAdvance, bool hasSelections)
        {
            var hasStandings = group.Standings.Any(s => s.Played > 0);
            var sectionClass = hasStandings ? "group-section gs-has-standings" : "group-section";
            var participantLabel = comp.Format is CompetitionFormat.DoublesKnockout or CompetitionFormat.DoublesGroupStage ? "pairs" : "players";
            html.AppendLine($"                    <div class=\"{sectionClass}\">");
            html.AppendLine($"                        <h4>{group.Name} <span class=\"group-count\">({group.ParticipantIds.Count} {participantLabel})</span></h4>");
            if (!string.IsNullOrWhiteSpace(group.VenueDisplay))
                html.AppendLine($"                        <p class=\"group-venue\">&#128205; {Esc(group.VenueDisplay)}</p>");
            html.AppendLine($"                        <div class=\"group-players\">");

            foreach (var pid in group.ParticipantIds)
            {
                var name = GetWebParticipantName(pid, comp, players, teams);

                var standing = group.Standings.FirstOrDefault(s => s.ParticipantId == pid);
                bool isAdvancing = standing != null && standing.Position > 0 && standing.Position <= topAdvance;
                bool isNoShow = comp.NoShowIds.Contains(pid);
                bool isEliminated = hasSelections && !isAdvancing && !isNoShow;
                bool isOrganiser = group.OrganiserParticipantId == pid;

                string cssClass;
                string badge;
                if (isNoShow)
                {
                    cssClass = "group-player gp-noshow";
                    badge = " <span class=\"gp-badge gp-badge-ns\">No Show</span>";
                }
                else if (isAdvancing)
                {
                    cssClass = "group-player gp-winner";
                    badge = " <span class=\"gp-badge gp-badge-w\">✓ Through</span>";
                }
                else if (isEliminated)
                {
                    cssClass = "group-player gp-loser";
                    badge = " <span class=\"gp-badge gp-badge-l\">✗ Out</span>";
                }
                else
                {
                    cssClass = "group-player";
                    badge = "";
                }

                if (isOrganiser && !isNoShow)
                    cssClass += " gp-organiser";

                var organiserMark = isOrganiser
                    ? " <span class=\"gp-organiser-star\" title=\"Group organiser — runs the draw on the night\">*</span>"
                    : "";

                html.AppendLine($"                            <div class=\"{cssClass}\">{name}{organiserMark}{badge}</div>");
            }

            html.AppendLine($"                        </div>");

            // Per-group standings table (only when matches have been played)
            if (group.Standings.Any(s => s.Played > 0))
            {
                var sortedStandings = group.Standings
                    .Where(s => group.ParticipantIds.Contains(s.ParticipantId))
                    .OrderByDescending(s => s.Points)
                    .ThenByDescending(s => s.FrameDifference)
                    .ThenByDescending(s => s.FramesFor)
                    .ToList();

                html.AppendLine("                        <div class=\"group-standings\" style=\"margin-top:10px;\">");
                html.AppendLine($"                            <table class=\"{GetTableClasses()}\" style=\"font-size:0.85rem;\">");
                html.AppendLine("                                <thead><tr><th>#</th><th>Player</th><th>P</th><th>W</th><th>D</th><th>L</th><th>FD</th><th>Pts</th></tr></thead>");
                html.AppendLine("                                <tbody>");
                int pos = 1;
                foreach (var s in sortedStandings)
                {
                    var pname = GetWebParticipantName(s.ParticipantId, comp, players, teams) ?? "?";
                    var fd = s.FrameDifference;
                    html.AppendLine($"                                    <tr><td>{pos++}</td><td>{Esc(pname)}</td><td>{s.Played}</td><td>{s.Won}</td><td>{s.Drawn}</td><td>{s.Lost}</td><td>{(fd >= 0 ? "+" : "")}{fd}</td><td><strong>{s.Points}</strong></td></tr>");
                }
                html.AppendLine("                                </tbody>");
                html.AppendLine("                            </table>");
                html.AppendLine("                        </div>");
            }

            // Per-group match results
            if (group.Matches.Any(m => m.IsComplete))
            {
                html.AppendLine("                        <div class=\"group-matches\" style=\"margin-top:10px;\">");
                html.AppendLine("                            <h5 style=\"margin:6px 0;font-size:0.85rem;color:var(--text-secondary,#64748B);\">Matches</h5>");
                foreach (var match in group.Matches.Where(m => m.IsComplete))
                    AppendMatchRow(html, match, comp, players, teams);
                html.AppendLine("                        </div>");
            }

            html.AppendLine($"                    </div>");
        }

        private void AppendMatchRow(StringBuilder html, CompetitionMatch match, Competition comp, List<Player> players, List<Team> teams)
        {
            var p1Name = GetWebParticipantName(match.Participant1Id, comp, players, teams);
            var p2Name = GetWebParticipantName(match.Participant2Id, comp, players, teams);
            var p1 = p1Name ?? (match.IsComplete ? "BYE" : "TBD");
            var p2 = p2Name ?? (match.IsComplete ? "BYE" : "TBD");
            var isP1Winner = match.WinnerId.HasValue && match.WinnerId == match.Participant1Id;
            var isP2Winner = match.WinnerId.HasValue && match.WinnerId == match.Participant2Id;

            html.AppendLine($"                        <div class=\"match-row{(match.IsComplete ? " match-complete" : "")}\">");
            html.AppendLine($"                            <span class=\"match-player{(isP1Winner ? " winner" : "")}\">{p1}</span>");
            html.AppendLine($"                            <span class=\"match-score\">{match.Participant1Score} - {match.Participant2Score}</span>");
            html.AppendLine($"                            <span class=\"match-player{(isP2Winner ? " winner" : "")}\">{p2}</span>");
            html.AppendLine($"                        </div>");
            if (match.ScheduledDate.HasValue)
                html.AppendLine($"                        <div class=\"match-date\" style=\"font-size:0.8rem;color:var(--text-secondary,#64748B);\">&#128197; {match.ScheduledDate.Value:dd MMM yyyy HH:mm}</div>");
            if (!string.IsNullOrEmpty(match.VenueDisplay))
                html.AppendLine($"                        <div class=\"match-venue\">&#128205; {System.Net.WebUtility.HtmlEncode(match.VenueDisplay)}</div>");
        }
        
        private static int GetParticipantCount(Competition comp)
        {
            return comp.Format is CompetitionFormat.DoublesKnockout or CompetitionFormat.DoublesGroupStage
                ? comp.DoublesTeams.Count
                : comp.ParticipantIds.Count;
        }

        private static List<(int pos, string name, int played, int won, int drawn, int lost, int fd, int pts)>
            CalculateRoundRobinStandings(Competition comp, List<Player> players, List<Team> teams)
        {
            var stats = new Dictionary<Guid, (int p, int w, int d, int l, int ff, int fa, int pts)>();

            foreach (var round in comp.Rounds)
            {
                foreach (var match in round.Matches)
                {
                    if (match.Participant1Id.HasValue && !stats.ContainsKey(match.Participant1Id.Value))
                        stats[match.Participant1Id.Value] = (0, 0, 0, 0, 0, 0, 0);
                    if (match.Participant2Id.HasValue && !stats.ContainsKey(match.Participant2Id.Value))
                        stats[match.Participant2Id.Value] = (0, 0, 0, 0, 0, 0, 0);

                    if (!match.IsComplete || !match.Participant1Id.HasValue || !match.Participant2Id.HasValue) continue;

                    var p1 = match.Participant1Id.Value;
                    var p2 = match.Participant2Id.Value;
                    var s1 = stats[p1]; var s2 = stats[p2];

                    s1.p++; s2.p++;
                    s1.ff += match.Participant1Score; s1.fa += match.Participant2Score;
                    s2.ff += match.Participant2Score; s2.fa += match.Participant1Score;

                    if (match.Participant1Score > match.Participant2Score)
                    { s1.w++; s1.pts += match.Participant1Score + 2; s2.l++; s2.pts += match.Participant2Score; }
                    else if (match.Participant2Score > match.Participant1Score)
                    { s2.w++; s2.pts += match.Participant2Score + 2; s1.l++; s1.pts += match.Participant1Score; }
                    else
                    { s1.d++; s1.pts += match.Participant1Score + 1; s2.d++; s2.pts += match.Participant2Score + 1; }

                    stats[p1] = s1; stats[p2] = s2;
                }
            }

            int pos = 1;
            return stats
                .OrderByDescending(s => s.Value.pts)
                .ThenByDescending(s => s.Value.ff - s.Value.fa)
                .ThenByDescending(s => s.Value.ff)
                .Select(s =>
                {
                    var name = GetWebParticipantName(s.Key, comp, players, teams) ?? "Unknown";
                    return (pos++, name, s.Value.p, s.Value.w, s.Value.d, s.Value.l, s.Value.ff - s.Value.fa, s.Value.pts);
                })
                .ToList();
        }
        
        private static string? GetWebParticipantName(Guid? id, Competition comp, List<Player> players, List<Team> teams)
        {
            if (!id.HasValue) return null;
            
            if (comp.Format is CompetitionFormat.DoublesKnockout or CompetitionFormat.DoublesGroupStage)
            {
                var dt = comp.DoublesTeams.FirstOrDefault(t => t.Id == id.Value);
                return dt?.TeamName;
            }
            else if (comp.Format == CompetitionFormat.TeamKnockout)
            {
                var team = teams.FirstOrDefault(t => t.Id == id.Value);
                return team?.Name;
            }
            else
            {
                var player = players.FirstOrDefault(p => p.Id == id.Value);
                return player?.FullName;
            }
        }

        private string GenerateEntryFormsPage(Season season, WebsiteTemplate template)
        {
            return GenerateFullPage($"{_settings.EntryFormsPageTitle} - {_settings.LeagueName}", season, "Entry Forms", html =>
            {
                html.AppendLine("            <div class=\"hero\">");
                html.AppendLine($"                <h2>&#128203; {Esc(_settings.EntryFormsPageTitle)}</h2>");
                html.AppendLine("                <p class=\"hero-dates\">Season entries &amp; registrations</p>");
                html.AppendLine("            </div>");

                var publishedForms = _settings.EntryForms
                    .Where(f => f.IsPublished)
                    .OrderBy(f => f.SortOrder)
                    .ThenByDescending(f => f.DateCreated)
                    .ToList();

                if (publishedForms.Count != 0)
                {
                    foreach (var form in publishedForms)
                    {
                        var formId = $"form-{form.Id:N}";
                        var isClosed = form.IsClosed || (form.ClosingDate.HasValue && form.ClosingDate.Value < DateTime.Now);

                        html.AppendLine($"            <div class=\"section entry-form-card\" id=\"{formId}\">");

                        // Form logo
                        if (form.LogoImageData != null && form.LogoImageData.Length > 0)
                        {
                            var imgOpt = new ImageOptimizationService();
                            var dataUrl = imgOpt.ToDataUrl(form.LogoImageData, imgOpt.GetMimeType("logo.png"));
                            html.AppendLine($"                <div class=\"entry-form-logo\"><img src=\"{dataUrl}\" alt=\"{Esc(form.Title)}\"></div>");
                        }

                        // Form header
                        html.AppendLine("                <div class=\"entry-form-header\">");
                        html.AppendLine($"                    <h3>{Esc(form.Title)}</h3>");
                        if (isClosed)
                            html.AppendLine("                    <span class=\"entry-form-badge closed\">Closed</span>");
                        else
                            html.AppendLine("                    <span class=\"entry-form-badge open\">Open</span>");
                        html.AppendLine("                </div>");

                        if (!string.IsNullOrWhiteSpace(form.Description))
                            html.AppendLine($"                <p class=\"entry-form-desc\">{Esc(form.Description)}</p>");

                        if (form.ClosingDate.HasValue)
                            html.AppendLine($"                <p class=\"entry-form-deadline\">&#9200; Closing date: <strong>{form.ClosingDate.Value:dddd dd MMMM yyyy}</strong></p>");

                        if (isClosed)
                        {
                            html.AppendLine("                <div class=\"entry-form-closed-msg\">");
                            html.AppendLine("                    <p>&#128683; This form is now closed for entries.</p>");
                            html.AppendLine("                </div>");
                        }
                        else
                        {
                            // Fillable form with print-based submission
                            if (form.Fields.Count != 0)
                            {
                                html.AppendLine($"                <form class=\"entry-form\" id=\"{formId}-form\" onsubmit=\"return handleEntrySubmit(this)\">");
                                html.AppendLine("                    <p style=\"margin-bottom:1rem;color:var(--text-secondary, #64748B);\"><em>Fill in the form below and click Submit to log your entry.</em></p>");
                                html.AppendLine($"                    <input type=\"hidden\" name=\"_formId\" value=\"{formId}\">");

                                foreach (var field in form.Fields.OrderBy(f => f.SortOrder))
                                {
                                    var fieldId = $"field-{field.Id:N}";
                                    var fieldName = Esc(field.Label);
                                    var requiredAttr = field.IsRequired ? " required" : "";
                                    var requiredStar = field.IsRequired ? " <span class=\"required\">*</span>" : "";
                                    var placeholder = !string.IsNullOrWhiteSpace(field.Placeholder) ? $" placeholder=\"{Esc(field.Placeholder)}\"" : "";

                                    html.AppendLine("                    <div class=\"form-group\">");

                                    switch (field.FieldType)
                                    {
                                        case "textarea":
                                            html.AppendLine($"                        <label for=\"{fieldId}\">{Esc(field.Label)}{requiredStar}</label>");
                                            html.AppendLine($"                        <textarea id=\"{fieldId}\" name=\"{fieldName}\" rows=\"4\"{placeholder}{requiredAttr}></textarea>");
                                            break;

                                        case "select":
                                            html.AppendLine($"                        <label for=\"{fieldId}\">{Esc(field.Label)}{requiredStar}</label>");
                                            html.AppendLine($"                        <select id=\"{fieldId}\" name=\"{fieldName}\"{requiredAttr}>");
                                            html.AppendLine("                            <option value=\"\">-- Select --</option>");
                                            if (!string.IsNullOrWhiteSpace(field.Options))
                                            {
                                                foreach (var opt in field.Options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                                    html.AppendLine($"                            <option value=\"{Esc(opt)}\">{Esc(opt)}</option>");
                                            }
                                            html.AppendLine("                        </select>");
                                            break;

                                        case "checkbox":
                                            html.AppendLine($"                        <label class=\"checkbox-label\"><input type=\"checkbox\" id=\"{fieldId}\" name=\"{fieldName}\"{requiredAttr}> {Esc(field.Label)}{requiredStar}</label>");
                                            break;

                                        default:
                                            var inputType = field.FieldType switch
                                            {
                                                "email" => "email",
                                                "phone" => "tel",
                                                "number" => "number",
                                                "date" => "date",
                                                _ => "text"
                                            };
                                            html.AppendLine($"                        <label for=\"{fieldId}\">{Esc(field.Label)}{requiredStar}</label>");
                                            html.AppendLine($"                        <input type=\"{inputType}\" id=\"{fieldId}\" name=\"{fieldName}\"{placeholder}{requiredAttr}>");
                                            break;
                                    }

                                    html.AppendLine("                    </div>");
                                }

                                var submitText = !string.IsNullOrWhiteSpace(form.SubmitButtonText) ? Esc(form.SubmitButtonText) : "Submit Entry";
                                html.AppendLine($"                    <button type=\"submit\" class=\"entry-form-submit\">{submitText}</button>");
                                html.AppendLine("                </form>");
                                html.AppendLine($"                <div class=\"entry-form-confirmation\" id=\"{formId}-confirm\" style=\"display:none;\">");
                                html.AppendLine("                    <p>&#9989; Your entry has been submitted successfully! The league secretary will review it shortly.</p>");
                                html.AppendLine("                </div>");

                                // Embed field labels as JSON for JS to read
                                var fieldLabels = form.Fields.OrderBy(f => f.SortOrder)
                                    .Select(f => $"{{\"id\":\"field-{f.Id:N}\",\"label\":\"{Esc(f.Label).Replace("\"", "\\\"")}\",\"type\":\"{f.FieldType}\"}}")
                                    .ToList();
                                html.AppendLine($"                <script>window.formFields = window.formFields || {{}}; window.formFields['{formId}'] = [{string.Join(",", fieldLabels)}];</script>");
                            }

                            // Contact information
                            var hasContact = !string.IsNullOrWhiteSpace(_settings.ContactEmail) || !string.IsNullOrWhiteSpace(_settings.ContactPhone);
                            if (hasContact)
                            {
                                html.AppendLine("                <div class=\"entry-form-contact\">");
                                html.AppendLine("                    <p><strong>&#128222; Contact the league secretary:</strong></p>");
                                if (!string.IsNullOrWhiteSpace(_settings.ContactPhone))
                                    html.AppendLine($"                    <p>Phone: <a href=\"tel:{Esc(_settings.ContactPhone)}\">{Esc(_settings.ContactPhone)}</a></p>");
                                if (!string.IsNullOrWhiteSpace(_settings.ContactEmail))
                                    html.AppendLine($"                    <p>Email: <a href=\"mailto:{Esc(_settings.ContactEmail)}\">{Esc(_settings.ContactEmail)}</a></p>");
                                html.AppendLine("                </div>");
                            }
                        }

                        html.AppendLine("            </div>");
                    }
                }
                else
                {
                    html.AppendLine("            <div class=\"section\">");
                    html.AppendLine("                <p class=\"empty-message\">No entry forms are currently available. Check back soon!</p>");
                    html.AppendLine("            </div>");
                }

                // Form submission JavaScript
                AppendEntryFormSubmitScript(html, "            ");
            });
        }

        /// <summary>
        /// Emits the handleEntrySubmit(form) script block.
        /// If a form service URL and API token are configured, submissions are sent to jsonbin.io;
        /// otherwise they save to localStorage.
        /// </summary>
        private void AppendEntryFormSubmitScript(StringBuilder html, string indent)
        {
            var serviceUrl = _settings.FormServiceUrl?.Trim() ?? "";
            var apiToken = _settings.FormServiceApiToken?.Trim() ?? "";
            var useService = !string.IsNullOrEmpty(serviceUrl) && !string.IsNullOrEmpty(apiToken);

            html.AppendLine($"{indent}<script>");
            html.AppendLine($"{indent}function handleEntrySubmit(form) {{");
            html.AppendLine($"{indent}    if (!form.checkValidity()) {{ form.reportValidity(); return false; }}");
            html.AppendLine($"{indent}    var formId = form.id.replace('-form', '');");
            html.AppendLine($"{indent}    var fields = window.formFields[formId] || [];");
            html.AppendLine($"{indent}    var values = {{}};");
            html.AppendLine($"{indent}    var entryName = '';");
            html.AppendLine($"{indent}    for (var i = 0; i < fields.length; i++) {{");
            html.AppendLine($"{indent}        var el = document.getElementById(fields[i].id);");
            html.AppendLine($"{indent}        if (!el) continue;");
            html.AppendLine($"{indent}        var val = fields[i].type === 'checkbox' ? (el.checked ? 'Yes' : 'No') : el.value;");
            html.AppendLine($"{indent}        values[fields[i].label] = val;");
            html.AppendLine($"{indent}        if (i === 0 && val) entryName = val;");
            html.AppendLine($"{indent}    }}");

            // Always save to localStorage as a backup
            html.AppendLine($"{indent}    try {{");
            html.AppendLine($"{indent}        var subs = JSON.parse(localStorage.getItem('wdpl2_submissions') || '[]');");
            html.AppendLine($"{indent}        subs.push({{ formId: formId, name: entryName, values: values, submittedAt: new Date().toISOString() }});");
            html.AppendLine($"{indent}        localStorage.setItem('wdpl2_submissions', JSON.stringify(subs));");
            html.AppendLine($"{indent}    }} catch(e) {{}}");

            if (useService)
            {
                // Submit to jsonbin.io: read current bin → append submission → write back
                html.AppendLine($"{indent}    var binUrl = '{EscJs(serviceUrl)}';");
                html.AppendLine($"{indent}    var apiKey = '{EscJs(apiToken)}';");
                html.AppendLine($"{indent}    var subId = Date.now().toString(36) + Math.random().toString(36).slice(2,7);");
                html.AppendLine($"{indent}    var newSub = {{ id: subId, formId: formId, name: entryName, values: values, submittedAt: new Date().toISOString() }};");
                html.AppendLine($"{indent}    var btn = form.querySelector('button[type=submit]');");
                html.AppendLine($"{indent}    if (btn) {{ btn.disabled = true; btn.textContent = 'Submitting\u2026'; }}");
                html.AppendLine($"{indent}    fetch(binUrl + '/latest', {{");
                html.AppendLine($"{indent}        headers: {{ 'X-Master-Key': apiKey, 'X-Bin-Meta': 'false' }}");
                html.AppendLine($"{indent}    }}).then(function(r) {{ return r.ok ? r.json() : []; }})");
                html.AppendLine($"{indent}    .then(function(arr) {{");
                html.AppendLine($"{indent}        if (!Array.isArray(arr)) arr = [];");
                html.AppendLine($"{indent}        arr.push(newSub);");
                html.AppendLine($"{indent}        return fetch(binUrl, {{");
                html.AppendLine($"{indent}            method: 'PUT',");
                html.AppendLine($"{indent}            headers: {{ 'Content-Type': 'application/json', 'X-Master-Key': apiKey }},");
                html.AppendLine($"{indent}            body: JSON.stringify(arr)");
                html.AppendLine($"{indent}        }});");
                html.AppendLine($"{indent}    }}).then(function(r) {{");
                html.AppendLine($"{indent}        if (r.ok) {{");
                html.AppendLine($"{indent}            var inputs = form.querySelectorAll('input,textarea,select');");
                html.AppendLine($"{indent}            for (var j = 0; j < inputs.length; j++) inputs[j].disabled = true;");
                html.AppendLine($"{indent}            if (btn) btn.style.display = 'none';");
                html.AppendLine($"{indent}            var confirmDiv = document.getElementById(formId + '-confirm');");
                html.AppendLine($"{indent}            if (confirmDiv) confirmDiv.style.display = 'block';");
                html.AppendLine($"{indent}        }} else {{");
                html.AppendLine($"{indent}            alert('Submission error. Please try again.');");
                html.AppendLine($"{indent}            if (btn) {{ btn.disabled = false; btn.textContent = 'Submit Entry'; }}");
                html.AppendLine($"{indent}        }}");
                html.AppendLine($"{indent}    }}).catch(function(err) {{");
                html.AppendLine($"{indent}        alert('Network error: ' + err.message);");
                html.AppendLine($"{indent}        if (btn) {{ btn.disabled = false; btn.textContent = 'Submit Entry'; }}");
                html.AppendLine($"{indent}    }});");
            }
            else
            {
                // No service configured — disable the form and show confirmation
                html.AppendLine($"{indent}    var inputs = form.querySelectorAll('input,textarea,select');");
                html.AppendLine($"{indent}    for (var j = 0; j < inputs.length; j++) inputs[j].disabled = true;");
                html.AppendLine($"{indent}    var btn = form.querySelector('button[type=submit]');");
                html.AppendLine($"{indent}    if (btn) btn.style.display = 'none';");
                html.AppendLine($"{indent}    var confirmDiv = document.getElementById(formId + '-confirm');");
                html.AppendLine($"{indent}    if (confirmDiv) confirmDiv.style.display = 'block';");
            }

            html.AppendLine($"{indent}    return false;");
            html.AppendLine($"{indent}}}");
            html.AppendLine($"{indent}</script>");
        }

        /// <summary>Escapes a string for safe use inside a JS string literal (single-quoted).</summary>
        private static string EscJs(string s) => s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");

        private string GenerateSubmissionsAdminPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();

            AppendDocumentHead(html, $"Submissions - {_settings.LeagueName}", season);
            html.AppendLine("<body>");

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);

            AppendHeader(html, season);
            AppendNavigation(html, "Submissions");

            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>&#128203; Submission Collection</h2>");
            html.AppendLine("                <p>Download pending form submissions to import into the league app</p>");
            html.AppendLine("            </div>");
            html.AppendLine("            <div class=\"section\">");
            html.AppendLine("                <div style=\"display:flex;gap:10px;align-items:center;margin-bottom:16px;flex-wrap:wrap;\">");
            html.AppendLine("                    <span id=\"sub-count\" style=\"font-size:1.1rem;font-weight:600;\"></span>");
            html.AppendLine("                    <button id=\"download-btn\" onclick=\"downloadSubmissions()\" style=\"padding:10px 20px;background:#10B981;color:#fff;border:none;border-radius:8px;font-size:14px;cursor:pointer;\" disabled>&#128229; Download JSON</button>");
            html.AppendLine("                    <button id=\"clear-btn\" onclick=\"clearSubmissions()\" style=\"padding:10px 20px;background:#EF4444;color:#fff;border:none;border-radius:8px;font-size:14px;cursor:pointer;\" disabled>&#128465; Clear All</button>");
            html.AppendLine("                </div>");
            html.AppendLine("                <div id=\"sub-list\"></div>");
            html.AppendLine("            </div>");
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");

            AppendFooter(html);

            html.AppendLine("    <script>");
            html.AppendLine("    function loadSubmissions() {");
            html.AppendLine("        var subs = JSON.parse(localStorage.getItem('wdpl2_submissions') || '[]');");
            html.AppendLine("        var countEl = document.getElementById('sub-count');");
            html.AppendLine("        var listEl = document.getElementById('sub-list');");
            html.AppendLine("        var dlBtn = document.getElementById('download-btn');");
            html.AppendLine("        var clrBtn = document.getElementById('clear-btn');");
            html.AppendLine("        countEl.textContent = subs.length + ' pending submission' + (subs.length !== 1 ? 's' : '');");
            html.AppendLine("        dlBtn.disabled = subs.length === 0;");
            html.AppendLine("        clrBtn.disabled = subs.length === 0;");
            html.AppendLine("        if (subs.length === 0) { listEl.innerHTML = '<p style=\"color:#94A3B8;\">No pending submissions. Entries submitted on the forms pages will appear here.</p>'; return; }");
            html.AppendLine("        var html = '<table style=\"width:100%;border-collapse:collapse;\">';");
            html.AppendLine("        html += '<thead><tr style=\"background:#F1F5F9;\"><th style=\"padding:8px;text-align:left;\">Name</th><th style=\"padding:8px;text-align:left;\">Form</th><th style=\"padding:8px;text-align:left;\">Date</th></tr></thead><tbody>';");
            html.AppendLine("        for (var i = 0; i < subs.length; i++) {");
            html.AppendLine("            var d = new Date(subs[i].submittedAt);");
            html.AppendLine("            html += '<tr style=\"border-bottom:1px solid #E2E8F0;\"><td style=\"padding:8px;\">' + (subs[i].name || '(unnamed)') + '</td><td style=\"padding:8px;font-size:0.85em;color:#64748B;\">' + (subs[i].formId || '') + '</td><td style=\"padding:8px;font-size:0.85em;color:#64748B;\">' + d.toLocaleDateString() + ' ' + d.toLocaleTimeString() + '</td></tr>';");
            html.AppendLine("        }");
            html.AppendLine("        html += '</tbody></table>';");
            html.AppendLine("        listEl.innerHTML = html;");
            html.AppendLine("    }");
            html.AppendLine("    function downloadSubmissions() {");
            html.AppendLine("        var subs = localStorage.getItem('wdpl2_submissions') || '[]';");
            html.AppendLine("        var blob = new Blob([subs], { type: 'application/json' });");
            html.AppendLine("        var a = document.createElement('a');");
            html.AppendLine("        a.href = URL.createObjectURL(blob);");
            html.AppendLine("        a.download = 'wdpl2-submissions.json';");
            html.AppendLine("        document.body.appendChild(a); a.click(); document.body.removeChild(a);");
            html.AppendLine("        URL.revokeObjectURL(a.href);");
            html.AppendLine("    }");
            html.AppendLine("    function clearSubmissions() {");
            html.AppendLine("        if (!confirm('Clear all pending submissions? Make sure you have downloaded them first.')) return;");
            html.AppendLine("        localStorage.removeItem('wdpl2_submissions');");
            html.AppendLine("        loadSubmissions();");
            html.AppendLine("    }");
            html.AppendLine("    loadSubmissions();");
            html.AppendLine("    </script>");
            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        private string GenerateHistoryPage(Season season, WebsiteTemplate template)
        {
            return GenerateFullPage($"{_settings.HistoryPageTitle} - {_settings.LeagueName}", season, "History", html =>
            {
                html.AppendLine("            <div class=\"hero\">");
                html.AppendLine($"                <h2>&#127942; {Esc(_settings.HistoryPageTitle)}</h2>");
                html.AppendLine("                <p class=\"hero-dates\">Historic winners and runners-up</p>");
                html.AppendLine("            </div>");

                // Group honours by season, preserving import order
                var seasons = _settings.HistoricHonours
                    .GroupBy(h => h.Season)
                    .OrderByDescending(g => g.Key)
                    .ToList();

                if (seasons.Count > 0)
                {
                    foreach (var group in seasons)
                    {
                        html.AppendLine("            <div class=\"section\">");
                        html.AppendLine($"                <h3>{Esc(group.Key)}</h3>");
                        html.AppendLine($"                <table class=\"{GetTableClasses()}\">");
                        html.AppendLine("                    <thead><tr>");
                        html.AppendLine("                        <th>Competition</th>");
                        html.AppendLine("                        <th>Winner</th>");
                        html.AppendLine("                        <th>Runner-Up</th>");
                        html.AppendLine("                    </tr></thead>");
                        html.AppendLine("                    <tbody>");

                        foreach (var honour in group.OrderBy(h => h.SortOrder))
                        {
                            html.AppendLine("                    <tr>");
                            html.AppendLine($"                        <td><strong>{Esc(honour.Title)}</strong></td>");
                            html.AppendLine($"                        <td>{Esc(honour.Winner)}</td>");
                            html.AppendLine($"                        <td>{Esc(honour.RunnerUp)}</td>");
                            html.AppendLine("                    </tr>");
                        }

                        html.AppendLine("                    </tbody>");
                        html.AppendLine("                </table>");
                        html.AppendLine("            </div>");
                    }
                }
                else
                {
                    html.AppendLine("            <div class=\"section\">");
                    html.AppendLine("                <p class=\"empty-message\">No historic honours data available yet.</p>");
                    html.AppendLine("            </div>");
                }
            });
        }

        private string GenerateCustomPage(Season season, WebsiteTemplate template, CustomPage page)
        {
            var html = new StringBuilder();
            
            AppendDocumentHead(html, $"{page.Title} - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, page.Title);
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>{page.Title}</h2>");
            html.AppendLine("            </div>");
            html.AppendLine("            <div class=\"section content-section\">");
            html.AppendLine($"                {page.Content}");
            html.AppendLine("            </div>");
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private string GenerateSitemap(List<string> pages)
        {
            var xml = new StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
            
            foreach (var page in pages.Where(p => p.EndsWith(".html")))
            {
                xml.AppendLine("  <url>");
                xml.AppendLine($"    <loc>{page}</loc>");
                xml.AppendLine($"    <lastmod>{DateTime.Now:yyyy-MM-dd}</lastmod>");
                xml.AppendLine("  </url>");
            }
            
            xml.AppendLine("</urlset>");
            return xml.ToString();
        }
        
        private string GetPlayerPageFileName(Guid playerId)
        {
            return $"player-{playerId:N}.html";
        }
        
        private string GeneratePlayerPage(Season season, WebsiteTemplate template, Player player, List<Team> teams, List<Fixture> fixtures, List<Player> allPlayers, PlayerStat stats)
        {
            var html = new StringBuilder();
            var team = player.TeamId.HasValue ? teams.FirstOrDefault(t => t.Id == player.TeamId.Value) : null;
            
            AppendDocumentHead(html, $"{stats.PlayerName} - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Players");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            
            // Player header
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>&#127942; {stats.PlayerName}</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{team?.Name ?? "No Team"}</p>");
            html.AppendLine("            </div>");
            
            // Player stats summary
            html.AppendLine("            <div class=\"stats-grid\" style=\"grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));\">");
            
            html.AppendLine("                <div class=\"stat-card\">");
            html.AppendLine($"                    <div class=\"stat-number\">{stats.Played}</div>");
            html.AppendLine("                    <div class=\"stat-label\">Played</div>");
            html.AppendLine("                </div>");
            
            html.AppendLine("                <div class=\"stat-card\">");
            html.AppendLine($"                    <div class=\"stat-number\">{stats.Won}</div>");
            html.AppendLine("                    <div class=\"stat-label\">Won</div>");
            html.AppendLine("                </div>");
            
            html.AppendLine("                <div class=\"stat-card\">");
            html.AppendLine($"                    <div class=\"stat-number\">{stats.Lost}</div>");
            html.AppendLine("                    <div class=\"stat-label\">Lost</div>");
            html.AppendLine("                </div>");
            
            html.AppendLine("                <div class=\"stat-card\">");
            html.AppendLine($"                    <div class=\"stat-number\">{stats.WinPercentage:F1}%</div>");
            html.AppendLine("                    <div class=\"stat-label\">Win %</div>");
            html.AppendLine("                </div>");
            
            if (stats.EightBalls > 0)
            {
                html.AppendLine("                <div class=\"stat-card\">");
                html.AppendLine($"                    <div class=\"stat-number\">{stats.EightBalls}</div>");
                html.AppendLine("                    <div class=\"stat-label\">8-Balls</div>");
                html.AppendLine("                </div>");
            }
            
            html.AppendLine("                <div class=\"stat-card\">");
            html.AppendLine($"                    <div class=\"stat-number\">{stats.Rating}</div>");
            html.AppendLine("                    <div class=\"stat-label\">Rating</div>");
            html.AppendLine("                </div>");

            // Form badge - last 5 results
            var formStr = GetLeaguePlayerForm(player.Id, fixtures, 5);
            if (!string.IsNullOrEmpty(formStr))
            {
                html.AppendLine("                <div class=\"stat-card\">");
                html.Append("                    <div class=\"stat-number\" style=\"font-size:1.1rem;letter-spacing:4px;\">");
                foreach (var ch in formStr)
                {
                    var color = ch == 'W' ? "#10B981" : "#EF4444";
                    html.Append($"<span style=\"color:{color};font-weight:bold;\">{ch}</span>");
                }
                html.AppendLine("</div>");
                html.AppendLine("                    <div class=\"stat-label\">Recent Form</div>");
                html.AppendLine("                </div>");
            }
            
            html.AppendLine("            </div>");
            
            // Match history
            var playerHistory = GetPlayerMatchHistory(player.Id, fixtures, teams, allPlayers);
            
            if (playerHistory.Count != 0)
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <h3>&#128203; Full Record</h3>");
                html.AppendLine("                <div class=\"table-responsive\">");
                html.AppendLine($"                <table class=\"{GetTableClasses()}\">");

                html.AppendLine("                    <thead>");
                html.AppendLine("                        <tr>");
                html.AppendLine("                            <th>Date</th>");
                html.AppendLine("                            <th>Opponent</th>");
                html.AppendLine("                            <th>Team</th>");
                html.AppendLine("                            <th>Result</th>");
                html.AppendLine("                        </tr>");
                html.AppendLine("                    </thead>");
                html.AppendLine("                    <tbody>");
                
                foreach (var record in playerHistory.OrderByDescending(r => r.Date))
                {
                    var resultClass = record.Won ? "text-positive" : "text-negative";
                    var resultText = record.Won ? "Won" : "Lost";
                    if (record.EightBall)
                        resultText += " (8-ball)";
                    
                    // Make opponent name clickable
                    var opponentLink = record.OpponentId != Guid.Empty
                        ? $"<a href=\"{GetPlayerPageFileName(record.OpponentId)}\" class=\"player-link\">{record.OpponentName}</a>"
                        : record.OpponentName;
                    
                    html.AppendLine("                        <tr>");
                    html.AppendLine($"                            <td>{record.Date:dd MMM yyyy}</td>");
                    html.AppendLine($"                            <td>{opponentLink}</td>");
                    html.AppendLine($"                            <td>{record.OpponentTeamName}</td>");
                    html.AppendLine($"                            <td class=\"{resultClass}\"><strong>{resultText}</strong></td>");
                    html.AppendLine("                        </tr>");
                }
                
                html.AppendLine("                    </tbody>");
                html.AppendLine("                </table>");
                html.AppendLine("                </div>");
                html.AppendLine("            </div>");
            }
            
            // Back to players link
            html.AppendLine("            <div class=\"section\" style=\"text-align: center;\">");
            html.AppendLine("                <a href=\"players.html\" class=\"back-link\">&#8592; Back to All Players</a>");
            html.AppendLine("            </div>");
            
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private List<PlayerMatchRecord> GetPlayerMatchHistory(Guid playerId, List<Fixture> fixtures, List<Team> teams, List<Player> allPlayers)
        {
            var records = new List<PlayerMatchRecord>();
            var teamById = teams.ToDictionary(t => t.Id, t => t);
            var playerById = allPlayers.ToDictionary(p => p.Id, p => p);
            
            foreach (var fixture in fixtures.Where(f => f.Frames.Count != 0 && f.Frames.Any(fr => fr.Winner != FrameWinner.None)))
            {
                foreach (var frame in fixture.Frames.Where(f => f.Winner != FrameWinner.None))
                {
                    Guid? opponentId = null;
                    bool isPlayer = false;
                    bool won = false;
                    bool eightBall = false;
                    
                    if (frame.HomePlayerId == playerId)
                    {
                        isPlayer = true;
                        opponentId = frame.AwayPlayerId;
                        won = frame.Winner == FrameWinner.Home;
                        eightBall = frame.EightBall && won;
                    }
                    else if (frame.AwayPlayerId == playerId)
                    {
                        isPlayer = true;
                        opponentId = frame.HomePlayerId;
                        won = frame.Winner == FrameWinner.Away;
                        eightBall = frame.EightBall && won;
                    }
                    
                    if (isPlayer && opponentId.HasValue)
                    {
                        var opponent = playerById.GetValueOrDefault(opponentId.Value);
                        var opponentTeam = opponent?.TeamId.HasValue == true 
                            ? teamById.GetValueOrDefault(opponent.TeamId.Value) 
                            : null;
                        
                        records.Add(new PlayerMatchRecord
                        {
                            Date = fixture.Date,
                            OpponentId = opponentId.Value,
                            OpponentName = opponent != null 
                                ? (opponent.FullName ?? $"{opponent.FirstName} {opponent.LastName}".Trim())
                                : "Unknown",
                            OpponentTeamName = opponentTeam?.Name ?? "",
                            Won = won,
                            EightBall = eightBall
                        });
                    }
                }
            }
            
            return records;
        }
        
        private sealed class PlayerMatchRecord
        {
            public DateTime Date { get; set; }
            public Guid OpponentId { get; set; }
            public string OpponentName { get; set; } = "";
            public string OpponentTeamName { get; set; } = "";
            public bool Won { get; set; }
            public bool EightBall { get; set; }
        }

        /// <summary>
        /// Build a form string (last N league frame results) for a player. W = win, L = loss.
        /// </summary>
        private static string GetLeaguePlayerForm(Guid playerId, List<Fixture> fixtures, int count)
        {
            var results = new List<char>();
            foreach (var fixture in fixtures.OrderBy(f => f.Date))
            {
                foreach (var frame in fixture.Frames)
                {
                    if (frame.HomePlayerId == playerId && frame.Winner != FrameWinner.None)
                        results.Add(frame.Winner == FrameWinner.Home ? 'W' : 'L');
                    else if (frame.AwayPlayerId == playerId && frame.Winner != FrameWinner.None)
                        results.Add(frame.Winner == FrameWinner.Away ? 'W' : 'L');
                }
            }
            return string.Concat(results.TakeLast(count));
        }
        

        #region Missing Methods
        
        private string GenerateGalleryPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var imageOptimizer = new ImageOptimizationService();
            
            AppendDocumentHead(html, $"{_settings.GalleryPageTitle} - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Gallery");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>&#128247; {Esc(_settings.GalleryPageTitle)}</h2>");
            html.AppendLine("            </div>");

            var images = _settings.GalleryImages.OrderBy(i => i.SortOrder).ToList();
            var categories = images.Select(i => i.Category).Distinct().ToList();
            
            if (_settings.GalleryShowCategories && categories.Count > 1)
            {
                html.AppendLine("            <div class=\"gallery-categories\">");
                html.AppendLine("                <button class=\"category-btn active\" data-category=\"all\">All</button>");
                foreach (var category in categories)
                {
                    html.AppendLine($"                <button class=\"category-btn\" data-category=\"{category.ToLower().Replace(" ", "-")}\">{category}</button>");
                }
                html.AppendLine("            </div>");
            }
            
            html.AppendLine($"            <div class=\"gallery-grid gallery-{_settings.GalleryLayout}\" style=\"--gallery-columns: {_settings.GalleryColumns};\">");
            
            foreach (var image in images)
            {
                if (image.ImageData.Length == 0) continue;
                
                var mimeType = imageOptimizer.GetMimeType(image.FileName);
                var dataUrl = imageOptimizer.ToDataUrl(image.ImageData, mimeType);
                var categoryClass = image.Category.ToLower().Replace(" ", "-");
                
                html.AppendLine($"                <div class=\"gallery-item\" data-category=\"{categoryClass}\">");
                if (_settings.GalleryEnableLightbox)
                    html.AppendLine($"                    <a href=\"{dataUrl}\" class=\"lightbox-link\">");
                html.AppendLine($"                    <img src=\"{dataUrl}\" alt=\"{image.Caption}\" loading=\"lazy\">");
                if (_settings.GalleryEnableLightbox)
                    html.AppendLine("                    </a>");
                if (_settings.GalleryShowCaptions && !string.IsNullOrWhiteSpace(image.Caption))
                    html.AppendLine($"                    <p class=\"caption\">{image.Caption}</p>");
                html.AppendLine("                </div>");
            }
            
            html.AppendLine("            </div>");
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private List<PlayerStat> CalculatePlayerStats(List<Player> players, List<Team> teams, List<Fixture> fixtures)
        {
            var stats = new List<PlayerStat>();
            var settings = _leagueSettings;
            var teamById = teams.ToDictionary(t => t.Id, t => t);
            
            // Get season start date for rating calculation
            var seasonId = _settings.SelectedSeasonId;
            var season = seasonId.HasValue
                ? _league.Seasons.FirstOrDefault(s => s.Id == seasonId.Value)
                : _league.Seasons.FirstOrDefault(s => s.IsActive);
            var seasonStartDate = season?.StartDate ?? DateTime.Now.AddMonths(-6);
            
            // Use the shared RatingCalculator to get all player ratings
            var allRatings = RatingCalculator.CalculateAllRatings(
                fixtures,
                players,
                teams,
                settings,
                seasonStartDate);
            
            // Convert to PlayerStat format
            foreach (var kvp in allRatings)
            {
                var ratingStats = kvp.Value;
                stats.Add(new PlayerStat
                {
                    PlayerId = ratingStats.PlayerId,
                    PlayerName = ratingStats.PlayerName,
                    TeamName = ratingStats.TeamName,
                    DivisionId = ratingStats.DivisionId,
                    Played = ratingStats.Played,
                    Won = ratingStats.Wins,
                    Lost = ratingStats.Losses,
                    EightBalls = ratingStats.EightBalls,
                    Rating = ratingStats.Rating
                });
            }
            
            return stats;
        }
        
        private static DateTime GetWeekStart(DateTime date)
        {
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date;
        }
        
        #endregion
        
        #region Player Stats Class
    
        private sealed class PlayerStat
        {
            public Guid PlayerId { get; set; }
            public string PlayerName { get; set; } = "";
            public string TeamName { get; set; } = "";
            public Guid? DivisionId { get; set; }
            public int Played { get; set; }
            public int Won { get; set; }
            public int Lost { get; set; }
            public int EightBalls { get; set; }
            public int Rating { get; set; } = 1000;
            public double WinPercentage => Played > 0 ? (Won * 100.0 / Played) : 0;
        }
        
        #endregion
    }
    
    /// <summary>
    /// Helper class for grouping items with a single key
    /// </summary>
    internal sealed class SingleGrouping<TKey, TElement> : IGrouping<TKey, TElement>
    {
        private readonly TKey _key;
        private readonly List<TElement> _elements;
        
        public SingleGrouping(TKey key, List<TElement> elements)
        {
            _key = key;
            _elements = elements;
        }
        
        public TKey Key => _key;
        
        public IEnumerator<TElement> GetEnumerator() => _elements.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
