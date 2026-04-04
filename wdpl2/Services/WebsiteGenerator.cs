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

        /// <summary>Effective league settings resolved for the website's selected season.</summary>
        private AppSettings _leagueSettings = null!;

        public WebsiteGenerator(LeagueData league, WebsiteSettings settings)
        {
            _league = league;
            _settings = settings;
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
            
            // Generate files based on template
            var template = WebsiteTemplate.GetTemplateById(_settings.SelectedTemplate) ?? WebsiteTemplate.Modern;
            
            // Core files
            files["index.html"] = GenerateIndexPage(season, template);
            files["style.css"] = GenerateStylesheet(template);
            
            // Optional pages
            if (_settings.ShowStandings)
                files["standings.html"] = GenerateStandingsPage(season, template);
            
            if (_settings.ShowFixtures)
                files["fixtures.html"] = GenerateFixturesPage(season, template);
            
            if (_settings.ShowResults)
                files["results.html"] = GenerateResultsPage(season, template);
            
            if (_settings.ShowPlayerStats)
            {
                files["players.html"] = GeneratePlayersPage(season, template);
                
                // Generate JSON data file and single template page (instead of individual HTML files per player)
                var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
                var jsonGenerator = new WebsiteJsonDataGenerator(_league, _settings, _leagueSettings);
                var templateGenerator = new WebsiteTemplatePageGenerator(_settings);

                files["players-data.json"] = jsonGenerator.GeneratePlayersJson(players, teams, fixtures);
                files["player.html"] = templateGenerator.GeneratePlayerTemplatePage(
                    season,
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
                var (divisions2, venues2, teams2, players2, fixtures2) = _league.GetSeasonData(season.Id);
                var jsonGenerator = new WebsiteJsonDataGenerator(_league, _settings, _leagueSettings);
                var templateGenerator = new WebsiteTemplatePageGenerator(_settings);

                files["teams-data.json"] = jsonGenerator.GenerateTeamsJson(teams2, divisions2, venues2, players2, fixtures2);
                files["team.html"] = templateGenerator.GenerateTeamTemplatePage(
                    season,
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

            // Add UK 8-Ball Pool Game
            if (_settings.ShowPoolGame)
                files["pool-game.html"] = PoolGameGenerator.GeneratePoolGameHtml(_settings.LeagueName);
            
            // Custom pages
            foreach (var page in _settings.CustomPages.Where(p => p.IsPublished))
            {
                var slug = string.IsNullOrWhiteSpace(page.Slug) ? page.Title.ToLower().Replace(" ", "-") : page.Slug;
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
            
            AppendDocumentHead(html, $"{_settings.LeagueName} - {season.Name}", season);
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

            var canvasHeight = blocks.Count != 0
                ? blocks.Max(b => b.TopPx + (b.HeightPx > 0 ? b.HeightPx : 350)) + 100
                : 800;

            html.AppendLine($"    <div class=\"page-canvas\" style=\"min-height:{canvasHeight.ToString("F0", inv)}px;\">");
            
            foreach (var block in blocks)
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
                    case "footer":
                        AppendFooterBlock(html, allAttrs);
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
                    AppendHomeWelcomeSection(html, season);
                    break;
                case "quick-stats":
                    AppendHomeQuickStatsSection(html, teams, players, divisions, completedFixtures);
                    break;
                case "league-leaders":
                    AppendHomeLeagueLeadersSection(html, players, teams, fixtures);
                    break;
                case "recent-results":
                    AppendHomeRecentResultsSection(html, teams, fixtures, completedFixtures);
                    break;
                case "upcoming-fixtures":
                    AppendHomeUpcomingFixturesSection(html, teams, venues, fixtures);
                    break;
                case "latest-news":
                    if (_settings.HomeShowLatestNews && _settings.ShowNews)
                        AppendHomeLatestNewsSection(html);
                    break;
                case "sponsors":
                    if (_settings.HomeShowSponsors && _settings.ShowSponsors && _settings.Sponsors.Any(s => s.IsActive))
                        AppendSponsorsSection(html);
                    break;
            }
        }
        
        private void AppendHomeWelcomeSection(StringBuilder html, Season season)
        {
            if (!_settings.HomeShowWelcomeSection) return;
            
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>Welcome to {season.Name}</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{season.StartDate:MMMM d, yyyy} - {season.EndDate:MMMM d, yyyy}</p>");
            if (!string.IsNullOrWhiteSpace(_settings.WelcomeMessage))
            {
                html.AppendLine($"                <p class=\"welcome-text\">{_settings.WelcomeMessage}</p>");
            }
            html.AppendLine("            </div>");
        }
        
        private void AppendHomeQuickStatsSection(StringBuilder html, List<Team> teams, List<Player> players, List<Division> divisions, int completedFixtures)
        {
            if (!_settings.HomeShowQuickStats) return;
            
            var statColumns = _settings.StatsColumns;
            html.AppendLine($"            <div class=\"stats-grid\" style=\"grid-template-columns: repeat(auto-fit, minmax(min({(statColumns == 2 ? "280px" : statColumns == 3 ? "200px" : "180px")}, 100%), 1fr));\">");
            html.AppendLine("                <div class=\"stat-card\">");
            html.AppendLine($"                    <div class=\"stat-number\">{teams.Count}</div>");
            html.AppendLine("                    <div class=\"stat-label\">Teams</div>");
            html.AppendLine("                </div>");
            html.AppendLine("                <div class=\"stat-card\">");
            html.AppendLine($"                    <div class=\"stat-number\">{players.Count}</div>");
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
            if (!(_settings.HomeShowLeagueLeaders && _settings.ShowTopScorers)) return;
            
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
            foreach (var player in topPlayers)
            {
                var medal = rank switch { 1 => "&#129351;", 2 => "&#129352;", 3 => "&#129353;", _ => $"#{rank}" };
                html.AppendLine("                    <div class=\"leader-item\">");
                html.AppendLine($"                        <span class=\"rank\">{medal}</span>");
                html.AppendLine($"                        <span class=\"player-name\">{player.PlayerName}</span>");
                html.AppendLine($"                        <span class=\"player-team\">{player.TeamName}</span>");
                html.AppendLine($"                        <span class=\"player-stat\">{player.Rating}</span>");
                html.AppendLine("                    </div>");
                rank++;
            }
            html.AppendLine("                </div>");
            if (_settings.ShowPlayerStats)
                html.AppendLine("                <p class=\"view-all\"><a href=\"players.html\">View All Players &#8594;</a></p>");
            html.AppendLine("            </section>");
        }
        
        private void AppendHomeRecentResultsSection(StringBuilder html, List<Team> teams, List<Fixture> fixtures, int completedFixtures)
        {
            if (!(_settings.HomeShowRecentResults && _settings.ShowResults && completedFixtures > 0)) return;
            
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
            if (!(_settings.HomeShowUpcomingFixtures && _settings.ShowFixtures)) return;
            
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

        private string GenerateStandingsPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            
            AppendDocumentHead(html, $"Standings - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Standings");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>League Standings</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{season.Name}</p>");
            html.AppendLine("            </div>");
            
            foreach (var division in divisions.OrderBy(d => d.Name))
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

                foreach (var standing in sortedStandings)
                {
                    var rowClass = "";
                    if (_settings.StandingsHighlightTop && position <= _settings.StandingsHighlightTopCount)
                        rowClass = "highlight-top";
                    else if (_settings.StandingsHighlightBottom && position > totalTeams - _settings.StandingsHighlightBottomCount)
                        rowClass = "highlight-bottom";
                    
                    html.AppendLine($"                        <tr{(string.IsNullOrEmpty(rowClass) ? "" : $" class=\"{rowClass}\"")} >");
                    
                    if (_settings.StandingsShowPosition)
                    {
                        var posDisplay = position.ToString();
                        if (_settings.StandingsShowMedals && position <= 3)
                        {
                            posDisplay = position switch { 1 => "&#129351;", 2 => "&#129352;", 3 => "&#129353;", _ => posDisplay };
                        }
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
                    position++;
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
            
            AppendDocumentHead(html, $"Fixtures - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Fixtures");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>&#128197; Fixtures</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">Upcoming Matches</p>");
            html.AppendLine("            </div>");
            
            var upcomingFixtures = fixtures
                .Where(f => f.Date >= DateTime.Now && !f.Frames.Any(fr => fr.Winner != FrameWinner.None))
                .OrderBy(f => f.Date)
                .Take(_settings.FixturesPerPage);
            
            var groupedFixtures = _settings.FixturesGroupByWeek
                ? upcomingFixtures.GroupBy(f => GetWeekStart(f.Date)).ToList()
                : (_settings.FixturesGroupByDate
                    ? upcomingFixtures.GroupBy(f => f.Date.Date).ToList()
                    : new List<IGrouping<DateTime, Fixture>> { new SingleGrouping<DateTime, Fixture>(DateTime.Today, upcomingFixtures.ToList()) });
            
            if (groupedFixtures.Any(g => g.Any()))
            {
                foreach (var dateGroup in groupedFixtures)
                {
                    if (!dateGroup.Any()) continue;
                    
                    html.AppendLine("            <div class=\"section\">");
                    
                    if (_settings.FixturesGroupByWeek)
                        html.AppendLine($"                <h3>Week of {dateGroup.Key:dd MMMM yyyy}</h3>");
                    else if (_settings.FixturesGroupByDate)
                        html.AppendLine($"                <h3>{dateGroup.Key:dddd, dd MMMM yyyy}</h3>");
                    
                    html.AppendLine("                <div class=\"fixtures-list\">");
                    
                    foreach (var fixture in dateGroup.OrderBy(f => f.Date))
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
                    html.AppendLine("            </div>");
                }
            }
            else
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <p class=\"empty-message\">No upcoming fixtures scheduled.</p>");
                html.AppendLine("            </div>");
            }
            
            // Add printable fixtures sheet section if enabled
            if (_settings.FixturesShowPrintableSheet)
            {
                AppendFixturesSheetSection(html, season, divisions, venues, teams, fixtures);
            }

            // Add team calendar download section if enabled
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
        
        private void AppendFixturesSheetSection(StringBuilder html, Season season, List<Division> divisions, List<Venue> venues, List<Team> teams, List<Fixture> fixtures)
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
            
            // Get embeddable content and CSS
            var sheetContent = fixturesSheetGenerator.GenerateEmbeddableContent(season.Id);
            var sheetCss = fixturesSheetGenerator.GetEmbeddableCSS();
            var scopedCss = fixturesSheetGenerator.GetScopedCSS();

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
            html.AppendLine("            </style>");
            html.AppendLine($"            <div class=\"section fixtures-sheet-section{expandedClass}\">");
            html.AppendLine("                <div class=\"fixtures-sheet-header\" onclick=\"toggleFixturesSheet()\">");
            html.AppendLine($"                    <h3>&#128197; {sheetTitle}</h3>");
            html.AppendLine("                    <div class=\"fixtures-sheet-controls\">");
            html.AppendLine("                        <span class=\"toggle-icon\">&#9660;</span>");
            html.AppendLine("                    </div>");
            html.AppendLine("                </div>");
            html.AppendLine("                <div class=\"fixtures-sheet-content\">");
            html.AppendLine("                    <div class=\"fixtures-sheet-actions\">");
            html.AppendLine("                        <button class=\"btn-download\" onclick=\"downloadFixturesSheet()\">&#128229; Download HTML</button>");
            html.AppendLine("                        <button class=\"btn-print\" onclick=\"printFixturesSheet()\">&#128424; Print</button>");
            html.AppendLine("                    </div>");
            html.AppendLine("                    <div class=\"fixtures-sheet-wrapper\" id=\"fixtures-sheet-container\">");
            html.AppendLine(sheetContent);
            html.AppendLine("                    </div>");
            html.AppendLine("                </div>");
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
        
        private string GenerateResultsPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            
            AppendDocumentHead(html, $"Results - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Results");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>&#127937; Match Results</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">Latest Results</p>");
            html.AppendLine("            </div>");
            
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
        
        private string GeneratePlayersPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            var appSettings = _leagueSettings;

            AppendDocumentHead(html, $"Players - {_settings.LeagueName}", season);
            html.AppendLine("<body>");

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);

            AppendHeader(html, season);
            AppendNavigation(html, "Players");

            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>&#127942; Player Statistics</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{players.Count} Players</p>");
            html.AppendLine("            </div>");

            // Calculate all player stats (across all divisions, ratings need cross-division data)
            var allPlayerStats = CalculatePlayerStats(players, teams, fixtures);

            // Generate one table per division — mirrors the app's LeagueTablesPage exactly
            var orderedDivisions = divisions.OrderBy(d => d.Name).ToList();

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
                    foreach (var stat in filteredStats)
                    {
                        html.AppendLine("                        <tr>");
                        if (_settings.PlayersShowPosition)
                        {
                            var posDisplay = position <= 3 
                                ? (position == 1 ? "&#129351;" : position == 2 ? "&#129352;" : "&#129353;")
                                : position.ToString();
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
                        position++;
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
            
            AppendDocumentHead(html, $"Divisions - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Divisions");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>&#127941; Divisions</h2>");
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

            AppendDocumentHead(html, $"Rules - {_settings.LeagueName}", season);
            html.AppendLine("<body>");

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);

            AppendHeader(html, season);
            AppendNavigation(html, "Rules");

            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>&#128214; League Rules</h2>");
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
            
            AppendDocumentHead(html, $"Contact - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Contact");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>&#128231; Contact Us</h2>");
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
            
            AppendDocumentHead(html, $"Sponsors - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Sponsors");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>&#129309; Our Sponsors</h2>");
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
            
            AppendDocumentHead(html, $"News - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "News");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>&#128240; Latest News</h2>");
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

            return GenerateFullPage($"Rows Reports - {_settings.LeagueName}", season, "Rows Reports", html =>
            {
                html.AppendLine("            <div class=\"hero\">");
                html.AppendLine("                <h2>&#128221; Rows Reports</h2>");
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

            AppendDocumentHead(html, $"Competitions - {_settings.LeagueName}", season);
            html.AppendLine("<body>");

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);

            AppendHeader(html, season);
            AppendNavigation(html, "Competitions");

            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>&#127942; Competitions</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{_settings.LeagueName}</p>");
            html.AppendLine("            </div>");

            // Show ALL competitions across all seasons (tournaments are standalone events)
            var competitions = _league.Competitions
                .Where(c => c.Status != CompetitionStatus.Draft)
                .OrderByDescending(c => c.Status == CompetitionStatus.InProgress)
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
                for (int i = 0; i < competitions.Count; i++)
                {
                    var comp = competitions[i];
                    var display = i == 0 ? "block" : "none";

                    html.AppendLine($"            <div class=\"comp-panel\" id=\"comp-{i}\" style=\"display:{display}\">");

                    // Meta info bar
                    var formatLabel = comp.Format.ToString().Replace("Knockout", " Knockout").Replace("GroupStage", " Group Stage").Replace("RoundRobin", "Round Robin");
                    html.AppendLine($"                <div class=\"comp-info-bar\">");
                    html.AppendLine($"                    <span>&#127919; {formatLabel}</span>");
                    html.AppendLine($"                    <span>&#128101; {GetParticipantCount(comp)} entries</span>");
                    if (comp.StartDate.HasValue)
                        html.AppendLine($"                    <span>&#128197; {comp.StartDate.Value:dd MMM yyyy}</span>");
                    var compSeasonName = _league.Seasons.FirstOrDefault(s => s.Id == comp.SeasonId)?.Name;
                    if (!string.IsNullOrWhiteSpace(compSeasonName))
                        html.AppendLine($"                    <span>&#127921; {compSeasonName}</span>");
                    if (!string.IsNullOrWhiteSpace(comp.Notes))
                        html.AppendLine($"                    <span>&#128221; {comp.Notes}</span>");
                    html.AppendLine($"                </div>");

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
                html.AppendLine($"                    <div class=\"round-section\">");
                html.AppendLine($"                        <h4>{round.Name}{rrDateHtml}</h4>");
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

            html.AppendLine("                <div class=\"bk-scroll\">");
            html.AppendLine("                <div class=\"bk-grid\">");

            for (int ri = 0; ri < totalRounds; ri++)
            {
                var round = orderedRounds[ri];
                string label = ri == totalRounds - 1 ? "Final"
                    : ri == totalRounds - 2 ? "Semi-Finals"
                    : (round.Name ?? $"Round {round.RoundNumber}");
                int completed = round.Matches.Count(m => m.IsComplete);
                int total = round.Matches.Count;
                var progColor = completed == total && total > 0 ? "#10B981" : "#6B7280";

                var roundDateHtml = round.Date.HasValue
                    ? $"<div class=\"round-date\">{round.Date.Value:dd MMM yyyy}</div>"
                    : "";
                html.AppendLine("                    <div class=\"bk-round\">");
                html.AppendLine($"                        <div class=\"bk-hdr\"><div class=\"bk-rn\">{label}</div>{roundDateHtml}<div class=\"bk-rp\" style=\"color:{progColor}\">{completed}/{total}</div></div>");
                html.AppendLine("                        <div class=\"bk-body\">");
                foreach (var match in round.Matches)
                    AppendBracketCard(html, match, comp, players, teams);
                html.AppendLine("                        </div>");
                html.AppendLine("                    </div>");

                // Connector column between rounds
                if (ri < totalRounds - 1)
                {
                    int pairs = orderedRounds[ri + 1].Matches.Count;
                    html.AppendLine("                    <div class=\"bk-conn\">");
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
            var p1 = GetWebParticipantName(match.Participant1Id, comp, players, teams) ?? "TBD";
            var p2 = GetWebParticipantName(match.Participant2Id, comp, players, teams) ?? "TBD";
            bool p1w = match.WinnerId.HasValue && match.WinnerId == match.Participant1Id;
            bool p2w = match.WinnerId.HasValue && match.WinnerId == match.Participant2Id;

            var cardClass = match.IsComplete ? "bk-card bk-done" : "bk-card";
            html.AppendLine($"                        <div class=\"{cardClass}\">");
            html.AppendLine($"                            <div class=\"bk-player{(p1w ? " bk-w" : "")}{(p1 == "TBD" ? " bk-tbd" : "")}\">");
            html.AppendLine($"                                <span class=\"bk-name\">{p1}</span><span class=\"bk-sc{(p1w ? " bk-sw" : "")}\">{match.Participant1Score}</span>");
            html.AppendLine($"                            </div>");
            html.AppendLine($"                            <div class=\"bk-dv\"></div>");
            html.AppendLine($"                            <div class=\"bk-player{(p2w ? " bk-w" : "")}{(p2 == "TBD" ? " bk-tbd" : "")}\">");
            html.AppendLine($"                                <span class=\"bk-name\">{p2}</span><span class=\"bk-sc{(p2w ? " bk-sw" : "")}\">{match.Participant2Score}</span>");
            html.AppendLine($"                            </div>");
            html.AppendLine($"                        </div>");
        }

        private void AppendGroupStage(StringBuilder html, Competition comp, List<Player> players, List<Team> teams)
        {
            int topAdvance = comp.GroupSettings?.TopPlayersAdvance ?? 2;

            // Render previous group rounds first (archived)
            if (comp.PreviousGroups.Count > 0)
            {
                var previousRounds = comp.PreviousGroups
                    .GroupBy(g => g.GroupRound)
                    .OrderBy(r => r.Key);

                foreach (var round in previousRounds)
                {
                    // Infer how many advanced per group from the stored standings
                    // (the TopPlayersAdvance setting may have changed for later rounds)
                    int roundAdvance = round.Max(g => g.Standings.Count(s => s.Position > 0));
                    if (roundAdvance < 1) roundAdvance = topAdvance;

                    html.AppendLine($"                <h3 class=\"group-round-title\">Group Round {round.Key}</h3>");
                    html.AppendLine("                <div class=\"comp-groups\">");
                    foreach (var group in round.OrderBy(g => g.GroupNumber))
                    {
                        AppendGroupSection(html, group, comp, players, teams, roundAdvance, hasSelections: true);
                    }
                    html.AppendLine("                </div>");
                }
            }

            // Current groups
            int currentRound = comp.Groups.Count > 0 ? comp.Groups.Max(g => g.GroupRound) : 1;
            bool hasSelections = comp.Groups.Any(g => g.Standings.Any(s => s.Position > 0));

            if (comp.PreviousGroups.Count > 0)
            {
                var currentGroupDate = comp.GroupSettings?.GroupDate;
                var currentGroupDateHtml = currentGroupDate.HasValue
                    ? $" <span class=\"round-date\">{currentGroupDate.Value:dd MMM yyyy}</span>"
                    : "";
                html.AppendLine($"                <h3 class=\"group-round-title\">Group Round {currentRound}{currentGroupDateHtml}</h3>");
            }
            else if (comp.GroupSettings?.GroupDate.HasValue == true)
            {
                html.AppendLine($"                <p class=\"round-date\" style=\"margin-bottom:8px\">{comp.GroupSettings.GroupDate.Value:dd MMM yyyy}</p>");
            }

            html.AppendLine("                <div class=\"comp-groups\">");
            foreach (var group in comp.Groups.OrderBy(g => g.GroupNumber))
            {
                AppendGroupSection(html, group, comp, players, teams, topAdvance, hasSelections);
            }
            html.AppendLine("                </div>");
        }

        private void AppendGroupSection(StringBuilder html, CompetitionGroup group, Competition comp,
            List<Player> players, List<Team> teams, int topAdvance, bool hasSelections)
        {
            html.AppendLine($"                    <div class=\"group-section\">");
            html.AppendLine($"                        <h4>{group.Name} <span class=\"group-count\">({group.ParticipantIds.Count} players)</span></h4>");
            if (!string.IsNullOrEmpty(group.VenueDisplay))
                html.AppendLine($"                        <p class=\"group-venue\">&#128205; {group.VenueDisplay}</p>");
            html.AppendLine($"                        <div class=\"group-players\">");

            foreach (var pid in group.ParticipantIds)
            {
                var name = GetWebParticipantName(pid, comp, players, teams);

                var standing = group.Standings.FirstOrDefault(s => s.ParticipantId == pid);
                bool isAdvancing = standing != null && standing.Position > 0 && standing.Position <= topAdvance;
                bool isNoShow = comp.NoShowIds.Contains(pid);
                bool isEliminated = hasSelections && !isAdvancing && !isNoShow;

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

                html.AppendLine($"                            <div class=\"{cssClass}\">{name}{badge}</div>");
            }

            html.AppendLine($"                        </div>");
            html.AppendLine($"                    </div>");
        }

        private void AppendMatchRow(StringBuilder html, CompetitionMatch match, Competition comp, List<Player> players, List<Team> teams)
        {
            var p1 = GetWebParticipantName(match.Participant1Id, comp, players, teams);
            var p2 = GetWebParticipantName(match.Participant2Id, comp, players, teams);
            var isP1Winner = match.WinnerId.HasValue && match.WinnerId == match.Participant1Id;
            var isP2Winner = match.WinnerId.HasValue && match.WinnerId == match.Participant2Id;

            html.AppendLine($"                        <div class=\"match-row{(match.IsComplete ? " match-complete" : "")}\">");
            html.AppendLine($"                            <span class=\"match-player{(isP1Winner ? " winner" : "")}\">{p1 ?? "TBD"}</span>");
            html.AppendLine($"                            <span class=\"match-score\">{match.Participant1Score} - {match.Participant2Score}</span>");
            html.AppendLine($"                            <span class=\"match-player{(isP2Winner ? " winner" : "")}\">{p2 ?? "TBD"}</span>");
            html.AppendLine($"                        </div>");
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
            
            AppendDocumentHead(html, $"Gallery - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Gallery");
            
            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>&#128247; Photo Gallery</h2>");
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
