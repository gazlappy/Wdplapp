using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Wdpl2.Services;

namespace Wdpl2.ViewModels;

/// <summary>
/// Base ViewModel with common functionality for all page ViewModels
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    protected readonly ISeasonService _seasonService;

    [ObservableProperty]
    protected bool _isLoading;

    [ObservableProperty]
    protected string _statusMessage = "";

    [ObservableProperty]
    protected Guid? _currentSeasonId;

    protected BaseViewModel(ISeasonService seasonService)
    {
        _seasonService = seasonService;
    }

    protected void SetStatus(string message)
    {
        StatusMessage = $"{DateTime.Now:HH:mm:ss}  {message}";
    }

    /// <summary>
    /// Safely run an async initialization task, catching and surfacing errors via StatusMessage.
    /// Use this instead of <c>_ = InitializeAsync()</c> to avoid silently swallowed exceptions.
    /// </summary>
    protected async void SafeFireAndForget(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{GetType().Name} init error: {ex.Message}");
            SetStatus($"Error: {ex.Message}");
        }
    }

    protected virtual void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        CurrentSeasonId = e.NewSeasonId;
        SetStatus($"Season: {e.NewSeason?.Name ?? "None"}");
    }

    public virtual void Cleanup()
    {
        _seasonService.SeasonChanged -= OnSeasonChanged;
    }
}
