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
                
                // Generate individual player pages
                var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
                var playerStats = CalculatePlayerStats(players, teams, fixtures);
                var statsById = playerStats.ToDictionary(s => s.PlayerId, s => s);
                
                foreach (var player in players)
                {
                    if (statsById.TryGetValue(player.Id, out var stats) && stats.Played > 0)
                    {
                        var pageFileName = GetPlayerPageFileName(player.Id);
                        files[pageFileName] = GeneratePlayerPage(season, template, player, teams, fixtures, players, stats);
                    }
                }
            }
            
            if (_settings.ShowDivisions)
                files["divisions.html"] = GenerateDivisionsPage(season, template);
            
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
                    
                    html.AppendLine($"                        <tr{(string.IsNullOrEmpty(rowClass) ? "" : $" class=\"{rowClass}\"")}>");
                    
                    if (_settings.StandingsShowPosition)
                    {
                        var posDisplay = position.ToString();
                        if (_settings.StandingsShowMedals && position <= 3)
                        {
                            posDisplay = position switch { 1 => "??", 2 => "??", 3 => "??", _ => posDisplay };
                        }
                        html.AppendLine($"                            <td>{posDisplay}</td>");
                    }
                    
                    html.AppendLine($"                            <td><strong>{standing.TeamName}</strong></td>");
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
            
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
            AppendFooter(html);
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
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
                    // Make player name a clickable link to their individual page
                    var playerPageName = GetPlayerPageFileName(stat.PlayerId);
                    html.AppendLine($"                            <td><strong><a href=\"{playerPageName}\" class=\"player-link\">{stat.PlayerName}</a></strong></td>");
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
                            html.AppendLine($"                    <div class=\"mini-standing-row\"><span class=\"pos\">{pos++}</span> <span class=\"team-name\">{standing.TeamName}</span> <span class=\"pts\">{standing.Points} pts</span></div>");
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
                        html.AppendLine($"                    <li><strong>{team.Name}</strong> <span class=\"player-count\">({teamPlayers.Count} players)</span></li>");
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
                        html.AppendLine($"                        <img src=\"{dataUrl}\" alt=\"{sponsor.Name}\" class=\"sponsor-logo\" style=\"max-height: {_settings.SponsorLogoMaxHeight}px;\">");
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
        
        private void AppendSponsorsSection(StringBuilder html)
        {
            var imageOptimizer = new ImageOptimizationService();
            var activeSponsors = _settings.Sponsors.Where(s => s.IsActive).OrderBy(s => s.SortOrder).Take(6).ToList();
            
            if (!activeSponsors.Any()) return;
            
            html.AppendLine("            <section class=\"section sponsors-section\">");
            html.AppendLine("                <h3>?? Our Sponsors</h3>");
            html.AppendLine("                <div class=\"sponsors-grid\">");
            
            foreach (var sponsor in activeSponsors)
            {
                html.AppendLine("                    <div class=\"sponsor-mini\">");
                if (sponsor.LogoData.Length > 0)
                {
                    var mimeType = imageOptimizer.GetMimeType(sponsor.LogoFileName);
                    var dataUrl = imageOptimizer.ToDataUrl(sponsor.LogoData, mimeType);
                    if (!string.IsNullOrWhiteSpace(sponsor.WebsiteUrl))
                        html.AppendLine($"                        <a href=\"{sponsor.WebsiteUrl}\" target=\"_blank\"><img src=\"{dataUrl}\" alt=\"{sponsor.Name}\" class=\"sponsor-logo-mini\"></a>");
                    else
                        html.AppendLine($"                        <img src=\"{dataUrl}\" alt=\"{sponsor.Name}\" class=\"sponsor-logo-mini\">");
                }
                else
                {
                    html.AppendLine($"                        <span class=\"sponsor-name\">{sponsor.Name}</span>");
                }
                html.AppendLine("                    </div>");
            }
            
            html.AppendLine("                </div>");
            if (_settings.ShowSponsors)
                html.AppendLine("                <p class=\"view-all\"><a href=\"sponsors.html\">View All Sponsors ?</a></p>");
            html.AppendLine("            </section>");
        }
        
        private string GenerateModernCSS()
        {
            var fontUrl = _settings.FontFamily switch
            {
                "Roboto" => "https://fonts.googleapis.com/css2?family=Roboto:wght@300;400;500;600;700&display=swap",
                "Open Sans" => "https://fonts.googleapis.com/css2?family=Open+Sans:wght@300;400;500;600;700&display=swap",
                "Poppins" => "https://fonts.googleapis.com/css2?family=Poppins:wght@300;400;500;600;700&display=swap",
                _ => "https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap"
            };
            
            var fontFamily = _settings.FontFamily switch
            {
                "Roboto" => "'Roboto', -apple-system, BlinkMacSystemFont, sans-serif",
                "Open Sans" => "'Open Sans', -apple-system, BlinkMacSystemFont, sans-serif",
                "Poppins" => "'Poppins', -apple-system, BlinkMacSystemFont, sans-serif",
                _ => "'Inter', -apple-system, BlinkMacSystemFont, sans-serif"
            };
            
            var animationStyles = _settings.EnableAnimations ? @"
/* Animations */
@keyframes fadeInUp {
    from { opacity: 0; transform: translateY(20px); }
    to { opacity: 1; transform: translateY(0); }
}

@keyframes pulse {
    0%, 100% { transform: scale(1); }
    50% { transform: scale(1.02); }
}

@keyframes shimmer {
    0% { background-position: -200% 0; }
    100% { background-position: 200% 0; }
}

.stat-card, .section, .result-item, .fixture-item {
    animation: fadeInUp 0.5s ease-out;
}

.stat-card:hover {
    animation: pulse 0.3s ease-in-out;
}
" : "";

            var gradientBg = _settings.EnableGradients 
                ? $"background: linear-gradient(135deg, {_settings.PrimaryColor} 0%, {_settings.SecondaryColor} 50%, {_settings.PrimaryColor} 100%); background-size: 200% 200%;"
                : $"background: {_settings.PrimaryColor};";

            return $@"/* {_settings.LeagueName} - Professional Modern Template */
@import url('{fontUrl}');

:root {{
    --primary-color: {_settings.PrimaryColor};
    --primary-light: {_settings.PrimaryColor}22;
    --primary-dark: {_settings.SecondaryColor};
    --secondary-color: {_settings.SecondaryColor};
    --accent-color: {_settings.AccentColor};
    --bg-color: #F8FAFC;
    --bg-alt: #F1F5F9;
    --card-bg: #FFFFFF;
    --text-color: #0F172A;
    --text-secondary: #64748B;
    --text-muted: #94A3B8;
    --border-color: #E2E8F0;
    --border-light: #F1F5F9;
    --shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.04);
    --shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.07), 0 2px 4px -2px rgba(0, 0, 0, 0.05);
    --shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.08), 0 4px 6px -4px rgba(0, 0, 0, 0.04);
    --shadow-xl: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.04);
    --radius-sm: 6px;
    --radius-md: 10px;
    --radius-lg: 16px;
    --radius-xl: 24px;
    --transition-fast: 150ms cubic-bezier(0.4, 0, 0.2, 1);
    --transition-normal: 250ms cubic-bezier(0.4, 0, 0.2, 1);
    --transition-slow: 350ms cubic-bezier(0.4, 0, 0.2, 1);
}}

