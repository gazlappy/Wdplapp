namespace Wdpl2;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("import", typeof(Views.ImportPage));
        Routing.RegisterRoute("inspector", typeof(Views.DatabaseInspectorPage));
        Routing.RegisterRoute("legacy", typeof(Views.LegacyAppPage));
        Routing.RegisterRoute("sourceviewer", typeof(Views.SourceCodeViewerPage));
    }
}
