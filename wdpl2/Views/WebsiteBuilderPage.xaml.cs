using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
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
            
            LoadSettings();
        }
        
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
            
            LeagueNameEntry.Text = settings.LeagueName;
            SubtitleEntry.Text = settings.LeagueSubtitle;
            
            PrimaryColorEntry.Text = settings.PrimaryColor;
            SecondaryColorEntry.Text = settings.SecondaryColor;
            AccentColorEntry.Text = settings.AccentColor;
            
            ShowStandingsCheck.IsChecked = settings.ShowStandings;
            ShowFixturesCheck.IsChecked = settings.ShowFixtures;
            ShowResultsCheck.IsChecked = settings.ShowResults;
            ShowPlayerStatsCheck.IsChecked = settings.ShowPlayerStats;
            ShowDivisionsCheck.IsChecked = settings.ShowDivisions;
            ShowGalleryCheck.IsChecked = settings.ShowGallery;
            
            // Update gallery count
            UpdateGalleryCount();
            
            FtpHostEntry.Text = settings.FtpHost;
            FtpPortEntry.Text = settings.FtpPort.ToString();
            FtpUsernameEntry.Text = settings.FtpUsername;
            FtpPasswordEntry.Text = settings.FtpPassword;
            FtpPathEntry.Text = settings.RemotePath;
            
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
        
        private void SaveCurrentSettings()
        {
            var settings = League.WebsiteSettings;
            
            settings.LeagueName = LeagueNameEntry.Text?.Trim() ?? "My Pool League";
            settings.LeagueSubtitle = SubtitleEntry.Text?.Trim() ?? "Weekly 8-Ball Competition";
            
            settings.PrimaryColor = PrimaryColorEntry.Text?.Trim() ?? "#3B82F6";
            settings.SecondaryColor = SecondaryColorEntry.Text?.Trim() ?? "#10B981";
            settings.AccentColor = AccentColorEntry.Text?.Trim() ?? "#F59E0B";
            
            settings.ShowStandings = ShowStandingsCheck.IsChecked;
            settings.ShowFixtures = ShowFixturesCheck.IsChecked;
            settings.ShowResults = ShowResultsCheck.IsChecked;
            settings.ShowPlayerStats = ShowPlayerStatsCheck.IsChecked;
            settings.ShowDivisions = ShowDivisionsCheck.IsChecked;
            settings.ShowGallery = ShowGalleryCheck.IsChecked;
            
            settings.FtpHost = FtpHostEntry.Text?.Trim() ?? "";
            
            if (int.TryParse(FtpPortEntry.Text, out int port))
            {
                settings.FtpPort = port;
            }
            
            settings.FtpUsername = FtpUsernameEntry.Text?.Trim() ?? "";
            settings.FtpPassword = FtpPasswordEntry.Text?.Trim() ?? "";
            settings.RemotePath = FtpPathEntry.Text?.Trim() ?? "/public_html/";
            
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
                
                if (success)
                {
                    await DisplayAlert("? Success", message, "OK");
                }
                else
                {
                    await DisplayAlert("? Connection Failed", message, "OK");
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                StatusLabel.IsVisible = true;
                
                await DisplayAlert("Error", $"Failed to test connection:\n\n{ex.Message}", "OK");
            }
            finally
            {
                TestConnectionBtn.IsEnabled = true;
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
                    // Read the file
                    using var stream = await result.OpenReadAsync();
                    using var memoryStream = new System.IO.MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    _uploadedLogoData = memoryStream.ToArray();
                    
                    // Update UI
                    LogoStatusLabel.Text = $"? {result.FileName}";
                    LogoStatusLabel.TextColor = Color.FromArgb("#10B981");
                    
                    // Show preview
                    var imageOptimizer = new ImageOptimizationService();
                    var mimeType = imageOptimizer.GetMimeType(result.FileName);
                    var dataUrl = imageOptimizer.ToDataUrl(_uploadedLogoData, mimeType);
                    
                    LogoPreviewImage.Source = ImageSource.FromStream(() => new System.IO.MemoryStream(_uploadedLogoData));
                    LogoPreviewImage.IsVisible = true;
                    
                    StatusLabel.Text = "Logo uploaded successfully";
                    StatusLabel.TextColor = Color.FromArgb("#10B981");
                    StatusLabel.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error uploading logo: {ex.Message}";
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
                
                // Generate website files
                var generator = new WebsiteGenerator(League, League.WebsiteSettings);
                _generatedFiles = generator.GenerateWebsite();
                
                // Show preview frame
                PreviewFrame.IsVisible = true;
                PreviewPagePicker.SelectedIndex = 0; // Select "Home"
                
                // Load home page in WebView
                LoadPreviewPage("index.html");
                
                StatusLabel.Text = $"? Preview ready - {_generatedFiles.Count} file(s) generated";
                StatusLabel.TextColor = Color.FromArgb("#10B981");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                
                await DisplayAlert("Error", $"Failed to generate preview:\n\n{ex.Message}", "OK");
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
            
            // Replace relative CSS references with inline styles or embedded data
            if (fileName != "style.css" && _generatedFiles.ContainsKey("style.css"))
            {
                var css = _generatedFiles["style.css"];
                // Inject CSS into the HTML
                html = html.Replace("<link rel=\"stylesheet\" href=\"style.css\">", 
                                   $"<style>{css}</style>");
            }
            
            PreviewWebView.Source = new HtmlWebViewSource { Html = html };
        }
        
        private async void OnGenerateAndUploadClicked(object sender, EventArgs e)
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
                
                var confirm = await DisplayAlert(
                    "Upload Website",
                    $"This will generate and upload your website to:\n\n{League.WebsiteSettings.FtpHost}{League.WebsiteSettings.RemotePath}\n\nContinue?",
                    "Yes, Upload",
                    "Cancel");
                
                if (!confirm) return;
                
                StatusLabel.IsVisible = true;
                UploadProgress.IsVisible = true;
                GenerateBtn.IsEnabled = false;
                
                // Generate website
                StatusLabel.Text = "Generating website...";
                StatusLabel.TextColor = Color.FromArgb("#3B82F6");
                
                var generator = new WebsiteGenerator(League, League.WebsiteSettings);
                var files = generator.GenerateWebsite();
                
                // Upload
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
                
                StatusLabel.Text = message;
                StatusLabel.TextColor = success ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
                
                if (success)
                {
                    League.WebsiteSettings.LastUploaded = DateTime.Now;
                    DataStore.Save();
                    
                    await DisplayAlert(
                        "? Upload Complete",
                        $"Your website has been uploaded successfully!\n\n" +
                        $"Files uploaded: {files.Count}\n" +
                        $"Visit: http://{League.WebsiteSettings.FtpHost}",
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
                StatusLabel.IsVisible = true;
                
                await DisplayAlert("Error", $"Failed to upload website:\n\n{ex.Message}", "OK");
            }
            finally
            {
                GenerateBtn.IsEnabled = true;
                UploadProgress.IsVisible = false;
            }
        }
        
        private async void OnSaveSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                SaveCurrentSettings();
                DataStore.Save();
                
                StatusLabel.Text = "? Settings saved successfully";
                StatusLabel.TextColor = Color.FromArgb("#10B981");
                StatusLabel.IsVisible = true;
                
                await DisplayAlert("? Saved", "Website settings saved successfully.", "OK");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                StatusLabel.IsVisible = true;
                
                await DisplayAlert("Error", $"Failed to save settings:\n\n{ex.Message}", "OK");
            }
        }

        private void OnTemplateChanged(object sender, EventArgs e)
        {
            if (TemplatePicker.SelectedItem is WebsiteTemplate template)
            {
                TemplateDescription.Text = template.Description;
                TemplateDescription.IsVisible = true;
                TemplateFeatures.Text = $"Features: {string.Join(", ", template.Features)}";
                TemplateFeatures.IsVisible = true;
            }
        }
        
        private void UpdateGalleryCount()
        {
            var count = League.WebsiteSettings.GalleryImages.Count;
            GalleryCountLabel.Text = count == 0 
                ? "No images in gallery" 
                : $"{count} image{(count == 1 ? "" : "s")} in gallery";
            GalleryCountLabel.TextColor = count > 0 
                ? Color.FromArgb("#10B981") 
                : Color.FromArgb("#6B7280");
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
                        
                        // Get image dimensions
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
                    StatusLabel.Text = $"? Added {addedCount} image(s) to gallery";
                    StatusLabel.TextColor = Color.FromArgb("#10B981");
                    
                    await DisplayAlert("Success", $"Added {addedCount} image(s) to the gallery.", "OK");
                }
                else
                {
                    StatusLabel.Text = "No images were added";
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
                await DisplayAlert("Empty Gallery", "No images in gallery yet. Click 'Add Photos' to upload images.", "OK");
                return;
            }
            
            // Show action sheet with options
            var action = await DisplayActionSheet(
                $"Manage Gallery ({League.WebsiteSettings.GalleryImages.Count} images)",
                "Cancel",
                "Clear All",
                "View Images",
                "Set Captions");
            
            if (action == "Clear All")
            {
                var confirm = await DisplayAlert(
                    "Clear Gallery",
                    $"Remove all {League.WebsiteSettings.GalleryImages.Count} images from gallery?",
                    "Yes, Clear",
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
            else if (action == "View Images")
            {
                var imageList = string.Join("\n", League.WebsiteSettings.GalleryImages.Select((img, i) => 
                    $"{i + 1}. {img.FileName} ({img.Width}x{img.Height}) - {img.Category}"));
                
                await DisplayAlert("Gallery Images", imageList, "OK");
            }
            else if (action == "Set Captions")
            {
                await DisplayAlert("Set Captions", "Caption editing will be available in the full gallery manager.", "OK");
            }
        }
    }
}