{animationStyles}

* {{
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}}

html {{
    scroll-behavior: smooth;
}}

body {{
    font-family: {fontFamily};
    line-height: 1.7;
    color: var(--text-color);
    background: var(--bg-color);
    font-size: 16px;
    -webkit-font-smoothing: antialiased;
    -moz-osx-font-smoothing: grayscale;
}}

.container {{
    max-width: 1280px;
    margin: 0 auto;
    padding: 0 24px;
}}

/* Header Styles */
header {{
    {gradientBg}
    color: white;
    padding: 60px 0 50px;
    text-align: center;
    position: relative;
    overflow: hidden;
}}

header::before {{
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: url(""data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='none' fill-rule='evenodd'%3E%3Cg fill='%23ffffff' fill-opacity='0.05'%3E%3Cpath d='M36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E"");
    pointer-events: none;
}}

header .container {{
    position: relative;
    z-index: 1;
}}

header h1 {{
    font-size: clamp(2rem, 5vw, 3.5rem);
    font-weight: 800;
    margin-bottom: 12px;
    letter-spacing: -0.02em;
    text-shadow: 0 2px 4px rgba(0,0,0,0.1);
}}

header .subtitle {{
    font-size: clamp(1rem, 2vw, 1.25rem);
    opacity: 0.92;
    font-weight: 400;
    max-width: 600px;
    margin: 0 auto;
}}

.season-badge {{
    display: inline-flex;
    align-items: center;
    gap: 8px;
    margin-top: 24px;
    padding: 12px 28px;
    background: rgba(255,255,255,0.18);
    backdrop-filter: blur(10px);
    border-radius: 50px;
    font-weight: 600;
    font-size: 0.95rem;
    border: 1px solid rgba(255,255,255,0.2);
    box-shadow: 0 4px 12px rgba(0,0,0,0.1);
}}

/* Navigation */
nav {{
    background: var(--card-bg);
    box-shadow: var(--shadow-md);
    position: sticky;
    top: 0;
    z-index: 1000;
    border-bottom: 1px solid var(--border-color);
}}

nav ul {{
    list-style: none;
    display: flex;
    justify-content: center;
    flex-wrap: wrap;
    gap: 4px;
    padding: 8px 0;
}}

nav a {{
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 12px 20px;
    color: var(--text-secondary);
    text-decoration: none;
    font-weight: 500;
    font-size: 0.95rem;
    border-radius: var(--radius-md);
    transition: all var(--transition-fast);
    position: relative;
}}

nav a:hover {{
    color: var(--primary-color);
    background: var(--primary-light);
}}

nav a.active {{
    color: var(--primary-color);
    background: var(--primary-light);
    font-weight: 600;
}}

nav a.active::after {{
    content: '';
    position: absolute;
    bottom: 4px;
    left: 50%;
    transform: translateX(-50%);
    width: 24px;
    height: 3px;
    background: var(--primary-color);
    border-radius: 3px;
}}

/* Main Content */
main {{
    padding: 48px 0 60px;
    min-height: calc(100vh - 350px);
}}

.hero {{
    text-align: center;
    margin-bottom: 48px;
    padding: 20px;
}}

.hero h2 {{
    font-size: clamp(1.5rem, 3vw, 2.25rem);
    font-weight: 700;
    color: var(--text-color);
    margin-bottom: 12px;
}}

.hero h2::after {{
    content: '';
    display: block;
    width: 60px;
    height: 4px;
    background: linear-gradient(90deg, var(--primary-color), var(--accent-color));
    margin: 16px auto 0;
    border-radius: 2px;
}}

.hero-dates {{
    color: var(--text-secondary);
    font-size: 1.1rem;
    font-weight: 400;
}}

/* Stats Grid */
.stats-grid {{
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    gap: 24px;
    margin-bottom: 48px;
}}

.stat-card {{
    background: var(--card-bg);
    padding: 32px 24px;
    border-radius: var(--radius-lg);
    text-align: center;
    box-shadow: var(--shadow-md);
    border: 1px solid var(--border-light);
    transition: all var(--transition-normal);
    position: relative;
    overflow: hidden;
}}

.stat-card:hover {{
    transform: translateY(-4px);
    box-shadow: var(--shadow-lg);
    border-color: var(--primary-color);
}}

.stat-number {{
    font-size: clamp(2.5rem, 4vw, 3.5rem);
    font-weight: 800;
    background: linear-gradient(135deg, var(--primary-color), var(--secondary-color));
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
    margin-bottom: 8px;
    line-height: 1.1;
}}

.stat-label {{
    color: var(--text-secondary);
    font-size: 0.875rem;
    text-transform: uppercase;
    letter-spacing: 1.5px;
    font-weight: 600;
}}

/* Section Cards */
.section {{
    background: var(--card-bg);
    padding: 32px;
    border-radius: var(--radius-lg);
    margin-bottom: 32px;
    box-shadow: var(--shadow-md);
    border: 1px solid var(--border-light);
}}

.section h3 {{
    color: var(--text-color);
    margin-bottom: 24px;
    font-size: 1.375rem;
    font-weight: 700;
    display: flex;
    align-items: center;
    gap: 12px;
}}

.section h3::before {{
    content: '';
    width: 4px;
    height: 24px;
    background: linear-gradient(to bottom, var(--primary-color), var(--accent-color));
    border-radius: 2px;
}}

.section h4 {{
    color: var(--text-color);
    margin: 24px 0 16px;
    font-size: 1.1rem;
    font-weight: 600;
}}

/* Results & Fixtures Lists */
.results-list,
.fixtures-list {{
    display: flex;
    flex-direction: column;
    gap: 12px;
}}

.result-item,
.fixture-item {{
    display: grid;
    grid-template-columns: 100px 1fr auto 1fr 120px;
    gap: 16px;
    padding: 20px 24px;
    background: var(--bg-alt);
    border-radius: var(--radius-md);
    align-items: center;
    transition: all var(--transition-fast);
    border: 1px solid transparent;
}}

.result-item:hover,
.fixture-item:hover {{
    background: var(--card-bg);
    border-color: var(--border-color);
    box-shadow: var(--shadow-sm);
}}

.date {{
    color: var(--text-muted);
    font-size: 0.875rem;
    font-weight: 600;
    background: var(--card-bg);
    padding: 8px 12px;
    border-radius: var(--radius-sm);
    text-align: center;
    border: 1px solid var(--border-color);
    line-height: 1.3;
}}

.team {{
    font-weight: 600;
    font-size: 0.95rem;
    color: var(--text-color);
    text-transform: uppercase;
    text-align: center;
}}

.home-team {{
    text-align: center;
    justify-self: center;
}}

.away-team {{
    text-align: center;
    justify-self: center;
}}

.team:first-of-type {{
    text-align: center;
    justify-self: center;
}}

.team:last-of-type {{
    text-align: center;
    justify-self: center;
}}

.score {{
    font-size: 1.25rem;
    font-weight: 800;
    color: var(--card-bg);
    background: linear-gradient(135deg, var(--primary-color), var(--secondary-color));
    padding: 8px 20px;
    border-radius: var(--radius-md);
    text-align: center;
    min-width: 90px;
    box-shadow: var(--shadow-sm);
}}

.vs {{
    color: var(--text-muted);
    text-align: center;
    font-size: 0.8rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 1px;
    background: var(--bg-color);
    padding: 8px 16px;
    border-radius: var(--radius-sm);
}}

.view-all {{
    text-align: center;
    margin-top: 24px;
    padding-top: 24px;
    border-top: 1px solid var(--border-color);
}}

.view-all a {{
    display: inline-flex;
    align-items: center;
    gap: 8px;
    color: var(--primary-color);
    text-decoration: none;
    font-weight: 600;
    padding: 12px 24px;
    border-radius: var(--radius-md);
    background: var(--primary-light);
    transition: all var(--transition-fast);
}}

.view-all a:hover {{
    background: var(--primary-color);
    color: white;
    transform: translateX(4px);
}}

/* Tables */
table {{
    width: 100%;
    border-collapse: separate;
    border-spacing: 0;
    background: var(--card-bg);
    border-radius: var(--radius-md);
    overflow: hidden;
    border: 1px solid var(--border-color);
}}

thead {{
    background: linear-gradient(135deg, var(--primary-color), var(--secondary-color));
    color: white;
}}

th {{
    padding: 16px 18px;
    font-weight: 600;
    text-transform: uppercase;
    font-size: 0.75rem;
    letter-spacing: 1px;
    text-align: left;
}}

td {{
    padding: 16px 18px;
    text-align: left;
    font-size: 0.95rem;
}}

tbody tr {{
    border-bottom: 1px solid var(--border-color);
    transition: background var(--transition-fast);
}}

tbody tr:last-child {{
    border-bottom: none;
}}

tbody tr:hover {{
    background: var(--bg-color);
}}

tbody tr:nth-child(1) td:first-child::before {{
    content: '??';
    margin-right: 4px;
}}

tbody tr:nth-child(2) td:first-child::before {{
    content: '??';
    margin-right: 4px;
}}

tbody tr:nth-child(3) td:first-child::before {{
    content: '??';
    margin-right: 4px;
}}

.winner {{
    color: var(--primary-color);
    font-weight: 700;
}}

/* Badges */
.division-badge {{
    display: inline-flex;
    align-items: center;
    padding: 6px 14px;
    background: var(--accent-color);
    color: white;
    border-radius: 20px;
    font-size: 0.75rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}}

.venue {{
    color: var(--text-muted);
    font-size: 0.85rem;
    text-align: right;
    justify-self: end;
}}

.venue::before {{
    content: '??';
    margin-right: 4px;
}}

.division-notes {{
    color: var(--text-secondary);
    font-style: italic;
    margin-bottom: 24px;
    padding: 16px;
    background: var(--bg-color);
    border-radius: var(--radius-md);
    border-left: 4px solid var(--primary-color);
}}

/* Team List */
.team-list {{
    list-style: none;
    padding: 0;
    display: grid;
    gap: 10px;
}}

.team-list li {{
    padding: 16px 20px;
    background: var(--bg-color);
    border-radius: var(--radius-md);
    transition: all var(--transition-fast);
    display: flex;
    justify-content: space-between;
    align-items: center;
    border: 1px solid var(--border-color);
}}

.team-list li:hover {{
    border-color: var(--primary-color);
    transform: translateX(4px);
}}

/* Player Links */
.player-link {{
    color: var(--primary-color);
    text-decoration: none;
    transition: all var(--transition-fast);
}}

.player-link:hover {{
    color: var(--secondary-color);
    text-decoration: underline;
}}

.back-link {{
    display: inline-flex;
    align-items: center;
    gap: 8px;
    color: var(--primary-color);
    text-decoration: none;
    font-weight: 600;
    padding: 12px 24px;
    border-radius: var(--radius-md);
    background: var(--primary-light);
    transition: all var(--transition-fast);
}}

.back-link:hover {{
    background: var(--primary-color);
    color: white;
}}

.text-positive {{
    color: #10B981;
}}

.text-negative {{
    color: #EF4444;
}}

/* Footer */
footer {{
    background: #020617;
    color: white;
    padding: 40px 0;
    text-align: center;
    margin-top: 60px;
    border-top: 1px solid var(--border-color);
}}

footer p {{
    opacity: 0.7;
    font-size: 0.9rem;
}}

@media (max-width: 768px) {{
    header {{
        padding: 40px 0 35px;
    }}
    
    .stats-grid {{
        grid-template-columns: repeat(2, 1fr);
        gap: 16px;
    }}
    
    .stat-card {{
        padding: 24px 16px;
    }}
    
    .result-item,
    .fixture-item {{
        grid-template-columns: 1fr;
        gap: 12px;
        text-align: center;
        padding: 20px;
    }}
    
    .result-item .date,
    .fixture-item .date {{
        order: -1;
        justify-self: center;
    }}
    
    .team:first-of-type,
    .team:last-of-type,
    .home-team,
    .away-team {{
        text-align: center;
        justify-self: center;
    }}
    
    .venue {{
        justify-self: center;
        text-align: center;
    }}
    
    nav ul {{
        padding: 8px;
    }}
    
    nav a {{
        padding: 10px 14px;
        font-size: 0.9rem;
    }}
    
    table {{
        font-size: 0.85rem;
        display: block;
        overflow-x: auto;
    }}
    
    th, td {{
        padding: 12px 10px;
        white-space: nowrap;
    }}
    
    .section {{
        padding: 20px;
        margin-bottom: 20px;
    }}
}}

@media (max-width: 480px) {{
    .stats-grid {{
        grid-template-columns: 1fr;
    }}
    
    nav ul {{
        justify-content: flex-start;
        overflow-x: auto;
        flex-wrap: nowrap;
    }}
}}

/* Print Styles */
@media print {{
    header {{
        background: var(--primary-color) !important;
        -webkit-print-color-adjust: exact;
        print-color-adjust: exact;
    }}
    
    nav {{
        display: none;
    }}
    
    .stat-card, .section {{
        break-inside: avoid;
        box-shadow: none;
        border: 1px solid var(--border-color);
    }}
}}
";
        }
        
        private string GenerateDarkModeCSS()
        {
            return GenerateModernCSS().Replace("--bg-color: #F8FAFC;", "--bg-color: #0F172A;")
                .Replace("--bg-alt: #F1F5F9;", "--bg-alt: #1E293B;")
                .Replace("--card-bg: #FFFFFF;", "--card-bg: #1E293B;")
                .Replace("--text-color: #0F172A;", "--text-color: #F1F5F9;")
                .Replace("--text-secondary: #64748B;", "--text-secondary: #94A3B8;")
                .Replace("--border-color: #E2E8F0;", "--border-color: #334155;")
                .Replace("--border-light: #F1F5F9;", "--border-light: #334155;");
        }
        
        private string GenerateSportCSS()
        {
            return GenerateModernCSS();
        }
        
        private string GenerateMinimalistCSS()
        {
            return GenerateModernCSS();
        }
        
        private void AppendHeader(StringBuilder html, Season season)
        {
            var imageOptimizer = new ImageOptimizationService();
            
            html.AppendLine("    <header>");
            html.AppendLine("        <div class=\"container\">");
            
            // Logo
            if (_settings.UseCustomLogo && _settings.LogoImageData != null && _settings.LogoImageData.Length > 0)
            {
                var mimeType = imageOptimizer.GetMimeType(_settings.LogoPath ?? "logo.png");
                var dataUrl = imageOptimizer.ToDataUrl(_settings.LogoImageData, mimeType);
                html.AppendLine($"            <img src=\"{dataUrl}\" alt=\"{_settings.LeagueName}\" class=\"logo\" style=\"max-width: {_settings.LogoMaxWidth}px; max-height: {_settings.LogoMaxHeight}px;\">");
            }
            
            html.AppendLine($"            <h1>{_settings.LeagueName}</h1>");
            html.AppendLine($"            <p class=\"subtitle\">{_settings.LeagueSubtitle}</p>");
            
            if (_settings.ShowSeasonBadge)
            {
                html.AppendLine($"            <div class=\"season-badge\">?? {season.Name}</div>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </header>");
        }
        
        private void AppendNavigation(StringBuilder html, string currentPage)
        {
            html.AppendLine("    <nav>");
            html.AppendLine("        <ul>");
            
            html.AppendLine($"            <li><a href=\"index.html\"{(currentPage == "Home" ? " class=\"active\"" : "")}>Home</a></li>");
            
            if (_settings.ShowStandings)
                html.AppendLine($"            <li><a href=\"standings.html\"{(currentPage == "Standings" ? " class=\"active\"" : "")}>Standings</a></li>");
            
            if (_settings.ShowFixtures)
                html.AppendLine($"            <li><a href=\"fixtures.html\"{(currentPage == "Fixtures" ? " class=\"active\"" : "")}>Fixtures</a></li>");
            
            if (_settings.ShowResults)
                html.AppendLine($"            <li><a href=\"results.html\"{(currentPage == "Results" ? " class=\"active\"" : "")}>Results</a></li>");
            
            if (_settings.ShowPlayerStats)
                html.AppendLine($"            <li><a href=\"players.html\"{(currentPage == "Players" ? " class=\"active\"" : "")}>Players</a></li>");
            
            if (_settings.ShowDivisions)
                html.AppendLine($"            <li><a href=\"divisions.html\"{(currentPage == "Divisions" ? " class=\"active\"" : "")}>Divisions</a></li>");
            
            if (_settings.ShowGallery && _settings.GalleryImages.Count > 0)
                html.AppendLine($"            <li><a href=\"gallery.html\"{(currentPage == "Gallery" ? " class=\"active\"" : "")}>Gallery</a></li>");
            
            if (_settings.ShowRules && !string.IsNullOrWhiteSpace(_settings.RulesContent))
                html.AppendLine($"            <li><a href=\"rules.html\"{(currentPage == "Rules" ? " class=\"active\"" : "")}>Rules</a></li>");
            
            if (_settings.ShowContactPage && _settings.HasContactInfo)
                html.AppendLine($"            <li><a href=\"contact.html\"{(currentPage == "Contact" ? " class=\"active\"" : "")}>Contact</a></li>");
            
            if (_settings.ShowSponsors && _settings.Sponsors.Count > 0)
                html.AppendLine($"            <li><a href=\"sponsors.html\"{(currentPage == "Sponsors" ? " class=\"active\"" : "")}>Sponsors</a></li>");
            
            if (_settings.ShowNews && _settings.NewsItems.Count > 0)
                html.AppendLine($"            <li><a href=\"news.html\"{(currentPage == "News" ? " class=\"active\"" : "")}>News</a></li>");
            
            // Custom pages in nav
            foreach (var page in _settings.CustomPages.Where(p => p.IsPublished && p.ShowInNav).OrderBy(p => p.NavOrder))
            {
                var slug = string.IsNullOrWhiteSpace(page.Slug) ? page.Title.ToLower().Replace(" ", "-") : page.Slug;
                html.AppendLine($"            <li><a href=\"{slug}.html\"{(currentPage == page.Title ? " class=\"active\"" : "")}>{page.Title}</a></li>");
            }
            
            html.AppendLine("        </ul>");
            html.AppendLine("    </nav>");
        }
        
        private void AppendFooter(StringBuilder html)
        {
            html.AppendLine("    <footer>");
            html.AppendLine("        <div class=\"container\">");
            
            if (_settings.ShowFooterSocialLinks && _settings.HasSocialLinks)
            {
                html.AppendLine("            <div class=\"social-links\">");
                if (!string.IsNullOrWhiteSpace(_settings.FacebookUrl))
                    html.AppendLine($"                <a href=\"{_settings.FacebookUrl}\" target=\"_blank\">Facebook</a>");
                if (!string.IsNullOrWhiteSpace(_settings.TwitterUrl))
                    html.AppendLine($"                <a href=\"{_settings.TwitterUrl}\" target=\"_blank\">Twitter</a>");
                if (!string.IsNullOrWhiteSpace(_settings.InstagramUrl))
                    html.AppendLine($"                <a href=\"{_settings.InstagramUrl}\" target=\"_blank\">Instagram</a>");
                if (!string.IsNullOrWhiteSpace(_settings.YouTubeUrl))
                    html.AppendLine($"                <a href=\"{_settings.YouTubeUrl}\" target=\"_blank\">YouTube</a>");
                if (!string.IsNullOrWhiteSpace(_settings.TikTokUrl))
                    html.AppendLine($"                <a href=\"{_settings.TikTokUrl}\" target=\"_blank\">TikTok</a>");
                html.AppendLine("            </div>");
            }
            
            if (_settings.ShowFooterContact && _settings.HasContactInfo)
            {
                html.AppendLine("            <div class=\"footer-contact\">");
                if (!string.IsNullOrWhiteSpace(_settings.ContactEmail))
                    html.AppendLine($"                <p>?? {_settings.ContactEmail}</p>");
                if (!string.IsNullOrWhiteSpace(_settings.ContactPhone))
                    html.AppendLine($"                <p>?? {_settings.ContactPhone}</p>");
                html.AppendLine("            </div>");
            }
            
            if (!string.IsNullOrWhiteSpace(_settings.CustomFooterText))
            {
                html.AppendLine($"            <p>{_settings.CustomFooterText}</p>");
            }
            
            var copyrightText = string.IsNullOrWhiteSpace(_settings.CopyrightText)
                ? $"© {DateTime.Now.Year} {_settings.LeagueName}"
                : _settings.CopyrightText;
            html.AppendLine($"            <p>{copyrightText}</p>");
            
            if (_settings.ShowLastUpdated)
            {
                html.AppendLine($"            <p class=\"last-updated\">Last updated: {DateTime.Now:dd MMMM yyyy HH:mm}</p>");
            }
            
            if (_settings.ShowPoweredBy)
            {
                html.AppendLine("            <p class=\"powered-by\">Powered by Pool League Manager</p>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </footer>");
        }
        
        private DateTime GetWeekStart(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff).Date;
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
            html.AppendLine($"                <p class=\"hero-dates\">{_settings.GalleryImages.Count} Photos</p>");
            html.AppendLine("            </div>");
            
            var categories = _settings.GalleryImages
                .Select(i => i.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            
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
            
            html.AppendLine($"            <div class=\"gallery-{_settings.GalleryLayout}\" style=\"grid-template-columns: repeat({_settings.GalleryColumns}, 1fr);\">");
            
            foreach (var image in _settings.GalleryImages.OrderBy(i => i.SortOrder))
            {
                var mimeType = imageOptimizer.GetMimeType(image.FileName);
                var dataUrl = imageOptimizer.ToDataUrl(image.ImageData, mimeType);
                var categoryClass = image.Category.ToLower().Replace(" ", "-");
                
                html.AppendLine($"                <div class=\"gallery-item\" data-category=\"{categoryClass}\">");
                html.AppendLine($"                    <img src=\"{dataUrl}\" alt=\"{image.Caption}\" loading=\"lazy\">");
                if (_settings.GalleryShowCaptions && !string.IsNullOrWhiteSpace(image.Caption))
                {
                    html.AppendLine($"                    <div class=\"gallery-caption\">{image.Caption}</div>");
                }
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
            
            // Get season start date for rating calculation
            var seasonId = _settings.SelectedSeasonId;
            var season = seasonId.HasValue 
                ? _league.Seasons.FirstOrDefault(s => s.Id == seasonId.Value)
                : _league.Seasons.FirstOrDefault(s => s.IsActive);
            var seasonStartDate = season?.StartDate ?? DateTime.Now.AddMonths(-6);
            
            // Use the shared RatingCalculator to get all player ratings
            // This ensures website ratings match the app exactly
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
                    .Where(f => f.Frames.Any() && (f.HomeTeamId == team.Id || f.AwayTeamId == team.Id))
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
