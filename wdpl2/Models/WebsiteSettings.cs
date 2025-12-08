using System;

namespace Wdpl2.Models
{
    /// <summary>
    /// Website configuration and FTP upload settings
    /// </summary>
    public sealed class WebsiteSettings
    {
        // Site Branding
        public string LeagueName { get; set; } = "My Pool League";
        public string LeagueSubtitle { get; set; } = "Weekly 8-Ball Pool Competition";
        public string? LogoPath { get; set; }
        public byte[]? LogoImageData { get; set; }  // NEW: Store logo as byte array
        
        // Theme Colors
        public string PrimaryColor { get; set; } = "#3B82F6"; // Blue
        public string SecondaryColor { get; set; } = "#10B981"; // Green
        public string AccentColor { get; set; } = "#F59E0B"; // Amber
        
        // Content Options
        public bool ShowStandings { get; set; } = true;
        public bool ShowFixtures { get; set; } = true;
        public bool ShowResults { get; set; } = true;
        public bool ShowPlayerStats { get; set; } = true;
        public bool ShowDivisions { get; set; } = true;
        
        // FTP Upload Settings
        public string FtpHost { get; set; } = "";
        public int FtpPort { get; set; } = 21;
        public string FtpUsername { get; set; } = "";
        public string FtpPassword { get; set; } = ""; // Should be encrypted in production
        public bool UseSftp { get; set; } = false;
        public string RemotePath { get; set; } = "/public_html/";
        
        // Generation Settings
        public Guid? SelectedSeasonId { get; set; }
        public string SelectedTemplate { get; set; } = "modern";
        public DateTime LastGenerated { get; set; }
        public DateTime LastUploaded { get; set; }
        
        // Image Settings (NEW)
        public bool UseCustomLogo { get; set; } = false;
        public int LogoMaxWidth { get; set; } = 300;
        public int LogoMaxHeight { get; set; } = 150;
        public int ImageQuality { get; set; } = 85; // 1-100
        
        /// <summary>
        /// Reset to default values
        /// </summary>
        public void ResetToDefaults()
        {
            LeagueName = "My Pool League";
            LeagueSubtitle = "Weekly 8-Ball Pool Competition";
            LogoPath = null;
            LogoImageData = null;
            
            PrimaryColor = "#3B82F6";
            SecondaryColor = "#10B981";
            AccentColor = "#F59E0B";
            
            ShowStandings = true;
            ShowFixtures = true;
            ShowResults = true;
            ShowPlayerStats = true;
            ShowDivisions = true;
            
            SelectedTemplate = "modern";
            
            // Reset image settings
            UseCustomLogo = false;
            LogoMaxWidth = 300;
            LogoMaxHeight = 150;
            ImageQuality = 85;
        }
    }
}
