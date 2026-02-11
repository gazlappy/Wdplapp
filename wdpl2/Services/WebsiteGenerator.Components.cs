using System;
using System.Linq;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    /// <summary>
    /// WebsiteGenerator partial class containing shared HTML component methods.
    /// </summary>
    public sealed partial class WebsiteGenerator
    {
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
        
        private void AppendHeader(StringBuilder html, Season season)
        {
            AppendHeaderBlock(html, season, "");
        }
        
        private void AppendHeaderBlock(StringBuilder html, Season season, string dataAttrs)
        {
            var logoData = _settings.GetEffectiveLogoData();
            var hasLogo = _settings.UseCustomLogo && logoData != null && logoData.Length > 0;
            
            html.AppendLine($"    <header {dataAttrs}>");
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
            AppendNavBlock(html, activePage, "");
        }
        
        private void AppendNavBlock(StringBuilder html, string activePage, string dataAttrs)
        {
            html.AppendLine($"    <nav {dataAttrs}>");
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
            
            // UK 8-Ball Pool Game
            NavLink("pool-game.html", "?? Play Pool", "Pool Game");
            
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
            AppendFooterBlock(html, "");
        }
        
        private void AppendFooterBlock(StringBuilder html, string dataAttrs)
        {
            html.AppendLine($"    <footer {dataAttrs}>");
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
    }
}
