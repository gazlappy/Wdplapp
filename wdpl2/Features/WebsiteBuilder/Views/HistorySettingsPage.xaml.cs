using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class HistorySettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public HistorySettingsPage()
    {
        InitializeComponent();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var settings = League.WebsiteSettings;

        if (string.IsNullOrWhiteSpace(settings.HistoryHtmlContent))
        {
            PreviewBorder.IsVisible = false;
            StatusLabel.Text = "No HTML page selected";
            return;
        }

        PreviewBorder.IsVisible = true;
        FileNameLabel.Text = string.IsNullOrWhiteSpace(settings.HistoryHtmlFileName)
            ? "history.html"
            : settings.HistoryHtmlFileName;
        FileInfoLabel.Text = $"{settings.HistoryHtmlContent.Length:N0} characters - published as history.html";
        StatusLabel.Text = "HTML page loaded";
    }

    private async void OnPickHtmlClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select HTML File",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, [".html", ".htm"] },
                    { DevicePlatform.macOS, ["html", "htm"] },
                    { DevicePlatform.iOS, ["public.html"] },
                    { DevicePlatform.Android, ["text/html"] }
                })
            });

            if (result == null) return;

            var content = await File.ReadAllTextAsync(result.FullPath);
            if (string.IsNullOrWhiteSpace(content))
            {
                await DisplayAlert("Empty File", "The selected file has no content.", "OK");
                return;
            }

            League.WebsiteSettings.HistoryHtmlContent = content;
            League.WebsiteSettings.HistoryHtmlFileName = result.FileName;
            League.WebsiteSettings.ShowHistory = true;
            DataStore.Save();
            RefreshStatus();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load HTML file: {ex.Message}", "OK");
        }
    }

    private async void OnClearClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(League.WebsiteSettings.HistoryHtmlContent)) return;

        bool confirm = await DisplayAlert("Clear History Page",
            "Remove the custom Roll of Honour HTML page?", "Clear", "Cancel");
        if (!confirm) return;

        League.WebsiteSettings.HistoryHtmlContent = "";
        League.WebsiteSettings.HistoryHtmlFileName = "";
        DataStore.Save();
        RefreshStatus();
    }
}
