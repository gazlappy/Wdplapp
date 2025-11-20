namespace Wdpl2.WinUI;

public partial class App : MauiWinUIApplication
{
    public App() => InitializeComponent();

    protected override MauiApp CreateMauiApp() => Wdpl2.MauiProgram.CreateMauiApp();
}
