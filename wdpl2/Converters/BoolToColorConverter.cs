using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Wdpl2.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        if (value is bool isActive)
        {
            if (isActive)
                return Color.FromArgb("#10B981"); // Green — same in both themes
            return isDark ? Color.FromArgb("#4B5563") : Color.FromArgb("#D1D5DB");
        }
        return isDark ? Color.FromArgb("#4B5563") : Color.FromArgb("#D1D5DB");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
