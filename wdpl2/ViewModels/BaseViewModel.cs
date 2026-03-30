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
    private CancellationTokenSource? _loadCts;

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
    /// Cancel any in-progress load and return a fresh <see cref="CancellationToken"/>.
    /// Called automatically by <see cref="OnSeasonChanged"/>; can also be called
    /// manually from <c>InitializeAsync</c> or relay-command load methods.
    /// </summary>
    protected CancellationToken ResetLoadToken()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        return _loadCts.Token;
    }

    /// <summary>
    /// The current load cancellation token. Use in datastore calls so a season
    /// switch cancels stale queries instead of letting them populate the UI.
    /// </summary>
    protected CancellationToken LoadToken => _loadCts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Safely run an async initialization task, catching and surfacing errors via StatusMessage.
    /// Use this instead of <c>_ = InitializeAsync()</c> to avoid silently swallowed exceptions.
    /// <see cref="OperationCanceledException"/> is silently ignored (expected on season switch).
    /// </summary>
    protected async void SafeFireAndForget(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            // Expected when a new load supersedes the previous one — nothing to report.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{GetType().Name} init error: {ex.Message}");
            SetStatus($"Error: {ex.Message}");
        }
    }

    protected virtual void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        ResetLoadToken();
        CurrentSeasonId = e.NewSeasonId;
        SetStatus($"Season: {e.NewSeason?.Name ?? "None"}");
    }

    public virtual void Cleanup()
    {
        _seasonService.SeasonChanged -= OnSeasonChanged;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }
}
