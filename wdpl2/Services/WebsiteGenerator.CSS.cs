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
                "banner" => "letter-spacing: 2px; text-transform: uppercase;",
                "stadium" => "letter-spacing: 3px; text-transform: uppercase; text-shadow: 0 0 40px rgba(255,255,255,0.3);",
                "text-only" => $"color: var(--primary-color); font-weight: 800;",
                "championship" => "letter-spacing: 1px; text-transform: uppercase;",
                "neon" => $"text-shadow: 0 0 10px var(--primary-color), 0 0 20px var(--primary-color), 0 0 40px var(--primary-color), 0 0 80px var(--secondary-color); animation: headerNeonPulse 2s ease-in-out infinite; letter-spacing: 3px; text-transform: uppercase;",
                "aurora" => "text-shadow: 0 0 30px rgba(255,255,255,0.4); letter-spacing: 2px;",
                _ => ""
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
                "banner" => "letter-spacing: 4px; text-transform: uppercase;",
                "stadium" => "letter-spacing: 2px; text-transform: uppercase; opacity: 0.7;",
                "text-only" => "color: var(--text-secondary); opacity: 1;",
                _ => ""
            };

            // --- badge ---
            var badgePad = layout is "compact" or "minimal-bar" ? "3px 10px" : "6px 16px";
            var badgeMargin = layout is "compact" or "minimal-bar" or "split" or "scoreboard" ? "0" : "15px";
            var badgeSize = layout is "compact" or "minimal-bar" ? "0.75rem" : "0.9rem";
            var badgeBg = layout is "text-only" or "underline" or "transparent"
                ? "rgba(var(--primary-color-rgb, 59,130,246), 0.15)"
                : "rgba(255,255,255,0.2)";
            var badgeExtra = layout switch
            {
                "minimal-bar" => "margin-left: auto;",
                "scoreboard" => "justify-self: end;",
                _ => ""
            };

            // --- logo ---
            var logoExtra = layout switch
            {
                "compact" or "minimal-bar" => "max-height: 36px; margin: 0;",
                "split" => "order: -1; margin: 0;",
                "scoreboard" => "margin: 0; justify-self: start;",
                _ => "margin-bottom: 15px;"
            };

            // --- text-group (split layout) ---
            var textGroupCss = layout == "split" ? "text-align: right; flex: 1;" : "";

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
    margin-bottom: {h1Margin};
    {h1Extra}
}}

header .subtitle {{
    opacity: 0.9;
    font-size: {subSize};
    {subExtra}
}}

header .season-badge {{
    display: inline-block;
    background: {badgeBg};
    padding: {badgePad};
    border-radius: 20px;
    margin-top: {badgeMargin};
    font-size: {badgeSize};
    {badgeExtra}
}}

header .logo {{
    max-width: 200px;
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
{twoRowCss}
{animations}";
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
    --nav-bg: {_settings.SecondaryColor};
    --nav-text: {_settings.HeaderTextColor};
    --nav-hover: {_settings.AccentColor};
    --footer-bg: #1F2937;
    --footer-text: #F9FAFB;
    --table-header-bg: {_settings.PrimaryColor};
    --table-header-text: {_settings.HeaderTextColor};
    --table-alt-bg: #F9FAFB;
    --font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
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

body {{
    font-family: var(--font-family), var(--font-family-emoji);
    font-size: {_settings.BaseFontSize}px;
    background: var(--background-color);
    color: var(--text-color);
    line-height: 1.6;
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
    {(_settings.NavStyle == "underline" ? "border-bottom: 2px solid transparent;" : "")}
}}

nav a:hover, nav a.active {{
    background: var(--primary-color);
    color: white;
    {(_settings.NavStyle == "underline" ? "background: transparent; color: var(--primary-color); border-bottom-color: var(--primary-color);" : "")}
}}

.content-area {{
    padding: var(--spacing) 0;
}}

/* Freeform canvas layout for home page */
.page-canvas {{
    position: relative;
    overflow: visible;
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
    grid-template-columns: minmax(80px, 100px) 1fr auto 1fr minmax(100px, 150px);
    align-items: center;
    padding: 16px;
    background: rgba(0,0,0,0.02);
    border-radius: calc(var(--border-radius) / 2);
    gap: 10px;
}}

/* When no venue - 4 columns */
.result-item:not(:has(.venue)), .fixture-item:not(:has(.venue)) {{
    grid-template-columns: minmax(80px, 100px) 1fr auto 1fr;
}}

/* When no date - adjust first column */
.result-item:not(:has(.date)), .fixture-item:not(:has(.date)) {{
    grid-template-columns: 1fr auto 1fr minmax(100px, 150px);
}}

/* When no date and no venue */
.result-item:not(:has(.date)):not(:has(.venue)), .fixture-item:not(:has(.date)):not(:has(.venue)) {{
    grid-template-columns: 1fr auto 1fr;
}}

.result-item .date, .fixture-item .date {{
    text-align: left;
    color: var(--text-secondary);
    font-size: 0.85rem;
}}

.result-item .team, .fixture-item .team {{
    font-weight: 600;
    text-align: center;
}}

.result-item .team.winner {{
    color: var(--primary-color);
}}

.result-item .score {{
    font-weight: bold;
    font-size: 1.2rem;
    text-align: center;
    padding: 0 15px;
    white-space: nowrap;
    min-width: 70px;
}}

.fixture-item .vs {{
    color: var(--text-secondary);
    text-align: center;
    padding: 0 10px;
    min-width: 40px;
}}

.fixture-item .venue, .result-item .venue {{
    color: var(--text-secondary);
    font-size: 0.9rem;
    text-align: right;
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
header h1 {{ font-size: 1.8rem; }}
.hero h2 {{ font-size: 1.5rem; }}
nav .nav-container {{ justify-content: center; }}
.two-col-row {{ grid-template-columns: 1fr; }}
/* Mobile: stack freeform canvas blocks vertically */
.page-canvas {{
    min-height: auto !important;
}}
.page-canvas > [data-block-id] {{
    position: relative !important;
    left: 0 !important;
    top: auto !important;
    width: 100% !important;
    height: auto !important;
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
    .gallery-grid {{ grid-template-columns: repeat(2, 1fr); }}
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
    }
}
