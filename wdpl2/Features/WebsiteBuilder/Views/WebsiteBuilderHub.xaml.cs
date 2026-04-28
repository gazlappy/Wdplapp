using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Wdpl2.Helpers;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class WebsiteBuilderHub : ContentPage
{
    private static LeagueData League => DataStore.Data;
    private readonly ObservableCollection<Season> _seasons = new();
    private readonly ObservableCollection<WebsiteTemplate> _templates = new();
    private Dictionary<string, string>? _generatedFiles;
    private string _currentPreviewPage = "index.html";
    private string? _currentQueryString;
    private bool _syncingPicker;

    public WebsiteBuilderHub()
    {
        InitializeComponent();
        
        SeasonPicker.ItemsSource = _seasons;
        SeasonPicker.ItemDisplayBinding = new Binding("Name");
        
        TemplatePicker.ItemsSource = _templates;
        TemplatePicker.ItemDisplayBinding = new Binding("Name");
        TemplatePicker.SelectedIndexChanged += OnTemplateChanged;
        
        PreviewPagePicker.ItemsSource = new[] 
        { 
            "Home", "Standings", "Fixtures", "Results", "Players", "Divisions", "Competitions" 
        };

        LoadData();
    }

    private void UpdatePreviewPagePicker()
    {
        if (_generatedFiles == null) return;

        var pages = new List<string> { "Home" };
        if (_generatedFiles.ContainsKey("standings.html")) pages.Add("Standings");
        if (_generatedFiles.ContainsKey("fixtures.html")) pages.Add("Fixtures");
        if (_generatedFiles.ContainsKey("results.html")) pages.Add("Results");
        if (_generatedFiles.ContainsKey("players.html")) pages.Add("Players");
        if (_generatedFiles.ContainsKey("divisions.html")) pages.Add("Divisions");
        if (_generatedFiles.ContainsKey("competitions.html")) pages.Add("Competitions");
        if (_generatedFiles.ContainsKey("gallery.html")) pages.Add("Gallery");
        if (_generatedFiles.ContainsKey("rules.html")) pages.Add("Rules");
        if (_generatedFiles.ContainsKey("contact.html")) pages.Add("Contact");
        if (_generatedFiles.ContainsKey("sponsors.html")) pages.Add("Sponsors");
        if (_generatedFiles.ContainsKey("news.html")) pages.Add("News");
        if (_generatedFiles.ContainsKey("rows-reports.html")) pages.Add("Rows Reports");
        if (_generatedFiles.ContainsKey("entry-forms.html")) pages.Add("Entry Forms");
        if (_generatedFiles.ContainsKey("captains.html")) pages.Add("Captains");

        // Custom pages
        foreach (var key in _generatedFiles.Keys.Where(k => k.EndsWith(".html") && !IsKnownPage(k)))
            pages.Add(Path.GetFileNameWithoutExtension(key));

        _syncingPicker = true;
        PreviewPagePicker.ItemsSource = pages;

        // Restore selection
        var currentLabel = FileNameToLabel(_currentPreviewPage);
        var idx = pages.IndexOf(currentLabel);
        PreviewPagePicker.SelectedIndex = idx >= 0 ? idx : 0;
        _syncingPicker = false;
    }

    private static bool IsKnownPage(string fileName) =>
        fileName is "index.html" or "standings.html" or "fixtures.html" or "results.html"
            or "players.html" or "divisions.html" or "competitions.html" or "gallery.html"
            or "rules.html" or "contact.html" or "sponsors.html" or "news.html"
            or "rows-reports.html" or "entry-forms.html"
            or "captains.html" or "captain-dashboard.html"
            or "player.html" or "team.html" or "pool-game.html" or "style.css"
            or "sitemap.xml" or "players-data.json" or "teams-data.json" or "captains-data.json";
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateGalleryCount();
        UpdateRowsReportsCount();
        UpdateRulesSummary();
        UpdateEntryFormsCount();
        UpdateSummaryLabels();

        // Auto-refresh preview when returning from settings sub-pages
        if (_generatedFiles != null)
        {
            try
            {
                LoadingOverlay.Show("Refreshing preview...");
                SaveSeasonAndTemplate();

                // Reload competitions from SQLite
                try
                {
                    using var context = new Data.LeagueContext();
                    context.Database.EnsureCreated();
                    League.Competitions = context.Competitions.AsNoTracking().ToList();
                }
                catch { }

                var generator = new WebsiteGenerator(League, League.WebsiteSettings);
                _generatedFiles = generator.GenerateWebsite();
                UpdatePreviewPagePicker();
                LoadPreviewPage(_currentPreviewPage);

                StatusLabel.Text = $"Preview refreshed ({_generatedFiles.Count} files)";
                StatusLabel.TextColor = Color.FromArgb("#10B981");
                StatusLabel.IsVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-refresh error: {ex.Message}");
            }
            finally
            {
                LoadingOverlay.Hide();
            }
        }
    }
    
    private void LoadData()
    {
        _seasons.Clear();
        foreach (var season in League.Seasons.OrderByDescending(s => s.StartDate))
            _seasons.Add(season);
        
        _templates.Clear();
        foreach (var template in WebsiteTemplate.GetAllTemplates())
            _templates.Add(template);
        
        var settings = League.WebsiteSettings;
        
        if (settings.SelectedSeasonId.HasValue)
        {
            var season = _seasons.FirstOrDefault(s => s.Id == settings.SelectedSeasonId.Value);
            if (season != null) SeasonPicker.SelectedItem = season;
        }
        else
        {
            var activeSeason = _seasons.FirstOrDefault(s => s.IsActive);
            if (activeSeason != null) SeasonPicker.SelectedItem = activeSeason;
        }
        
        var selectedTemplate = _templates.FirstOrDefault(t => t.Id == settings.SelectedTemplate);
        TemplatePicker.SelectedItem = selectedTemplate ?? _templates.FirstOrDefault();

        UpdateGalleryCount();
        UpdateRowsReportsCount();
        UpdateRulesSummary();
        UpdateEntryFormsCount();
        UpdateHistorySummary();
        UpdateSummaryLabels();
        UpdateLastActivityLabel();
    }

    private void UpdateLastActivityLabel()
    {
        var settings = League.WebsiteSettings;
        var parts = new List<string>();

        if (settings.LastGenerated != default)
            parts.Add($"Generated: {settings.LastGenerated:dd MMM yyyy HH:mm}");
        if (settings.LastUploaded != default)
            parts.Add($"Deployed: {settings.LastUploaded:dd MMM yyyy HH:mm}");

        if (parts.Count > 0)
        {
            LastActivityLabel.Text = string.Join("  •  ", parts);
            LastActivityLabel.IsVisible = true;
        }
        else
        {
            LastActivityLabel.IsVisible = false;
        }
    }
    
    private void UpdateGalleryCount()
    {
        var count = League.WebsiteSettings.GalleryImages.Count;
        GalleryCountLabel.Text = $"Manage photos ({count} image{(count == 1 ? "" : "s")})";
    }

    private void UpdateRowsReportsCount()
    {
        var count = League.WebsiteSettings.RowsReports.Count;
        RowsReportsCountLabel.Text = $"Weekly match reports ({count})";
    }

    private void UpdateRulesSummary()
    {
        var s = League.WebsiteSettings;
        if (!s.ShowRules)
        {
            RulesSummaryLabel.Text = "Rules page disabled";
            return;
        }

        var sections = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.ConstitutionContent)) sections.Add("Constitution");
        if (!string.IsNullOrWhiteSpace(s.MatchRulesContent)) sections.Add("Match Rules");
        if (!string.IsNullOrWhiteSpace(s.EpaRulesContent)) sections.Add("EPA");

        RulesSummaryLabel.Text = sections.Count > 0
            ? $"Enabled · {string.Join(", ", sections)}"
            : "Enabled · No content yet";
    }

    private void UpdateEntryFormsCount()
    {
        var count = League.WebsiteSettings.EntryForms.Count;
        var published = League.WebsiteSettings.EntryForms.Count(f => f.IsPublished && !f.IsClosed);
        var totalEntries = League.WebsiteSettings.EntryForms.Sum(f => f.Submissions.Count);
        EntryFormsSummaryLabel.Text = count == 0
            ? "No forms created yet"
            : $"{count} form{(count == 1 ? "" : "s")} ({published} active) \u2022 {totalEntries} entr{(totalEntries == 1 ? "y" : "ies")} logged";
    }

    private void UpdateHistorySummary()
    {
        var honours = League.WebsiteSettings.HistoricHonours;
        HistorySummaryLabel.Text = honours.Count == 0
            ? "Import historic winners and runners-up"
            : $"{honours.Count} honours across {honours.Select(h => h.Season).Distinct().Count()} seasons";
    }

    private void UpdateSummaryLabels()
    {
        var s = League.WebsiteSettings;

        // Branding
        var brandingParts = new List<string>();
        if (s.LeagueName != "My Pool League") brandingParts.Add(s.LeagueName);
        if (s.UseCustomLogo) brandingParts.Add("Logo ✓");
        if (!string.IsNullOrWhiteSpace(s.FaviconUrl)) brandingParts.Add("Favicon ✓");
        BrandingSummaryLabel.Text = brandingParts.Count > 0 
            ? string.Join(" · ", brandingParts) 
            : "Name, logo, favicon";

        // Contact
        var contactParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.ContactEmail)) contactParts.Add("Email");
        if (!string.IsNullOrWhiteSpace(s.ContactPhone)) contactParts.Add("Phone");
        if (s.HasSocialLinks) contactParts.Add("Social");
        ContactSummaryLabel.Text = contactParts.Count > 0 
            ? string.Join(" · ", contactParts) + " configured" 
            : "Not configured yet";

        // Colors
        ColorsSummaryLabel.Text = $"Primary: {s.PrimaryColor}";

        // Layout
        LayoutSummaryLabel.Text = $"{Capitalize(s.HeaderLayout)} header · {s.HeaderFontFamily} font";

        // Home Page
        var homeSections = 0;
        if (s.HomeShowWelcomeSection) homeSections++;
        if (s.HomeShowQuickStats) homeSections++;
        if (s.HomeShowLeagueLeaders) homeSections++;
        if (s.HomeShowRecentResults) homeSections++;
        if (s.HomeShowUpcomingFixtures) homeSections++;
        if (s.HomeShowLatestNews) homeSections++;
        if (s.HomeShowSponsors) homeSections++;
        homeSections += s.HomeFeaturedPages.Count;
        HomeSummaryLabel.Text = $"{homeSections} section{(homeSections == 1 ? "" : "s")} visible";

        // Content — count enabled pages
        var pageCount = 0;
        if (s.ShowStandings) pageCount++;
        if (s.ShowFixtures) pageCount++;
        if (s.ShowResults) pageCount++;
        if (s.ShowPlayerStats) pageCount++;
        if (s.ShowDivisions) pageCount++;
        if (s.ShowCompetitions) pageCount++;
        if (s.ShowGallery) pageCount++;
        if (s.ShowNews) pageCount++;
        if (s.ShowRowsReports) pageCount++;
        if (s.ShowSponsors) pageCount++;
        if (s.ShowRules) pageCount++;
        if (s.ShowContactPage) pageCount++;
        if (s.ShowEntryForms && s.EntryForms.Count > 0) pageCount++;
        ContentSummaryLabel.Text = $"{pageCount} page{(pageCount == 1 ? "" : "s")} enabled · {Capitalize(s.HomeLayout)} home";

        // SEO
        var seoParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.MetaDescription)) seoParts.Add("Meta");
        if (!string.IsNullOrWhiteSpace(s.CustomCss)) seoParts.Add("CSS");
        if (!string.IsNullOrWhiteSpace(s.CustomHeadHtml) || !string.IsNullOrWhiteSpace(s.CustomBodyStartHtml) || !string.IsNullOrWhiteSpace(s.CustomBodyEndHtml))
            seoParts.Add("HTML");
        if (s.GenerateSitemap) seoParts.Add("Sitemap");
        SeoSummaryLabel.Text = seoParts.Count > 0 
            ? string.Join(" · ", seoParts) + " configured" 
            : "Not configured yet";

        UpdateSetupProgress();
    }

    private void UpdateSetupProgress()
    {
        var s = League.WebsiteSettings;
        var total = 6;
        var remaining = new List<string>();

        // 1. League name set
        if (s.LeagueName == "My Pool League")
            remaining.Add("Set your league name in Site Branding");
        // 2. Template chosen (always done)
        if (string.IsNullOrWhiteSpace(s.SelectedTemplate))
            remaining.Add("Choose a website template");
        // 3. Colors customized
        if (s.PrimaryColor == "#3B82F6")
            remaining.Add("Customize your color scheme in Colors and Theme");
        // 4. At least one page enabled
        if (!(s.ShowStandings || s.ShowFixtures || s.ShowResults || s.ShowPlayerStats))
            remaining.Add("Enable at least one page in Pages and Content");
        // 5. Logo uploaded
        if (!s.UseCustomLogo)
            remaining.Add("Upload your league logo in Site Branding");
        // 6. Has been previewed at least once
        if (s.LastGenerated == default)
            remaining.Add("Preview your website at least once");

        var steps = total - remaining.Count;
        var progress = (double)steps / total;
        SetupProgressBar.Progress = progress;
        SetupProgressLabel.Text = $"{steps}/{total}";

        if (remaining.Count == 0)
        {
            SetupHintLabel.Text = "All set! Your website is ready to deploy.";
            SetupProgressBar.ProgressColor = Color.FromArgb("#10B981");
            SetupProgressLabel.TextColor = Color.FromArgb("#10B981");
            SetupRemainingLabel.IsVisible = false;
        }
        else
        {
            SetupHintLabel.Text = $"{remaining.Count} step{(remaining.Count == 1 ? "" : "s")} remaining:";
            SetupProgressBar.ProgressColor = Color.FromArgb("#3B82F6");
            SetupProgressLabel.TextColor = Color.FromArgb("#3B82F6");
            SetupRemainingLabel.Text = string.Join("\n", remaining.Select(r => $"  ○  {r}"));
            SetupRemainingLabel.IsVisible = true;
        }
    }

    private static string Capitalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return char.ToUpperInvariant(value[0]) + value[1..];
    }
    
    private void OnTemplateChanged(object? sender, EventArgs e)
    {
        if (TemplatePicker.SelectedItem is WebsiteTemplate template)
        {
            TemplateDescription.Text = template.Description;
            TemplateDescription.IsVisible = true;

            TemplateFeaturesLayout.Children.Clear();
            if (template.Features.Length > 0)
            {
                foreach (var feature in template.Features)
                {
                    var badge = new Frame
                    {
                        Padding = new Thickness(8, 4),
                        CornerRadius = 12,
                        BackgroundColor = Color.FromArgb("#DBEAFE"),
                        BorderColor = Color.FromArgb("#93C5FD"),
                        HasShadow = false,
                        Margin = new Thickness(0, 2, 6, 2),
                        Content = new Label
                        {
                            Text = feature,
                            FontSize = 10,
                            TextColor = Color.FromArgb("#1D4ED8")
                        }
                    };
                    TemplateFeaturesLayout.Children.Add(badge);
                }
                TemplateFeaturesLayout.IsVisible = true;
            }
            else
            {
                TemplateFeaturesLayout.IsVisible = false;
            }
        }
    }
    
    private async void OnBrandingTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new BrandingSettingsPage());
    
    private async void OnContactTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new ContactSettingsPage());
    
    private async void OnColorsTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new ColorsSettingsPage());
    
    private async void OnDragDropLayoutTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new DragDropLayoutPage());
    
    private async void OnLayoutTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new LayoutSettingsPage());
    
    private async void OnContentTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new ContentSettingsPage());

    private async void OnHomeTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new HomeSettingsPage());
    
    private async void OnGalleryTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new GallerySettingsPage());

    private async void OnRowsReportsTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new RowsReportsSettingsPage());

    private async void OnRulesTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new RulesSettingsPage());

    private async void OnEntryFormsTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new EntryFormsSettingsPage());

    private async void OnHistoryTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new HistorySettingsPage());

    private async void OnCaptainsTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new CaptainsAccessSettingsPage());

    private async void OnFixturesSheetTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new FixturesSheetPage());

    private async void OnSocialCardTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new SocialCardPage());

    private async void OnSeoTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new SeoSettingsPage());
    
    private async void OnDeploymentTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new DeploymentSettingsPage());

    private void OnSettingsSearchChanged(object? sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim().ToLowerInvariant();
        var isSearching = !string.IsNullOrEmpty(query);

        var tiles = new (Frame tile, string[] keywords)[]
        {
            (BrandingTile, ["branding", "site branding", "name", "logo", "favicon", "league name"]),
            (ContactTile, ["contact", "social", "email", "phone", "social links", "facebook", "twitter"]),
            (DragDropTile, ["visual layout", "drag", "drop", "page layout", "editor"]),
            (ColorsTile, ["colors", "colour", "theme", "color scheme", "presets", "primary color"]),
            (LayoutTile, ["layout", "styling", "header", "nav", "footer", "fonts", "navigation", "font"]),
            (HomeTile, ["home", "home page", "welcome", "quick stats", "league leaders", "recent results", "upcoming fixtures", "hide", "show", "sections", "featured", "featured pages"]),
            (ContentTile, ["content", "pages", "home", "standings", "fixtures", "results", "players", "divisions"]),
            (GalleryTile, ["gallery", "photos", "images", "photo"]),
            (RowsReportsTile, ["rows reports", "match reports", "weekly", "reports"]),
            (RulesTile, ["rules", "league rules", "constitution", "match rules"]),
            (EntryFormsTile, ["entry forms", "forms", "entries", "submissions", "entry"]),
            (HistoryTile, ["history", "honours", "roll of honour", "historic", "winners"]),
            (CaptainsTile, ["captains", "captain", "login", "pin", "team login", "access", "private", "contact list"]),
            (FixturesSheetTile, ["fixtures sheet", "print", "printable", "fixtures"]),
            (SocialCardTile, ["social media", "share", "result cards", "post"]),
            (SeoTile, ["seo", "advanced", "meta tags", "custom css", "html", "sitemap", "meta"]),
            (DeploymentTile, ["deployment", "deploy", "export", "github pages", "ftp", "publish", "upload"]),
        };

        foreach (var (tile, keywords) in tiles)
            tile.IsVisible = !isSearching || keywords.Any(k => k.Contains(query!));

        var sections = new (Label label, Frame[] sectionTiles)[]
        {
            (BrandingSectionLabel, [BrandingTile, ContactTile]),
            (DesignSectionLabel, [DragDropTile, ColorsTile, LayoutTile]),
            (ContentSectionLabel, [HomeTile, ContentTile, GalleryTile, RowsReportsTile, RulesTile, EntryFormsTile, HistoryTile, CaptainsTile]),
            (PrintExportSectionLabel, [FixturesSheetTile]),
            (SocialSectionLabel, [SocialCardTile]),
            (AdvancedSectionLabel, [SeoTile, DeploymentTile]),
        };

        foreach (var (label, sectionTiles) in sections)
            label.IsVisible = !isSearching || sectionTiles.Any(t => t.IsVisible);

        ResetBtn.IsVisible = !isSearching;
    }

    private async void OnResetClicked(object sender, EventArgs e)
    {
        var confirmed = await DisplayAlert(
            "Reset Settings",
            "This will reset ALL website builder settings (colors, layout, content, SEO, etc.) to their default values. This cannot be undone.\n\nAre you sure?",
            "Reset Everything",
            "Cancel");

        if (!confirmed) return;

        League.WebsiteSettings.ResetToDefaults();
        DataStore.Save();

        // Reload the page data to reflect new defaults
        LoadData();
        _generatedFiles = null;
        PreviewPlaceholder.IsVisible = true;
        PreviewWebView.IsVisible = false;

        StatusLabel.Text = "All settings reset to defaults";
        StatusLabel.TextColor = Color.FromArgb("#F59E0B");
        StatusLabel.IsVisible = true;
    }
    
    private void SaveSeasonAndTemplate()
    {
        var settings = League.WebsiteSettings;
        var selectedSeason = SeasonPicker.SelectedItem as Season;
        settings.SelectedSeasonId = selectedSeason?.Id;
        
        var selectedTemplate = TemplatePicker.SelectedItem as WebsiteTemplate;
        settings.SelectedTemplate = selectedTemplate?.Id ?? "modern";
    }
    
    private async void OnPreviewClicked(object sender, EventArgs e)
    {
        try
        {
            var selectedSeason = SeasonPicker.SelectedItem as Season;
            if (selectedSeason == null)
            {
                await DisplayAlert("No Season", "Please select a season first.", "OK");
                return;
            }

            SaveSeasonAndTemplate();

            StatusLabel.Text = "Generating preview...";
            StatusLabel.TextColor = Color.FromArgb("#3B82F6");
            StatusLabel.IsVisible = true;
            PreviewBtn.IsEnabled = false;
            RefreshPreviewBtn.IsEnabled = false;
            LoadingOverlay.Show("Generating website...");

            // Load competitions from SQLite
            try
            {
                using var context = new Data.LeagueContext();
                context.Database.EnsureCreated();
                League.Competitions = context.Competitions.AsNoTracking().ToList();
            }
            catch { }

            var generator = new WebsiteGenerator(League, League.WebsiteSettings);
            _generatedFiles = generator.GenerateWebsite();

            // Show WebView, hide placeholder
            PreviewPlaceholder.IsVisible = false;
            PreviewWebView.IsVisible = true;

            UpdatePreviewPagePicker();

            if (PreviewPagePicker.SelectedIndex < 0)
                PreviewPagePicker.SelectedIndex = 0;
            else
                LoadPreviewPage(GetSelectedFileName());

            StatusLabel.Text = $"Preview ready ({_generatedFiles.Count} files)";
            StatusLabel.TextColor = Color.FromArgb("#10B981");

            League.WebsiteSettings.LastGenerated = DateTime.Now;
            UpdateLastActivityLabel();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.TextColor = Color.FromArgb("#EF4444");
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            LoadingOverlay.Hide();
            PreviewBtn.IsEnabled = true;
            RefreshPreviewBtn.IsEnabled = true;
        }
    }
    
    private async void OnDeployClicked(object sender, EventArgs e)
    {
        var selectedSeason = SeasonPicker.SelectedItem as Season;
        if (selectedSeason == null)
        {
            await DisplayAlert("No Season", "Please select a season first.", "OK");
            return;
        }
        
        SaveSeasonAndTemplate();
        await Navigation.PushAsync(new DeploymentSettingsPage());
    }
    
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            SaveSeasonAndTemplate();
            DataStore.Save();
            
            StatusLabel.Text = "Settings saved";
            StatusLabel.TextColor = Color.FromArgb("#10B981");
            StatusLabel.IsVisible = true;
            
            await DisplayAlert("Saved", "Website settings saved.", "OK");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.TextColor = Color.FromArgb("#EF4444");
            StatusLabel.IsVisible = true;
        }
    }
    
    private void OnPreviewPageChanged(object sender, EventArgs e)
    {
        if (_syncingPicker || PreviewPagePicker.SelectedIndex < 0 || _generatedFiles == null) return;
        _currentPreviewPage = GetSelectedFileName();
        _currentQueryString = null;
        LoadPreviewPage(_currentPreviewPage);
    }

    private string GetSelectedFileName()
    {
        var pageName = PreviewPagePicker.SelectedItem?.ToString();
        return pageName?.ToLowerInvariant() switch
        {
            "home" => "home.html",
            "standings" => "standings.html",
            "fixtures" => "fixtures.html",
            "results" => "results.html",
            "players" => "players.html",
            "divisions" => "divisions.html",
            "competitions" => "competitions.html",
            "gallery" => "gallery.html",
            "rules" => "rules.html",
            "contact" => "contact.html",
            "sponsors" => "sponsors.html",
            "news" => "news.html",
            "rows reports" => "rows-reports.html",
            "captains" => "captains.html",
            _ => pageName != null ? $"{pageName.ToLowerInvariant()}.html" : "home.html"
        };
    }

    private static string FileNameToLabel(string fileName)
    {
        return fileName.ToLowerInvariant() switch
        {
            "home.html" => "Home",
            "standings.html" => "Standings",
            "fixtures.html" => "Fixtures",
            "results.html" => "Results",
            "players.html" => "Players",
            "divisions.html" => "Divisions",
            "competitions.html" => "Competitions",
            "gallery.html" => "Gallery",
            "rules.html" => "Rules",
            "contact.html" => "Contact",
            "sponsors.html" => "Sponsors",
            "news.html" => "News",
            "rows-reports.html" => "Rows Reports",
            "captains.html" => "Captains",
            "captain-dashboard.html" => "Captain Dashboard",
            _ => Path.GetFileNameWithoutExtension(fileName)
        };
    }

    private void LoadPreviewPage(string fileName)
    {
        if (_generatedFiles == null || !_generatedFiles.TryGetValue(fileName, out var html))
        {
            PreviewWebView.Source = new HtmlWebViewSource
            {
                Html = "<html><body style='font-family:sans-serif;padding:40px;color:#6B7280'><h2>Page not found</h2><p>This page wasn't generated. Check your content settings.</p></body></html>"
            };
            return;
        }

        // Inline CSS (handle optional cache-buster query string)
        if (_generatedFiles.TryGetValue("style.css", out var css))
            html = System.Text.RegularExpressions.Regex.Replace(
                html,
                @"<link rel=""stylesheet"" href=""style\.css(\?v=[^""]*)?"">",
                _ => $"<style>{css}</style>");

        // Inline JSON data so fetch() works in the WebView
        html = InlineJsonData(html);

        // Inject query string for template pages (player.html?id=xxx)
        if (!string.IsNullOrEmpty(_currentQueryString))
        {
            var fakeQs = _currentQueryString.Replace("\\", "\\\\").Replace("'", "\\'");
            var qsScript = $"<script>if(!window.location.search){{" +
                           $"Object.defineProperty(window,'_editorQS',{{value:'{fakeQs}'}});" +
                           $"var _origUSP=URLSearchParams;" +
                           $"URLSearchParams=function(s){{return new _origUSP(window._editorQS||s);}};" +
                           $"}}</script>";
            html = html.Replace("<head>", "<head>" + qsScript);
        }

        WebViewHelper.LoadHtml(PreviewWebView, html);
    }

    private string InlineJsonData(string html)
    {
        if (_generatedFiles == null) return html;

        if (_generatedFiles.TryGetValue("players-data.json", out var playersJson))
        {
            var escaped = playersJson.Replace("\\", "\\\\").Replace("'", "\\'")
                .Replace("\r", "").Replace("\n", "");
            html = ReplaceFetchWithInline(html, "players-data.json", escaped);
        }

        if (_generatedFiles.TryGetValue("teams-data.json", out var teamsJson))
        {
            var escaped = teamsJson.Replace("\\", "\\\\").Replace("'", "\\'")
                .Replace("\r", "").Replace("\n", "");
            html = ReplaceFetchWithInline(html, "teams-data.json", escaped);
        }

        if (_generatedFiles.TryGetValue("captains-data.json", out var captainsJson))
        {
            var escaped = captainsJson.Replace("\\", "\\\\").Replace("'", "\\'")
                .Replace("\r", "").Replace("\n", "");
            html = ReplaceFetchWithInline(html, "captains-data.json", escaped);
        }

        return html;
    }

    /// <summary>
    /// Replaces fetch('file.json') or fetch('file.json?v=' + cacheBuster) plus
    /// the following .then(function(r) { return r.json(); }) with an inline
    /// Promise.resolve(JSON.parse('...')) so the preview works without HTTP.
    /// </summary>
    private static string ReplaceFetchWithInline(string html, string fileName, string escapedJson)
    {
        // Match both plain fetch('file.json') and cache-busted fetch('file.json?v=' + cacheBuster)
        var escapedFile = System.Text.RegularExpressions.Regex.Escape(fileName);
        var pattern = @"fetch\((?:'" + escapedFile + @"'|'" + escapedFile
            + @"\?v='\s*\+\s*cacheBuster)\)\s*\.then\(function\(r\)\s*\{\s*return\s+r\.json\(\);\s*\}\)";
        return System.Text.RegularExpressions.Regex.Replace(
            html,
            pattern,
            _ => $"Promise.resolve(JSON.parse('{escapedJson}'))");
    }

    private void OnPreviewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        // Intercept .html link clicks so navigation stays within the preview
        var url = e.Url;
        if (url.StartsWith("app://")) { e.Cancel = true; return; }

        string? targetFile = null;
        string? queryString = null;

        var lastSlash = url.LastIndexOf('/');
        var pathPart = lastSlash >= 0 ? url[(lastSlash + 1)..] : url;
        var qIdx = pathPart.IndexOf('?');
        if (qIdx >= 0)
        {
            queryString = pathPart[qIdx..];
            pathPart = pathPart[..qIdx];
        }

        if (pathPart.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            targetFile = pathPart;

        if (targetFile != null && _generatedFiles != null && _generatedFiles.ContainsKey(targetFile))
        {
            e.Cancel = true;
            _currentPreviewPage = targetFile;
            _currentQueryString = queryString;
            SyncPagePicker(targetFile);
            LoadPreviewPage(targetFile);
        }
    }

    private void SyncPagePicker(string fileName)
    {
        var label = FileNameToLabel(fileName);
        if (PreviewPagePicker.ItemsSource is IList<string> items)
        {
            var idx = items.IndexOf(label);
            if (idx >= 0)
            {
                _syncingPicker = true;
                PreviewPagePicker.SelectedIndex = idx;
                _syncingPicker = false;
            }
        }
    }
}
