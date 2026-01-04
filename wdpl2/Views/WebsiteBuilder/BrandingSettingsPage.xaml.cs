using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class BrandingSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;
    private byte[]? _uploadedLogoData;

    public BrandingSettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        
        LeagueNameEntry.Text = settings.LeagueName;
        SubtitleEntry.Text = settings.LeagueSubtitle;
        
        var positions = (LogoPositionPicker.ItemsSource as IList<string>)!;
        var posIndex = positions.IndexOf(settings.LogoPosition);
        if (posIndex >= 0) LogoPositionPicker.SelectedIndex = posIndex;
        
        if (settings.LogoImageData != null && settings.UseCustomLogo)
        {
            _uploadedLogoData = settings.LogoImageData;
            LogoStatusLabel.Text = "Logo loaded";
            LogoStatusLabel.TextColor = Color.FromArgb("#10B981");
            LogoPreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(_uploadedLogoData));
            LogoPreviewImage.IsVisible = true;
            RemoveLogoBtn.IsVisible = true;
        }
    }

    private async void OnUploadLogoClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select League Logo",
                FileTypes = FilePickerFileType.Images
            });

            if (result != null)
            {
                using var stream = await result.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                _uploadedLogoData = memoryStream.ToArray();

                LogoStatusLabel.Text = result.FileName;
                LogoStatusLabel.TextColor = Color.FromArgb("#10B981");
                
                LogoPreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(_uploadedLogoData));
                LogoPreviewImage.IsVisible = true;
                RemoveLogoBtn.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to upload logo: {ex.Message}", "OK");
        }
    }

    private void OnRemoveLogoClicked(object sender, EventArgs e)
    {
        _uploadedLogoData = null;
        LogoStatusLabel.Text = "No logo selected";
        LogoStatusLabel.TextColor = Color.FromArgb("#6B7280");
        LogoPreviewImage.IsVisible = false;
        RemoveLogoBtn.IsVisible = false;
        
        League.WebsiteSettings.LogoImageData = null;
        League.WebsiteSettings.UseCustomLogo = false;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            settings.LeagueName = LeagueNameEntry.Text?.Trim() ?? "My Pool League";
            settings.LeagueSubtitle = SubtitleEntry.Text?.Trim() ?? "";
            settings.LogoPosition = LogoPositionPicker.SelectedItem?.ToString() ?? "above";
            
            if (_uploadedLogoData != null)
            {
                settings.LogoImageData = _uploadedLogoData;
                settings.UseCustomLogo = true;
            }
            
            DataStore.Save();
            
            await DisplayAlert("Saved", "Branding settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
