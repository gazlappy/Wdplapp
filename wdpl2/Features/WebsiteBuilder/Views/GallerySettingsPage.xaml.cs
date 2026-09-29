using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class GallerySettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private const string NoSeason = "No season";

    /// <summary>Newest first, which is the order the website shows them in.</summary>
    private List<Season> _seasons = new();

    /// <summary>What the list is showing: null for every photo, Guid.Empty for photos with no season.</summary>
    private Guid? _showing;

    public GallerySettingsPage()
    {
        InitializeComponent();
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

        _seasons = League.Seasons.OrderByDescending(x => x.StartDate).ToList();

        // New photos go into the season being played, unless told otherwise.
        AddSeasonPicker.ItemsSource = _seasons.Select(x => x.Name).Append(NoSeason).ToList();
        var current = _seasons.FindIndex(x => x.IsActive);
        AddSeasonPicker.SelectedIndex = current >= 0 ? current : (_seasons.Count > 0 ? 0 : _seasons.Count);

        RefreshImageList();
    }

    /// <summary>The season chosen for photos being added, or null for none.</summary>
    private Guid? SeasonForNewPhotos =>
        AddSeasonPicker.SelectedIndex >= 0 && AddSeasonPicker.SelectedIndex < _seasons.Count
            ? _seasons[AddSeasonPicker.SelectedIndex].Id
            : null;

    /// <summary>
    /// A photo whose season has been deleted counts as having none - the
    /// website files it under "Other photos" the same way.
    /// </summary>
    private Guid? SeasonOf(GalleryImage image) =>
        image.SeasonId is Guid id && _seasons.Any(x => x.Id == id) ? id : null;

    private void RefreshShowPicker()
    {
        var images = League.WebsiteSettings.GalleryImages;
        var options = new List<(string Label, Guid? Filter)> { ($"All photos ({images.Count})", null) };
        foreach (var season in _seasons)
        {
            var n = images.Count(i => SeasonOf(i) == season.Id);
            if (n > 0) options.Add(($"{season.Name} ({n})", season.Id));
        }
        var loose = images.Count(i => SeasonOf(i) is null);
        if (loose > 0) options.Add(($"{NoSeason} ({loose})", Guid.Empty));

        _showOptions = options;
        _refreshingShow = true;
        ShowPicker.ItemsSource = options.Select(o => o.Label).ToList();
        var keep = options.FindIndex(o => o.Filter == _showing);
        if (keep < 0) { keep = 0; _showing = null; }
        ShowPicker.SelectedIndex = keep;
        _refreshingShow = false;
    }

    private List<(string Label, Guid? Filter)> _showOptions = new();
    private bool _refreshingShow;

    private void OnShowChanged(object? sender, EventArgs e)
    {
        if (_refreshingShow) return;
        var i = ShowPicker.SelectedIndex;
        _showing = i >= 0 && i < _showOptions.Count ? _showOptions[i].Filter : null;
        RefreshImageList();
    }

    private void RefreshImageList()
    {
        RefreshShowPicker();

        var all = League.WebsiteSettings.GalleryImages;
        var shown = all.Where(i => _showing switch
        {
            null => true,
            var id when id == Guid.Empty => SeasonOf(i) is null,
            var id => SeasonOf(i) == id,
        }).ToList();

        ImageRows.Children.Clear();
        foreach (var image in shown)
            ImageRows.Children.Add(ImageRow(image));

        ImageCountLabel.Text = all.Count.ToString();
        EmptyView.IsVisible = shown.Count == 0;
        EmptyText.Text = all.Count == 0 ? "No images yet" : "No photos in this season";
    }

    /// <summary>One photo: what it is, which season it is in, and a way to remove it.</summary>
    private View ImageRow(GalleryImage image)
    {
        ImageSource? thumbnail = null;
        if (image.ImageData.Length > 0)
        {
            try { thumbnail = ImageSource.FromStream(() => new MemoryStream(image.ImageData)); }
            catch { /* ignore bad data */ }
        }

        var season = new Picker { WidthRequest = 220, FontSize = 13 };
        season.ItemsSource = _seasons.Select(x => x.Name).Append(NoSeason).ToList();
        var at = SeasonOf(image) is Guid id ? _seasons.FindIndex(x => x.Id == id) : -1;
        season.SelectedIndex = at >= 0 ? at : _seasons.Count;
        season.SelectedIndexChanged += (_, _) =>
        {
            var pick = season.SelectedIndex;
            image.SeasonId = pick >= 0 && pick < _seasons.Count ? _seasons[pick].Id : null;
            DataStore.Save();

            // Moved out of the season being shown: let the list catch up.
            RefreshImageList();
            StatusLabel.Text = $"Moved {image.FileName} to {(image.SeasonId is null ? NoSeason : _seasons[pick].Name)}.";
            StatusLabel.TextColor = Color.FromArgb("#10B981");
            StatusLabel.IsVisible = true;
        };

        var delete = new Button
        {
            Text = "X",
            BackgroundColor = Color.FromArgb("#EF4444"),
            TextColor = Colors.White,
            WidthRequest = 35,
            HeightRequest = 35,
            Padding = 0,
            CommandParameter = image.Id,
        };
        delete.Clicked += OnDeleteImageClicked;

        var grid = new Grid
        {
            ColumnDefinitions = { new(50), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) },
            ColumnSpacing = 10,
        };
        grid.Add(new Image { Source = thumbnail, HeightRequest = 40, WidthRequest = 40, Aspect = Aspect.AspectFill, VerticalOptions = LayoutOptions.Center }, 0, 0);
        grid.Add(new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = image.FileName, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.TailTruncation },
                new Label { Text = $"{image.Width}x{image.Height}", FontSize = 11, TextColor = Color.FromArgb("#6B7280") },
            },
        }, 1, 0);
        grid.Add(season, 2, 0);
        grid.Add(delete, 3, 0);

        return new Frame
        {
            Padding = 10,
            BorderColor = Color.FromArgb("#E5E7EB"),
            CornerRadius = 6,
            HasShadow = false,
            Content = grid,
        };
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
                        Category = "General",
                        SeasonId = SeasonForNewPhotos,
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
                var into = SeasonForNewPhotos is Guid sid ? _seasons.First(x => x.Id == sid).Name : NoSeason;
                StatusLabel.Text = $"Added {addedCount} image(s) to {into}";
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
}
