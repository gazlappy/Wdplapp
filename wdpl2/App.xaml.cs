namespace Wdpl2;

using Wdpl2.Services;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Load data
        DataStore.Load();
        
        // Apply saved theme settings
        ThemeService.ApplyTheme();
        
        // Initialize season service
        SeasonService.Initialize();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}
