using System;
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
                files["players.html"] = GeneratePlayersPage(season, template);
            
            if (_settings.ShowDivisions)
                files["divisions.html"] = GenerateDivisionsPage(season, template);
            
            if (_settings.ShowGallery && _settings.GalleryImages.Count > 0)
                files["gallery.html"] = GenerateGalleryPage(season, template);
            
            return files;
        }
        
        private string GenerateIndexPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine($"    <title>{_settings.LeagueName} - {season.Name}</title>");
            html.AppendLine("    <link rel=\"stylesheet\" href=\"style.css\">");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            AppendHeader(html, season);
            AppendNavigation(html, "Home");
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2>Welcome to {season.Name}</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{season.StartDate:MMMM d, yyyy} - {season.EndDate:MMMM d, yyyy}</p>");
            html.AppendLine("            </div>");
            
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            var completedFixtures = fixtures.Count(f => f.Frames.Any(fr => fr.Winner != FrameWinner.None));
            
            html.AppendLine("            <div class=\"stats-grid\">");
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
            
            if (_settings.ShowResults && completedFixtures > 0)
            {
                html.AppendLine("            <section class=\"section\">");
                html.AppendLine("                <h3>Recent Results</h3>");
                
                var recentResults = fixtures
                    .Where(f => f.Frames.Any(fr => fr.Winner != FrameWinner.None))
                    .OrderByDescending(f => f.Date)
                    .Take(5)
                    .ToList();
                
                html.AppendLine("                <div class=\"results-list\">");
                foreach (var fixture in recentResults)
                {
                    var homeTeam = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
                    var awayTeam = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId);
                    
                    html.AppendLine("                    <div class=\"result-item\">");
                    html.AppendLine($"                        <span class=\"date\">{fixture.Date:ddd dd MMM}</span>");
                    html.AppendLine($"                        <span class=\"team\">{homeTeam?.Name ?? "Home"}</span>");
                    html.AppendLine($"                        <span class=\"score\">{fixture.HomeScore} - {fixture.AwayScore}</span>");
                    html.AppendLine($"                        <span class=\"team\">{awayTeam?.Name ?? "Away"}</span>");
                    html.AppendLine("                    </div>");
                }
                html.AppendLine("                </div>");
                html.AppendLine("                <p class=\"view-all\"><a href=\"results.html\">View All Results ?</a></p>");
                html.AppendLine("            </section>");
            }
            
            if (_settings.ShowFixtures)
            {
                var upcomingFixtures = fixtures
                    .Where(f => f.Date > DateTime.Now)
                    .OrderBy(f => f.Date)
                    .Take(5)
                    .ToList();
                
                if (upcomingFixtures.Any())
                {
                    html.AppendLine("            <section class=\"section\">");
                    html.AppendLine("                <h3>Upcoming Fixtures</h3>");
                    html.AppendLine("                <div class=\"fixtures-list\">");
                    
                    foreach (var fixture in upcomingFixtures)
                    {
                        var homeTeam = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
                        var awayTeam = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId);
                        
                        html.AppendLine("                    <div class=\"fixture-item\">");
                        html.AppendLine($"                        <span class=\"date\">{fixture.Date:ddd dd MMM HH:mm}</span>");
                        html.AppendLine($"                        <span class=\"team\">{homeTeam?.Name ?? "Home"}</span>");
                        html.AppendLine("                        <span class=\"vs\">vs</span>");
                        html.AppendLine($"                        <span class=\"team\">{awayTeam?.Name ?? "Away"}</span>");
                        html.AppendLine("                    </div>");
                    }
                    
                    html.AppendLine("                </div>");
                    html.AppendLine("                <p class=\"view-all\"><a href=\"fixtures.html\">View All Fixtures ?</a></p>");
                    html.AppendLine("            </section>");
                }
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
            AppendFooter(html);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
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
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine($"    <title>Standings - {_settings.LeagueName}</title>");
            html.AppendLine("    <link rel=\"stylesheet\" href=\"style.css\">");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
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
                html.AppendLine("                <table>");
                html.AppendLine("                    <thead>");
                html.AppendLine("                        <tr>");
                html.AppendLine("                            <th>Pos</th>");
                html.AppendLine("                            <th>Team</th>");
                html.AppendLine("                            <th>P</th>");
                html.AppendLine("                            <th>W</th>");
                html.AppendLine("                            <th>D</th>");
                html.AppendLine("                            <th>L</th>");
                html.AppendLine("                            <th>F</th>");
                html.AppendLine("                            <th>A</th>");
                html.AppendLine("                            <th>Diff</th>");
                html.AppendLine("                            <th>Pts</th>");
                html.AppendLine("                        </tr>");
                html.AppendLine("                    </thead>");
                html.AppendLine("                    <tbody>");
                
                var standings = CalculateStandings(divisionTeams, fixtures);
                var position = 1;
                
                foreach (var standing in standings.OrderByDescending(s => s.Points)
                    .ThenByDescending(s => s.FramesDiff)
                    .ThenByDescending(s => s.FramesFor))
                {
                    html.AppendLine("                        <tr>");
                    html.AppendLine($"                            <td>{position++}</td>");
                    html.AppendLine($"                            <td>{standing.TeamName}</td>");
                    html.AppendLine($"                            <td>{standing.Played}</td>");
                    html.AppendLine($"                            <td>{standing.Won}</td>");
                    html.AppendLine($"                            <td>{standing.Drawn}</td>");
                    html.AppendLine($"                            <td>{standing.Lost}</td>");
                    html.AppendLine($"                            <td>{standing.FramesFor}</td>");
                    html.AppendLine($"                            <td>{standing.FramesAgainst}</td>");
                    html.AppendLine($"                            <td>{standing.FramesDiff:+0;-0;0}</td>");
                    html.AppendLine($"                            <td><strong>{standing.Points}</strong></td>");
                    html.AppendLine("                        </tr>");
                }
                
                html.AppendLine("                    </tbody>");
                html.AppendLine("                </table>");
                html.AppendLine("            </div>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
            AppendFooter(html);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private string GenerateFixturesPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine($"    <title>Fixtures - {_settings.LeagueName}</title>");
            html.AppendLine("    <link rel=\"stylesheet\" href=\"style.css\">");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            AppendHeader(html, season);
            AppendNavigation(html, "Fixtures");
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>Fixtures</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">Upcoming Matches</p>");
            html.AppendLine("            </div>");
            
            var upcomingFixtures = fixtures
                .Where(f => f.Date >= DateTime.Now)
                .OrderBy(f => f.Date)
                .GroupBy(f => f.Date.Date)
                .ToList();
            
            if (upcomingFixtures.Any())
            {
                foreach (var dateGroup in upcomingFixtures)
                {
                    html.AppendLine("            <div class=\"section\">");
                    html.AppendLine($"                <h3>{dateGroup.Key:dddd, dd MMMM yyyy}</h3>");
                    html.AppendLine("                <div class=\"fixtures-list\">");
                    
                    foreach (var fixture in dateGroup.OrderBy(f => f.Date))
                    {
                        var homeTeam = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
                        var awayTeam = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId);
                        var venue = fixture.VenueId.HasValue ? venues.FirstOrDefault(v => v.Id == fixture.VenueId.Value) : null;
                        var division = fixture.DivisionId.HasValue ? divisions.FirstOrDefault(d => d.Id == fixture.DivisionId.Value) : null;
                        
                        html.AppendLine("                    <div class=\"fixture-item\">");
                        html.AppendLine($"                        <span class=\"date\">{fixture.Date:HH:mm}</span>");
                        html.AppendLine($"                        <span class=\"team\">{homeTeam?.Name ?? "TBD"}</span>");
                        html.AppendLine("                        <span class=\"vs\">vs</span>");
                        html.AppendLine($"                        <span class=\"team\">{awayTeam?.Name ?? "TBD"}</span>");
                        if (venue != null)
                        {
                            html.AppendLine($"                        <span class=\"venue\">{venue.Name}</span>");
                        }
                        if (division != null)
                        {
                            html.AppendLine($"                        <span class=\"division-badge\">{division.Name}</span>");
                        }
                        html.AppendLine("                    </div>");
                    }
                    
                    html.AppendLine("                </div>");
                    html.AppendLine("            </div>");
                }
            }
            else
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <p>No upcoming fixtures scheduled.</p>");
                html.AppendLine("            </div>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
            AppendFooter(html);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private string GenerateResultsPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine($"    <title>Results - {_settings.LeagueName}</title>");
            html.AppendLine("    <link rel=\"stylesheet\" href=\"style.css\">");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            AppendHeader(html, season);
            AppendNavigation(html, "Results");
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>Match Results</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">Latest Results</p>");
            html.AppendLine("            </div>");
            
            var completedFixtures = fixtures
                .Where(f => f.Frames.Any(fr => fr.Winner != FrameWinner.None))
                .OrderByDescending(f => f.Date)
                .GroupBy(f => f.Date.Date)
                .Take(10)
                .ToList();
            
            if (completedFixtures.Any())
            {
                foreach (var dateGroup in completedFixtures)
                {
                    html.AppendLine("            <div class=\"section\">");
                    html.AppendLine($"                <h3>{dateGroup.Key:dddd, dd MMMM yyyy}</h3>");
                    html.AppendLine("                <div class=\"results-list\">");
                    
                    foreach (var fixture in dateGroup.OrderByDescending(f => f.Date))
                    {
                        var homeTeam = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
                        var awayTeam = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId);
                        var division = fixture.DivisionId.HasValue ? divisions.FirstOrDefault(d => d.Id == fixture.DivisionId.Value) : null;
                        
                        var isHomeWin = fixture.HomeScore > fixture.AwayScore;
                        var isDraw = fixture.HomeScore == fixture.AwayScore;
                        
                        html.AppendLine("                    <div class=\"result-item\">");
                        html.AppendLine($"                        <span class=\"date\">{fixture.Date:HH:mm}</span>");
                        html.AppendLine($"                        <span class=\"team{(isHomeWin ? " winner" : "")}\">{homeTeam?.Name ?? "Home"}</span>");
                        html.AppendLine($"                        <span class=\"score\">{fixture.HomeScore} - {fixture.AwayScore}</span>");
                        html.AppendLine($"                        <span class=\"team{(!isHomeWin && !isDraw ? " winner" : "")}\">{awayTeam?.Name ?? "Away"}</span>");
                        if (division != null)
                        {
                            html.AppendLine($"                        <span class=\"division-badge\">{division.Name}</span>");
                        }
                        html.AppendLine("                    </div>");
                    }
                    
                    html.AppendLine("                </div>");
                    html.AppendLine("            </div>");
                }
            }
            else
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <p>No results available yet.</p>");
                html.AppendLine("            </div>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
            AppendFooter(html);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private string GeneratePlayersPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine($"    <title>Players - {_settings.LeagueName}</title>");
            html.AppendLine("    <link rel=\"stylesheet\" href=\"style.css\">");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            AppendHeader(html, season);
            AppendNavigation(html, "Players");
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>Player Statistics</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{players.Count} Players</p>");
            html.AppendLine("            </div>");
            
            var playerStats = CalculatePlayerStats(players, teams, fixtures);
            
            if (playerStats.Any())
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <h3>Top Performers</h3>");
                html.AppendLine("                <table>");
                html.AppendLine("                    <thead>");
                html.AppendLine("                        <tr>");
                html.AppendLine("                            <th>Pos</th>");
                html.AppendLine("                            <th>Player</th>");
                html.AppendLine("                            <th>Team</th>");
                html.AppendLine("                            <th>Played</th>");
                html.AppendLine("                            <th>Won</th>");
                html.AppendLine("                            <th>Lost</th>");
                html.AppendLine("                            <th>Win %</th>");
                html.AppendLine("                            <th>8-Balls</th>");
                html.AppendLine("                        </tr>");
                html.AppendLine("                    </thead>");
                html.AppendLine("                    <tbody>");
                
                var position = 1;
                foreach (var stat in playerStats.OrderByDescending(s => s.WinPercentage).ThenByDescending(s => s.Won).Take(50))
                {
                    html.AppendLine("                        <tr>");
                    html.AppendLine($"                            <td>{position++}</td>");
                    html.AppendLine($"                            <td><strong>{stat.PlayerName}</strong></td>");
                    html.AppendLine($"                            <td>{stat.TeamName}</td>");
                    html.AppendLine($"                            <td>{stat.Played}</td>");
                    html.AppendLine($"                            <td>{stat.Won}</td>");
                    html.AppendLine($"                            <td>{stat.Lost}</td>");
                    html.AppendLine($"                            <td>{stat.WinPercentage:F1}%</td>");
                    html.AppendLine($"                            <td>{stat.EightBalls}</td>");
                    html.AppendLine("                        </tr>");
                }
                
                html.AppendLine("                    </tbody>");
                html.AppendLine("                </table>");
                html.AppendLine("            </div>");
            }
            else
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <p>No player statistics available yet.</p>");
                html.AppendLine("            </div>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
            AppendFooter(html);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private string GenerateDivisionsPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var (divisions, venues, teams, players, fixtures) = _league.GetSeasonData(season.Id);
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine($"    <title>Divisions - {_settings.LeagueName}</title>");
            html.AppendLine("    <link rel=\"stylesheet\" href=\"style.css\">");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            AppendHeader(html, season);
            AppendNavigation(html, "Divisions");
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>Divisions</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{divisions.Count} Division(s)</p>");
            html.AppendLine("            </div>");
            
            foreach (var division in divisions.OrderBy(d => d.Name))
            {
                var divisionTeams = teams.Where(t => t.DivisionId == division.Id).ToList();
                var divisionPlayers = players.Where(p => divisionTeams.Any(t => t.Id == p.TeamId)).ToList();
                
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine($"                <h3>{division.Name}</h3>");
                
                if (!string.IsNullOrWhiteSpace(division.Notes))
                {
                    html.AppendLine($"                <p class=\"division-notes\">{division.Notes}</p>");
                }
                
                html.AppendLine("                <div class=\"stats-grid\">");
                html.AppendLine("                    <div class=\"stat-card\">");
                html.AppendLine($"                        <div class=\"stat-number\">{divisionTeams.Count}</div>");
                html.AppendLine("                        <div class=\"stat-label\">Teams</div>");
                html.AppendLine("                    </div>");
                html.AppendLine("                    <div class=\"stat-card\">");
                html.AppendLine($"                        <div class=\"stat-number\">{divisionPlayers.Count}</div>");
                html.AppendLine("                        <div class=\"stat-label\">Players</div>");
                html.AppendLine("                    </div>");
                html.AppendLine("                </div>");
                
                if (divisionTeams.Any())
                {
                    html.AppendLine("                <h4>Teams</h4>");
                    html.AppendLine("                <ul class=\"team-list\">");
                    foreach (var team in divisionTeams.OrderBy(t => t.Name))
                    {
                        var teamPlayers = players.Where(p => p.TeamId == team.Id).ToList();
                        html.AppendLine($"                    <li><strong>{team.Name}</strong> ({teamPlayers.Count} players)</li>");
                    }
                    html.AppendLine("                </ul>");
                }
                
                html.AppendLine("            </div>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
            AppendFooter(html);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private string GenerateGalleryPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();
            var imageOptimizer = new ImageOptimizationService();
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine("<head>");
            html.AppendLine("    <meta charset=\"UTF-8\">");
            html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            html.AppendLine($"    <title>Gallery - {_settings.LeagueName}</title>");
            html.AppendLine("    <link rel=\"stylesheet\" href=\"style.css\">");
            html.AppendLine("    <style>");
            html.AppendLine("        .gallery-grid {");
            html.AppendLine("            display: grid;");
            html.AppendLine("            grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));");
            html.AppendLine("            gap: 20px;");
            html.AppendLine("            padding: 20px 0;");
            html.AppendLine("        }");
            html.AppendLine("        .gallery-item {");
            html.AppendLine("            background: var(--card-bg);");
            html.AppendLine("            border-radius: 12px;");
            html.AppendLine("            overflow: hidden;");
            html.AppendLine("            box-shadow: 0 2px 4px rgba(0,0,0,0.1);");
            html.AppendLine("            transition: transform 0.3s ease;");
            html.AppendLine("        }");
            html.AppendLine("        .gallery-item:hover {");
            html.AppendLine("            transform: translateY(-4px);");
            html.AppendLine("            box-shadow: 0 8px 12px rgba(0,0,0,0.15);");
            html.AppendLine("        }");
            html.AppendLine("        .gallery-item img {");
            html.AppendLine("            width: 100%;");
            html.AppendLine("            height: 250px;");
            html.AppendLine("            object-fit: cover;");
            html.AppendLine("            display: block;");
            html.AppendLine("        }");
            html.AppendLine("        .gallery-item .caption {");
            html.AppendLine("            padding: 15px;");
            html.AppendLine("            font-size: 0.9rem;");
            html.AppendLine("            color: var(--text-secondary);");
            html.AppendLine("        }");
            html.AppendLine("        .gallery-item .category {");
            html.AppendLine("            display: inline-block;");
            html.AppendLine("            padding: 4px 10px;");
            html.AppendLine("            background: var(--accent-color);");
            html.AppendLine("            color: white;");
            html.AppendLine("            border-radius: 12px;");
            html.AppendLine("            font-size: 0.75rem;");
            html.AppendLine("            margin-bottom: 8px;");
            html.AppendLine("        }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");
            
            AppendHeader(html, season);
            AppendNavigation(html, "Gallery");
            
            html.AppendLine("    <main>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine("                <h2>?? Photo Gallery</h2>");
            html.AppendLine($"                <p class=\"hero-dates\">{_settings.GalleryImages.Count} Photo{(_settings.GalleryImages.Count == 1 ? "" : "s")}</p>");
            html.AppendLine("            </div>");
            
            if (_settings.GalleryImages.Count > 0)
            {
                html.AppendLine("            <div class=\"gallery-grid\">");
                
                foreach (var image in _settings.GalleryImages.OrderByDescending(i => i.DateAdded))
                {
                    var mimeType = imageOptimizer.GetMimeType(image.FileName);
                    var dataUrl = imageOptimizer.ToDataUrl(image.ImageData, mimeType);
                    
                    html.AppendLine("                <div class=\"gallery-item\">");
                    html.AppendLine($"                    <img src=\"{dataUrl}\" alt=\"{image.Caption}\" loading=\"lazy\">");
                    html.AppendLine("                    <div style=\"padding: 15px;\">");
                    if (!string.IsNullOrWhiteSpace(image.Category))
                    {
                        html.AppendLine($"                        <span class=\"category\">{image.Category}</span>");
                    }
                    if (!string.IsNullOrWhiteSpace(image.Caption))
                    {
                        html.AppendLine($"                        <div class=\"caption\">{image.Caption}</div>");
                    }
                    html.AppendLine($"                        <div style=\"font-size: 0.75rem; color: var(--text-secondary); margin-top: 8px;\">{image.DateAdded:MMM d, yyyy}</div>");
                    html.AppendLine("                    </div>");
                    html.AppendLine("                </div>");
                }
                
                html.AppendLine("            </div>");
            }
            else
            {
                html.AppendLine("            <div class=\"section\">");
                html.AppendLine("                <p>No images available in the gallery.</p>");
                html.AppendLine("            </div>");
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </main>");
            
            AppendFooter(html);
            
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }
        
        private void AppendHeader(StringBuilder html, Season season)
        {
            html.AppendLine("    <header>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine($"            <h1>{_settings.LeagueName}</h1>");
            html.AppendLine($"            <p class=\"subtitle\">{_settings.LeagueSubtitle}</p>");
            html.AppendLine($"            <p class=\"season-badge\">{season.Name}</p>");
            html.AppendLine("        </div>");
            html.AppendLine("    </header>");
        }
        
        private void AppendNavigation(StringBuilder html, string activePage)
        {
            html.AppendLine("    <nav>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <ul>");
            html.AppendLine($"                <li><a href=\"index.html\"{(activePage == "Home" ? " class=\"active\"" : "")}>Home</a></li>");
            if (_settings.ShowStandings)
                html.AppendLine($"                <li><a href=\"standings.html\"{(activePage == "Standings" ? " class=\"active\"" : "")}>Standings</a></li>");
            if (_settings.ShowFixtures)
                html.AppendLine($"                <li><a href=\"fixtures.html\"{(activePage == "Fixtures" ? " class=\"active\"" : "")}>Fixtures</a></li>");
            if (_settings.ShowResults)
                html.AppendLine($"                <li><a href=\"results.html\"{(activePage == "Results" ? " class=\"active\"" : "")}>Results</a></li>");
            if (_settings.ShowPlayerStats)
                html.AppendLine($"                <li><a href=\"players.html\"{(activePage == "Players" ? " class=\"active\"" : "")}>Players</a></li>");
            if (_settings.ShowDivisions)
                html.AppendLine($"                <li><a href=\"divisions.html\"{(activePage == "Divisions" ? " class=\"active\"" : "")}>Divisions</a></li>");
            if (_settings.ShowGallery)
                html.AppendLine($"                <li><a href=\"gallery.html\"{(activePage == "Gallery" ? " class=\"active\"" : "")}>Gallery</a></li>");
            html.AppendLine("            </ul>");
            html.AppendLine("        </div>");
            html.AppendLine("    </nav>");
        }
        
        private void AppendFooter(StringBuilder html)
        {
            html.AppendLine("    <footer>");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine($"            <p>&copy; {DateTime.Now.Year} {_settings.LeagueName}. Generated {DateTime.Now:dd/MM/yyyy HH:mm}</p>");
            html.AppendLine("        </div>");
            html.AppendLine("    </footer>");
        }
        
        private sealed class TeamStanding
        {
            public string TeamName { get; set; } = "";
            public int Played { get; set; }
            public int Won { get; set; }
            public int Drawn { get; set; }
            public int Lost { get; set; }
            public int FramesFor { get; set; }
            public int FramesAgainst { get; set; }
            public int FramesDiff => FramesFor - FramesAgainst;
            public int Points { get; set; }
        }
        
        private List<TeamStanding> CalculateStandings(List<Team> teams, List<Fixture> fixtures)
        {
            var standings = new List<TeamStanding>();
            var settings = _league.Settings;
            
            foreach (var team in teams)
            {
                var standing = new TeamStanding { TeamName = team.Name ?? "Unknown" };
                
                foreach (var fixture in fixtures.Where(f => f.HomeTeamId == team.Id || f.AwayTeamId == team.Id))
                {
                    if (!fixture.Frames.Any(fr => fr.Winner != FrameWinner.None)) continue;
                    
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
                    }
                    else if (teamScore == oppScore)
                    {
                        standing.Drawn++;
                        standing.Points += teamScore + settings.MatchDrawBonus;
                    }
                    else
                    {
                        standing.Lost++;
                        standing.Points += teamScore;
                    }
                }
                
                standings.Add(standing);
            }
            
            return standings;
        }
        
        private sealed class PlayerStat
        {
            public string PlayerName { get; set; } = "";
            public string TeamName { get; set; } = "";
            public int Played { get; set; }
            public int Won { get; set; }
            public int Lost { get; set; }
            public double WinPercentage => Played > 0 ? (Won * 100.0 / Played) : 0;
            public int EightBalls { get; set; }
        }
        
        private List<PlayerStat> CalculatePlayerStats(List<Player> players, List<Team> teams, List<Fixture> fixtures)
        {
            var stats = new List<PlayerStat>();
            
            foreach (var player in players)
            {
                var team = teams.FirstOrDefault(t => t.Id == player.TeamId);
                var stat = new PlayerStat 
                { 
                    PlayerName = player.FullName,
                    TeamName = team?.Name ?? "No Team"
                };
                
                foreach (var fixture in fixtures)
                {
                    foreach (var frame in fixture.Frames)
                    {
                        if (frame.HomePlayerId == player.Id || frame.AwayPlayerId == player.Id)
                        {
                            stat.Played++;
                            
                            bool isHome = frame.HomePlayerId == player.Id;
                            bool wonFrame = (isHome && frame.Winner == FrameWinner.Home) || 
                                          (!isHome && frame.Winner == FrameWinner.Away);
                            
                            if (wonFrame)
                            {
                                stat.Won++;
                                if (frame.EightBall) stat.EightBalls++;
                            }
                            else if (frame.Winner != FrameWinner.None)
                            {
                                stat.Lost++;
                            }
                        }
                    }
                }
                
                if (stat.Played > 0)
                {
                    stats.Add(stat);
                }
            }
            
            return stats;
        }
        
        private string GenerateModernCSS()
        {
            return $@"/* {_settings.LeagueName} - Modern Template */
:root {{
    --primary-color: {_settings.PrimaryColor};
    --secondary-color: {_settings.SecondaryColor};
    --accent-color: {_settings.AccentColor};
    --bg-color: #F9FAFB;
    --card-bg: #FFFFFF;
    --text-color: #1F2937;
    --text-secondary: #6B7280;
    --border-color: #E5E7EB;
}}

* {{
    margin: 0;
    padding: 0;
    box-sizing: border-box;
}}

body {{
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    line-height: 1.6;
    color: var(--text-color);
    background: var(--bg-color);
}}

.container {{
    max-width: 1200px;
    margin: 0 auto;
    padding: 0 20px;
}}

header {{
    background: linear-gradient(135deg, var(--primary-color) 0%, var(--secondary-color) 100%);
    color: white;
    padding: 40px 0;
    text-align: center;
}}

header h1 {{
    font-size: 2.5rem;
    font-weight: 700;
    margin-bottom: 8px;
}}

header .subtitle {{
    font-size: 1.1rem;
    opacity: 0.9;
}}

.season-badge {{
    display: inline-block;
    margin-top: 16px;
    padding: 8px 20px;
    background: rgba(255,255,255,0.2);
    border-radius: 20px;
    font-weight: 600;
}}

nav {{
    background: var(--card-bg);
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    position: sticky;
    top: 0;
    z-index: 100;
}}

nav ul {{
    list-style: none;
    display: flex;
    justify-content: center;
    flex-wrap: wrap;
}}

nav li {{
    margin: 0;
}}

nav a {{
    display: block;
    padding: 16px 24px;
    color: var(--text-color);
    text-decoration: none;
    font-weight: 500;
    transition: all 0.3s ease;
}}

nav a:hover,
nav a.active {{
    color: var(--primary-color);
    background: var(--bg-color);
}}

main {{
    padding: 40px 0;
    min-height: calc(100vh - 300px);
}}

.hero {{
    text-align: center;
    margin-bottom: 40px;
}}

.hero h2 {{
    font-size: 2rem;
    color: var(--primary-color);
    margin-bottom: 8px;
}}

.hero-dates {{
    color: var(--text-secondary);
    font-size: 1.1rem;
}}

.stats-grid {{
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
    gap: 20px;
    margin-bottom: 40px;
}}

.stat-card {{
    background: var(--card-bg);
    padding: 30px;
    border-radius: 12px;
    text-align: center;
    box-shadow: 0 4px 6px rgba(0,0,0,0.05);
    transition: transform 0.3s ease;
}}

.stat-card:hover {{
    transform: translateY(-4px);
    box-shadow: 0 8px 12px rgba(0,0,0,0.1);
}}

.stat-number {{
    font-size: 3rem;
    font-weight: 700;
    color: var(--primary-color);
    margin-bottom: 8px;
}}

.stat-label {{
    color: var(--text-secondary);
    font-size: 0.9rem;
    text-transform: uppercase;
    letter-spacing: 1px;
}}

.section {{
    background: var(--card-bg);
    padding: 30px;
    border-radius: 12px;
    margin-bottom: 30px;
    box-shadow: 0 2px 4px rgba(0,0,0,0.05);
}}

.section h3 {{
    color: var(--primary-color);
    margin-bottom: 20px;
    font-size: 1.5rem;
}}

.results-list,
.fixtures-list {{
    display: flex;
    flex-direction: column;
    gap: 12px;
}}

.result-item,
.fixture-item {{
    display: grid;
    grid-template-columns: 120px 1fr auto 1fr;
    gap: 16px;
    padding: 16px;
    background: var(--bg-color);
    border-radius: 8px;
    align-items: center;
}}

.fixture-item {{
    grid-template-columns: 150px 1fr auto 1fr;
}}

.date {{
    color: var(--text-secondary);
    font-size: 0.9rem;
    font-weight: 500;
}}

.team {{
    font-weight: 600;
}}

.score {{
    font-size: 1.2rem;
    font-weight: 700;
    color: var(--primary-color);
    text-align: center;
}}

.vs {{
    color: var(--text-secondary);
    text-align: center;
    font-size: 0.9rem;
}}

.view-all {{
    text-align: center;
    margin-top: 20px;
}}

.view-all a {{
    color: var(--primary-color);
    text-decoration: none;
    font-weight: 600;
    transition: color 0.3s ease;
}}

.view-all a:hover {{
    color: var(--secondary-color);
}}

table {{
    width: 100%;
    border-collapse: collapse;
    background: var(--card-bg);
    border-radius: 8px;
    overflow: hidden;
}}

thead {{
    background: var(--primary-color);
    color: white;
}}

th, td {{
    padding: 12px 16px;
    text-align: left;
}}

th {{
    font-weight: 600;
    text-transform: uppercase;
    font-size: 0.85rem;
    letter-spacing: 0.5px;
}}

tbody tr {{
    border-bottom: 1px solid var(--border-color);
}}

tbody tr:last-child {{
    border-bottom: none;
}}

tbody tr:hover {{
    background: var(--bg-color);
}}

.winner {{
    color: var(--primary-color);
    font-weight: 700;
}}

.division-badge {{
    display: inline-block;
    padding: 4px 12px;
    background: var(--accent-color);
    color: white;
    border-radius: 12px;
    font-size: 0.75rem;
    font-weight: 600;
    text-transform: uppercase;
}}

.venue {{
    color: var(--text-secondary);
    font-size: 0.85rem;
}}

.division-notes {{
    color: var(--text-secondary);
    font-style: italic;
    margin-bottom: 20px;
}}

.team-list {{
    list-style: none;
    padding: 0;
}}

.team-list li {{
    padding: 12px;
    background: var(--bg-color);
    border-radius: 6px;
    margin-bottom: 8px;
    transition: background 0.3s ease;
}}

.team-list li:hover {{
    background: var(--border-color);
}}

footer {{
    background: var(--text-color);
    color: white;
    padding: 30px 0;
    text-align: center;
    margin-top: 60px;
}}

footer p {{
    opacity: 0.8;
    font-size: 0.9rem;
}}

@media (max-width: 768px) {{
    header h1 {{
        font-size: 1.8rem;
    }}
    
    .hero h2 {{
        font-size: 1.5rem;
    }}
    
    .stats-grid {{
        grid-template-columns: repeat(2, 1fr);
    }}
    
    .result-item,
    .fixture-item {{
        grid-template-columns: 1fr;
        gap: 8px;
        text-align: center;
    }}
    
    nav ul {{
        flex-direction: column;
    }}
    
    nav a {{
        border-bottom: 1px solid var(--border-color);
    }}
    
    table {{
        font-size: 0.85rem;
    }}
    
    th, td {{
        padding: 8px;
    }}
}}";
        }
        
        private string GenerateDarkModeCSS()
        {
            return GenerateModernCSS().Replace("#F9FAFB", "#111827")
                .Replace("#FFFFFF", "#1F2937")
                .Replace("#1F2937", "#F9FAFB")
                .Replace("#6B7280", "#9CA3AF")
                .Replace("#E5E7EB", "#374151");
        }
        
        private string GenerateSportCSS()
        {
            return GenerateModernCSS().Replace("'Segoe UI', Roboto", "'Arial Black', Arial")
                .Replace("font-weight: 500", "font-weight: 900")
                .Replace("font-weight: 600", "font-weight: 900")
                .Replace("font-weight: 700", "font-weight: 900");
        }
        
        private string GenerateMinimalistCSS()
        {
            return GenerateModernCSS().Replace("#F9FAFB", "#FFFFFF")
                .Replace("font-weight: 700", "font-weight: 300")
                .Replace("font-weight: 600", "font-weight: 400")
                .Replace("font-weight: 500", "font-weight: 300");
        }
    }
}
