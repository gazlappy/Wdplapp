namespace Wdpl2;

using System.Threading.Tasks;
using Wdpl2.Services;
using Wdpl2.Views;

/// <summary>
/// Starts the app, and gets out of the way while the league opens.
/// </summary>
/// <remarks>
/// All of this used to happen in the constructor, before any window existed:
/// bringing up the database, reading twenty megabytes of league, rebuilding the
/// database copy if it had fallen behind, then reading every table back. On
/// Windows that was a slow start nobody complained about. On Android it was
/// fatal - the system kills an app that has not finished starting within about
/// ten seconds, and with a real league this one never did. Testing on a phone
/// ended in "Wdpl2 isn't responding" every time.
/// <para>
/// So the window comes up first with <see cref="StartupPage"/>, the work runs
/// off the thread that draws, and the shell replaces it when the league is
/// ready. The order of the work is exactly as it was - the database before the
/// file, the file before the season - because each step still depends on the
/// one before it.
/// </para>
/// </remarks>
public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly ISeasonService _seasonService;
    private readonly IThemeService _themeService;

    public App(IServiceProvider services, ISeasonService seasonService, IThemeService themeService)
    {
        _services = services;
        _seasonService = seasonService;
        _themeService = themeService;

        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var startup = new StartupPage();
        var window = new Window(startup);

        // Deliberately not awaited: the window has to be returned now, which is
        // the whole point. The work carries on behind it.
        _ = OpenTheLeagueAsync(window, startup);

        return window;
    }

    private static async Task SettleGalleryAsync(StartupPage startup)
    {
        var gallery = DataStore.Data.WebsiteSettings.GalleryImages;
        var waiting = gallery.Count(i => i.ImageData.Length > 0 || string.IsNullOrEmpty(i.StoredFile));
        if (waiting == 0) return;

        startup.Report($"Moving {waiting} photo(s) into the gallery…");
        try
        {
            var (moved, failed) = await Task.Run(() => GalleryStore.ForLeague().Settle(DataStore.Data.WebsiteSettings));
            if (moved > 0) await Task.Run(DataStore.Save);
            if (failed.Count > 0)
                System.Diagnostics.Debug.WriteLine($"Gallery: {failed.Count} photo(s) could not be moved: {string.Join(", ", failed)}");
        }
        catch (Exception ex)
        {
            // The league opens regardless; the photos stay where they were.
            System.Diagnostics.Debug.WriteLine($"Gallery: settling photos failed: {ex.Message}");
        }
    }

    private async Task OpenTheLeagueAsync(Window window, StartupPage startup)
    {
        try
        {
            // A restore chosen last time goes in before anything opens the
            // league - the one moment nothing is holding it.
            string? restored = null;
            if (Services.BackupFiles.HasPending(AppPaths.Data))
            {
                startup.Report("Restoring your backup…");
                restored = await Services.BackupService.ApplyPendingRestoreAsync();
            }

            startup.Report("Preparing the database…");

            // Schema first: everything after this reads or writes through it.
            try
            {
                await MauiProgram.InitializeDatabaseAsync(_services);
            }
            catch (Exception ex)
            {
                // As before - a database that will not come up is not a reason
                // to refuse to start, because the JSON file still has the league.
                System.Diagnostics.Debug.WriteLine($"Database init failed: {ex.Message}");
            }

            DataStore.SetServiceProvider(_services);

            startup.Report("Reading your league…");

            // The heavy part, and the part that has to leave the screen alone:
            // a season's fixtures is tens of thousands of frames.
            await Task.Run(DataStore.Load);

            // Photos used to be kept inside the league file, which made saving
            // it run out of memory once there were enough of them. Any still
            // there - or waiting as originals in the gallery's incoming folder -
            // are resized into the gallery store once, and the file shrinks.
            await SettleGalleryAsync(startup);

            // Back on the thread that draws, because both of these touch it.
            _themeService.ApplyTheme();
            _seasonService.Initialize();

            window.Page = new AppShell();

            if (restored is not null)
                await window.Page.DisplayAlert("Restore", restored, "OK");

            // A league with no seasons at all has just been installed: walk
            // through setting it up rather than leaving a row of empty pages.
            if (DataStore.Data.Seasons.Count == 0
                && _services.GetService(typeof(SetupWizardPage)) is SetupWizardPage wizard)
                await window.Page.Navigation.PushModalAsync(wizard);
        }
        catch (Exception ex)
        {
            // Nothing else will say this, and a spinner that never stops says
            // nothing at all.
            startup.Failed($"Could not open your league: {ex.Message}");
        }
    }
}
