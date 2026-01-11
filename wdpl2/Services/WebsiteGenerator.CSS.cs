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
    }
}
