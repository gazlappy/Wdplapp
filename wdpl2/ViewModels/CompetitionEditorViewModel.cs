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
/// ViewModel for editing a competition's details
/// </summary>
public partial class CompetitionEditorViewModel : ObservableObject
{
    private readonly IDataStore _dataStore;
    private readonly Competition _competition;
    
    [ObservableProperty]
    private string _name = "";
    
    [ObservableProperty]
    private CompetitionStatus _status;
    
    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;
    
    [ObservableProperty]
    private string _notes = "";
    
    [ObservableProperty]
    private string _formatDisplay = "";
    
    [ObservableProperty]
    private ObservableCollection<ParticipantItem> _participants = new();
    
    [ObservableProperty]
    private bool _isGroupStageFormat;
    
    [ObservableProperty]
    private bool _isKnockoutFormat;
    
    [ObservableProperty]
    private bool _hasGroups;
    
    [ObservableProperty]
    private bool _hasRounds;
    
    [ObservableProperty]
    private string _statusMessage = "";
    
    [ObservableProperty]
    private Guid? _currentSeasonId;

    public Competition Competition => _competition;
    public CompetitionFormat Format => _competition.Format;
    public GroupStageSettings? GroupSettings => _competition.GroupSettings;
    public int GroupCount => _competition.Groups.Count;
    public int RoundCount => _competition.Rounds.Count;

    public CompetitionEditorViewModel(IDataStore dataStore, Competition competition, Guid? currentSeasonId)
    {
        _dataStore = dataStore;
        _competition = competition;
        _currentSeasonId = currentSeasonId;
        
        LoadCompetitionData();
    }

