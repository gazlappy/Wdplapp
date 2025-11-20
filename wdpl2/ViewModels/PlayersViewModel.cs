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
/// ViewModel for PlayersPage - manages player list and CRUD operations
/// </summary>
public partial class PlayersViewModel : BaseViewModel
{
    private readonly IDataStore _dataStore;
    
    [ObservableProperty]
    private ObservableCollection<Player> _players = new();
    
    [ObservableProperty]
    private Player? _selectedPlayer;
    
    [ObservableProperty]
    private string _searchText = "";
    
    [ObservableProperty]
    private string _firstName = "";
    
    [ObservableProperty]
    private string _lastName = "";
    
    [ObservableProperty]
    private bool _isMultiSelectMode;

    public PlayersViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
        SeasonService.SeasonChanged += OnSeasonChanged;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        _currentSeasonId = SeasonService.CurrentSeasonId;
        await LoadPlayersAsync();
    }

    protected override void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        base.OnSeasonChanged(sender, e);
        _ = LoadPlayersAsync();
    }

    [RelayCommand]
    private async Task LoadPlayersAsync()
    {
        _isLoading = true;
        
        try
        {
            if (!_currentSeasonId.HasValue)
            {
                SetStatus("No season selected");
                _players.Clear();
                return;
            }

            var allPlayers = await _dataStore.GetPlayersAsync(_currentSeasonId);

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var lower = _searchText.ToLower();
                allPlayers = allPlayers
                    .Where(p => p.FullName.ToLower().Contains(lower))
                    .ToList();
            }

            _players.Clear();
            foreach (var player in allPlayers)
                _players.Add(player);

            SetStatus($"{_players.Count} player(s)");
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading players: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task SearchPlayersAsync(string? searchText)
    {
        _searchText = searchText ?? "";
        await LoadPlayersAsync();
    }

    [RelayCommand]
    private async Task AddPlayerAsync()
    {
        if (string.IsNullOrWhiteSpace(_firstName) || string.IsNullOrWhiteSpace(_lastName))
        {
            SetStatus("First and last name required");
            return;
        }

        if (!_currentSeasonId.HasValue)
        {
            SetStatus("Please select a season first");
            return;
        }

        var player = new Player
        {
            SeasonId = _currentSeasonId.Value,
            FirstName = _firstName.Trim(),
            LastName = _lastName.Trim()
        };

        await _dataStore.AddPlayerAsync(player);
        await _dataStore.SaveAsync();
        await LoadPlayersAsync();

        ClearEditor();
        SetStatus($"Added: {player.FullName}");
    }

    [RelayCommand]
    private async Task UpdatePlayerAsync()
    {
        if (_selectedPlayer == null)
        {
            SetStatus("No player selected");
            return;
        }

        _selectedPlayer.FirstName = _firstName?.Trim() ?? "";
        _selectedPlayer.LastName = _lastName?.Trim() ?? "";

        await _dataStore.UpdatePlayerAsync(_selectedPlayer);
        await _dataStore.SaveAsync();
        await LoadPlayersAsync();

        SetStatus($"Updated: {_selectedPlayer.FullName}");
    }

    [RelayCommand]
    private async Task DeletePlayerAsync(Player? player)
    {
        if (player == null)
        {
            SetStatus("No player selected");
            return;
        }

        await _dataStore.DeletePlayerAsync(player);
        await _dataStore.SaveAsync();
        await LoadPlayersAsync();

        ClearEditor();
        SetStatus("Deleted player");
    }

    [RelayCommand]
    private void ToggleMultiSelect()
    {
        _isMultiSelectMode = !_isMultiSelectMode;
        SetStatus(_isMultiSelectMode ? "Multi-select enabled" : "Multi-select disabled");
    }

    [RelayCommand]
    private async Task BulkDeleteAsync(System.Collections.Generic.List<Player>? players)
    {
        if (players == null || players.Count == 0)
        {
            SetStatus("No players selected");
            return;
        }

        foreach (var player in players)
        {
            await _dataStore.DeletePlayerAsync(player);
        }

        await _dataStore.SaveAsync();
        await LoadPlayersAsync();
        SetStatus($"Deleted {players.Count} player(s)");
    }

    partial void OnSelectedPlayerChanged(Player? value)
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

    private void LoadEditor(Player player)
    {
        _firstName = player.FirstName ?? "";
        _lastName = player.LastName ?? "";
    }

    private void ClearEditor()
    {
        _firstName = "";
        _lastName = "";
    }
}
