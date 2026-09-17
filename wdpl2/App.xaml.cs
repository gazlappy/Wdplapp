namespace Wdpl2;

using System.Threading.Tasks;
using Wdpl2.Services;

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

        // Ensure database schema is up-to-date before any pages are constructed
        try
        {
            Task.Run(() => MauiProgram.InitializeDatabaseAsync(_services)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database init failed: {ex.Message}");
        }

        // Bridge static DataStore to EF Core so both stores stay in sync
        DataStore.SetServiceProvider(_services);

        // Load data (entities come from EF Core, settings from JSON)
        // Load checks the database against the file and rebuilds it if it has
        // fallen behind - which has to happen there, before the database is
        // copied back over what was read.
        DataStore.Load();

        // Apply saved theme settings
        _themeService.ApplyTheme();

        // Initialize season service
        _seasonService.Initialize();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
