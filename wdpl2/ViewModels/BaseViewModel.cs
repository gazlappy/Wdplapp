using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Wdpl2.Services;

namespace Wdpl2.ViewModels;

/// <summary>
/// Base ViewModel with common functionality for all page ViewModels
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    protected bool _isLoading;
    
    [ObservableProperty]
    protected string _statusMessage = "";
    
    [ObservableProperty]
    protected Guid? _currentSeasonId;

    protected void SetStatus(string message)
    {
        StatusMessage = $"{DateTime.Now:HH:mm:ss}  {message}";
    }

    protected virtual void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        CurrentSeasonId = e.NewSeasonId;
        SetStatus($"Season: {e.NewSeason?.Name ?? "None"}");
    }

    public virtual void Cleanup()
    {
        SeasonService.SeasonChanged -= OnSeasonChanged;
    }
}
