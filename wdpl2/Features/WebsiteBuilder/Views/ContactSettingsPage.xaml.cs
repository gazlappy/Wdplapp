using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class ContactSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public ContactSettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;

        WelcomeMessageEditor.Text = settings.WelcomeMessage;
        AboutTextEditor.Text = settings.AboutText;
        ContactEmailEntry.Text = settings.ContactEmail;
        ContactPhoneEntry.Text = settings.ContactPhone;
        ContactAddressEntry.Text = settings.ContactAddress;

        FacebookUrlEntry.Text = settings.FacebookUrl;
        TwitterUrlEntry.Text = settings.TwitterUrl;
        InstagramUrlEntry.Text = settings.InstagramUrl;
        YouTubeUrlEntry.Text = settings.YouTubeUrl;
        TikTokUrlEntry.Text = settings.TikTokUrl;
        WebsiteUrlEntry.Text = settings.WebsiteUrl;

        UpdateFillIndicators();
    }

    private void OnFieldChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateFillIndicators();
    }

    private void UpdateFillIndicators()
    {
        // Contact fields
        var contactEntries = new[] { ContactEmailEntry, ContactPhoneEntry, ContactAddressEntry };
        var contactFilled = contactEntries.Count(e => !string.IsNullOrWhiteSpace(e.Text));
        ContactFillLabel.Text = $"{contactFilled}/3 filled";
        ContactFillLabel.TextColor = contactFilled == 3
            ? Color.FromArgb("#10B981")
            : contactFilled > 0
                ? Color.FromArgb("#F59E0B")
                : Color.FromArgb("#9CA3AF");

        // Social fields
        var socialEntries = new[] { FacebookUrlEntry, TwitterUrlEntry, InstagramUrlEntry, YouTubeUrlEntry, TikTokUrlEntry, WebsiteUrlEntry };
        var socialFilled = socialEntries.Count(e => !string.IsNullOrWhiteSpace(e.Text));
        SocialFillLabel.Text = $"{socialFilled}/6 linked";
        SocialFillLabel.TextColor = socialFilled > 0
            ? Color.FromArgb("#10B981")
            : Color.FromArgb("#9CA3AF");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            settings.WelcomeMessage = WelcomeMessageEditor.Text?.Trim() ?? "";
            settings.AboutText = AboutTextEditor.Text?.Trim() ?? "";
            settings.ContactEmail = ContactEmailEntry.Text?.Trim() ?? "";
            settings.ContactPhone = ContactPhoneEntry.Text?.Trim() ?? "";
            settings.ContactAddress = ContactAddressEntry.Text?.Trim() ?? "";
            
            settings.FacebookUrl = FacebookUrlEntry.Text?.Trim() ?? "";
            settings.TwitterUrl = TwitterUrlEntry.Text?.Trim() ?? "";
            settings.InstagramUrl = InstagramUrlEntry.Text?.Trim() ?? "";
            settings.YouTubeUrl = YouTubeUrlEntry.Text?.Trim() ?? "";
            settings.TikTokUrl = TikTokUrlEntry.Text?.Trim() ?? "";
            settings.WebsiteUrl = WebsiteUrlEntry.Text?.Trim() ?? "";
            
            DataStore.Save();
            
            await DisplayAlert("Saved", "Contact settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
