using System.Collections.Generic;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    /// <summary>
    /// WebsiteGenerator partial class containing CSS/stylesheet generation methods.
    /// </summary>
    public sealed partial class WebsiteGenerator
    {
        private string GenerateHeaderLayoutCSS()
        {
            var layout = _settings.HeaderLayout;
            var patternSvg = _settings.ShowHeaderPattern
                ? "background-image: url(\"data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='none' fill-rule='evenodd'%3E%3Cg fill='%23ffffff' fill-opacity='0.05'%3E%3Cpath d='M36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E\");"
                : "";
            var gradient = "linear-gradient(135deg, var(--primary-color) 0%, var(--secondary-color) 100%)";

            // --- header element ---
            var headerBg = layout switch
            {
                "glass" => "rgba(255,255,255,0.1)",
                "animated-gradient" or "wave-gradient" => $"linear-gradient(270deg, var(--primary-color), var(--secondary-color), var(--accent-color), var(--primary-color))",
                "mesh-gradient" => $"var(--primary-color)",
                "stadium" => "#0a0a0a",
                "pulse-glow" => gradient,
                "shimmer" => gradient,
                "aurora" => "#0a0a2e",
                "neon" => "#0a0a0a",
                "spotlight-sweep" => gradient,
                "breathing" => gradient,
                "text-only" => "transparent",
                "underline" => "transparent",
                "transparent" => "transparent",
                _ => gradient
            };
            var headerPadding = layout switch
            {
                "compact" or "minimal-bar" => "12px 20px",
                "banner" or "stadium" or "aurora" or "neon" => "60px 20px",
                "text-only" or "underline" or "transparent" => "30px 20px",
                _ => "40px 20px"
            };
            var headerAlign = layout switch
            {
                "split" or "minimal-bar" or "scoreboard" => "left",
                _ => "center"
            };
            var headerColor = layout switch
            {
                "text-only" or "underline" or "transparent" => "var(--text-color)",
                _ => "var(--header-text)"
            };
            var headerExtra = layout switch
            {
                "glass" => "backdrop-filter: blur(20px); -webkit-backdrop-filter: blur(20px); border-bottom: 1px solid rgba(255,255,255,0.18);",
                "animated-gradient" => "background-size: 300% 300%; animation: headerGradientShift 8s ease infinite;",
                "wave-gradient" => "background-size: 300% 300%; animation: headerWaveGradient 6s ease-in-out infinite;",
                "mesh-gradient" => $"background: radial-gradient(ellipse at 20% 50%, var(--secondary-color) 0%, transparent 50%), radial-gradient(ellipse at 80% 20%, var(--accent-color) 0%, transparent 50%), radial-gradient(ellipse at 50% 80%, var(--primary-color) 0%, transparent 60%), var(--primary-color);",
                "stadium" => "background: radial-gradient(ellipse at 30% 0%, rgba(255,255,255,0.15) 0%, transparent 50%), radial-gradient(ellipse at 70% 0%, rgba(255,255,255,0.12) 0%, transparent 50%), radial-gradient(ellipse at 50% 100%, rgba(255,255,255,0.05) 0%, transparent 40%), #0a0a0a;",
                "pulse-glow" => "animation: headerPulseGlow 3s ease-in-out infinite; overflow: hidden;",
                "shimmer" => "overflow: hidden; position: relative;",
                "aurora" => "overflow: hidden; background: linear-gradient(135deg, #0a0a2e 0%, #1a1a4e 100%);",
                "neon" => "overflow: hidden;",
                "spotlight-sweep" => "overflow: hidden; position: relative;",
                "breathing" => "",
                "card" => "overflow: visible;",
                "overlay-hero" => "position: relative; z-index: 2; margin-bottom: -40px;",
                "championship" => "clip-path: polygon(0 0, 100% 0, 100% 85%, 50% 100%, 0 85%); padding-bottom: 60px;",
                "underline" => $"border-bottom: 4px solid var(--primary-color);",
                _ => ""
            };
            var headerPatternUse = layout switch
            {
                "glass" or "text-only" or "underline" or "transparent" or "mesh-gradient"
                or "animated-gradient" or "wave-gradient" or "pulse-glow" or "shimmer"
                or "aurora" or "neon" or "spotlight-sweep" or "breathing" => "",
                _ => patternSvg
            };

            // --- .header-content ---
            var contentFlex = layout switch
            {
                "split" => "display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 16px;",
                "banner" => "display: flex; flex-direction: column; align-items: center; gap: 8px;",
                "compact" => "display: flex; align-items: center; gap: 16px; justify-content: center;",
                "minimal-bar" => "display: flex; align-items: center; gap: 12px;",
                "two-row" => "display: flex; flex-direction: column; gap: 8px;",
                "card" => $"background: var(--card-bg); color: var(--text-color); border-radius: var(--border-radius); padding: 30px; box-shadow: 0 8px 32px rgba(0,0,0,0.15); max-width: 700px;",
                "scoreboard" => "display: grid; grid-template-columns: auto 1fr auto; align-items: center; gap: 20px;",
                "breathing" => "animation: headerBreathe 4s ease-in-out infinite;",
                _ => ""
            };

            // --- h1 ---
            var h1Size = layout switch
            {
                "compact" => "1.4rem",
                "minimal-bar" => "1.2rem",
                "banner" or "stadium" or "neon" or "aurora" => "3rem",
                "text-only" => "2.8rem",
                _ => "2.5rem"
            };
            var h1Margin = layout switch
            {
                "compact" or "minimal-bar" or "split" or "scoreboard" => "0",
                _ => "10px"
            };
            var h1Extra = layout switch
            {
                "banner" => "letter-spacing: 2px; text-transform: uppercase; font-weight: 800;",
                "stadium" => "letter-spacing: 3px; text-transform: uppercase; text-shadow: 0 0 40px rgba(255,255,255,0.3); font-weight: 800;",
                "text-only" => $"color: var(--primary-color); font-weight: 800;",
                "championship" => "letter-spacing: 1px; text-transform: uppercase; font-weight: 800;",
                "neon" => $"text-shadow: 0 0 10px var(--primary-color), 0 0 20px var(--primary-color), 0 0 40px var(--primary-color), 0 0 80px var(--secondary-color); animation: headerNeonPulse 2s ease-in-out infinite; letter-spacing: 3px; text-transform: uppercase; font-weight: 800;",
                "aurora" => "text-shadow: 0 0 30px rgba(255,255,255,0.4); letter-spacing: 2px; font-weight: 700;",
                "glass" => "text-shadow: 0 2px 4px rgba(0,0,0,0.3);",
                _ => "text-shadow: 0 1px 3px rgba(0,0,0,0.15);"
            };

            // --- subtitle ---
            var subSize = layout switch
            {
                "compact" or "minimal-bar" => "0.85rem",
                "banner" => "0.9rem",
                _ => "1.1rem"
            };
            var subExtra = layout switch
            {
                "compact" or "minimal-bar" => "margin: 0;",
                "banner" => "letter-spacing: 4px; text-transform: uppercase; font-weight: 500;",
                "stadium" => "letter-spacing: 2px; text-transform: uppercase; opacity: 0.7;",
                "text-only" => "color: var(--text-secondary); opacity: 1;",
                "transparent" or "underline" => "opacity: 1; color: var(--text-secondary);",
                _ => "text-shadow: 0 1px 2px rgba(0,0,0,0.1);"
            };

            // --- badge ---
            var badgePad = layout is "compact" or "minimal-bar" ? "3px 10px" : "6px 16px";
            var badgeMargin = layout is "compact" or "minimal-bar" or "split" or "scoreboard" ? "0" : "15px";
            var badgeSize = layout is "compact" or "minimal-bar" ? "0.75rem" : "0.9rem";
            var badgeBg = layout is "text-only" or "underline" or "transparent"
                ? "rgba(var(--primary-color-rgb, 59,130,246), 0.12)"
                : "rgba(255,255,255,0.18)";
            var badgeExtra = layout switch
            {
                "minimal-bar" => "margin-left: auto;",
                "scoreboard" => "justify-self: end;",
                _ => ""
            };

            // --- logo ---
            var logoExtra = layout switch
            {
                "compact" or "minimal-bar" => "max-height: 36px; margin: 0; filter: drop-shadow(0 1px 2px rgba(0,0,0,0.15));",
                "split" => "order: -1; margin: 0; filter: drop-shadow(0 2px 4px rgba(0,0,0,0.2));",
                "scoreboard" => "margin: 0; justify-self: start; filter: drop-shadow(0 2px 4px rgba(0,0,0,0.2));",
                "text-only" or "underline" or "transparent" => "margin-bottom: 15px;",
                _ => "margin-bottom: 15px; filter: drop-shadow(0 2px 6px rgba(0,0,0,0.25));"
            };

            // --- text-group (split layout) ---
            var textGroupCss = layout == "split" ? "text-align: right; flex: 1; min-width: 0;" : "min-width: 0;";

            // --- two-row specific ---
            var twoRowCss = layout == "two-row" ? @"
.header-row {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 16px;
}
.header-row:first-child {
    gap: 20px;
}" : "";

            // --- animations ---
            var animations = layout switch
            {
                "animated-gradient" => @"
@keyframes headerGradientShift {
    0% { background-position: 0% 50%; }
    50% { background-position: 100% 50%; }
    100% { background-position: 0% 50%; }
}",
                "wave-gradient" => @"
@keyframes headerWaveGradient {
    0% { background-position: 0% 50%; background-size: 300% 300%; }
    25% { background-size: 400% 400%; }
    50% { background-position: 100% 50%; background-size: 300% 300%; }
    75% { background-size: 200% 200%; }
    100% { background-position: 0% 50%; background-size: 300% 300%; }
}",
                "pulse-glow" => @"
@keyframes headerPulseGlow {
    0%, 100% { filter: brightness(1); box-shadow: 0 0 0 rgba(0,0,0,0); }
    50% { filter: brightness(1.15); box-shadow: 0 0 40px rgba(255,255,255,0.1); }
}",
                "shimmer" => @"
header::after {
    content: '';
    position: absolute;
    top: 0; left: -100%; width: 50%; height: 100%;
    background: linear-gradient(90deg, transparent, rgba(255,255,255,0.15), transparent);
    animation: headerShimmer 3s ease-in-out infinite;
    pointer-events: none;
}
@keyframes headerShimmer {
    0% { left: -100%; }
    100% { left: 200%; }
}",
                "aurora" => @"
header::before {
    content: '';
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    background: 
        linear-gradient(120deg, rgba(0,255,128,0.15) 0%, transparent 40%),
        linear-gradient(240deg, rgba(0,128,255,0.2) 0%, transparent 40%),
        linear-gradient(0deg, rgba(128,0,255,0.15) 0%, transparent 50%);
    animation: headerAurora 8s ease-in-out infinite alternate;
    pointer-events: none;
}
header { position: relative; }
@keyframes headerAurora {
    0% { opacity: 0.6; transform: translateX(-5%) scaleY(1); }
    33% { opacity: 1; transform: translateX(3%) scaleY(1.1); }
    66% { opacity: 0.8; transform: translateX(-2%) scaleY(0.95); }
    100% { opacity: 1; transform: translateX(5%) scaleY(1.05); }
}",
                "neon" => @"
@keyframes headerNeonPulse {
    0%, 100% { text-shadow: 0 0 10px var(--primary-color), 0 0 20px var(--primary-color), 0 0 40px var(--primary-color), 0 0 80px var(--secondary-color); }
    50% { text-shadow: 0 0 5px var(--primary-color), 0 0 10px var(--primary-color), 0 0 20px var(--primary-color), 0 0 40px var(--secondary-color); }
}
header::before {
    content: '';
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    background: radial-gradient(ellipse at 50% 50%, rgba(var(--primary-color-rgb, 59,130,246), 0.08) 0%, transparent 70%);
    pointer-events: none;
}
header { position: relative; }",
                "spotlight-sweep" => @"
header::after {
    content: '';
    position: absolute;
    top: -50%; left: -20%; width: 40%; height: 200%;
    background: radial-gradient(ellipse, rgba(255,255,255,0.12) 0%, transparent 70%);
    animation: headerSpotlight 5s ease-in-out infinite;
    pointer-events: none;
}
header { position: relative; }
@keyframes headerSpotlight {
    0% { left: -30%; }
    50% { left: 90%; }
    100% { left: -30%; }
}",
                "breathing" => @"
@keyframes headerBreathe {
    0%, 100% { transform: scale(1); opacity: 1; }
    50% { transform: scale(1.015); opacity: 0.95; }
}",
                _ => ""
            };

            return $@"
header {{
    background: {headerBg};
    color: {headerColor};
    padding: {headerPadding};
    text-align: {headerAlign};
    {headerPatternUse}
    {headerExtra}
}}

.header-content {{
    max-width: var(--max-content-width);
    margin: 0 auto;
    {contentFlex}
}}

header h1 {{
    font-family: var(--header-font);
    font-size: {h1Size};
    font-weight: 700;
    margin-bottom: {h1Margin};
    line-height: 1.15;
    letter-spacing: -0.02em;
    {h1Extra}
}}

header .subtitle {{
    font-size: {subSize};
    font-weight: 400;
    opacity: 0.85;
    line-height: 1.4;
    {subExtra}
}}

header .season-badge {{
    display: inline-block;
    background: {badgeBg};
    padding: {badgePad};
    border-radius: 20px;
    margin-top: {badgeMargin};
    font-size: {badgeSize};
    font-weight: 600;
    letter-spacing: 0.03em;
    backdrop-filter: blur(4px);
    -webkit-backdrop-filter: blur(4px);
    border: 1px solid rgba(255,255,255,0.1);
    {badgeExtra}
}}

header .logo {{
    max-width: 200px;
    height: auto;
    object-fit: contain;
    {logoExtra}
}}

.header-text-group {{
    {textGroupCss}
}}

.header-text-group h1 {{
    margin-bottom: 4px;
}}

.header-text-group .subtitle {{
    margin: 0;
}}

/* Logo position variants */
.header-content.header-logo-left,
.header-content.header-logo-right {{
    display: flex;
    align-items: center;
    gap: 24px;
}}
.header-content.header-logo-left {{ flex-direction: row; }}
.header-content.header-logo-right {{ flex-direction: row-reverse; }}
.header-content.header-logo-left .header-text-group,
.header-content.header-logo-right .header-text-group {{
    flex: 1;
    min-width: 0;
}}
.header-content.header-logo-above,
.header-content.header-logo-below {{
    display: flex;
    flex-direction: column;
    align-items: center;
}}
.header-content.header-logo-below .logo {{
    order: 10;
    margin-top: 16px;
    margin-bottom: 0;
}}
.header-content.header-logo-top-left,
.header-content.header-logo-top-right,
.header-content.header-logo-bottom-left,
.header-content.header-logo-bottom-right {{
    position: relative;
    min-height: 80px;
}}
.header-content.header-logo-top-left .logo {{ position: absolute; top: 0; left: 0; margin: 0; }}
.header-content.header-logo-top-right .logo {{ position: absolute; top: 0; right: 0; margin: 0; }}
.header-content.header-logo-bottom-left .logo {{ position: absolute; bottom: 0; left: 0; margin: 0; }}
.header-content.header-logo-bottom-right .logo {{ position: absolute; bottom: 0; right: 0; margin: 0; }}

/* Dual logo: logo on both sides of the title */
.header-content.header-dual-logo {{
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 24px;
}}
.header-content.header-dual-logo .header-text-group {{
    flex: 1;
    text-align: center;
    min-width: 0;
}}
.header-content.header-dual-logo .logo {{
    flex-shrink: 0;
    margin: 0;
}}

{twoRowCss}
{animations}";
        }

        private string GenerateStylesheet(WebsiteTemplate template)
        {
            var templateCSS = template.Id switch
            {
                "dark" => GenerateDarkModeCSS(),
                "sport" => GenerateSportCSS(),
                "minimalist" => GenerateMinimalistCSS(),
                "classic" => GenerateModernCSS(),
                "minimal" => GenerateModernCSS(),
                _ => GenerateModernCSS()
            };
            return templateCSS + GetCompetitionCSS() + GetCaptainsAreaCSS() + GetLiveScoresCSS();
        }

        private string GetLiveScoresCSS() => @"
/* ── Live Scores ──
   A studio panel inside the league's own page. The colours are set here rather
   than taken from the template: a scoreboard reads as a scoreboard whatever
   palette the league has picked, and white cards on a dark panel would not. */
.live-studio {
    --studio-bg: #0B1220;
    --studio-panel: #16213D;
    --studio-line: rgba(148,163,184,0.22);
    --studio-text: #E8EDF7;
    --studio-muted: #94A3B8;
    --studio-gold: #FBBF24;
    background: linear-gradient(160deg, #0B1220 0%, #16213D 100%);
    color: var(--studio-text);
    border-radius: var(--border-radius);
    padding: 22px 20px 26px;
    margin: 18px 0 28px;
    box-shadow: 0 18px 44px rgba(11,18,32,0.35);
}
.live-studio-head {
    display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between;
    gap: 12px; padding-bottom: 14px; border-bottom: 1px solid var(--studio-line);
}
.live-studio-title { display: flex; align-items: center; flex-wrap: wrap; gap: 12px; }
.live-studio-title h2 { margin: 0; color: #fff; font-size: 1.5rem; letter-spacing: -0.01em; }
.live-onair {
    display: inline-flex; align-items: center; gap: 7px;
    font-size: 0.68rem; font-weight: 800; text-transform: uppercase; letter-spacing: 0.12em;
    padding: 4px 10px; border-radius: 999px;
    background: rgba(220,38,38,0.16); color: #FCA5A5; border: 1px solid rgba(220,38,38,0.45);
}
.live-studio-meta { display: flex; align-items: center; gap: 14px; }
.live-count { font-size: 0.78rem; color: var(--studio-muted); text-transform: uppercase; letter-spacing: 0.08em; }
.live-clock {
    font-variant-numeric: tabular-nums; font-weight: 800; font-size: 1.05rem;
    color: var(--studio-gold); letter-spacing: 0.04em;
}
.live-dot {
    display: inline-block; width: 9px; height: 9px; border-radius: 50%;
    background: #DC2626; flex: 0 0 auto;
    animation: livePulse 1.6s ease-in-out infinite;
}
@keyframes livePulse {
    0%, 100% { opacity: 1; box-shadow: 0 0 0 0 rgba(220,38,38,0.6); }
    50% { opacity: 0.65; box-shadow: 0 0 0 7px rgba(220,38,38,0); }
}
.live-subtitle { color: var(--studio-muted); font-size: 0.86rem; margin: 12px 0 0; }
.live-status { color: var(--studio-muted); font-size: 0.85rem; margin: 14px 0 0; }
.live-board { display: grid; gap: 14px; grid-template-columns: 1fr; margin-top: 16px; }

.live-card {
    background: var(--studio-panel); border-radius: 12px;
    padding: 14px 16px 16px; border-left: 4px solid #DC2626;
    box-shadow: 0 6px 18px rgba(0,0,0,0.28);
}
.live-card-confirming { border-left-color: var(--studio-gold); }
.live-card-head { display: flex; justify-content: space-between; align-items: center; gap: 10px; margin-bottom: 12px; }
.live-badge {
    display: inline-flex; align-items: center; gap: 6px;
    font-size: 0.66rem; font-weight: 800; text-transform: uppercase; letter-spacing: 0.09em;
    padding: 3px 9px; border-radius: 999px; background: rgba(220,38,38,0.18); color: #FCA5A5;
}
.live-card-confirming .live-badge { background: rgba(251,191,36,0.16); color: #FCD34D; }
.live-division { font-size: 0.76rem; color: var(--studio-muted); }
.live-kind {
    font-size: 0.62rem; font-weight: 800; text-transform: uppercase; letter-spacing: 0.08em;
    padding: 2px 7px; border-radius: 999px; margin-left: 7px;
    border: 1px solid var(--studio-gold); color: var(--studio-gold);
}

.live-teams { display: grid; grid-template-columns: 1fr auto 1fr; align-items: center; gap: 12px; }
.live-team { font-weight: 700; color: #fff; font-size: 1.02rem; line-height: 1.25; }
.live-home { text-align: right; }
.live-away { text-align: left; }
.live-score { display: inline-flex; align-items: center; gap: 8px; white-space: nowrap; }
.live-score-n {
    font-variant-numeric: tabular-nums; font-size: 2rem; font-weight: 800; color: #fff;
    min-width: 1.5em; text-align: center; line-height: 1;
}
.live-score-sep { color: var(--studio-muted); font-size: 1.2rem; }

.live-progress { height: 4px; border-radius: 999px; background: rgba(148,163,184,0.18); margin: 12px 0 8px; overflow: hidden; }
.live-progress span { display: block; height: 100%; background: var(--studio-gold); border-radius: 999px; transition: width 0.4s ease; }
.live-meta { margin: 0; font-size: 0.78rem; color: var(--studio-muted); text-align: center; }

/* Frame-by-frame player results. */
.live-results { margin-top: 14px; padding-top: 12px; border-top: 1px solid var(--studio-line); }
.live-results-head {
    margin: 0 0 8px; font-size: 0.66rem; font-weight: 800; letter-spacing: 0.12em;
    text-transform: uppercase; color: var(--studio-muted);
}
.live-frames { list-style: none; margin: 0; padding: 0; }
.live-frame {
    display: grid; grid-template-columns: 34px 1fr 22px 1fr 20px;
    align-items: center; gap: 8px; padding: 4px 0; font-size: 0.85rem;
    color: var(--studio-muted); border-bottom: 1px solid rgba(148,163,184,0.10);
}
.live-frame:last-child { border-bottom: 0; }
.live-frame-no {
    font-size: 0.66rem; font-weight: 800; letter-spacing: 0.04em; color: var(--studio-muted);
    background: rgba(148,163,184,0.14); border-radius: 4px; padding: 2px 0; text-align: center;
}
.live-frame-home { text-align: right; }
.live-frame-away { text-align: left; }
.live-frame-mark { text-align: center; font-size: 0.62rem; color: var(--studio-gold); }
.live-frame-eight { text-align: center; }
.live-frame-eight abbr {
    display: inline-block; width: 16px; height: 16px; line-height: 16px; text-align: center;
    border-radius: 50%; background: #0B1220; color: #fff; border: 1px solid var(--studio-muted);
    font-size: 0.6rem; font-weight: 800; text-decoration: none; cursor: help;
}
/* The side that took the frame, named in full and lit. */
.live-frame-won-home .live-frame-home,
.live-frame-won-away .live-frame-away { font-weight: 700; color: #fff; }

/* ── The crawl along the foot of the screen ──
   The same line the captains see under their own card. Fixed to the viewport
   rather than the page, so it stays put while the page scrolls. */
.live-tick {
    position: fixed; left: 0; right: 0; bottom: 0; z-index: 40;
    height: 34px; display: flex; align-items: center; overflow: hidden;
    background: linear-gradient(90deg, rgba(11,18,32,0.97) 0%, rgba(22,33,61,0.97) 100%);
    border-top: 2px solid #FBBF24;
    box-shadow: 0 -6px 20px rgba(0,0,0,0.55);
}
.live-tick[hidden] { display: none; }
.tick-badge {
    flex: 0 0 auto; align-self: stretch; display: flex; align-items: center;
    padding: 0 10px; font-size: 0.62rem; font-weight: 800; letter-spacing: 0.06em;
    text-transform: uppercase; color: #0B1220; background: #FBBF24;
    font-variant-numeric: tabular-nums;
}
.tick-window { flex: 1 1 auto; overflow: hidden; }
/* Two identical runs, shifted by exactly half the track: at -50% the second run
   sits where the first began, so it repeats with no gap and no jump. */
.tick-track {
    display: inline-flex; white-space: nowrap; will-change: transform;
    animation: tickRun linear infinite;
    animation-duration: var(--tick-secs, 40s);
}
@keyframes tickRun {
    from { transform: translateX(0); }
    to   { transform: translateX(-50%); }
}
.tick-run { display: inline-flex; align-items: center; }
.tick-item {
    display: inline-flex; align-items: baseline; gap: 6px;
    padding: 0 18px; font-size: 0.82rem; color: #E8EDF7; white-space: nowrap;
}
.tick-item .sc {
    font-variant-numeric: tabular-nums; font-weight: 800; color: #fff;
    padding: 1px 7px; border-radius: 999px; background: rgba(59,130,246,0.30);
}
.tick-item .of { font-size: 0.68rem; color: #94A3B8; }
.tick-item .cup {
    font-size: 0.58rem; font-weight: 800; letter-spacing: 0.04em; padding: 1px 5px;
    border-radius: 999px; border: 1px solid #FBBF24; color: #FBBF24;
}
.tick-item .dot { color: #FBBF24; opacity: 0.6; padding: 0 2px; }
/* A frame has just gone in. It stays marked until the score changes again, so
   it is still lit when it next comes round - a flash on a timer would nearly
   always have expired before the line scrolled past. */
.tick-item.moved { color: #FBBF24; }
.tick-item.moved .sc { background: #FBBF24; color: #0B1220; }

/* Room for it, so the crawl never sits over the last thing on the page. */
body.has-tick { padding-bottom: 52px; }

@media (prefers-reduced-motion: reduce) {
    .tick-track { animation: none; }
    .tick-window { overflow-x: auto; }
    .live-dot { animation: none; }
}

@media (min-width: 900px) {
    .live-board { grid-template-columns: 1fr 1fr; }
}
@media (max-width: 520px) {
    .live-studio { padding: 16px 12px 20px; }
    .live-team { font-size: 0.9rem; }
    .live-score-n { font-size: 1.6rem; }
    .live-frame { grid-template-columns: 30px 1fr 18px 1fr 18px; font-size: 0.78rem; gap: 6px; }
}
";

        private string GetCaptainsAreaCSS() => @"
/* ── Captains Area ── */
.captain-login-card { max-width: 480px; margin: 40px auto; }
.captain-login-card form { display: flex; flex-direction: column; gap: 6px; margin-top: 16px; }
.captain-login-card label { font-size: 0.9rem; color: var(--text-color); }
.captain-login-card select,
.captain-login-card input[type=password] {
    padding: 12px 14px; font-size: 1rem; border: 1px solid #CBD5E1; border-radius: 8px;
    background: var(--card-bg); color: var(--text-color); width: 100%; box-sizing: border-box;
}
.captain-login-card select:focus,
.captain-login-card input:focus { outline: 2px solid var(--primary-color); outline-offset: 0; border-color: var(--primary-color); }
.captain-btn {
    margin-top: 14px; padding: 12px 18px; font-size: 1rem; font-weight: 600;
    background: var(--primary-color); color: #fff; border: 0; border-radius: 8px; cursor: pointer;
    transition: filter 0.15s;
}
.captain-btn:hover { filter: brightness(1.08); }
.captain-btn-secondary { background: #64748B; }
.captain-error { color: #B91C1C; background: #FEE2E2; padding: 10px 12px; border-radius: 6px; margin-top: 12px; font-size: 0.9rem; }
.captain-welcome { color: var(--text-secondary, #475569); margin-bottom: 8px; }
.captain-hint { color: #64748B; font-size: 0.85rem; margin-top: 12px; }

.captain-header-row { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
.captain-grid { display: grid; gap: 16px; grid-template-columns: 1fr; }
.captain-action-list { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 8px; }
.captain-action {
    display: inline-flex; align-items: center; gap: 6px;
    padding: 10px 14px; border-radius: 8px; background: var(--primary-color); color: #fff;
    text-decoration: none; font-weight: 600; font-size: 0.9rem;
}
.captain-action:hover { filter: brightness(1.1); color: #fff; }
@media (min-width: 900px) {
    .captain-grid { grid-template-columns: 1fr 1fr; }
    .captain-actions { grid-column: 1 / -1; }
}

/* Sub-tabs inside captain dashboard */
.captain-tabs {
    display: flex; flex-wrap: wrap; gap: 6px; margin: 8px 0 16px;
    border-bottom: 2px solid #E2E8F0; padding-bottom: 0;
}
.captain-tab-btn {
    background: transparent; border: 0; padding: 10px 16px; cursor: pointer;
    font-size: 0.95rem; font-weight: 600; color: #64748B;
    border-bottom: 3px solid transparent; margin-bottom: -2px;
    border-radius: 6px 6px 0 0; transition: color 0.15s, border-color 0.15s, background 0.15s;
}
.captain-tab-btn:hover { color: var(--primary-color); background: rgba(59,130,246,0.06); }
.captain-tab-btn.active {
    color: var(--primary-color);
    border-bottom-color: var(--primary-color);
    background: rgba(59,130,246,0.08);
}
.captain-tab-panels { display: block; }
.captain-tab-panel { display: none; }
.captain-tab-panel.active { display: block; }

.captain-update-form { display: grid; gap: 10px; margin-top: 8px; }
.captain-update-form label { display: block; font-size: 0.85rem; color: var(--text-color); font-weight: 600; }
.captain-update-form input,
.captain-update-form textarea {
    width: 100%; padding: 10px 12px; font-size: 0.95rem; box-sizing: border-box;
    border: 1px solid #CBD5E1; border-radius: 8px; background: var(--card-bg); color: var(--text-color);
    margin-top: 4px; font-family: inherit;
}
.captain-update-form input:focus,
.captain-update-form textarea:focus { outline: 2px solid var(--primary-color); outline-offset: 0; border-color: var(--primary-color); }
.captain-update-form textarea { resize: vertical; min-height: 60px; }
.captain-update-actions { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; margin-top: 4px; }
.captain-btn-primary { background: var(--primary-color); }
.captain-update-status { font-size: 0.85rem; color: #64748B; }
.captain-update-status.ok { color: #047857; }
.captain-update-status.err { color: #B91C1C; }
";
        
        private string GetTableClasses()
        {
            var classes = new List<string> { "data-table" };
            if (_settings.TableStriped) classes.Add("striped");
            if (_settings.TableHoverable) classes.Add("hoverable");
            if (_settings.TableBordered) classes.Add("bordered");
            if (_settings.TableCompact) classes.Add("compact");
            return string.Join(" ", classes);
        }
        
        private string GenerateModernCSS()
        {
            var _btnBaseStyle = _settings.ButtonStyle switch
            {
                "filled" => "background: var(--primary-color); color: white; border: 2px solid var(--primary-color);",
                "outline" => "background: transparent; color: var(--primary-color); border: 2px solid var(--primary-color);",
                "ghost" => "background: transparent; color: var(--primary-color); border: 2px solid transparent;",
                _ => "background: var(--primary-color); color: white; border: 2px solid var(--primary-color);"
            };
            
            var _btnHoverStyle = _settings.ButtonStyle switch
            {
                "filled" => "opacity: 0.9;",
                "outline" => "background: var(--primary-color); color: white;",
                "ghost" => "background: rgba(59,130,246,0.1);",
                _ => "opacity: 0.9;"
            };
            
            return $@"
/* Modern CSS Variables */
:root {{
    --primary-color: {_settings.PrimaryColor};
    --secondary-color: {_settings.SecondaryColor};
    --accent-color: {_settings.AccentColor};
    --background-color: {_settings.BackgroundColor};
    --text-color: {_settings.TextColor};
    --text-secondary: {_settings.TextSecondaryColor};
    --border-color: #e5e7eb;
    --card-bg: {_settings.CardBackgroundColor};
    --header-bg: {_settings.PrimaryColor};
    --header-text: {_settings.HeaderTextColor};
    --header-font: '{_settings.HeaderFontFamily}', 'Segoe UI', sans-serif;
    --nav-bg: {_settings.SecondaryColor};
    --nav-text: {_settings.HeaderTextColor};
    --nav-hover: {_settings.AccentColor};
    --footer-bg: #1F2937;
    --footer-text: #F9FAFB;
    --table-header-bg: {_settings.PrimaryColor};
    --table-header-text: {_settings.HeaderTextColor};
    --table-alt-bg: #F9FAFB;
    --font-family: '{_settings.FontFamily}', 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
    --font-family-emoji: 'Segoe UI Emoji', 'Segoe UI Symbol', 'Apple Color Emoji', 'Noto Color Emoji', 'Twemoji Mozilla', sans-serif;
    --border-radius: {_settings.BorderRadius}px;
    --shadow: 0 2px 4px rgba(0,0,0,0.1);
    --shadow-lg: 0 4px 6px rgba(0,0,0,0.1);
    --max-content-width: {_settings.MaxContentWidth}px;
    --spacing: {_settings.SectionSpacing}px;
    --card-spacing: {_settings.CardSpacing}px;
    --transition: all 0.2s ease;
}}

/* Emoji Support - ensure emojis display correctly */
h1, h2, h3, h4, h5, h6, p, span, div, a, li {{
    font-family: var(--font-family), var(--font-family-emoji);
}}

* {{ box-sizing: border-box; margin: 0; padding: 0; }}

html {{ overflow-x: hidden; }}

img {{ max-width: 100%; height: auto; }}

body {{
    font-family: var(--font-family), var(--font-family-emoji);
    font-size: {_settings.BaseFontSize}px;
    background: var(--background-color);
    color: var(--text-color);
    line-height: 1.6;
    display: flex;
    flex-direction: column;
    min-height: 100vh;
}}

.container {{
    max-width: var(--max-content-width);
    margin: 0 auto;
    padding: 0 20px;
}}

{GenerateHeaderLayoutCSS()}

/* Header freeform sub-element layout */
.header-freeform {{
    position: relative;
    min-height: 120px;
}}

.header-content > [data-block-id] {{
    width: fit-content;
}}

.header-freeform > [data-block-id] {{
    cursor: default;
}}

nav {{
    background: var(--card-bg);
    border-bottom: 1px solid rgba(0,0,0,0.1);
    {(_settings.NavSticky ? "position: sticky; top: 0; z-index: 100;" : "")}
}}

nav .nav-container {{
    display: flex;
    justify-content: {_settings.NavPosition};
    align-items: center;
    flex-wrap: wrap;
    gap: 8px;
    padding: 15px 20px;
    max-width: var(--max-content-width);
    margin: 0 auto;
}}

nav a {{
    color: var(--text-color);
    text-decoration: none;
    padding: 8px 16px;
    border-radius: {(_settings.NavStyle == "pills" ? "20px" : _settings.NavStyle == "buttons" ? "8px" : "0")};
    transition: all 0.2s;
    line-height: 1.2;
    display: inline-flex;
    align-items: center;
    {(_settings.NavStyle == "underline" ? "border-bottom: 2px solid transparent;" : "")}
}}

nav a:hover, nav a.active {{
    background: var(--primary-color);
    color: white;
    {(_settings.NavStyle == "underline" ? "background: transparent; color: var(--primary-color); border-bottom-color: var(--primary-color);" : "")}
}}

.nav-dropdown {{
    position: relative;
    display: inline-flex;
    align-items: center;
}}
.nav-dropdown > a {{ display: inline-flex; align-items: center; }}
.nav-dropdown-menu {{
    position: absolute;
    top: 100%;
    left: 0;
    min-width: 200px;
    background: var(--card-bg);
    border: 1px solid rgba(0,0,0,0.1);
    border-radius: 6px;
    box-shadow: 0 8px 24px rgba(0,0,0,0.15);
    z-index: 200;
    padding: 6px 0;
    opacity: 0;
    visibility: hidden;
    transform: translateY(-6px);
    transition: opacity 0.25s ease, transform 0.25s ease, visibility 0s linear 0.25s;
    pointer-events: none;
}}
.nav-dropdown:hover .nav-dropdown-menu,
.nav-dropdown:focus-within .nav-dropdown-menu {{
    opacity: 1;
    visibility: visible;
    transform: translateY(0);
    pointer-events: auto;
    transition: opacity 0.25s ease, transform 0.25s ease, visibility 0s linear 0s;
}}
.nav-dropdown-menu a {{
    display: block;
    padding: 8px 14px;
    border-radius: 0;
    color: var(--text-color);
    text-decoration: none;
    white-space: nowrap;
    transition: background 0.2s ease, color 0.2s ease;
}}
.nav-dropdown-menu a:hover {{ background: var(--primary-color); color: white; }}

.content-area {{
    padding: var(--spacing) 0;
    flex: 1;
}}

/* Freeform canvas layout for home page */
.page-canvas {{
    position: relative;
    overflow: visible;
    flex: 1;
}}

.page-canvas > [data-block-id] {{
    position: absolute;
    box-sizing: border-box;
}}

/* On non-home pages, blocks stay in normal flow */
.page-canvas > header[data-block-id],
.page-canvas > nav[data-block-id],
.page-canvas > footer[data-block-id],
.page-canvas > .content-area[data-block-id] {{
    position: relative;
    width: 100%;
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
    overflow: hidden;
    {(_settings.EnableShadows ? "box-shadow: 0 4px 6px rgba(0,0,0,0.07);" : "")}
}}

.section h3 {{
    font-family: var(--header-font);
    margin-bottom: 20px;
    color: var(--text-color);
}}

/* Two-column layout for half-width blocks */
.two-col-row {{
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: var(--spacing);
    margin-bottom: var(--spacing);
}}

.two-col-row .section {{
    margin-bottom: 0;
}}

.col-half {{
    min-width: 0;
}}

.stats-grid {{
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(min(180px, 100%), 1fr));
    gap: clamp(10px, 2vw, 20px);
    margin-bottom: var(--spacing);
}}

.stat-card {{
    background: var(--card-bg);
    border-radius: var(--border-radius);
    padding: clamp(16px, 4vw, 24px);
    text-align: center;
    {(_settings.EnableShadows ? "box-shadow: 0 2px 4px rgba(0,0,0,0.05);" : "")}
    {(_settings.CardShowTopAccent ? "border-top: 3px solid var(--primary-color);" : "")}
}}

.stat-number {{
    font-size: clamp(1.5rem, 5vw, 2.5rem);
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
    padding: clamp(6px, 1.5vw, 12px);
    text-align: left;
    border-bottom: 1px solid rgba(0,0,0,0.08);
}}

.data-table th {{
    background: linear-gradient(135deg, var(--primary-color) 0%, var(--secondary-color) 100%);
    color: white;
    font-weight: 600;
}}

.data-table.striped tbody tr:nth-child(even) {{
    background: rgba(0,0,0,0.02);
}}

.data-table.hoverable tbody tr:hover {{
    background: rgba(0,0,0,0.04);
}}

.data-table.bordered td, .data-table.bordered th {{
    border: 1px solid rgba(0,0,0,0.1);
}}

.data-table.compact th, .data-table.compact td {{
    padding: 8px;
}}

.table-responsive {{
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
    max-width: 100%;
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
    display: grid;
    grid-template-columns: minmax(60px, 100px) minmax(0, 1fr) auto minmax(0, 1fr) minmax(60px, 140px);
    align-items: center;
    padding: clamp(10px, 2vw, 16px);
    background: rgba(0,0,0,0.02);
    border-radius: calc(var(--border-radius) / 2);
    gap: clamp(6px, 1.5vw, 12px);
}}

/* When no venue - 4 columns */
.result-item:not(:has(.venue)), .fixture-item:not(:has(.venue)) {{
    grid-template-columns: minmax(60px, 100px) minmax(0, 1fr) auto minmax(0, 1fr);
}}

/* When no date - adjust first column */
.result-item:not(:has(.date)), .fixture-item:not(:has(.date)) {{
    grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr) minmax(60px, 140px);
}}

/* When no date and no venue */
.result-item:not(:has(.date)):not(:has(.venue)), .fixture-item:not(:has(.date)):not(:has(.venue)) {{
    grid-template-columns: minmax(0, 1fr) auto minmax(0, 1fr);
}}

.result-item .date, .fixture-item .date {{
    text-align: left;
    color: var(--text-secondary);
    font-size: 0.85rem;
}}

.result-item .team, .fixture-item .team {{
    font-weight: 600;
    text-align: center;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}}

.result-item .team.winner {{
    color: var(--primary-color);
}}

.result-item .score {{
    font-weight: bold;
    font-size: 1.2rem;
    text-align: center;
    padding: 0 8px;
    white-space: nowrap;
}}

.fixture-item .vs {{
    color: var(--text-secondary);
    text-align: center;
    padding: 0 6px;
    white-space: nowrap;
}}

.fixture-item .venue, .result-item .venue {{
    color: var(--text-secondary);
    font-size: 0.9rem;
    text-align: right;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}}

.date {{
    color: var(--text-secondary);
    font-size: 0.85rem;
    white-space: nowrap;
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

.scorecard {{
    background: rgba(0,0,0,0.02);
    border-radius: calc(var(--border-radius) / 2);
    margin-bottom: 12px;
    overflow: hidden;
}}

.scorecard[open] {{
    background: rgba(0,0,0,0.04);
}}

.scorecard-summary {{
    display: flex;
    align-items: center;
    flex-wrap: wrap;
    gap: clamp(6px, 1.5vw, 12px);
    padding: clamp(10px, 2vw, 16px);
    cursor: pointer;
    font-weight: 600;
}}

.scorecard-date {{
    color: var(--text-secondary);
    font-size: 0.85rem;
    white-space: nowrap;
}}

.scorecard-teams {{
    flex: 1;
}}

.scorecard-teams strong {{
    color: var(--primary-color);
    padding: 0 6px;
    white-space: nowrap;
}}

.scorecard-table {{
    margin: 0 clamp(10px, 2vw, 16px) clamp(10px, 2vw, 16px);
    width: calc(100% - clamp(20px, 4vw, 32px));
}}

.scorecard-table .frame-winner {{
    font-weight: 700;
    color: var(--primary-color);
}}

.scorecard-table .frame-result {{
    text-align: center;
    color: var(--text-secondary);
    white-space: nowrap;
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

.rows-report {{
    position: relative;
    border-left: 4px solid var(--primary-color);
}}

.report-header {{
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 8px;
}}

.week-badge {{
    background: var(--primary-color);
    color: white;
    padding: 4px 12px;
    border-radius: 16px;
    font-size: 0.85rem;
    font-weight: 600;
}}

.report-author {{
    color: var(--text-secondary);
    font-size: 0.85rem;
    font-style: italic;
}}

.report-meta {{
    color: var(--text-secondary);
    margin-bottom: 12px;
    font-size: 0.9rem;
}}

.report-summary {{
    font-size: 1.05rem;
    color: var(--text-color);
    margin-bottom: 16px;
    line-height: 1.6;
    font-weight: 500;
}}

.report-results {{
    background: var(--bg-color);
    border-radius: var(--border-radius);
    padding: 16px;
    margin: 16px 0;
}}

.report-results h4 {{
    margin: 0 0 12px 0;
    font-size: 0.95rem;
    color: var(--text-secondary);
    text-transform: uppercase;
    letter-spacing: 0.5px;
}}

.report-result-row {{
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 12px;
    padding: 8px 0;
    border-bottom: 1px solid rgba(0,0,0,0.06);
}}

.report-result-row:last-child {{
    border-bottom: none;
}}

.report-result-row .team-name {{
    flex: 1;
    font-size: 0.95rem;
}}

.report-result-row .team-name:first-child {{
    text-align: right;
}}

.report-result-row .team-name.winner {{
    font-weight: 700;
    color: var(--primary-color);
}}

.result-score {{
    background: var(--primary-color);
    color: white;
    padding: 4px 12px;
    border-radius: 8px;
    font-weight: 700;
    font-size: 0.9rem;
    min-width: 60px;
    text-align: center;
}}

.report-content {{
    margin-top: 16px;
    line-height: 1.8;
    color: var(--text-color);
}}

.report-tags {{
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    margin-top: 16px;
}}

.report-tag {{
    background: rgba(0,0,0,0.05);
    color: var(--text-secondary);
    padding: 4px 10px;
    border-radius: 12px;
    font-size: 0.8rem;
}}

.division-badge {{
    background: rgba(0,0,0,0.1);
    padding: 4px 10px;
    border-radius: 12px;
    font-size: 0.8rem;
}}

/* Rules page tabs */
.rules-tabs {{
    margin-top: 20px;
}}

/* Rules page search */
.rules-search {{
    display: flex;
    align-items: center;
    gap: 12px;
    margin-top: 20px;
}}
.rules-search-input {{
    flex: 1;
    max-width: 420px;
    padding: 10px 14px;
    font-size: 0.95rem;
    border: 1px solid rgba(0,0,0,0.15);
    border-radius: 8px;
    background: var(--card-bg);
    color: var(--text-color);
    font-family: inherit;
    transition: border-color 0.2s, box-shadow 0.2s;
}}
.rules-search-input:focus {{
    outline: none;
    border-color: var(--primary-color);
    box-shadow: 0 0 0 3px rgba(59,130,246,0.15);
}}
.rules-search-summary {{
    font-size: 0.85rem;
    color: var(--text-secondary);
}}
.rules-mark {{
    background: #FEF08A;
    color: inherit;
    padding: 0 2px;
    border-radius: 3px;
}}
.rules-search-hidden {{ display: none !important; }}
.rules-tab-btn.rules-search-has-hits::after {{
    content: '';
    display: inline-block;
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: var(--primary-color);
    margin-left: 6px;
    vertical-align: middle;
}}

.rules-tab-buttons {{
    display: flex;
    gap: 0;
    border-bottom: 2px solid rgba(0,0,0,0.1);
    margin-bottom: 24px;
    flex-wrap: wrap;
}}

.rules-tab-btn {{
    padding: 12px 24px;
    border: none;
    background: none;
    cursor: pointer;
    font-size: 1rem;
    font-weight: 600;
    color: var(--text-secondary);
    border-bottom: 3px solid transparent;
    margin-bottom: -2px;
    transition: color 0.2s, border-color 0.2s;
    font-family: inherit;
}}

.rules-tab-btn:hover {{
    color: var(--primary-color);
}}

.rules-tab-btn.active {{
    color: var(--primary-color);
    border-bottom-color: var(--primary-color);
}}

/* Rules layout: sidebar TOC + content body */
.rules-layout {{
    display: grid;
    grid-template-columns: 240px 1fr;
    gap: 32px;
    align-items: start;
}}

@media (max-width: 768px) {{
    .rules-layout {{
        grid-template-columns: 1fr;
        gap: 16px;
    }}
    .rules-toc {{
        position: static !important;
    }}
}}

.rules-toc {{
    position: sticky;
    top: 100px;
    background: rgba(0,0,0,0.02);
    border: 1px solid rgba(0,0,0,0.06);
    border-radius: 12px;
    padding: 20px;
}}

.rules-toc h4 {{
    font-size: 0.85rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: var(--text-secondary);
    margin-bottom: 12px;
    padding-bottom: 8px;
    border-bottom: 1px solid rgba(0,0,0,0.08);
}}

.rules-toc ul {{
    list-style: none;
    padding: 0;
    margin: 0;
}}

.rules-toc li {{
    margin-bottom: 4px;
}}

.rules-toc a {{
    display: block;
    padding: 6px 10px;
    border-radius: 6px;
    text-decoration: none;
    color: var(--text-secondary);
    font-size: 0.88rem;
    line-height: 1.4;
    transition: background 0.2s, color 0.2s;
}}

.rules-toc a:hover {{
    background: rgba(var(--primary-rgb, 59,130,246), 0.1);
    color: var(--primary-color);
}}

.rules-body {{
    line-height: 1.8;
}}

.rules-body h3 {{
    margin-top: 32px;
    margin-bottom: 12px;
    padding-top: 16px;
    color: var(--text-color);
    font-size: 1.15rem;
    border-top: 1px solid rgba(0,0,0,0.06);
}}

.rules-body h3:first-child {{
    margin-top: 0;
    padding-top: 0;
    border-top: none;
}}

.rules-body p {{
    margin-bottom: 8px;
    color: var(--text-color);
}}

.rules-body ul {{
    margin-bottom: 16px;
    padding-left: 24px;
}}

.rules-body li {{
    margin-bottom: 6px;
}}

/* Rack diagram */
.rack-diagram {{
    text-align: center;
    margin: 24px 0;
    padding: 24px;
    background: rgba(0,0,0,0.02);
    border: 1px solid rgba(0,0,0,0.06);
    border-radius: 12px;
}}

.rack-diagram img {{
    max-width: 400px;
    width: 100%;
    height: auto;
    margin: 0 auto;
    display: block;
}}

/* Entry Forms */
.entry-form-directory {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 260px), 1fr)); gap: 16px; max-width: 1100px; margin: 24px auto 40px; }}
.entry-form-directory-link {{ display: flex; flex-direction: column; align-items: flex-start; gap: 12px; padding: 24px; border-radius: 20px; border: 1px solid color-mix(in srgb, var(--text-color) 18%, transparent); background: var(--card-bg); color: var(--text-color); text-decoration: none; overflow-wrap: anywhere; box-shadow: 0 8px 24px #0f172a08; }}
.entry-form-directory-link strong {{ font-size: 1.2rem; }}
.entry-form-directory-link > span:not(.entry-form-badge) {{ font-size: .9rem; }}
.entry-form-directory-link:hover, .entry-form-directory-link:focus-visible {{ border-color: var(--primary-color); outline: 2px solid var(--primary-color); outline-offset: 3px; }}
.entry-form-directory-action {{ margin-top: auto; padding-top: 8px; font-weight: 700; color: var(--primary-color); }}
.entry-form-card {{
    max-width: 960px;
    margin: 24px auto;
    padding: clamp(20px, 4vw, 40px);
    border: 1px solid color-mix(in srgb, var(--text-color) 16%, transparent);
    border-top: 5px solid var(--primary-color);
    border-radius: 24px;
    box-shadow: 0 12px 36px rgba(15, 23, 42, 0.07);
    overflow-wrap: anywhere;
    scroll-margin-top: 100px;
}}

.entry-form-intro {{ padding: 24px; border-radius: 18px; background: linear-gradient(125deg, color-mix(in srgb, var(--primary-color) 12%, var(--card-bg)), var(--card-bg)); border: 1px solid color-mix(in srgb, var(--primary-color) 18%, transparent); }}
.entry-form-eyebrow {{ color: var(--primary-color); font-size: .78rem; font-weight: 800; letter-spacing: .12em; text-transform: uppercase; margin-bottom: 12px; }}
.entry-form-steps {{ display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 8px; list-style: none; padding: 0; margin: 8px 0; }}
.entry-form-steps li {{ display: flex; align-items: center; gap: 8px; padding: 12px; border: 1px solid color-mix(in srgb, var(--text-color) 18%, transparent); border-radius: 12px; font-size: .85rem; font-weight: 600; color: var(--text-secondary); }}
.entry-form-steps li span {{ font-weight: 800; font-size: .75rem; }}
.entry-form-steps li[aria-current=step] {{ border-color: var(--primary-color); color: var(--text-color); background: color-mix(in srgb, var(--primary-color) 14%, var(--card-bg)); box-shadow: inset 0 -3px var(--primary-color); }}
.entry-form-steps li.is-complete {{ border-color: #15803D; color: var(--text-color); }}
.entry-form-completion {{ display: grid; gap: 8px; color: var(--text-secondary); font-size: .85rem; }}
.entry-form-completion progress {{ width: 100%; height: 8px; border: 0; border-radius: 20px; overflow: hidden; accent-color: var(--primary-color); }}
.entry-form-completion progress::-webkit-progress-bar {{ background: color-mix(in srgb, var(--text-color) 12%, var(--card-bg)); border-radius: 20px; }}
.entry-form-completion progress::-webkit-progress-value {{ background: var(--primary-color); border-radius: 20px; }}
.entry-form-contact {{ margin-top: 24px; padding-top: 20px; border-top: 1px solid color-mix(in srgb, var(--text-color) 18%, transparent); color: var(--text-secondary); }}
.entry-form-contact p {{ margin: 6px 0; }}
.entry-form-card .form-group input, .entry-form-card .form-group select, .entry-form-card .form-group textarea {{ border-color: color-mix(in srgb, var(--text-color) 24%, transparent); border-radius: 12px; }}
.entry-form-fields {{ display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 24px; border: 0; padding: 0; margin: 0; min-width: 0; }}
.entry-form-fields .form-group {{ min-width: 0; }}
.entry-form-wide {{ grid-column: 1 / -1; }}
.entry-form-hint, .entry-form-optional {{ font-size: .85rem; color: var(--text-secondary); font-weight: 400; }}
.entry-form-sr-only {{ position: absolute; width: 1px; height: 1px; padding: 0; overflow: hidden; clip-path: inset(50%); white-space: nowrap; }}
.entry-form-notice {{ border: 1px solid #D97706; border-radius: 12px; padding: 16px; background: #FFFBEB; color: #78350F; line-height: 1.6; }}
.entry-form-actions {{ display: flex; gap: 12px; flex-wrap: wrap; align-items: center; padding-top: 12px; }}
.entry-form-secondary {{ background: var(--card-bg); color: var(--text-color); border: 1px solid currentColor; border-radius: 12px; padding: 12px 20px; font: inherit; font-weight: 600; cursor: pointer; min-height: 48px; }}
.entry-form-card [hidden] {{ display: none !important; }}
.entry-form-review {{ background: color-mix(in srgb, var(--primary-color) 5%, var(--card-bg)); border: 1px solid color-mix(in srgb, var(--text-color) 18%, transparent); border-radius: 16px; padding: 24px; }}
.entry-form-review h4 {{ font-size: 1.25rem; margin: 0 0 8px; }}
.entry-form-review dl {{ display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 2fr); gap: 12px 24px; }}
.entry-form-review dt {{ font-weight: 600; }}
.entry-form-review dd {{ margin: 0; white-space: pre-wrap; }}
.entry-form-feedback:not(:empty) {{ padding: 16px; border-radius: 12px; background: #F0FDF4; border: 1px solid #15803D; color: #14532D; line-height: 1.6; }}
.entry-form-feedback.is-error {{ background: #FEF2F2; border-color: #B91C1C; color: #991B1B; }}
.entry-form-card :focus-visible {{ outline: 3px solid var(--primary-color); outline-offset: 4px; }}
.entry-form-card button:disabled {{ cursor: wait; opacity: .65; transform: none; }}
.entry-form-fields input:not([type=checkbox]), .entry-form-fields select, .entry-form-fields textarea {{ width: 100%; box-sizing: border-box; min-height: 48px; font-size: 1rem; }}
.entry-form-fields input:user-invalid, .entry-form-fields select:user-invalid, .entry-form-fields textarea:user-invalid {{ border-color: #B91C1C; }}
@media (max-width: 640px) {{
    .entry-form-steps li {{ flex-direction: column; align-items: flex-start; padding: 10px; }}
    .entry-form-intro {{ padding: 16px; }}
    .entry-form-fields, .entry-form-review dl {{ grid-template-columns: minmax(0, 1fr); }}
    .entry-form-review dd {{ padding-bottom: 12px; }}
    .entry-form-card {{ border-radius: 16px; }}
    .entry-form-actions button {{ flex: 1 1 180px; }}
}}
@media (prefers-reduced-motion: reduce) {{
    .entry-form-card *, .entry-form-card *:hover {{ transition: none; transform: none; }}
}}

.entry-form-logo {{
    text-align: center;
    margin-bottom: 16px;
}}

.entry-form-logo img {{
    max-height: 100px;
    max-width: 280px;
    height: auto;
    width: auto;
}}

.entry-form-header {{
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    margin-bottom: 8px;
    flex-wrap: wrap;
}}

.entry-form-header h3 {{
    margin: 0;
    font-size: clamp(1.5rem, 3vw, 2rem);
    letter-spacing: -.03em;
}}

.entry-form-badge {{
    display: inline-block;
    padding: 4px 14px;
    border-radius: 16px;
    font-size: 0.8rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.5px;
}}

.entry-form-badge.open {{
    background: #D1FAE5;
    color: #065F46;
}}

.entry-form-badge.closed {{
    background: #FEE2E2;
    color: #991B1B;
}}

.entry-form-desc {{
    color: var(--text-secondary);
    margin-bottom: 12px;
    line-height: 1.6;
    white-space: pre-wrap;
}}

.entry-form-deadline {{
    color: var(--text-secondary);
    font-size: 0.9rem;
    margin-bottom: 16px;
}}

.entry-form-closed-msg {{
    background: #FEF2F2;
    border: 1px solid #FECACA;
    border-radius: var(--border-radius);
    padding: 16px;
    text-align: center;
    color: #991B1B;
}}

.entry-forms-list {{
    display: flex;
    flex-direction: column;
    gap: 12px;
    margin-bottom: 16px;
}}

.featured-page-card {{
    padding: 0 !important;
}}

.featured-page-link {{
    display: flex;
    align-items: center;
    gap: 20px;
    padding: 24px 28px;
    text-decoration: none;
    color: inherit;
    transition: background 0.2s;
    border-radius: var(--border-radius);
}}

.featured-page-link:hover {{
    background: rgba(0,0,0,0.03);
}}

.featured-page-icon {{
    font-size: 2rem;
    flex-shrink: 0;
}}

.featured-page-info h3 {{
    margin: 0 0 4px 0;
    font-size: 1.1rem;
}}

.featured-page-info p {{
    margin: 0;
    color: var(--text-secondary);
    font-size: 0.9rem;
}}

.featured-page-arrow {{
    margin-left: auto;
    font-size: 1.3rem;
    color: var(--primary-color);
    flex-shrink: 0;
}}

.entry-form {{
    display: flex;
    flex-direction: column;
    gap: 16px;
    margin-top: 16px;
}}

.form-group {{
    display: flex;
    flex-direction: column;
    gap: 6px;
}}

.form-group label {{
    font-weight: 600;
    font-size: 0.9rem;
    color: var(--text-color);
}}

.form-group .required {{
    color: #EF4444;
}}

.form-group input,
.form-group textarea,
.form-group select {{
    padding: 10px 14px;
    border: 1px solid rgba(0,0,0,0.15);
    border-radius: 8px;
    font-size: 0.95rem;
    font-family: inherit;
    min-height: 48px;
    background: var(--card-bg);
    color: var(--text-color);
    transition: border-color 0.2s, box-shadow 0.2s;
}}

.form-group input:focus,
.form-group textarea:focus,
.form-group select:focus {{
    outline: none;
    border-color: var(--primary-color);
    box-shadow: 0 0 0 3px rgba(var(--primary-rgb, 59,130,246), 0.15);
}}

.form-group textarea {{
    resize: vertical;
    min-height: 80px;
}}

.checkbox-label {{
    display: flex;
    align-items: center;
    gap: 8px;
    font-weight: 400;
    cursor: pointer;
}}

.checkbox-label input[type=""checkbox""] {{
    width: 18px;
    height: 18px;
    accent-color: var(--primary-color);
}}

.entry-form-submit {{
    background: var(--primary-color);
    color: white;
    border: none;
    padding: 12px 28px;
    border-radius: {(_settings.ButtonRounded ? "20px" : "var(--border-radius)")};
    font-size: 1rem;
    font-weight: 600;
    cursor: pointer;
    transition: var(--transition);
    align-self: flex-start;
    font-family: inherit;
}}

.entry-form-submit:hover {{
    opacity: 0.9;
    transform: translateY(-1px);
}}

.entry-form-confirmation {{
    background: #F0FDF4;
    border: 1px solid #BBF7D0;
    border-radius: var(--border-radius);
    padding: 20px;
    text-align: center;
    color: #065F46;
    font-size: 1.05rem;
}}

.entry-form-field-info {{
    color: var(--text-secondary, #64748B);
    font-size: 0.9rem;
    margin: 4px 0 0 0;
    font-style: italic;
}}

.entry-form-contact {{
    background: #EFF6FF;
    border: 1px solid #BFDBFE;
    border-radius: var(--border-radius);
    padding: 16px 20px;
    margin: 16px 0;
}}

.entry-form-contact p {{
    margin: 4px 0;
    color: #1E40AF;
}}

.entry-form-contact a {{
    color: #2563EB;
    font-weight: 600;
}}

@media print {{
    .entry-form-submit {{ display: none; }}
    .entry-form-contact {{ border: 1px solid #999; }}
}}

@media (max-width: 768px) {{
    .entry-form-header {{
        flex-direction: column;
        align-items: flex-start;
    }}
}}

footer {{
    background: #1E293B;
    color: #E2E8F0;
    padding: 40px 20px;
    margin-top: auto;
}}

.footer-content {{
    max-width: var(--max-content-width);
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

.footer-notes {{
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    gap: 12px;
    margin: 16px 0;
}}

.footer-note {{
    background: rgba(255,255,255,0.06);
    border: 1px solid rgba(255,255,255,0.1);
    border-radius: var(--border-radius);
    padding: 10px 18px;
    font-size: 0.85rem;
    color: #CBD5E1;
    margin: 0;
}}

.copyright, .powered-by, .last-updated {{
    font-size: 0.85rem;
    color: #94A3B8;
    margin-top: 10px;
}}

/* Button Styling */
.btn {{
    display: inline-block;
    padding: {(_settings.ButtonRounded ? "10px 24px" : "10px 20px")};
    border-radius: {(_settings.ButtonRounded ? "20px" : "var(--border-radius)")};
    text-decoration: none;
    font-weight: 600;
    cursor: pointer;
    transition: var(--transition);
    {_btnBaseStyle}
}}

.btn:hover {{
    {_btnHoverStyle}
}}

/* Sidebar Layout */
{(_settings.PageLayout != "full-width" ? $@"
.page-layout {{
    display: grid;
    grid-template-columns: {(_settings.PageLayout == "sidebar-left" ? $"{_settings.SidebarWidth}px 1fr" : $"1fr {_settings.SidebarWidth}px")};
    gap: var(--spacing);
    max-width: var(--max-content-width);
    margin: 0 auto;
    padding: 0 20px;
}}

.page-main {{
    min-width: 0;
}}

.page-sidebar {{
    min-width: 0;
}}

.page-sidebar .section {{
    position: sticky;
    top: 80px;
}}

@media (max-width: 900px) {{
    .page-layout {{
        grid-template-columns: 1fr;
    }}
    .page-sidebar .section {{
        position: static;
    }}
}}
" : "")}

@media (max-width: 768px) {{
header {{ padding: 24px 16px; }}
header h1 {{ font-size: 1.6rem; letter-spacing: -0.01em; }}
header .subtitle {{ font-size: 0.9rem; }}
header .logo {{ max-width: 120px !important; max-height: 60px !important; }}
.header-content.header-logo-left,
.header-content.header-logo-right {{
    flex-direction: column;
    text-align: center;
    gap: 12px;
}}
.header-content.header-logo-left .header-text-group,
.header-content.header-logo-right .header-text-group {{
    text-align: center;
}}
.header-content.header-logo-top-left .logo,
.header-content.header-logo-top-right .logo,
.header-content.header-logo-bottom-left .logo,
.header-content.header-logo-bottom-right .logo {{
    position: static;
    margin: 0 auto 12px;
    display: block;
}}
.header-content.header-dual-logo {{
    flex-direction: column;
    gap: 12px;
}}
.hero h2 {{ font-size: 1.5rem; }}
nav .nav-container {{
    justify-content: center;
    padding: 10px 12px;
    gap: 4px;
}}
nav a {{
    padding: 6px 10px;
    font-size: 0.85rem;
}}
.container {{ padding: 0 14px; }}
.section {{ padding: clamp(16px, 4vw, 24px); margin-bottom: 16px; }}
.content-area {{ padding: 16px 0; }}
.two-col-row {{ grid-template-columns: 1fr; }}
/* Mobile: stack freeform canvas blocks vertically */
.page-canvas {{
    min-height: auto !important;
    display: flex !important;
    flex-direction: column !important;
    gap: 8px;
    padding: 8px 0;
}}
.page-canvas > [data-block-id] {{
    position: relative !important;
    left: 0 !important;
    top: auto !important;
    width: 100% !important;
    max-width: 100% !important;
    height: auto !important;
    overflow: visible !important;
    z-index: auto !important;
}}
.page-canvas .container {{
    padding: 0 clamp(16px, 5vw, 24px);
}}
.page-canvas .section {{
    padding: clamp(20px, 5vw, {_settings.SectionSpacing}px);
}}
.page-canvas .leader-item {{
    padding: 16px 18px;
    gap: 6px 14px;
}}
.page-canvas .result-item,
.page-canvas .fixture-item {{
    padding: 16px 18px;
}}
.page-canvas .results-list,
.page-canvas .fixtures-list {{
    gap: 14px;
}}
.page-canvas .leaders-list {{
    gap: 14px;
}}
.header-freeform > [data-block-id] {{
    position: relative !important;
    left: auto !important;
    top: auto !important;
}}
.result-item, .fixture-item {{ 
    grid-template-columns: 1fr !important;
    text-align: center;
    gap: 8px;
}}
.result-item .date, .fixture-item .date,
.result-item .venue, .fixture-item .venue,
.result-item .team, .fixture-item .team,
.result-item .score, .fixture-item .vs {{
    text-align: center;
}}
.result-item .score {{ font-size: 1.1rem; min-width: auto; padding: 0 8px; }}
.date {{ min-width: auto; }}
.gallery-grid {{ grid-template-columns: repeat(2, 1fr); gap: 10px; }}
/* Tables: scroll horizontally on mobile */
.data-table {{ font-size: 0.85rem; }}
.data-table th, .data-table td {{ padding: 8px 6px; }}
.table-responsive {{ -webkit-overflow-scrolling: touch; }}
/* Leader items: tighter on mobile */
.leader-item {{ gap: 10px; padding: 10px; }}
.leader-item .rank {{ font-size: 1.2rem; min-width: 32px; }}
.leader-item .player-team {{ font-size: 0.8rem; }}
/* Stat cards */
.stats-grid {{ gap: 10px; }}
.stat-card {{ padding: 14px; }}
.stat-number {{ font-size: 1.4rem; }}
/* Entry form */
.entry-form-submit {{ width: 100%; text-align: center; padding: 14px 20px; }}
.form-group input, .form-group textarea, .form-group select {{ font-size: 16px; padding: 12px 14px; }}
.entry-form-logo img {{ max-height: 70px; max-width: 200px; }}
/* Featured pages */
.featured-page-link {{ padding: 16px 18px; gap: 14px; }}
.featured-page-icon {{ font-size: 1.5rem; }}
.featured-page-info h3 {{ font-size: 1rem; }}
.featured-page-info p {{ font-size: 0.85rem; }}
/* Standings table on home */
.home-standings th, .home-standings td {{ padding: 6px 5px; font-size: 0.8rem; }}
/* Footer */
footer {{ padding: 24px 16px; }}
.footer-content {{ font-size: 0.85rem; }}
.footer-social {{ gap: 10px; margin: 14px 0; }}
/* Mini stats */
.mini-stats {{ flex-wrap: wrap; gap: 10px; }}
.mini-stats .stat-card {{ flex: 1 1 calc(50% - 10px); min-width: 0; }}
.contact-grid {{ grid-template-columns: 1fr; gap: 16px; }}
.sponsors-grid {{ grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: 12px; }}
}}

@media (max-width: 480px) {{
header {{ padding: 18px 12px; }}
header h1 {{ font-size: 1.3rem; }}
header .subtitle {{ font-size: 0.8rem; }}
header .logo {{ max-width: 90px !important; max-height: 50px !important; }}
nav a {{ padding: 5px 8px; font-size: 0.8rem; }}
.container {{ padding: 0 10px; }}
.section {{ padding: 14px; }}
.hero h2 {{ font-size: 1.3rem; }}
.hero {{ padding: 24px 12px; }}
.page-canvas .container {{ padding: 0 clamp(14px, 4vw, 20px); }}
.page-canvas .section {{ padding: clamp(18px, 5vw, {_settings.SectionSpacing}px); }}
.page-canvas .leader-item {{ padding: 14px 16px; gap: 4px 12px; }}
.page-canvas .result-item,
.page-canvas .fixture-item {{ padding: 14px 16px; }}
.gallery-grid {{ grid-template-columns: 1fr; }}
.stats-grid {{ grid-template-columns: 1fr 1fr; }}
.stat-card {{ padding: 12px; }}
.stat-number {{ font-size: 1.2rem; }}
.leader-item .rank {{ font-size: 1rem; min-width: 28px; }}
.data-table {{ font-size: 0.8rem; }}
.data-table th, .data-table td {{ padding: 6px 4px; }}
.featured-page-link {{ padding: 14px; gap: 10px; }}
.entry-form-header h3 {{ font-size: 1.1rem; }}
.entry-form-logo img {{ max-height: 50px; max-width: 160px; }}
footer {{ padding: 20px 12px; }}
}}

{_settings.CustomCss}
";
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

        private static string GetCompetitionCSS()
        {
            return @"
/* Competition Page */
.competition-card { margin-bottom: 2rem; }
.competition-header { display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 8px; margin-bottom: 8px; }
.competition-header h3 { margin: 0; }
.competition-meta { color: var(--text-secondary); font-size: 0.9rem; margin-bottom: 1rem; }
.badge { display: inline-block; padding: 4px 12px; border-radius: 20px; font-size: 0.8rem; font-weight: 600; }
.status-completed { background: #D1FAE5; color: #065F46; }
.status-active { background: #DBEAFE; color: #1E40AF; }
.status-draft { background: #F3F4F6; color: #4B5563; }
.competition-rounds, .competition-groups, .comp-groups, .comp-results { margin-top: 1rem; }
.comp-groups { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 16px; }
.group-round-title { font-size: 1.1rem; font-weight: 700; margin: 1.5rem 0 0.5rem; padding-bottom: 4px; border-bottom: 2px solid var(--primary-color); display: inline-block; }
.round-section, .group-section { margin-bottom: 1.5rem; }
.round-section h4, .group-section h4 { font-size: 1rem; margin-bottom: 0.5rem; border-bottom: 2px solid var(--primary-color); padding-bottom: 4px; display: inline-block; }
.group-count { font-weight: 400; font-size: 0.85rem; color: var(--text-secondary); }
.group-players { display: flex; flex-direction: column; gap: 4px; }
.group-player { padding: 6px 10px; border-radius: 6px; background: #F9FAFB; font-size: 0.9rem; display: flex; justify-content: space-between; align-items: center; }
.group-player.gp-winner { background: #D1FAE5; font-weight: 700; color: #065F46; }
.group-player.gp-loser { background: #FEE2E2; color: #991B1B; }
.group-player.gp-noshow { background: #FEE2E2; color: #991B1B; text-decoration: line-through; opacity: 0.7; }
.gp-badge { font-size: 0.75rem; font-weight: 600; }
.gp-badge-w { color: #059669; }
.gp-badge-l { color: #DC2626; }
.gp-badge-ns { color: #DC2626; font-style: italic; }
.match-row { display: grid; grid-template-columns: 1fr auto 1fr; gap: 8px; align-items: center; padding: 8px 12px; border-radius: 6px; margin-bottom: 4px; background: #F9FAFB; }
.match-row.match-complete { background: #F0FDF4; }
.match-player { font-size: 0.9rem; }
.match-player:first-child { text-align: right; }
.match-player.winner { font-weight: 700; }
.match-score { font-weight: 700; text-align: center; min-width: 50px; }
.match-venue { font-size: 0.8rem; color: #6B7280; text-align: center; margin: -2px 0 6px; padding-left: 12px; }
tr.qualifying td { background: #EFF6FF; }
.comp-standings { margin-bottom: 1.5rem; }
.round-date { font-size: 0.82rem; color: var(--text-secondary); font-weight: 400; margin-left: 6px; }
.round-date::before { content: '\1F4C5 '; }

/* Competition selector tabs */
.comp-tabs { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 1.5rem; }
.comp-tab { display: flex; flex-direction: column; align-items: flex-start; gap: 4px; padding: 12px 20px; border: 2px solid #E5E7EB; border-radius: 10px; background: #fff; cursor: pointer; transition: all .2s; font-family: inherit; font-size: inherit; }
.comp-tab:hover { border-color: var(--primary-color); background: #F8FAFC; }
.comp-tab.active { border-color: var(--primary-color); background: var(--primary-color); color: #fff; box-shadow: 0 2px 8px rgba(0,0,0,.12); }
.comp-tab.active .badge { background: rgba(255,255,255,.25); color: #fff; }
.comp-tab.active .comp-tab-name { color: #fff; }
.comp-tab-name { font-weight: 700; font-size: 0.95rem; }

/* Competition info bar */
.comp-info-bar { display: flex; flex-wrap: wrap; gap: 16px; padding: 10px 16px; background: #F8FAFC; border-radius: 8px; margin-bottom: 1.5rem; font-size: 0.9rem; color: var(--text-secondary); }

/* Competition panel */
.comp-panel { animation: fadeIn .25s ease; }
@keyframes fadeIn { from { opacity: 0; transform: translateY(6px); } to { opacity: 1; transform: translateY(0); } }

/* ═══════════════════════════════════════════════════════════
   KNOCKOUT BRACKET — CSS Grid layout + connector lines
   ═══════════════════════════════════════════════════════════ */

.bk-scroll { overflow-x: auto; -webkit-overflow-scrolling: touch; padding-bottom: 12px; }
.bk-scroll::-webkit-scrollbar { height: 6px; }
.bk-scroll::-webkit-scrollbar-track { background: #F1F5F9; border-radius: 3px; }
.bk-scroll::-webkit-scrollbar-thumb { background: #CBD5E1; border-radius: 3px; }

.bk-grid { display: grid; grid-template-rows: auto 1fr; min-width: max-content; padding: 8px 0; }

/* Round header (row 1) */
.bk-hdr { text-align: center; padding: 0 4px 6px; align-self: end; }
.bk-rn { font-size: .8rem; font-weight: 700; color: #6B7280; }
.bk-rv { font-size: .7rem; color: #6B7280; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 220px; }

/* Round body (row 2) — match cards stacked vertically */
.bk-body { display: flex; flex-direction: column; justify-content: space-around; padding: 0 4px; }

/* Match card */
.bk-card { background: #fff; border: 1px solid #E5E7EB; border-radius: 10px; overflow: hidden; box-shadow: 0 1px 4px rgba(0,0,0,.08); }
.bk-card.bk-done { border-color: #10B981; border-width: 2px; }
.bk-player { display: flex; align-items: center; justify-content: space-between; padding: 7px 12px; font-size: .88rem; min-height: 34px; }
.bk-player.bk-w { background: #ECFDF5; font-weight: 700; color: #065F46; }
.bk-player.bk-tbd .bk-name { color: #9CA3AF; font-style: italic; }
.bk-player.bk-bye .bk-name { color: #D1D5DB; font-style: italic; font-size: .8rem; }
.bk-name { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; margin-right: 8px; }
.bk-sc { font-weight: 700; min-width: 20px; text-align: center; }
.bk-sc.bk-sw { color: #065F46; }
.bk-dv { height: 1px; background: #E5E7EB; }
.bk-card.bk-done .bk-dv { background: #D1FAE5; }
.bk-venue { font-size: .75rem; color: #6B7280; text-align: center; padding: 3px 8px; background: #F8FAFC; border-top: 1px solid #E5E7EB; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }

/* ── Connector column between rounds (row 2 only — aligned with match bodies) ── */
.bk-conn { display: flex; flex-direction: column; }
.bk-cg { position: relative; flex: 1; }
.bk-cg span { position: absolute; display: block; }

/* Horizontal lines from source matches to vertical bar */
.bk-hl { left: 0; width: 50%; height: 2px; background: #CBD5E1; }

/* Vertical bar connecting the pair */
.bk-vl { left: calc(50% - 1px); top: 25%; bottom: 25%; width: 2px; background: #CBD5E1; }

/* Horizontal line from vertical bar to destination match */
.bk-rl { left: 50%; right: 0; top: calc(50% - 1px); height: 2px; background: #CBD5E1; }

@media (max-width: 768px) {
    .match-row { grid-template-columns: 1fr; text-align: center; gap: 2px; }
    .match-player:first-child { text-align: center; }
    .comp-tabs { flex-direction: column; }
    .comp-info-bar { flex-direction: column; gap: 6px; }
    .bk-hdr { max-width: 180px; }
    .bk-rv { max-width: 180px; }
}
";
        }
    }
}
