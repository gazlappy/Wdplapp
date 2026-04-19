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
        private static readonly HashSet<string> GoogleFonts = new(StringComparer.OrdinalIgnoreCase)
        {
            "Inter", "Roboto", "Open Sans", "Poppins", "Lato", "Montserrat",
            "Raleway", "Nunito", "Oswald", "Playfair Display", "Merriweather",
            "Source Sans Pro", "PT Sans", "Barlow", "Rubik", "Work Sans",
            "DM Sans", "Manrope", "Outfit", "Space Grotesk", "Plus Jakarta Sans"
        };

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

            // Google Fonts
            var fontsToLoad = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (GoogleFonts.Contains(_settings.FontFamily))
                fontsToLoad.Add(_settings.FontFamily);
            if (GoogleFonts.Contains(_settings.HeaderFontFamily))
                fontsToLoad.Add(_settings.HeaderFontFamily);
            if (fontsToLoad.Count > 0)
            {
                var families = string.Join("&family=", fontsToLoad.Select(f => f.Replace(" ", "+") + ":wght@400;500;600;700;800"));
                html.AppendLine("    <link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
                html.AppendLine("    <link rel=\"preconnect\" href=\"https://fonts.gstatic.com\" crossorigin>");
                html.AppendLine($"    <link href=\"https://fonts.googleapis.com/css2?family={families}&display=swap\" rel=\"stylesheet\">");
            }

            html.AppendLine($"    <link rel=\"stylesheet\" href=\"style.css?v={_cacheBuster}\">");

            // Custom head HTML
            if (!string.IsNullOrWhiteSpace(_settings.CustomHeadHtml))
                html.AppendLine(_settings.CustomHeadHtml);

            html.AppendLine("</head>");
        }
        
        private void AppendHeader(StringBuilder html, Season season)
        {
            AppendHeaderBlock(html, season, "data-block-id=\"header\" data-block-name=\"Header\" data-structural=\"true\"");
        }
        
        private void AppendHeaderBlock(StringBuilder html, Season season, string dataAttrs)
        {
            var logoData = _settings.GetEffectiveLogoData();
            var hasLogo = _settings.UseCustomLogo && logoData != null && logoData.Length > 0;
            var hasSub = !string.IsNullOrWhiteSpace(_settings.LeagueSubtitle);
            var hasBadge = _settings.ShowSeasonBadge;
            var layout = _settings.HeaderLayout;
            var logoPos = _settings.LogoPosition; // above, below, left, right, top-left, top-right, bottom-left, bottom-right, hidden
            var dualLogo = _settings.DuplicateLogoBothSides && hasLogo && logoPos != "hidden";

            // Check if any sub-element has a freeform position set (from drag-drop editor)
            bool freeform = !string.IsNullOrEmpty(_settings.HeaderLogoPos)
                         || !string.IsNullOrEmpty(_settings.HeaderTitlePos)
                         || !string.IsNullOrEmpty(_settings.HeaderSubtitlePos)
                         || !string.IsNullOrEmpty(_settings.HeaderBadgePos);

            string logoTag = "";
            string logoTagRight = "";
            if (hasLogo && logoPos != "hidden")
            {
                var imageOptimizer = new ImageOptimizationService();
                var mimeType = imageOptimizer.GetMimeType("logo.png");
                var dataUrl = imageOptimizer.ToDataUrl(logoData!, mimeType);
                var posStyle = freeform ? BuildSubElementStyle(_settings.HeaderLogoPos) : "";
                logoTag = $"<img src=\"{dataUrl}\" alt=\"{_settings.LeagueName}\" class=\"logo\" data-block-id=\"header-logo\" data-block-name=\"Logo\" style=\"max-width: {_settings.LogoMaxWidth}px; max-height: {_settings.LogoMaxHeight}px;{posStyle}\">";
                if (dualLogo)
                {
                    // Use the right-side logo if set, otherwise fall back to the main logo
                    var rightLogoData = _settings.GetEffectiveRightLogoData();
                    var rightDataUrl = dataUrl;
                    if (rightLogoData != null && rightLogoData != logoData)
                    {
                        var rightMime = imageOptimizer.GetMimeType("logo.png");
                        rightDataUrl = imageOptimizer.ToDataUrl(rightLogoData, rightMime);
                    }
                    var rightMaxW = _settings.RightLogoMaxWidth;
                    var rightMaxH = _settings.RightLogoMaxHeight;
                    logoTagRight = $"<img src=\"{rightDataUrl}\" alt=\"{_settings.LeagueName}\" class=\"logo logo-right\" data-block-id=\"header-logo-right\" data-block-name=\"Logo (Right)\" style=\"max-width: {rightMaxW}px; max-height: {rightMaxH}px;\">";
                }
            }

            // Determine CSS class for logo position (used in default/standard layouts)
            var logoPosClass = (hasLogo && !freeform && logoPos != "hidden" && !dualLogo) ? $" header-logo-{logoPos}" : "";
            var dualLogoClass = dualLogo ? " header-dual-logo" : "";

            html.AppendLine($"    <header {dataAttrs}>");
            html.AppendLine($"        <div class=\"header-content{logoPosClass}{dualLogoClass}{(freeform ? " header-freeform" : "")}\">");

            if (dualLogo)
            {
                // Dual logo layout: logo | title group (+ badge) | logo
                html.AppendLine($"            {logoTag}");
                html.AppendLine("            <div class=\"header-text-group\">");
                html.AppendLine($"                <h1 data-block-id=\"header-title\" data-block-name=\"Title\">{_settings.LeagueName}</h1>");
                if (hasSub)
                    html.AppendLine($"                <p class=\"subtitle\" data-block-id=\"header-subtitle\" data-block-name=\"Subtitle\">{_settings.LeagueSubtitle}</p>");
                if (hasBadge)
                    html.AppendLine($"                <span class=\"season-badge\" data-block-id=\"header-badge\" data-block-name=\"Season Badge\">{season.Name}</span>");
                html.AppendLine("            </div>");
                html.AppendLine($"            {logoTagRight}");
            }
            else
            {
                switch (layout)
            {
                case "split":
                    // Logo left, title group right, badge far right
                    if (hasLogo && logoPos != "hidden") html.AppendLine($"            {logoTag}");
                    html.AppendLine("            <div class=\"header-text-group\">");
                    html.AppendLine($"                <h1 data-block-id=\"header-title\" data-block-name=\"Title\">{_settings.LeagueName}</h1>");
                    if (hasSub)
                        html.AppendLine($"                <p class=\"subtitle\" data-block-id=\"header-subtitle\" data-block-name=\"Subtitle\">{_settings.LeagueSubtitle}</p>");
                    html.AppendLine("            </div>");
                    if (hasBadge)
                        html.AppendLine($"            <span class=\"season-badge\" data-block-id=\"header-badge\" data-block-name=\"Season Badge\">{season.Name}</span>");
                    break;

                case "two-row":
                    // Row 1: logo + badge, Row 2: title + subtitle
                    html.AppendLine("            <div class=\"header-row\">");
                    if (hasLogo && logoPos != "hidden") html.AppendLine($"                {logoTag}");
                    if (hasBadge)
                        html.AppendLine($"                <span class=\"season-badge\" data-block-id=\"header-badge\" data-block-name=\"Season Badge\">{season.Name}</span>");
                    html.AppendLine("            </div>");
                    html.AppendLine("            <div class=\"header-row\">");
                    html.AppendLine($"                <h1 data-block-id=\"header-title\" data-block-name=\"Title\">{_settings.LeagueName}</h1>");
                    if (hasSub)
                        html.AppendLine($"                <p class=\"subtitle\" data-block-id=\"header-subtitle\" data-block-name=\"Subtitle\">{_settings.LeagueSubtitle}</p>");
                    html.AppendLine("            </div>");
                    break;

                case "scoreboard":
                    // Grid: logo | title+subtitle | badge
                    if (hasLogo && logoPos != "hidden") html.AppendLine($"            {logoTag}");
                    else html.AppendLine("            <div></div>");
                    html.AppendLine("            <div style=\"text-align:center;\">");
                    html.AppendLine($"                <h1 data-block-id=\"header-title\" data-block-name=\"Title\">{_settings.LeagueName}</h1>");
                    if (hasSub)
                        html.AppendLine($"                <p class=\"subtitle\" data-block-id=\"header-subtitle\" data-block-name=\"Subtitle\">{_settings.LeagueSubtitle}</p>");
                    html.AppendLine("            </div>");
                    if (hasBadge)
                        html.AppendLine($"            <span class=\"season-badge\" data-block-id=\"header-badge\" data-block-name=\"Season Badge\">{season.Name}</span>");
                    else html.AppendLine("            <div></div>");
                    break;

                default:
                    // Standard flow — uses LogoPosition to arrange elements
                    AppendDefaultHeaderContent(html, season, logoTag, hasLogo, hasSub, hasBadge, logoPos, freeform);
                    break;
            }
            }

            html.AppendLine("        </div>");
            html.AppendLine("    </header>");
        }

        /// <summary>
        /// Renders the default/standard header layout, respecting LogoPosition.
        /// </summary>
        private void AppendDefaultHeaderContent(StringBuilder html, Season season,
            string logoTag, bool hasLogo, bool hasSub, bool hasBadge, string logoPos, bool freeform)
        {
            var showLogo = hasLogo && logoPos != "hidden";

            // Corner positions: logo is placed inside header-content but CSS positions it absolutely
            var isCorner = logoPos is "top-left" or "top-right" or "bottom-left" or "bottom-right";

            // "left" / "right": logo sits beside the text group in a row
            var isSide = logoPos is "left" or "right";

            if (isCorner && showLogo)
            {
                // Corner logo — just place it, CSS handles absolute positioning
                html.AppendLine($"            {logoTag}");
            }

            if (isSide && showLogo && logoPos == "left")
            {
                html.AppendLine($"            {logoTag}");
            }

            // "above" logo
            if (showLogo && logoPos == "above")
            {
                html.AppendLine($"            {logoTag}");
            }

            // Title block
            if (isSide)
            {
                html.AppendLine("            <div class=\"header-text-group\">");
            }

            {
                var posStyle = freeform ? BuildSubElementStyle(_settings.HeaderTitlePos) : "";
                var styleAttr = !string.IsNullOrEmpty(posStyle) ? $" style=\"{posStyle}\"" : "";
                html.AppendLine($"            <h1 data-block-id=\"header-title\" data-block-name=\"Title\"{styleAttr}>{_settings.LeagueName}</h1>");
            }
            if (hasSub)
            {
                var posStyle = freeform ? BuildSubElementStyle(_settings.HeaderSubtitlePos) : "";
                var styleAttr = !string.IsNullOrEmpty(posStyle) ? $" style=\"{posStyle}\"" : "";
                html.AppendLine($"            <p class=\"subtitle\" data-block-id=\"header-subtitle\" data-block-name=\"Subtitle\"{styleAttr}>{_settings.LeagueSubtitle}</p>");
            }

            if (isSide)
            {
                html.AppendLine("            </div>");
            }

            if (isSide && showLogo && logoPos == "right")
            {
                html.AppendLine($"            {logoTag}");
            }

            // "below" logo
            if (showLogo && logoPos == "below")
            {
                html.AppendLine($"            {logoTag}");
            }

            if (hasBadge)
            {
                var posStyle = freeform ? BuildSubElementStyle(_settings.HeaderBadgePos) : "";
                var styleAttr = !string.IsNullOrEmpty(posStyle) ? $" style=\"{posStyle}\"" : "";
                html.AppendLine($"            <span class=\"season-badge\" data-block-id=\"header-badge\" data-block-name=\"Season Badge\"{styleAttr}>{season.Name}</span>");
            }
        }
        
        /// <summary>
        /// Converts a "left%;top%" position string to inline CSS.
        /// Returns empty string if no position is set.
        /// </summary>
        private static string BuildSubElementStyle(string pos)
        {
            if (string.IsNullOrEmpty(pos)) return "";
            var parts = pos.Split(';');
            if (parts.Length < 2) return "";
            return $" position:absolute; left:{parts[0]}; top:{parts[1]};";
        }
        
        private void AppendNavigation(StringBuilder html, string activePage)
        {
            AppendNavBlock(html, activePage, "data-block-id=\"nav\" data-block-name=\"Navigation\" data-structural=\"true\"");
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
            
            NavLink("home.html", _settings.HomeNavLabel, "Home");

            if (_settings.ShowStandings)
                NavLink("standings.html", _settings.StandingsNavLabel, "Standings");

            if (_settings.ShowFixtures)
                NavLink("fixtures.html", _settings.FixturesNavLabel, "Fixtures");

            if (_settings.ShowResults)
                NavLink("results.html", _settings.ResultsNavLabel, "Results");

            if (_settings.ShowPlayerStats)
                NavLink("players.html", _settings.PlayersNavLabel, "Players");

            if (_settings.ShowDivisions)
                NavLink("divisions.html", _settings.DivisionsNavLabel, "Divisions");

            if (_settings.ShowCompetitions)
                NavLink("competitions.html", _settings.CompetitionsNavLabel, "Competitions");

            // UK 8-Ball Pool Game
            if (_settings.ShowPoolGame)
                NavLink("pool-game.html", _settings.PoolGameNavLabel, "Pool Game");

            if (_settings.ShowGallery && _settings.GalleryImages.Count > 0)
                NavLink("gallery.html", _settings.GalleryNavLabel, "Gallery");

            if (_settings.ShowNews && _settings.NewsItems.Count > 0)
                NavLink("news.html", _settings.NewsNavLabel, "News");

            if (_settings.ShowRowsReports && _settings.RowsReports.Count > 0)
                NavLink("rows-reports.html", _settings.RowsReportsNavLabel, "Rows Reports");

            if (_settings.ShowSponsors && _settings.Sponsors.Count > 0)
                NavLink("sponsors.html", _settings.SponsorsNavLabel, "Sponsors");

            if (_settings.ShowRules && _settings.HasAnyRulesContent)
                NavLink("rules.html", _settings.RulesNavLabel, "Rules");

            if (_settings.ShowEntryForms && _settings.EntryForms.Any(f => f.IsPublished))
                NavLink("entry-forms.html", _settings.EntryFormsNavLabel, "Entry Forms");

            if (_settings.ShowHistory && _settings.HistoricHonours.Count > 0)
                NavLink("history.html", _settings.HistoryNavLabel, "History");

            if (_settings.ShowContactPage && _settings.HasContactInfo)
                NavLink("contact.html", _settings.ContactNavLabel, "Contact");

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
            AppendFooterBlock(html, "data-block-id=\"footer\" data-block-name=\"Footer\" data-structural=\"true\"");
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

            if (_settings.FooterNotes.Count > 0)
            {
                html.AppendLine("            <div class=\"footer-notes\">");
                foreach (var note in _settings.FooterNotes)
                {
                    if (!string.IsNullOrWhiteSpace(note))
                        html.AppendLine($"                <p class=\"footer-note\">{note}</p>");
                }
                html.AppendLine("            </div>");
            }

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
            if (activeSponsors.Count == 0) return;
            
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
