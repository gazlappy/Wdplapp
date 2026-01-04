using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class LayoutSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public LayoutSettingsPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = League.WebsiteSettings;
        
        EnableAnimationsCheck.IsChecked = settings.EnableAnimations;
        EnableGradientsCheck.IsChecked = settings.EnableGradients;
        EnableShadowsCheck.IsChecked = settings.EnableShadows;
        EnableRoundedCornersCheck.IsChecked = settings.EnableRoundedCorners;
        ShowLastUpdatedCheck.IsChecked = settings.ShowLastUpdated;
        
        SetPickerValue(FontFamilyPicker, settings.FontFamily);
        SetPickerValue(HeaderFontFamilyPicker, settings.HeaderFontFamily);
        BaseFontSizeEntry.Text = settings.BaseFontSize.ToString();
        BorderRadiusEntry.Text = settings.BorderRadius.ToString();
        
        SetPickerValue(HeaderStylePicker, settings.HeaderStyle);
        SetPickerValue(HeaderAlignmentPicker, settings.HeaderAlignment);
        ShowHeaderPatternCheck.IsChecked = settings.ShowHeaderPattern;
        ShowSeasonBadgeCheck.IsChecked = settings.ShowSeasonBadge;
        
        SetPickerValue(NavStylePicker, settings.NavStyle);
        SetPickerValue(NavPositionPicker, settings.NavPosition);
        NavStickyCheck.IsChecked = settings.NavSticky;
        ShowNavIconsCheck.IsChecked = settings.ShowNavIcons;
        
        SetPickerValue(FooterStylePicker, settings.FooterStyle);
        ShowFooterSocialLinksCheck.IsChecked = settings.ShowFooterSocialLinks;
        ShowFooterContactCheck.IsChecked = settings.ShowFooterContact;
        ShowPoweredByCheck.IsChecked = settings.ShowPoweredBy;
        CustomFooterTextEntry.Text = settings.CustomFooterText;
        CopyrightTextEntry.Text = settings.CopyrightText;
        
        TableStripedCheck.IsChecked = settings.TableStriped;
        TableHoverableCheck.IsChecked = settings.TableHoverable;
        TableBorderedCheck.IsChecked = settings.TableBordered;
        TableCompactCheck.IsChecked = settings.TableCompact;
        SetPickerValue(TableHeaderStylePicker, settings.TableHeaderStyle);
        SetPickerValue(CardStylePicker, settings.CardStyle);
        SetPickerValue(CardAccentPositionPicker, settings.CardAccentPosition);
        CardShowTopAccentCheck.IsChecked = settings.CardShowTopAccent;
    }

    private void SetPickerValue(Picker picker, string value)
    {
        if (picker.ItemsSource is IList<string> items)
        {
            var index = items.IndexOf(value);
            if (index >= 0) picker.SelectedIndex = index;
        }
    }

    private string GetPickerValue(Picker picker, string defaultValue)
        => picker.SelectedItem?.ToString() ?? defaultValue;

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;
            
            settings.EnableAnimations = EnableAnimationsCheck.IsChecked;
            settings.EnableGradients = EnableGradientsCheck.IsChecked;
            settings.EnableShadows = EnableShadowsCheck.IsChecked;
            settings.EnableRoundedCorners = EnableRoundedCornersCheck.IsChecked;
            settings.ShowLastUpdated = ShowLastUpdatedCheck.IsChecked;
            
            settings.FontFamily = GetPickerValue(FontFamilyPicker, "Inter");
            settings.HeaderFontFamily = GetPickerValue(HeaderFontFamilyPicker, "Inter");
            if (int.TryParse(BaseFontSizeEntry.Text, out int fontSize))
                settings.BaseFontSize = fontSize;
            if (int.TryParse(BorderRadiusEntry.Text, out int borderRadius))
                settings.BorderRadius = borderRadius;
            
            settings.HeaderStyle = GetPickerValue(HeaderStylePicker, "gradient");
            settings.HeaderAlignment = GetPickerValue(HeaderAlignmentPicker, "center");
            settings.ShowHeaderPattern = ShowHeaderPatternCheck.IsChecked;
            settings.ShowSeasonBadge = ShowSeasonBadgeCheck.IsChecked;
            
            settings.NavStyle = GetPickerValue(NavStylePicker, "pills");
            settings.NavPosition = GetPickerValue(NavPositionPicker, "center");
            settings.NavSticky = NavStickyCheck.IsChecked;
            settings.ShowNavIcons = ShowNavIconsCheck.IsChecked;
            
            settings.FooterStyle = GetPickerValue(FooterStylePicker, "dark");
            settings.ShowFooterSocialLinks = ShowFooterSocialLinksCheck.IsChecked;
            settings.ShowFooterContact = ShowFooterContactCheck.IsChecked;
            settings.ShowPoweredBy = ShowPoweredByCheck.IsChecked;
            settings.CustomFooterText = CustomFooterTextEntry.Text?.Trim() ?? "";
            settings.CopyrightText = CopyrightTextEntry.Text?.Trim() ?? "";
            
            settings.TableStriped = TableStripedCheck.IsChecked;
            settings.TableHoverable = TableHoverableCheck.IsChecked;
            settings.TableBordered = TableBorderedCheck.IsChecked;
            settings.TableCompact = TableCompactCheck.IsChecked;
            settings.TableHeaderStyle = GetPickerValue(TableHeaderStylePicker, "gradient");
            settings.CardStyle = GetPickerValue(CardStylePicker, "elevated");
            settings.CardAccentPosition = GetPickerValue(CardAccentPositionPicker, "top");
            settings.CardShowTopAccent = CardShowTopAccentCheck.IsChecked;
            
            DataStore.Save();
            
            await DisplayAlert("Saved", "Layout settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
