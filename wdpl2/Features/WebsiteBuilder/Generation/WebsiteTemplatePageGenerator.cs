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
        string cacheBuster,
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
        html.AppendLine("                <a href=\"players.html\" class=\"back-link\">&#8592; Back to All Players</a>");
        html.AppendLine("            </div>");

        // Player content
        html.AppendLine("            <div id=\"player-content\" style=\"display: none;\">");
        html.AppendLine("                <div class=\"section\">");
        html.AppendLine("                    <h2>&#128100; <span id=\"player-name\"></span></h2>");
        html.AppendLine("                    <p class=\"hero-dates\" id=\"player-team\"></p>");
        html.AppendLine("                </div>");
        html.AppendLine("                <div class=\"stats-grid\" id=\"stats-grid\" style=\"grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));\"></div>");
        html.AppendLine("                <div class=\"section\">");
        html.AppendLine("                    <h3>&#128203; Full Record</h3>");
        html.AppendLine("                    <div class=\"table-responsive\">");
        html.AppendLine($"                    <table class=\"{tableClasses}\">");
        html.AppendLine("                        <thead><tr><th>Date</th><th>Opponent</th><th>Team</th><th>Result</th></tr></thead>");
        html.AppendLine("                        <tbody id=\"match-history\"></tbody>");
        html.AppendLine("                    </table>");
        html.AppendLine("                    </div>");
        html.AppendLine("                </div>");
        html.AppendLine("                <div class=\"section\" style=\"text-align: center;\">");
        html.AppendLine("                    <a href=\"players.html\" class=\"back-link\">&#8592; Back to All Players</a>");
        html.AppendLine("                </div>");
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        appendFooter(html);

        // JavaScript to load player data
        var leagueName = _settings.LeagueName.Replace("'", "\\'");
        html.AppendLine(@"    <script>
(function() {
    var cacheBuster = '" + cacheBuster + @"';
    var urlParams = new URLSearchParams(window.location.search);
    var playerId = urlParams.get('id');
    if (!playerId) { showError(); return; }
    fetch('players-data.json?v=' + cacheBuster)
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
        string cacheBuster,
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
        html.AppendLine("                <a href=\"divisions.html\" class=\"back-link\">&#8592; Back to Divisions</a>");
        html.AppendLine("            </div>");

        // Team content (populated by JS)
        html.AppendLine("            <div id=\"team-content\" style=\"display: none;\">");
        html.AppendLine("                <div class=\"hero\">");
        html.AppendLine("                    <h2>&#127942; <span id=\"team-name\"></span></h2>");
        html.AppendLine("                    <p class=\"hero-dates\" id=\"team-division\"></p>");
        html.AppendLine("                </div>");
        html.AppendLine("                <div class=\"stats-grid\" id=\"stats-grid\" style=\"grid-template-columns: repeat(auto-fit, minmax(120px, 1fr));\"></div>");

        // Team info section
        html.AppendLine("                <div class=\"section\" id=\"team-info\"></div>");

        // Roster section - table with player stats
        html.AppendLine("                <div class=\"section\">");
        html.AppendLine("                    <h3>&#128101; Team Roster</h3>");
        html.AppendLine("                    <div class=\"table-responsive\">");
        html.AppendLine($"                    <table class=\"{tableClasses}\">");
        html.AppendLine("                        <thead><tr><th>Pos</th><th>Player</th><th>P</th><th>W</th><th>L</th><th>Win %</th><th>Rating</th></tr></thead>");
        html.AppendLine("                        <tbody id=\"roster-list\"></tbody>");
        html.AppendLine("                    </table>");
        html.AppendLine("                    </div>");
        html.AppendLine("                </div>");

        // Match history
        html.AppendLine("                <div class=\"section\">");
        html.AppendLine("                    <h3>&#128197; Recent Matches</h3>");
        html.AppendLine("                    <div class=\"table-responsive\">");
        html.AppendLine($"                    <table class=\"{tableClasses}\">");
        html.AppendLine("                        <thead><tr><th>Date</th><th>Opponent</th><th>H/A</th><th>Result</th><th>Score</th></tr></thead>");
        html.AppendLine("                        <tbody id=\"match-history\"></tbody>");
        html.AppendLine("                    </table>");
        html.AppendLine("                    </div>");
        html.AppendLine("                </div>");

        // Back link
        html.AppendLine("                <div class=\"section\" style=\"text-align: center;\">");
        html.AppendLine("                    <a href=\"divisions.html\" class=\"back-link\">&#8592; Back to Divisions</a>");
        html.AppendLine("                </div>");
        html.AppendLine("            </div>");
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        appendFooter(html);

        // JavaScript to load team data
        var leagueName = _settings.LeagueName.Replace("'", "\\'");
        html.AppendLine(@"    <script>
(function() {
    var cacheBuster = '" + cacheBuster + @"';
    var urlParams = new URLSearchParams(window.location.search);
    var teamId = urlParams.get('id');
    if (!teamId) { showError(); return; }
    fetch('teams-data.json?v=' + cacheBuster)
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
        var infoHtml = '<h3>&#128712; Team Info</h3>';
        if (t.venue) infoHtml += '<p><strong>Venue:</strong> ' + t.venue + '</p>';
        if (t.providesFood) infoHtml += '<p><strong>&#127860; Food Available</strong></p>';
        if (t.form && t.form.length > 0) {
            infoHtml += '<p><strong>Form:</strong> ';
            for (var i = 0; i < t.form.length; i++) {
                var f = t.form[i];
                if (f === 'W') infoHtml += '<span style=""color:#10B981"">&#9679;</span> ';
                else if (f === 'L') infoHtml += '<span style=""color:#EF4444"">&#9679;</span> ';
                else infoHtml += '<span style=""color:#F59E0B"">&#9679;</span> ';
            }
            infoHtml += '</p>';
        }
        document.getElementById('team-info').innerHTML = infoHtml;
        
        // Roster table
        var rosterHtml = '';
        if (t.roster && t.roster.length > 0) {
            var sorted = t.roster.slice().sort(function(a,b){ return b.rating - a.rating; });
            var pos = 1;
            for (var i = 0; i < sorted.length; i++) {
                var p = sorted[i];
                if (i === 0 || sorted[i-1].rating !== p.rating) pos = i + 1;
                var isJoint = (i > 0 && sorted[i-1].rating === p.rating) || (i < sorted.length-1 && sorted[i+1].rating === p.rating);
                var posDisplay = isJoint ? pos + '=' : '' + pos;
                rosterHtml += '<tr><td>' + posDisplay + '</td><td><strong><a href=""player.html?id=' + p.id + '"" class=""player-link"">' + p.name + '</a></strong></td><td>' + p.played + '</td><td>' + p.won + '</td><td>' + p.lost + '</td><td>' + p.winPct.toFixed(1) + '%</td><td>' + p.rating + '</td></tr>';
            }
        } else {
            rosterHtml = '<tr><td colspan=""7"">No players assigned</td></tr>';
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

    /// <summary>
    /// Captains Area login page (captains.html). Captain picks their team and enters a PIN.
    /// PIN is hashed client-side (SHA-256) and compared against captains-data.json.
    /// On success, team id is stored in localStorage and the user is redirected to captain-dashboard.html.
    /// </summary>
    public string GenerateCaptainsLoginPage(
        Season season,
        string cacheBuster,
        Action<StringBuilder, string, Season> appendDocumentHead,
        Action<StringBuilder, Season> appendHeader,
        Action<StringBuilder, string> appendNavigation,
        Action<StringBuilder> appendFooter)
    {
        var html = new StringBuilder();

        appendDocumentHead(html, $"{_settings.CaptainsAreaPageTitle} - {_settings.LeagueName}", season);
        html.AppendLine("<body>");

        if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
            html.AppendLine(_settings.CustomBodyStartHtml);

        appendHeader(html, season);
        appendNavigation(html, "Captains");

        html.AppendLine("    <div class=\"content-area\">");
        html.AppendLine("        <div class=\"container\">");

        html.AppendLine("            <div class=\"section captain-login-card\">");
        html.AppendLine($"                <h2>&#128274; {EscapeHtml(_settings.CaptainsAreaPageTitle)}</h2>");
        if (!string.IsNullOrWhiteSpace(_settings.CaptainsWelcomeMessage))
            html.AppendLine($"                <p class=\"captain-welcome\">{EscapeHtml(_settings.CaptainsWelcomeMessage)}</p>");

        html.AppendLine("                <form id=\"captain-login-form\" autocomplete=\"off\" onsubmit=\"return false;\">");
        html.AppendLine("                    <label for=\"captain-team\"><strong>Team</strong></label>");
        html.AppendLine("                    <select id=\"captain-team\" required></select>");
        html.AppendLine("                    <label for=\"captain-pin\" style=\"margin-top:12px;\"><strong>PIN</strong></label>");
        html.AppendLine("                    <input id=\"captain-pin\" type=\"password\" inputmode=\"numeric\" autocomplete=\"current-password\" minlength=\"4\" maxlength=\"32\" required />");
        html.AppendLine("                    <button id=\"captain-login-btn\" type=\"submit\" class=\"captain-btn\">Sign in</button>");
        html.AppendLine("                    <p id=\"captain-login-error\" class=\"captain-error\" style=\"display:none;\"></p>");
        html.AppendLine("                </form>");
        html.AppendLine("                <p class=\"captain-hint\">Don't have a PIN? Ask the league admin to set one for your team.</p>");
        html.AppendLine("            </div>");

        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        appendFooter(html);

        html.AppendLine(@"    <script>
(function(){
    var cacheBuster = '" + cacheBuster + @"';
    var teamSel = document.getElementById('captain-team');
    var pinInput = document.getElementById('captain-pin');
    var errEl = document.getElementById('captain-login-error');
    var dataPromise = fetch('captains-data.json?v=' + cacheBuster).then(function(r){ return r.json(); });

    dataPromise.then(function(data){
        if (!data.teams || data.teams.length === 0) {
            teamSel.innerHTML = '<option>(no teams enabled)</option>';
            teamSel.disabled = true;
            return;
        }
        var opts = '<option value="""">Select your team...</option>';
        for (var i = 0; i < data.teams.length; i++) {
            var t = data.teams[i];
            opts += '<option value=""' + t.id + '"">' + escapeHtml(t.name) + (t.division ? ' (' + escapeHtml(t.division) + ')' : '') + '</option>';
        }
        teamSel.innerHTML = opts;
        // Pre-select last used team
        try {
            var last = localStorage.getItem('wdpl_captain_last_team');
            if (last) teamSel.value = last;
        } catch (e) {}
    }).catch(function(){
        teamSel.innerHTML = '<option>(failed to load)</option>';
        teamSel.disabled = true;
    });

    function escapeHtml(s){ return (s||'').replace(/[&<>""']/g, function(c){ return {'&':'&amp;','<':'&lt;','>':'&gt;','""':'&quot;','\'':'&#39;'}[c]; }); }

    async function sha256Hex(text){
        var enc = new TextEncoder().encode(text);
        var buf = await crypto.subtle.digest('SHA-256', enc);
        var arr = Array.from(new Uint8Array(buf));
        return arr.map(function(b){ return b.toString(16).padStart(2,'0'); }).join('');
    }

    document.getElementById('captain-login-form').addEventListener('submit', async function(){
        errEl.style.display = 'none';
        var teamId = teamSel.value;
        var pin = (pinInput.value || '').trim();
        if (!teamId) { showError('Please select your team.'); return; }
        if (!pin) { showError('Enter your PIN.'); return; }
        try {
            var data = await dataPromise;
            var team = data.teams.find(function(t){ return t.id === teamId; });
            if (!team) { showError('Team not found.'); return; }
            var hash = await sha256Hex(pin);
            if (hash !== team.pinHash) { showError('Incorrect PIN.'); return; }
            try {
                localStorage.setItem('wdpl_captain_team', teamId);
                localStorage.setItem('wdpl_captain_last_team', teamId);
                localStorage.setItem('wdpl_captain_login_at', String(Date.now()));
            } catch (e) {}
            window.location.href = 'captain-dashboard.html';
        } catch (e) {
            showError('Sign-in failed. Please try again.');
        }
    });

    function showError(msg){ errEl.textContent = msg; errEl.style.display = 'block'; }
})();
    </script>");

        if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
            html.AppendLine(_settings.CustomBodyEndHtml);

        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    /// <summary>
    /// Captain dashboard (captain-dashboard.html). Requires a logged-in team id in localStorage.
    /// Loads captains-data.json and shows fixtures, results, contact list, score submission and availability links.
    /// </summary>
    public string GenerateCaptainDashboardPage(
        Season season,
        string cacheBuster,
        Action<StringBuilder, string, Season> appendDocumentHead,
        Action<StringBuilder, Season> appendHeader,
        Action<StringBuilder, string> appendNavigation,
        Action<StringBuilder> appendFooter,
        string tableClasses)
    {
        var html = new StringBuilder();

        appendDocumentHead(html, $"Dashboard - {_settings.CaptainsAreaPageTitle}", season);
        html.AppendLine("<body>");

        if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
            html.AppendLine(_settings.CustomBodyStartHtml);

        appendHeader(html, season);
        appendNavigation(html, "Captains");

        html.AppendLine("    <div class=\"content-area\">");
        html.AppendLine("        <div class=\"container\">");

        html.AppendLine("            <div id=\"cap-loading\" class=\"section\" style=\"text-align:center;\"><p>Loading...</p></div>");

        html.AppendLine("            <div id=\"cap-content\" style=\"display:none;\">");
        html.AppendLine("                <div class=\"section captain-header\">");
        html.AppendLine("                    <div class=\"captain-header-row\">");
        html.AppendLine("                        <div>");
        html.AppendLine("                            <h2>&#127942; <span id=\"cap-team-name\"></span></h2>");
        html.AppendLine("                            <p class=\"hero-dates\"><span id=\"cap-team-division\"></span> &middot; Captain: <span id=\"cap-captain-name\"></span></p>");
        html.AppendLine("                        </div>");
        html.AppendLine("                        <button id=\"cap-logout\" class=\"captain-btn captain-btn-secondary\" type=\"button\">Sign out</button>");
        html.AppendLine("                    </div>");
        html.AppendLine("                </div>");

        // Build sub-tab nav (only enabled sections become tabs)
        html.AppendLine("                <nav class=\"captain-tabs\" role=\"tablist\">");
        html.AppendLine("                    <button type=\"button\" class=\"captain-tab-btn active\" data-tab=\"overview\" role=\"tab\" aria-selected=\"true\">&#9889; Overview</button>");
        if (_settings.CaptainsShowTeamRoster)
            html.AppendLine("                    <button type=\"button\" class=\"captain-tab-btn\" data-tab=\"team\" role=\"tab\" aria-selected=\"false\">&#128101; Team</button>");
        if (_settings.CaptainsShowFixtures)
            html.AppendLine("                    <button type=\"button\" class=\"captain-tab-btn\" data-tab=\"fixtures\" role=\"tab\" aria-selected=\"false\">&#128197; Fixtures</button>");
        if (_settings.CaptainsShowResults)
            html.AppendLine("                    <button type=\"button\" class=\"captain-tab-btn\" data-tab=\"results\" role=\"tab\" aria-selected=\"false\">&#128203; Results</button>");
        if (_settings.CaptainsShowContactList)
            html.AppendLine("                    <button type=\"button\" class=\"captain-tab-btn\" data-tab=\"contacts\" role=\"tab\" aria-selected=\"false\">&#128222; Contacts</button>");
        if (_settings.CaptainsAllowSelfUpdate)
            html.AppendLine("                    <button type=\"button\" class=\"captain-tab-btn\" data-tab=\"me\" role=\"tab\" aria-selected=\"false\">&#128100; My Details</button>");
        html.AppendLine("                </nav>");

        html.AppendLine("                <div class=\"captain-tab-panels\">");

        // Quick actions panel
        html.AppendLine("                    <div class=\"section captain-actions captain-tab-panel active\" data-tab=\"overview\" role=\"tabpanel\">");
        html.AppendLine("                        <h3>&#9889; Quick Actions</h3>");
        html.AppendLine("                        <div id=\"cap-actions\" class=\"captain-action-list\"></div>");
        html.AppendLine("                    </div>");

        // Team roster
        if (_settings.CaptainsShowTeamRoster)
        {
            html.AppendLine("                    <div class=\"section captain-tab-panel\" data-tab=\"team\" role=\"tabpanel\">");
            html.AppendLine("                        <h3>&#128101; Team Roster</h3>");
            html.AppendLine("                        <p class=\"captain-hint\">Players currently registered to your team. Contact your league administrator to add or remove players.</p>");
            html.AppendLine("                        <div class=\"table-responsive\">");
            html.AppendLine($"                        <table class=\"{tableClasses}\">");
            html.AppendLine("                            <thead><tr><th>#</th><th>Player</th><th>Status</th></tr></thead>");
            html.AppendLine("                            <tbody id=\"cap-roster\"></tbody>");
            html.AppendLine("                        </table>");
            html.AppendLine("                        </div>");
            html.AppendLine("                    </div>");
        }

        // Upcoming fixtures
        if (_settings.CaptainsShowFixtures)
        {
            html.AppendLine("                    <div class=\"section captain-tab-panel\" data-tab=\"fixtures\" role=\"tabpanel\">");
            html.AppendLine("                        <h3>&#128197; Upcoming Fixtures</h3>");
            html.AppendLine("                        <div class=\"table-responsive\">");
            html.AppendLine($"                        <table class=\"{tableClasses}\">");
            html.AppendLine("                            <thead><tr><th>Date</th><th>Opponent</th><th>H/A</th><th>Venue</th></tr></thead>");
            html.AppendLine("                            <tbody id=\"cap-fixtures\"></tbody>");
            html.AppendLine("                        </table>");
            html.AppendLine("                        </div>");
            html.AppendLine("                    </div>");
        }

        // Recent results
        if (_settings.CaptainsShowResults)
        {
            html.AppendLine("                    <div class=\"section captain-tab-panel\" data-tab=\"results\" role=\"tabpanel\">");
            html.AppendLine("                        <h3>&#128203; Recent Results</h3>");
            html.AppendLine("                        <div class=\"table-responsive\">");
            html.AppendLine($"                        <table class=\"{tableClasses}\">");
            html.AppendLine("                            <thead><tr><th>Date</th><th>Opponent</th><th>H/A</th><th>Score</th><th>Result</th></tr></thead>");
            html.AppendLine("                            <tbody id=\"cap-results\"></tbody>");
            html.AppendLine("                        </table>");
            html.AppendLine("                        </div>");
            html.AppendLine("                    </div>");
        }

        // Captain contacts
        if (_settings.CaptainsShowContactList)
        {
            html.AppendLine("                    <div class=\"section captain-tab-panel\" data-tab=\"contacts\" role=\"tabpanel\">");
            html.AppendLine("                        <h3>&#128222; Captain Contacts</h3>");
            html.AppendLine("                        <p class=\"captain-hint\">Other team captains' contact details. Please use responsibly.</p>");
            html.AppendLine("                        <div class=\"table-responsive\">");
            html.AppendLine($"                        <table class=\"{tableClasses}\">");
            html.AppendLine("                            <thead><tr><th>Team</th><th>Division</th><th>Captain</th><th>Email</th><th>Phone</th></tr></thead>");
            html.AppendLine("                            <tbody id=\"cap-contacts\"></tbody>");
            html.AppendLine("                        </table>");
            html.AppendLine("                        </div>");
            html.AppendLine("                    </div>");
        }

        // My Details (self-service update)
        if (_settings.CaptainsAllowSelfUpdate)
        {
            html.AppendLine("                    <div class=\"section captain-tab-panel\" data-tab=\"me\" role=\"tabpanel\">");
            html.AppendLine("                        <h3>&#128100; My Details</h3>");
            html.AppendLine("                        <p class=\"captain-hint\">Update your contact details. Changes are sent to the league administrator for review.</p>");
            html.AppendLine("                        <form id=\"cap-update-form\" class=\"captain-update-form\" autocomplete=\"off\">");
            html.AppendLine("                            <label>Captain name<br/><input type=\"text\" id=\"cap-up-name\" name=\"captainName\" maxlength=\"80\" required/></label>");
            html.AppendLine("                            <label>Email<br/><input type=\"email\" id=\"cap-up-email\" name=\"captainEmail\" maxlength=\"120\"/></label>");
            html.AppendLine("                            <label>Phone<br/><input type=\"tel\" id=\"cap-up-phone\" name=\"captainPhone\" maxlength=\"40\"/></label>");
            html.AppendLine("                            <label>New PIN <span class=\"captain-hint\">(leave blank to keep current)</span><br/><input type=\"text\" inputmode=\"numeric\" pattern=\"[0-9]{4,12}\" id=\"cap-up-pin\" name=\"newPin\" maxlength=\"12\" autocomplete=\"new-password\"/></label>");
            html.AppendLine("                            <label>Notes for admin<br/><textarea id=\"cap-up-notes\" name=\"notes\" rows=\"3\" maxlength=\"500\"></textarea></label>");
            html.AppendLine("                            <div class=\"captain-update-actions\">");
            html.AppendLine("                                <button type=\"submit\" class=\"captain-btn captain-btn-primary\" id=\"cap-up-submit\">Send update</button>");
            html.AppendLine("                                <span id=\"cap-up-status\" class=\"captain-update-status\"></span>");
            html.AppendLine("                            </div>");
            html.AppendLine("                        </form>");
            html.AppendLine("                    </div>");
        }

        html.AppendLine("                </div>"); // captain-tab-panels
        html.AppendLine("            </div>"); // cap-content
        html.AppendLine("        </div>");
        html.AppendLine("    </div>");

        appendFooter(html);

        var scoreSubUrl = (_settings.CaptainsScoreSubmissionUrl ?? "").Replace("'", "\\'");
        var availUrl = (_settings.CaptainsAvailabilityUrl ?? "").Replace("'", "\\'");
        var leagueEmail = (_settings.ContactEmail ?? "").Replace("'", "\\'");
        var notifyEmail = (string.IsNullOrWhiteSpace(_settings.CaptainsUpdateNotifyEmail)
            ? _settings.ContactEmail ?? ""
            : _settings.CaptainsUpdateNotifyEmail).Replace("'", "\\'");
        var formServiceUrl = (_settings.FormServiceUrl ?? "").Replace("'", "\\'");
        var showScore = _settings.CaptainsShowScoreSubmission ? "true" : "false";
        var showAvail = _settings.CaptainsShowAvailability ? "true" : "false";
        var showSheet = _settings.CaptainsShowFixturesSheetDownload ? "true" : "false";
        var showContacts = _settings.CaptainsShowContactList ? "true" : "false";
        var showFixtures = _settings.CaptainsShowFixtures ? "true" : "false";
        var showResults = _settings.CaptainsShowResults ? "true" : "false";
        var showSelfUpdate = _settings.CaptainsAllowSelfUpdate ? "true" : "false";
        var showRoster = _settings.CaptainsShowTeamRoster ? "true" : "false";

        html.AppendLine(@"    <script>
(function(){
    var cacheBuster = '" + cacheBuster + @"';
    var SHOW = {
        score: " + showScore + @", avail: " + showAvail + @", sheet: " + showSheet + @",
        contacts: " + showContacts + @", fixtures: " + showFixtures + @", results: " + showResults + @",
        selfUpdate: " + showSelfUpdate + @", roster: " + showRoster + @"
    };
    var URLS = { score: '" + scoreSubUrl + @"', avail: '" + availUrl + @"', leagueEmail: '" + leagueEmail + @"', notifyEmail: '" + notifyEmail + @"', formService: '" + formServiceUrl + @"' };

    var teamId = null;
    try { teamId = localStorage.getItem('wdpl_captain_team'); } catch (e) {}
    if (!teamId) { window.location.href = 'captains.html'; return; }

    function escapeHtml(s){ return (s||'').replace(/[&<>""']/g, function(c){ return {'&':'&amp;','<':'&lt;','>':'&gt;','""':'&quot;','\'':'&#39;'}[c]; }); }

    function initTabs(){
        var btns = document.querySelectorAll('.captain-tab-btn');
        var panels = document.querySelectorAll('.captain-tab-panel');
        if (!btns.length) return;
        function activate(name){
            var found = false;
            btns.forEach(function(b){
                var on = b.getAttribute('data-tab') === name;
                if (on) found = true;
                b.classList.toggle('active', on);
                b.setAttribute('aria-selected', on ? 'true' : 'false');
            });
            if (!found) { activate(btns[0].getAttribute('data-tab')); return; }
            panels.forEach(function(p){
                p.classList.toggle('active', p.getAttribute('data-tab') === name);
            });
            try { localStorage.setItem('wdpl_captain_tab', name); } catch (e) {}
        }
        btns.forEach(function(b){
            b.addEventListener('click', function(){ activate(b.getAttribute('data-tab')); });
        });
        var saved = null;
        try { saved = localStorage.getItem('wdpl_captain_tab'); } catch (e) {}
        if (saved) activate(saved);
    }

    fetch('captains-data.json?v=' + cacheBuster).then(function(r){ return r.json(); }).then(function(data){
        var team = (data.teams || []).find(function(t){ return t.id === teamId; });
        if (!team) { signOut(); return; }
        document.getElementById('cap-loading').style.display = 'none';
        document.getElementById('cap-content').style.display = 'block';
        document.getElementById('cap-team-name').textContent = team.name;
        document.getElementById('cap-team-division').textContent = team.division || '';
        document.getElementById('cap-captain-name').textContent = team.captain || '(not set)';
        initTabs();

        // Quick actions
        var actions = '';
        if (SHOW.score) {
            var href = URLS.score || (URLS.leagueEmail ? 'mailto:' + URLS.leagueEmail + '?subject=' + encodeURIComponent('Score submission - ' + team.name) : '');
            if (href) actions += '<a class=""captain-action"" href=""' + href + '"" target=""_blank"" rel=""noopener"">&#128221; Submit Score Card</a>';
        }
        if (SHOW.avail) {
            var ahref = URLS.avail;
            if (ahref) actions += '<a class=""captain-action"" href=""' + ahref + '"" target=""_blank"" rel=""noopener"">&#128197; Player Availability</a>';
        }
        if (SHOW.sheet) {
            actions += '<a class=""captain-action"" href=""fixtures.html"" target=""_blank"">&#128196; Printable Fixtures Sheet</a>';
        }
        document.getElementById('cap-actions').innerHTML = actions || '<p class=""captain-hint"">No quick actions configured.</p>';

        // Team roster
        if (SHOW.roster) {
            var roster = team.roster || [];
            var rosterEl = document.getElementById('cap-roster');
            if (rosterEl) {
                var rhtml = '';
                if (roster.length === 0) rhtml = '<tr><td colspan=""3"">No players registered.</td></tr>';
                else roster.forEach(function(p, i){
                    var status = p.isActive ? '<span class=""text-positive"">Active</span>' : '<span class=""text-negative"">Inactive</span>';
                    rhtml += '<tr><td>' + (i + 1) + '</td><td>' + escapeHtml(p.name) + '</td><td>' + status + '</td></tr>';
                });
                rosterEl.innerHTML = rhtml;
            }
        }

        // Fixtures (not yet played)
        if (SHOW.fixtures) {
            var upcoming = (team.fixtures || []).filter(function(f){ return !f.played; });
            var html = '';
            if (upcoming.length === 0) html = '<tr><td colspan=""4"">No upcoming fixtures.</td></tr>';
            else upcoming.forEach(function(f){
                html += '<tr><td>' + escapeHtml(f.dateDisplay) + ' ' + escapeHtml(f.time) + '</td><td>' + escapeHtml(f.opponent) + '</td><td>' + (f.isHome ? 'H' : 'A') + '</td><td>' + escapeHtml(f.venue) + '</td></tr>';
            });
            document.getElementById('cap-fixtures').innerHTML = html;
        }

        // Results (played)
        if (SHOW.results) {
            var played = (team.fixtures || []).filter(function(f){ return f.played; }).reverse();
            var html2 = '';
            if (played.length === 0) html2 = '<tr><td colspan=""5"">No results yet.</td></tr>';
            else played.forEach(function(f){
                var cls = f.result === 'W' ? 'text-positive' : (f.result === 'L' ? 'text-negative' : '');
                html2 += '<tr><td>' + escapeHtml(f.dateDisplay) + '</td><td>' + escapeHtml(f.opponent) + '</td><td>' + (f.isHome ? 'H' : 'A') + '</td><td>' + f.teamScore + '-' + f.oppScore + '</td><td class=""' + cls + '""><strong>' + (f.result || '-') + '</strong></td></tr>';
            });
            document.getElementById('cap-results').innerHTML = html2;
        }

        // Contacts
        if (SHOW.contacts) {
            var contacts = (data.contacts || []);
            var html3 = '';
            if (contacts.length === 0) html3 = '<tr><td colspan=""5"">No contacts available.</td></tr>';
            else contacts.forEach(function(c){
                var emailCell = c.email ? '<a href=""mailto:' + escapeHtml(c.email) + '"">' + escapeHtml(c.email) + '</a>' : '';
                var phoneCell = c.phone ? '<a href=""tel:' + escapeHtml(c.phone.replace(/\s+/g,'')) + '"">' + escapeHtml(c.phone) + '</a>' : '';
                var rowCls = (c.teamId === teamId) ? ' style=""background:rgba(59,130,246,0.08);""' : '';
                html3 += '<tr' + rowCls + '><td><strong>' + escapeHtml(c.teamName) + '</strong></td><td>' + escapeHtml(c.division) + '</td><td>' + escapeHtml(c.captain) + '</td><td>' + emailCell + '</td><td>' + phoneCell + '</td></tr>';
            });
            document.getElementById('cap-contacts').innerHTML = html3;
        }

        // My Details (self-service update)
        if (SHOW.selfUpdate) {
            var nameEl = document.getElementById('cap-up-name');
            var emailEl = document.getElementById('cap-up-email');
            var phoneEl = document.getElementById('cap-up-phone');
            var pinEl = document.getElementById('cap-up-pin');
            var notesEl = document.getElementById('cap-up-notes');
            var statusEl = document.getElementById('cap-up-status');
            var submitBtn = document.getElementById('cap-up-submit');
            var formEl = document.getElementById('cap-update-form');
            function fallbackMailto(payload) {
                var to = URLS.notifyEmail || URLS.leagueEmail;
                if (!to) {
                    statusEl.className = 'captain-update-status err';
                    statusEl.textContent = 'No submission endpoint configured. Contact your league administrator.';
                    return;
                }
                var lines = [
                    'Captain update request',
                    '',
                    'Team: ' + payload.teamName + (payload.division ? ' (' + payload.division + ')' : ''),
                    'Captain name: ' + payload.captainName,
                    'Email: ' + payload.captainEmail,
                    'Phone: ' + payload.captainPhone,
                    'New PIN requested: ' + (payload.newPin ? '(yes - ' + payload.newPin.length + ' digits)' : '(no change)'),
                    '',
                    'Notes:',
                    payload.notes || '(none)'
                ];
                var subject = encodeURIComponent('Captain update - ' + payload.teamName);
                var body = encodeURIComponent(lines.join('\n'));
                window.location.href = 'mailto:' + to + '?subject=' + subject + '&body=' + body;
                statusEl.className = 'captain-update-status ok';
                statusEl.textContent = 'Opening your email client...';
            }
            if (nameEl && formEl) {
                nameEl.value = team.captain || '';
                emailEl.value = team.email || '';
                phoneEl.value = team.phone || '';
                formEl.addEventListener('submit', function(ev){
                    ev.preventDefault();
                    statusEl.className = 'captain-update-status';
                    statusEl.textContent = '';
                    var newPin = (pinEl.value || '').trim();
                    if (newPin && !/^[0-9]{4,12}$/.test(newPin)) {
                        statusEl.className = 'captain-update-status err';
                        statusEl.textContent = 'PIN must be 4-12 digits.';
                        return;
                    }
                    var payload = {
                        formType: 'captain-update',
                        teamId: team.id,
                        teamName: team.name,
                        division: team.division || '',
                        captainName: (nameEl.value || '').trim(),
                        captainEmail: (emailEl.value || '').trim(),
                        captainPhone: (phoneEl.value || '').trim(),
                        newPin: newPin,
                        notes: (notesEl.value || '').trim(),
                        submittedAt: new Date().toISOString()
                    };
                    submitBtn.disabled = true;
                    statusEl.textContent = 'Sending...';
                    if (URLS.formService) {
                        fetch(URLS.formService, {
                            method: 'POST',
                            headers: { 'Accept': 'application/json', 'Content-Type': 'application/json' },
                            body: JSON.stringify(payload)
                        }).then(function(r){
                            if (!r.ok) throw new Error('HTTP ' + r.status);
                            statusEl.className = 'captain-update-status ok';
                            statusEl.textContent = 'Sent. The administrator will review your changes.';
                            pinEl.value = '';
                            notesEl.value = '';
                        }).catch(function(){
                            fallbackMailto(payload);
                        }).finally(function(){ submitBtn.disabled = false; });
                    } else {
                        fallbackMailto(payload);
                        submitBtn.disabled = false;
                    }
                });
            }
        }
    }).catch(function(){
        document.getElementById('cap-loading').innerHTML = '<p>Failed to load captain data.</p>';
    });

    function signOut(){
        try {
            localStorage.removeItem('wdpl_captain_team');
            localStorage.removeItem('wdpl_captain_login_at');
        } catch (e) {}
        window.location.href = 'captains.html';
    }
    document.getElementById('cap-logout').addEventListener('click', signOut);
})();
    </script>");

        if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
            html.AppendLine(_settings.CustomBodyEndHtml);

        html.AppendLine("</body>");
        html.AppendLine("</html>");
        return html.ToString();
    }

    private static string EscapeHtml(string s) =>
        (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
}
