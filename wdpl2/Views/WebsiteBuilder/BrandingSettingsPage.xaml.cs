using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class BrandingSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;
    private byte[]? _uploadedLogoData;
    private readonly ObservableCollection<BrandingLogoCatalogItem> _logoCatalog = new();
    private bool _usingCatalogLogo;
    private string? _currentCatalogLogoId;

    public BrandingSettingsPage()
    {
        InitializeComponent();
        LogoCatalogCollection.ItemsSource = _logoCatalog;
        LoadSettings();
        LoadLogoCatalog();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        
        LeagueNameEntry.Text = settings.LeagueName;
        SubtitleEntry.Text = settings.LeagueSubtitle;
        
        // Load logo position
        var posIndex = GetPositionIndex(settings.LogoPosition);
        if (posIndex >= 0) LogoPositionPicker.SelectedIndex = posIndex;
        
        // Load logo size
        LogoWidthEntry.Text = settings.LogoMaxWidth.ToString();
        LogoHeightEntry.Text = settings.LogoMaxHeight.ToString();
        MaintainAspectRatioCheck.IsChecked = settings.LogoMaintainAspectRatio;
        
        // Load logo data
        var effectiveLogo = settings.GetEffectiveLogoData();
        if (effectiveLogo != null && effectiveLogo.Length > 0)
        {
            _uploadedLogoData = effectiveLogo;
            _usingCatalogLogo = !string.IsNullOrEmpty(settings.SelectedCatalogLogoId);
            _currentCatalogLogoId = settings.SelectedCatalogLogoId;
            UpdateLogoPreview();
        }
    }

    private void LoadLogoCatalog()
    {
        _logoCatalog.Clear();
        foreach (var item in League.WebsiteSettings.LogoCatalog)
        {
            _logoCatalog.Add(new BrandingLogoCatalogItem
            {
                Id = item.Id,
                Name = item.Name,
                Category = item.Category,
                ImageData = item.ImageData
            });
        }
    }

    private int GetPositionIndex(string position)
    {
        return position switch
        {
            "above" => 0,
            "below" => 1,
            "left" => 2,
            "right" => 3,
            "top-left" => 4,
            "top-right" => 5,
            "bottom-left" => 6,
            "bottom-right" => 7,
            "hidden" => 8,
            _ => 0
        };
    }

    private string GetPositionValue(int index)
    {
        return index switch
        {
            0 => "above",
            1 => "below",
            2 => "left",
            3 => "right",
            4 => "top-left",
            5 => "top-right",
            6 => "bottom-left",
            7 => "bottom-right",
            8 => "hidden",
            _ => "above"
        };
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
                _usingCatalogLogo = false;
                _currentCatalogLogoId = null;

                UpdateLogoPreview();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to upload logo: {ex.Message}", "OK");
        }
    }

    private async void OnSelectFromCatalogClicked(object sender, EventArgs e)
    {
        if (_logoCatalog.Count == 0)
        {
            await DisplayAlert("No Logos", "No logos saved in catalog. Upload a logo first, then save it to the catalog.", "OK");
            return;
        }
        
        var logoNames = _logoCatalog.Select(l => l.Name).ToArray();
        var selected = await DisplayActionSheet("Select Logo from Catalog", "Cancel", null, logoNames);
        
        if (!string.IsNullOrEmpty(selected) && selected != "Cancel")
        {
            var logo = _logoCatalog.FirstOrDefault(l => l.Name == selected);
            if (logo != null)
            {
                _uploadedLogoData = logo.ImageData;
                _usingCatalogLogo = true;
                _currentCatalogLogoId = logo.Id;
                UpdateLogoPreview();
            }
        }
    }

    private void OnRemoveLogoClicked(object sender, EventArgs e)
    {
        _uploadedLogoData = null;
        _usingCatalogLogo = false;
        _currentCatalogLogoId = null;
        
        LogoPreviewFrame.IsVisible = false;
        SaveToCatalogBtn.IsEnabled = false;
        
        League.WebsiteSettings.LogoImageData = null;
        League.WebsiteSettings.UseCustomLogo = false;
        League.WebsiteSettings.SelectedCatalogLogoId = null;
    }

    private async void OnSaveToCatalogClicked(object sender, EventArgs e)
    {
        if (_uploadedLogoData == null || _uploadedLogoData.Length == 0)
        {
            await DisplayAlert("No Logo", "Please upload a logo first.", "OK");
            return;
        }

        var name = await DisplayPromptAsync("Save to Catalog", "Enter a name for this logo:", placeholder: "League Logo");
        if (string.IsNullOrWhiteSpace(name)) return;

        var category = await DisplayPromptAsync("Save to Catalog", "Enter a category (optional):", placeholder: "General");
        if (string.IsNullOrEmpty(category)) category = "General";

        // Add to WebsiteSettings catalog
        League.WebsiteSettings.AddLogoCatalogItem(name, _uploadedLogoData, "", category);
        
        // Refresh the display catalog
        LoadLogoCatalog();
        
        DataStore.Save();
        await DisplayAlert("Saved", $"Logo '{name}' saved to catalog.", "OK");
    }

    private void OnUseCatalogLogoClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is BrandingLogoCatalogItem item)
        {
            _uploadedLogoData = item.ImageData;
            _usingCatalogLogo = true;
            _currentCatalogLogoId = item.Id;
            UpdateLogoPreview();
        }
    }

    private async void OnDeleteCatalogLogoClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is BrandingLogoCatalogItem item)
        {
            var confirm = await DisplayAlert("Delete Logo", $"Delete '{item.Name}' from catalog?", "Delete", "Cancel");
            if (confirm)
            {
                League.WebsiteSettings.RemoveLogoCatalogItem(item.Id);
                _logoCatalog.Remove(item);
                
                // If this was the current logo, clear it
                if (_currentCatalogLogoId == item.Id)
                {
                    _uploadedLogoData = null;
                    _usingCatalogLogo = false;
                    _currentCatalogLogoId = null;
                    LogoPreviewFrame.IsVisible = false;
                    SaveToCatalogBtn.IsEnabled = false;
                }
                
                DataStore.Save();
            }
        }
    }

    private void UpdateLogoPreview()
    {
        if (_uploadedLogoData != null && _uploadedLogoData.Length > 0)
        {
            LogoPreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(_uploadedLogoData));
            LogoPreviewFrame.IsVisible = true;
            SaveToCatalogBtn.IsEnabled = !_usingCatalogLogo; // Only enable save if it's a new upload
            
            if (_usingCatalogLogo)
            {
                var catalogItem = _logoCatalog.FirstOrDefault(l => l.Id == _currentCatalogLogoId);
                LogoStatusLabel.Text = $"From catalog: {catalogItem?.Name ?? "Unknown"}";
            }
            else
            {
                LogoStatusLabel.Text = "Custom uploaded logo";
            }
            LogoStatusLabel.TextColor = Color.FromArgb("#10B981");
        }
        else
        {
            LogoPreviewFrame.IsVisible = false;
            SaveToCatalogBtn.IsEnabled = false;
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            settings.LeagueName = LeagueNameEntry.Text?.Trim() ?? "My Pool League";
            settings.LeagueSubtitle = SubtitleEntry.Text?.Trim() ?? "";
            settings.LogoPosition = GetPositionValue(LogoPositionPicker.SelectedIndex);
            
            // Parse logo size
            if (int.TryParse(LogoWidthEntry.Text, out int width) && width > 0)
                settings.LogoMaxWidth = width;
            if (int.TryParse(LogoHeightEntry.Text, out int height) && height >= 0)
                settings.LogoMaxHeight = height;
            
            settings.LogoMaintainAspectRatio = MaintainAspectRatioCheck.IsChecked;
            
            // Save logo data
            if (_uploadedLogoData != null && _uploadedLogoData.Length > 0)
            {
                if (_usingCatalogLogo)
                {
                    settings.SelectedCatalogLogoId = _currentCatalogLogoId;
                    settings.LogoImageData = null;
                }
                else
                {
                    settings.LogoImageData = _uploadedLogoData;
                    settings.SelectedCatalogLogoId = null;
                }
                settings.UseCustomLogo = true;
            }
            else
            {
                settings.LogoImageData = null;
                settings.SelectedCatalogLogoId = null;
                settings.UseCustomLogo = false;
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

/// <summary>
/// Display item for logo catalog in the Branding settings UI
/// </summary>
public class BrandingLogoCatalogItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "General";
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    
    public ImageSource? ImageSource => ImageData.Length > 0 
        ? Microsoft.Maui.Controls.ImageSource.FromStream(() => new MemoryStream(ImageData)) 
        : null;
}
