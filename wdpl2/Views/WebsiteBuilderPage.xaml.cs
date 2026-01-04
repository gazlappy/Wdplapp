using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views
{
    public partial class WebsiteBuilderPage : ContentPage
    {
        private static LeagueData League => DataStore.Data;
        private readonly ObservableCollection<Season> _seasons = new();
        private readonly ObservableCollection<WebsiteTemplate> _templates = new();
        private Dictionary<string, string>? _generatedFiles;
        private byte[]? _uploadedLogoData;
        
        // Deployment method tracking
        private enum DeploymentMethod { LocalExport, GitHubPages, FTP }
        private DeploymentMethod _selectedDeploymentMethod = DeploymentMethod.LocalExport;
        
        public WebsiteBuilderPage()
        {
            InitializeComponent();
            
            SeasonPicker.ItemsSource = _seasons;
            SeasonPicker.ItemDisplayBinding = new Binding("Name");
            
            TemplatePicker.ItemsSource = _templates;
            TemplatePicker.ItemDisplayBinding = new Binding("Name");
            
            // Setup preview page picker
            PreviewPagePicker.ItemsSource = new[] 
            { 
                "Home", "Standings", "Fixtures", "Results", "Players", "Divisions" 
            };
            
            // Setup color scheme picker
            ColorSchemePicker.ItemsSource = WebsiteSettings.ColorSchemes.Select(cs => cs.Value.Name).ToList();
            
            // Wire up GitHub URL preview updates
            GitHubUsernameEntry.TextChanged += (_, _) => UpdateGitHubUrlPreview();
            GitHubRepoEntry.TextChanged += (_, _) => UpdateGitHubUrlPreview();
            
            LoadSettings();
            
            // Default to Local Export
            DeploymentMethodPicker.SelectedIndex = 0;
        }
        
        #region Collapsible Section Toggles
        
        private void ToggleSection(VerticalStackLayout content, Label expandIcon)
        {
            content.IsVisible = !content.IsVisible;
            expandIcon.Text = content.IsVisible ? "?" : "?";
        }
        
        private void OnBrandingSectionTapped(object sender, EventArgs e)
            => ToggleSection(BrandingContent, BrandingExpandIcon);
        
        private void OnContactSectionTapped(object sender, EventArgs e)
            => ToggleSection(ContactContent, ContactExpandIcon);
        
        private void OnColorsSectionTapped(object sender, EventArgs e)
            => ToggleSection(ColorsContent, ColorsExpandIcon);
        
        private void OnLayoutSectionTapped(object sender, EventArgs e)
            => ToggleSection(LayoutContent, LayoutExpandIcon);
        
        private void OnContentSectionTapped(object sender, EventArgs e)
            => ToggleSection(ContentContent, ContentExpandIcon);
        
        private void OnGallerySectionTapped(object sender, EventArgs e)
            => ToggleSection(GalleryContent, GalleryExpandIcon);
        
        private void OnSeoSectionTapped(object sender, EventArgs e)
            => ToggleSection(SeoContent, SeoExpandIcon);
        
        private void OnDeploymentSectionTapped(object sender, EventArgs e)
            => ToggleSection(DeploymentContent, DeploymentExpandIcon);
        
        #endregion
        
        private void LoadSettings()
        {
            // Load seasons
            _seasons.Clear();
            foreach (var season in League.Seasons.OrderByDescending(s => s.StartDate))
            {
                _seasons.Add(season);
            }
            
            // Load templates
            _templates.Clear();
            foreach (var template in WebsiteTemplate.GetAllTemplates())
            {
                _templates.Add(template);
            }
            
            // Load saved settings
            var settings = League.WebsiteSettings;
            
            // Site Branding
            LeagueNameEntry.Text = settings.LeagueName;
            SubtitleEntry.Text = settings.LeagueSubtitle;
            SetPickerValue(LogoPositionPicker, settings.LogoPosition);
            
            // Contact & About
            WelcomeMessageEditor.Text = settings.WelcomeMessage;
            AboutTextEditor.Text = settings.AboutText;
            ContactEmailEntry.Text = settings.ContactEmail;
            ContactPhoneEntry.Text = settings.ContactPhone;
            ContactAddressEntry.Text = settings.ContactAddress;
            
            // Social Media
            FacebookUrlEntry.Text = settings.FacebookUrl;
            TwitterUrlEntry.Text = settings.TwitterUrl;
            InstagramUrlEntry.Text = settings.InstagramUrl;
            YouTubeUrlEntry.Text = settings.YouTubeUrl;
            TikTokUrlEntry.Text = settings.TikTokUrl;
            WebsiteUrlEntry.Text = settings.WebsiteUrl;
            
            // Colors
            PrimaryColorEntry.Text = settings.PrimaryColor;
            SecondaryColorEntry.Text = settings.SecondaryColor;
            AccentColorEntry.Text = settings.AccentColor;
            BackgroundColorEntry.Text = settings.BackgroundColor;
            CardBackgroundColorEntry.Text = settings.CardBackgroundColor;
            TextColorEntry.Text = settings.TextColor;
            TextSecondaryColorEntry.Text = settings.TextSecondaryColor;
            HeaderTextColorEntry.Text = settings.HeaderTextColor;
            
            // Theme Options
            EnableAnimationsCheck.IsChecked = settings.EnableAnimations;
            EnableGradientsCheck.IsChecked = settings.EnableGradients;
            EnableShadowsCheck.IsChecked = settings.EnableShadows;
            EnableRoundedCornersCheck.IsChecked = settings.EnableRoundedCorners;
            ShowLastUpdatedCheck.IsChecked = settings.ShowLastUpdated;
            SetPickerValue(FontFamilyPicker, settings.FontFamily);
            SetPickerValue(HeaderFontFamilyPicker, settings.HeaderFontFamily);
            BaseFontSizeEntry.Text = settings.BaseFontSize.ToString();
            BorderRadiusEntry.Text = settings.BorderRadius.ToString();
            
            // Header Options
            SetPickerValue(HeaderStylePicker, settings.HeaderStyle);
            SetPickerValue(HeaderAlignmentPicker, settings.HeaderAlignment);
            ShowHeaderPatternCheck.IsChecked = settings.ShowHeaderPattern;
            ShowSeasonBadgeCheck.IsChecked = settings.ShowSeasonBadge;
            
            // Navigation Options
            SetPickerValue(NavStylePicker, settings.NavStyle);
            SetPickerValue(NavPositionPicker, settings.NavPosition);
            NavStickyCheck.IsChecked = settings.NavSticky;
            ShowNavIconsCheck.IsChecked = settings.ShowNavIcons;
            
            // Footer Options
            SetPickerValue(FooterStylePicker, settings.FooterStyle);
            ShowFooterSocialLinksCheck.IsChecked = settings.ShowFooterSocialLinks;
            ShowFooterContactCheck.IsChecked = settings.ShowFooterContact;
            ShowPoweredByCheck.IsChecked = settings.ShowPoweredBy;
            CustomFooterTextEntry.Text = settings.CustomFooterText;
            CopyrightTextEntry.Text = settings.CopyrightText;
            
            // Content Toggles
            ShowStandingsCheck.IsChecked = settings.ShowStandings;
            ShowFixturesCheck.IsChecked = settings.ShowFixtures;
            ShowResultsCheck.IsChecked = settings.ShowResults;
            ShowPlayerStatsCheck.IsChecked = settings.ShowPlayerStats;
            ShowDivisionsCheck.IsChecked = settings.ShowDivisions;
            ShowGalleryCheck.IsChecked = settings.ShowGallery;
            ShowTopScorersCheck.IsChecked = settings.ShowTopScorers;
            ShowRecentFormCheck.IsChecked = settings.ShowRecentForm;
            ShowNewsCheck.IsChecked = settings.ShowNews;
            ShowSponsorsCheck.IsChecked = settings.ShowSponsors;
            ShowRulesCheck.IsChecked = settings.ShowRules;
            ShowContactPageCheck.IsChecked = settings.ShowContactPage;
            
            // Home Page Options
            SetPickerValue(HomeLayoutPicker, settings.HomeLayout);
            HomeShowWelcomeSectionCheck.IsChecked = settings.HomeShowWelcomeSection;
            HomeShowQuickStatsCheck.IsChecked = settings.HomeShowQuickStats;
            HomeShowRecentResultsCheck.IsChecked = settings.HomeShowRecentResults;
            HomeShowUpcomingFixturesCheck.IsChecked = settings.HomeShowUpcomingFixtures;
            HomeShowLeagueLeadersCheck.IsChecked = settings.HomeShowLeagueLeaders;
            HomeShowLatestNewsCheck.IsChecked = settings.HomeShowLatestNews;
            HomeShowSponsorsCheck.IsChecked = settings.HomeShowSponsors;
            HomeRecentResultsCountEntry.Text = settings.HomeRecentResultsCount.ToString();
            HomeUpcomingFixturesCountEntry.Text = settings.HomeUpcomingFixturesCount.ToString();
            HomeLeagueLeadersCountEntry.Text = settings.HomeLeagueLeadersCount.ToString();
            
            // Table & Card Styling
            TableStripedCheck.IsChecked = settings.TableStriped;
            TableHoverableCheck.IsChecked = settings.TableHoverable;
            TableBorderedCheck.IsChecked = settings.TableBordered;
            TableCompactCheck.IsChecked = settings.TableCompact;
            SetPickerValue(TableHeaderStylePicker, settings.TableHeaderStyle);
            SetPickerValue(CardStylePicker, settings.CardStyle);
            SetPickerValue(CardAccentPositionPicker, settings.CardAccentPosition);
            CardShowTopAccentCheck.IsChecked = settings.CardShowTopAccent;
            
            // SEO Options
            MetaDescriptionEditor.Text = settings.MetaDescription;
            MetaKeywordsEntry.Text = settings.MetaKeywords;
            GenerateSitemapCheck.IsChecked = settings.GenerateSitemap;
            
            // Gallery Options
            SetPickerValue(GalleryLayoutPicker, settings.GalleryLayout);
            GalleryColumnsEntry.Text = settings.GalleryColumns.ToString();
            GalleryShowCaptionsCheck.IsChecked = settings.GalleryShowCaptions;
            GalleryShowCategoriesCheck.IsChecked = settings.GalleryShowCategories;
            GalleryEnableLightboxCheck.IsChecked = settings.GalleryEnableLightbox;
            
            // Custom CSS
            CustomCssEditor.Text = settings.CustomCss;
            
            // Update gallery count
            UpdateGalleryCount();
            
            // FTP Settings
            FtpHostEntry.Text = settings.FtpHost;
            FtpPortEntry.Text = settings.FtpPort.ToString();
            FtpUsernameEntry.Text = settings.FtpUsername;
            FtpPasswordEntry.Text = settings.FtpPassword;
            FtpPathEntry.Text = settings.RemotePath;
            
            // GitHub Settings
            GitHubTokenEntry.Text = settings.GitHubToken;
            GitHubUsernameEntry.Text = settings.GitHubUsername;
            GitHubRepoEntry.Text = settings.GitHubRepoName;
            
            // Load logo if exists
            if (settings.LogoImageData != null && settings.UseCustomLogo)
            {
                _uploadedLogoData = settings.LogoImageData;
                LogoStatusLabel.Text = "? Logo loaded";
                LogoStatusLabel.TextColor = Color.FromArgb("#10B981");
                LogoPreviewImage.Source = ImageSource.FromStream(() => new System.IO.MemoryStream(_uploadedLogoData));
                LogoPreviewImage.IsVisible = true;
            }
            
            // Select active season by default
            if (settings.SelectedSeasonId.HasValue)
            {
                var season = _seasons.FirstOrDefault(s => s.Id == settings.SelectedSeasonId.Value);
                if (season != null)
                {
                    SeasonPicker.SelectedItem = season;
                }
            }
            else
            {
                var activeSeason = _seasons.FirstOrDefault(s => s.IsActive);
                if (activeSeason != null)
                {
                    SeasonPicker.SelectedItem = activeSeason;
                }
            }
            
            // Select template
            var selectedTemplate = _templates.FirstOrDefault(t => t.Id == settings.SelectedTemplate);
            if (selectedTemplate != null)
            {
                TemplatePicker.SelectedItem = selectedTemplate;
            }
            else
            {
                TemplatePicker.SelectedItem = _templates.FirstOrDefault();
            }
        }
        
        private void SetPickerValue(Picker picker, string value)
        {
            if (picker.ItemsSource is IList<string> items)
            {
                var index = items.IndexOf(value);
                if (index >= 0)
                {
                    picker.SelectedIndex = index;
                }
            }
        }
        
        private string GetPickerValue(Picker picker, string defaultValue)
        {
            return picker.SelectedItem?.ToString() ?? defaultValue;
        }
        
        private void SaveCurrentSettings()
        {
            var settings = League.WebsiteSettings;
            
            // Site Branding
            settings.LeagueName = LeagueNameEntry.Text?.Trim() ?? "My Pool League";
            settings.LeagueSubtitle = SubtitleEntry.Text?.Trim() ?? "Weekly 8-Ball Competition";
            settings.LogoPosition = GetPickerValue(LogoPositionPicker, "above");
            
            // Contact & About
            settings.WelcomeMessage = WelcomeMessageEditor.Text?.Trim() ?? "";
            settings.AboutText = AboutTextEditor.Text?.Trim() ?? "";
            settings.ContactEmail = ContactEmailEntry.Text?.Trim() ?? "";
            settings.ContactPhone = ContactPhoneEntry.Text?.Trim() ?? "";
            settings.ContactAddress = ContactAddressEntry.Text?.Trim() ?? "";
            
            // Social Media
            settings.FacebookUrl = FacebookUrlEntry.Text?.Trim() ?? "";
            settings.TwitterUrl = TwitterUrlEntry.Text?.Trim() ?? "";
            settings.InstagramUrl = InstagramUrlEntry.Text?.Trim() ?? "";
            settings.YouTubeUrl = YouTubeUrlEntry.Text?.Trim() ?? "";
            settings.TikTokUrl = TikTokUrlEntry.Text?.Trim() ?? "";
            settings.WebsiteUrl = WebsiteUrlEntry.Text?.Trim() ?? "";
            
            // Colors
            settings.PrimaryColor = PrimaryColorEntry.Text?.Trim() ?? "#3B82F6";
            settings.SecondaryColor = SecondaryColorEntry.Text?.Trim() ?? "#10B981";
            settings.AccentColor = AccentColorEntry.Text?.Trim() ?? "#F59E0B";
            settings.BackgroundColor = BackgroundColorEntry.Text?.Trim() ?? "#F8FAFC";
            settings.CardBackgroundColor = CardBackgroundColorEntry.Text?.Trim() ?? "#FFFFFF";
            settings.TextColor = TextColorEntry.Text?.Trim() ?? "#0F172A";
            settings.TextSecondaryColor = TextSecondaryColorEntry.Text?.Trim() ?? "#64748B";
            settings.HeaderTextColor = HeaderTextColorEntry.Text?.Trim() ?? "#FFFFFF";
            
            // Theme Options
            settings.EnableAnimations = EnableAnimationsCheck.IsChecked;
            settings.EnableGradients = EnableGradientsCheck.IsChecked;
            settings.EnableShadows = EnableShadowsCheck.IsChecked;
            settings.EnableRoundedCorners = EnableRoundedCornersCheck.IsChecked;
            settings.ShowLastUpdated = ShowLastUpdatedCheck.IsChecked;
            settings.FontFamily = GetPickerValue(FontFamilyPicker, "Inter");
            settings.HeaderFontFamily = GetPickerValue(HeaderFontFamilyPicker, "Inter");
            if (int.TryParse(BaseFontSizeEntry.Text, out int fontSize))
                settings.BaseFontSize = fontSize;
            if (int.TryParse(BorderRadiusEntry.Text, out int borderRadius))
                settings.BorderRadius = borderRadius;
            
            // Header Options
            settings.HeaderStyle = GetPickerValue(HeaderStylePicker, "gradient");
            settings.HeaderAlignment = GetPickerValue(HeaderAlignmentPicker, "center");
            settings.ShowHeaderPattern = ShowHeaderPatternCheck.IsChecked;
            settings.ShowSeasonBadge = ShowSeasonBadgeCheck.IsChecked;
            
            // Navigation Options
            settings.NavStyle = GetPickerValue(NavStylePicker, "pills");
            settings.NavPosition = GetPickerValue(NavPositionPicker, "center");
            settings.NavSticky = NavStickyCheck.IsChecked;
            settings.ShowNavIcons = ShowNavIconsCheck.IsChecked;
            
            // Footer Options
            settings.FooterStyle = GetPickerValue(FooterStylePicker, "dark");
            settings.ShowFooterSocialLinks = ShowFooterSocialLinksCheck.IsChecked;
            settings.ShowFooterContact = ShowFooterContactCheck.IsChecked;
            settings.ShowPoweredBy = ShowPoweredByCheck.IsChecked;
            settings.CustomFooterText = CustomFooterTextEntry.Text?.Trim() ?? "";
            settings.CopyrightText = CopyrightTextEntry.Text?.Trim() ?? "";
            
            // Content Toggles
            settings.ShowStandings = ShowStandingsCheck.IsChecked;
            settings.ShowFixtures = ShowFixturesCheck.IsChecked;
            settings.ShowResults = ShowResultsCheck.IsChecked;
            settings.ShowPlayerStats = ShowPlayerStatsCheck.IsChecked;
            settings.ShowDivisions = ShowDivisionsCheck.IsChecked;
            settings.ShowGallery = ShowGalleryCheck.IsChecked;
            settings.ShowTopScorers = ShowTopScorersCheck.IsChecked;
            settings.ShowRecentForm = ShowRecentFormCheck.IsChecked;
            settings.ShowNews = ShowNewsCheck.IsChecked;
            settings.ShowSponsors = ShowSponsorsCheck.IsChecked;
            settings.ShowRules = ShowRulesCheck.IsChecked;
            settings.ShowContactPage = ShowContactPageCheck.IsChecked;
            
            // Home Page Options
            settings.HomeLayout = GetPickerValue(HomeLayoutPicker, "standard");
            settings.HomeShowWelcomeSection = HomeShowWelcomeSectionCheck.IsChecked;
            settings.HomeShowQuickStats = HomeShowQuickStatsCheck.IsChecked;
            settings.HomeShowRecentResults = HomeShowRecentResultsCheck.IsChecked;
            settings.HomeShowUpcomingFixtures = HomeShowUpcomingFixturesCheck.IsChecked;
            settings.HomeShowLeagueLeaders = HomeShowLeagueLeadersCheck.IsChecked;
            settings.HomeShowLatestNews = HomeShowLatestNewsCheck.IsChecked;
            settings.HomeShowSponsors = HomeShowSponsorsCheck.IsChecked;
            if (int.TryParse(HomeRecentResultsCountEntry.Text, out int recentResultsCount))
                settings.HomeRecentResultsCount = recentResultsCount;
            if (int.TryParse(HomeUpcomingFixturesCountEntry.Text, out int upcomingFixturesCount))
                settings.HomeUpcomingFixturesCount = upcomingFixturesCount;
            if (int.TryParse(HomeLeagueLeadersCountEntry.Text, out int leagueLeadersCount))
                settings.HomeLeagueLeadersCount = leagueLeadersCount;
            
            // Table & Card Styling
            settings.TableStriped = TableStripedCheck.IsChecked;
            settings.TableHoverable = TableHoverableCheck.IsChecked;
            settings.TableBordered = TableBorderedCheck.IsChecked;
            settings.TableCompact = TableCompactCheck.IsChecked;
            settings.TableHeaderStyle = GetPickerValue(TableHeaderStylePicker, "gradient");
            settings.CardStyle = GetPickerValue(CardStylePicker, "elevated");
            settings.CardAccentPosition = GetPickerValue(CardAccentPositionPicker, "top");
            settings.CardShowTopAccent = CardShowTopAccentCheck.IsChecked;
            
            // SEO Options
            settings.MetaDescription = MetaDescriptionEditor.Text?.Trim() ?? "";
            settings.MetaKeywords = MetaKeywordsEntry.Text?.Trim() ?? "";
            settings.GenerateSitemap = GenerateSitemapCheck.IsChecked;
            
            // Gallery Options
            settings.GalleryLayout = GetPickerValue(GalleryLayoutPicker, "grid");
            if (int.TryParse(GalleryColumnsEntry.Text, out int galleryColumns))
                settings.GalleryColumns = galleryColumns;
            settings.GalleryShowCaptions = GalleryShowCaptionsCheck.IsChecked;
            settings.GalleryShowCategories = GalleryShowCategoriesCheck.IsChecked;
            settings.GalleryEnableLightbox = GalleryEnableLightboxCheck.IsChecked;
            
            // Custom CSS
            settings.CustomCss = CustomCssEditor.Text?.Trim() ?? "";
            
            // FTP Settings
            settings.FtpHost = FtpHostEntry.Text?.Trim() ?? "";
            if (int.TryParse(FtpPortEntry.Text, out int port))
            {
                settings.FtpPort = port;
            }
            settings.FtpUsername = FtpUsernameEntry.Text?.Trim() ?? "";
            settings.FtpPassword = FtpPasswordEntry.Text?.Trim() ?? "";
            settings.RemotePath = FtpPathEntry.Text?.Trim() ?? "/public_html/";
            
            // GitHub Settings
            settings.GitHubToken = GitHubTokenEntry.Text?.Trim() ?? "";
            settings.GitHubUsername = GitHubUsernameEntry.Text?.Trim() ?? "";
            settings.GitHubRepoName = GitHubRepoEntry.Text?.Trim() ?? "";
            
            var selectedSeason = SeasonPicker.SelectedItem as Season;
            settings.SelectedSeasonId = selectedSeason?.Id;
            
            var selectedTemplate = TemplatePicker.SelectedItem as WebsiteTemplate;
            settings.SelectedTemplate = selectedTemplate?.Id ?? "modern";
            
            // Save logo data if uploaded
            if (_uploadedLogoData != null)
            {
                settings.LogoImageData = _uploadedLogoData;
                settings.UseCustomLogo = true;
            }
        }
        
        private void OnColorSchemeChanged(object sender, EventArgs e)
        {
            if (ColorSchemePicker.SelectedIndex < 0) return;
            
            var selectedName = ColorSchemePicker.SelectedItem?.ToString();
            var scheme = WebsiteSettings.ColorSchemes.FirstOrDefault(cs => cs.Value.Name == selectedName);
            
            if (scheme.Value != null)
            {
                PrimaryColorEntry.Text = scheme.Value.Primary;
                SecondaryColorEntry.Text = scheme.Value.Secondary;
                AccentColorEntry.Text = scheme.Value.Accent;
                BackgroundColorEntry.Text = scheme.Value.Background;
                CardBackgroundColorEntry.Text = scheme.Value.CardBackground;
                TextColorEntry.Text = scheme.Value.Text;
            }
        }
        
        private void OnDeploymentMethodChanged(object sender, EventArgs e)
        {
            var selectedIndex = DeploymentMethodPicker.SelectedIndex;
            
            // Hide all deployment panels
            LocalExportFrame.IsVisible = false;
            GitHubFrame.IsVisible = false;
            FtpFrame.IsVisible = false;
            
            // Show selected panel
            switch (selectedIndex)
            {
                case 0: // Local Export
                    LocalExportFrame.IsVisible = true;
                    _selectedDeploymentMethod = DeploymentMethod.LocalExport;
                    break;
                case 1: // GitHub Pages
                    GitHubFrame.IsVisible = true;
                    _selectedDeploymentMethod = DeploymentMethod.GitHubPages;
                    UpdateGitHubUrlPreview();
                    break;
                case 2: // FTP
                    FtpFrame.IsVisible = true;
                    _selectedDeploymentMethod = DeploymentMethod.FTP;
                    break;
            }
        }
        
        private void UpdateGitHubUrlPreview()
        {
            var username = GitHubUsernameEntry.Text?.Trim() ?? "username";
            var repo = GitHubRepoEntry.Text?.Trim() ?? "repo";
            
            if (string.IsNullOrWhiteSpace(username)) username = "username";
            if (string.IsNullOrWhiteSpace(repo)) repo = "repo";
            
            GitHubUrlPreview.Text = $"Your site: https://{username}.github.io/{repo}/";
        }
        
        #region Local Export
        
        private async void OnExportToFolderClicked(object sender, EventArgs e)
        {
            try
            {
                SaveCurrentSettings();
                
                var selectedSeason = SeasonPicker.SelectedItem as Season;
                if (selectedSeason == null)
                {
                    await DisplayAlert("No Season", "Please select a season first.", "OK");
                    return;
                }
                
                StatusLabel.Text = "Generating website...";
                StatusLabel.TextColor = Color.FromArgb("#3B82F6");
                StatusLabel.IsVisible = true;
                ExportToFolderBtn.IsEnabled = false;
                
                // Generate website files
                var generator = new WebsiteGenerator(League, League.WebsiteSettings);
                var files = generator.GenerateWebsite();
                
                // Get export folder
                var exportFolder = LocalExportService.GetDefaultExportFolder();
                
                var exportService = new LocalExportService();
                var progress = new Progress<string>(msg =>
                {
                    MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = msg);
                });
                
                var (success, message, outputPath) = await exportService.ExportToFolderAsync(files, exportFolder, progress);
                
                StatusLabel.Text = message;
                StatusLabel.TextColor = success ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
                
                if (success)
                {
                    await DisplayAlert("? Export Complete", 
                        $"Website exported!\n\nLocation: {outputPath}\nFiles: {files.Count}",
                        "OK");
                }
                else
                {
                    await DisplayAlert("Export Failed", message, "OK");
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                await DisplayAlert("Error", $"Export failed:\n\n{ex.Message}", "OK");
            }
            finally
            {
                ExportToFolderBtn.IsEnabled = true;
            }
        }
        
        private async void OnExportAsZipClicked(object sender, EventArgs e)
        {
            try
            {
                SaveCurrentSettings();
                
                var selectedSeason = SeasonPicker.SelectedItem as Season;
                if (selectedSeason == null)
                {
                    await DisplayAlert("No Season", "Please select a season first.", "OK");
                    return;
                }
                
                StatusLabel.Text = "Generating website...";
                StatusLabel.TextColor = Color.FromArgb("#3B82F6");
                StatusLabel.IsVisible = true;
                ExportAsZipBtn.IsEnabled = false;
                
                // Generate website files
                var generator = new WebsiteGenerator(League, League.WebsiteSettings);
                var files = generator.GenerateWebsite();
                
                var exportService = new LocalExportService();
                var progress = new Progress<string>(msg =>
                {
                    MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = msg);
                });
                
                // Create ZIP in memory for sharing
                var (success, message, zipStream) = await exportService.ExportToMemoryStreamAsync(files, progress);
                
                if (success && zipStream != null)
                {
                    StatusLabel.Text = "Preparing to share...";
                    
                    // Save to cache and share
                    var leagueName = League.WebsiteSettings.LeagueName ?? "PoolLeague";
                    var safeName = string.Join("_", leagueName.Split(System.IO.Path.GetInvalidFileNameChars()));
                    var fileName = $"{safeName}_Website_{DateTime.Now:yyyyMMdd}.zip";
                    var cachePath = System.IO.Path.Combine(FileSystem.CacheDirectory, fileName);
                    
                    await using (var fileStream = System.IO.File.Create(cachePath))
                    {
                        zipStream.Position = 0;
                        await zipStream.CopyToAsync(fileStream);
                    }
                    
                    await Share.RequestAsync(new ShareFileRequest
                    {
                        Title = "Export Website ZIP",
                        File = new ShareFile(cachePath)
                    });
                    
                    StatusLabel.Text = $"? ZIP created ({files.Count} files)";
                    StatusLabel.TextColor = Color.FromArgb("#10B981");
                }
                else
                {
                    StatusLabel.Text = message;
                    StatusLabel.TextColor = Color.FromArgb("#EF4444");
                    await DisplayAlert("Export Failed", message, "OK");
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                await DisplayAlert("Error", $"ZIP export failed:\n\n{ex.Message}", "OK");
            }
            finally
            {
                ExportAsZipBtn.IsEnabled = true;
            }
        }
        
        #endregion
        
        #region GitHub Pages
        
        private async void OnDeployToGitHubClicked(object sender, EventArgs e)
        {
            try
            {
                SaveCurrentSettings();
                
                var selectedSeason = SeasonPicker.SelectedItem as Season;
                if (selectedSeason == null)
                {
                    await DisplayAlert("No Season", "Please select a season first.", "OK");
                    return;
                }
                
                var token = GitHubTokenEntry.Text?.Trim();
                var username = GitHubUsernameEntry.Text?.Trim();
                var repoName = GitHubRepoEntry.Text?.Trim();
                
                if (string.IsNullOrWhiteSpace(token))
                {
                    await DisplayAlert("Missing Token", "Please enter your GitHub Personal Access Token.", "OK");
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(username))
                {
                    await DisplayAlert("Missing Username", "Please enter your GitHub username.", "OK");
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(repoName))
                {
                    await DisplayAlert("Missing Repository", "Please enter a repository name.", "OK");
                    return;
                }
                
                var confirm = await DisplayAlert(
                    "Deploy to GitHub Pages",
                    $"Deploy to:\nhttps://{username}.github.io/{repoName}/",
                    "Deploy",
                    "Cancel");
                
                if (!confirm) return;
                
                StatusLabel.Text = "Connecting to GitHub...";
                StatusLabel.TextColor = Color.FromArgb("#3B82F6");
                StatusLabel.IsVisible = true;
                UploadProgress.IsVisible = true;
                UploadProgress.Progress = 0;
                DeployToGitHubBtn.IsEnabled = false;
                
                var gitHubService = new GitHubPagesService(token, username, repoName);
                
                // Validate credentials first
                var (validCreds, credMessage) = await gitHubService.ValidateConnectionAsync();
                if (!validCreds)
                {
                    StatusLabel.Text = credMessage;
                    StatusLabel.TextColor = Color.FromArgb("#EF4444");
                    await DisplayAlert("Authentication Failed", credMessage, "OK");
                    return;
                }
                
                StatusLabel.Text = "Generating website...";
                UploadProgress.Progress = 0.1;
                
                // Generate website files
                var generator = new WebsiteGenerator(League, League.WebsiteSettings);
                var files = generator.GenerateWebsite();
                
                UploadProgress.Progress = 0.2;
                
                var progress = new Progress<string>(msg =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        StatusLabel.Text = msg;
                        if (UploadProgress.Progress < 0.9)
                            UploadProgress.Progress += 0.05;
                    });
                });
                
                var (success, message, siteUrl) = await gitHubService.DeployAsync(
                    files, 
                    GitHubCreateRepoCheck.IsChecked, 
                    progress);
                
                UploadProgress.Progress = 1.0;
                StatusLabel.Text = message;
                StatusLabel.TextColor = success ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
                
                if (success)
                {
                    League.WebsiteSettings.LastUploaded = DateTime.Now;
                    DataStore.Save();
                    
                    await DisplayAlert(
                        "? Deployed!",
                        $"Files: {files.Count}\nURL: {siteUrl}\n\nNote: May take a few minutes to go live.",
                        "OK");
                }
                else
                {
                    await DisplayAlert("Deployment Failed", message, "OK");
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                await DisplayAlert("Error", $"GitHub deployment failed:\n\n{ex.Message}", "OK");
            }
            finally
            {
                DeployToGitHubBtn.IsEnabled = true;
                UploadProgress.IsVisible = false;
            }
        }

        private async void OnCheckGitHubStatusClicked(object sender, EventArgs e)
        {
            try
            {
                var token = GitHubTokenEntry.Text?.Trim();
                var username = GitHubUsernameEntry.Text?.Trim();
                var repoName = GitHubRepoEntry.Text?.Trim();
                
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(repoName))
                {
                    await DisplayAlert("Missing Info", "Enter token, username, and repository first.", "OK");
                    return;
                }
                
                StatusLabel.Text = "Checking status...";
                StatusLabel.TextColor = Color.FromArgb("#3B82F6");
                StatusLabel.IsVisible = true;
                CheckGitHubStatusBtn.IsEnabled = false;
                GitHubStatusFrame.IsVisible = true;
                GitHubStatusLabel.Text = "? Checking...";
                
                var gitHubService = new GitHubPagesService(token, username, repoName);
                var (enabled, url, status, buildError) = await gitHubService.GetPagesStatusAsync();
                
                if (!enabled)
                {
                    GitHubStatusLabel.Text = status == "not_enabled" 
                        ? "? GitHub Pages not enabled" 
                        : $"? Error: {buildError ?? status}";
                    GitHubStatusFrame.BackgroundColor = Color.FromArgb("#FEE2E2");
                    StatusLabel.Text = "Not enabled";
                    StatusLabel.TextColor = Color.FromArgb("#EF4444");
                }
                else
                {
                    switch (status?.ToLowerInvariant())
                    {
                        case "built":
                            GitHubStatusLabel.Text = $"? LIVE: {url}";
                            GitHubStatusFrame.BackgroundColor = Color.FromArgb("#D1FAE5");
                            StatusLabel.Text = "? Site is live!";
                            StatusLabel.TextColor = Color.FromArgb("#10B981");
                            break;
                        case "building":
                            GitHubStatusLabel.Text = "? Building...";
                            GitHubStatusFrame.BackgroundColor = Color.FromArgb("#FEF3C7");
                            StatusLabel.Text = "Building...";
                            StatusLabel.TextColor = Color.FromArgb("#F59E0B");
                            break;
                        case "errored":
                            GitHubStatusLabel.Text = $"? Build failed: {buildError}";
                            GitHubStatusFrame.BackgroundColor = Color.FromArgb("#FEE2E2");
                            StatusLabel.Text = "Build failed";
                            StatusLabel.TextColor = Color.FromArgb("#EF4444");
                            break;
                        default:
                            GitHubStatusLabel.Text = $"Status: {status}";
                            GitHubStatusFrame.BackgroundColor = Color.FromArgb("#FEF3C7");
                            StatusLabel.Text = status ?? "Unknown";
                            StatusLabel.TextColor = Color.FromArgb("#3B82F6");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                GitHubStatusLabel.Text = $"? {ex.Message}";
                GitHubStatusFrame.BackgroundColor = Color.FromArgb("#FEE2E2");
            }
            finally
            {
                CheckGitHubStatusBtn.IsEnabled = true;
            }
        }
        
        #endregion
        
        #region FTP
        
        private async void OnTestConnectionClicked(object sender, EventArgs e)
        {
            try
            {
                SaveCurrentSettings();
                
                StatusLabel.Text = "Testing connection...";
                StatusLabel.TextColor = Color.FromArgb("#3B82F6");
                StatusLabel.IsVisible = true;
                
                TestConnectionBtn.IsEnabled = false;
                
                var ftpService = new FtpUploadService(League.WebsiteSettings);
                var (success, message) = await ftpService.TestConnectionAsync();
                
                StatusLabel.Text = message;
                StatusLabel.TextColor = success ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
                
                await DisplayAlert(success ? "? Success" : "? Failed", message, "OK");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                await DisplayAlert("Error", $"Connection test failed:\n\n{ex.Message}", "OK");
            }
            finally
            {
                TestConnectionBtn.IsEnabled = true;
            }
        }
        
        #endregion
        
        #region Common Actions
        
        private async void OnDeployClicked(object sender, EventArgs e)
        {
            switch (_selectedDeploymentMethod)
            {
                case DeploymentMethod.LocalExport:
                    OnExportToFolderClicked(sender, e);
                    break;
                case DeploymentMethod.GitHubPages:
                    OnDeployToGitHubClicked(sender, e);
                    break;
                case DeploymentMethod.FTP:
                    await DeployViaFtpAsync();
                    break;
            }
        }
        
        private async Task DeployViaFtpAsync()
        {
            try
            {
                SaveCurrentSettings();
                
                var selectedSeason = SeasonPicker.SelectedItem as Season;
                if (selectedSeason == null)
                {
                    await DisplayAlert("No Season", "Please select a season first.", "OK");
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(League.WebsiteSettings.FtpHost))
                {
                    await DisplayAlert("FTP Not Configured", "Please configure FTP settings first.", "OK");
                    return;
                }
                
                var remotePath = League.WebsiteSettings.RemotePath ?? "/";
                if (!remotePath.StartsWith("/")) remotePath = "/" + remotePath;
                if (!remotePath.EndsWith("/")) remotePath += "/";
                
                var confirm = await DisplayAlert(
                    "Upload Website",
                    $"Upload to:\n{League.WebsiteSettings.FtpHost}{remotePath}",
                    "Upload",
                    "Cancel");
                
                if (!confirm) return;
                
                StatusLabel.IsVisible = true;
                UploadProgress.IsVisible = true;
                GenerateBtn.IsEnabled = false;
                
                StatusLabel.Text = "Generating website...";
                StatusLabel.TextColor = Color.FromArgb("#3B82F6");
                
                var generator = new WebsiteGenerator(League, League.WebsiteSettings);
                var files = generator.GenerateWebsite();
                
                if (!files.ContainsKey("index.html"))
                {
                    StatusLabel.Text = "Error: index.html was not generated!";
                    StatusLabel.TextColor = Color.FromArgb("#EF4444");
                    await DisplayAlert("Error", "index.html was not generated.", "OK");
                    return;
                }
                
                StatusLabel.Text = "Uploading...";
                
                var ftpService = new FtpUploadService(League.WebsiteSettings);
                var progress = new Progress<UploadProgress>(p =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        UploadProgress.Progress = p.PercentComplete / 100.0;
                        StatusLabel.Text = p.Status;
                    });
                });
                
                var (success, message) = await ftpService.UploadWebsiteAsync(files, progress);
                
                StatusLabel.Text = success ? "? Upload complete!" : $"? {message}";
                StatusLabel.TextColor = success ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
                
                if (success)
                {
                    League.WebsiteSettings.LastUploaded = DateTime.Now;
                    DataStore.Save();
                    
                    var (verified, _, _) = await ftpService.VerifyUploadAsync();
                    
                    await DisplayAlert(
                        "? Uploaded",
                        $"Files: {files.Count}\n{(verified ? "? Verified on server" : "? Verification failed")}",
                        "OK");
                }
                else
                {
                    await DisplayAlert("? Upload Failed", message, "OK");
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                await DisplayAlert("Error", $"Upload failed:\n\n{ex.Message}", "OK");
            }
            finally
            {
                GenerateBtn.IsEnabled = true;
                UploadProgress.IsVisible = false;
            }
        }
        
        private async void OnUploadLogoClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select League Logo",
                    FileTypes = FilePickerFileType.Images
                });
                
                if (result != null)
                {
                    using var stream = await result.OpenReadAsync();
                    using var memoryStream = new System.IO.MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    _uploadedLogoData = memoryStream.ToArray();
                    
                    LogoStatusLabel.Text = $"? {result.FileName}";
                    LogoStatusLabel.TextColor = Color.FromArgb("#10B981");
                    
                    LogoPreviewImage.Source = ImageSource.FromStream(() => new System.IO.MemoryStream(_uploadedLogoData));
                    LogoPreviewImage.IsVisible = true;
                    
                    StatusLabel.Text = "Logo uploaded";
                    StatusLabel.TextColor = Color.FromArgb("#10B981");
                    StatusLabel.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                StatusLabel.IsVisible = true;
                await DisplayAlert("Error", $"Failed to upload logo:\n\n{ex.Message}", "OK");
            }
        }
        
        private async void OnPreviewClicked(object sender, EventArgs e)
        {
            try
            {
                SaveCurrentSettings();
                
                var selectedSeason = SeasonPicker.SelectedItem as Season;
                if (selectedSeason == null)
                {
                    await DisplayAlert("No Season", "Please select a season first.", "OK");
                    return;
                }
                
                StatusLabel.Text = "Generating preview...";
                StatusLabel.TextColor = Color.FromArgb("#3B82F6");
                StatusLabel.IsVisible = true;
                
                PreviewBtn.IsEnabled = false;
                
                var generator = new WebsiteGenerator(League, League.WebsiteSettings);
                _generatedFiles = generator.GenerateWebsite();
                
                PreviewFrame.IsVisible = true;
                PreviewPagePicker.SelectedIndex = 0;
                
                LoadPreviewPage("index.html");
                
                StatusLabel.Text = $"? Preview ready ({_generatedFiles.Count} files)";
                StatusLabel.TextColor = Color.FromArgb("#10B981");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                await DisplayAlert("Error", $"Preview failed:\n\n{ex.Message}", "OK");
            }
            finally
            {
                PreviewBtn.IsEnabled = true;
            }
        }
        
        private void OnPreviewPageChanged(object sender, EventArgs e)
        {
            if (PreviewPagePicker.SelectedIndex < 0 || _generatedFiles == null) return;
            
            var pageName = PreviewPagePicker.SelectedItem?.ToString();
            var fileName = pageName?.ToLowerInvariant() switch
            {
                "home" => "index.html",
                "standings" => "standings.html",
                "fixtures" => "fixtures.html",
                "results" => "results.html",
                "players" => "players.html",
                "divisions" => "divisions.html",
                _ => "index.html"
            };
            
            LoadPreviewPage(fileName);
        }
        
        private void OnClosePreviewClicked(object sender, EventArgs e)
        {
            PreviewFrame.IsVisible = false;
            _generatedFiles = null;
        }
        
        private void LoadPreviewPage(string fileName)
        {
            if (_generatedFiles == null || !_generatedFiles.ContainsKey(fileName))
            {
                PreviewWebView.Source = new HtmlWebViewSource
                {
                    Html = "<html><body><h1>File not found</h1></body></html>"
                };
                return;
            }
            
            var html = _generatedFiles[fileName];
            
            if (fileName != "style.css" && _generatedFiles.ContainsKey("style.css"))
            {
                var css = _generatedFiles["style.css"];
                html = html.Replace("<link rel=\"stylesheet\" href=\"style.css\">", 
                                   $"<style>{css}</style>");
            }
            
            PreviewWebView.Source = new HtmlWebViewSource { Html = html };
        }
        
        private async void OnSaveSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                SaveCurrentSettings();
                DataStore.Save();
                
                StatusLabel.Text = "? Settings saved";
                StatusLabel.TextColor = Color.FromArgb("#10B981");
                StatusLabel.IsVisible = true;
                
                await DisplayAlert("? Saved", "Website settings saved.", "OK");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                StatusLabel.IsVisible = true;
                await DisplayAlert("Error", $"Failed to save:\n\n{ex.Message}", "OK");
            }
        }

        private void OnTemplateChanged(object sender, EventArgs e)
        {
            if (TemplatePicker.SelectedItem is WebsiteTemplate template)
            {
                TemplateDescription.Text = template.Description;
                TemplateDescription.IsVisible = true;
            }
        }
        
        private void UpdateGalleryCount()
        {
            var count = League.WebsiteSettings.GalleryImages.Count;
            GalleryCountLabel.Text = count == 0 ? "(0 images)" : $"({count} image{(count == 1 ? "" : "s")})";
            GalleryCountLabel.TextColor = count > 0 ? Color.FromArgb("#10B981") : Color.FromArgb("#6B7280");
        }
        
        private async void OnAddGalleryImagesClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Select Photos",
                    FileTypes = FilePickerFileType.Images
                });
                
                if (result == null) return;
                
                var files = result.ToList();
                if (files.Count == 0) return;
                
                StatusLabel.Text = $"Processing {files.Count} image(s)...";
                StatusLabel.TextColor = Color.FromArgb("#3B82F6");
                StatusLabel.IsVisible = true;
                AddGalleryImageBtn.IsEnabled = false;
                
                var optimizer = new ImageOptimizationService();
                var addedCount = 0;
                
                foreach (var file in files)
                {
                    try
                    {
                        using var stream = await file.OpenReadAsync();
                        using var memoryStream = new System.IO.MemoryStream();
                        await stream.CopyToAsync(memoryStream);
                        var imageData = memoryStream.ToArray();
                        
                        var (width, height) = await optimizer.GetImageDimensionsAsync(imageData);
                        
                        var galleryImage = new GalleryImage
                        {
                            FileName = file.FileName,
                            ImageData = imageData,
                            Width = width,
                            Height = height,
                            DateAdded = DateTime.Now,
                            Caption = "",
                            Category = "General"
                        };
                        
                        League.WebsiteSettings.GalleryImages.Add(galleryImage);
                        addedCount++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error adding {file.FileName}: {ex.Message}");
                    }
                }
                
                if (addedCount > 0)
                {
                    DataStore.Save();
                    UpdateGalleryCount();
                    StatusLabel.Text = $"? Added {addedCount} image(s)";
                    StatusLabel.TextColor = Color.FromArgb("#10B981");
                    await DisplayAlert("Success", $"Added {addedCount} image(s).", "OK");
                }
                else
                {
                    StatusLabel.Text = "No images added";
                    StatusLabel.TextColor = Color.FromArgb("#EF4444");
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                StatusLabel.IsVisible = true;
                await DisplayAlert("Error", $"Failed to add photos:\n\n{ex.Message}", "OK");
            }
            finally
            {
                AddGalleryImageBtn.IsEnabled = true;
            }
        }
        
        private async void OnManageGalleryClicked(object sender, EventArgs e)
        {
            if (League.WebsiteSettings.GalleryImages.Count == 0)
            {
                await DisplayAlert("Empty", "No images yet. Click 'Add Photos' first.", "OK");
                return;
            }
            
            var action = await DisplayActionSheet(
                $"Gallery ({League.WebsiteSettings.GalleryImages.Count} images)",
                "Cancel",
                "Clear All",
                "View List");
            
            if (action == "Clear All")
            {
                var confirm = await DisplayAlert(
                    "Clear Gallery",
                    $"Remove all {League.WebsiteSettings.GalleryImages.Count} images?",
                    "Clear",
                    "Cancel");
                
                if (confirm)
                {
                    League.WebsiteSettings.GalleryImages.Clear();
                    DataStore.Save();
                    UpdateGalleryCount();
                    
                    StatusLabel.Text = "? Gallery cleared";
                    StatusLabel.TextColor = Color.FromArgb("#10B981");
                    StatusLabel.IsVisible = true;
                }
            }
            else if (action == "View List")
            {
                var imageList = string.Join("\n", League.WebsiteSettings.GalleryImages.Select((img, i) => 
                    $"{i + 1}. {img.FileName} ({img.Width}x{img.Height})"));
                
                await DisplayAlert("Gallery Images", imageList, "OK");
            }
        }
        
        #endregion
    }
}
