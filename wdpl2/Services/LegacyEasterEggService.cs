using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Wdpl2.Services;

/// <summary>
/// Easter egg service that can launch the original Delphi Pool League Manager
/// or show information about it on non-Windows platforms.
/// 
/// Trigger: Triple-tap the app title, or Ctrl+Shift+L on Windows
/// </summary>
public static class LegacyEasterEggService
{
    private const string LegacyExeName = "plm.exe";
    private const string LegacyFolderPath = @"Pool League 1.1\Pool League 1.1\plm";
    
    // Track taps for gesture detection
    private static DateTime _lastTapTime = DateTime.MinValue;
    private static int _tapCount = 0;
    private const int TapTimeWindowMs = 800; // 800ms window for triple-tap
    
    /// <summary>
    /// Event fired when easter egg is triggered
    /// </summary>
    public static event EventHandler? EasterEggTriggered;
    
    /// <summary>
    /// Check if we're on Windows (where we can launch the legacy app)
    /// </summary>
    public static bool CanLaunchLegacyApp => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    
    /// <summary>
    /// Get the path to the legacy executable
    /// </summary>
    public static string? GetLegacyExePath()
    {
        // Try multiple locations
        var possiblePaths = new[]
        {
            // Relative to app directory
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LegacyFolderPath, LegacyExeName),
            // Development path
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", LegacyFolderPath, LegacyExeName),
            // Direct in app folder
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LegacyExeName),
            // Common install locations
            @"C:\Program Files\Pool League Manager\plm.exe",
            @"C:\Program Files (x86)\Pool League Manager\plm.exe",
        };
        
        foreach (var path in possiblePaths)
        {
            try
            {
                var normalizedPath = Path.GetFullPath(path);
                if (File.Exists(normalizedPath))
                    return normalizedPath;
            }
            catch
            {
                // Ignore path errors
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Register a tap for gesture detection.
    /// Returns true if easter egg should be triggered (triple-tap detected).
    /// </summary>
    public static bool RegisterTap()
    {
        var now = DateTime.Now;
        var timeSinceLastTap = (now - _lastTapTime).TotalMilliseconds;
        
        if (timeSinceLastTap > TapTimeWindowMs)
        {
            // Reset tap count if too much time has passed
            _tapCount = 1;
        }
        else
        {
            _tapCount++;
        }
        
        _lastTapTime = now;
        
        if (_tapCount >= 3)
        {
            _tapCount = 0;
            EasterEggTriggered?.Invoke(null, EventArgs.Empty);
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Launch the legacy Delphi application (Windows only)
    /// </summary>
    public static (bool success, string message) LaunchLegacyApp()
    {
        if (!CanLaunchLegacyApp)
        {
            return (false, "The original Pool League Manager can only run on Windows.");
        }
        
        var exePath = GetLegacyExePath();
        if (string.IsNullOrEmpty(exePath))
        {
            return (false, "Could not find the original plm.exe file.\n\n" +
                "The legacy application may not be included in this installation.");
        }
        
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = true
            };
            
            Process.Start(startInfo);
            return (true, "Launching Admin4Pool (Pool League Manager 1.1)...\n\n" +
                "?? The original Delphi application from circa 2001!");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to launch legacy application:\n{ex.Message}");
        }
    }
    
    /// <summary>
    /// Get information about the original application
    /// </summary>
    public static LegacyAppInfo GetLegacyAppInfo()
    {
        return new LegacyAppInfo
        {
            Name = "Admin4Pool (Pool League Manager)",
            Version = "1.1",
            Developer = "Original Developer",
            Year = "~2001",
            Technology = "Borland Delphi 5/6",
            Database = "Paradox 7.x (.DB files) via BDE",
            Description = "The original pool league management software that WDPL2 is modernizing. " +
                "Written in Delphi for Windows, using the Borland Database Engine (BDE) to access Paradox databases. " +
                "Features included player ratings, league tables, fixture management, and report generation.\n\n" +
                "?? Note: Requires the Borland Database Engine (BDE) which is not compatible with Windows 10/11 " +
                "without special configuration. This is one of the reasons WDPL2 was created!",
            Features =
            [
                "Player Management & Ratings",
                "Team Management",
                "Division/League Tables",
                "Fixture Scheduling",
                "Match Results Entry",
                "QuickReport Generation",
                "Web Page Generation",
                "Mailshot System",
                "Venue & Captain Labels",
                "8-Ball Statistics"
            ],
            SourceFiles =
            [
                "main.pas - Main application form",
                "datamodule.pas - BDE database connections",
                "player.pas - Player management",
                "team.pas - Team management",
                "division.pas - Division handling",
                "venue.pas - Venue management",
                "update.pas - Match results entry",
                "prating.pas - Player ratings report",
                "ptable.pas - League table report",
                "webpages.pas - HTML generation"
            ],
            Requirements =
            [
                "Windows 95/98/ME/2000/XP/7",
                "Borland Database Engine (BDE) 5.x",
                "Paradox database files (.DB)",
                "~10MB disk space",
                "640x480 minimum resolution"
            ],
            WhyItFails =
            [
                "BDE not included in Windows 10/11",
                "BDE installer has compatibility issues",
                "Registry access restrictions in modern Windows",
                "16-bit components deprecated",
                "Paradox ODBC drivers discontinued"
            ]
        };
    }
}

/// <summary>
/// Information about the legacy application
/// </summary>
public class LegacyAppInfo
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Developer { get; set; } = "";
    public string Year { get; set; } = "";
    public string Technology { get; set; } = "";
    public string Database { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] Features { get; set; } = [];
    public string[] SourceFiles { get; set; } = [];
    public string[] Requirements { get; set; } = [];
    public string[] WhyItFails { get; set; } = [];
}
