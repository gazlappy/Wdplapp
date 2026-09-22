namespace Wdpl2;

public partial class AppShell : Shell
{
    /// <summary>
    /// The sidebar, in order: each section's heading (none for the ungrouped
    /// ends) and its pages as (title shown, Shell route).
    /// </summary>
    /// <remarks>
    /// The one list the sidebar is drawn from. Every route here is a
    /// FlyoutItem in AppShell.xaml, and every FlyoutItem is here -
    /// AppWiringTests checks both ways, so a page cannot be added to the shell
    /// and left unreachable.
    /// </remarks>
    internal static readonly (string? Heading, (string Title, string Route)[] Pages)[] Sections =
    [
        (null,      [("Dashboard", "Dashboard")]),
        ("League",  [("Seasons", "Seasons"), ("Divisions", "Divisions"), ("Teams", "Teams"),
                     ("Players", "Players"), ("Venues", "Venues")]),
        ("Matches", [("Fixtures", "Fixtures"), ("Calendar", "Calendar"), ("Tables", "Tables"),
                     ("Competitions", "Competitions")]),
        ("Stats",   [("Analytics", "Analytics")]),
        ("Online",  [("Website", "Website"), ("Web Control", "WebControl")]),
        (null,      [("Settings", "Settings")]),
    ];

    private static readonly Color SelectedLight = Color.FromArgb("#DCEFE6");
    private static readonly Color SelectedDark = Color.FromArgb("#1C3B2F");

    private readonly Dictionary<string, (Border Row, Label Text)> _entries = new(StringComparer.OrdinalIgnoreCase);

    public AppShell()
    {
        InitializeComponent();

        // Register routes for programmatic navigation
        Routing.RegisterRoute("careerstats", typeof(Views.CareerStatsPage));
        Routing.RegisterRoute("framestats", typeof(Views.FrameStatsPage));
        Routing.RegisterRoute("achievements", typeof(Views.AchievementsPage));
        Routing.RegisterRoute("seasonawards", typeof(Views.SeasonAwardsPage));
        Routing.RegisterRoute("matchday", typeof(Views.MatchDayDashboardPage));
        Routing.RegisterRoute("teamanalytics", typeof(Views.TeamAnalyticsPage));
        Routing.RegisterRoute("whatif", typeof(Views.WhatIfSimulatorPage));
        Routing.RegisterRoute("playerprofile", typeof(Views.PlayerProfilePage));
        Routing.RegisterRoute("playerresults", typeof(Views.PlayerResultsPage));
        Routing.RegisterRoute("seasonsetup", typeof(Views.SeasonSetupPage));
        Routing.RegisterRoute("search", typeof(Views.SearchPage));

        BuildSidebar();
        Navigated += (_, _) => Highlight(CurrentRoute());
        Highlight("Dashboard");
    }

    private void BuildSidebar()
    {
        var name = new Label
        {
            Text = Product.Name,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(10, 0, 0, 10),
        };
        name.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#16634B"), Color.FromArgb("#4ADE80"));
        Sidebar.Children.Add(name);

        foreach (var (heading, pages) in Sections)
        {
            if (heading is null)
            {
                // An ungrouped section still needs a little air above it.
                Sidebar.Children.Add(new BoxView { HeightRequest = 8, Color = Colors.Transparent });
            }
            else
            {
                var label = new Label
                {
                    Text = heading.ToUpperInvariant(),
                    FontSize = 11,
                    FontAttributes = FontAttributes.Bold,
                    CharacterSpacing = 1.2,
                    Margin = new Thickness(10, 14, 0, 4),
                };
                label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#6B7280"), Color.FromArgb("#8B9A94"));
                Sidebar.Children.Add(label);
            }

            foreach (var (title, route) in pages)
                Sidebar.Children.Add(Entry(title, route));
        }
    }

    /// <summary>
    /// One page in the sidebar.
    /// </summary>
    /// <remarks>
    /// A MAUI button centres its text and has no way to left-align it, and a
    /// tappable label is invisible to screen readers and UI Automation. So
    /// each entry is a button that does the work - named after the page,
    /// with AutomationId "nav-Route" for the smoke test - under a label that
    /// only draws the text.
    /// </remarks>
    private View Entry(string title, string route)
    {
        var button = new Button
        {
            Text = title,
            AutomationId = "nav-" + route,
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.Transparent,
            BorderWidth = 0,
            CornerRadius = 8,
            HeightRequest = 38,
            Padding = 0,
        };
        button.Clicked += async (_, _) => await GoTo(route);

        var text = new Label
        {
            Text = title,
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(12, 0),
            InputTransparent = true,
        };
        AutomationProperties.SetIsInAccessibleTree(text, false);
        text.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#1F2937"), Color.FromArgb("#D1D5DB"));

        var row = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.Transparent,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Content = new Grid { Children = { button, text } },
        };

        _entries[route] = (row, text);
        return row;
    }

    private async Task GoTo(string route)
    {
        await GoToAsync("//" + route);

        // On a phone the sidebar slides over the page; put it away once a page
        // is chosen. On a PC it is locked open and this does nothing.
        if (FlyoutBehavior == FlyoutBehavior.Flyout)
            FlyoutIsPresented = false;
    }

    /// <summary>The page's own route - the first part of "//Teams/playerprofile".</summary>
    private string CurrentRoute()
    {
        var location = CurrentState?.Location?.OriginalString ?? "";
        return location.TrimStart('/').Split('/', 2)[0];
    }

    private void Highlight(string route)
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        foreach (var (key, (row, text)) in _entries)
        {
            var on = string.Equals(key, route, StringComparison.OrdinalIgnoreCase);
            row.BackgroundColor = on ? (dark ? SelectedDark : SelectedLight) : Colors.Transparent;
            text.FontAttributes = on ? FontAttributes.Bold : FontAttributes.None;
        }
    }
}
