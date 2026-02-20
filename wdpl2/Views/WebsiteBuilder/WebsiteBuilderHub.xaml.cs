using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;
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
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateGalleryCount();
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
    }
    
    private void UpdateGalleryCount()
    {
        var count = League.WebsiteSettings.GalleryImages.Count;
        GalleryCountLabel.Text = $"Manage photos ({count} image{(count == 1 ? "" : "s")})";
    }
    
    private void OnTemplateChanged(object? sender, EventArgs e)
    {
        if (TemplatePicker.SelectedItem is WebsiteTemplate template)
        {
            TemplateDescription.Text = template.Description;
            TemplateDescription.IsVisible = true;
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
    
    private async void OnGalleryTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new GallerySettingsPage());
    
    private async void OnFixturesSheetTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new FixturesSheetPage());
    
    private async void OnSeoTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new SeoSettingsPage());
    
    private async void OnDeploymentTapped(object sender, EventArgs e)
        => await Navigation.PushAsync(new DeploymentSettingsPage());
    
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

            if (PreviewPagePicker.SelectedIndex < 0)
                PreviewPagePicker.SelectedIndex = 0;
            else
                LoadPreviewPage(GetSelectedFileName());

            StatusLabel.Text = $"Preview ready ({_generatedFiles.Count} files)";
            StatusLabel.TextColor = Color.FromArgb("#10B981");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.TextColor = Color.FromArgb("#EF4444");
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
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
            "home" => "index.html",
            "standings" => "standings.html",
            "fixtures" => "fixtures.html",
            "results" => "results.html",
            "players" => "players.html",
            "divisions" => "divisions.html",
            "competitions" => "competitions.html",
            _ => "index.html"
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

        // Inline CSS
        if (_generatedFiles.TryGetValue("style.css", out var css))
            html = html.Replace("<link rel=\"stylesheet\" href=\"style.css\">", $"<style>{css}</style>");

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

        PreviewWebView.Source = new HtmlWebViewSource { Html = html };
    }

    private string InlineJsonData(string html)
    {
        if (_generatedFiles == null) return html;

        if (_generatedFiles.TryGetValue("players-data.json", out var playersJson))
        {
            var escaped = playersJson.Replace("\\", "\\\\").Replace("'", "\\'")
                .Replace("\r", "").Replace("\n", "");
            html = ReplaceFetchPattern(html, "fetch('players-data.json')",
                ".then(function(r) { return r.json(); })", escaped);
        }

        if (_generatedFiles.TryGetValue("teams-data.json", out var teamsJson))
        {
            var escaped = teamsJson.Replace("\\", "\\\\").Replace("'", "\\'")
                .Replace("\r", "").Replace("\n", "");
            html = ReplaceFetchPattern(html, "fetch('teams-data.json')",
                ".then(function(r) { return r.json(); })", escaped);
        }

        return html;
    }

    private static string ReplaceFetchPattern(string html, string fetchPart, string thenPart, string escapedJson)
    {
        var idx = html.IndexOf(fetchPart, StringComparison.Ordinal);
        if (idx < 0) return html;

        var afterFetch = idx + fetchPart.Length;
        var thenIdx = html.IndexOf(thenPart, afterFetch, StringComparison.Ordinal);
        if (thenIdx < 0) return html;

        var endIdx = thenIdx + thenPart.Length;
        return string.Concat(
            html.AsSpan(0, idx),
            $"Promise.resolve(JSON.parse('{escapedJson}'))",
            html.AsSpan(endIdx));
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
        var label = fileName.ToLowerInvariant() switch
        {
            "index.html" => "Home",
            "standings.html" => "Standings",
            "fixtures.html" => "Fixtures",
            "results.html" => "Results",
            "players.html" => "Players",
            "divisions.html" => "Divisions",
            "competitions.html" => "Competitions",
            _ => null
        };
        if (label != null && PreviewPagePicker.ItemsSource is IList<string> items)
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