    private void LoadCompetitionData()
    {
        _name = _competition.Name;
        _status = _competition.Status;
        _startDate = _competition.StartDate ?? DateTime.Today;
        _notes = _competition.Notes ?? "";
        _formatDisplay = _competition.Format.ToString();
        
        _isGroupStageFormat = _competition.Format == CompetitionFormat.SinglesGroupStage ||
                            _competition.Format == CompetitionFormat.DoublesGroupStage;
        _isKnockoutFormat = !_isGroupStageFormat;
        
        _hasGroups = _competition.Groups.Count > 0;
        _hasRounds = _competition.Rounds.Count > 0;
        
        _ = LoadParticipantsAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            _competition.Name = _name;
            _competition.Status = _status;
            _competition.StartDate = _startDate;
            _competition.Notes = _notes;
            
            await _dataStore.UpdateCompetitionAsync(_competition);
            await _dataStore.SaveAsync();
            
            _statusMessage = "Competition saved";
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error saving: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadParticipantsAsync()
    {
        _participants.Clear();
        
        var format = _competition.Format;
        
        if (format == CompetitionFormat.SinglesKnockout || format == CompetitionFormat.RoundRobin ||
            format == CompetitionFormat.Swiss || format == CompetitionFormat.SinglesGroupStage)
        {
            // Singles - use players
            var players = await _dataStore.GetPlayersAsync(_currentSeasonId);
            foreach (var playerId in _competition.ParticipantIds)
            {
                var player = players.FirstOrDefault(p => p.Id == playerId);
                if (player != null)
                {
                    _participants.Add(new ParticipantItem
                    {
                        Id = player.Id,
                        Name = player.FullName
                    });
                }
            }
        }
        else if (format == CompetitionFormat.DoublesKnockout || format == CompetitionFormat.DoublesGroupStage)
        {
            // Doubles - use doubles teams
            var players = await _dataStore.GetPlayersAsync(_currentSeasonId);
            foreach (var team in _competition.DoublesTeams)
            {
                var p1 = players.FirstOrDefault(p => p.Id == team.Player1Id);
                var p2 = players.FirstOrDefault(p => p.Id == team.Player2Id);
                var name = $"{p1?.FullName ?? "?"} & {p2?.FullName ?? "?"}";
                
                _participants.Add(new ParticipantItem
                {
                    Id = team.Id,
                    Name = name
                });
            }
        }
        else if (format == CompetitionFormat.TeamKnockout)
        {
            // Team knockout - use teams
            var teams = await _dataStore.GetTeamsAsync(_currentSeasonId);
            foreach (var teamId in _competition.ParticipantIds)
            {
                var team = teams.FirstOrDefault(t => t.Id == teamId);
                if (team != null)
                {
                    _participants.Add(new ParticipantItem
                    {
                        Id = team.Id,
                        Name = team.Name ?? "Unnamed Team"
                    });
                }
            }
        }
    }

    [RelayCommand]
    private async Task RemoveParticipantAsync(Guid participantId)
    {
        _competition.ParticipantIds.Remove(participantId);
        _competition.DoublesTeams.RemoveAll(t => t.Id == participantId);
        
        await LoadParticipantsAsync();
        _statusMessage = "Participant removed";
    }

    [RelayCommand]
    private async Task ClearParticipantsAsync()
    {
        _competition.ParticipantIds.Clear();
        _competition.DoublesTeams.Clear();
        
        await LoadParticipantsAsync();
        _statusMessage = "Participants cleared";
    }

    [RelayCommand]
    private async Task GenerateBracketAsync(bool randomize)
    {
        try
        {
            int participantCount = _competition.Format == CompetitionFormat.DoublesKnockout
                ? _competition.DoublesTeams.Count
                : _competition.ParticipantIds.Count;

            if (participantCount < 2)
            {
                _statusMessage = "Need at least 2 participants to generate bracket";
                return;
            }

            var participants = _competition.Format == CompetitionFormat.DoublesKnockout || 
                              _competition.Format == CompetitionFormat.DoublesGroupStage
                ? _competition.DoublesTeams.Select(t => t.Id).ToList()
                : _competition.ParticipantIds;

            var rounds = _competition.Format switch
            {
                CompetitionFormat.SinglesKnockout => CompetitionGenerator.GenerateSingleKnockout(participants, randomize),
                CompetitionFormat.DoublesKnockout => CompetitionGenerator.GenerateSingleKnockout(participants, randomize),
                CompetitionFormat.TeamKnockout => CompetitionGenerator.GenerateSingleKnockout(participants, randomize),
                CompetitionFormat.RoundRobin => CompetitionGenerator.GenerateRoundRobin(participants, randomize),
                _ => new System.Collections.Generic.List<CompetitionRound>()
            };

            _competition.Rounds = rounds;
            _competition.Status = CompetitionStatus.InProgress;
            
            await _dataStore.SaveAsync();
            
            _hasRounds = _competition.Rounds.Count > 0;
            _statusMessage = $"Generated {rounds.Count} rounds with {rounds.Sum(r => r.Matches.Count)} matches {(randomize ? "(RANDOM)" : "(ordered)")}";
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error generating bracket: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GenerateGroupsAsync()
    {
        if (_competition.GroupSettings == null)
        {
            _statusMessage = "No group settings configured";
            return;
        }

        try
        {
            var participants = _competition.Format == CompetitionFormat.DoublesGroupStage
                ? _competition.DoublesTeams.Select(t => t.Id).ToList()
                : _competition.ParticipantIds;

            if (participants.Count < _competition.GroupSettings.NumberOfGroups * 2)
            {
                _statusMessage = $"Need at least {_competition.GroupSettings.NumberOfGroups * 2} participants";
                return;
            }

            var (groups, plateCompetition) = CompetitionGenerator.GenerateGroupStage(
                participants,
                _competition.GroupSettings,
                _competition.Format,
                _competition.SeasonId,
                _competition.Name,
                randomize: true
            );

            _competition.Groups = groups;

            if (plateCompetition != null)
            {
                await _dataStore.AddCompetitionAsync(plateCompetition);
                _competition.PlateCompetitionId = plateCompetition.Id;
            }

            _competition.Status = CompetitionStatus.InProgress;
            await _dataStore.SaveAsync();

            _hasGroups = _competition.Groups.Count > 0;
            _statusMessage = $"Generated {groups.Count} groups with {groups.Sum(g => g.Matches.Count)} total matches";
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error generating groups: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task FinalizeGroupsAsync()
    {
        if (_competition.GroupSettings == null)
        {
            _statusMessage = "No group settings configured";
            return;
        }

        try
        {
            var (knockoutParticipants, plateParticipants) = CompetitionGenerator.AdvanceFromGroups(
                _competition.Groups,
                _competition.GroupSettings.TopPlayersAdvance,
                _competition.GroupSettings.LowerPlayersToPlate
            );

            if (knockoutParticipants.Count >= 2)
            {
                _competition.Rounds = CompetitionGenerator.GenerateSingleKnockout(
                    knockoutParticipants,
                    randomize: false
                );
            }

            if (plateParticipants.Count >= 2 && _competition.PlateCompetitionId.HasValue)
            {
                var data = _dataStore.GetData();
                var plateComp = data.Competitions
                    .FirstOrDefault(c => c.Id == _competition.PlateCompetitionId.Value);

                if (plateComp != null)
                {
                    if (_competition.Format == CompetitionFormat.DoublesGroupStage)
                    {
                        plateComp.DoublesTeams = _competition.DoublesTeams
                            .Where(t => plateParticipants.Contains(t.Id))
                            .ToList();
                    }
                    else
                    {
                        plateComp.ParticipantIds = plateParticipants;
                    }

                    plateComp.Rounds = CompetitionGenerator.GenerateSingleKnockout(
                        plateParticipants,
                        randomize: false
                    );
                    plateComp.Status = CompetitionStatus.InProgress;
                }
            }

            _competition.Status = CompetitionStatus.InProgress;
            await _dataStore.SaveAsync();

            _hasRounds = _competition.Rounds.Count > 0;
            _statusMessage = $"Knockouts created! Main: {knockoutParticipants.Count}, Plate: {plateParticipants.Count}";
        }
        catch (Exception ex)
        {
            _statusMessage = $"Error finalizing groups: {ex.Message}";
        }
    }
}

/// <summary>
/// Display item for a participant in the competition
/// </summary>
public class ParticipantItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}
