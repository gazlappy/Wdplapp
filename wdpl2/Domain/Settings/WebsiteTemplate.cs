using System;

namespace Wdpl2.Models
{
    /// <summary>
    /// Represents a website template with metadata
    /// </summary>
    public sealed class WebsiteTemplate
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string PreviewImagePath { get; set; } = "";
        public bool IsResponsive { get; set; } = true;
        public string[] Features { get; set; } = Array.Empty<string>();
        
        // Built-in templates
        public static WebsiteTemplate Modern => new()
        {
            Id = "modern",
            Name = "Modern",
            Description = "Clean, modern design with gradient headers and card-based layout",
            PreviewImagePath = "template_modern.png",
            IsResponsive = true,
            Features = new[] { "Responsive", "Dark mode toggle", "Animated tables", "Mobile-friendly" }
        };
        
        public static WebsiteTemplate Classic => new()
        {
            Id = "classic",
            Name = "Classic",
            Description = "Traditional table-based layout with simple styling",
            PreviewImagePath = "template_classic.png",
            IsResponsive = true,
            Features = new[] { "Responsive", "Printable", "Fast loading", "High contrast" }
        };
        
        public static WebsiteTemplate Minimal => new()
        {
            Id = "minimal",
            Name = "Minimal",
            Description = "Ultra-lightweight design focused on performance",
            PreviewImagePath = "template_minimal.png",
            IsResponsive = true,
            Features = new[] { "Responsive", "Ultra-fast", "Accessibility focused", "No JavaScript" }
        };
        
        public static WebsiteTemplate DarkMode => new()
        {
            Id = "dark",
            Name = "Dark Mode",
            Description = "Modern dark theme with vibrant accents",
            PreviewImagePath = "template_dark.png",
            IsResponsive = true,
            Features = new[] { "Responsive", "Dark theme", "High contrast", "Eye-friendly" }
        };
        
        public static WebsiteTemplate Sport => new()
        {
            Id = "sport",
            Name = "Sport",
            Description = "Bold, athletic design with team colors",
            PreviewImagePath = "template_sport.png",
            IsResponsive = true,
            Features = new[] { "Responsive", "Team colors", "Bold typography", "Action-focused" }
        };
        
        public static WebsiteTemplate Minimalist => new()
        {
            Id = "minimalist",
            Name = "Minimalist",
            Description = "Clean lines, maximum white space, elegant simplicity",
            PreviewImagePath = "template_minimalist.png",
            IsResponsive = true,
            Features = new[] { "Responsive", "Clean design", "Typography-focused", "Elegant" }
        };
        
        public static WebsiteTemplate[] GetAllTemplates() => new[]
        {
            Modern,
            Classic,
            DarkMode,
            Sport,
            Minimalist,
            Minimal
        };
        
        public static WebsiteTemplate? GetTemplateById(string id)
        {
            return id.ToLowerInvariant() switch
            {
                "modern" => Modern,
                "classic" => Classic,
                "minimal" => Minimal,
                "dark" => DarkMode,
                "sport" => Sport,
                "minimalist" => Minimalist,
                _ => null
            };
        }
    }
}
