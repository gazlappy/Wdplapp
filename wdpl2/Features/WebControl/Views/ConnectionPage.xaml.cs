using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Web;

namespace Wdpl2.Views.WebControl;

/// <summary>
/// Backend address, administrator credentials, database details and deployment.
/// </summary>
/// <remarks>
/// Code and configuration deploy separately on purpose: redeploying code must
/// never be able to overwrite working credentials on the server, which is how
/// the previous backend produced hard-to-diagnose outages.
/// </remarks>
public partial class ConnectionPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private readonly WebDeployService _deploy;
    private WebConnection _connection = new();

    public ConnectionPage(WebDeployService deploy)
    {
        InitializeComponent();
        _deploy = deploy;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _connection = await WebConnection.LoadAsync();
        BaseUrlEntry.Text = _connection.BaseUrl;
        AdminUserEntry.Text = _connection.AdminUser;
        AdminPasswordEntry.Text = _connection.AdminPassword;

        var settings = League.WebsiteSettings;
        DbHostEntry.Text = settings.BackendDbHost;
        DbNameEntry.Text = settings.BackendDbName;
        DbUserEntry.Text = settings.BackendDbUser;
        DbPasswordEntry.Text = settings.BackendDbPassword;

        TargetLabel.Text = string.IsNullOrWhiteSpace(settings.FtpHost)
            ? "No FTP host configured. Set one under Website → Deployment."
            : $"{settings.FtpHost}{settings.GetEffectiveBackendRemotePath()}";
    }

    private async Task<bool> SaveAsync()
    {
        _connection.BaseUrl = BaseUrlEntry.Text?.Trim() ?? "";
        _connection.AdminUser = AdminUserEntry.Text?.Trim() ?? "";
        _connection.AdminPassword = AdminPasswordEntry.Text ?? "";

        try
        {
            _connection.ResolveEndpoint();
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
            return false;
        }

        await _connection.SaveAsync();

        var settings = League.WebsiteSettings;
        settings.BackendDbHost = DbHostEntry.Text?.Trim() ?? "localhost";
        settings.BackendDbName = DbNameEntry.Text?.Trim() ?? "";
        settings.BackendDbUser = DbUserEntry.Text?.Trim() ?? "";
        settings.BackendDbPassword = DbPasswordEntry.Text ?? "";
        DataStore.SaveJsonOnly();

        return true;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (await SaveAsync())
            Report("Connection saved.", error: false);
    }

    private async void OnPingClicked(object? sender, EventArgs e)
    {
        if (!await SaveAsync()) return;

        await RunAsync(PingButton, async () =>
        {
            using var client = new WebApiClient(_connection);

            // Unauthenticated first: separates "cannot reach it" from "wrong password".
            var ping = await client.PublicAsync("system", "ping");
            var secure = ping.TryGetProperty("secure", out var s) && s.ValueKind == JsonValueKind.True;
            if (!secure)
            {
                Report("Reached the backend, but it does not consider the request secure. Check the site's HTTPS certificate.", error: true);
                return;
            }

            // Must be the authenticated call: whoami reports whoever the request
            // is authenticated as, so asking it anonymously always answers "no
            // one" and would report a working password as a failure.
            var who = await client.AdminAsync("system", "whoami");
            var admin = who.TryGetProperty("admin", out var a) && a.ValueKind == JsonValueKind.True;

            Report(admin
                ? "Backend reachable over HTTPS and the administrator sign-in works."
                : "Backend reachable over HTTPS, but it did not accept this username and password. Re-check them, then use \"Deploy with configuration\" to write them to the server.",
                error: !admin);
        });
    }

    private async void OnDeployCodeClicked(object? sender, EventArgs e)
    {
        if (!await SaveAsync()) return;

        if (!await DisplayAlert("Deploy backend?",
                $"Upload the backend code to:\n\n{TargetLabel.Text}\n\n" +
                "Existing credentials on the server are left untouched. FTP uploads are not atomic, so avoid doing this mid-match.",
                "Deploy", "Cancel"))
            return;

        await DeployAsync(configPhp: null);
    }

    private async void OnDeployConfigClicked(object? sender, EventArgs e)
    {
        if (!await SaveAsync()) return;

        if (string.IsNullOrWhiteSpace(DbNameEntry.Text) || string.IsNullOrWhiteSpace(DbUserEntry.Text))
        {
            Report("Enter the database name and user before deploying configuration.", error: true);
            return;
        }

        if (string.IsNullOrEmpty(_connection.AdminPassword))
        {
            Report("Enter an administrator password before deploying configuration.", error: true);
            return;
        }

        if (!await DisplayAlert("Deploy with configuration?",
                $"Upload the backend code AND overwrite api/config.php on:\n\n{TargetLabel.Text}\n\n" +
                "This replaces the server's database credentials and administrator account with what is entered here.",
                "Deploy configuration", "Cancel"))
            return;

        var configPhp = WebDeployService.BuildConfigPhp(
            League.WebsiteSettings,
            _connection,
            PasswordHash.Create(_connection.AdminPassword),
            PasswordHash.CreatePepper());

        await DeployAsync(configPhp);
    }

    private async Task DeployAsync(string? configPhp)
    {
        DeployProgress.IsVisible = true;
        DeployProgress.Progress = 0;

        var progress = new Progress<UploadProgress>(p =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DeployProgress.Progress = p.PercentComplete / 100.0;
                StatusLabel.Text = p.Status;
            }));

        await RunAsync(DeployCodeButton, async () =>
        {
            var result = await _deploy.DeployAsync(League.WebsiteSettings, configPhp, progress);

            if (!result.Success)
            {
                var detail = result.Problems.Count > 0
                    ? "\n\n" + string.Join("\n", result.Problems.Take(10))
                    : "";
                Report(result.Message + detail, error: true);
                return;
            }

            Report($"Uploaded {result.FilesUploaded} files. Now choose Install tables.", error: false);
        });

        DeployProgress.IsVisible = false;
    }

    private async void OnInstallClicked(object? sender, EventArgs e)
    {
        if (!await SaveAsync()) return;

        await RunAsync(InstallButton, async () =>
        {
            using var client = new WebApiClient(_connection);
            var result = await client.AdminAsync("system", "install");

            var changed = 0;
            var total = 0;
            if (result.TryGetProperty("applied", out var applied) && applied.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in applied.EnumerateArray())
                {
                    total++;
                    if (entry.TryGetProperty("changed", out var c) && c.ValueKind == JsonValueKind.True)
                        changed++;
                }
            }

            Report(changed == 0
                ? $"All {total} module(s) already up to date."
                : $"Installed or updated {changed} of {total} module(s).",
                error: false);
        });
    }

    /// <summary>Shared button/error handling so every action reports failures the same way.</summary>
    private async Task RunAsync(Button button, Func<Task> action)
    {
        var wasEnabled = button.IsEnabled;
        button.IsEnabled = false;
        try
        {
            await action();
        }
        catch (WebApiException ex)
        {
            Report(ex.Code == "not_configured"
                ? ex.Message + " Use \"Deploy with configuration\"."
                : ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report(ex.Message, error: true);
        }
        finally
        {
            button.IsEnabled = wasEnabled;
        }
    }

    private void Report(string message, bool error)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = Color.FromArgb(error ? "#EF4444" : "#10B981");
    }
}
