using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.ViewModels;

/// <summary>
/// ViewModel for SettingsPage - manages app settings
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly IDataStore _dataStore;
    
    [ObservableProperty]
    private string _statusMessage = "";
    
    [ObservableProperty]
    private bool _darkMode;
    
    [ObservableProperty]
    private string _defaultVenue = "";
    
    [ObservableProperty]
    private int _framesPerMatch = 8;
    
    [ObservableProperty]
    private bool _autoSave = true;

    public SettingsViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _dataStore.GetData().Settings;
        if (settings != null)
        {
            // Load settings from data store if they exist
            _framesPerMatch = 8; // Default value
            _autoSave = true;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            // Save settings logic here
            await _dataStore.SaveAsync();
            SetStatus("Settings saved");
        }
        catch (System.Exception ex)
        {
            SetStatus($"Error saving settings: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        _darkMode = false;
        _defaultVenue = "";
        _framesPerMatch = 8;
        _autoSave = true;
        
        await SaveSettingsAsync();
        SetStatus("Settings reset to defaults");
    }

    [RelayCommand]
    private async Task ClearAllDataAsync()
    {
        // This would need confirmation dialog in the view
        SetStatus("Clear all data requested");
    }

    private void SetStatus(string message)
    {
        _statusMessage = $"{System.DateTime.Now:HH:mm:ss}  {message}";
    }
}
