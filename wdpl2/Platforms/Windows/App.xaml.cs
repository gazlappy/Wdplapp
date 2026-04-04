namespace Wdpl2.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        // Prevent unhandled WinUI exceptions from crashing the app
        UnhandledException += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"Unhandled WinUI exception: {e.Exception}");
            e.Handled = true;
        };
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => Wdpl2.MauiProgram.CreateMauiApp();
}
