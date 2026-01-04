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
