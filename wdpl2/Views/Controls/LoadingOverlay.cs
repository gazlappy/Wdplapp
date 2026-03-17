using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Wdpl2.Views.Controls;

/// <summary>
/// A reusable loading overlay that can be added to any page.
/// Shows a centered ActivityIndicator with an optional message.
/// Usage: Add to page layout, then call Show()/Hide() or bind IsVisible.
/// </summary>
public class LoadingOverlay : ContentView
{
    private readonly ActivityIndicator _indicator;
    private readonly Label _messageLabel;

    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(LoadingOverlay), "Loading...",
            propertyChanged: (b, _, n) => ((LoadingOverlay)b)._messageLabel.Text = (string)n);

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public LoadingOverlay()
    {
        IsVisible = false;
        BackgroundColor = Color.FromArgb("#80000000");
        ZIndex = 999;

        _indicator = new ActivityIndicator
        {
            IsRunning = true,
            Color = Colors.White,
            HeightRequest = 48,
            WidthRequest = 48
        };

        _messageLabel = new Label
        {
            Text = "Loading...",
            TextColor = Colors.White,
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0)
        };

        Content = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children = { _indicator, _messageLabel }
        };
    }

    public void Show(string? message = null)
    {
        if (message != null)
            _messageLabel.Text = message;
        _indicator.IsRunning = true;
        IsVisible = true;
    }

    public void Hide()
    {
        IsVisible = false;
        _indicator.IsRunning = false;
    }
}
