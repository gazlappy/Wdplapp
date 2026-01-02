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
            
            // Wire up GitHub URL preview updates
            GitHubUsernameEntry.TextChanged += (_, _) => UpdateGitHubUrlPreview();
            GitHubRepoEntry.TextChanged += (_, _) => UpdateGitHubUrlPreview();
            
            LoadSettings();
            
            // Default to Local Export
            DeploymentMethodPicker.SelectedIndex = 0;
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
            
            GitHubUrlPreview.Text = $"Your site will be at: https://{username}.github.io/{repo}/";
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
                        $"Website exported successfully!\n\nLocation: {outputPath}\n\n" +
                        $"Files: {files.Count}\n\n" +
                        "You can now upload these files to any web host, or open index.html in a browser to preview.",
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
                    $"This will deploy your website to:\n\nhttps://{username}.github.io/{repoName}/\n\nContinue?",
                    "Yes, Deploy",
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
                        // Increment progress gradually
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
                        "?? Deployment Complete!",
                        $"Your website has been deployed to GitHub Pages!\n\n" +
                        $"Files: {files.Count}\n" +
                        $"URL: {siteUrl}\n\n" +
                        "Note: It may take a few minutes for GitHub Pages to build and publish your site.",
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
                    await DisplayAlert("Missing Information", "Please enter your GitHub token, username, and repository name first.", "OK");
                    return;
                }
                
                StatusLabel.Text = "Checking GitHub Pages status...";
                StatusLabel.TextColor = Color.FromArgb("#3B82F6");
                StatusLabel.IsVisible = true;
                CheckGitHubStatusBtn.IsEnabled = false;
                GitHubStatusFrame.IsVisible = true;
                GitHubStatusLabel.Text = "? Checking status...";
                
                var gitHubService = new GitHubPagesService(token, username, repoName);
                var (enabled, url, status, buildError) = await gitHubService.GetPagesStatusAsync();
                
                if (!enabled)
                {
                    if (status == "not_enabled")
                    {
                        GitHubStatusLabel.Text = "? GitHub Pages is not enabled for this repository.\n\nGo to repository Settings > Pages to enable it.";
                        GitHubStatusFrame.BackgroundColor = Color.FromArgb("#FEE2E2");
                        StatusLabel.Text = "GitHub Pages not enabled";
                        StatusLabel.TextColor = Color.FromArgb("#EF4444");
                    }
                    else
                    {
                        GitHubStatusLabel.Text = $"? Error checking status: {buildError ?? status}";
                        GitHubStatusFrame.BackgroundColor = Color.FromArgb("#FEE2E2");
                        StatusLabel.Text = "Error checking status";
                        StatusLabel.TextColor = Color.FromArgb("#EF4444");
                    }
                }
                else
                {
                    switch (status?.ToLowerInvariant())
                    {
                        case "built":
                            GitHubStatusLabel.Text = $"? Site is LIVE!\n\n?? {url ?? $"https://{username}.github.io/{repoName}/"}";
                            GitHubStatusFrame.BackgroundColor = Color.FromArgb("#D1FAE5");
                            StatusLabel.Text = "? Site is live!";
                            StatusLabel.TextColor = Color.FromArgb("#10B981");
                            break;
                            
                        case "building":
                            GitHubStatusLabel.Text = "?? Site is currently building...\n\nPlease wait 1-2 minutes and check again.";
                            GitHubStatusFrame.BackgroundColor = Color.FromArgb("#FEF3C7");
                            StatusLabel.Text = "Site is building...";
                            StatusLabel.TextColor = Color.FromArgb("#F59E0B");
                            break;
                            
                        case "errored":
                            GitHubStatusLabel.Text = $"? Build failed!\n\nError: {buildError ?? "Unknown error"}";
                            GitHubStatusFrame.BackgroundColor = Color.FromArgb("#FEE2E2");
                            StatusLabel.Text = "Build failed";
                            StatusLabel.TextColor = Color.FromArgb("#EF4444");
                            break;
                            
                        default:
                            GitHubStatusLabel.Text = $"?? Status: {status}\n\nURL: {url ?? "Not available yet"}";
                            GitHubStatusFrame.BackgroundColor = Color.FromArgb("#FEF3C7");
                            StatusLabel.Text = $"Status: {status}";
                            StatusLabel.TextColor = Color.FromArgb("#3B82F6");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                GitHubStatusLabel.Text = $"? Error: {ex.Message}";
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
        
        #endregion
        
        #region Common Actions
        
        private async void OnDeployClicked(object sender, EventArgs e)
        {
            // Route to appropriate deployment method
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
                    $"This will generate and upload your website files to:\n\n" +
                    $"Host: {League.WebsiteSettings.FtpHost}\n" +
                    $"Path: {remotePath}\n\n" +
                    $"Files will include: index.html, style.css, and other pages.\n\n" +
                    "Continue?",
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
                
                // Log which files are being generated
                System.Diagnostics.Debug.WriteLine($"Generated {files.Count} files:");
                foreach (var f in files.Keys)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {f}");
                }
                
                // Verify index.html is in the files
                if (!files.ContainsKey("index.html"))
                {
                    StatusLabel.Text = "Error: index.html was not generated!";
                    StatusLabel.TextColor = Color.FromArgb("#EF4444");
                    await DisplayAlert("Generation Error", "The website generator did not create index.html. Please check your settings.", "OK");
                    return;
                }
                
                // Upload
                StatusLabel.Text = $"Uploading to {League.WebsiteSettings.FtpHost}{remotePath}...";
                
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
                    
                    // Verify the upload
                    StatusLabel.Text = "Verifying upload...";
                    var (verified, foundFiles, verifyMessage) = await ftpService.VerifyUploadAsync();
                    
                    var resultMessage = new System.Text.StringBuilder();
                    resultMessage.AppendLine($"? Upload completed successfully!");
                    resultMessage.AppendLine();
                    resultMessage.AppendLine($"Files uploaded: {files.Count}");
                    resultMessage.AppendLine($"Location: {League.WebsiteSettings.FtpHost}{remotePath}");
                    resultMessage.AppendLine();
                    
                    if (verified)
                    {
                        resultMessage.AppendLine("? Verification: index.html found on server!");
                        resultMessage.AppendLine();
                        resultMessage.AppendLine("Your website should now be live at:");
                        resultMessage.AppendLine($"http://your-domain.com{(remotePath == "/" ? "" : remotePath)}");
                        resultMessage.AppendLine();
                        resultMessage.AppendLine("(Replace 'your-domain.com' with your actual domain)");
                    }
                    else
                    {
                        resultMessage.AppendLine("?? Warning: Could not verify index.html on server");
                        resultMessage.AppendLine();
                        resultMessage.AppendLine("This might mean:");
                        resultMessage.AppendLine("• Files uploaded to wrong directory");
                        resultMessage.AppendLine("• Need to adjust Remote Path setting");
                        resultMessage.AppendLine();
                        resultMessage.AppendLine("Try 'Test Connection' to see what's in the folder.");
                    }
                    
                    await DisplayAlert("Upload Result", resultMessage.ToString(), "OK");
                    
                    StatusLabel.Text = verified ? "? Upload verified!" : "? Uploaded (verification failed)";
                    StatusLabel.TextColor = Color.FromArgb("#10B981");
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
        
        #endregion
    }
}
