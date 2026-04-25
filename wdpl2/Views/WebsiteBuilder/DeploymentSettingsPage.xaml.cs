using Microsoft.EntityFrameworkCore;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class DeploymentSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public DeploymentSettingsPage()
    {
        InitializeComponent();
        
        GitHubUsernameEntry.TextChanged += (_, _) => UpdateGitHubUrlPreview();
        GitHubRepoEntry.TextChanged += (_, _) => UpdateGitHubUrlPreview();
        
        LoadSettings();
        DeploymentMethodPicker.SelectedIndex = 0;
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;

        FtpHostEntry.Text = settings.FtpHost;
        FtpPortEntry.Text = settings.FtpPort.ToString();
        FtpUsernameEntry.Text = settings.FtpUsername;
        FtpPasswordEntry.Text = settings.FtpPassword;
        FtpPathEntry.Text = settings.RemotePath;

        GitHubTokenEntry.Text = settings.GitHubToken;
        GitHubUsernameEntry.Text = settings.GitHubUsername;
        GitHubRepoEntry.Text = settings.GitHubRepoName;

        // Cloud sync
        CloudSyncSwitch.IsToggled = settings.EnableCloudSync;
        UpdateLastSyncLabel();
    }

    private void SaveSettings()
    {
        var settings = League.WebsiteSettings;

        settings.FtpHost = FtpHostEntry.Text?.Trim() ?? "";
        if (int.TryParse(FtpPortEntry.Text, out int port))
            settings.FtpPort = port;
        settings.FtpUsername = FtpUsernameEntry.Text?.Trim() ?? "";
        settings.FtpPassword = FtpPasswordEntry.Text?.Trim() ?? "";
        settings.RemotePath = FtpPathEntry.Text?.Trim() ?? "/public_html/";

        settings.GitHubToken = GitHubTokenEntry.Text?.Trim() ?? "";
        settings.GitHubUsername = GitHubUsernameEntry.Text?.Trim() ?? "";
        settings.GitHubRepoName = GitHubRepoEntry.Text?.Trim() ?? "";

        settings.EnableCloudSync = CloudSyncSwitch.IsToggled;

        DataStore.Save();
    }

    protected override void OnDisappearing()
    {
        // Persist any in-flight credential edits when the user leaves the page.
        try { SaveSettings(); } catch { /* non-fatal */ }
        base.OnDisappearing();
    }

    private async void OnSaveGitHubCredsClicked(object sender, EventArgs e)
    {
        try
        {
            SaveSettings();
            StatusLabel.Text = "\u2713 GitHub credentials saved";
            StatusLabel.TextColor = Color.FromArgb("#10B981");
            StatusLabel.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save Failed", ex.Message, "OK");
        }
    }

    private async void OnSaveFtpCredsClicked(object sender, EventArgs e)
    {
        try
        {
            SaveSettings();
            StatusLabel.Text = "\u2713 FTP credentials saved";
            StatusLabel.TextColor = Color.FromArgb("#10B981");
            StatusLabel.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save Failed", ex.Message, "OK");
        }
    }

    private void OnDeploymentMethodChanged(object sender, EventArgs e)
    {
        LocalExportFrame.IsVisible = false;
        GitHubFrame.IsVisible = false;
        FtpFrame.IsVisible = false;
        
        switch (DeploymentMethodPicker.SelectedIndex)
        {
            case 0:
                LocalExportFrame.IsVisible = true;
                break;
            case 1:
                GitHubFrame.IsVisible = true;
                UpdateGitHubUrlPreview();
                break;
            case 2:
                FtpFrame.IsVisible = true;
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

    private Dictionary<string, string>? GenerateWebsite()
    {
        // Competitions live in SQLite, not JSON — load them into LeagueData
        // so the website generator can include them.
        try
        {
            using var context = new LeagueContext();
            context.Database.EnsureCreated();
            League.Competitions = context.Competitions.AsNoTracking().ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebGen] Error loading competitions: {ex.Message}");
        }

        var generator = new WebsiteGenerator(League, League.WebsiteSettings);
        return generator.GenerateWebsite();
    }

    private async void OnExportToFolderClicked(object sender, EventArgs e)
    {
        try
        {
            SaveSettings();
            
            StatusLabel.Text = "Generating website...";
            StatusLabel.TextColor = Color.FromArgb("#3B82F6");
            StatusLabel.IsVisible = true;
            ExportToFolderBtn.IsEnabled = false;
            
            var files = GenerateWebsite();
            if (files == null || files.Count == 0)
            {
                await DisplayAlert("Error", "Failed to generate website files.", "OK");
                return;
            }
            
            var exportFolder = LocalExportService.GetDefaultExportFolder();
            var exportService = new LocalExportService();
            var progress = new Progress<string>(msg =>
                MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = msg));
            
            var (success, message, outputPath) = await exportService.ExportToFolderAsync(files, exportFolder, progress);
            
            StatusLabel.Text = message;
            StatusLabel.TextColor = success ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
            
            if (success)
                await DisplayAlert("Export Complete", $"Location: {outputPath}\nFiles: {files.Count}", "OK");
            else
                await DisplayAlert("Export Failed", message, "OK");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.TextColor = Color.FromArgb("#EF4444");
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
            SaveSettings();
            
            StatusLabel.Text = "Generating website...";
            StatusLabel.TextColor = Color.FromArgb("#3B82F6");
            StatusLabel.IsVisible = true;
            ExportAsZipBtn.IsEnabled = false;
            
            var files = GenerateWebsite();
            if (files == null || files.Count == 0)
            {
                await DisplayAlert("Error", "Failed to generate website files.", "OK");
                return;
            }
            
            var exportService = new LocalExportService();
            var progress = new Progress<string>(msg =>
                MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = msg));
            
            var (success, message, zipStream) = await exportService.ExportToMemoryStreamAsync(files, progress);
            
            if (success && zipStream != null)
            {
                StatusLabel.Text = "Preparing to share...";
                
                var leagueName = League.WebsiteSettings.LeagueName ?? "PoolLeague";
                var safeName = string.Join("_", leagueName.Split(Path.GetInvalidFileNameChars()));
                var fileName = $"{safeName}_Website_{DateTime.Now:yyyyMMdd}.zip";
                var cachePath = Path.Combine(FileSystem.CacheDirectory, fileName);
                
                await using (var fileStream = File.Create(cachePath))
                {
                    zipStream.Position = 0;
                    await zipStream.CopyToAsync(fileStream);
                }
                
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = "Export Website ZIP",
                    File = new ShareFile(cachePath)
                });
                
                StatusLabel.Text = $"ZIP created ({files.Count} files)";
                StatusLabel.TextColor = Color.FromArgb("#10B981");
            }
            else
            {
                StatusLabel.Text = message;
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.TextColor = Color.FromArgb("#EF4444");
        }
        finally
        {
            ExportAsZipBtn.IsEnabled = true;
        }
    }

    private async void OnDeployToGitHubClicked(object sender, EventArgs e)
    {
        try
        {
            SaveSettings();
            
            var token = GitHubTokenEntry.Text?.Trim();
            var username = GitHubUsernameEntry.Text?.Trim();
            var repoName = GitHubRepoEntry.Text?.Trim();
            
            if (string.IsNullOrWhiteSpace(token))
            {
                await DisplayAlert("Missing Token", "Please enter your GitHub Personal Access Token.", "OK");
                return;
            }
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(repoName))
            {
                await DisplayAlert("Missing Info", "Please enter username and repository name.", "OK");
                return;
            }
            
            var confirm = await DisplayAlert("Deploy", $"Deploy to https://{username}.github.io/{repoName}/?", "Deploy", "Cancel");
            if (!confirm) return;
            
            StatusLabel.Text = "Connecting to GitHub...";
            StatusLabel.TextColor = Color.FromArgb("#3B82F6");
            StatusLabel.IsVisible = true;
            ProgressBar.IsVisible = true;
            ProgressBar.Progress = 0;
            DeployToGitHubBtn.IsEnabled = false;
            
            var gitHubService = new GitHubPagesService(token, username, repoName);
            
            var (validCreds, credMessage) = await gitHubService.ValidateConnectionAsync();
            if (!validCreds)
            {
                StatusLabel.Text = credMessage;
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
                await DisplayAlert("Auth Failed", credMessage, "OK");
                return;
            }
            
            StatusLabel.Text = "Generating website...";
            ProgressBar.Progress = 0.1;
            
            var files = GenerateWebsite();
            if (files == null || files.Count == 0)
            {
                await DisplayAlert("Error", "Failed to generate website files.", "OK");
                return;
            }
            
            ProgressBar.Progress = 0.2;
            
            var progress = new Progress<string>(msg =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StatusLabel.Text = msg;
                    if (ProgressBar.Progress < 0.9)
                        ProgressBar.Progress += 0.05;
                });
            });
            
            var (success, message, siteUrl) = await gitHubService.DeployAsync(files, GitHubCreateRepoCheck.IsChecked, progress);
            
            ProgressBar.Progress = 1.0;
            StatusLabel.Text = message;
            StatusLabel.TextColor = success ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
            
            if (success)
            {
                League.WebsiteSettings.LastUploaded = DateTime.Now;
                DataStore.Save();
                await DisplayAlert("Deployed!", $"Files: {files.Count}\nURL: {siteUrl}", "OK");
            }
            else
            {
                await DisplayAlert("Failed", message, "OK");
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.TextColor = Color.FromArgb("#EF4444");
        }
        finally
        {
            DeployToGitHubBtn.IsEnabled = true;
            ProgressBar.IsVisible = false;
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
                await DisplayAlert("Missing", "Enter token, username, and repository.", "OK");
                return;
            }
            
            StatusLabel.Text = "Checking status...";
            StatusLabel.TextColor = Color.FromArgb("#3B82F6");
            StatusLabel.IsVisible = true;
            CheckGitHubStatusBtn.IsEnabled = false;
            GitHubStatusFrame.IsVisible = true;
            GitHubStatusLabel.Text = "Checking...";
            
            var gitHubService = new GitHubPagesService(token, username, repoName);
            var (enabled, url, status, buildError) = await gitHubService.GetPagesStatusAsync();
            
            if (!enabled)
            {
                GitHubStatusLabel.Text = status == "not_enabled" ? "GitHub Pages not enabled" : $"Error: {buildError ?? status}";
                GitHubStatusFrame.BackgroundColor = Color.FromArgb("#FEE2E2");
                StatusLabel.Text = "Not enabled";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
            }
            else
            {
                switch (status?.ToLowerInvariant())
                {
                    case "built":
                        GitHubStatusLabel.Text = $"LIVE: {url}";
                        GitHubStatusFrame.BackgroundColor = Color.FromArgb("#D1FAE5");
                        StatusLabel.Text = "Site is live!";
                        StatusLabel.TextColor = Color.FromArgb("#10B981");
                        break;
                    case "building":
                        GitHubStatusLabel.Text = "Building...";
                        GitHubStatusFrame.BackgroundColor = Color.FromArgb("#FEF3C7");
                        StatusLabel.Text = "Building...";
                        StatusLabel.TextColor = Color.FromArgb("#F59E0B");
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
            GitHubStatusLabel.Text = ex.Message;
            GitHubStatusFrame.BackgroundColor = Color.FromArgb("#FEE2E2");
        }
        finally
        {
            CheckGitHubStatusBtn.IsEnabled = true;
        }
    }

    private async void OnTestConnectionClicked(object sender, EventArgs e)
    {
        try
        {
            SaveSettings();
            
            StatusLabel.Text = "Testing connection...";
            StatusLabel.TextColor = Color.FromArgb("#3B82F6");
            StatusLabel.IsVisible = true;
            TestConnectionBtn.IsEnabled = false;
            
            var ftpService = new FtpUploadService(League.WebsiteSettings);
            var (success, message) = await ftpService.TestConnectionAsync();
            
            StatusLabel.Text = message;
            StatusLabel.TextColor = success ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
            
            await DisplayAlert(success ? "Success" : "Failed", message, "OK");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.TextColor = Color.FromArgb("#EF4444");
        }
        finally
        {
            TestConnectionBtn.IsEnabled = true;
        }
    }

    private async void OnUploadFtpClicked(object sender, EventArgs e)
    {
        try
        {
            SaveSettings();

            if (string.IsNullOrWhiteSpace(League.WebsiteSettings.FtpHost))
            {
                await DisplayAlert("Not Configured", "Please configure FTP settings.", "OK");
                return;
            }

            var remotePath = League.WebsiteSettings.RemotePath ?? "/";
            if (!remotePath.StartsWith("/")) remotePath = "/" + remotePath;
            if (!remotePath.EndsWith("/")) remotePath += "/";

            StatusLabel.Text = "Generating website...";
            StatusLabel.TextColor = Color.FromArgb("#3B82F6");
            StatusLabel.IsVisible = true;
            UploadFtpBtn.IsEnabled = false;

            var allFiles = GenerateWebsite();
            if (allFiles == null || allFiles.Count == 0)
            {
                await DisplayAlert("Error", "Failed to generate website files.", "OK");
                return;
            }

            // Determine which files changed
            var previousHashes = League.WebsiteSettings.UploadedFileHashes;
            var changedFiles = FtpUploadService.GetChangedFiles(allFiles, previousHashes);

            Dictionary<string, string> filesToUpload;
            if (changedFiles.Count == 0)
            {
                var forceAll = await DisplayAlert("No Changes",
                    "All files are identical to the last upload. Upload everything anyway?",
                    "Upload All", "Cancel");
                if (!forceAll) return;
                filesToUpload = allFiles;
            }
            else if (changedFiles.Count < allFiles.Count && previousHashes.Count > 0)
            {
                var choice = await DisplayActionSheet(
                    $"{changedFiles.Count} of {allFiles.Count} files changed",
                    "Cancel", null,
                    $"Upload Changed Only ({changedFiles.Count} files)",
                    $"Upload All ({allFiles.Count} files)");
                if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;
                filesToUpload = choice.Contains("Changed Only") ? changedFiles : allFiles;
            }
            else
            {
                // First upload or all files changed
                var confirm = await DisplayAlert("Upload",
                    $"Upload {allFiles.Count} files to {League.WebsiteSettings.FtpHost}{remotePath}?",
                    "Upload", "Cancel");
                if (!confirm) return;
                filesToUpload = allFiles;
            }

            StatusLabel.Text = $"Uploading {filesToUpload.Count} file(s)...";
            ProgressBar.IsVisible = true;
            
            var ftpService = new FtpUploadService(League.WebsiteSettings);
            var progress = new Progress<UploadProgress>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ProgressBar.Progress = p.PercentComplete / 100.0;
                    StatusLabel.Text = p.Status;
                });
            });

            var (success, message) = await ftpService.UploadWebsiteAsync(filesToUpload, progress);

            StatusLabel.Text = success ? "Upload complete!" : message;
            StatusLabel.TextColor = success ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");

            if (success)
            {
                // Store hashes of ALL generated files (not just uploaded subset)
                // so next time we correctly detect what changed
                League.WebsiteSettings.UploadedFileHashes = FtpUploadService.BuildHashDictionary(allFiles);
                League.WebsiteSettings.LastUploaded = DateTime.Now;
                DataStore.Save();
                var skipped = allFiles.Count - filesToUpload.Count;
                var msg = skipped > 0
                    ? $"Uploaded {filesToUpload.Count} changed file(s), {skipped} unchanged file(s) skipped."
                    : $"Uploaded {filesToUpload.Count} file(s).";
                await DisplayAlert("Uploaded", msg, "OK");
            }
            else
            {
                await DisplayAlert("Failed", message, "OK");
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.TextColor = Color.FromArgb("#EF4444");
        }
        finally
        {
                    UploadFtpBtn.IsEnabled = true;
                        ProgressBar.IsVisible = false;
                    }
                }

                // ── Cloud Sync ─────────────────────────────────────────────

                private void OnCloudSyncToggled(object sender, ToggledEventArgs e)
                {
                    SaveSettings();
                }

                private void UpdateLastSyncLabel()
                {
                    var last = League.WebsiteSettings.LastCloudSyncUtc;
                    LastSyncLabel.Text = last == default
                        ? "Never synced"
                        : $"Last synced: {last.ToLocalTime():dd MMM yyyy HH:mm}";
                }

                private bool ValidateGitHubCredentials()
                {
                    return !string.IsNullOrWhiteSpace(GitHubTokenEntry.Text)
                        && !string.IsNullOrWhiteSpace(GitHubUsernameEntry.Text)
                        && !string.IsNullOrWhiteSpace(GitHubRepoEntry.Text);
                }

                private async void OnCloudValidateClicked(object sender, EventArgs e)
                {
                    if (!ValidateGitHubCredentials())
                    {
                        await DisplayAlert("Missing", "Enter your GitHub token, username, and repository in the GitHub Pages section above.", "OK");
                        return;
                    }

                    SaveSettings();
                    CloudValidateBtn.IsEnabled = false;
                    CloudSyncStatusFrame.IsVisible = true;
                    CloudSyncStatusLabel.Text = "Testing connection...";
                    CloudSyncStatusFrame.BackgroundColor = Color.FromArgb("#FEF3C7");

                    try
                    {
                        var s = League.WebsiteSettings;
                        using var sync = new CloudSyncService(s.GitHubToken, s.GitHubUsername, s.GitHubRepoName);
                        var (ok, msg) = await sync.ValidateAsync();

                        CloudSyncStatusLabel.Text = msg;
                        CloudSyncStatusFrame.BackgroundColor = ok ? Color.FromArgb("#D1FAE5") : Color.FromArgb("#FEE2E2");
                    }
                    catch (Exception ex)
                    {
                        CloudSyncStatusLabel.Text = ex.Message;
                        CloudSyncStatusFrame.BackgroundColor = Color.FromArgb("#FEE2E2");
                    }
                    finally
                    {
                        CloudValidateBtn.IsEnabled = true;
                    }
                }

                private async void OnCloudPushClicked(object sender, EventArgs e)
                {
                    if (!ValidateGitHubCredentials())
                    {
                        await DisplayAlert("Missing", "Enter your GitHub token, username, and repository in the GitHub Pages section above.", "OK");
                        return;
                    }

                    var confirm = await DisplayAlert("Push to Cloud",
                        "This will upload your current league data to the cloud, overwriting any existing cloud data. Continue?",
                        "Push", "Cancel");
                    if (!confirm) return;

                    SaveSettings();
                    CloudPushBtn.IsEnabled = false;
                    StatusLabel.IsVisible = true;
                    StatusLabel.Text = "Pushing to cloud...";
                    StatusLabel.TextColor = Color.FromArgb("#3B82F6");

                    try
                    {
                        var s = League.WebsiteSettings;
                        using var sync = new CloudSyncService(s.GitHubToken, s.GitHubUsername, s.GitHubRepoName);
                        var progress = new Progress<string>(msg =>
                            MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = msg));

                        var (ok, msg) = await sync.PushAsync(League, progress);

                        StatusLabel.Text = msg;
                        StatusLabel.TextColor = ok ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");

                        if (ok)
                        {
                            s.LastCloudSyncUtc = DateTime.UtcNow;
                            DataStore.Save();
                            UpdateLastSyncLabel();
                            await DisplayAlert("Pushed", "League data uploaded to cloud.", "OK");
                        }
                        else
                        {
                            await DisplayAlert("Failed", msg, "OK");
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusLabel.Text = $"Error: {ex.Message}";
                        StatusLabel.TextColor = Color.FromArgb("#EF4444");
                    }
                    finally
                    {
                        CloudPushBtn.IsEnabled = true;
                    }
                }

                private async void OnCloudPullClicked(object sender, EventArgs e)
                {
                    if (!ValidateGitHubCredentials())
                    {
                        await DisplayAlert("Missing", "Enter your GitHub token, username, and repository in the GitHub Pages section above.", "OK");
                        return;
                    }

                    var confirm = await DisplayAlert("Pull from Cloud",
                        "This will replace ALL local data with the cloud version. Make sure you have pushed your latest changes from another machine first.\n\nA backup will be created before pulling.",
                        "Pull", "Cancel");
                    if (!confirm) return;

                    SaveSettings();
                    CloudPullBtn.IsEnabled = false;
                    StatusLabel.IsVisible = true;
                    StatusLabel.Text = "Pulling from cloud...";
                    StatusLabel.TextColor = Color.FromArgb("#3B82F6");

                    try
                    {
                        var progress = new Progress<string>(msg =>
                            MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = msg));

                        var (ok, msg) = await DataStore.PullFromCloudAsync(progress);

                        StatusLabel.Text = msg;
                        StatusLabel.TextColor = ok ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");

                        if (ok)
                        {
                            UpdateLastSyncLabel();
                            LoadSettings();
                            await DisplayAlert("Pulled", "Local data replaced with cloud version. Restart the app to see all changes.", "OK");
                        }
                        else
                        {
                            await DisplayAlert("Failed", msg, "OK");
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusLabel.Text = $"Error: {ex.Message}";
                        StatusLabel.TextColor = Color.FromArgb("#EF4444");
                    }
                    finally
                    {
                        CloudPullBtn.IsEnabled = true;
                    }
                }
            }
