using System;
using System.Collections.Generic;

namespace Wdpl2.Models
{
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
        
        // Header Options
        public string HeaderStyle { get; set; } = "gradient"; // gradient, solid, image, minimal
        public string HeaderBackgroundImage { get; set; } = "";
        public byte[]? HeaderBackgroundImageData { get; set; }
        public bool ShowHeaderPattern { get; set; } = true;
        public bool ShowSeasonBadge { get; set; } = true;
        public string HeaderAlignment { get; set; } = "center"; // left, center, right
        
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
        public int PlayersMinGames { get; set; } = 0;
        public int PlayersPerPage { get; set; } = 50;
        public string PlayersSortBy { get; set; } = "winpercentage"; // winpercentage, won, played, eightballs
        
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
        
        // Image Settings
        public bool UseCustomLogo { get; set; } = false;
        public int LogoMaxWidth { get; set; } = 300;
        public int LogoMaxHeight { get; set; } = 150;
        public int ImageQuality { get; set; } = 85;
        public string LogoPosition { get; set; } = "above"; // above, left, right, hidden
        
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
            
            // Fixtures
            FixturesShowVenue = true;
            FixturesShowDivision = true;
            FixturesShowDate = true;
            FixturesShowTime = true;
            FixturesGroupByDate = true;
            FixturesShowCountdown = false;
            FixturesPerPage = 20;
            
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
            PlayersMinGames = 0;
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
            
            // Images
            UseCustomLogo = false;
            LogoMaxWidth = 300;
            LogoMaxHeight = 150;
            ImageQuality = 85;
            LogoPosition = "above";
            
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
