using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.ViewModels;

namespace Wdpl2.Views;

/// <summary>
/// Main competition management page - uses MVVM pattern with CompetitionEditorViewModel
/// </summary>
public partial class CompetitionsPage : ContentPage
{
    private readonly CompetitionsViewModel _viewModel;
    private readonly IDataStore _dataStore;
    internal CompetitionEditorViewModel? _editorViewModel;

    // Keep these for backward compatibility with existing partial classes
    private Competition? _selectedCompetition => _editorViewModel?.Competition ?? _viewModel.SelectedCompetition;
    private Guid? _currentSeasonId => _viewModel.CurrentSeasonId;
    
    // UI Elements for editor (shared across partials)
    internal Entry? _nameEntry;
    internal Picker? _statusPicker;
    internal DatePicker? _startDatePicker;
    internal Entry? _notesEntry;
    internal CollectionView? _participantsView;

    // Default constructor for Shell navigation
    public CompetitionsPage() : this(null)
    {
    }

    // Constructor with DI
    public CompetitionsPage(CompetitionsViewModel? viewModel)
    {
        InitializeComponent();

        if (viewModel == null)
        {
            // Resolve IDataStore from DI when no ViewModel is provided
            var dataStore = Application.Current?.Handler?.MauiContext?.Services.GetService<IDataStore>()
                ?? throw new InvalidOperationException("IDataStore not registered");
            var seasonService = Application.Current?.Handler?.MauiContext?.Services.GetService<ISeasonService>()
                ?? SeasonService.Current;
            _dataStore = dataStore;
            _viewModel = new CompetitionsViewModel(_dataStore, seasonService);
        }
        else
        {
            _viewModel = viewModel;
            _dataStore = _viewModel.DataStore;
        }

        BindingContext = _viewModel;

        // React when the ViewModel finishes reloading (e.g. after season change)
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdateSeasonLabel();

        try
        {
            RefreshList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CompetitionsPage RefreshList error: {ex.Message}");
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // When the ViewModel finishes loading, rebuild the list
        if (e.PropertyName == nameof(CompetitionsViewModel.IsLoading) && !_viewModel.IsLoading)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    RefreshList();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CompetitionsPage season-refresh error: {ex.Message}");
                }
            });
        }
        // When season changes, update the label and clear the editor
        else if (e.PropertyName == nameof(CompetitionsViewModel.CurrentSeasonId))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateSeasonLabel();
                _editorViewModel = null;
                ShowEmptyState();
            });
        }
    }

    private void UpdateSeasonLabel()
    {
        var season = SeasonService.Current.GetCurrentSeason();
        SeasonLabel.Text = season != null
            ? $"Season: {season.Name}"
            : "No season selected";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateSeasonLabel();
        try
        {
            RefreshList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CompetitionsPage OnAppearing error: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Cleanup();
    }

    private void OnCompetitionTapped(Competition competition)
    {
        _viewModel.SelectedCompetition = competition;
        ShowCompetitionEditor(competition);
        RefreshList(); // Update selection highlight
    }

    private async void OnNewCompetition(object? sender, EventArgs e)
    {
        if (!_viewModel.CurrentSeasonId.HasValue)
        {
            SetStatus("Please select a season first");
            return;
        }

        if (DataStore.Data.IsSeasonLocked(_viewModel.CurrentSeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot create competitions — this season is locked.", "OK");
            return;
        }

        try
        {
            var wizard = new CompetitionWizardPage(_viewModel.CurrentSeasonId.Value);
            await Navigation.PushModalAsync(new NavigationPage(wizard));
            
            var competition = await wizard.GetResultAsync();
            
            if (competition != null)
            {
                await _viewModel.CreateCompetitionCommand.ExecuteAsync(competition);
                RefreshList();
                ShowCompetitionEditor(competition);
                SetStatus($"Created: {competition.Name}");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
    }

    private void OnShowActive(object? sender, EventArgs e)
    {
        _viewModel.ShowHistory = false;
        RefreshList();
        ActiveTab.BackgroundColor = Color.FromArgb("#3B82F6");
        HistoryTab.BackgroundColor = Color.FromArgb("#6B7280");
        NewBtn.IsVisible = true;
    }

    private async void OnDeleteCompetition(object? sender, EventArgs e)
    {
        var competition = _editorViewModel?.Competition ?? _viewModel.SelectedCompetition;
        if (competition == null)
        {
            SetStatus("No competition selected");
            return;
        }

        if (DataStore.Data.IsSeasonLocked(competition.SeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot delete competitions — this season is locked.", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Delete Competition",
            $"Are you sure you want to delete \"{competition.Name}\"?\n\nThis will permanently remove all groups, rounds, and results. This cannot be undone.",
            "Delete",
            "Cancel");

        if (!confirm) return;

        await _viewModel.DeleteCompetitionCommand.ExecuteAsync(competition);
        _editorViewModel = null;
        ShowEmptyState();
        RefreshList();
    }

    private void OnShowHistory(object? sender, EventArgs e)
    {
        _viewModel.ShowHistory = true;
        RefreshList();
        ActiveTab.BackgroundColor = Color.FromArgb("#6B7280");
        HistoryTab.BackgroundColor = Color.FromArgb("#3B82F6");
        NewBtn.IsVisible = false;
    }

    /// <summary>
    /// Rebuild the competition list from scratch.
    /// Uses programmatic UI instead of CollectionView which is unreliable on Windows MAUI.
    /// </summary>
    private void RefreshList()
    {
        CompetitionsList.Children.Clear();

        var source = _viewModel.ShowHistory
            ? _viewModel.CompletedCompetitions
            : _viewModel.ActiveCompetitions;

        if (source == null || source.Count == 0)
        {
            CompetitionsList.Children.Add(new VerticalStackLayout
            {
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Spacing = 8,
                Padding = new Thickness(20),
                Children =
                {
                    new Label { Text = "No competitions yet", FontSize = 15, TextColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center },
                    new Label { Text = "Tap 'New' to create one", FontSize = 13, TextColor = Colors.Gray, HorizontalTextAlignment = TextAlignment.Center }
                }
            });
            return;
        }

        var selected = _editorViewModel?.Competition;
        foreach (var comp in source)
        {
            var isSelected = selected != null && comp.Id == selected.Id;
            var frame = new Border
            {
                Padding = new Thickness(12, 10),
                Margin = new Thickness(0, 2),
                BackgroundColor = isSelected ? Color.FromArgb("#DBEAFE") : Color.FromArgb("#FFFFFF"),
                Stroke = isSelected ? Color.FromArgb("#3B82F6") : Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                Content = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                    ColumnSpacing = 8,
                    Children =
                    {
                        new VerticalStackLayout
                        {
                            Children =
                            {
                                new Label { Text = comp.Name, FontSize = 15, FontAttributes = FontAttributes.Bold },
                                new Label { Text = comp.Format.ToString(), FontSize = 12, TextColor = Colors.Gray },
                                new Label { Text = comp.Status.ToString(), FontSize = 11, TextColor = Colors.Gray }
                            }
                        }
                    }
                }
            };

            var countLabel = new Label
            {
                Text = $"{comp.ParticipantIds?.Count ?? 0} entries",
                FontSize = 12,
                VerticalTextAlignment = TextAlignment.Center,
                TextColor = Colors.Gray
            };
            Grid.SetColumn(countLabel, 1);
            ((Grid)frame.Content).Children.Add(countLabel);

            var tap = new TapGestureRecognizer();
            var captured = comp; // capture for closure
            tap.Tapped += (_, _) => OnCompetitionTapped(captured);
            frame.GestureRecognizers.Add(tap);

            CompetitionsList.Children.Add(frame);
        }
    }

    private void ShowEmptyState()
    {
        SetContentPanel(new VerticalStackLayout
        {
            Spacing = 16,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "Select a competition to view details",
                    FontSize = 16,
                    TextColor = Colors.Gray,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new Label
                {
                    Text = "or create a new competition",
                    FontSize = 14,
                    TextColor = Colors.Gray,
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        });
    }

    /// <summary>
    /// Replace the right-side editor content.
    /// </summary>
    internal void SetContentPanel(View content)
    {
        ContentPanel.Children.Clear();
        ContentPanel.Children.Add(content);
    }

    internal void SetStatus(string text)
    {
        _viewModel.StatusMessage = $"{DateTime.Now:HH:mm:ss}  {text}";
    }
}
