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

    private async Task OpenTheLeagueAsync(Window window, StartupPage startup)
    {
        try
        {
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

            // Back on the thread that draws, because both of these touch it.
            _themeService.ApplyTheme();
            _seasonService.Initialize();

            window.Page = new AppShell();
        }
        catch (Exception ex)
        {
            // Nothing else will say this, and a spinner that never stops says
            // nothing at all.
            startup.Failed($"Could not open your league: {ex.Message}");
        }
    }
}
