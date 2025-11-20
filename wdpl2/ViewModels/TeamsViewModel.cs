using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.ViewModels;

/// <summary>
/// ViewModel for TeamsPage - manages team list and CRUD operations
/// </summary>
public partial class TeamsViewModel : BaseViewModel
{
    private readonly IDataStore _dataStore;
    
    [ObservableProperty]
    private ObservableCollection<Team> _teams = new();
    
    [ObservableProperty]
    private Team? _selectedTeam;
    
    [ObservableProperty]
    private string _searchText = "";
    
    [ObservableProperty]
    private string _teamName = "";
    
    [ObservableProperty]
    private string _captain = "";
    
    [ObservableProperty]
    private Guid? _captainPlayerId;
    
    [ObservableProperty]
    private ObservableCollection<Player> _availablePlayers = new();
    
    [ObservableProperty]
    private ObservableCollection<Venue> _availableVenues = new();
    
    [ObservableProperty]
    private bool _isMultiSelectMode;

    public TeamsViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
        SeasonService.SeasonChanged += OnSeasonChanged;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        _currentSeasonId = SeasonService.CurrentSeasonId;
        await LoadTeamsAsync();
        await LoadReferenceDataAsync();
    }

    protected override void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        base.OnSeasonChanged(sender, e);
        _ = LoadTeamsAsync();
        _ = LoadReferenceDataAsync();
    }

    [RelayCommand]
    private async Task LoadTeamsAsync()
    {
        _isLoading = true;
        
        try
        {
            if (!_currentSeasonId.HasValue)
            {
                SetStatus("No season selected");
                _teams.Clear();
                return;
            }

            var allTeams = await _dataStore.GetTeamsAsync(_currentSeasonId);

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var lower = _searchText.ToLower();
                allTeams = allTeams
                    .Where(t => (t.Name ?? "").ToLower().Contains(lower))
                    .ToList();
            }

            _teams.Clear();
            foreach (var team in allTeams)
                _teams.Add(team);

            SetStatus($"{_teams.Count} team(s)");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading teams: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadReferenceDataAsync()
    {
        if (!_currentSeasonId.HasValue) return;

        var players = await _dataStore.GetPlayersAsync(_currentSeasonId);
        _availablePlayers.Clear();
        foreach (var player in players)
            _availablePlayers.Add(player);

        var venues = await _dataStore.GetVenuesAsync(_currentSeasonId);
        _availableVenues.Clear();
        foreach (var venue in venues)
            _availableVenues.Add(venue);
    }

    [RelayCommand]
    private async Task SearchTeamsAsync(string? searchText)
    {
        _searchText = searchText ?? "";
        await LoadTeamsAsync();
    }

    [RelayCommand]
    private async Task AddTeamAsync()
    {
        if (string.IsNullOrWhiteSpace(_teamName))
        {
            SetStatus("Team name required");
            return;
        }

        if (!_currentSeasonId.HasValue)
        {
            SetStatus("Please select a season first");
            return;
        }

        var team = new Team
        {
            SeasonId = _currentSeasonId.Value,
            Name = _teamName.Trim(),
            Captain = _captain?.Trim(),
            CaptainPlayerId = _captainPlayerId
        };

        await _dataStore.AddTeamAsync(team);
        await _dataStore.SaveAsync();
        await LoadTeamsAsync();

        ClearEditor();
        SetStatus($"Added: {team.Name}");
    }

    [RelayCommand]
    private async Task UpdateTeamAsync()
    {
        if (_selectedTeam == null)
        {
            SetStatus("No team selected");
            return;
        }

        _selectedTeam.Name = _teamName?.Trim() ?? "";
        _selectedTeam.Captain = _captain?.Trim();
        _selectedTeam.CaptainPlayerId = _captainPlayerId;

        await _dataStore.UpdateTeamAsync(_selectedTeam);
        await _dataStore.SaveAsync();
        await LoadTeamsAsync();

        SetStatus($"Updated: {_selectedTeam.Name}");
    }

    [RelayCommand]
    private async Task DeleteTeamAsync(Team? team)
    {
        if (team == null)
        {
            SetStatus("No team selected");
            return;
        }

        await _dataStore.DeleteTeamAsync(team);
        await _dataStore.SaveAsync();
        await LoadTeamsAsync();

        ClearEditor();
        SetStatus("Deleted team");
    }

    [RelayCommand]
    private void ToggleMultiSelect()
    {
        _isMultiSelectMode = !_isMultiSelectMode;
        SetStatus(_isMultiSelectMode ? "Multi-select enabled" : "Multi-select disabled");
    }

    [RelayCommand]
    private async Task BulkDeleteAsync(System.Collections.Generic.List<Team>? teams)
    {
        if (teams == null || teams.Count == 0)
        {
            SetStatus("No teams selected");
            return;
        }

        foreach (var team in teams)
        {
            await _dataStore.DeleteTeamAsync(team);
        }

        await _dataStore.SaveAsync();
        await LoadTeamsAsync();
        SetStatus($"Deleted {teams.Count} team(s)");
    }

    partial void OnSelectedTeamChanged(Team? value)
    {
        if (value == null)
        {
            ClearEditor();
        }
        else
        {
            LoadEditor(value);
        }
    }

    private void LoadEditor(Team team)
    {
        _teamName = team.Name ?? "";
        _captain = team.Captain ?? "";
        _captainPlayerId = team.CaptainPlayerId;
    }

    private void ClearEditor()
    {
        _teamName = "";
        _captain = "";
        _captainPlayerId = null;
    }
}
