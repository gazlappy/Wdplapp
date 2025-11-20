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
/// Main competition management page - now using MVVM pattern
/// </summary>
public partial class CompetitionsPage : ContentPage
{
    private readonly CompetitionsViewModel _viewModel;

    // Keep these for backward compatibility with existing partial classes
    private Competition? _selectedCompetition => _viewModel.SelectedCompetition;
    private Guid? _currentSeasonId => _viewModel.CurrentSeasonId;
    
    // UI Elements for editor (shared across partials) - still needed for existing UI generation code
    internal Entry? _nameEntry;
    internal Picker? _statusPicker;
    internal DatePicker? _startDatePicker;
    internal Entry? _notesEntry;
    internal CollectionView? _participantsView;
    internal ObservableCollection<ParticipantItem> _participants = new();

    // Default constructor for Shell navigation
    public CompetitionsPage() : this(null)
    {
    }

    // Constructor with DI
    public CompetitionsPage(CompetitionsViewModel? viewModel)
    {
        InitializeComponent();
        
        // If no ViewModel provided (Shell navigation), create one manually
        if (viewModel == null)
        {
            var dataStore = new DataStoreService();
            _viewModel = new CompetitionsViewModel(dataStore);
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

        // Show setup dialog
        var setupDialog = new CompetitionSetupDialog(_viewModel.CurrentSeasonId.Value);
        await Navigation.PushModalAsync(new NavigationPage(setupDialog));
        
        var competition = await setupDialog.GetResultAsync();
        
        if (competition != null)
        {
            await _viewModel.CreateCompetitionCommand.ExecuteAsync(competition);
        }
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

    // Helper class for participant display (still needed by existing partial classes)
    internal class ParticipantItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
    }
}
