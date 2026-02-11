using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Generates template HTML pages that load data dynamically via JavaScript.
/// These pages use JSON data files instead of generating individual HTML files per entity.
/// </summary>
public sealed class WebsiteTemplatePageGenerator
{
    private readonly WebsiteSettings _settings;

    public WebsiteTemplatePageGenerator(WebsiteSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Generate the player template page (player.html) that loads from players-data.json
    /// </summary>
    public string GeneratePlayerTemplatePage(
        Season season,
        Action<StringBuilder, string, Season> appendDocumentHead,
        Action<StringBuilder, Season> appendHeader,
        Action<StringBuilder, string> appendNavigation,
        Action<StringBuilder> appendFooter,
        string tableClasses)
    {
        var html = new StringBuilder();

        appendDocumentHead(html, $"Player - {_settings.LeagueName}", season);
        html.AppendLine("<body>");

        if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
            html.AppendLine(_settings.CustomBodyStartHtml);

        appendHeader(html, season);
        appendNavigation(html, "Players");

        html.AppendLine("    <div class=\"content-area\">");
        html.AppendLine("        <div class=\"container\">");

        // Loading state
        html.AppendLine("            <div id=\"loading\" class=\"section\" style=\"text-align: center;\">");
        html.AppendLine("                <p>Loading player data...</p>");
        html.AppendLine("            </div>");

        // Error state
        html.AppendLine("            <div id=\"error\" class=\"section\" style=\"display: none; text-align: center;\">");
        html.AppendLine("                <h2>Player Not Found</h2>");
        html.AppendLine("                <p>The requested player could not be found.</p>");
        html.AppendLine("                <a href=\"players.html\" class=\"back-link\">? Back to All Players</a>");
        html.AppendLine("            </div>");

        // Player content (populated by JS)
        html.AppendLine("            <div id=\"player-content\" style=\"display: none;\">");
        html.AppendLine("                <div class=\"hero\">");
        html.AppendLine("                    <h2>?? <span id=\"player-name\"></span></h2>");
        html.AppendLine("                    <p class=\"hero-dates\" id=\"player-team\"></p>");
        html.AppendLine("                </div>");
        html.AppendLine("                <div class=\"stats-grid\" id=\"stats-grid\" style=\"grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));\"></div>");
        html.AppendLine("                <div class=\"section\">");
        html.AppendLine("                    <h3>?? Full Record</h3>");
        html.AppendLine("                    <div class=\"table-responsive\">");
        html.AppendLine($"                    <table class=\"{tableClasses}\">");
        html.AppendLine("                        <thead><tr><th>Date</th><th>Opponent</th><th>Team</th><th>Result</th></tr></thead>");
        html.AppendLine("                        <tbody id=\"match-history\"></tbody>");
        html.AppendLine("                    </table>");
        html.AppendLine("                    </div>");
        html.AppendLine("                </div>");
        html.AppendLine("                <div class=\"section\" style=\"text-align: center;\">");
        html.AppendLine("                    <a href=\"players.html\" class=\"back-link\">? Back to All Players</a>");
        html.AppendLine("                </div>");
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        appendFooter(html);

        // JavaScript to load player data
        var leagueName = _settings.LeagueName.Replace("'", "\\'");
        html.AppendLine(@"    <script>
(function() {
    var urlParams = new URLSearchParams(window.location.search);
    var playerId = urlParams.get('id');
    if (!playerId) { showError(); return; }
    fetch('players-data.json')
        .then(function(r) { return r.json(); })
        .then(function(data) {
            var player = data.players.find(function(p) { return p.id === playerId; });
            if (player) displayPlayer(player); else showError();
        })
        .catch(function() { showError(); });
    function showError() {
        document.getElementById('loading').style.display = 'none';
        document.getElementById('error').style.display = 'block';
    }
    function displayPlayer(p) {
        document.getElementById('loading').style.display = 'none';
        document.getElementById('player-content').style.display = 'block';
        document.title = p.name + ' - " + leagueName + @"';
        document.getElementById('player-name').textContent = p.name;
        document.getElementById('player-team').textContent = p.team || 'No Team';
        var statsHtml = '<div class=""stat-card""><div class=""stat-number"">' + p.played + '</div><div class=""stat-label"">Played</div></div>';
        statsHtml += '<div class=""stat-card""><div class=""stat-number"">' + p.won + '</div><div class=""stat-label"">Won</div></div>';
        statsHtml += '<div class=""stat-card""><div class=""stat-number"">' + p.lost + '</div><div class=""stat-label"">Lost</div></div>';
        statsHtml += '<div class=""stat-card""><div class=""stat-number"">' + p.winPct + '%</div><div class=""stat-label"">Win %</div></div>';
        if (p.eightBalls > 0) statsHtml += '<div class=""stat-card""><div class=""stat-number"">' + p.eightBalls + '</div><div class=""stat-label"">8-Balls</div></div>';
        statsHtml += '<div class=""stat-card""><div class=""stat-number"">' + p.rating + '</div><div class=""stat-label"">Rating</div></div>';
        document.getElementById('stats-grid').innerHTML = statsHtml;
        var historyHtml = '';
        if (p.history && p.history.length > 0) {
            for (var i = 0; i < p.history.length; i++) {
                var m = p.history[i];
                var resultClass = m.won ? 'text-positive' : 'text-negative';
                var resultText = m.won ? 'Won' : 'Lost';
                if (m.eightBall) resultText += ' (8-ball)';
                var oppLink = m.opponentId ? '<a href=""player.html?id=' + m.opponentId + '"" class=""player-link"">' + m.opponentName + '</a>' : m.opponentName;
                historyHtml += '<tr><td>' + m.dateDisplay + '</td><td>' + oppLink + '</td><td>' + m.opponentTeam + '</td><td class=""' + resultClass + '""><strong>' + resultText + '</strong></td></tr>';
            }
        } else {
            historyHtml = '<tr><td colspan=""4"">No match history</td></tr>';
        }
        document.getElementById('match-history').innerHTML = historyHtml;
    }
})();
    </script>");

        if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
            html.AppendLine(_settings.CustomBodyEndHtml);

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }

    /// <summary>
    /// Generate the team template page (team.html) that loads from teams-data.json
    /// </summary>
    public string GenerateTeamTemplatePage(
        Season season,
        Action<StringBuilder, string, Season> appendDocumentHead,
        Action<StringBuilder, Season> appendHeader,
        Action<StringBuilder, string> appendNavigation,
        Action<StringBuilder> appendFooter,
        string tableClasses)
    {
        var html = new StringBuilder();

        appendDocumentHead(html, $"Team - {_settings.LeagueName}", season);
        html.AppendLine("<body>");

        if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
            html.AppendLine(_settings.CustomBodyStartHtml);

        appendHeader(html, season);
        appendNavigation(html, "Divisions");

        html.AppendLine("    <div class=\"content-area\">");
        html.AppendLine("        <div class=\"container\">");

        // Loading state
        html.AppendLine("            <div id=\"loading\" class=\"section\" style=\"text-align: center;\">");
        html.AppendLine("                <p>Loading team data...</p>");
        html.AppendLine("            </div>");

        // Error state
        html.AppendLine("            <div id=\"error\" class=\"section\" style=\"display: none; text-align: center;\">");
        html.AppendLine("                <h2>Team Not Found</h2>");
        html.AppendLine("                <p>The requested team could not be found.</p>");
        html.AppendLine("                <a href=\"divisions.html\" class=\"back-link\">? Back to Divisions</a>");
        html.AppendLine("            </div>");

        // Team content (populated by JS)
        html.AppendLine("            <div id=\"team-content\" style=\"display: none;\">");
        html.AppendLine("                <div class=\"hero\">");
        html.AppendLine("                    <h2>?? <span id=\"team-name\"></span></h2>");
        html.AppendLine("                    <p class=\"hero-dates\" id=\"team-division\"></p>");
        html.AppendLine("                </div>");
        html.AppendLine("                <div class=\"stats-grid\" id=\"stats-grid\" style=\"grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));\"></div>");

        // Team info section
        html.AppendLine("                <div class=\"section\" id=\"team-info\"></div>");

        // Roster section
        html.AppendLine("                <div class=\"section\">");
        html.AppendLine("                    <h3>?? Team Roster</h3>");
        html.AppendLine("                    <ul class=\"team-list\" id=\"roster-list\"></ul>");
        html.AppendLine("                </div>");

        // Match history
        html.AppendLine("                <div class=\"section\">");
        html.AppendLine("                    <h3>?? Recent Matches</h3>");
        html.AppendLine("                    <div class=\"table-responsive\">");
        html.AppendLine($"                    <table class=\"{tableClasses}\">");
        html.AppendLine("                        <thead><tr><th>Date</th><th>Opponent</th><th>H/A</th><th>Result</th><th>Score</th></tr></thead>");
        html.AppendLine("                        <tbody id=\"match-history\"></tbody>");
        html.AppendLine("                    </table>");
        html.AppendLine("                    </div>");
        html.AppendLine("                </div>");

        // Back link
        html.AppendLine("                <div class=\"section\" style=\"text-align: center;\">");
        html.AppendLine("                    <a href=\"divisions.html\" class=\"back-link\">? Back to Divisions</a>");
        html.AppendLine("                </div>");
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        appendFooter(html);

        // JavaScript to load team data
        var leagueName = _settings.LeagueName.Replace("'", "\\'");
        html.AppendLine(@"    <script>
(function() {
    var urlParams = new URLSearchParams(window.location.search);
    var teamId = urlParams.get('id');
    if (!teamId) { showError(); return; }
    fetch('teams-data.json')
        .then(function(r) { return r.json(); })
        .then(function(data) {
            var team = data.teams.find(function(t) { return t.id === teamId; });
            if (team) displayTeam(team); else showError();
        })
        .catch(function() { showError(); });
    function showError() {
        document.getElementById('loading').style.display = 'none';
        document.getElementById('error').style.display = 'block';
    }
    function displayTeam(t) {
        document.getElementById('loading').style.display = 'none';
        document.getElementById('team-content').style.display = 'block';
        document.title = t.name + ' - " + leagueName + @"';
        document.getElementById('team-name').textContent = t.name;
        document.getElementById('team-division').textContent = t.division || 'No Division';
        
        // Stats grid
        var statsHtml = '<div class=""stat-card""><div class=""stat-number"">' + t.played + '</div><div class=""stat-label"">Played</div></div>';
        statsHtml += '<div class=""stat-card""><div class=""stat-number"">' + t.won + '</div><div class=""stat-label"">Won</div></div>';
        if (t.drawn > 0) statsHtml += '<div class=""stat-card""><div class=""stat-number"">' + t.drawn + '</div><div class=""stat-label"">Drawn</div></div>';
        statsHtml += '<div class=""stat-card""><div class=""stat-number"">' + t.lost + '</div><div class=""stat-label"">Lost</div></div>';
        statsHtml += '<div class=""stat-card""><div class=""stat-number"">' + t.framesFor + '-' + t.framesAgainst + '</div><div class=""stat-label"">Frames</div></div>';
        statsHtml += '<div class=""stat-card""><div class=""stat-number"">' + t.points + '</div><div class=""stat-label"">Points</div></div>';
        document.getElementById('stats-grid').innerHTML = statsHtml;
        
        // Team info
        var infoHtml = '<h3>?? Team Info</h3>';
        if (t.venue) infoHtml += '<p><strong>Venue:</strong> ' + t.venue + '</p>';
        if (t.providesFood) infoHtml += '<p><strong>?? Food Available</strong></p>';
        if (t.form && t.form.length > 0) {
            infoHtml += '<p><strong>Form:</strong> ';
            for (var i = 0; i < t.form.length; i++) {
                var f = t.form[i];
                if (f === 'W') infoHtml += '<span style=""color:#10B981"">?</span> ';
                else if (f === 'L') infoHtml += '<span style=""color:#EF4444"">?</span> ';
                else infoHtml += '<span style=""color:#F59E0B"">?</span> ';
            }
            infoHtml += '</p>';
        }
        document.getElementById('team-info').innerHTML = infoHtml;
        
        // Roster
        var rosterHtml = '';
        if (t.roster && t.roster.length > 0) {
            for (var i = 0; i < t.roster.length; i++) {
                var p = t.roster[i];
                rosterHtml += '<li><a href=""player.html?id=' + p.id + '"" class=""player-link"">' + p.name + '</a></li>';
            }
        } else {
            rosterHtml = '<li>No players assigned</li>';
        }
        document.getElementById('roster-list').innerHTML = rosterHtml;
        
        // Match history
        var historyHtml = '';
        if (t.history && t.history.length > 0) {
            for (var i = 0; i < t.history.length; i++) {
                var m = t.history[i];
                var resultClass = m.result === 'W' ? 'text-positive' : (m.result === 'L' ? 'text-negative' : '');
                var resultText = m.result === 'W' ? 'Won' : (m.result === 'L' ? 'Lost' : 'Draw');
                var oppLink = '<a href=""team.html?id=' + m.opponentId + '"" class=""player-link"">' + m.opponentName + '</a>';
                var homeAway = m.isHome ? 'H' : 'A';
                historyHtml += '<tr><td>' + m.dateDisplay + '</td><td>' + oppLink + '</td><td>' + homeAway + '</td><td class=""' + resultClass + '""><strong>' + resultText + '</strong></td><td>' + m.teamScore + '-' + m.oppScore + '</td></tr>';
            }
        } else {
            historyHtml = '<tr><td colspan=""5"">No match history</td></tr>';
        }
        document.getElementById('match-history').innerHTML = historyHtml;
    }
})();
    </script>");

        if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
            html.AppendLine(_settings.CustomBodyEndHtml);

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }
}
