namespace Wdpl2;

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

        // Load data
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
