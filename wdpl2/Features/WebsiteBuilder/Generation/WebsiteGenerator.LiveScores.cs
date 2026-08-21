using System;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    /// <summary>
    /// Live Scores page generation.
    ///
    /// The page is static HTML that polls the public backend endpoint
    /// (<c>api/public/live.php</c>) so spectators see frame-by-frame scores while
    /// the team captains are still filling in their shared online scorecard.
    /// </summary>
    public sealed partial class WebsiteGenerator
    {
        /// <summary>
        /// Resolves the live scores endpoint used by the generated JavaScript.
        /// Returns an empty string when it should be auto-detected at runtime.
        /// </summary>
        private string LiveScoresEndpoint() =>
            (_settings.LiveScoresApiBaseUrl ?? string.Empty).Trim();

        /// <summary>
        /// Poll interval in milliseconds, clamped to a range that is kind to shared hosting.
        /// </summary>
        private int LiveScoresPollMs()
        {
            var seconds = _settings.LiveScoresRefreshSeconds;
            if (seconds < 5) seconds = 5;
            if (seconds > 300) seconds = 300;
            return seconds * 1000;
        }

        /// <summary>
        /// Emits the shared <c>wdplLive</c> helper: endpoint resolution plus a
        /// polling loop that invokes a render callback with the fetched payload.
        /// </summary>
        private void AppendLiveScoresScript(StringBuilder html)
        {
            var configured = JsString(LiveScoresEndpoint());

            html.AppendLine("    <script>");
            html.AppendLine("    (function () {");
            html.AppendLine($"        var configured = {configured};");
            html.AppendLine("        // The generated site normally sits one folder below the backend,");
            html.AppendLine("        // matching the ../captain/ convention used by the captains page.");
            html.AppendLine("        function resolveEndpoint() {");
            html.AppendLine("            if (configured) return configured;");
            html.AppendLine("            var segments = window.location.pathname.split('/').filter(function (s) { return s.length > 0; });");
            html.AppendLine("            return segments.length <= 1 ? '/api/public/live.php' : '../api/public/live.php';");
            html.AppendLine("        }");
            html.AppendLine("        var endpoint = resolveEndpoint();");
            html.AppendLine($"        var intervalMs = {LiveScoresPollMs()};");
            html.AppendLine("        var timer = null;");
            html.AppendLine();
            html.AppendLine("        function poll(render, onError) {");
            html.AppendLine("            var url = endpoint + (endpoint.indexOf('?') >= 0 ? '&' : '?') + '_=' + Date.now();");
            html.AppendLine("            fetch(url, { cache: 'no-store' })");
            html.AppendLine("                .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })");
            html.AppendLine("                .then(function (data) { render(data && data.items ? data.items : [], data); })");
            html.AppendLine("                .catch(function (err) { if (onError) onError(err); });");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        window.wdplLive = {");
            html.AppendLine("            endpoint: endpoint,");
            html.AppendLine("            intervalMs: intervalMs,");
            html.AppendLine("            start: function (render, onError) {");
            html.AppendLine("                poll(render, onError);");
            html.AppendLine("                if (timer) clearInterval(timer);");
            html.AppendLine("                timer = setInterval(function () {");
            html.AppendLine("                    // Pause polling while the tab is hidden to save data/battery.");
            html.AppendLine("                    if (document.hidden) return;");
            html.AppendLine("                    poll(render, onError);");
            html.AppendLine("                }, intervalMs);");
            html.AppendLine("            }");
            html.AppendLine("        };");
            html.AppendLine("    })();");
            html.AppendLine("    </script>");
        }

        /// <summary>
        /// Generates <c>live.html</c> — the public live scores board.
        /// </summary>
        private string GenerateLiveScoresPage(Season season, WebsiteTemplate template)
        {
            var html = new StringBuilder();

            AppendDocumentHead(html, $"{_settings.LiveScoresPageTitle} - {_settings.LeagueName}", season);
            html.AppendLine("<body>");

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyStartHtml))
                html.AppendLine(_settings.CustomBodyStartHtml);

            AppendHeader(html, season);
            AppendNavigation(html, "Live Scores");

            html.AppendLine("    <div class=\"content-area\">");
            html.AppendLine("        <div class=\"container\">");
            html.AppendLine("            <div class=\"hero\">");
            html.AppendLine($"                <h2><span class=\"live-dot\"></span> {Esc(_settings.LiveScoresPageTitle)}</h2>");
            html.AppendLine($"                <p class=\"live-subtitle\">Scores update automatically every {_settings.LiveScoresRefreshSeconds} seconds while captains score their match.</p>");
            html.AppendLine("            </div>");
            html.AppendLine();
            html.AppendLine("            <p class=\"live-status\" id=\"live-status\">Loading live scores&hellip;</p>");
            html.AppendLine("            <div class=\"live-board\" id=\"live-board\"></div>");
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");

            AppendLiveScoresScript(html);
            AppendLiveScoresBoardScript(html);

            AppendFooter(html);

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        /// <summary>
        /// Rendering script for the full live board (cards per match, optional frame detail).
        /// </summary>
        private void AppendLiveScoresBoardScript(StringBuilder html)
        {
            var emptyMessage = JsString(_settings.LiveScoresEmptyMessage);

            html.AppendLine("    <script>");
            html.AppendLine("    (function () {");
            html.AppendLine("        var board = document.getElementById('live-board');");
            html.AppendLine("        var status = document.getElementById('live-status');");
            html.AppendLine("        if (!board || !window.wdplLive) return;");
            html.AppendLine($"        var emptyMessage = {emptyMessage};");
            html.AppendLine($"        var showFrames = {(_settings.LiveScoresShowFrameDetail ? "true" : "false")};");
            html.AppendLine();
            html.AppendLine("        function esc(value) {");
            html.AppendLine("            return String(value === null || value === undefined ? '' : value)");
            html.AppendLine("                .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\"/g, '&quot;');");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        function frameDetail(match) {");
            html.AppendLine("            if (!showFrames || !match.frames || !match.frames.length) return '';");
            html.AppendLine("            var decided = match.frames.filter(function (f) { return f.winner; });");
            html.AppendLine("            if (!decided.length) return '';");
            html.AppendLine("            var rows = decided.map(function (f) {");
            html.AppendLine("                return '<li class=\"live-frame live-frame-' + esc(f.winner) + '\">' +");
            html.AppendLine("                    '<span class=\"live-frame-no\">F' + esc(f.number) + '</span>' +");
            html.AppendLine("                    '<span class=\"live-frame-home\">' + esc(f.home_player || '') + '</span>' +");
            html.AppendLine("                    '<span class=\"live-frame-vs\">v</span>' +");
            html.AppendLine("                    '<span class=\"live-frame-away\">' + esc(f.away_player || '') + '</span>' +");
            html.AppendLine("                    (f.eight_ball ? '<span class=\"live-frame-eight\" title=\"8-ball\">&#127923;</span>' : '') +");
            html.AppendLine("                '</li>';");
            html.AppendLine("            }).join('');");
            html.AppendLine("            return '<ul class=\"live-frames\">' + rows + '</ul>';");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        function card(match) {");
            html.AppendLine("            var badge = match.status === 'final' ? 'Final'");
            html.AppendLine("                : (match.status === 'confirming' ? 'Awaiting confirmation' : 'Live');");
            html.AppendLine("            return '<article class=\"live-card live-card-' + esc(match.status) + '\">' +");
            html.AppendLine("                '<header class=\"live-card-head\">' +");
            html.AppendLine("                    '<span class=\"live-badge\">' + esc(badge) + '</span>' +");
            html.AppendLine("                    '<span class=\"live-division\">' + esc(match.division_name || '') + '</span>' +");
            html.AppendLine("                '</header>' +");
            html.AppendLine("                '<div class=\"live-teams\">' +");
            html.AppendLine("                    '<span class=\"live-team live-home\">' + esc(match.home_team_name) + '</span>' +");
            html.AppendLine("                    '<span class=\"live-score\">' + esc(match.home_score) + ' &ndash; ' + esc(match.away_score) + '</span>' +");
            html.AppendLine("                    '<span class=\"live-team live-away\">' + esc(match.away_team_name) + '</span>' +");
            html.AppendLine("                '</div>' +");
            html.AppendLine("                '<p class=\"live-meta\">' + esc(match.frames_played) + ' of ' + esc(match.frames_total) + ' frames' +");
            html.AppendLine("                    (match.venue_name ? ' &middot; &#128205; ' + esc(match.venue_name) : '') + '</p>' +");
            html.AppendLine("                frameDetail(match) +");
            html.AppendLine("            '</article>';");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        function render(items) {");
            html.AppendLine("            if (!items.length) {");
            html.AppendLine("                board.innerHTML = '';");
            html.AppendLine("                status.textContent = emptyMessage;");
            html.AppendLine("                return;");
            html.AppendLine("            }");
            html.AppendLine("            status.textContent = 'Last updated ' + new Date().toLocaleTimeString();");
            html.AppendLine("            board.innerHTML = items.map(card).join('');");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        window.wdplLive.start(render, function () {");
            html.AppendLine("            status.textContent = 'Live scores are unavailable right now.';");
            html.AppendLine("        });");
            html.AppendLine("    })();");
            html.AppendLine("    </script>");
        }

        /// <summary>
        /// Appends a compact "matches in progress" strip to the home page.
        /// The strip hides itself entirely when nothing is being scored.
        /// </summary>
        private void AppendHomeLiveScoresWidget(StringBuilder html)
        {
            if (!_settings.ShowLiveScores || !_settings.LiveScoresShowOnHome) return;

            html.AppendLine("    <div class=\"container\" id=\"live-strip-wrap\" style=\"display:none;\">");
            html.AppendLine("        <div class=\"section\">");
            html.AppendLine($"            <h2><span class=\"live-dot\"></span> {Esc(_settings.LiveScoresPageTitle)}</h2>");
            html.AppendLine("            <div class=\"live-board\" id=\"live-strip\"></div>");
            html.AppendLine($"            <p class=\"view-all\"><a href=\"live.html\">{Esc(_settings.LiveScoresNavLabel)} &#8594;</a></p>");
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");

            AppendLiveScoresScript(html);

            html.AppendLine("    <script>");
            html.AppendLine("    (function () {");
            html.AppendLine("        var wrap = document.getElementById('live-strip-wrap');");
            html.AppendLine("        var strip = document.getElementById('live-strip');");
            html.AppendLine("        if (!wrap || !strip || !window.wdplLive) return;");
            html.AppendLine();
            html.AppendLine("        function esc(value) {");
            html.AppendLine("            return String(value === null || value === undefined ? '' : value)");
            html.AppendLine("                .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\"/g, '&quot;');");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        window.wdplLive.start(function (items) {");
            html.AppendLine("            if (!items.length) { wrap.style.display = 'none'; return; }");
            html.AppendLine("            wrap.style.display = '';");
            html.AppendLine("            strip.innerHTML = items.slice(0, 6).map(function (m) {");
            html.AppendLine("                return '<article class=\"live-card live-card-' + esc(m.status) + '\">' +");
            html.AppendLine("                    '<div class=\"live-teams\">' +");
            html.AppendLine("                        '<span class=\"live-team live-home\">' + esc(m.home_team_name) + '</span>' +");
            html.AppendLine("                        '<span class=\"live-score\">' + esc(m.home_score) + ' &ndash; ' + esc(m.away_score) + '</span>' +");
            html.AppendLine("                        '<span class=\"live-team live-away\">' + esc(m.away_team_name) + '</span>' +");
            html.AppendLine("                    '</div>' +");
            html.AppendLine("                    '<p class=\"live-meta\">' + esc(m.frames_played) + ' of ' + esc(m.frames_total) + ' frames</p>' +");
            html.AppendLine("                '</article>';");
            html.AppendLine("            }).join('');");
            html.AppendLine("        }, function () { wrap.style.display = 'none'; });");
            html.AppendLine("    })();");
            html.AppendLine("    </script>");
        }

        /// <summary>Encodes a string as a JavaScript string literal (including quotes).</summary>
        private static string JsString(string? value)
        {
            var raw = value ?? string.Empty;
            var sb = new StringBuilder(raw.Length + 2);
            sb.Append('\'');
            foreach (var ch in raw)
            {
                switch (ch)
                {
                    case '\'': sb.Append("\\'"); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '<': sb.Append("\\u003C"); break;
                    default: sb.Append(ch); break;
                }
            }
            sb.Append('\'');
            return sb.ToString();
        }
    }
}
