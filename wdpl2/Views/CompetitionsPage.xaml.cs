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
    private Competition? _selectedCompetition => _viewModel.SelectedCompetition;
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
        
        _dataStore = new DataStoreService();
        
        if (viewModel == null)
        {
            _viewModel = new CompetitionsViewModel(_dataStore);
        }
        else
        {
            _viewModel = viewModel;
        }
        
        BindingContext = _viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Cleanup();
    }

    private void OnCompetitionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var competition = e.CurrentSelection?.FirstOrDefault() as Competition;
        
        if (competition == null)
        {
            _editorViewModel = null;
            ShowEmptyState();
        }
        else
        {
            ShowCompetitionEditor(competition);
        }
    }

    private async void OnNewCompetition(object? sender, EventArgs e)
    {
        if (!_viewModel.CurrentSeasonId.HasValue)
        {
            SetStatus("Please select a season first");
            return;
        }

        var wizard = new CompetitionWizardPage(_viewModel.CurrentSeasonId.Value);
        await Navigation.PushModalAsync(new NavigationPage(wizard));
        
        var competition = await wizard.GetResultAsync();
        
        if (competition != null)
        {
            await _viewModel.CreateCompetitionCommand.ExecuteAsync(competition);
        }
    }

    private void OnShowActive(object? sender, EventArgs e)
    {
        _viewModel.ShowHistory = false;
        CompetitionsList.ItemsSource = _viewModel.ActiveCompetitions;
        ActiveTab.BackgroundColor = Color.FromArgb("#3B82F6");
        HistoryTab.BackgroundColor = Color.FromArgb("#6B7280");
        NewBtn.IsVisible = true;
    }

    private void OnShowHistory(object? sender, EventArgs e)
    {
        _viewModel.ShowHistory = true;
        CompetitionsList.ItemsSource = _viewModel.CompletedCompetitions;
        ActiveTab.BackgroundColor = Color.FromArgb("#6B7280");
        HistoryTab.BackgroundColor = Color.FromArgb("#3B82F6");
        NewBtn.IsVisible = false;
    }

    private void ShowEmptyState()
    {
        ContentPanel.Content = new VerticalStackLayout
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
        };
    }

    internal void SetStatus(string text)
    {
        _viewModel.StatusMessage = $"{DateTime.Now:HH:mm:ss}  {text}";
    }
}
