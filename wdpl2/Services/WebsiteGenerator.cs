using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    /// <summary>
    /// Generates static HTML websites from league data
    /// </summary>
    public sealed class WebsiteGenerator
    {
        private readonly LeagueData _league;
        private readonly WebsiteSettings _settings;
        
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
                var jsonGenerator = new WebsiteJsonDataGenerator(_league, _settings);
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
                var jsonGenerator = new WebsiteJsonDataGenerator(_league, _settings);
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
            
            if (_settings.ShowGallery && _settings.GalleryImages.Count > 0)
                files["gallery.html"] = GenerateGalleryPage(season, template);
            
            if (_settings.ShowRules && !string.IsNullOrWhiteSpace(_settings.RulesContent))
                files["rules.html"] = GenerateRulesPage(season, template);
            
            if (_settings.ShowContactPage && _settings.HasContactInfo)
                files["contact.html"] = GenerateContactPage(season, template);
            
            if (_settings.ShowSponsors && _settings.Sponsors.Count > 0)
                files["sponsors.html"] = GenerateSponsorsPage(season, template);
            
            if (_settings.ShowNews && _settings.NewsItems.Count > 0)
                files["news.html"] = GenerateNewsPage(season, template);
            
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
            
            AppendDocumentHead(html, $"{_settings.LeagueName} - {season.Name}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Home");
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            
            // Welcome Section
            if (_settings.HomeShowWelcomeSection)
            {
                html.AppendLine("            <div class=\"hero\">");
                html.AppendLine($"                <h2>Welcome to {season.Name}</h2>");
                html.AppendLine($"                <p class=\"hero-dates\">{season.StartDate:MMMM d, yyyy} - {season.EndDate:MMMM d, yyyy}</p>");
                if (!string.IsNullOrWhiteSpace(_settings.WelcomeMessage))
                {
                    html.AppendLine($"                <p class=\"welcome-text\">{_settings.WelcomeMessage}</p>");
                }
                html.AppendLine("            </div>");
            }
            
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            var completedFixtures = fixtures.Count(f => f.Frames.Any(fr => fr.Winner != FrameWinner.None));
            
            // Quick Stats
            if (_settings.HomeShowQuickStats)
            {
                var statColumns = _settings.StatsColumns;
                html.AppendLine($"            <div class=\"stats-grid\" style=\"grid-template-columns: repeat(auto-fit, minmax({(statColumns == 2 ? "280px" : statColumns == 3 ? "200px" : "180px")}, 1fr));\">");
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
            
            // League Leaders
            if (_settings.HomeShowLeagueLeaders && _settings.ShowTopScorers)
            {
                var playerStats = CalculatePlayerStats(players, teams, fixtures);
                
                // Apply filter based on settings
                int minFramesRequired = 0;
                if (_settings.PlayersUsePercentageFilter && _settings.PlayersMinFramesPercentage > 0)
                {
                    // Calculate max frames available in the season
                    var maxFrames = playerStats.Any() ? playerStats.Max(p => p.Played) : 0;
                    minFramesRequired = (int)Math.Ceiling(maxFrames * (_settings.PlayersMinFramesPercentage / 100.0));
                }
                else
                {
                    minFramesRequired = _settings.PlayersMinGames;
                }
                
                var topPlayers = playerStats
                    .Where(p => p.Played >= minFramesRequired)
                    .OrderByDescending(s => s.WinPercentage)
                    .ThenByDescending(s => s.Won)
                    .Take(_settings.HomeLeagueLeadersCount)
                    .ToList();
                
                if (topPlayers.Any())
                {
                    html.AppendLine("            <section class=\"section\">");
                    html.AppendLine("                <h3>?? League Leaders</h3>");
                    html.AppendLine("                <div class=\"leaders-list\">");
                    var rank = 1;
                    foreach (var player in topPlayers)
                    {
                        var medal = rank switch { 1 => "??", 2 => "??", 3 => "??", _ => $"#{rank}" };
                        html.AppendLine("                    <div class=\"leader-item\">");
                        html.AppendLine($"                        <span class=\"rank\">{medal}</span>");
                        html.AppendLine($"                        <span class=\"player-name\">{player.PlayerName}</span>");
                        html.AppendLine($"                        <span class=\"player-team\">{player.TeamName}</span>");
                        html.AppendLine($"                        <span class=\"player-stat\">{player.WinPercentage:F1}%</span>");
                        html.AppendLine("                    </div>");
                        rank++;
                    }
                    html.AppendLine("                </div>");
                    if (_settings.ShowPlayerStats)
                        html.AppendLine("                <p class=\"view-all\"><a href=\"players.html\">View All Players ?</a></p>");
                    html.AppendLine("            </section>");
                }
            }
            
            // Recent Results
            if (_settings.HomeShowRecentResults && _settings.ShowResults && completedFixtures > 0)
            {
                html.AppendLine("            <section class=\"section\">");
                html.AppendLine("                <h3>?? Recent Results</h3>");
                
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
                html.AppendLine("                <p class=\"view-all\"><a href=\"results.html\">View All Results ?</a></p>");
                html.AppendLine("            </section>");
            }
            
            // Upcoming Fixtures
            if (_settings.HomeShowUpcomingFixtures && _settings.ShowFixtures)
            {
                var upcomingFixtures = fixtures
                    .Where(f => f.Date > DateTime.Now && !f.Frames.Any(fr => fr.Winner != FrameWinner.None))
                    .OrderBy(f => f.Date)
                    .Take(_settings.HomeUpcomingFixturesCount)
                    .ToList();
                
                if (upcomingFixtures.Any())
                {
                    html.AppendLine("            <section class=\"section\">");
                    html.AppendLine("                <h3>?? Upcoming Fixtures</h3>");
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
                    html.AppendLine("                <p class=\"view-all\"><a href=\"fixtures.html\">View All Fixtures ?</a></p>");
                    html.AppendLine("            </section>");
                }
            }
            
            // Sponsors on home page
            if (_settings.HomeShowSponsors && _settings.ShowSponsors && _settings.Sponsors.Any(s => s.IsActive))
            {
                AppendSponsorsSection(html);
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private void AppendDocumentHead(StringBuilder html, string title, Season season)
        {
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine($"    <title>{title}</title>");
            
            // Meta description
            if (!string.IsNullOrWhiteSpace(_settings.MetaDescription))
                html.AppendLine($"    <meta name=\"description\" content=\"{_settings.MetaDescription}\">");
            else
                html.AppendLine($"    <meta name=\"description\" content=\"{_settings.LeagueName} - {_settings.LeagueSubtitle}\">");
            
            // Meta keywords
            if (!string.IsNullOrWhiteSpace(_settings.MetaKeywords))
                html.AppendLine($"    <meta name=\"keywords\" content=\"{_settings.MetaKeywords}\">");
            
            // Open Graph
            html.AppendLine($"    <meta property=\"og:title\" content=\"{title}\">");
            html.AppendLine($"    <meta property=\"og:site_name\" content=\"{_settings.LeagueName}\">");
            if (!string.IsNullOrWhiteSpace(_settings.OgImage))
                html.AppendLine($"    <meta property=\"og:image\" content=\"{_settings.OgImage}\">");
            
            // Favicon
            if (!string.IsNullOrWhiteSpace(_settings.FaviconUrl))
                html.AppendLine($"    <link rel=\"icon\" href=\"{_settings.FaviconUrl}\">");
            
            html.AppendLine("    <link rel=\"stylesheet\" href=\"style.css\">");
            
            // Custom head HTML
            if (!string.IsNullOrWhiteSpace(_settings.CustomHeadHtml))
                html.AppendLine(_settings.CustomHeadHtml);
            
            html.AppendLine("</head>");
        }
        
        private string GenerateStylesheet(WebsiteTemplate template)
        {
            return template.Id switch
            {
                "dark" => GenerateDarkModeCSS(),
                "sport" => GenerateSportCSS(),
                "minimalist" => GenerateMinimalistCSS(),
                "classic" => GenerateModernCSS(),
                "minimal" => GenerateModernCSS(),
                _ => GenerateModernCSS()
            };
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
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>League Standings</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{season.Name}</p>");
            html.AppendLine("            </div>");
            
            foreach (var division in divisions.OrderBy(d => d.Name))
            {
                var divisionTeams = teams.Where(t => t.DivisionId == division.Id).ToList();
                if (!divisionTeams.Any()) continue;
                
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
                if (_settings.StandingsShowDrawn) html.AppendLine("                            <th>D</th>");
                if (_settings.StandingsShowLost) html.AppendLine("                            <th>L</th>");
                if (_settings.StandingsShowFramesFor) html.AppendLine("                            <th>F</th>");
                if (_settings.StandingsShowFramesAgainst) html.AppendLine("                            <th>A</th>");
                if (_settings.StandingsShowFramesDiff) html.AppendLine("                            <th>Diff</th>");
                if (_settings.StandingsShowForm) html.AppendLine("                            <th>Form</th>");
                if (_settings.StandingsShowPoints) html.AppendLine("                            <th>Pts</th>");
                html.AppendLine("                        </tr>");
                html.AppendLine("                    </thead>");
                html.AppendLine("                    <tbody>");
                
                var standings = CalculateStandingsWithForm(divisionTeams, fixtures);
                var position = 1;
                var totalTeams = standings.Count;
                
                foreach (var standing in standings.OrderByDescending(s => s.Points)
                    .ThenByDescending(s => s.FramesDiff)
                    .ThenByDescending(s => s.FramesFor))
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
                            posDisplay = position switch { 1 => "??", 2 => "??", 3 => "??", _ => posDisplay };
                        }
                        html.AppendLine($"                            <td>{posDisplay}</td>");
                    }
                    
                    html.AppendLine($"                            <td><strong><a href=\"team.html?id={standing.TeamId:N}\" class=\"team-link\">{standing.TeamName}</a></strong></td>");
                    if (_settings.StandingsShowPlayed) html.AppendLine($"                            <td>{standing.Played}</td>");
                    if (_settings.StandingsShowWon) html.AppendLine($"                            <td>{standing.Won}</td>");
                    if (_settings.StandingsShowDrawn) html.AppendLine($"                            <td>{standing.Drawn}</td>");
                    if (_settings.StandingsShowLost) html.AppendLine($"                            <td>{standing.Lost}</td>");
                    if (_settings.StandingsShowFramesFor) html.AppendLine($"                            <td>{standing.FramesFor}</td>");
                    if (_settings.StandingsShowFramesAgainst) html.AppendLine($"                            <td>{standing.FramesAgainst}</td>");
                    if (_settings.StandingsShowFramesDiff) html.AppendLine($"                            <td class=\"{(standing.FramesDiff > 0 ? "text-positive" : standing.FramesDiff < 0 ? "text-negative" : "")}\">{standing.FramesDiff:+0;-0;0}</td>");
                    if (_settings.StandingsShowForm) html.AppendLine($"                            <td class=\"form\">{standing.FormDisplay}</td>");
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
            html.AppendLine("    </main>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private string GetTableClasses()
        {
            var classes = new List<string> { "data-table" };
            if (_settings.TableStriped) classes.Add("striped");
            if (_settings.TableHoverable) classes.Add("hoverable");
            if (_settings.TableBordered) classes.Add("bordered");
            if (_settings.TableCompact) classes.Add("compact");
            return string.Join(" ", classes);
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
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>?? Fixtures</h2>");
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
                    html.AppendLine("                <p class=\"view-all\"><a href=\"fixtures.html\">View All Fixtures ?</a></p>");
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
            
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
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
            html.AppendLine("            .fixtures-sheet-wrapper .fixtures-sheet { transform: scale(0.85); transform-origin: top left; }");
            html.AppendLine("            @media (max-width: 768px) { .fixtures-sheet-actions { flex-direction: column; } .fixtures-sheet-actions button { width: 100%; justify-content: center; } }");
            html.AppendLine("            </style>");
            html.AppendLine($"            <div class=\"section fixtures-sheet-section{expandedClass}\">");
            html.AppendLine("                <div class=\"fixtures-sheet-header\" onclick=\"toggleFixturesSheet()\">");
            html.AppendLine($"                    <h3>?? {sheetTitle}</h3>");
            html.AppendLine("                    <div class=\"fixtures-sheet-controls\">");
            html.AppendLine("                        <span class=\"toggle-icon\">?</span>");
            html.AppendLine("                    </div>");
            html.AppendLine("                </div>");
            html.AppendLine("                <div class=\"fixtures-sheet-content\">");
            html.AppendLine("                    <div class=\"fixtures-sheet-actions\">");
            html.AppendLine("                        <button class=\"btn-download\" onclick=\"downloadFixturesSheet()\">? Download HTML</button>");
            html.AppendLine("                        <button class=\"btn-print\" onclick=\"printFixturesSheet()\">?? Print</button>");
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
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>?? Match Results</h2>");
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
            html.AppendLine("    </main>");
            
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
            
            AppendDocumentHead(html, $"Players - {_settings.LeagueName}", season);
            html.AppendLine("<body>");
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);
            
            AppendHeader(html, season);
            AppendNavigation(html, "Players");
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>?? Player Statistics</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{players.Count} Players</p>");
            html.AppendLine("            </div>");
            
            var playerStats = CalculatePlayerStats(players, teams, fixtures);
            
            // Apply filter based on settings
            int minFramesRequired = 0;
            if (_settings.PlayersUsePercentageFilter && _settings.PlayersMinFramesPercentage > 0)
            {
                // Calculate max frames available in the season
                var maxFrames = playerStats.Any() ? playerStats.Max(p => p.Played) : 0;
                minFramesRequired = (int)Math.Ceiling(maxFrames * (_settings.PlayersMinFramesPercentage / 100.0));
            }
            else
            {
                minFramesRequired = _settings.PlayersMinGames;
            }
            
            playerStats = playerStats
                .Where(p => p.Played >= minFramesRequired)
                .ToList();
            
            // Sort based on settings
            playerStats = _settings.PlayersSortBy switch
            {
                "won" => playerStats.OrderByDescending(s => s.Won).ThenByDescending(s => s.WinPercentage).ToList(),
                "played" => playerStats.OrderByDescending(s => s.Played).ThenByDescending(s => s.WinPercentage).ToList(),
                "eightballs" => playerStats.OrderByDescending(s => s.EightBalls).ThenByDescending(s => s.WinPercentage).ToList(),
                "rating" => playerStats.OrderByDescending(s => s.Rating).ThenByDescending(s => s.WinPercentage).ToList(),
                _ => playerStats.OrderByDescending(s => s.WinPercentage).ThenByDescending(s => s.Won).ToList()
            };
            
            playerStats = playerStats.Take(_settings.PlayersPerPage).ToList();
            
            if (playerStats.Any())
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <h3>Top Performers</h3>");
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
                html.AppendLine("                        </tr>");
                html.AppendLine("                    </thead>");
                html.AppendLine("                    <tbody>");
                
                var position = 1;
                foreach (var stat in playerStats)
                {
                    html.AppendLine("                        <tr>");
                    if (_settings.PlayersShowPosition)
                    {
                        var posDisplay = position <= 3 
                            ? (position == 1 ? "??" : position == 2 ? "??" : "??")
                            : position.ToString();
                        html.AppendLine($"                            <td>{posDisplay}</td>");
                    }
                    // Make player name a clickable link to the single template page with query parameter
                    html.AppendLine($"                            <td><strong><a href=\"player.html?id={stat.PlayerId:N}\" class=\"player-link\">{stat.PlayerName}</a></strong></td>");
                    if (_settings.PlayersShowTeam) html.AppendLine($"                            <td>{stat.TeamName}</td>");
                    if (_settings.PlayersShowPlayed) html.AppendLine($"                            <td>{stat.Played}</td>");
                    if (_settings.PlayersShowWon) html.AppendLine($"                            <td>{stat.Won}</td>");
                    if (_settings.PlayersShowLost) html.AppendLine($"                            <td>{stat.Lost}</td>");
                    if (_settings.PlayersShowWinPercentage) html.AppendLine($"                            <td><strong>{stat.WinPercentage:F1}%</strong></td>");
                    if (_settings.PlayersShowEightBalls) html.AppendLine($"                            <td>{stat.EightBalls}</td>");
                    if (_settings.PlayersShowRating) html.AppendLine($"                            <td>{stat.Rating}</td>");
                    html.AppendLine("                        </tr>");
                    position++;
                }
                
                html.AppendLine("                    </tbody>");
                html.AppendLine("                </table>");
                html.AppendLine("                </div>");
                
                if (_settings.PlayersUsePercentageFilter && _settings.PlayersMinFramesPercentage > 0)
                    html.AppendLine($"                <p class=\"table-note\">* Minimum {_settings.PlayersMinFramesPercentage}% of available frames required to qualify ({minFramesRequired} frames)</p>");
                else if (_settings.PlayersMinGames > 0)
                    html.AppendLine($"                <p class=\"table-note\">* Minimum {_settings.PlayersMinGames} games played to qualify</p>");
                
                html.AppendLine("            </div>");
            }
            else
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <p class=\"empty-message\">No player statistics available yet.</p>");
                html.AppendLine("            </div>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
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
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>?? Divisions</h2>");
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
                
                if (_settings.DivisionsShowMiniStandings && divisionTeams.Any())
                {
                    var standings = CalculateStandings(divisionTeams, fixtures)
                        .OrderByDescending(s => s.Points)
                        .Take(5)
                        .ToList();
                    
                    if (standings.Any())
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
                
                if (_settings.DivisionsShowTeamList && divisionTeams.Any())
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
            html.AppendLine("    </main>");
            
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
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>?? League Rules</h2>");
            html.AppendLine("            </div>");
            html.AppendLine("            <div class=\"section content-section\">");
            html.AppendLine($"                {_settings.RulesContent}");
            html.AppendLine("            </div>");
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
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
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>?? Contact Us</h2>");
            html.AppendLine("            </div>");
            html.AppendLine("            <div class=\"section\">");
            html.AppendLine("                <div class=\"contact-grid\">");
            
            if (!string.IsNullOrWhiteSpace(_settings.ContactEmail))
            {
                html.AppendLine("                    <div class=\"contact-item\">");
                html.AppendLine("                        <h4>?? Email</h4>");
                html.AppendLine($"                        <a href=\"mailto:{_settings.ContactEmail}\">{_settings.ContactEmail}</a>");
                html.AppendLine("                    </div>");
            }
            
            if (!string.IsNullOrWhiteSpace(_settings.ContactPhone))
            {
                html.AppendLine("                    <div class=\"contact-item\">");
                html.AppendLine("                        <h4>?? Phone</h4>");
                html.AppendLine($"                        <a href=\"tel:{_settings.ContactPhone}\">{_settings.ContactPhone}</a>");
                html.AppendLine("                    </div>");
            }
            
            if (!string.IsNullOrWhiteSpace(_settings.ContactAddress))
            {
                html.AppendLine("                    <div class=\"contact-item\">");
                html.AppendLine("                        <h4>?? Address</h4>");
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
            html.AppendLine("    </main>");
            
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
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>?? Our Sponsors</h2>");
            html.AppendLine("                <p class=\"hero-dates\">Thank you to our supporters</p>");
            html.AppendLine("            </div>");
            
            var activeSponsors = _settings.Sponsors.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToList();
            var tiers = activeSponsors.Select(s => s.Tier).Distinct().ToList();
            
            foreach (var tier in tiers)
            {
                var tierSponsors = activeSponsors.Where(s => s.Tier == tier).ToList();
                if (!tierSponsors.Any()) continue;
                
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
                            html.AppendLine($"                        <img src=\"{dataUrl}\" alt=\"{sponsor.Name}\" style=\"max-height: {_settings.SponsorLogoMaxHeight}px;\">");
                    }
                    html.AppendLine($"                        <h4>{sponsor.Name}</h4>");
                    if (!string.IsNullOrWhiteSpace(sponsor.Description))
                        html.AppendLine($"                        <p>{sponsor.Description}</p>");
                    if (!string.IsNullOrWhiteSpace(sponsor.WebsiteUrl))
                        html.AppendLine($"                        <a href=\"{sponsor.WebsiteUrl}\" target=\"_blank\" class=\"sponsor-link\">Visit Website ?</a>");
                    html.AppendLine("                    </div>");
                }
                
                html.AppendLine("                </div>");
                html.AppendLine("            </div>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
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
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>?? Latest News</h2>");
            html.AppendLine("            </div>");
            
            var publishedNews = _settings.NewsItems
                .Where(n => n.IsPublished)
                .OrderByDescending(n => n.IsPinned)
                .ThenByDescending(n => n.DatePublished)
                .Take(_settings.NewsItemsToShow)
                .ToList();
            
            if (publishedNews.Any())
            {
                foreach (var news in publishedNews)
                {
                    html.AppendLine("            <article class=\"section news-article\">");
                    if (news.IsPinned)
                        html.AppendLine("                <span class=\"pinned-badge\">?? Pinned</span>");
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
            html.AppendLine("    </main>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
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
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>{page.Title}</h2>");
            html.AppendLine("            </div>");
            html.AppendLine("            <div class=\"section content-section\">");
            html.AppendLine($"                {page.Content}");
            html.AppendLine("            </div>");
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
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
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            
            // Player header
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>?? {stats.PlayerName}</h2>");
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
            
            html.AppendLine("            </div>");
            
            // Match history
            var playerHistory = GetPlayerMatchHistory(player.Id, fixtures, teams, allPlayers);
            
            if (playerHistory.Any())
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <h3>?? Full Record</h3>");
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
            html.AppendLine("                <a href=\"players.html\" class=\"back-link\">? Back to All Players</a>");
            html.AppendLine("            </div>");
            
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
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
            
            foreach (var fixture in fixtures.Where(f => f.Frames.Any() && f.Frames.Any(fr => fr.Winner != FrameWinner.None)))
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
            public int FramesDiff => FramesFor - FramesAgainst;
            public int Points { get; set; }
            public List<char> RecentForm { get; set; } = new();
            public string FormDisplay => string.Join("", RecentForm.Take(5).Select(f => f switch { 'W' => "??", 'D' => "??", 'L' => "??", _ => "?" }));
        }
        
        private List<TeamStanding> CalculateStandings(List<Team> teams, List<Fixture> fixtures)
        {
            return CalculateStandingsWithForm(teams, fixtures);
        }
        
        private List<TeamStanding> CalculateStandingsWithForm(List<Team> teams, List<Fixture> fixtures)
        {
            var standings = new List<TeamStanding>();
            var settings = _league.Settings;
            
            foreach (var team in teams)
            {
                var standing = new TeamStanding
                {
                    TeamId = team.Id,
                    TeamName = team.Name ?? "Unknown"
                };
                
                // Get all completed fixtures for this team
                var teamFixtures = fixtures
                    .Where(f => f.Frames.Any() && ( f.HomeTeamId == team.Id || f.AwayTeamId == team.Id))
                    .OrderByDescending(f => f.Date)
                    .ToList();
                
                foreach (var fixture in teamFixtures)
                {
                    bool isHome = fixture.HomeTeamId == team.Id;
                    int teamScore = isHome ? fixture.HomeScore : fixture.AwayScore;
                    int oppScore = isHome ? fixture.AwayScore : fixture.HomeScore;
                    
                    standing.Played++;
                    standing.FramesFor += teamScore;
                    standing.FramesAgainst += oppScore;
                    
                    if (teamScore > oppScore)
                    {
                        standing.Won++;
                        standing.Points += teamScore + settings.MatchWinBonus;
                        standing.RecentForm.Add('W');
                    }
                    else if (teamScore < oppScore)
                    {
                        standing.Lost++;
                        standing.Points += teamScore;
                        standing.RecentForm.Add('L');
                    }
                    else
                    {
                        standing.Drawn++;
                        standing.Points += teamScore + settings.MatchDrawBonus;
                        standing.RecentForm.Add('D');
                    }
                }
                
                standings.Add(standing);
            }
            
            return standings;
        }
        
        #region Missing Methods
        
        private void AppendHeader(StringBuilder html, Season season)
        {
            var logoData = _settings.GetEffectiveLogoData();
            var hasLogo = _settings.UseCustomLogo && logoData != null && logoData.Length > 0;
            
            html.AppendLine("    <header>");
            html.AppendLine("        <div class=\"header-content\">");
            
            if (hasLogo)
            {
                var imageOptimizer = new ImageOptimizationService();
                var mimeType = imageOptimizer.GetMimeType("logo.png");
                var dataUrl = imageOptimizer.ToDataUrl(logoData!, mimeType);
                html.AppendLine($"            <img src=\"{dataUrl}\" alt=\"{_settings.LeagueName}\" class=\"logo\" style=\"max-width: {_settings.LogoMaxWidth}px; max-height: {_settings.LogoMaxHeight}px;\">");
            }
            
            html.AppendLine($"            <h1>{_settings.LeagueName}</h1>");
            if (!string.IsNullOrWhiteSpace(_settings.LeagueSubtitle))
                html.AppendLine($"            <p class=\"subtitle\">{_settings.LeagueSubtitle}</p>");
            
            if (_settings.ShowSeasonBadge)
                html.AppendLine($"            <span class=\"season-badge\">{season.Name}</span>");
            
            html.AppendLine("        </div>");
            html.AppendLine("    </header>");
        }
        
        private void AppendNavigation(StringBuilder html, string activePage)
        {
            html.AppendLine("    <nav>");
            html.AppendLine("        <div class=\"nav-container\">");
            
            void NavLink(string href, string text, string page)
            {
                var activeClass = activePage.Equals(page, StringComparison.OrdinalIgnoreCase) ? " class=\"active\"" : "";
                html.AppendLine($"            <a href=\"{href}\"{activeClass}>{text}</a>");
            }
            
            NavLink("index.html", "Home", "Home");
            
            if (_settings.ShowStandings)
                NavLink("standings.html", "Standings", "Standings");
            
            if (_settings.ShowFixtures)
                NavLink("fixtures.html", "Fixtures", "Fixtures");
            
            if (_settings.ShowResults)
                NavLink("results.html", "Results", "Results");
            
            if (_settings.ShowPlayerStats)
                NavLink("players.html", "Players", "Players");
            
            if (_settings.ShowDivisions)
                NavLink("divisions.html", "Divisions", "Divisions");
            
            if (_settings.ShowGallery && _settings.GalleryImages.Count > 0)
                NavLink("gallery.html", "Gallery", "Gallery");
            
            if (_settings.ShowNews && _settings.NewsItems.Count > 0)
                NavLink("news.html", "News", "News");
            
            if (_settings.ShowSponsors && _settings.Sponsors.Count > 0)
                NavLink("sponsors.html", "Sponsors", "Sponsors");
            
            if (_settings.ShowRules && !string.IsNullOrWhiteSpace(_settings.RulesContent))
                NavLink("rules.html", "Rules", "Rules");
            
            if (_settings.ShowContactPage && _settings.HasContactInfo)
                NavLink("contact.html", "Contact", "Contact");
            
            // Custom pages in nav
            foreach (var page in _settings.CustomPages.Where(p => p.IsPublished && p.ShowInNav).OrderBy(p => p.NavOrder))
            {
                var slug = string.IsNullOrWhiteSpace(page.Slug) ? page.Title.ToLower().Replace(" ", "-") : page.Slug;
                NavLink($"{slug}.html", page.Title, page.Title);
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </nav>");
        }
        
        private void AppendFooter(StringBuilder html)
        {
            html.AppendLine("    <footer>");
            html.AppendLine("        <div class=\"footer-content\">");
            
            if (_settings.ShowFooterContact && _settings.HasContactInfo)
            {
                html.AppendLine("            <div class=\"footer-contact\">");
                if (!string.IsNullOrWhiteSpace(_settings.ContactEmail))
                    html.AppendLine($"                <p>Email: <a href=\"mailto:{_settings.ContactEmail}\">{_settings.ContactEmail}</a></p>");
                if (!string.IsNullOrWhiteSpace(_settings.ContactPhone))
                    html.AppendLine($"                <p>Phone: {_settings.ContactPhone}</p>");
                html.AppendLine("            </div>");
            }
            
            if (_settings.ShowFooterSocialLinks && _settings.HasSocialLinks)
            {
                html.AppendLine("            <div class=\"footer-social\">");
                if (!string.IsNullOrWhiteSpace(_settings.FacebookUrl))
                    html.AppendLine($"                <a href=\"{_settings.FacebookUrl}\" target=\"_blank\">Facebook</a>");
                if (!string.IsNullOrWhiteSpace(_settings.TwitterUrl))
                    html.AppendLine($"                <a href=\"{_settings.TwitterUrl}\" target=\"_blank\">Twitter</a>");
                if (!string.IsNullOrWhiteSpace(_settings.InstagramUrl))
                    html.AppendLine($"                <a href=\"{_settings.InstagramUrl}\" target=\"_blank\">Instagram</a>");
                html.AppendLine("            </div>");
            }
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomFooterText))
                html.AppendLine($"            <p class=\"footer-custom\">{_settings.CustomFooterText}</p>");
            
            var copyrightText = !string.IsNullOrWhiteSpace(_settings.CopyrightText)
                ? _settings.CopyrightText
                : $"© {DateTime.Now.Year} {_settings.LeagueName}";
            html.AppendLine($"            <p class=\"copyright\">{copyrightText}</p>");
            
            if (_settings.ShowPoweredBy)
                html.AppendLine("            <p class=\"powered-by\">Powered by WDPL League Manager</p>");
            
            if (_settings.ShowLastUpdated)
                html.AppendLine($"            <p class=\"last-updated\">Last updated: {DateTime.Now:dd MMM yyyy HH:mm}</p>");
            
            html.AppendLine("        </div>");
            html.AppendLine("    </footer>");
        }
        
        private void AppendSponsorsSection(StringBuilder html)
        {
            var activeSponsors = _settings.Sponsors.Where(s => s.IsActive).Take(6).ToList();
            if (!activeSponsors.Any()) return;
            
            var imageOptimizer = new ImageOptimizationService();
            
            html.AppendLine("            <section class=\"section sponsors-section\">");
            html.AppendLine("                <h3>Our Sponsors</h3>");
            html.AppendLine("                <div class=\"sponsors-grid\">");
            
            foreach (var sponsor in activeSponsors)
            {
                html.AppendLine("                    <div class=\"sponsor-item\">");
                if (sponsor.LogoData.Length > 0)
                {
                    var mimeType = imageOptimizer.GetMimeType(sponsor.LogoFileName);
                    var dataUrl = imageOptimizer.ToDataUrl(sponsor.LogoData, mimeType);
                    if (!string.IsNullOrWhiteSpace(sponsor.WebsiteUrl))
                        html.AppendLine($"                        <a href=\"{sponsor.WebsiteUrl}\" target=\"_blank\"><img src=\"{dataUrl}\" alt=\"{sponsor.Name}\" style=\"max-height: {_settings.SponsorLogoMaxHeight}px;\"></a>");
                    else
                        html.AppendLine($"                        <img src=\"{dataUrl}\" alt=\"{sponsor.Name}\" style=\"max-height: {_settings.SponsorLogoMaxHeight}px;\">");
                }
                else
                {
                    html.AppendLine($"                        <span class=\"sponsor-name\">{sponsor.Name}</span>");
                }
                html.AppendLine("                    </div>");
            }
            
            html.AppendLine("                </div>");
            if (_settings.Sponsors.Count(s => s.IsActive) > 6)
                html.AppendLine("                <p class=\"view-all\"><a href=\"sponsors.html\">View All Sponsors ?</a></p>");
            html.AppendLine("            </section>");
        }
        
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
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>?? Photo Gallery</h2>");
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
            html.AppendLine("    </main>");
            
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
            var settings = _league.Settings;
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
        
        private string GenerateModernCSS()
        {
            var css = new StringBuilder();
            
            css.AppendLine($@"
:root {{
    --primary-color: {_settings.PrimaryColor};
    --secondary-color: {_settings.SecondaryColor};
    --accent-color: {_settings.AccentColor};
    --bg-color: {_settings.BackgroundColor};
    --card-bg: {_settings.CardBackgroundColor};
    --text-color: {_settings.TextColor};
    --text-secondary: {_settings.TextSecondaryColor};
    --header-text: {_settings.HeaderTextColor};
    --border-radius: {_settings.BorderRadius}px;
    --spacing: {_settings.CardSpacing}px;
    --font-family: {WebsiteSettings.FontFamilies.GetValueOrDefault(_settings.FontFamily, "Inter")};
    --header-font: {WebsiteSettings.FontFamilies.GetValueOrDefault(_settings.HeaderFontFamily, "Inter")};
}}

* {{ box-sizing: border-box; margin: 0; padding: 0; }}

body {{
    font-family: var(--font-family);
    font-size: {_settings.BaseFontSize}px;
    background: var(--bg-color);
    color: var(--text-color);
    line-height: 1.6;
}}

.container {{
    max-width: 1200px;
    margin: 0 auto;
    padding: 0 20px;
}}

header {{
    background: linear-gradient(135deg, var(--primary-color) 0%, var(--secondary-color) 100%);
    color: var(--header-text);
    padding: 40px 20px;
    text-align: center;
}}

header h1 {{
    font-family: var(--header-font);
    font-size: 2.5rem;
    margin-bottom: 10px;
}}

header .subtitle {{
    opacity: 0.9;
    font-size: 1.1rem;
}}

header .season-badge {{
    display: inline-block;
    background: rgba(255,255,255,0.2);
    padding: 6px 16px;
    border-radius: 20px;
    margin-top: 15px;
    font-size: 0.9rem;
}}

header .logo {{
    max-width: 200px;
    margin-bottom: 15px;
}}

nav {{
    background: var(--card-bg);
    border-bottom: 1px solid rgba(0,0,0,0.1);
    {(_settings.NavSticky ? "position: sticky; top: 0; z-index: 100;" : "")}
}}

nav .nav-container {{
    display: flex;
    justify-content: {_settings.NavPosition};
    flex-wrap: wrap;
    gap: 8px;
    padding: 15px 20px;
    max-width: 1200px;
    margin: 0 auto;
}}

nav a {{
    color: var(--text-color);
    text-decoration: none;
    padding: 8px 16px;
    border-radius: {(_settings.NavStyle == "pills" ? "20px" : _settings.NavStyle == "buttons" ? "8px" : "0")};
    transition: all 0.2s;
    {(_settings.NavStyle == "underline" ? "border-bottom: 2px solid transparent;" : "")}
}}

nav a:hover, nav a.active {{
    background: var(--primary-color);
    color: white;
    {(_settings.NavStyle == "underline" ? "background: transparent; color: var(--primary-color); border-bottom-color: var(--primary-color);" : "")}
}}

main {{
    padding: var(--spacing) 0;
}}

.hero {{
    text-align: center;
    padding: 40px 20px;
    margin-bottom: var(--spacing);
}}

.hero h2 {{
    font-family: var(--header-font);
    font-size: 2rem;
    color: var(--text-color);
    margin-bottom: 10px;
}}

.hero-dates {{
    color: var(--text-secondary);
}}

.section {{
    background: var(--card-bg);
    border-radius: var(--border-radius);
    padding: var(--spacing);
    margin-bottom: var(--spacing);
    {(_settings.EnableShadows ? "box-shadow: 0 4px 6px rgba(0,0,0,0.07);" : "")}
}}

.section h3 {{
    font-family: var(--header-font);
    margin-bottom: 20px;
    color: var(--text-color);
}}

.stats-grid {{
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
    gap: 20px;
    margin-bottom: var(--spacing);
}}

.stat-card {{
    background: var(--card-bg);
    border-radius: var(--border-radius);
    padding: 24px;
    text-align: center;
    {(_settings.EnableShadows ? "box-shadow: 0 2px 4px rgba(0,0,0,0.05);" : "")}
    {(_settings.CardShowTopAccent ? "border-top: 3px solid var(--primary-color);" : "")}
}}

.stat-number {{
    font-size: 2.5rem;
    font-weight: bold;
    color: var(--primary-color);
}}

.stat-label {{
    color: var(--text-secondary);
    font-size: 0.9rem;
    margin-top: 5px;
}}

.data-table {{
    width: 100%;
    border-collapse: collapse;
}}

.data-table th, .data-table td {{
    padding: 12px;
    text-align: left;
    border-bottom: 1px solid rgba(0,0,0,0.08);
}}

.data-table th {{
    background: linear-gradient(135deg, var(--primary-color) 0%, var(--secondary-color) 100%);
    color: white;
    font-weight: 600;
}}

.data-table.striped tbody tr:nth-child(even) {{
    background: rgba(255,255,255,0.03);
}}

.data-table.hoverable tbody tr:hover {{
    background: rgba(255,255,255,0.08);
}}

.data-table.bordered td, .data-table.bordered th {{
    border: 1px solid rgba(0,0,0,0.1);
}}

.data-table.compact th, .data-table.compact td {{
    padding: 8px;
}}

.table-responsive {{
    overflow-x: auto;
}}

.text-positive {{ color: #10B981; }}
.text-negative {{ color: #EF4444; }}

.highlight-top {{ background: rgba(34, 197, 94, 0.1); }}
.highlight-bottom {{ background: rgba(239, 68, 68, 0.1); }}

.results-list, .fixtures-list {{
    display: flex;
    flex-direction: column;
    gap: 12px;
}}

.result-item, .fixture-item {{
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 16px;
    background: rgba(0,0,0,0.02);
    border-radius: calc(var(--border-radius) / 2);
    flex-wrap: wrap;
    gap: 10px;
}}

.result-item .team, .fixture-item .team {{
    font-weight: 600;
    flex: 1;
    min-width: 120px;
}}

.result-item .score {{
    font-weight: bold;
    font-size: 1.2rem;
    padding: 0 15px;
}}

.result-item .team.winner {{
    color: var(--primary-color);
}}

.fixture-item .vs {{
    color: var(--text-secondary);
    padding: 0 10px;
}}

.fixture-item .venue, .result-item .venue {{
    color: var(--text-secondary);
    font-size: 0.9rem;
}}

.date {{
    color: var(--text-secondary);
    font-size: 0.85rem;
    min-width: 80px;
}}

.view-all {{
    text-align: center;
    margin-top: 20px;
}}

.view-all a {{
    color: var(--primary-color);
    text-decoration: none;
    font-weight: 600;
}}

.leaders-list {{
    display: flex;
    flex-direction: column;
    gap: 10px;
}}

.leader-item {{
    display: flex;
    align-items: center;
    gap: 15px;
    padding: 12px;
    background: rgba(0,0,0,0.02);
    border-radius: calc(var(--border-radius) / 2);
}}

.leader-item .rank {{
    font-size: 1.5rem;
    min-width: 40px;
}}

.leader-item .player-name {{
    font-weight: 600;
    flex: 1;
}}

.leader-item .player-team {{
    color: var(--text-secondary);
}}

.leader-item .player-stat {{
    font-weight: bold;
    color: var(--primary-color);
}}

.player-link, .team-link {{
    color: var(--primary-color);
    text-decoration: none;
}}

.player-link:hover, .team-link:hover {{
    text-decoration: underline;
}}

.back-link {{
    color: var(--primary-color);
    text-decoration: none;
    font-weight: 600;
}}

.form {{ display: flex; gap: 4px; }}

.empty-message {{
    text-align: center;
    color: var(--text-secondary);
    padding: 40px;
}}

.table-note {{
    color: var(--text-secondary);
    font-size: 0.85rem;
    margin-top: 15px;
    font-style: italic;
}}

.gallery-grid {{
    display: grid;
    grid-template-columns: repeat(var(--gallery-columns, 3), 1fr);
    gap: 20px;
}}

.gallery-item img {{
    width: 100%;
    border-radius: var(--border-radius);
    {(_settings.EnableShadows ? "box-shadow: 0 2px 8px rgba(0,0,0,0.1);" : "")}
}}

.gallery-item .caption {{
    text-align: center;
    margin-top: 8px;
    color: var(--text-secondary);
}}

.sponsors-grid {{
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
    gap: 20px;
    align-items: center;
}}

.sponsor-item {{
    text-align: center;
}}

.sponsor-item img {{
    max-width: 100%;
}}

.contact-grid {{
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
    gap: 30px;
}}

.contact-item h4 {{
    margin-bottom: 10px;
}}

.contact-item a {{
    color: var(--primary-color);
    text-decoration: none;
}}

.social-links {{
    display: flex;
    gap: 15px;
    flex-wrap: wrap;
}}

.social-link {{
    padding: 10px 20px;
    background: var(--primary-color);
    color: white;
    text-decoration: none;
    border-radius: var(--border-radius);
}}

.division-card h3 {{
    margin-bottom: 15px;
}}

.mini-stats {{
    display: flex;
    gap: 15px;
    margin-bottom: 15px;
}}

.mini-stats .stat-card {{
    padding: 15px;
}}

.mini-standings {{
    display: flex;
    flex-direction: column;
    gap: 5px;
}}

.mini-standing-row {{
    display: flex;
    gap: 10px;
    padding: 8px;
    background: rgba(0,0,0,0.02);
    border-radius: 4px;
}}

.mini-standing-row .pos {{
    font-weight: bold;
    min-width: 25px;
}}

.mini-standing-row .pts {{
    margin-left: auto;
    color: var(--text-secondary);
}}

.team-list {{
    list-style: none;
}}

.team-list li {{
    padding: 8px 0;
    border-bottom: 1px solid rgba(0,0,0,0.05);
}}

.player-count {{
    color: var(--text-secondary);
    font-size: 0.9rem;
}}

.news-article {{
    position: relative;
}}

.pinned-badge {{
    position: absolute;
    top: 15px;
    right: 15px;
    background: var(--accent-color);
    color: white;
    padding: 4px 10px;
    border-radius: 12px;
    font-size: 0.8rem;
}}

.news-meta {{
    color: var(--text-secondary);
    margin-bottom: 15px;
}}

.category-badge {{
    background: var(--primary-color);
    color: white;
    padding: 2px 8px;
    border-radius: 10px;
    font-size: 0.8rem;
    margin-left: 10px;
}}

.division-badge {{
    background: rgba(0,0,0,0.1);
    padding: 4px 10px;
    border-radius: 12px;
    font-size: 0.8rem;
}}

footer {{
    background: #1E293B;
    color: #E2E8F0;
    padding: 40px 20px;
    margin-top: 40px;
}}

.footer-content {{
    max-width: 1200px;
    margin: 0 auto;
    text-align: center;
}}

footer a {{
    color: #60A5FA;
}}

.footer-social {{
    display: flex;
    justify-content: center;
    gap: 15px;
    margin: 20px 0;
}}

.footer-social a {{
    color: #E2E8F0;
}}

.copyright, .powered-by, .last-updated {{
    font-size: 0.85rem;
    color: #94A3B8;
    margin-top: 10px;
}}

@media (max-width: 768px) {{
    header h1 {{ font-size: 1.8rem; }}
    .hero h2 {{ font-size: 1.5rem; }}
    nav .nav-container {{ justify-content: center; }}
    .result-item, .fixture-item {{ flex-direction: column; text-align: center; }}
    .result-item .team, .fixture-item .team {{ min-width: auto; }}
    .gallery-grid {{ grid-template-columns: repeat(2, 1fr); }}
}}

{_settings.CustomCss}
");
            
            return css.ToString();
        }
        
        private string GenerateDarkModeCSS()
        {
            var baseCSS = GenerateModernCSS();
            
            var darkOverrides = @"
:root {
    --bg-color: #0F172A;
    --card-bg: #1E293B;
    --text-color: #E2E8F0;
    --text-secondary: #94A3B8;
}

.data-table th {
    background: linear-gradient(135deg, #3B82F6 0%, #1D4ED8 100%);
}

.result-item, .fixture-item, .leader-item {
    background: rgba(255,255,255,0.05);
}

.data-table.striped tbody tr:nth-child(even) {
    background: rgba(255,255,255,0.03);
}

.data-table.hoverable tbody tr:hover {
    background: rgba(255,255,255,0.08);
}

footer {
    background: #020617;
}
";
            
            return baseCSS + darkOverrides;
        }
        
        private string GenerateSportCSS()
        {
            var baseCSS = GenerateModernCSS();
            
            var sportOverrides = @"
header {
    background: linear-gradient(135deg, #DC2626 0%, #7F1D1D 100%);
}

.stat-number {
    color: #DC2626;
}

nav a:hover, nav a.active {
    background: #DC2626;
}

.data-table th {
    background: linear-gradient(135deg, #DC2626 0%, #991B1B 100%);
}
";
            
            return baseCSS + sportOverrides;
        }
        
        private string GenerateMinimalistCSS()
        {
            var baseCSS = GenerateModernCSS();
            
            var minimalistOverrides = @"
header {
    background: white;
    color: #0F172A;
    border-bottom: 1px solid #E2E8F0;
}

header .season-badge {
    background: #F1F5F9;
    color: #64748B;
}

.section {
    box-shadow: none;
    border: 1px solid #E2E8F0;
}

.stat-card {
    box-shadow: none;
    border: 1px solid #E2E8F0;
    border-top: none;
}

.data-table th {
    background: #F8FAFC;
    color: #0F172A;
}

nav a:hover, nav a.active {
    background: #F1F5F9;
    color: #0F172A;
}
";
            
            return baseCSS + minimalistOverrides;
        }
        
        #endregion
        
        #region Player Stats Class
        
        private sealed class PlayerStat
        {
            public Guid PlayerId { get; set; }
            public string PlayerName { get; set; } = "";
            public string TeamName { get; set; } = "";
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
