using System.Collections.ObjectModel;
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
            "Home", "Standings", "Fixtures", "Results", "Players", "Divisions" 
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
            
            var generator = new WebsiteGenerator(League, League.WebsiteSettings);
            _generatedFiles = generator.GenerateWebsite();
            
            PreviewFrame.IsVisible = true;
            PreviewPagePicker.SelectedIndex = 0;
            LoadPreviewPage("index.html");
            
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
}
