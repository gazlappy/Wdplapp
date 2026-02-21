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
    private readonly IDataStore _competitionStore;  // for competition CRUD (SQLite)
    private readonly IDataStore _playerStore;       // for player/team lookups (JSON)
    private readonly Competition _competition;
    private List<Player> _cachedPlayers = new();
    private List<Team> _cachedTeams = new();

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

    public CompetitionEditorViewModel(IDataStore competitionStore, IDataStore playerStore, Competition competition, Guid? currentSeasonId)
    {
        _competitionStore = competitionStore;
        _playerStore = playerStore;
        _competition = competition;
        _currentSeasonId = currentSeasonId;

        LoadCompetitionData();
    }

    private void LoadCompetitionData()
    {
        Name = _competition.Name;
        Status = _competition.Status;
        StartDate = _competition.StartDate ?? DateTime.Today;
        Notes = _competition.Notes ?? "";
        FormatDisplay = _competition.Format.ToString();
        
        IsGroupStageFormat = _competition.Format == CompetitionFormat.SinglesGroupStage ||
                            _competition.Format == CompetitionFormat.DoublesGroupStage;
        IsKnockoutFormat = !IsGroupStageFormat;
        
        HasGroups = _competition.Groups.Count > 0;
        HasRounds = _competition.Rounds.Count > 0;
        
        _ = LoadParticipantsAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            _competition.Name = Name;
            _competition.Status = Status;
            _competition.StartDate = StartDate;
            _competition.Notes = Notes;
            
            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();
            
            StatusMessage = "Competition saved";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadParticipantsAsync()
    {
        Participants.Clear();

        var format = _competition.Format;
        var seasonId = _competition.SeasonId ?? CurrentSeasonId;

        if (format == CompetitionFormat.SinglesKnockout || format == CompetitionFormat.RoundRobin ||
            format == CompetitionFormat.Swiss || format == CompetitionFormat.SinglesGroupStage)
        {
            // Singles - use players
            _cachedPlayers = await _playerStore.GetPlayersAsync(seasonId);
            foreach (var playerId in _competition.ParticipantIds)
            {
                var player = _cachedPlayers.FirstOrDefault(p => p.Id == playerId);
                if (player != null)
                {
                    Participants.Add(new ParticipantItem
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
            _cachedPlayers = await _playerStore.GetPlayersAsync(seasonId);
            foreach (var team in _competition.DoublesTeams)
            {
                var p1 = _cachedPlayers.FirstOrDefault(p => p.Id == team.Player1Id);
                var p2 = _cachedPlayers.FirstOrDefault(p => p.Id == team.Player2Id);
                var name = $"{p1?.FullName ?? "?"} & {p2?.FullName ?? "?"}";

                Participants.Add(new ParticipantItem
                {
                    Id = team.Id,
                    Name = name
                });
            }
        }
        else if (format == CompetitionFormat.TeamKnockout)
        {
            // Team knockout - use teams
            _cachedTeams = await _playerStore.GetTeamsAsync(seasonId);
            foreach (var teamId in _competition.ParticipantIds)
            {
                var team = _cachedTeams.FirstOrDefault(t => t.Id == teamId);
                if (team != null)
                {
                    Participants.Add(new ParticipantItem
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

        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();
        await LoadParticipantsAsync();
        StatusMessage = "Participant removed";
    }

    [RelayCommand]
    private async Task ClearParticipantsAsync()
    {
        _competition.ParticipantIds.Clear();
        _competition.DoublesTeams.Clear();

        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();
        await LoadParticipantsAsync();
        StatusMessage = "Participants cleared";
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
                StatusMessage = "Need at least 2 participants to generate bracket";
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
                CompetitionFormat.Swiss => throw new NotSupportedException("Swiss format bracket generation is not yet implemented"),
                _ => new System.Collections.Generic.List<CompetitionRound>()
            };

            _competition.Rounds = rounds;
            _competition.Status = CompetitionStatus.InProgress;

            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();
            
            HasRounds = _competition.Rounds.Count > 0;
            StatusMessage = $"Generated {rounds.Count} rounds with {rounds.Sum(r => r.Matches.Count)} matches {(randomize ? "(RANDOM)" : "(ordered)")}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating bracket: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GenerateGroupsAsync()
    {
        if (_competition.GroupSettings == null)
        {
            StatusMessage = "No group settings configured";
            return;
        }

        try
        {
            var participants = _competition.Format == CompetitionFormat.DoublesGroupStage
                ? _competition.DoublesTeams.Select(t => t.Id).ToList()
                : _competition.ParticipantIds;

            if (participants.Count < _competition.GroupSettings.NumberOfGroups * 2)
            {
                StatusMessage = $"Need at least {_competition.GroupSettings.NumberOfGroups * 2} participants";
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
                await _competitionStore.AddCompetitionAsync(plateCompetition);
                _competition.PlateCompetitionId = plateCompetition.Id;
            }

            _competition.Status = CompetitionStatus.InProgress;
            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();

            HasGroups = _competition.Groups.Count > 0;
            StatusMessage = $"Generated {groups.Count} groups with {groups.Sum(g => g.Matches.Count)} total matches";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating groups: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task FinalizeGroupsAsync()
    {
        if (_competition.GroupSettings == null)
        {
            StatusMessage = "No group settings configured";
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
                var plateComps = await _competitionStore.GetCompetitionsAsync(_competition.SeasonId);
                var plateComp = plateComps
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

                    await _competitionStore.UpdateCompetitionAsync(plateComp);
                }
            }

            _competition.Status = CompetitionStatus.InProgress;
            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();

            HasRounds = _competition.Rounds.Count > 0;
            StatusMessage = $"Knockouts created! Main: {knockoutParticipants.Count}, Plate: {plateParticipants.Count}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error finalizing groups: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddParticipantIdsAsync(List<Guid> ids)
    {
        foreach (var id in ids)
        {
            if (!_competition.ParticipantIds.Contains(id))
                _competition.ParticipantIds.Add(id);
        }
        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();
        await LoadParticipantsAsync();
        StatusMessage = $"Added {ids.Count} participant(s)";
    }

    [RelayCommand]
    private async Task AddDoublesTeamAsync(DoublesTeam team)
    {
        _competition.DoublesTeams.Add(team);
        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();
        await LoadParticipantsAsync();
        StatusMessage = $"Added doubles team: {team.TeamName}";
    }

    public string? GetParticipantName(Guid? participantId)
    {
        if (!participantId.HasValue) return null;

        if (_competition.Format == CompetitionFormat.DoublesKnockout || _competition.Format == CompetitionFormat.DoublesGroupStage)
        {
            var team = _competition.DoublesTeams.FirstOrDefault(t => t.Id == participantId.Value);
            return team?.TeamName;
        }
        else if (_competition.Format == CompetitionFormat.TeamKnockout)
        {
            var team = _cachedTeams.FirstOrDefault(t => t.Id == participantId.Value);
            return team?.Name;
        }
        else
        {
            var player = _cachedPlayers.FirstOrDefault(p => p.Id == participantId.Value);
            return player?.FullName;
        }
    }

    public async Task<List<Player>> GetAvailablePlayersAsync()
    {
        var seasonId = _competition.SeasonId ?? CurrentSeasonId;
        var players = await _playerStore.GetPlayersAsync(seasonId);
        return players
            .Where(p => !_competition.ParticipantIds.Contains(p.Id))
            .OrderBy(p => p.FullName)
            .ToList();
    }

    public async Task<List<Player>> GetAvailableDoublesPlayersAsync()
    {
        var usedPlayerIds = new HashSet<Guid>();
        foreach (var team in _competition.DoublesTeams)
        {
            usedPlayerIds.Add(team.Player1Id);
            usedPlayerIds.Add(team.Player2Id);
        }
        var seasonId = _competition.SeasonId ?? CurrentSeasonId;
        var players = await _playerStore.GetPlayersAsync(seasonId);
        return players
            .Where(p => !usedPlayerIds.Contains(p.Id))
            .OrderBy(p => p.FullName)
            .ToList();
    }

    public async Task<List<Team>> GetAvailableTeamsAsync()
    {
        var seasonId = _competition.SeasonId ?? CurrentSeasonId;
        var teams = await _playerStore.GetTeamsAsync(seasonId);
        return teams
            .Where(t => !_competition.ParticipantIds.Contains(t.Id))
            .OrderBy(t => t.Name)
            .ToList();
    }

    [RelayCommand]
    private async Task ApplyBracketScoresAsync()
    {
        bool anyUpdates = false;
        int ftw = _competition.FramesToWin; // 0 = unlimited / not set

        foreach (var round in _competition.Rounds)
        {
            foreach (var match in round.Matches)
            {
                if (!match.IsComplete && match.Participant1Id.HasValue && match.Participant2Id.HasValue)
                {
                    Guid? winnerId = null;

                    if (ftw > 0)
                    {
                        // Best-of mode: only complete when a player reaches the winning score
                        if (match.Participant1Score >= ftw)
                            winnerId = match.Participant1Id;
                        else if (match.Participant2Score >= ftw)
                            winnerId = match.Participant2Id;
                        else
                            continue; // Neither player has reached the winning score yet
                    }
                    else
                    {
                        // Unlimited mode: whoever is ahead wins (scores must differ)
                        if (match.Participant1Score > match.Participant2Score)
                            winnerId = match.Participant1Id;
                        else if (match.Participant2Score > match.Participant1Score)
                            winnerId = match.Participant2Id;
                        else
                            continue; // Tied — can't determine a winner
                    }

                    match.WinnerId = winnerId;
                    match.IsComplete = true;
                    anyUpdates = true;
                    AdvanceWinner(round, match);
                }
            }
        }

        if (anyUpdates)
        {
            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();
            StatusMessage = "All scores applied - winners advanced to next rounds";
        }
        else
        {
            StatusMessage = "No new scores to apply";
        }
    }

    private void AdvanceWinner(CompetitionRound round, CompetitionMatch match)
    {
        var nextRound = _competition.Rounds.FirstOrDefault(r => r.RoundNumber == round.RoundNumber + 1);
        if (nextRound == null || !match.WinnerId.HasValue) return;

        int matchIndex = round.Matches.IndexOf(match);
        if (matchIndex < 0) return;

        int nextMatchIndex = matchIndex / 2;
        if (nextMatchIndex >= nextRound.Matches.Count) return;

        var nextMatch = nextRound.Matches[nextMatchIndex];
        if (matchIndex % 2 == 0)
            nextMatch.Participant1Id = match.WinnerId;
        else
            nextMatch.Participant2Id = match.WinnerId;
    }

    [RelayCommand]
    private async Task ApplyGroupScoresAsync()
    {
        bool anyUpdates = false;

        foreach (var group in _competition.Groups)
        {
            foreach (var match in group.Matches)
            {
                if (!match.IsComplete && match.Participant1Id.HasValue && match.Participant2Id.HasValue)
                {
                    if (match.Participant1Score > match.Participant2Score)
                        match.WinnerId = match.Participant1Id;
                    else if (match.Participant2Score > match.Participant1Score)
                        match.WinnerId = match.Participant2Id;
                    else if (match.Participant1Score > 0 || match.Participant2Score > 0)
                        match.WinnerId = null;
                    else
                        continue;

                    match.IsComplete = true;
                    anyUpdates = true;
                }
            }
        }

        if (anyUpdates)
        {
            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();
            StatusMessage = "All group scores applied";
        }
        else
        {
            StatusMessage = "No new scores to apply";
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
