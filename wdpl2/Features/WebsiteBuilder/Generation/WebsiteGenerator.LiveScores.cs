using System;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    /// <summary>
    /// Live Scores page generation.
    ///
    /// The page is static HTML that polls the public backend endpoint
    /// (<c>api/index.php?m=league&amp;a=live</c>) so spectators see frame-by-frame scores while
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
        /// <param name="withFrames">
        /// Whether to ask the backend for the frame-by-frame detail. Only the
        /// full board draws it, and only when the league has turned it on - the
        /// home page strip asks for the scoreline alone.
        /// </param>
        private void AppendLiveScoresScript(StringBuilder html, bool withFrames)
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
            html.AppendLine("            var base = segments.length <= 1 ? '/api/' : '../api/';");
            html.AppendLine("            return base + 'index.php?m=league&a=live';");
            html.AppendLine("        }");
            html.AppendLine("        var endpoint = resolveEndpoint();");
            html.AppendLine($"        var intervalMs = {LiveScoresPollMs()};");
            html.AppendLine("        var timer = null;");
            html.AppendLine();
            html.AppendLine($"        var wantFrames = {(withFrames ? "true" : "false")};");
            html.AppendLine();
            html.AppendLine("        // The board and the crawl both want the same scores. They subscribe");
            html.AppendLine("        // rather than each starting their own loop, so the page makes one");
            html.AppendLine("        // request a tick however many things are reading it - and so a second");
            html.AppendLine("        // subscriber cannot cancel the first one's timer.");
            html.AppendLine("        var subscribers = [];");
            html.AppendLine("        var latest = null;");
            html.AppendLine();
            html.AppendLine("        function deliver(sub, items, payload) {");
            html.AppendLine("            // One subscriber throwing must not stop the rest being drawn.");
            html.AppendLine("            try { sub.render(items, payload); } catch (e) { }");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        function poll() {");
            html.AppendLine("            var url = endpoint + (endpoint.indexOf('?') >= 0 ? '&' : '?')");
            html.AppendLine("                + (wantFrames ? 'frames=1&' : '') + '_=' + Date.now();");
            html.AppendLine("            fetch(url, { cache: 'no-store' })");
            html.AppendLine("                .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })");
            html.AppendLine("                // The backend wraps every reply as {ok, data}; older builds returned the payload bare.");
            html.AppendLine("                .then(function (body) {");
            html.AppendLine("                    var payload = body && body.data ? body.data : body;");
            html.AppendLine("                    var items = payload && payload.items ? payload.items : [];");
            html.AppendLine("                    latest = { items: items, payload: payload };");
            html.AppendLine("                    for (var i = 0; i < subscribers.length; i++) deliver(subscribers[i], items, payload);");
            html.AppendLine("                })");
            html.AppendLine("                .catch(function (err) {");
            html.AppendLine("                    for (var i = 0; i < subscribers.length; i++) {");
            html.AppendLine("                        var onError = subscribers[i].onError;");
            html.AppendLine("                        if (onError) { try { onError(err); } catch (e) { } }");
            html.AppendLine("                    }");
            html.AppendLine("                });");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        window.wdplLive = {");
            html.AppendLine("            endpoint: endpoint,");
            html.AppendLine("            intervalMs: intervalMs,");
            html.AppendLine("            /** The time a payload was generated, as a clock reads it. */");
            html.AppendLine("            clock: function (iso) {");
            html.AppendLine("                var when = iso ? new Date(iso) : new Date();");
            html.AppendLine("                if (isNaN(when)) when = new Date();");
            html.AppendLine("                return when.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });");
            html.AppendLine("            },");
            html.AppendLine("            start: function (render, onError) {");
            html.AppendLine("                var sub = { render: render, onError: onError };");
            html.AppendLine("                subscribers.push(sub);");
            html.AppendLine();
            html.AppendLine("                // A later subscriber draws from what has already arrived rather");
            html.AppendLine("                // than sitting blank until the next tick.");
            html.AppendLine("                if (latest) deliver(sub, latest.items, latest.payload);");
            html.AppendLine();
            html.AppendLine("                if (timer) return;");
            html.AppendLine("                poll();");
            html.AppendLine("                timer = setInterval(function () {");
            html.AppendLine("                    // Pause polling while the tab is hidden to save data/battery.");
            html.AppendLine("                    if (document.hidden) return;");
            html.AppendLine("                    poll();");
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
            // The board is a dark studio panel inside the league's own page
            // chrome, rather than a dark page: the header, nav and footer stay
            // in the league's colours whatever template they have chosen.
            html.AppendLine("            <section class=\"live-studio\">");
            html.AppendLine("                <div class=\"live-studio-head\">");
            html.AppendLine("                    <div class=\"live-studio-title\">");
            html.AppendLine($"                        <span class=\"live-onair\"><span class=\"live-dot\"></span> On air</span>");
            html.AppendLine($"                        <h2>{Esc(_settings.LiveScoresPageTitle)}</h2>");
            html.AppendLine("                    </div>");
            html.AppendLine("                    <div class=\"live-studio-meta\">");
            html.AppendLine("                        <span class=\"live-count\" id=\"live-count\">&mdash;</span>");
            html.AppendLine("                        <span class=\"live-clock\" id=\"live-clock\">&mdash;&mdash;:&mdash;&mdash;</span>");
            html.AppendLine("                    </div>");
            html.AppendLine("                </div>");
            html.AppendLine($"                <p class=\"live-subtitle\">Updating every {_settings.LiveScoresRefreshSeconds} seconds while the captains score their match.</p>");
            html.AppendLine("                <p class=\"live-status\" id=\"live-status\">Loading live scores&hellip;</p>");
            html.AppendLine("                <div class=\"live-board\" id=\"live-board\"></div>");
            html.AppendLine("            </section>");
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");

            AppendLiveScoresScript(html, _settings.LiveScoresShowFrameDetail);
            AppendLiveScoresBoardScript(html);

            AppendFooter(html);

            // After the footer and outside the page flow: the crawl is fixed to
            // the foot of the screen, so it must not sit inside a column that
            // could clip it.
            AppendLiveTicker(html);

            if (!string.IsNullOrWhiteSpace(_settings.CustomBodyEndHtml))
                html.AppendLine(_settings.CustomBodyEndHtml);

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        /// <summary>
        /// Rendering script for the full live board: one broadcast panel per
        /// match, with the frame-by-frame player results underneath it.
        /// </summary>
        private void AppendLiveScoresBoardScript(StringBuilder html)
        {
            var emptyMessage = JsString(_settings.LiveScoresEmptyMessage);

            html.AppendLine("    <script>");
            html.AppendLine("    (function () {");
            html.AppendLine("        var board = document.getElementById('live-board');");
            html.AppendLine("        var status = document.getElementById('live-status');");
            html.AppendLine("        var clock = document.getElementById('live-clock');");
            html.AppendLine("        var count = document.getElementById('live-count');");
            html.AppendLine("        if (!board || !window.wdplLive) return;");
            html.AppendLine($"        var emptyMessage = {emptyMessage};");
            html.AppendLine($"        var showFrames = {(_settings.LiveScoresShowFrameDetail ? "true" : "false")};");
            html.AppendLine();
            html.AppendLine("        function esc(value) {");
            html.AppendLine("            return String(value === null || value === undefined ? '' : value)");
            html.AppendLine("                .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\"/g, '&quot;');");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        // The frames the backend sends are the decided ones only, newest last.");
            html.AppendLine("        // Shown newest first here, the way a results feed reads.");
            html.AppendLine("        function results(match) {");
            html.AppendLine("            if (!showFrames || !match.frames || !match.frames.length) return '';");
            html.AppendLine("            var rows = match.frames.slice().reverse().map(function (f) {");
            html.AppendLine("                var homeWon = f.winner === 'home';");
            html.AppendLine("                var tag = (f.doubles ? 'D' : 'F') + f.number;");
            html.AppendLine("                return '<li class=\"live-frame live-frame-won-' + esc(f.winner) + '\">' +");
            html.AppendLine("                    '<span class=\"live-frame-no\">' + esc(tag) + '</span>' +");
            html.AppendLine("                    '<span class=\"live-frame-home\">' + esc(f.home_player || '&mdash;') + '</span>' +");
            html.AppendLine("                    '<span class=\"live-frame-mark\">' + (homeWon ? '&#9666;' : '&#9656;') + '</span>' +");
            html.AppendLine("                    '<span class=\"live-frame-away\">' + esc(f.away_player || '&mdash;') + '</span>' +");
            html.AppendLine("                    '<span class=\"live-frame-eight\">' + (f.eight_ball ? '<abbr title=\"Won on the black\">8</abbr>' : '') + '</span>' +");
            html.AppendLine("                '</li>';");
            html.AppendLine("            }).join('');");
            html.AppendLine("            return '<div class=\"live-results\">' +");
            html.AppendLine("                '<h4 class=\"live-results-head\">Frames</h4>' +");
            html.AppendLine("                '<ul class=\"live-frames\">' + rows + '</ul>' +");
            html.AppendLine("            '</div>';");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        function card(match) {");
            html.AppendLine("            // A card stays 'live' until one captain signs it off; it then waits on");
            html.AppendLine("            // the other, which can be a long time with the last frame already played.");
            html.AppendLine("            var confirming = match.status === 'confirming';");
            html.AppendLine("            var badge = confirming ? 'Awaiting confirmation' : 'Live';");
            html.AppendLine("            var kind = match.kind === 'cup' ? '<span class=\"live-kind\">Cup</span>' : '';");
            html.AppendLine("            var total = match.frames_total || 0;");
            html.AppendLine("            var pct = total ? Math.round((match.frames_played / total) * 100) : 0;");
            html.AppendLine("            return '<article class=\"live-card live-card-' + esc(match.status || 'live') + '\">' +");
            html.AppendLine("                '<div class=\"live-card-head\">' +");
            html.AppendLine("                    '<span class=\"live-badge\">' + (confirming ? '' : '<span class=\"live-dot\"></span>') + esc(badge) + '</span>' +");
            html.AppendLine("                    '<span class=\"live-division\">' + esc(match.division_name || '') + kind + '</span>' +");
            html.AppendLine("                '</div>' +");
            html.AppendLine("                '<div class=\"live-teams\">' +");
            html.AppendLine("                    '<span class=\"live-team live-home\">' + esc(match.home_team_name) + '</span>' +");
            html.AppendLine("                    '<span class=\"live-score\">' +");
            html.AppendLine("                        '<span class=\"live-score-n\">' + esc(match.home_score) + '</span>' +");
            html.AppendLine("                        '<span class=\"live-score-sep\">&ndash;</span>' +");
            html.AppendLine("                        '<span class=\"live-score-n\">' + esc(match.away_score) + '</span>' +");
            html.AppendLine("                    '</span>' +");
            html.AppendLine("                    '<span class=\"live-team live-away\">' + esc(match.away_team_name) + '</span>' +");
            html.AppendLine("                '</div>' +");
            html.AppendLine("                '<div class=\"live-progress\"><span style=\"width:' + pct + '%\"></span></div>' +");
            html.AppendLine("                '<p class=\"live-meta\">' + esc(match.frames_played) + ' of ' + esc(total) + ' frames' +");
            html.AppendLine("                    (match.venue_name ? ' &middot; ' + esc(match.venue_name) : '') + '</p>' +");
            html.AppendLine("                results(match) +");
            html.AppendLine("            '</article>';");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        function render(items, payload) {");
            html.AppendLine("            if (clock) clock.textContent = window.wdplLive.clock(payload && payload.generatedUtc);");
            html.AppendLine("            if (count) count.textContent = items.length");
            html.AppendLine("                ? items.length + (items.length === 1 ? ' match on' : ' matches on')");
            html.AppendLine("                : 'Off air';");
            html.AppendLine("            if (!items.length) {");
            html.AppendLine("                board.innerHTML = '';");
            html.AppendLine("                status.textContent = emptyMessage;");
            html.AppendLine("                return;");
            html.AppendLine("            }");
            html.AppendLine("            status.textContent = '';");
            html.AppendLine("            board.innerHTML = items.map(card).join('');");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        window.wdplLive.start(render, function () {");
            html.AppendLine("            status.textContent = 'Live scores are unavailable right now.';");
            html.AppendLine("            if (count) count.textContent = 'Off air';");
            html.AppendLine("        });");
            html.AppendLine("    })();");
            html.AppendLine("    </script>");
        }

        /// <summary>
        /// The crawl along the foot of the screen - the same one the captains
        /// see under their own card, so the scores read the same in the pub as
        /// they do on the phone.
        /// </summary>
        /// <remarks>
        /// The track holds two identical runs and the animation travels exactly
        /// half of it, so the copy arrives where the original began and the line
        /// repeats with no gap. The duration is measured from the rendered width
        /// rather than guessed, so the line moves at one reading pace whether
        /// two matches are on or twenty.
        /// </remarks>
        private void AppendLiveTicker(StringBuilder html)
        {
            html.AppendLine("    <div id=\"tick\" class=\"live-tick\" hidden aria-hidden=\"true\">");
            html.AppendLine("        <span class=\"tick-badge\" id=\"tickLabel\">Live</span>");
            html.AppendLine("        <div class=\"tick-window\"><div class=\"tick-track\" id=\"tickTrack\"></div></div>");
            html.AppendLine("    </div>");

            html.AppendLine("    <script>");
            html.AppendLine("    (function () {");
            html.AppendLine("        var tick = document.getElementById('tick');");
            html.AppendLine("        var track = document.getElementById('tickTrack');");
            html.AppendLine("        var label = document.getElementById('tickLabel');");
            html.AppendLine("        if (!tick || !track || !window.wdplLive) return;");
            html.AppendLine();
            html.AppendLine("        /** How fast the line travels, in pixels a second. Reading pace, not racing. */");
            html.AppendLine("        var SPEED = 60;");
            html.AppendLine("        var seen = {};");
            html.AppendLine("        var first = true;");
            html.AppendLine();
            html.AppendLine("        function esc(value) {");
            html.AppendLine("            return String(value === null || value === undefined ? '' : value)");
            html.AppendLine("                .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\"/g, '&quot;');");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        function show(on) {");
            html.AppendLine("            tick.hidden = !on;");
            html.AppendLine("            document.body.classList.toggle('has-tick', on);");
            html.AppendLine("        }");
            html.AppendLine();
            html.AppendLine("        window.wdplLive.start(function (items, payload) {");
            html.AppendLine("            if (label) label.textContent = window.wdplLive.clock(payload && payload.generatedUtc);");
            html.AppendLine();
            html.AppendLine("            if (!items.length) { show(false); return; }");
            html.AppendLine();
            html.AppendLine("            var run = items.map(function (m) {");
            html.AppendLine("                var key = String(m.fixture_id);");
            html.AppendLine("                var score = m.home_score + '-' + m.away_score;");
            html.AppendLine("                // A frame has just gone in. It stays marked until the score changes");
            html.AppendLine("                // again, so it is still lit when it next comes round - a flash on a");
            html.AppendLine("                // timer would nearly always expire before the line scrolled past.");
            html.AppendLine("                var moved = !first && seen[key] !== undefined && seen[key] !== score;");
            html.AppendLine("                seen[key] = score;");
            html.AppendLine();
            html.AppendLine("                return '<span class=\"tick-item' + (moved ? ' moved' : '') + '\">' +");
            html.AppendLine("                    esc(m.home_team_name || '?') +");
            html.AppendLine("                    '<span class=\"sc\">' + esc(m.home_score) + '\\u2013' + esc(m.away_score) + '</span>' +");
            html.AppendLine("                    esc(m.away_team_name || '?') +");
            html.AppendLine("                    (m.kind === 'cup' ? '<span class=\"cup\">CUP</span>' : '') +");
            html.AppendLine("                    '<span class=\"of\">' + esc(m.frames_played) + '/' + esc(m.frames_total) + '</span>' +");
            html.AppendLine("                    '<span class=\"dot\">\\u25CF</span>' +");
            html.AppendLine("                '</span>';");
            html.AppendLine("            }).join('');");
            html.AppendLine();
            html.AppendLine("            track.innerHTML = '<div class=\"tick-run\">' + run + '</div>' +");
            html.AppendLine("                              '<div class=\"tick-run\">' + run + '</div>';");
            html.AppendLine("            show(true);");
            html.AppendLine();
            html.AppendLine("            // Measured, not guessed: one speed whatever is on tonight.");
            html.AppendLine("            var one = track.firstElementChild;");
            html.AppendLine("            var width = one ? one.offsetWidth : 0;");
            html.AppendLine("            track.style.setProperty('--tick-secs', Math.max(12, Math.round(width / SPEED)) + 's');");
            html.AppendLine("            first = false;");
            html.AppendLine("        }, function () { show(false); });");
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

            // The strip is a scoreline and nothing else, so it never asks for frames.
            AppendLiveScoresScript(html, false);

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
            html.AppendLine("                return '<article class=\"live-card live-card-' + esc(m.status || 'live') + '\">' +");
            html.AppendLine("                    '<div class=\"live-teams\">' +");
            html.AppendLine("                        '<span class=\"live-team live-home\">' + esc(m.home_team_name) + '</span>' +");
            html.AppendLine("                        '<span class=\"live-score\">' + esc(m.home_score) + ' &ndash; ' + esc(m.away_score) + '</span>' +");
            html.AppendLine("                        '<span class=\"live-team live-away\">' + esc(m.away_team_name) + '</span>' +");
            html.AppendLine("                    '</div>' +");
            html.AppendLine("                    '<p class=\"live-meta\">' + esc(m.frames_played) + ' of ' + esc(m.frames_total) + ' frames' +");
            html.AppendLine("                        (m.kind === 'cup' ? ' &middot; Cup' : '') + '</p>' +");
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
