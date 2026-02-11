using System;
using System.Collections.Generic;

namespace Wdpl2.Models
{
    /// <summary>
    /// Represents a draggable content block on the home page
    /// </summary>
    public sealed class LayoutBlock
    {
        public string Id { get; set; } = "";
        public string BlockType { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Icon { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
        public int ColumnSpan { get; set; } = 2;
        public int Order { get; set; }
        public bool IsStructural { get; set; }
        
        // Freeform canvas position (PowerPoint-style)
        public double LeftPercent { get; set; }
        public double TopPx { get; set; }
        public double WidthPercent { get; set; } = 100;
        public double HeightPx { get; set; } // 0 = auto
        public int ZIndex { get; set; } = 1;
        
        public static List<LayoutBlock> GetDefaultBlocks() =>
        [
            new() { Id = "header",             BlockType = "header",             DisplayName = "Header",              Icon = "\U0001F3AF", IsEnabled = true,  ColumnSpan = 2, Order = 0,  IsStructural = true,
                    LeftPercent = 0,  TopPx = 0,    WidthPercent = 100, ZIndex = 10 },
            new() { Id = "nav",                BlockType = "nav",                DisplayName = "Navigation",          Icon = "\U0001F9ED", IsEnabled = true,  ColumnSpan = 2, Order = 1,  IsStructural = true,
                    LeftPercent = 0,  TopPx = 130,  WidthPercent = 100, ZIndex = 10 },
            new() { Id = "welcome",           BlockType = "welcome",           DisplayName = "Welcome / Hero",      Icon = "\U0001F3E0", IsEnabled = true,  ColumnSpan = 2, Order = 2,
                    LeftPercent = 2,  TopPx = 210,  WidthPercent = 96,  ZIndex = 1 },
            new() { Id = "quick-stats",        BlockType = "quick-stats",        DisplayName = "Quick Stats",         Icon = "\U0001F4CA", IsEnabled = true,  ColumnSpan = 2, Order = 3,
                    LeftPercent = 2,  TopPx = 490,  WidthPercent = 96,  ZIndex = 1 },
            new() { Id = "league-leaders",     BlockType = "league-leaders",     DisplayName = "League Leaders",      Icon = "\U0001F3C6", IsEnabled = true,  ColumnSpan = 1, Order = 4,
                    LeftPercent = 2,  TopPx = 770,  WidthPercent = 47,  ZIndex = 1 },
            new() { Id = "recent-results",     BlockType = "recent-results",     DisplayName = "Recent Results",      Icon = "\U0001F3C1", IsEnabled = true,  ColumnSpan = 1, Order = 5,
                    LeftPercent = 51, TopPx = 770,  WidthPercent = 47,  ZIndex = 1 },
            new() { Id = "upcoming-fixtures",  BlockType = "upcoming-fixtures",  DisplayName = "Upcoming Fixtures",   Icon = "\U0001F4C5", IsEnabled = true,  ColumnSpan = 1, Order = 6,
                    LeftPercent = 2,  TopPx = 1090, WidthPercent = 47,  ZIndex = 1 },
            new() { Id = "latest-news",        BlockType = "latest-news",        DisplayName = "Latest News",         Icon = "\U0001F4F0", IsEnabled = false, ColumnSpan = 1, Order = 7,
                    LeftPercent = 51, TopPx = 1090, WidthPercent = 47,  ZIndex = 1 },
            new() { Id = "sponsors",           BlockType = "sponsors",           DisplayName = "Sponsors",            Icon = "\u2B50",     IsEnabled = false, ColumnSpan = 2, Order = 8,
                    LeftPercent = 2,  TopPx = 1410, WidthPercent = 96,  ZIndex = 1 },
            new() { Id = "footer",             BlockType = "footer",             DisplayName = "Footer",              Icon = "\U0001F4CB", IsEnabled = true,  ColumnSpan = 2, Order = 9,  IsStructural = true,
                    LeftPercent = 0,  TopPx = 1430, WidthPercent = 100, ZIndex = 10 },
        ];
        
        /// <summary>
        /// Auto-calculate positions for blocks that haven't been positioned yet.
        /// Produces a clean layout matching the style of other pages:
        /// header at top, nav below header, content blocks with consistent gaps,
        /// paired half-width blocks side by side, footer below everything.
        /// </summary>
        public static void AutoPositionBlocks(List<LayoutBlock> blocks)
        {
            if (blocks.Any(b => b.IsEnabled && b.TopPx > 0))
                return; // Already positioned
            
            const double headerH = 130;
            const double navH = 60;
            const double contentGap = 30;
            const double fullRowH = 250;
            const double halfRowH = 300;
            const double margin = 2;
            
            double y = 0;
            
            // Header
            foreach (var b in blocks.Where(b => b.IsEnabled && b.BlockType == "header"))
            { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; }
            y += headerH;
            
            // Nav
            foreach (var b in blocks.Where(b => b.IsEnabled && b.BlockType == "nav"))
            { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; }
            y += navH + contentGap;
            
            // Content blocks
            var content = blocks.Where(b => b.IsEnabled && !b.IsStructural).OrderBy(b => b.Order).ToList();
            for (int i = 0; i < content.Count; i++)
            {
                var block = content[i];
                var next = (i + 1 < content.Count) ? content[i + 1] : null;
                
                if (block.ColumnSpan == 1 && next != null && next.ColumnSpan == 1)
                {
                    // Pair side by side
                    block.LeftPercent = margin;
                    block.TopPx = y;
                    block.WidthPercent = 47;
                    block.ZIndex = 1;
                    
                    next.LeftPercent = 51;
                    next.TopPx = y;
                    next.WidthPercent = 47;
                    next.ZIndex = 1;
                    
                    i++; // skip next, already placed
                    y += halfRowH + contentGap;
                }
                else
                {
                    // Full width
                    block.LeftPercent = margin;
                    block.TopPx = y;
                    block.WidthPercent = 96;
                    block.ZIndex = 1;
                    y += fullRowH + contentGap;
                }
            }
            
            // Footer
            foreach (var b in blocks.Where(b => b.IsEnabled && b.BlockType == "footer"))
            { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; }
        }
    }
    
    /// <summary>
    /// Website configuration and deployment settings
    /// </summary>
    public sealed class WebsiteSettings
    {
        // Site Branding
        public string LeagueName { get; set; } = "My Pool League";
        public string LeagueSubtitle { get; set; } = "Weekly 8-Ball Pool Competition";
        public string? LogoPath { get; set; }
        public byte[]? LogoImageData { get; set; }
        public string FaviconUrl { get; set; } = "";
        
        // Welcome/About Section
        public string WelcomeMessage { get; set; } = "";
        public string AboutText { get; set; } = "";
        public string ContactEmail { get; set; } = "";
        public string ContactPhone { get; set; } = "";
        public string ContactAddress { get; set; } = "";
        
        // Social Media Links
        public string FacebookUrl { get; set; } = "";
        public string TwitterUrl { get; set; } = "";
        public string InstagramUrl { get; set; } = "";
        public string YouTubeUrl { get; set; } = "";
        public string TikTokUrl { get; set; } = "";
        public string WebsiteUrl { get; set; } = "";
        
        // Theme Colors
        public string PrimaryColor { get; set; } = "#3B82F6";
        public string SecondaryColor { get; set; } = "#10B981";
        public string AccentColor { get; set; } = "#F59E0B";
        public string BackgroundColor { get; set; } = "#F8FAFC";
        public string CardBackgroundColor { get; set; } = "#FFFFFF";
        public string TextColor { get; set; } = "#0F172A";
        public string TextSecondaryColor { get; set; } = "#64748B";
        public string HeaderTextColor { get; set; } = "#FFFFFF";
        
        // Advanced Theme Options
        public bool EnableAnimations { get; set; } = true;
        public bool EnableGradients { get; set; } = true;
        public bool EnableShadows { get; set; } = true;
        public bool EnableRoundedCorners { get; set; } = true;
        public bool ShowLastUpdated { get; set; } = true;
        public string FontFamily { get; set; } = "Inter";
        public string HeaderFontFamily { get; set; } = "Inter";
        public int BaseFontSize { get; set; } = 16;
        public int BorderRadius { get; set; } = 12;
        public int CardSpacing { get; set; } = 24;
        
        // Page Layout Options
        public int MaxContentWidth { get; set; } = 1200; // 960, 1200, 1400, 1600
        public int SectionSpacing { get; set; } = 24; // px gap between sections
        public string PageLayout { get; set; } = "full-width"; // full-width, sidebar-right, sidebar-left
        public int SidebarWidth { get; set; } = 320; // px width when sidebar layout
        
        // Home Section Order (controls display order on home page)
        public List<string> HomeSectionOrder { get; set; } = new()
        {
            "welcome", "quick-stats", "league-leaders", "recent-results", "upcoming-fixtures", "latest-news", "sponsors"
        };
        
        // Drag-and-drop layout blocks for home page
        public List<LayoutBlock> HomeLayoutBlocks { get; set; } = new();
        
        /// <summary>
        /// Available max content widths
        /// </summary>
        public static readonly Dictionary<int, string> ContentWidths = new()
        {
            [960] = "Narrow (960px)",
            [1200] = "Standard (1200px)",
            [1400] = "Wide (1400px)",
            [1600] = "Extra Wide (1600px)"
        };
        
        // Header Options
        public string HeaderStyle { get; set; } = "gradient"; // gradient, solid, image, minimal
        public string HeaderLayout { get; set; } = "centered"; // centered, split, banner, compact, minimal-bar
        public string HeaderBackgroundImage { get; set; } = "";
        public byte[]? HeaderBackgroundImageData { get; set; }
        public bool ShowHeaderPattern { get; set; } = true;
        public bool ShowSeasonBadge { get; set; } = true;
        public string HeaderAlignment { get; set; } = "center"; // left, center, right
        
        // Header sub-element positions (freeform inside header, percentage-based)
        // Each is {left}%;{top}% — empty string means use auto/flow layout
        public string HeaderLogoPos { get; set; } = "";
        public string HeaderTitlePos { get; set; } = "";
        public string HeaderSubtitlePos { get; set; } = "";
        public string HeaderBadgePos { get; set; } = "";
        
        // Navigation Options
        public string NavStyle { get; set; } = "pills"; // pills, underline, buttons, minimal
        public bool NavSticky { get; set; } = true;
        public bool ShowNavIcons { get; set; } = false;
        public string NavPosition { get; set; } = "center"; // left, center, right
        
        // Footer Options
        public string FooterStyle { get; set; } = "dark"; // dark, light, gradient, minimal
        public bool ShowFooterSocialLinks { get; set; } = true;
        public bool ShowFooterContact { get; set; } = true;
        public bool ShowPoweredBy { get; set; } = true;
        public string CustomFooterText { get; set; } = "";
        public string CopyrightText { get; set; } = "";
        
        // Content Options - Main Toggles
        public bool ShowStandings { get; set; } = true;
        public bool ShowFixtures { get; set; } = true;
        public bool ShowResults { get; set; } = true;
        public bool ShowPlayerStats { get; set; } = true;
        public bool ShowDivisions { get; set; } = true;
        public bool ShowGallery { get; set; } = false;
        public bool ShowTopScorers { get; set; } = true;
        public bool ShowRecentForm { get; set; } = true;
        public bool ShowNews { get; set; } = false;
        public bool ShowSponsors { get; set; } = false;
        public bool ShowRules { get; set; } = false;
        public bool ShowContactPage { get; set; } = false;
        
        // Home Page Options
        public bool HomeShowWelcomeSection { get; set; } = true;
        public bool HomeShowQuickStats { get; set; } = true;
        public bool HomeShowRecentResults { get; set; } = true;
        public bool HomeShowUpcomingFixtures { get; set; } = true;
        public bool HomeShowLeagueLeaders { get; set; } = true;
        public bool HomeShowLatestNews { get; set; } = false;
        public bool HomeShowSponsors { get; set; } = false;
        public int HomeRecentResultsCount { get; set; } = 5;
        public int HomeUpcomingFixturesCount { get; set; } = 5;
        public int HomeLeagueLeadersCount { get; set; } = 5;
        public string HomeLayout { get; set; } = "standard"; // standard, magazine, minimal, dashboard
        
        // Standings Page Options
        public bool StandingsShowPosition { get; set; } = true;
        public bool StandingsShowPlayed { get; set; } = true;
        public bool StandingsShowWon { get; set; } = true;
        public bool StandingsShowDrawn { get; set; } = true;
        public bool StandingsShowLost { get; set; } = true;
        public bool StandingsShowFramesFor { get; set; } = true;
        public bool StandingsShowFramesAgainst { get; set; } = true;
        public bool StandingsShowFramesDiff { get; set; } = true;
        public bool StandingsShowPoints { get; set; } = true;
        public bool StandingsShowForm { get; set; } = true;
        public bool StandingsShowMedals { get; set; } = true;
        public bool StandingsHighlightTop { get; set; } = true;
        public bool StandingsHighlightBottom { get; set; } = false;
        public int StandingsHighlightTopCount { get; set; } = 3;
        public int StandingsHighlightBottomCount { get; set; } = 2;
        public string StandingsTopHighlightColor { get; set; } = "#22C55E";
        public string StandingsBottomHighlightColor { get; set; } = "#EF4444";
        
        // Results Page Options
        public bool ResultsShowScore { get; set; } = true;
        public bool ResultsShowVenue { get; set; } = true;
        public bool ResultsShowDivision { get; set; } = true;
        public bool ResultsShowDate { get; set; } = true;
        public bool ResultsShowTime { get; set; } = true;
        public bool ResultsHighlightWinner { get; set; } = true;
        public bool ResultsGroupByDate { get; set; } = true;
        public bool ResultsGroupByWeek { get; set; } = false;
        public bool ResultsShowFrameDetails { get; set; } = false;
        public int ResultsPerPage { get; set; } = 20;
        public string ResultsDateFormat { get; set; } = "ddd dd MMM";
        
        // Results/Fixtures Layout Alignment
        public string ResultsHomeTeamAlign { get; set; } = "right";   // right, left, center
        public string ResultsAwayTeamAlign { get; set; } = "left";    // right, left, center
        public string ResultsScoreAlign { get; set; } = "center";     // center, left, right
        public string ResultsMobileTeamAlign { get; set; } = "center"; // center, left, right (mobile view)
        
        // Fixtures Page Options
        public bool FixturesShowVenue { get; set; } = true;
        public bool FixturesShowDivision { get; set; } = true;
        public bool FixturesShowDate { get; set; } = true;
        public bool FixturesShowTime { get; set; } = true;
        public bool FixturesGroupByDate { get; set; } = true;
        public bool FixturesGroupByWeek { get; set; } = false;
        public bool FixturesShowCountdown { get; set; } = false;
        public int FixturesPerPage { get; set; } = 20;
        public string FixturesDateFormat { get; set; } = "ddd dd MMM";
        public string FixturesHomeTeamAlign { get; set; } = "right";  // right, left, center
        public string FixturesAwayTeamAlign { get; set; } = "left";   // right, left, center
        public string FixturesVsAlign { get; set; } = "center";       // center, left, right
        
        // Fixtures Sheet Options (for fixtures page)
        public bool FixturesShowPrintableSheet { get; set; } = false;
        public bool FixturesSheetDefaultExpanded { get; set; } = false;
        public string FixturesSheetTitle { get; set; } = "Printable Fixtures Sheet";
        
        // Players Page Options
        public bool PlayersShowPosition { get; set; } = true;
        public bool PlayersShowTeam { get; set; } = true;
        public bool PlayersShowPlayed { get; set; } = true;
        public bool PlayersShowWon { get; set; } = true;
        public bool PlayersShowLost { get; set; } = true;
        public bool PlayersShowWinPercentage { get; set; } = true;
        public bool PlayersShowEightBalls { get; set; } = true;
        public bool PlayersShowAverage { get; set; } = false;
        public bool PlayersShowForm { get; set; } = false;
        public bool PlayersShowPhoto { get; set; } = false;
        public bool PlayersShowRating { get; set; } = true;
        public int PlayersMinGames { get; set; } = 0;
        public int PlayersMinFramesPercentage { get; set; } = 0;
        public bool PlayersUsePercentageFilter { get; set; } = false;
        public int PlayersPerPage { get; set; } = 50;
        public string PlayersSortBy { get; set; } = "winpercentage"; // winpercentage, won, played, eightballs, rating
        
        // Divisions Page Options
        public bool DivisionsShowTeamCount { get; set; } = true;
        public bool DivisionsShowPlayerCount { get; set; } = true;
        public bool DivisionsShowDescription { get; set; } = true;
        public bool DivisionsShowTeamList { get; set; } = true;
        public bool DivisionsShowMiniStandings { get; set; } = false;
        public string DivisionsLayout { get; set; } = "cards"; // cards, list, grid
        
        // Table Styling Options
        public bool TableStriped { get; set; } = false;
        public bool TableHoverable { get; set; } = true;
        public bool TableBordered { get; set; } = false;
        public bool TableCompact { get; set; } = false;
        public string TableHeaderStyle { get; set; } = "gradient"; // gradient, solid, minimal
        
        // Card Styling Options
        public string CardStyle { get; set; } = "elevated"; // elevated, flat, bordered, glass
        public bool CardShowTopAccent { get; set; } = true;
        public string CardAccentPosition { get; set; } = "top"; // top, left, none
        
        // Button Styling
        public string ButtonStyle { get; set; } = "filled"; // filled, outline, ghost
        public bool ButtonRounded { get; set; } = true;
        
        // Stats Card Options
        public string StatsCardStyle { get; set; } = "gradient"; // gradient, solid, minimal, icon
        public bool StatsShowIcons { get; set; } = false;
        public int StatsColumns { get; set; } = 4;
        
        // Custom CSS
        public string CustomCss { get; set; } = "";
        public string CustomHeadHtml { get; set; } = "";
        public string CustomBodyStartHtml { get; set; } = "";
        public string CustomBodyEndHtml { get; set; } = "";
        
        // SEO Options
        public string MetaDescription { get; set; } = "";
        public string MetaKeywords { get; set; } = "";
        public string OgImage { get; set; } = "";
        public bool GenerateSitemap { get; set; } = false;
        
        // FTP Upload Settings
        public string FtpHost { get; set; } = "";
        public int FtpPort { get; set; } = 21;
        public string FtpUsername { get; set; } = "";
        public string FtpPassword { get; set; } = "";
        public bool UseSftp { get; set; } = false;
        public string RemotePath { get; set; } = "/";
        
        // GitHub Pages Settings
        public string GitHubToken { get; set; } = "";
        public string GitHubUsername { get; set; } = "";
        public string GitHubRepoName { get; set; } = "";
        
        // Generation Settings
        public Guid? SelectedSeasonId { get; set; }
        public string SelectedTemplate { get; set; } = "modern";
        public DateTime LastGenerated { get; set; }
        public DateTime LastUploaded { get; set; }
        
        // Image Settings - Extended Logo Options
        public bool UseCustomLogo { get; set; } = false;
        public int LogoMaxWidth { get; set; } = 300;
        public int LogoMaxHeight { get; set; } = 150;
        public int ImageQuality { get; set; } = 85;
        public string LogoPosition { get; set; } = "above"; // above, below, left, right, top-left, top-right, bottom-left, bottom-right, hidden
        public bool LogoMaintainAspectRatio { get; set; } = true;
        public string? SelectedCatalogLogoId { get; set; }
        
        // Logo Catalog - stored logos that can be reused across website and fixtures sheets
        public List<WebsiteLogoCatalogItem> LogoCatalog { get; set; } = new();
        
        /// <summary>
        /// Get the effective logo data (either custom uploaded or from catalog)
        /// </summary>
        public byte[]? GetEffectiveLogoData()
        {
            // If custom logo data is set, use that
            if (LogoImageData != null && LogoImageData.Length > 0)
                return LogoImageData;
            
            // Otherwise, check if a catalog logo is selected
            if (!string.IsNullOrEmpty(SelectedCatalogLogoId))
            {
                var catalogItem = LogoCatalog.Find(l => l.Id == SelectedCatalogLogoId);
                if (catalogItem != null && catalogItem.ImageData.Length > 0)
                    return catalogItem.ImageData;
            }
            
            return null;
        }
        
        /// <summary>
        /// Get effective layout blocks, migrating from HomeSectionOrder if needed
        /// </summary>
        public List<LayoutBlock> GetEffectiveLayoutBlocks()
        {
            // If we already have blocks with structural items, use them
            if (HomeLayoutBlocks.Count > 0 && HomeLayoutBlocks.Any(b => b.IsStructural))
                return HomeLayoutBlocks.OrderBy(b => b.Order).ToList();
            
            // Migrate: add structural blocks around existing content blocks
            var defaults = LayoutBlock.GetDefaultBlocks();
            var existing = HomeLayoutBlocks.Count > 0
                ? HomeLayoutBlocks.ToList()
                : HomeSectionOrder.Count > 0
                    ? HomeSectionOrder.Select((key, i) =>
                    {
                        var tmpl = defaults.Find(d => d.Id == key);
                        return new LayoutBlock
                        {
                            Id = key, BlockType = key,
                            DisplayName = tmpl?.DisplayName ?? key,
                            Icon = tmpl?.Icon ?? "\U0001F4E6",
                            IsEnabled = true,
                            ColumnSpan = tmpl?.ColumnSpan ?? 2,
                            Order = i
                        };
                    }).ToList()
                    : defaults.Where(b => !b.IsStructural).ToList();
            
            // Build full block list: header, nav, [content...], footer
            var headerBlock = defaults.First(b => b.Id == "header");
            var navBlock = defaults.First(b => b.Id == "nav");
            var footerBlock = defaults.First(b => b.Id == "footer");
            
            var result = new List<LayoutBlock> { headerBlock, navBlock };
            result.AddRange(existing.Where(b => !b.IsStructural));
            result.Add(footerBlock);
            
            for (int i = 0; i < result.Count; i++)
                result[i].Order = i;
            
            LayoutBlock.AutoPositionBlocks(result);
            
            return result;
        }
        
        /// <summary>
        /// Add a logo to the catalog
        /// </summary>
        public void AddLogoCatalogItem(string name, byte[] imageData, string description = "", string category = "General")
        {
            LogoCatalog.Add(new WebsiteLogoCatalogItem
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = description,
                ImageData = imageData,
                Category = category
            });
        }
        
        /// <summary>
        /// Remove a logo from the catalog
        /// </summary>
        public bool RemoveLogoCatalogItem(string id)
        {
            var item = LogoCatalog.Find(l => l.Id == id);
            if (item != null)
            {
                LogoCatalog.Remove(item);
                if (SelectedCatalogLogoId == id)
                    SelectedCatalogLogoId = null;
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Select a logo from the catalog
        /// </summary>
        public void SelectCatalogLogo(string id)
        {
            SelectedCatalogLogoId = id;
            // Clear custom logo when selecting from catalog
            LogoImageData = null;
        }
        
        /// <summary>
        /// Use a custom uploaded logo (clears catalog selection)
        /// </summary>
        public void UseCustomLogoData(byte[] imageData)
        {
            LogoImageData = imageData;
            SelectedCatalogLogoId = null;
            UseCustomLogo = true;
        }
        
        /// <summary>
        /// Available logo position options
        /// </summary>
        public static readonly Dictionary<string, string> LogoPositions = new()
        {
            ["above"] = "Above Title",
            ["below"] = "Below Title",
            ["left"] = "Left of Title",
            ["right"] = "Right of Title",
            ["top-left"] = "Top Left Corner",
            ["top-right"] = "Top Right Corner",
            ["bottom-left"] = "Bottom Left Corner",
            ["bottom-right"] = "Bottom Right Corner",
            ["hidden"] = "Hidden"
        };
        
        // Gallery Settings
        public List<GalleryImage> GalleryImages { get; set; } = new();
        public int GalleryThumbnailSize { get; set; } = 300;
        public int GalleryFullSize { get; set; } = 1200;
        public int GalleryColumns { get; set; } = 3;
        public string GalleryLayout { get; set; } = "grid"; // grid, masonry, carousel
        public bool GalleryShowCaptions { get; set; } = true;
        public bool GalleryShowCategories { get; set; } = true;
        public bool GalleryEnableLightbox { get; set; } = true;
        
        // Sponsor Settings
        public List<Sponsor> Sponsors { get; set; } = new();
        public string SponsorLayout { get; set; } = "grid"; // grid, carousel, list
        public int SponsorLogoMaxHeight { get; set; } = 80;
        
        // News/Announcements
        public List<NewsItem> NewsItems { get; set; } = new();
        public int NewsItemsToShow { get; set; } = 5;
        
        // Rules Content
        public string RulesContent { get; set; } = "";
        
        // Custom Pages
        public List<CustomPage> CustomPages { get; set; } = new();
        
        /// <summary>
        /// Available font families
        /// </summary>
        public static readonly Dictionary<string, string> FontFamilies = new()
        {
            ["Inter"] = "'Inter', -apple-system, BlinkMacSystemFont, sans-serif",
            ["Roboto"] = "'Roboto', -apple-system, BlinkMacSystemFont, sans-serif",
            ["Open Sans"] = "'Open Sans', -apple-system, BlinkMacSystemFont, sans-serif",
            ["Poppins"] = "'Poppins', -apple-system, BlinkMacSystemFont, sans-serif",
            ["Lato"] = "'Lato', -apple-system, BlinkMacSystemFont, sans-serif",
            ["Montserrat"] = "'Montserrat', -apple-system, BlinkMacSystemFont, sans-serif",
            ["Source Sans Pro"] = "'Source Sans Pro', -apple-system, BlinkMacSystemFont, sans-serif",
            ["Nunito"] = "'Nunito', -apple-system, BlinkMacSystemFont, sans-serif",
            ["Raleway"] = "'Raleway', -apple-system, BlinkMacSystemFont, sans-serif",
            ["System"] = "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif"
        };
        
        /// <summary>
        /// Pre-defined color schemes
        /// </summary>
        public static readonly Dictionary<string, ColorScheme> ColorSchemes = new()
        {
            ["classic-blue"] = new("#3B82F6", "#1E40AF", "#F59E0B", "#F8FAFC", "#FFFFFF", "#0F172A", "Classic Blue"),
            ["emerald"] = new("#10B981", "#047857", "#F59E0B", "#F0FDF4", "#FFFFFF", "#064E3B", "Emerald Green"),
            ["purple-haze"] = new("#8B5CF6", "#6D28D9", "#EC4899", "#FAF5FF", "#FFFFFF", "#4C1D95", "Purple Haze"),
            ["sunset"] = new("#F97316", "#DC2626", "#FBBF24", "#FFF7ED", "#FFFFFF", "#7C2D12", "Sunset Orange"),
            ["ocean"] = new("#0EA5E9", "#0284C7", "#06B6D4", "#F0F9FF", "#FFFFFF", "#0C4A6E", "Ocean Blue"),
            ["forest"] = new("#22C55E", "#15803D", "#84CC16", "#F0FDF4", "#FFFFFF", "#14532D", "Forest Green"),
            ["slate"] = new("#64748B", "#475569", "#0EA5E9", "#F8FAFC", "#FFFFFF", "#1E293B", "Slate Gray"),
            ["rose"] = new("#F43F5E", "#BE123C", "#FB7185", "#FFF1F2", "#FFFFFF", "#881337", "Rose Red"),
            ["amber"] = new("#F59E0B", "#D97706", "#FBBF24", "#FFFBEB", "#FFFFFF", "#78350F", "Amber Gold"),
            ["teal"] = new("#14B8A6", "#0D9488", "#2DD4BF", "#F0FDFA", "#FFFFFF", "#134E4A", "Teal"),
            ["indigo"] = new("#6366F1", "#4F46E5", "#A5B4FC", "#EEF2FF", "#FFFFFF", "#312E81", "Indigo"),
            ["pink"] = new("#EC4899", "#DB2777", "#F472B6", "#FDF2F8", "#FFFFFF", "#831843", "Pink"),
            ["dark"] = new("#3B82F6", "#1E40AF", "#F59E0B", "#0F172A", "#1E293B", "#F1F5F9", "Dark Mode"),
            ["midnight"] = new("#8B5CF6", "#7C3AED", "#F472B6", "#020617", "#0F172A", "#E2E8F0", "Midnight"),
        };
        
        /// <summary>
        /// Apply a pre-defined color scheme
        /// </summary>
        public void ApplyColorScheme(string schemeId)
        {
            if (ColorSchemes.TryGetValue(schemeId, out var scheme))
            {
                PrimaryColor = scheme.Primary;
                SecondaryColor = scheme.Secondary;
                AccentColor = scheme.Accent;
                BackgroundColor = scheme.Background;
                CardBackgroundColor = scheme.CardBackground;
                TextColor = scheme.Text;
            }
        }
        
        /// <summary>
        /// Reset to default values
        /// </summary>
        public void ResetToDefaults()
        {
            // Branding
            LeagueName = "My Pool League";
            LeagueSubtitle = "Weekly 8-Ball Pool Competition";
            LogoPath = null;
            LogoImageData = null;
            FaviconUrl = "";
            
            // Contact
            WelcomeMessage = "";
            AboutText = "";
            ContactEmail = "";
            ContactPhone = "";
            ContactAddress = "";
            
            // Social
            FacebookUrl = "";
            TwitterUrl = "";
            InstagramUrl = "";
            YouTubeUrl = "";
            TikTokUrl = "";
            WebsiteUrl = "";
            
            // Colors
            PrimaryColor = "#3B82F6";
            SecondaryColor = "#10B981";
            AccentColor = "#F59E0B";
            BackgroundColor = "#F8FAFC";
            CardBackgroundColor = "#FFFFFF";
            TextColor = "#0F172A";
            TextSecondaryColor = "#64748B";
            HeaderTextColor = "#FFFFFF";
            
            // Theme options
            EnableAnimations = true;
            EnableGradients = true;
            EnableShadows = true;
            EnableRoundedCorners = true;
            ShowLastUpdated = true;
            FontFamily = "Inter";
            HeaderFontFamily = "Inter";
            BaseFontSize = 16;
            BorderRadius = 12;
            CardSpacing = 24;
            
            // Page Layout
            MaxContentWidth = 1200;
            SectionSpacing = 24;
            PageLayout = "full-width";
            SidebarWidth = 320;
            HomeSectionOrder = new List<string>
            {
                "welcome", "quick-stats", "league-leaders", "recent-results", "upcoming-fixtures", "latest-news", "sponsors"
            };
            HomeLayoutBlocks = LayoutBlock.GetDefaultBlocks();

            
            // Buttons
            ButtonStyle = "filled";
            ButtonRounded = true;
            
            // Header
            HeaderStyle = "gradient";
            HeaderBackgroundImage = "";
            HeaderBackgroundImageData = null;
            ShowHeaderPattern = true;
            ShowSeasonBadge = true;
            HeaderAlignment = "center";
            
            // Navigation
            NavStyle = "pills";
            NavSticky = true;
            ShowNavIcons = false;
            NavPosition = "center";
            
            // Footer
            FooterStyle = "dark";
            ShowFooterSocialLinks = true;
            ShowFooterContact = true;
            ShowPoweredBy = true;
            CustomFooterText = "";
            CopyrightText = "";
            
            // Content toggles
            ShowStandings = true;
            ShowFixtures = true;
            ShowResults = true;
            ShowPlayerStats = true;
            ShowDivisions = true;
            ShowGallery = false;
            ShowTopScorers = true;
            ShowRecentForm = true;
            ShowNews = false;
            ShowSponsors = false;
            ShowRules = false;
            ShowContactPage = false;
            
            // Home page
            HomeShowWelcomeSection = true;
            HomeShowQuickStats = true;
            HomeShowRecentResults = true;
            HomeShowUpcomingFixtures = true;
            HomeShowLeagueLeaders = true;
            HomeShowLatestNews = false;
            HomeShowSponsors = false;
            HomeRecentResultsCount = 5;
            HomeUpcomingFixturesCount = 5;
            HomeLeagueLeadersCount = 5;
            HomeLayout = "standard";
            
            // Standings
            StandingsShowPosition = true;
            StandingsShowPlayed = true;
            StandingsShowWon = true;
            StandingsShowDrawn = true;
            StandingsShowLost = true;
            StandingsShowFramesFor = true;
            StandingsShowFramesAgainst = true;
            StandingsShowFramesDiff = true;
            StandingsShowPoints = true;
            StandingsShowForm = true;
            StandingsShowMedals = true;
            StandingsHighlightTop = true;
            StandingsHighlightBottom = false;
            StandingsHighlightTopCount = 3;
            StandingsHighlightBottomCount = 2;
            
            // Results
            ResultsShowScore = true;
            ResultsShowVenue = true;
            ResultsShowDivision = true;
            ResultsShowDate = true;
            ResultsShowTime = true;
            ResultsHighlightWinner = true;
            ResultsGroupByDate = true;
            ResultsShowFrameDetails = false;
            ResultsPerPage = 20;
            ResultsHomeTeamAlign = "right";
            ResultsAwayTeamAlign = "left";
            ResultsScoreAlign = "center";
            ResultsMobileTeamAlign = "center";
            
            // Fixtures
            FixturesShowVenue = true;
            FixturesShowDivision = true;
            FixturesShowDate = true;
            FixturesShowTime = true;
            FixturesGroupByDate = true;
            FixturesShowCountdown = false;
            FixturesPerPage = 20;
            FixturesHomeTeamAlign = "right";
            FixturesAwayTeamAlign = "left";
            FixturesVsAlign = "center";
            
            // Players
            PlayersShowPosition = true;
            PlayersShowTeam = true;
            PlayersShowPlayed = true;
            PlayersShowWon = true;
            PlayersShowLost = true;
            PlayersShowWinPercentage = true;
            PlayersShowEightBalls = true;
            PlayersShowAverage = false;
            PlayersShowForm = false;
            PlayersShowRating = true;
            PlayersMinGames = 0;
            PlayersMinFramesPercentage = 0;
            PlayersUsePercentageFilter = false;
            PlayersPerPage = 50;
            
            // Divisions
            DivisionsShowTeamCount = true;
            DivisionsShowPlayerCount = true;
            DivisionsShowDescription = true;
            DivisionsShowTeamList = true;
            DivisionsLayout = "cards";
            
            // Table styling
            TableStriped = false;
            TableHoverable = true;
            TableBordered = false;
            TableCompact = false;
            TableHeaderStyle = "gradient";
            
            // Card styling
            CardStyle = "elevated";
            CardShowTopAccent = true;
            CardAccentPosition = "top";
            
            // Button styling
            ButtonStyle = "filled";
            ButtonRounded = true;
            
            // Stats cards
            StatsCardStyle = "gradient";
            StatsShowIcons = false;
            StatsColumns = 4;
            
            // Custom code
            CustomCss = "";
            CustomHeadHtml = "";
            CustomBodyStartHtml = "";
            CustomBodyEndHtml = "";
            
            // SEO
            MetaDescription = "";
            MetaKeywords = "";
            OgImage = "";
            GenerateSitemap = false;
            
            // Template
            SelectedTemplate = "modern";
            
            // Images - Extended Logo Options
            UseCustomLogo = false;
            LogoMaxWidth = 300;
            LogoMaxHeight = 150;
            ImageQuality = 85;
            LogoPosition = "above";
            LogoMaintainAspectRatio = true;
            SelectedCatalogLogoId = null;
            // Note: Don't clear LogoCatalog on reset - keep saved logos
            
            // Gallery
            GalleryImages.Clear();
            GalleryThumbnailSize = 300;
            GalleryFullSize = 1200;
            GalleryColumns = 3;
            GalleryLayout = "grid";
            GalleryShowCaptions = true;
            GalleryShowCategories = true;
            GalleryEnableLightbox = true;
            
            // Sponsors
            Sponsors.Clear();
            SponsorLayout = "grid";
            SponsorLogoMaxHeight = 80;
            
            // News
            NewsItems.Clear();
            NewsItemsToShow = 5;
            
            // Rules
            RulesContent = "";
            
            // Custom pages
            CustomPages.Clear();
            
            // GitHub
            GitHubToken = "";
            GitHubUsername = "";
            GitHubRepoName = "";
        }
        
        /// <summary>
        /// Check if any social links are configured
        /// </summary>
        public bool HasSocialLinks => 
            !string.IsNullOrWhiteSpace(FacebookUrl) || 
            !string.IsNullOrWhiteSpace(TwitterUrl) || 
            !string.IsNullOrWhiteSpace(InstagramUrl) ||
            !string.IsNullOrWhiteSpace(YouTubeUrl) ||
            !string.IsNullOrWhiteSpace(TikTokUrl);
        
        /// <summary>
        /// Check if contact info is configured
        /// </summary>
        public bool HasContactInfo => 
            !string.IsNullOrWhiteSpace(ContactEmail) || 
            !string.IsNullOrWhiteSpace(ContactPhone) ||
            !string.IsNullOrWhiteSpace(ContactAddress);
    }
    
    /// <summary>
    /// Logo catalog item for website (separate from fixtures sheet to allow independent catalogs)
    /// </summary>
    public sealed class WebsiteLogoCatalogItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public string Category { get; set; } = "General";
        public DateTime DateAdded { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// Color scheme definition
    /// </summary>
    public sealed class ColorScheme
    {
        public string Primary { get; set; }
        public string Secondary { get; set; }
        public string Accent { get; set; }
        public string Background { get; set; }
        public string CardBackground { get; set; }
        public string Text { get; set; }
        public string Name { get; set; }
        
        public ColorScheme(string primary, string secondary, string accent, string background, string cardBackground, string text, string name)
        {
            Primary = primary;
            Secondary = secondary;
            Accent = accent;
            Background = background;
            CardBackground = cardBackground;
            Text = text;
            Name = name;
        }
    }
    
    /// <summary>
    /// Gallery image with metadata
    /// </summary>
    public sealed class GalleryImage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FileName { get; set; } = "";
        public string Caption { get; set; } = "";
        public string Category { get; set; } = "General";
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public DateTime DateAdded { get; set; } = DateTime.Now;
        public int Width { get; set; }
        public int Height { get; set; }
        public int SortOrder { get; set; }
    }
    
    /// <summary>
    /// Sponsor definition
    /// </summary>
    public sealed class Sponsor
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string LogoFileName { get; set; } = "";
        public byte[] LogoData { get; set; } = Array.Empty<byte>();
        public string WebsiteUrl { get; set; } = "";
        public string Description { get; set; } = "";
        public string Tier { get; set; } = "Standard"; // Platinum, Gold, Silver, Standard
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }
    
    /// <summary>
    /// News/Announcement item
    /// </summary>
    public sealed class NewsItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Summary { get; set; } = "";
        public DateTime DatePublished { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; } = false;
        public bool IsPublished { get; set; } = true;
        public string Category { get; set; } = "General";
        public byte[]? ImageData { get; set; }
    }
    
    /// <summary>
    /// Custom page definition
    /// </summary>
    public sealed class CustomPage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = "";
        public string Slug { get; set; } = "";
        public string Content { get; set; } = "";
        public bool ShowInNav { get; set; } = true;
        public int NavOrder { get; set; }
        public bool IsPublished { get; set; } = true;
    }
}
