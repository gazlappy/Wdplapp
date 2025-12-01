using Microsoft.Maui.Devices;

namespace Wdpl2.Services;

/// <summary>
/// Helper service for responsive UI design based on device characteristics
/// </summary>
public static class ResponsiveHelper
{
    /// <summary>
    /// Get device category based on screen width
    /// </summary>
    public static DeviceCategory GetDeviceCategory()
    {
        var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
        var widthInDp = displayInfo.Width / displayInfo.Density;
        
        if (widthInDp < 600)
            return DeviceCategory.Phone;
        else if (widthInDp < 900)
            return DeviceCategory.Tablet;
        else
            return DeviceCategory.Desktop;
    }
    
    /// <summary>
    /// Check if current device is a phone
    /// </summary>
    public static bool IsPhone => GetDeviceCategory() == DeviceCategory.Phone;
    
    /// <summary>
    /// Check if current device is a tablet
    /// </summary>
    public static bool IsTablet => GetDeviceCategory() == DeviceCategory.Tablet;
    
    /// <summary>
    /// Check if current device is desktop
    /// </summary>
    public static bool IsDesktop => GetDeviceCategory() == DeviceCategory.Desktop;
    
    /// <summary>
    /// Get device screen width in density-independent pixels
    /// </summary>
    public static double GetScreenWidthInDp()
    {
        var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
        return displayInfo.Width / displayInfo.Density;
    }
    
    /// <summary>
    /// Get device screen height in density-independent pixels
    /// </summary>
    public static double GetScreenHeightInDp()
    {
        var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
        return displayInfo.Height / displayInfo.Density;
    }
    
    /// <summary>
    /// Get appropriate button style resource key based on device
    /// </summary>
    public static string GetButtonStyleKey(string baseStyleKey = "PrimaryButtonStyle")
    {
        return IsPhone ? $"Mobile{baseStyleKey}" : baseStyleKey;
    }
    
    /// <summary>
    /// Get appropriate spacing based on device
    /// </summary>
    public static double GetSpacing(SpacingSize size = SpacingSize.Standard)
    {
        if (IsPhone)
        {
            return size switch
            {
                SpacingSize.Small => 8,
                SpacingSize.Standard => 12,
                SpacingSize.Medium => 16,
                SpacingSize.Large => 24,
                SpacingSize.ExtraLarge => 32,
                _ => 12
            };
        }
        else
        {
            return size switch
            {
                SpacingSize.Small => 4,
                SpacingSize.Standard => 8,
                SpacingSize.Medium => 12,
                SpacingSize.Large => 16,
                SpacingSize.ExtraLarge => 24,
                _ => 8
            };
        }
    }
    
    /// <summary>
    /// Get appropriate padding based on device
    /// </summary>
    public static Thickness GetPadding(PaddingSize size = PaddingSize.Standard)
    {
        if (IsPhone)
        {
            return size switch
            {
                PaddingSize.Small => new Thickness(8),
                PaddingSize.Standard => new Thickness(12),
                PaddingSize.Medium => new Thickness(16),
                PaddingSize.Large => new Thickness(20),
                PaddingSize.ExtraLarge => new Thickness(24),
                _ => new Thickness(12)
            };
        }
        else
        {
            return size switch
            {
                PaddingSize.Small => new Thickness(4),
                PaddingSize.Standard => new Thickness(8),
                PaddingSize.Medium => new Thickness(12),
                PaddingSize.Large => new Thickness(16),
                PaddingSize.ExtraLarge => new Thickness(24),
                _ => new Thickness(8)
            };
        }
    }
    
    /// <summary>
    /// Get appropriate font size based on device
    /// </summary>
    public static double GetFontSize(FontSizeCategory category)
    {
        var multiplier = IsPhone ? 1.15 : 1.0;
        
        return category switch
        {
            FontSizeCategory.Caption => 12 * multiplier,
            FontSizeCategory.Body => 14 * multiplier,
            FontSizeCategory.Subheadline => 16 * multiplier,
            FontSizeCategory.Headline => 18 * multiplier,
            FontSizeCategory.Title => 24 * multiplier,
            FontSizeCategory.LargeTitle => 28 * multiplier,
            _ => 14 * multiplier
        };
    }
    
    /// <summary>
    /// Get minimum touch target size (44x44 for accessibility)
    /// </summary>
    public static double GetMinimumTouchTarget()
    {
        return IsPhone ? 48 : 44;
    }
    
    /// <summary>
    /// Apply responsive layout to a Grid (convert to single column on phone)
    /// </summary>
    public static void ApplyResponsiveLayout(Grid grid, int desktopColumns = 2)
    {
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        
        if (IsPhone)
        {
            // Single column layout for phones
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            
            // Convert to rows
            var childCount = grid.Children.Count;
            for (int i = 0; i < childCount; i++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                
                if (grid.Children[i] is View child)
                {
                    Grid.SetColumn(child, 0);
                    Grid.SetRow(child, i);
                }
            }
        }
        else
        {
            // Multi-column layout for tablets/desktop
            for (int i = 0; i < desktopColumns; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }
        }
    }
}

public enum DeviceCategory
{
    Phone,
    Tablet,
    Desktop
}

public enum SpacingSize
{
    Small,
    Standard,
    Medium,
    Large,
    ExtraLarge
}

public enum PaddingSize
{
    Small,
    Standard,
    Medium,
    Large,
    ExtraLarge
}

public enum FontSizeCategory
{
    Caption,
    Body,
    Subheadline,
    Headline,
    Title,
    LargeTitle
}
