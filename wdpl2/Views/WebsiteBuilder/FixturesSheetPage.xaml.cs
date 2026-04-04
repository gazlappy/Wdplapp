using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class FixturesSheetPage : ContentPage
{
    private static LeagueData League => DataStore.Data;
    private readonly ObservableCollection<Season> _seasons = new();
    private readonly ObservableCollection<DivisionSelection> _divisions = new();
    private readonly ObservableCollection<SpecialEventItem> _events = new();
    private readonly ObservableCollection<VenuePhoneItem> _venuePhones = new();
    private readonly ObservableCollection<LogoCatalogDisplayItem> _logoCatalog = new();
    private string? _generatedHtml;
    
    // Logo state
    private byte[]? _currentLogoData;
    private string? _currentCatalogLogoId;
    private bool _usingCatalogLogo;

    public FixturesSheetPage()
    {
        InitializeComponent();
        
        SeasonPicker.ItemsSource = _seasons;
        SeasonPicker.ItemDisplayBinding = new Binding("Name");
        SeasonPicker.SelectedIndexChanged += OnSeasonChanged;
        
        DivisionsCollection.ItemsSource = _divisions;
        EventsCollection.ItemsSource = _events;
        VenuePhonesCollection.ItemsSource = _venuePhones;
        LogoCatalogCollection.ItemsSource = _logoCatalog;
        
        // Set default logo position
        LogoPositionPicker.SelectedIndex = 0;
        
        LoadData();
        LoadLogoCatalog();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Refresh the shared catalog in case logos were added/removed in Site Branding
        LoadLogoCatalog();
    }

    private void LoadData()
    {
        _seasons.Clear();
        foreach (var season in League.Seasons.OrderByDescending(s => s.StartDate))
            _seasons.Add(season);
        
        // Select active season
        var activeSeason = _seasons.FirstOrDefault(s => s.IsActive) ?? _seasons.FirstOrDefault();
        if (activeSeason != null)
        {
            SeasonPicker.SelectedItem = activeSeason;
        }
        
        // Load website settings defaults
        var settings = League.WebsiteSettings;
        LeagueNameEntry.Text = settings.LeagueName;
        WebsiteUrlEntry.Text = settings.WebsiteUrl;
        EmailEntry.Text = settings.ContactEmail;

        // Load printable sheet embed settings
        ShowPrintableSheetCheck.IsChecked = settings.FixturesShowPrintableSheet;
        SheetDefaultExpandedCheck.IsChecked = settings.FixturesSheetDefaultExpanded;
        SheetTitleEntry.Text = settings.FixturesSheetTitle;

        // Load logo from website settings if available (supports both uploaded and catalog logos)
        var effectiveLogo = settings.GetEffectiveLogoData();
        if (settings.UseCustomLogo && effectiveLogo != null && effectiveLogo.Length > 0)
        {
            _currentLogoData = effectiveLogo;
            _usingCatalogLogo = !string.IsNullOrEmpty(settings.SelectedCatalogLogoId);
            _currentCatalogLogoId = settings.SelectedCatalogLogoId;
            ShowLogoCheck.IsChecked = true;
            UpdateLogoPreview();
        }

        // Auto-load saved design settings if available
        if (settings.FixturesSheetDesign != null)
        {
            OnLoadDesignClicked(this, EventArgs.Empty);
        }
    }

    private void LoadLogoCatalog()
    {
        _logoCatalog.Clear();
        foreach (var item in League.WebsiteSettings.LogoCatalog)
        {
            _logoCatalog.Add(LogoCatalogDisplayItem.FromModel(item));
        }
    }

    private void OnSeasonChanged(object? sender, EventArgs e)
    {
        if (SeasonPicker.SelectedItem is not Season season) return;
        
        _divisions.Clear();
        var seasonDivisions = League.Divisions.Where(d => d.SeasonId == season.Id).OrderBy(d => d.Name);
        foreach (var div in seasonDivisions)
        {
            _divisions.Add(new DivisionSelection { Id = div.Id, Name = div.Name ?? "", IsSelected = true });
        }
        
        SeasonTitleEntry.Text = season.Name;
        
        // Auto-populate venue phones from venues
        _venuePhones.Clear();
        var venues = League.Venues.Where(v => v.SeasonId == season.Id);
        foreach (var venue in venues)
        {
            if (!string.IsNullOrWhiteSpace(venue.Notes) && venue.Notes.Any(char.IsDigit))
            {
                // Assume notes might contain phone number
                _venuePhones.Add(new VenuePhoneItem { VenueName = venue.Name, PhoneNumber = venue.Notes });
            }
        }
    }

    private void OnDivisionCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        // Handle in binding
    }

    #region Logo Handling

    private void OnShowLogoChanged(object sender, CheckedChangedEventArgs e)
    {
        LogoOptionsStack.IsVisible = e.Value;
    }

    private async void OnUploadLogoClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Logo Image",
                FileTypes = FilePickerFileType.Images
            });

            if (result != null)
            {
                using var stream = await result.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                _currentLogoData = memoryStream.ToArray();
                _usingCatalogLogo = false;
                _currentCatalogLogoId = null;
                
                UpdateLogoPreview();
                SetStatus($"Logo loaded: {result.FileName}");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load logo: {ex.Message}", "OK");
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
                _currentLogoData = logo.ImageData;
                _usingCatalogLogo = true;
                _currentCatalogLogoId = logo.Id;
                UpdateLogoPreview();
                SetStatus($"Using catalog logo: {logo.Name}");
            }
        }
    }

    private void OnRemoveLogoClicked(object sender, EventArgs e)
    {
        _currentLogoData = null;
        _usingCatalogLogo = false;
        _currentCatalogLogoId = null;
        
        LogoPreviewFrame.IsVisible = false;
        SaveToCatalogBtn.IsEnabled = false;
        SetStatus("Logo removed");
    }

    private async void OnSaveToCatalogClicked(object sender, EventArgs e)
    {
        if (_currentLogoData == null || _currentLogoData.Length == 0)
        {
            await DisplayAlert("No Logo", "Please upload a logo first.", "OK");
            return;
        }

        var name = await DisplayPromptAsync("Save to Catalog", "Enter a name for this logo:", placeholder: "League Logo");
        if (string.IsNullOrWhiteSpace(name)) return;

        var category = await DisplayPromptAsync("Save to Catalog", "Enter a category (optional):", placeholder: "General");
        if (string.IsNullOrEmpty(category)) category = "General";

        // Add to the shared catalog in WebsiteSettings
        League.WebsiteSettings.AddLogoCatalogItem(name, _currentLogoData, "", category);
        DataStore.Save();

        // Refresh the display list
        LoadLogoCatalog();

        SetStatus($"Logo saved to catalog: {name}");
    }

    private void OnUseCatalogLogoClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is LogoCatalogDisplayItem item)
        {
            _currentLogoData = item.ImageData;
            _usingCatalogLogo = true;
            _currentCatalogLogoId = item.Id;
            UpdateLogoPreview();
            SetStatus($"Using catalog logo: {item.Name}");
        }
    }

    private async void OnDeleteCatalogLogoClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is LogoCatalogDisplayItem item)
        {
            var confirm = await DisplayAlert("Delete Logo", $"Delete '{item.Name}' from catalog?", "Delete", "Cancel");
            if (confirm)
            {
                League.WebsiteSettings.RemoveLogoCatalogItem(item.Id);
                _logoCatalog.Remove(item);

                // If this was the current logo, clear it
                if (_currentCatalogLogoId == item.Id)
                {
                    _currentLogoData = null;
                    _usingCatalogLogo = false;
                    _currentCatalogLogoId = null;
                    LogoPreviewFrame.IsVisible = false;
                    SaveToCatalogBtn.IsEnabled = false;
                }

                DataStore.Save();
                SetStatus($"Logo removed from catalog: {item.Name}");
            }
        }
    }

    private void UpdateLogoPreview()
    {
        if (_currentLogoData != null && _currentLogoData.Length > 0)
        {
            LogoPreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(_currentLogoData));
            LogoPreviewFrame.IsVisible = true;
            SaveToCatalogBtn.IsEnabled = !_usingCatalogLogo; // Only enable save if it's a new upload
            
            if (_usingCatalogLogo)
            {
                var catalogItem = _logoCatalog.FirstOrDefault(l => l.Id == _currentCatalogLogoId);
                LogoSourceLabel.Text = $"From catalog: {catalogItem?.Name ?? "Unknown"}";
            }
            else
            {
                LogoSourceLabel.Text = "Custom uploaded logo";
            }
        }
        else
        {
            LogoPreviewFrame.IsVisible = false;
            SaveToCatalogBtn.IsEnabled = false;
        }
    }

    private LogoPosition GetSelectedLogoPosition()
    {
        return LogoPositionPicker.SelectedIndex switch
        {
            0 => LogoPosition.AboveTitle,
            1 => LogoPosition.BelowTitle,
            2 => LogoPosition.LeftOfTitle,
            3 => LogoPosition.RightOfTitle,
            4 => LogoPosition.TopLeft,
            5 => LogoPosition.TopRight,
            6 => LogoPosition.BottomLeft,
            7 => LogoPosition.BottomRight,
            _ => LogoPosition.AboveTitle
        };
    }

    private string GetSelectedAccentColor()
    {
        return AccentColorPicker.SelectedIndex switch
        {
            1 => "#1E3A5F",
            2 => "#1B4332",
            3 => "#6B1D2A",
            4 => "#334155",
            5 => "#1D4ED8",
            6 => "#047857",
            7 => "#B91C1C",
            _ => "#1a1a1a"
        };
    }

    private TitleStyle GetSelectedTitleStyle()
    {
        return TitleStylePicker.SelectedIndex switch
        {
            1 => TitleStyle.SingleRule,
            2 => TitleStyle.BoxBorder,
            3 => TitleStyle.None,
            4 => TitleStyle.Gradient,
            5 => TitleStyle.Shadow,
            _ => TitleStyle.DoubleRule
        };
    }

    private GridBorderWeight GetSelectedGridBorders()
    {
        return GridBorderPicker.SelectedIndex switch
        {
            0 => GridBorderWeight.Fine,
            2 => GridBorderWeight.Bold,
            3 => GridBorderWeight.Double,
            _ => GridBorderWeight.Medium
        };
    }

    private HomeBadgeStyle GetSelectedHomeBadge()
    {
        return HomeBadgePicker.SelectedIndex switch
        {
            1 => HomeBadgeStyle.BoldOnly,
            2 => HomeBadgeStyle.Underline,
            3 => HomeBadgeStyle.Highlight,
            4 => HomeBadgeStyle.None,
            _ => HomeBadgeStyle.Pill
        };
    }

    private SheetFontFamily GetSelectedFontFamily()
    {
        return FontFamilyPicker.SelectedIndex switch
        {
            1 => SheetFontFamily.Classic,
            2 => SheetFontFamily.Mono,
            3 => SheetFontFamily.Sport,
            4 => SheetFontFamily.Elegant,
            5 => SheetFontFamily.Handwritten,
            6 => SheetFontFamily.Condensed,
            7 => SheetFontFamily.Rounded,
            8 => SheetFontFamily.Newspaper,
            9 => SheetFontFamily.Technical,
            10 => SheetFontFamily.Display,
            11 => SheetFontFamily.Casual,
            _ => SheetFontFamily.Modern
        };
    }

    private FontScale GetSelectedFontScale()
    {
        return FontScalePicker.SelectedIndex switch
        {
            0 => FontScale.ExtraSmall,
            1 => FontScale.Small,
            3 => FontScale.Medium,
            4 => FontScale.Large,
            5 => FontScale.ExtraLarge,
            _ => FontScale.Default
        };
    }

    private TitleFontSize GetSelectedTitleFontSize()
    {
        return TitleFontSizePicker.SelectedIndex switch
        {
            0 => TitleFontSize.Small,
            2 => TitleFontSize.Large,
            3 => TitleFontSize.ExtraLarge,
            4 => TitleFontSize.Huge,
            _ => TitleFontSize.Medium
        };
    }

    private Services.FontWeight GetSelectedFontWeight()
    {
        return FontWeightPicker.SelectedIndex switch
        {
            0 => Services.FontWeight.Light,
            2 => Services.FontWeight.SemiBold,
            3 => Services.FontWeight.Bold,
            _ => Services.FontWeight.Normal
        };
    }

    private MonthPalette GetSelectedMonthPalette()
    {
        return MonthPalettePicker.SelectedIndex switch
        {
            1 => MonthPalette.Vibrant,
            2 => MonthPalette.Monochrome,
            3 => MonthPalette.Earth,
            4 => MonthPalette.Ocean,
            5 => MonthPalette.Pastel,
            6 => MonthPalette.Neon,
            _ => MonthPalette.Muted
        };
    }

    private ColumnBanding GetSelectedColumnBanding()
    {
        return ColumnBandingPicker.SelectedIndex switch
        {
            0 => ColumnBanding.None,
            2 => ColumnBanding.Strong,
            3 => ColumnBanding.Alternating,
            _ => ColumnBanding.Subtle
        };
    }

    private SubtitleStyle GetSelectedSubtitleStyle()
    {
        return SubtitleStylePicker.SelectedIndex switch
        {
            1 => SubtitleStyle.Outline,
            2 => SubtitleStyle.TextOnly,
            _ => SubtitleStyle.FilledBar
        };
    }

    private DivisionLayout GetSelectedDivisionLayout()
    {
        return DivisionLayoutPicker.SelectedIndex switch
        {
            1 => DivisionLayout.Stacked,
            2 => DivisionLayout.Compact,
            _ => DivisionLayout.Auto
        };
    }

    private TextDensity GetSelectedTextDensity()
    {
        return TextDensityPicker.SelectedIndex switch
        {
            0 => TextDensity.Compact,
            2 => TextDensity.Spacious,
            _ => TextDensity.Normal
        };
    }

    private CardStyle GetSelectedCardStyle()
    {
        return CardStylePicker.SelectedIndex switch
        {
            1 => CardStyle.Frosted,
            2 => CardStyle.Translucent,
            3 => CardStyle.Outlined,
            4 => CardStyle.Minimal,
            _ => CardStyle.Solid
        };
    }

    private SheetLayout GetSelectedLayout()
    {
        return LayoutPicker.SelectedIndex switch
        {
            1 => SheetLayout.WeeklyList,
            2 => SheetLayout.CompactGrid,
            3 => SheetLayout.SeasonMatrix,
            _ => SheetLayout.MonthCards
        };
    }

    private HeaderPattern GetSelectedHeaderPattern()
    {
        return HeaderPatternPicker.SelectedIndex switch
        {
            1 => HeaderPattern.Dots,
            2 => HeaderPattern.Diagonal,
            3 => HeaderPattern.Circles,
            4 => HeaderPattern.None,
            _ => HeaderPattern.Crosshatch
        };
    }

    private RowStriping GetSelectedRowStriping()
    {
        return RowStripingPicker.SelectedIndex switch
        {
            0 => RowStriping.None,
            2 => RowStriping.Medium,
            3 => RowStriping.Accent,
            _ => RowStriping.Subtle
        };
    }

    private VsSeparatorStyle GetSelectedVsSeparator()
    {
        return VsSeparatorPicker.SelectedIndex switch
        {
            1 => VsSeparatorStyle.Vs,
            2 => VsSeparatorStyle.Dash,
            3 => VsSeparatorStyle.Dot,
            4 => VsSeparatorStyle.None,
            _ => VsSeparatorStyle.LowercaseV
        };
    }

    private SheetFooterStyle GetSelectedFooterStyle()
    {
        return FooterStylePicker.SelectedIndex switch
        {
            1 => SheetFooterStyle.FullAccent,
            2 => SheetFooterStyle.Simple,
            3 => SheetFooterStyle.Minimal,
            _ => SheetFooterStyle.AccentTop
        };
    }

    private CornerStyle GetSelectedCornerStyle()
    {
        return CornerStylePicker.SelectedIndex switch
        {
            0 => CornerStyle.Sharp,
            2 => CornerStyle.ExtraRound,
            _ => CornerStyle.Rounded
        };
    }

    private void OnToggleAdvancedClicked(object sender, EventArgs e)
    {
        AdvancedOptionsStack.IsVisible = !AdvancedOptionsStack.IsVisible;
        AdvancedToggleIcon.Text = AdvancedOptionsStack.IsVisible ? "\u25bc" : "\u25b6";
    }

    private void OnApplyLayoutPresetClicked(object sender, EventArgs e)
    {
        if (LayoutPresetPicker.SelectedIndex < 0)
        {
            SetStatus("Please select a layout preset first");
            return;
        }

        // Layout presets only set: LayoutIndex, DivisionLayoutIndex, TextDensityIndex, CornerStyleIndex,
        // IsLandscape, ShowMatchNight, ShowTeamNumbers, ShowVenueInfo, ShowDivisionLists, ShowGridLegend
        switch (LayoutPresetPicker.SelectedIndex)
        {
            case 0: // Month Cards — Standard Portrait
                LayoutPicker.SelectedIndex = 0; DivisionLayoutPicker.SelectedIndex = 0; TextDensityPicker.SelectedIndex = 1;
                CornerStylePicker.SelectedIndex = 1; LandscapeRadio.IsChecked = false; PortraitRadio.IsChecked = true;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
            case 1: // Month Cards — Compact Portrait
                LayoutPicker.SelectedIndex = 0; DivisionLayoutPicker.SelectedIndex = 2; TextDensityPicker.SelectedIndex = 0;
                CornerStylePicker.SelectedIndex = 0; LandscapeRadio.IsChecked = false; PortraitRadio.IsChecked = true;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = false;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = false;
                break;
            case 2: // Month Cards — Spacious Portrait
                LayoutPicker.SelectedIndex = 0; DivisionLayoutPicker.SelectedIndex = 0; TextDensityPicker.SelectedIndex = 2;
                CornerStylePicker.SelectedIndex = 2; LandscapeRadio.IsChecked = false; PortraitRadio.IsChecked = true;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
            case 3: // Month Cards — Landscape
                LayoutPicker.SelectedIndex = 0; DivisionLayoutPicker.SelectedIndex = 1; TextDensityPicker.SelectedIndex = 1;
                CornerStylePicker.SelectedIndex = 1; LandscapeRadio.IsChecked = true; PortraitRadio.IsChecked = false;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
            case 4: // Month Cards — Landscape Dense
                LayoutPicker.SelectedIndex = 0; DivisionLayoutPicker.SelectedIndex = 2; TextDensityPicker.SelectedIndex = 0;
                CornerStylePicker.SelectedIndex = 0; LandscapeRadio.IsChecked = true; PortraitRadio.IsChecked = false;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
            case 5: // Month Cards — Minimal
                LayoutPicker.SelectedIndex = 0; DivisionLayoutPicker.SelectedIndex = 0; TextDensityPicker.SelectedIndex = 0;
                CornerStylePicker.SelectedIndex = 1; LandscapeRadio.IsChecked = false; PortraitRadio.IsChecked = true;
                ShowMatchNightCheck.IsChecked = false; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = false;
                ShowDivisionListsCheck.IsChecked = false; ShowGridLegendCheck.IsChecked = false;
                break;
            case 6: // Weekly List — Standard Portrait
                LayoutPicker.SelectedIndex = 1; DivisionLayoutPicker.SelectedIndex = 0; TextDensityPicker.SelectedIndex = 1;
                CornerStylePicker.SelectedIndex = 1; LandscapeRadio.IsChecked = false; PortraitRadio.IsChecked = true;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
            case 7: // Weekly List — Spacious Portrait
                LayoutPicker.SelectedIndex = 1; DivisionLayoutPicker.SelectedIndex = 0; TextDensityPicker.SelectedIndex = 2;
                CornerStylePicker.SelectedIndex = 2; LandscapeRadio.IsChecked = false; PortraitRadio.IsChecked = true;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
            case 8: // Weekly List — Compact Portrait
                LayoutPicker.SelectedIndex = 1; DivisionLayoutPicker.SelectedIndex = 2; TextDensityPicker.SelectedIndex = 0;
                CornerStylePicker.SelectedIndex = 0; LandscapeRadio.IsChecked = false; PortraitRadio.IsChecked = true;
                ShowMatchNightCheck.IsChecked = false; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = false;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = false;
                break;
            case 9: // Weekly List — Landscape
                LayoutPicker.SelectedIndex = 1; DivisionLayoutPicker.SelectedIndex = 1; TextDensityPicker.SelectedIndex = 1;
                CornerStylePicker.SelectedIndex = 1; LandscapeRadio.IsChecked = true; PortraitRadio.IsChecked = false;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
            case 10: // Compact Grid — Portrait
                LayoutPicker.SelectedIndex = 2; DivisionLayoutPicker.SelectedIndex = 0; TextDensityPicker.SelectedIndex = 1;
                CornerStylePicker.SelectedIndex = 1; LandscapeRadio.IsChecked = false; PortraitRadio.IsChecked = true;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
            case 11: // Compact Grid — Portrait Dense
                LayoutPicker.SelectedIndex = 2; DivisionLayoutPicker.SelectedIndex = 1; TextDensityPicker.SelectedIndex = 0;
                CornerStylePicker.SelectedIndex = 0; LandscapeRadio.IsChecked = false; PortraitRadio.IsChecked = true;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
            case 12: // Compact Grid — Landscape
                LayoutPicker.SelectedIndex = 2; DivisionLayoutPicker.SelectedIndex = 0; TextDensityPicker.SelectedIndex = 1;
                CornerStylePicker.SelectedIndex = 1; LandscapeRadio.IsChecked = true; PortraitRadio.IsChecked = false;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
            case 13: // Compact Grid — Landscape Dense
                LayoutPicker.SelectedIndex = 2; DivisionLayoutPicker.SelectedIndex = 2; TextDensityPicker.SelectedIndex = 0;
                CornerStylePicker.SelectedIndex = 0; LandscapeRadio.IsChecked = true; PortraitRadio.IsChecked = false;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
            case 14: // Season Matrix — Landscape
                LayoutPicker.SelectedIndex = 3; DivisionLayoutPicker.SelectedIndex = 0; TextDensityPicker.SelectedIndex = 0;
                CornerStylePicker.SelectedIndex = 0; LandscapeRadio.IsChecked = true; PortraitRadio.IsChecked = false;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = false; ShowGridLegendCheck.IsChecked = true;
                break;
            case 15: // Season Matrix — Portrait
                LayoutPicker.SelectedIndex = 3; DivisionLayoutPicker.SelectedIndex = 0; TextDensityPicker.SelectedIndex = 0;
                CornerStylePicker.SelectedIndex = 1; LandscapeRadio.IsChecked = false; PortraitRadio.IsChecked = true;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = false;
                ShowDivisionListsCheck.IsChecked = false; ShowGridLegendCheck.IsChecked = true;
                break;
            case 16: // Season Matrix — Landscape Spacious
                LayoutPicker.SelectedIndex = 3; DivisionLayoutPicker.SelectedIndex = 0; TextDensityPicker.SelectedIndex = 2;
                CornerStylePicker.SelectedIndex = 2; LandscapeRadio.IsChecked = true; PortraitRadio.IsChecked = false;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = false; ShowGridLegendCheck.IsChecked = true;
                break;
            case 17: // Month Cards — Notice Board
                LayoutPicker.SelectedIndex = 0; DivisionLayoutPicker.SelectedIndex = 1; TextDensityPicker.SelectedIndex = 2;
                CornerStylePicker.SelectedIndex = 2; LandscapeRadio.IsChecked = true; PortraitRadio.IsChecked = false;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
            case 18: // Weekly List — Full Detail
                LayoutPicker.SelectedIndex = 1; DivisionLayoutPicker.SelectedIndex = 0; TextDensityPicker.SelectedIndex = 2;
                CornerStylePicker.SelectedIndex = 1; LandscapeRadio.IsChecked = false; PortraitRadio.IsChecked = true;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
            case 19: // Compact Grid — Pub Handout
                LayoutPicker.SelectedIndex = 2; DivisionLayoutPicker.SelectedIndex = 1; TextDensityPicker.SelectedIndex = 0;
                CornerStylePicker.SelectedIndex = 0; LandscapeRadio.IsChecked = true; PortraitRadio.IsChecked = false;
                ShowMatchNightCheck.IsChecked = true; ShowTeamNumbersCheck.IsChecked = true; ShowVenueInfoCheck.IsChecked = true;
                ShowDivisionListsCheck.IsChecked = true; ShowGridLegendCheck.IsChecked = true;
                break;
        }

        SetStatus($"Applied layout: {LayoutPresetPicker.SelectedItem}");
    }

    private void OnApplyStylePresetClicked(object sender, EventArgs e)
    {
        if (StylePresetPicker.SelectedIndex < 0)
        {
            SetStatus("Please select a style preset first");
            return;
        }

        // Style presets only set visual properties: AccentColor, TitleStyle, GridBorders, HomeBadge,
        // FontFamily, SubtitleStyle, MonthPalette, ColumnBanding, CardStyle, HeaderPattern,
        // RowStriping, VsSeparator, FooterStyle, TitleUppercase, MonthUppercase
        switch (StylePresetPicker.SelectedIndex)
        {
            case 0: // Classic Pool League
                AccentColorPicker.SelectedIndex = 0; TitleStylePicker.SelectedIndex = 0; GridBorderPicker.SelectedIndex = 1;
                HomeBadgePicker.SelectedIndex = 0; FontFamilyPicker.SelectedIndex = 1; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 0; ColumnBandingPicker.SelectedIndex = 1; CardStylePicker.SelectedIndex = 0;
                HeaderPatternPicker.SelectedIndex = 0; RowStripingPicker.SelectedIndex = 1; VsSeparatorPicker.SelectedIndex = 0;
                FooterStylePicker.SelectedIndex = 0; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 1;
                break;
            case 1: // Modern Sports
                AccentColorPicker.SelectedIndex = 5; TitleStylePicker.SelectedIndex = 4; GridBorderPicker.SelectedIndex = 0;
                HomeBadgePicker.SelectedIndex = 3; FontFamilyPicker.SelectedIndex = 0; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 1; ColumnBandingPicker.SelectedIndex = 0; CardStylePicker.SelectedIndex = 1;
                HeaderPatternPicker.SelectedIndex = 1; RowStripingPicker.SelectedIndex = 0; VsSeparatorPicker.SelectedIndex = 1;
                FooterStylePicker.SelectedIndex = 1; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 2; FontWeightPicker.SelectedIndex = 2;
                break;
            case 2: // Pub League
                AccentColorPicker.SelectedIndex = 3; TitleStylePicker.SelectedIndex = 2; GridBorderPicker.SelectedIndex = 2;
                HomeBadgePicker.SelectedIndex = 1; FontFamilyPicker.SelectedIndex = 3; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 3; ColumnBandingPicker.SelectedIndex = 2; CardStylePicker.SelectedIndex = 0;
                HeaderPatternPicker.SelectedIndex = 4; RowStripingPicker.SelectedIndex = 2; VsSeparatorPicker.SelectedIndex = 0;
                FooterStylePicker.SelectedIndex = 2; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = false;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 2; FontWeightPicker.SelectedIndex = 1;
                break;
            case 3: // Professional Tournament
                AccentColorPicker.SelectedIndex = 4; TitleStylePicker.SelectedIndex = 5; GridBorderPicker.SelectedIndex = 0;
                HomeBadgePicker.SelectedIndex = 2; FontFamilyPicker.SelectedIndex = 4; SubtitleStylePicker.SelectedIndex = 1;
                MonthPalettePicker.SelectedIndex = 2; ColumnBandingPicker.SelectedIndex = 1; CardStylePicker.SelectedIndex = 3;
                HeaderPatternPicker.SelectedIndex = 2; RowStripingPicker.SelectedIndex = 1; VsSeparatorPicker.SelectedIndex = 2;
                FooterStylePicker.SelectedIndex = 0; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 2; FontWeightPicker.SelectedIndex = 1;
                break;
            case 4: // Community / Sunday League
                AccentColorPicker.SelectedIndex = 6; TitleStylePicker.SelectedIndex = 3; GridBorderPicker.SelectedIndex = 1;
                HomeBadgePicker.SelectedIndex = 0; FontFamilyPicker.SelectedIndex = 7; SubtitleStylePicker.SelectedIndex = 2;
                MonthPalettePicker.SelectedIndex = 4; ColumnBandingPicker.SelectedIndex = 1; CardStylePicker.SelectedIndex = 0;
                HeaderPatternPicker.SelectedIndex = 3; RowStripingPicker.SelectedIndex = 1; VsSeparatorPicker.SelectedIndex = 1;
                FooterStylePicker.SelectedIndex = 3; TitleUppercaseCheck.IsChecked = false; MonthUppercaseCheck.IsChecked = false;
                FontScalePicker.SelectedIndex = 3; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 1;
                break;
            case 5: // Retro
                AccentColorPicker.SelectedIndex = 0; TitleStylePicker.SelectedIndex = 0; GridBorderPicker.SelectedIndex = 3;
                HomeBadgePicker.SelectedIndex = 1; FontFamilyPicker.SelectedIndex = 2; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 2; ColumnBandingPicker.SelectedIndex = 3; CardStylePicker.SelectedIndex = 4;
                HeaderPatternPicker.SelectedIndex = 0; RowStripingPicker.SelectedIndex = 3; VsSeparatorPicker.SelectedIndex = 2;
                FooterStylePicker.SelectedIndex = 2; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 1;
                break;
            case 6: // Snooker Club
                AccentColorPicker.SelectedIndex = 2; TitleStylePicker.SelectedIndex = 5; GridBorderPicker.SelectedIndex = 1;
                HomeBadgePicker.SelectedIndex = 2; FontFamilyPicker.SelectedIndex = 4; SubtitleStylePicker.SelectedIndex = 1;
                MonthPalettePicker.SelectedIndex = 3; ColumnBandingPicker.SelectedIndex = 1; CardStylePicker.SelectedIndex = 3;
                HeaderPatternPicker.SelectedIndex = 2; RowStripingPicker.SelectedIndex = 1; VsSeparatorPicker.SelectedIndex = 2;
                FooterStylePicker.SelectedIndex = 0; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 2; FontWeightPicker.SelectedIndex = 0;
                break;
            case 7: // Midnight
                AccentColorPicker.SelectedIndex = 0; TitleStylePicker.SelectedIndex = 4; GridBorderPicker.SelectedIndex = 0;
                HomeBadgePicker.SelectedIndex = 3; FontFamilyPicker.SelectedIndex = 0; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 6; ColumnBandingPicker.SelectedIndex = 0; CardStylePicker.SelectedIndex = 1;
                HeaderPatternPicker.SelectedIndex = 1; RowStripingPicker.SelectedIndex = 0; VsSeparatorPicker.SelectedIndex = 3;
                FooterStylePicker.SelectedIndex = 1; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 1;
                break;
            case 8: // Ocean Breeze
                AccentColorPicker.SelectedIndex = 5; TitleStylePicker.SelectedIndex = 1; GridBorderPicker.SelectedIndex = 0;
                HomeBadgePicker.SelectedIndex = 0; FontFamilyPicker.SelectedIndex = 7; SubtitleStylePicker.SelectedIndex = 2;
                MonthPalettePicker.SelectedIndex = 4; ColumnBandingPicker.SelectedIndex = 1; CardStylePicker.SelectedIndex = 2;
                HeaderPatternPicker.SelectedIndex = 3; RowStripingPicker.SelectedIndex = 1; VsSeparatorPicker.SelectedIndex = 1;
                FooterStylePicker.SelectedIndex = 2; TitleUppercaseCheck.IsChecked = false; MonthUppercaseCheck.IsChecked = false;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 0;
                break;
            case 9: // Woodland
                AccentColorPicker.SelectedIndex = 2; TitleStylePicker.SelectedIndex = 2; GridBorderPicker.SelectedIndex = 1;
                HomeBadgePicker.SelectedIndex = 1; FontFamilyPicker.SelectedIndex = 1; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 3; ColumnBandingPicker.SelectedIndex = 2; CardStylePicker.SelectedIndex = 0;
                HeaderPatternPicker.SelectedIndex = 0; RowStripingPicker.SelectedIndex = 2; VsSeparatorPicker.SelectedIndex = 0;
                FooterStylePicker.SelectedIndex = 0; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 1;
                break;
            case 10: // Championship Gold
                AccentColorPicker.SelectedIndex = 7; TitleStylePicker.SelectedIndex = 5; GridBorderPicker.SelectedIndex = 2;
                HomeBadgePicker.SelectedIndex = 0; FontFamilyPicker.SelectedIndex = 3; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 1; ColumnBandingPicker.SelectedIndex = 2; CardStylePicker.SelectedIndex = 0;
                HeaderPatternPicker.SelectedIndex = 2; RowStripingPicker.SelectedIndex = 3; VsSeparatorPicker.SelectedIndex = 1;
                FooterStylePicker.SelectedIndex = 1; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 3; FontWeightPicker.SelectedIndex = 3;
                break;
            case 11: // Minimalist
                AccentColorPicker.SelectedIndex = 4; TitleStylePicker.SelectedIndex = 3; GridBorderPicker.SelectedIndex = 0;
                HomeBadgePicker.SelectedIndex = 1; FontFamilyPicker.SelectedIndex = 6; SubtitleStylePicker.SelectedIndex = 2;
                MonthPalettePicker.SelectedIndex = 2; ColumnBandingPicker.SelectedIndex = 0; CardStylePicker.SelectedIndex = 4;
                HeaderPatternPicker.SelectedIndex = 4; RowStripingPicker.SelectedIndex = 0; VsSeparatorPicker.SelectedIndex = 2;
                FooterStylePicker.SelectedIndex = 3; TitleUppercaseCheck.IsChecked = false; MonthUppercaseCheck.IsChecked = false;
                FontScalePicker.SelectedIndex = 1; TitleFontSizePicker.SelectedIndex = 0; FontWeightPicker.SelectedIndex = 0;
                break;
            case 12: // Newspaper
                AccentColorPicker.SelectedIndex = 0; TitleStylePicker.SelectedIndex = 0; GridBorderPicker.SelectedIndex = 3;
                HomeBadgePicker.SelectedIndex = 1; FontFamilyPicker.SelectedIndex = 8; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 2; ColumnBandingPicker.SelectedIndex = 3; CardStylePicker.SelectedIndex = 4;
                HeaderPatternPicker.SelectedIndex = 4; RowStripingPicker.SelectedIndex = 2; VsSeparatorPicker.SelectedIndex = 2;
                FooterStylePicker.SelectedIndex = 2; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 3; FontWeightPicker.SelectedIndex = 2;
                break;
            case 13: // Neon Nights
                AccentColorPicker.SelectedIndex = 7; TitleStylePicker.SelectedIndex = 4; GridBorderPicker.SelectedIndex = 0;
                HomeBadgePicker.SelectedIndex = 3; FontFamilyPicker.SelectedIndex = 0; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 6; ColumnBandingPicker.SelectedIndex = 0; CardStylePicker.SelectedIndex = 1;
                HeaderPatternPicker.SelectedIndex = 1; RowStripingPicker.SelectedIndex = 3; VsSeparatorPicker.SelectedIndex = 3;
                FooterStylePicker.SelectedIndex = 1; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 2; FontWeightPicker.SelectedIndex = 3;
                break;
            case 14: // Elegant
                AccentColorPicker.SelectedIndex = 1; TitleStylePicker.SelectedIndex = 1; GridBorderPicker.SelectedIndex = 0;
                HomeBadgePicker.SelectedIndex = 2; FontFamilyPicker.SelectedIndex = 4; SubtitleStylePicker.SelectedIndex = 1;
                MonthPalettePicker.SelectedIndex = 0; ColumnBandingPicker.SelectedIndex = 1; CardStylePicker.SelectedIndex = 3;
                HeaderPatternPicker.SelectedIndex = 2; RowStripingPicker.SelectedIndex = 1; VsSeparatorPicker.SelectedIndex = 2;
                FooterStylePicker.SelectedIndex = 0; TitleUppercaseCheck.IsChecked = false; MonthUppercaseCheck.IsChecked = false;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 0;
                break;
            case 15: // Tropical
                AccentColorPicker.SelectedIndex = 6; TitleStylePicker.SelectedIndex = 4; GridBorderPicker.SelectedIndex = 1;
                HomeBadgePicker.SelectedIndex = 3; FontFamilyPicker.SelectedIndex = 7; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 1; ColumnBandingPicker.SelectedIndex = 1; CardStylePicker.SelectedIndex = 2;
                HeaderPatternPicker.SelectedIndex = 3; RowStripingPicker.SelectedIndex = 1; VsSeparatorPicker.SelectedIndex = 0;
                FooterStylePicker.SelectedIndex = 1; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 1;
                break;
            case 16: // Corporate
                AccentColorPicker.SelectedIndex = 4; TitleStylePicker.SelectedIndex = 1; GridBorderPicker.SelectedIndex = 0;
                HomeBadgePicker.SelectedIndex = 1; FontFamilyPicker.SelectedIndex = 6; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 2; ColumnBandingPicker.SelectedIndex = 1; CardStylePicker.SelectedIndex = 0;
                HeaderPatternPicker.SelectedIndex = 4; RowStripingPicker.SelectedIndex = 1; VsSeparatorPicker.SelectedIndex = 2;
                FooterStylePicker.SelectedIndex = 2; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 1;
                break;
            case 17: // Vintage
                AccentColorPicker.SelectedIndex = 3; TitleStylePicker.SelectedIndex = 2; GridBorderPicker.SelectedIndex = 2;
                HomeBadgePicker.SelectedIndex = 1; FontFamilyPicker.SelectedIndex = 1; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 3; ColumnBandingPicker.SelectedIndex = 2; CardStylePicker.SelectedIndex = 0;
                HeaderPatternPicker.SelectedIndex = 0; RowStripingPicker.SelectedIndex = 2; VsSeparatorPicker.SelectedIndex = 0;
                FooterStylePicker.SelectedIndex = 0; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 1;
                break;
            case 18: // Frostbite
                AccentColorPicker.SelectedIndex = 5; TitleStylePicker.SelectedIndex = 5; GridBorderPicker.SelectedIndex = 0;
                HomeBadgePicker.SelectedIndex = 0; FontFamilyPicker.SelectedIndex = 0; SubtitleStylePicker.SelectedIndex = 1;
                MonthPalettePicker.SelectedIndex = 4; ColumnBandingPicker.SelectedIndex = 0; CardStylePicker.SelectedIndex = 1;
                HeaderPatternPicker.SelectedIndex = 1; RowStripingPicker.SelectedIndex = 0; VsSeparatorPicker.SelectedIndex = 3;
                FooterStylePicker.SelectedIndex = 1; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 0;
                break;
            case 19: // Warm Glow
                AccentColorPicker.SelectedIndex = 3; TitleStylePicker.SelectedIndex = 4; GridBorderPicker.SelectedIndex = 0;
                HomeBadgePicker.SelectedIndex = 0; FontFamilyPicker.SelectedIndex = 4; SubtitleStylePicker.SelectedIndex = 2;
                MonthPalettePicker.SelectedIndex = 5; ColumnBandingPicker.SelectedIndex = 1; CardStylePicker.SelectedIndex = 2;
                HeaderPatternPicker.SelectedIndex = 2; RowStripingPicker.SelectedIndex = 1; VsSeparatorPicker.SelectedIndex = 1;
                FooterStylePicker.SelectedIndex = 2; TitleUppercaseCheck.IsChecked = false; MonthUppercaseCheck.IsChecked = false;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 0;
                break;
            case 20: // Print Ready
                AccentColorPicker.SelectedIndex = 0; TitleStylePicker.SelectedIndex = 0; GridBorderPicker.SelectedIndex = 1;
                HomeBadgePicker.SelectedIndex = 1; FontFamilyPicker.SelectedIndex = 0; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 2; ColumnBandingPicker.SelectedIndex = 1; CardStylePicker.SelectedIndex = 0;
                HeaderPatternPicker.SelectedIndex = 4; RowStripingPicker.SelectedIndex = 1; VsSeparatorPicker.SelectedIndex = 0;
                FooterStylePicker.SelectedIndex = 2; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 1; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 1;
                break;
            case 21: // Friday Night
                AccentColorPicker.SelectedIndex = 7; TitleStylePicker.SelectedIndex = 5; GridBorderPicker.SelectedIndex = 1;
                HomeBadgePicker.SelectedIndex = 0; FontFamilyPicker.SelectedIndex = 3; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 1; ColumnBandingPicker.SelectedIndex = 1; CardStylePicker.SelectedIndex = 0;
                HeaderPatternPicker.SelectedIndex = 0; RowStripingPicker.SelectedIndex = 3; VsSeparatorPicker.SelectedIndex = 1;
                FooterStylePicker.SelectedIndex = 1; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 3; FontWeightPicker.SelectedIndex = 3;
                break;
            case 22: // Seaside
                AccentColorPicker.SelectedIndex = 6; TitleStylePicker.SelectedIndex = 1; GridBorderPicker.SelectedIndex = 0;
                HomeBadgePicker.SelectedIndex = 2; FontFamilyPicker.SelectedIndex = 11; SubtitleStylePicker.SelectedIndex = 2;
                MonthPalettePicker.SelectedIndex = 4; ColumnBandingPicker.SelectedIndex = 0; CardStylePicker.SelectedIndex = 3;
                HeaderPatternPicker.SelectedIndex = 3; RowStripingPicker.SelectedIndex = 0; VsSeparatorPicker.SelectedIndex = 2;
                FooterStylePicker.SelectedIndex = 3; TitleUppercaseCheck.IsChecked = false; MonthUppercaseCheck.IsChecked = false;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 0;
                break;
            case 23: // Dark Mode
                AccentColorPicker.SelectedIndex = 0; TitleStylePicker.SelectedIndex = 4; GridBorderPicker.SelectedIndex = 0;
                HomeBadgePicker.SelectedIndex = 3; FontFamilyPicker.SelectedIndex = 0; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 6; ColumnBandingPicker.SelectedIndex = 0; CardStylePicker.SelectedIndex = 1;
                HeaderPatternPicker.SelectedIndex = 1; RowStripingPicker.SelectedIndex = 0; VsSeparatorPicker.SelectedIndex = 3;
                FooterStylePicker.SelectedIndex = 1; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 1;
                break;
            case 24: // Pastel Dream
                AccentColorPicker.SelectedIndex = 5; TitleStylePicker.SelectedIndex = 3; GridBorderPicker.SelectedIndex = 0;
                HomeBadgePicker.SelectedIndex = 0; FontFamilyPicker.SelectedIndex = 4; SubtitleStylePicker.SelectedIndex = 2;
                MonthPalettePicker.SelectedIndex = 5; ColumnBandingPicker.SelectedIndex = 1; CardStylePicker.SelectedIndex = 2;
                HeaderPatternPicker.SelectedIndex = 3; RowStripingPicker.SelectedIndex = 1; VsSeparatorPicker.SelectedIndex = 2;
                FooterStylePicker.SelectedIndex = 3; TitleUppercaseCheck.IsChecked = false; MonthUppercaseCheck.IsChecked = false;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 0;
                break;
            case 25: // Matrix Master
                AccentColorPicker.SelectedIndex = 1; TitleStylePicker.SelectedIndex = 5; GridBorderPicker.SelectedIndex = 1;
                HomeBadgePicker.SelectedIndex = 0; FontFamilyPicker.SelectedIndex = 9; SubtitleStylePicker.SelectedIndex = 0;
                MonthPalettePicker.SelectedIndex = 1; ColumnBandingPicker.SelectedIndex = 1; CardStylePicker.SelectedIndex = 0;
                HeaderPatternPicker.SelectedIndex = 0; RowStripingPicker.SelectedIndex = 1; VsSeparatorPicker.SelectedIndex = 0;
                FooterStylePicker.SelectedIndex = 0; TitleUppercaseCheck.IsChecked = true; MonthUppercaseCheck.IsChecked = true;
                FontScalePicker.SelectedIndex = 2; TitleFontSizePicker.SelectedIndex = 1; FontWeightPicker.SelectedIndex = 2;
                break;
        }

        SetStatus($"Applied style: {StylePresetPicker.SelectedItem}");
    }

    private void OnSaveDesignClicked(object sender, EventArgs e)
    {
        var design = new SavedFixturesSheetDesign
        {
            AccentColorIndex = AccentColorPicker.SelectedIndex,
            TitleStyleIndex = TitleStylePicker.SelectedIndex,
            GridBordersIndex = GridBorderPicker.SelectedIndex,
            HomeBadgeIndex = HomeBadgePicker.SelectedIndex,
            FontFamilyIndex = FontFamilyPicker.SelectedIndex,
            FontScaleIndex = FontScalePicker.SelectedIndex,
            TitleFontSizeIndex = TitleFontSizePicker.SelectedIndex,
            FontWeightIndex = FontWeightPicker.SelectedIndex,
            SubtitleStyleIndex = SubtitleStylePicker.SelectedIndex,
            MonthPaletteIndex = MonthPalettePicker.SelectedIndex,
            ColumnBandingIndex = ColumnBandingPicker.SelectedIndex,
            DivisionLayoutIndex = DivisionLayoutPicker.SelectedIndex,
            TextDensityIndex = TextDensityPicker.SelectedIndex,
            CardStyleIndex = CardStylePicker.SelectedIndex,
            LayoutIndex = LayoutPicker.SelectedIndex,
            HeaderPatternIndex = HeaderPatternPicker.SelectedIndex,
            RowStripingIndex = RowStripingPicker.SelectedIndex,
            VsSeparatorIndex = VsSeparatorPicker.SelectedIndex,
            FooterStyleIndex = FooterStylePicker.SelectedIndex,
            CornerStyleIndex = CornerStylePicker.SelectedIndex,
            ShowMatchNight = ShowMatchNightCheck.IsChecked,
            TitleUppercase = TitleUppercaseCheck.IsChecked,
            MonthUppercase = MonthUppercaseCheck.IsChecked,
            ShowGridLegend = ShowGridLegendCheck.IsChecked,
            ShowTeamNumbers = ShowTeamNumbersCheck.IsChecked,
            ShowVenueInfo = ShowVenueInfoCheck.IsChecked,
            ShowDivisionLists = ShowDivisionListsCheck.IsChecked,
            IsLandscape = LandscapeRadio.IsChecked
        };

        League.WebsiteSettings.FixturesSheetDesign = design;

        // Save printable sheet embed settings
        League.WebsiteSettings.FixturesShowPrintableSheet = ShowPrintableSheetCheck.IsChecked;
        League.WebsiteSettings.FixturesSheetDefaultExpanded = SheetDefaultExpandedCheck.IsChecked;
        League.WebsiteSettings.FixturesSheetTitle = string.IsNullOrWhiteSpace(SheetTitleEntry.Text) 
            ? "Printable Fixtures Sheet" 
            : SheetTitleEntry.Text;

        // Also persist the full settings so the website generator uses the designed sheet
        if (SeasonPicker.SelectedItem is Season season)
        {
            League.FixturesSheetSettings = BuildCurrentSettings(season);
        }

        DataStore.Save();
        SetStatus("Design settings saved");
    }

    private void OnLoadDesignClicked(object sender, EventArgs e)
    {
        var design = League.WebsiteSettings.FixturesSheetDesign;
        if (design == null)
        {
            SetStatus("No saved design found");
            return;
        }

        ApplyDesign(design);
        SetStatus("Design settings loaded");
    }

    private void ApplyDesign(SavedFixturesSheetDesign design)
    {
        AccentColorPicker.SelectedIndex = Clamp(design.AccentColorIndex, 0, 7);
        TitleStylePicker.SelectedIndex = Clamp(design.TitleStyleIndex, 0, 5);
        GridBorderPicker.SelectedIndex = Clamp(design.GridBordersIndex, 0, 3);
        HomeBadgePicker.SelectedIndex = Clamp(design.HomeBadgeIndex, 0, 4);
        FontFamilyPicker.SelectedIndex = Clamp(design.FontFamilyIndex, 0, 11);
        FontScalePicker.SelectedIndex = Clamp(design.FontScaleIndex, 0, 5);
        TitleFontSizePicker.SelectedIndex = Clamp(design.TitleFontSizeIndex, 0, 4);
        FontWeightPicker.SelectedIndex = Clamp(design.FontWeightIndex, 0, 3);
        SubtitleStylePicker.SelectedIndex = Clamp(design.SubtitleStyleIndex, 0, 2);
        MonthPalettePicker.SelectedIndex = Clamp(design.MonthPaletteIndex, 0, 6);
        ColumnBandingPicker.SelectedIndex = Clamp(design.ColumnBandingIndex, 0, 3);
        DivisionLayoutPicker.SelectedIndex = Clamp(design.DivisionLayoutIndex, 0, 2);
        TextDensityPicker.SelectedIndex = Clamp(design.TextDensityIndex, 0, 2);
        CardStylePicker.SelectedIndex = Clamp(design.CardStyleIndex, 0, 4);
        LayoutPicker.SelectedIndex = Clamp(design.LayoutIndex, 0, 3);
        HeaderPatternPicker.SelectedIndex = Clamp(design.HeaderPatternIndex, 0, 4);
        RowStripingPicker.SelectedIndex = Clamp(design.RowStripingIndex, 0, 3);
        VsSeparatorPicker.SelectedIndex = Clamp(design.VsSeparatorIndex, 0, 4);
        FooterStylePicker.SelectedIndex = Clamp(design.FooterStyleIndex, 0, 3);
        CornerStylePicker.SelectedIndex = Clamp(design.CornerStyleIndex, 0, 2);
        ShowMatchNightCheck.IsChecked = design.ShowMatchNight;
        TitleUppercaseCheck.IsChecked = design.TitleUppercase;
        MonthUppercaseCheck.IsChecked = design.MonthUppercase;
        ShowGridLegendCheck.IsChecked = design.ShowGridLegend;
        ShowTeamNumbersCheck.IsChecked = design.ShowTeamNumbers;
        ShowVenueInfoCheck.IsChecked = design.ShowVenueInfo;
        ShowDivisionListsCheck.IsChecked = design.ShowDivisionLists;
        LandscapeRadio.IsChecked = design.IsLandscape;
        PortraitRadio.IsChecked = !design.IsLandscape;
    }

    private static int Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;

    #endregion

    private async void OnPreviewClicked(object sender, EventArgs e)
    {
        try
        {
            _generatedHtml = GenerateSheet();
            if (_generatedHtml == null) return;

            Helpers.WebViewHelper.LoadHtml(PreviewWebView, _generatedHtml);
            PreviewPlaceholder.IsVisible = false;
            PreviewWebView.IsVisible = true;

            SetStatus("Preview generated");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to generate preview: {ex.Message}", "OK");
        }
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            _generatedHtml = GenerateSheet();
            if (_generatedHtml == null) return;
            
            var season = SeasonPicker.SelectedItem as Season;
            var fileName = $"fixtures-sheet-{season?.Name?.Replace(" ", "-") ?? "export"}.html";
            
            // Save to a file
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, _generatedHtml);
            
            // Share the file
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export Fixtures Sheet",
                File = new ShareFile(filePath)
            });
            
            SetStatus($"Exported: {fileName}");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to export: {ex.Message}", "OK");
        }
    }

    private async void OnPrintClicked(object sender, EventArgs e)
    {
        try
        {
            _generatedHtml = GenerateSheet();
            if (_generatedHtml == null) return;
            
            // For printing, we'll export and let the user print from their browser/app
            var season = SeasonPicker.SelectedItem as Season;
            var fileName = $"fixtures-sheet-{season?.Name?.Replace(" ", "-") ?? "print"}.html";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, _generatedHtml);
            
            // Open the file in the default browser for printing
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                Title = "Print Fixtures Sheet",
                File = new ReadOnlyFile(filePath)
            });
            
            SetStatus("Opened for printing");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to prepare for printing: {ex.Message}", "OK");
        }
    }

    private FixturesSheetSettings BuildCurrentSettings(Season season)
    {
        // Parse logo dimensions
        int logoWidth = 100;
        int logoHeight = 60;
        if (int.TryParse(LogoWidthEntry.Text, out int parsedWidth) && parsedWidth > 0)
            logoWidth = parsedWidth;
        if (int.TryParse(LogoHeightEntry.Text, out int parsedHeight) && parsedHeight >= 0)
            logoHeight = parsedHeight;

        var settings = new FixturesSheetSettings
        {
            LeagueName = LeagueNameEntry.Text ?? "Pool League",
            SeasonName = SeasonTitleEntry.Text ?? season.Name,
            Subtitle = SubtitleEntry.Text ?? "",
            Orientation = LandscapeRadio.IsChecked ? PageOrientation.Landscape : PageOrientation.Portrait,
            ShowTeamNumbers = ShowTeamNumbersCheck.IsChecked,
            ShowVenueInfo = ShowVenueInfoCheck.IsChecked,
            ShowDivisionTeamLists = ShowDivisionListsCheck.IsChecked,
            WebsiteUrl = WebsiteUrlEntry.Text ?? "",
            EmailAddress = EmailEntry.Text ?? "",
            CancelledMatchContact = CancelledMatchContactEntry.Text ?? "",
            CancelledCompetitionContact = CancelledCompContactEntry.Text ?? "",

            // Logo settings
            ShowLeagueLogo = ShowLogoCheck.IsChecked && _currentLogoData != null,
            LogoImageData = _currentLogoData,
            LogoPosition = GetSelectedLogoPosition(),
            LogoWidth = logoWidth,
            LogoHeight = logoHeight,
            LogoMaintainAspectRatio = MaintainAspectRatioCheck.IsChecked,

            // Design settings
            AccentColor = GetSelectedAccentColor(),
            TitleStyle = GetSelectedTitleStyle(),
            GridBorders = GetSelectedGridBorders(),
            HomeBadge = GetSelectedHomeBadge(),
            FontFamily = GetSelectedFontFamily(),
            FontScale = GetSelectedFontScale(),
            TitleFontSize = GetSelectedTitleFontSize(),
            FontWeight = GetSelectedFontWeight(),
            ShowMatchNight = ShowMatchNightCheck.IsChecked,
            MonthColors = GetSelectedMonthPalette(),
            ColumnBanding = GetSelectedColumnBanding(),
            SubtitleStyle = GetSelectedSubtitleStyle(),
            DivisionLayout = GetSelectedDivisionLayout(),
            TextDensity = GetSelectedTextDensity(),
            TitleUppercase = TitleUppercaseCheck.IsChecked,
            MonthUppercase = MonthUppercaseCheck.IsChecked,
            ShowGridLegend = ShowGridLegendCheck.IsChecked,
            CardStyle = GetSelectedCardStyle(),
            Layout = GetSelectedLayout(),
            HeaderPattern = GetSelectedHeaderPattern(),
            RowStriping = GetSelectedRowStriping(),
            VsSeparator = GetSelectedVsSeparator(),
            FooterStyle = GetSelectedFooterStyle(),
            CornerStyle = GetSelectedCornerStyle()
        };

        // Add special events
        foreach (var evt in _events)
        {
            settings.SpecialEvents.Add(new SpecialEvent
            {
                Date = evt.Date,
                DayOfWeek = evt.Date.ToString("dddd"),
                Description = evt.Description,
                Color = evt.Color
            });
        }

        // Add venue phone numbers
        foreach (var phone in _venuePhones)
        {
            settings.VenuePhoneNumbers[phone.VenueName] = phone.PhoneNumber;
        }

        // Add footer notes
        if (!string.IsNullOrWhiteSpace(FooterNotesEditor.Text))
        {
            var notes = FooterNotesEditor.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            settings.FooterNotes.AddRange(notes.Select(n => n.Trim()));
        }

        return settings;
    }

    private string? GenerateSheet()
    {
        var season = SeasonPicker.SelectedItem as Season;
        if (season == null)
        {
            DisplayAlert("Error", "Please select a season", "OK");
            return null;
        }

        var selectedDivisions = _divisions.Where(d => d.IsSelected).Select(d => d.Id).ToList();
        if (selectedDivisions.Count == 0)
        {
            DisplayAlert("Error", "Please select at least one division", "OK");
            return null;
        }

        var settings = BuildCurrentSettings(season);

        // Persist settings so the website generator uses the same design
        League.FixturesSheetSettings = settings;
        DataStore.Save();

        var generator = new FixturesSheetGenerator(League, settings);
        return generator.GenerateFixturesSheet(season.Id, selectedDivisions);
    }

    private async void OnAddEventClicked(object sender, EventArgs e)
    {
        var datePicker = new DatePicker { Date = DateTime.Today };
        var descEntry = new Entry { Placeholder = "Event description (e.g. Team K.O. First Round)" };
        
        var content = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Date:" },
                datePicker,
                new Label { Text = "Description:" },
                descEntry
            }
        };
        
        var result = await DisplayAlert("Add Special Event", "Enter event details", "Add", "Cancel");
        
        // Simple approach - just add with default values for demo
        // In production, you'd use a proper dialog
        var description = await DisplayPromptAsync("Add Event", "Enter event description:", 
            placeholder: "Team K.O. First Round");
        
        if (!string.IsNullOrWhiteSpace(description))
        {
            _events.Add(new SpecialEventItem
            {
                Date = DateTime.Today,
                Description = description,
                Color = "#FFE4B5"
            });
        }
    }

    private void OnRemoveEventClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is SpecialEventItem item)
        {
            _events.Remove(item);
        }
    }

    private async void OnAddVenuePhoneClicked(object sender, EventArgs e)
    {
        var venueName = await DisplayPromptAsync("Add Venue Phone", "Venue name:");
        if (string.IsNullOrWhiteSpace(venueName)) return;
        
        var phoneNumber = await DisplayPromptAsync("Add Venue Phone", "Phone number:");
        if (string.IsNullOrWhiteSpace(phoneNumber)) return;
        
        _venuePhones.Add(new VenuePhoneItem
        {
            VenueName = venueName,
            PhoneNumber = phoneNumber
        });
    }

    private void OnRemoveVenuePhoneClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is VenuePhoneItem item)
        {
            _venuePhones.Remove(item);
        }
    }

    private void SetStatus(string message)
    {
        StatusLabel.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
        StatusLabel.IsVisible = true;
    }
}

// Helper classes
public class DivisionSelection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsSelected { get; set; }
}

public class SpecialEventItem
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public string Color { get; set; } = "#FFE4B5";
    public string DateDisplay => Date.ToString("ddd d MMM");
}

public class VenuePhoneItem
{
    public string VenueName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
}
