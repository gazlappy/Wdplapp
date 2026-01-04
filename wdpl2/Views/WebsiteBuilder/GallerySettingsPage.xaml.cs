using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class GallerySettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;
    private readonly ObservableCollection<GalleryImageViewModel> _images = new();

    public GallerySettingsPage()
    {
        InitializeComponent();
        ImageList.ItemsSource = _images;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        
        var layouts = (GalleryLayoutPicker.ItemsSource as IList<string>)!;
        var layoutIndex = layouts.IndexOf(settings.GalleryLayout);
        if (layoutIndex >= 0) GalleryLayoutPicker.SelectedIndex = layoutIndex;
        
        GalleryColumnsEntry.Text = settings.GalleryColumns.ToString();
        GalleryShowCaptionsCheck.IsChecked = settings.GalleryShowCaptions;
        GalleryShowCategoriesCheck.IsChecked = settings.GalleryShowCategories;
        GalleryEnableLightboxCheck.IsChecked = settings.GalleryEnableLightbox;
        
        RefreshImageList();
    }

    private void RefreshImageList()
    {
        _images.Clear();
        foreach (var img in League.WebsiteSettings.GalleryImages)
        {
            _images.Add(new GalleryImageViewModel
            {
                Id = img.Id,
                FileName = img.FileName,
                Dimensions = $"{img.Width}x{img.Height}"
            });
        }
        
        ImageCountLabel.Text = _images.Count.ToString();
    }

    private async void OnAddPhotosClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Select Photos",
                FileTypes = FilePickerFileType.Images
            });
            
            if (result == null) return;
            
            var files = result.ToList();
            if (files.Count == 0) return;
            
            StatusLabel.Text = $"Processing {files.Count} image(s)...";
            StatusLabel.TextColor = Color.FromArgb("#3B82F6");
            StatusLabel.IsVisible = true;
            AddPhotosBtn.IsEnabled = false;
            
            var optimizer = new ImageOptimizationService();
            var addedCount = 0;
            
            foreach (var file in files)
            {
                try
                {
                    using var stream = await file.OpenReadAsync();
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    var imageData = memoryStream.ToArray();
                    
                    var (width, height) = await optimizer.GetImageDimensionsAsync(imageData);
                    
                    var galleryImage = new GalleryImage
                    {
                        FileName = file.FileName,
                        ImageData = imageData,
                        Width = width,
                        Height = height,
                        DateAdded = DateTime.Now,
                        Caption = "",
                        Category = "General"
                    };
                    
                    League.WebsiteSettings.GalleryImages.Add(galleryImage);
                    addedCount++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error adding {file.FileName}: {ex.Message}");
                }
            }
            
            if (addedCount > 0)
            {
                DataStore.Save();
                RefreshImageList();
                StatusLabel.Text = $"Added {addedCount} image(s)";
                StatusLabel.TextColor = Color.FromArgb("#10B981");
            }
            else
            {
                StatusLabel.Text = "No images added";
                StatusLabel.TextColor = Color.FromArgb("#EF4444");
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.TextColor = Color.FromArgb("#EF4444");
            StatusLabel.IsVisible = true;
        }
        finally
        {
            AddPhotosBtn.IsEnabled = true;
        }
    }

    private async void OnClearAllClicked(object sender, EventArgs e)
    {
        if (League.WebsiteSettings.GalleryImages.Count == 0)
        {
            await DisplayAlert("Empty", "No images to clear.", "OK");
            return;
        }
        
        var confirm = await DisplayAlert(
            "Clear Gallery",
            $"Remove all {League.WebsiteSettings.GalleryImages.Count} images?",
            "Clear All",
            "Cancel");
        
        if (confirm)
        {
            League.WebsiteSettings.GalleryImages.Clear();
            DataStore.Save();
            RefreshImageList();
            
            StatusLabel.Text = "Gallery cleared";
            StatusLabel.TextColor = Color.FromArgb("#10B981");
            StatusLabel.IsVisible = true;
        }
    }

    private async void OnDeleteImageClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is Guid id)
        {
            var image = League.WebsiteSettings.GalleryImages.FirstOrDefault(i => i.Id == id);
            if (image != null)
            {
                var confirm = await DisplayAlert("Delete", $"Remove '{image.FileName}'?", "Delete", "Cancel");
                if (confirm)
                {
                    League.WebsiteSettings.GalleryImages.Remove(image);
                    DataStore.Save();
                    RefreshImageList();
                }
            }
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            settings.GalleryLayout = GalleryLayoutPicker.SelectedItem?.ToString() ?? "grid";
            if (int.TryParse(GalleryColumnsEntry.Text, out int columns))
                settings.GalleryColumns = columns;
            settings.GalleryShowCaptions = GalleryShowCaptionsCheck.IsChecked;
            settings.GalleryShowCategories = GalleryShowCategoriesCheck.IsChecked;
            settings.GalleryEnableLightbox = GalleryEnableLightboxCheck.IsChecked;
            
            DataStore.Save();
            
            await DisplayAlert("Saved", "Gallery settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
    
    private class GalleryImageViewModel
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = "";
        public string Dimensions { get; set; } = "";
    }
}
