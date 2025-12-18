using Microsoft.Maui.Controls;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class LegacyAppPage : ContentPage
{
    public LegacyAppPage()
    {
        InitializeComponent();
        LoadLegacyInfo();
    }

    private void LoadLegacyInfo()
    {
        var info = LegacyEasterEggService.GetLegacyAppInfo();
        
        AppNameLabel.Text = info.Name;
        VersionLabel.Text = $"Version {info.Version}";
        YearLabel.Text = info.Year;
        TechLabel.Text = $"Built with {info.Technology}";
        DescriptionLabel.Text = info.Description;
        
        // Populate features
        FeaturesStack.Children.Clear();
        foreach (var feature in info.Features)
        {
            FeaturesStack.Children.Add(new Label
            {
                Text = $"• {feature}",
                FontSize = 13,
                TextColor = Color.FromArgb("#a0ffa0")
            });
        }
        
        // Populate source files
        SourceFilesStack.Children.Clear();
        foreach (var file in info.SourceFiles)
        {
            SourceFilesStack.Children.Add(new Label
            {
                Text = file,
                FontSize = 12,
                TextColor = Color.FromArgb("#80c0ff"),
                FontFamily = "Consolas"
            });
        }

        // Populate why WDPL2 was created
        WhyCreatedLabel.Text = string.Join("\n", new[]
        {
            "• BDE discontinued - no modern Windows support",
            "• Paradox databases are obsolete technology",
            "• No mobile/cross-platform support possible",
            "• Security vulnerabilities in old code",
            "• Cannot extend with modern features",
            "• Source code changes would require full rewrite",
            "",
            "WDPL2 brings the same functionality to .NET 9 MAUI",
            "with modern JSON storage and cross-platform support!"
        });
        
        // Update launch button based on platform
        if (!LegacyEasterEggService.CanLaunchLegacyApp)
        {
            LaunchButton.Text = "Windows Only";
            LaunchButton.BackgroundColor = Color.FromArgb("#444");
            LaunchButton.IsEnabled = false;
            LaunchNote.Text = "The original application only runs on Windows.";
        }
        else
        {
            var exePath = LegacyEasterEggService.GetLegacyExePath();
            if (string.IsNullOrEmpty(exePath))
            {
                LaunchButton.Text = "plm.exe Not Found";
                LaunchButton.BackgroundColor = Color.FromArgb("#664400");
                LaunchNote.Text = "The original plm.exe was not found in the expected location.";
            }
            else
            {
                // Show warning about BDE requirement
                LaunchNote.Text = "Requires Borland Database Engine (BDE) - will fail on Windows 10/11";
            }
        }
    }

    private async void OnLaunchClicked(object? sender, EventArgs e)
    {
        // Show detailed explanation
        var choice = await DisplayActionSheet(
            "BDE Compatibility Issue",
            "Cancel",
            null,
            "Try Launch Anyway (will likely fail)",
            "Show Workaround Options",
            "Why Can't We Fix the Source Code?");

        switch (choice)
        {
            case "Try Launch Anyway (will likely fail)":
                await TryLaunchLegacyApp();
                break;
            
            case "Show Workaround Options":
                await ShowWorkaroundOptions();
                break;
            
            case "Why Can't We Fix the Source Code?":
                await ShowWhyCantFix();
                break;
        }
    }

    private async Task TryLaunchLegacyApp()
    {
        var (success, message) = LegacyEasterEggService.LaunchLegacyApp();
        
        if (success)
        {
            await DisplayAlert("Time Machine Activated!", message, "OK");
            
            // Show follow-up about potential BDE error
            await Task.Delay(2000);
            await DisplayAlert(
                "Did it work?",
                "If you saw 'Exception EDBEngineError' or 'Cannot initialize BDE (error $2108)', " +
                "that's the expected result on Windows 10/11.\n\n" +
                "The Borland Database Engine hasn't been updated since 2000 and doesn't work reliably on modern Windows.",
                "OK");
        }
        else
        {
            await DisplayAlert("Cannot Launch", message, "OK");
        }
    }

    private async Task ShowWorkaroundOptions()
    {
        await DisplayAlert(
            "Workaround Options",
            "Option 1: Install BDE Manually (Complex)\n" +
            "???????????????????????????????\n" +
            "1. Download BDE 5.2 installer from archive sites\n" +
            "2. Right-click installer ? Properties ? Compatibility\n" +
            "3. Set 'Run as Windows XP SP3'\n" +
            "4. Run as Administrator\n" +
            "5. Configure PDOXUSRS.NET path in BDE Administrator\n" +
            "6. May still fail due to UAC restrictions\n\n" +
            
            "Option 2: Virtual Machine (Recommended)\n" +
            "???????????????????????????????\n" +
            "1. Install VirtualBox or VMware (free)\n" +
            "2. Create Windows XP or Windows 7 VM\n" +
            "3. Install BDE in the VM (works perfectly)\n" +
            "4. Copy plm.exe and database files to VM\n" +
            "5. Run the original app in its native environment\n\n" +
            
            "Option 3: Just Use WDPL2! (Best)\n" +
            "???????????????????????????????\n" +
            "WDPL2 does everything the original did, plus:\n" +
            "• Works on Windows, Mac, iOS, Android\n" +
            "• No database engine required\n" +
            "• Modern, maintained codebase\n" +
            "• Career tracking across seasons\n" +
            "• Competition/tournament management",
            "OK");
    }

    private async Task ShowWhyCantFix()
    {
        await DisplayAlert(
            "Why Can't We Fix the Source Code?",
            "TECHNICAL ANALYSIS\n" +
            "???????????????????????????????\n\n" +
            
            "The Problem:\n" +
            "The entire application is built on TTable, TQuery, and TDatabase " +
            "components from Borland's DBTables unit. These components ONLY work " +
            "with the Borland Database Engine (BDE).\n\n" +
            
            "To remove BDE dependency, we would need to:\n\n" +
            
            "1. Purchase modern Delphi ($1,500-$5,000)\n" +
            "   The original Delphi 5/6 won't run on Windows 11\n\n" +
            
            "2. Replace ALL data access components\n" +
            "   • 50+ TTable components ? FireDAC/other\n" +
            "   • 30+ TQuery components ? new SQL syntax\n" +
            "   • Rewrite all .DFM form files\n\n" +
            
            "3. Rewrite database connection logic\n" +
            "   datamodule.pas has 800+ lines of BDE code\n\n" +
            
            "4. Update all SQL queries\n" +
            "   Paradox SQL dialect ? standard SQL\n" +
            "   (various string and date formatting changes)\n\n" +
            
            "5. Test everything\n" +
            "   Ratings, fixtures, reports, etc.\n\n" +
            
            "This is essentially a FULL REWRITE - which is exactly " +
            "what WDPL2 already is! We chose .NET MAUI instead of " +
            "modern Delphi because it's free, cross-platform, and " +
            "has a larger developer community.\n\n" +
            
            "In summary: it's not feasible to fix or update the " +
            "original source code. But you can enjoy the benefits " +
            "of modern .NET development with WDPL2!",
            "I Understand");
    }

    private async void OnViewSourceClicked(object? sender, EventArgs e)
    {
        // Show a sample of the original Delphi source code
        var sourceViewer = new SourceCodeViewerPage();
        await Navigation.PushAsync(sourceViewer);
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
