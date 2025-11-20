using System;

namespace Wdpl2.Helpers;

/// <summary>
/// Central repository for emoji constants used throughout the application.
/// Ensures consistency and proper Unicode representation across all pages.
/// </summary>
public static class Emojis
{
    // ========== SUCCESS & STATUS ==========
    public const string Success = "\u2705";        // ? Check mark
    public const string Error = "\u274C";          // ? Cross mark
    public const string Warning = "\u26A0\uFE0F";  // ?? Warning
    public const string Info = "\u2139\uFE0F";     // ?? Information
    public const string Check = "\u2713";          // ? Check
    public const string Cross = "\u2717";          // ? Cross
    
    // ========== ACTIONS ==========
    public const string Add = "\u2795";            // ? Plus
    public const string Delete = "\U0001F5D1";     // ??? Wastebasket
    public const string Edit = "\u270F\uFE0F";     // ?? Pencil
    public const string Save = "\U0001F4BE";       // ?? Floppy disk
    public const string Reload = "\U0001F504";     // ?? Counterclockwise arrows
    public const string Export = "\U0001F4E4";     // ?? Outbox tray
    public const string Import = "\U0001F4E5";     // ?? Inbox tray
    public const string Copy = "\U0001F4CB";       // ?? Clipboard
    public const string Settings = "\u2699\uFE0F"; // ?? Gear
    
    // ========== SPORTS & GAMES ==========
    public const string EightBall = "8\u20E3";     // 8? Keycap 8
    public const string Trophy = "\U0001F3C6";     // ?? Trophy
    public const string Medal = "\U0001F3C5";      // ?? Sports medal
    public const string Target = "\U0001F3AF";     // ?? Direct hit
    public const string Fire = "\U0001F525";       // ?? Fire
    public const string Star = "\u2B50";           // ? Star
    
    // ========== PEOPLE & TEAMS ==========
    public const string Player = "\U0001F464";     // ?? Bust in silhouette
    public const string Team = "\U0001F465";       // ?? Busts in silhouette
    public const string Crown = "\U0001F451";      // ?? Crown (for captain)
    public const string Person = "\U0001F9D1";     // ?? Person
    
    // ========== LOCATIONS ==========
    public const string Location = "\U0001F4CD";   // ?? Round pushpin
    public const string Building = "\U0001F3E2";   // ?? Office building
    public const string Home = "\U0001F3E0";       // ?? House
    public const string MapPin = "\U0001F4CC";     // ?? Pushpin
    
    // ========== TIME & CALENDAR ==========
    public const string Calendar = "\U0001F4C5";   // ?? Calendar
    public const string Clock = "\U0001F550";      // ?? Clock
    public const string Hourglass = "\u231B";      // ? Hourglass
    public const string Stopwatch = "\u23F1\uFE0F";// ?? Stopwatch
    
    // ========== DATA & STATS ==========
    public const string Chart = "\U0001F4CA";      // ?? Bar chart
    public const string Table = "\U0001F4CB";      // ?? Clipboard  
    public const string Document = "\U0001F4C4";   // ?? Page facing up
    public const string Folder = "\U0001F4C1";     // ?? File folder
    public const string Database = "\U0001F4BE";   // ?? Floppy disk
    
    // ========== NAVIGATION ==========
    public const string Back = "\u2B05\uFE0F";     // ?? Left arrow
    public const string Forward = "\u27A1\uFE0F";  // ?? Right arrow
    public const string Up = "\u2B06\uFE0F";       // ?? Up arrow
    public const string Down = "\u2B07\uFE0F";     // ?? Down arrow
    public const string Menu = "\U0001F4CB";       // ?? Menu
    
    // ========== SEASON & COMPETITION ==========
    public const string Season = "\U0001F343";     // ?? Leaf
    public const string Competition = "\U0001F3C6";// ?? Trophy
    public const string Fixture = "\U0001F4C5";    // ?? Calendar
    public const string Division = "\U0001F4CA";   // ?? Bar chart
    
    // ========== STATUS INDICATORS ==========
    public const string Active = "\U0001F7E2";     // ?? Green circle
    public const string Inactive = "\u26AA";       // ? White circle
    public const string InProgress = "\U0001F7E1"; // ?? Yellow circle
    public const string Complete = "\U0001F7E2";   // ?? Green circle
    
    // ========== FOOD & MISC ==========
    public const string Food = "\U0001F374";       // ?? Fork and knife
    public const string Note = "\U0001F4DD";       // ?? Memo
    public const string Lock = "\U0001F512";       // ?? Lock
    public const string Unlock = "\U0001F513";     // ?? Unlocked
    public const string Lightning = "\u26A1";      // ? Lightning
    public const string Wrench = "\U0001F527";     // ?? Wrench
    public const string Rocket = "\U0001F680";     // ?? Rocket
    public const string Sparkles = "\u2728";       // ? Sparkles
    public const string Bullet = "\u2022";         // • Bullet point
    public const string ThumbsUp = "\U0001F44D";   // ?? Thumbs up
    public const string ThumbsDown = "\U0001F44E"; // ?? Thumbs down
    
    /// <summary>
    /// Font family to use for proper emoji rendering on Windows
    /// </summary>
    public const string FontFamily = "Segoe UI Emoji";
}
