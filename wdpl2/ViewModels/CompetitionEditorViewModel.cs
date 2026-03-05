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

    /// <summary>
    /// Get all venues for the current season (for venue selection in group stage).
    /// </summary>
    public async Task<List<Venue>> GetAvailableVenuesAsync()
    {
        var seasonId = _competition.SeasonId ?? CurrentSeasonId;
        return await _playerStore.GetVenuesAsync(seasonId);
    }

    /// <summary>
    /// Save the selected venues to the competition's group settings and persist.
    /// </summary>
    public async Task SaveSelectedVenuesAsync(List<CompetitionVenue> venues)
    {
        if (_competition.GroupSettings == null)
            _competition.GroupSettings = new GroupStageSettings();

        _competition.GroupSettings.SelectedVenues = venues;
        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();
        StatusMessage = $"Saved {venues.Count} venue(s) with {venues.Sum(v => v.TableCount)} total tables";
    }

    /// <summary>
    /// Save the number of groups to group settings and persist.
    /// </summary>
    public async Task SaveGroupCountAsync(int groupCount)
    {
        if (_competition.GroupSettings == null)
            _competition.GroupSettings = new GroupStageSettings();

        _competition.GroupSettings.NumberOfGroups = groupCount;
        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();
        StatusMessage = $"Group count set to {groupCount}";
    }

    /// <summary>
    /// Calculate the recommended number of groups based on participants, tables,
    /// and ensuring groups × topAdvance produces a power-of-2 for the KO bracket.
    /// </summary>
    public (int recommended, int totalTables, int participantCount, string explanation) GetGroupRecommendation()
    {
        int participantCount = _competition.Format == CompetitionFormat.DoublesGroupStage
            ? _competition.DoublesTeams.Count
            : _competition.ParticipantIds.Count;

        int totalTables = _competition.GroupSettings?.SelectedVenues.Sum(v => v.TableCount) ?? 0;
        int topAdvance = _competition.GroupSettings?.TopPlayersAdvance ?? 2;

        if (participantCount < 4 || totalTables < 1)
            return (0, totalTables, participantCount, "Add participants and select venues first.");

        // Max groups limited by tables (1 group per table)
        int maxGroups = totalTables;

        // Find the best group count where:
        //  1. groups × topAdvance is a power of 2 (valid KO bracket)
        //  2. groups ≤ tables
        //  3. each group has ≥ 3 participants (meaningful round-robin)
        //  4. target ~4 per group for ideal group size
        int recommended = 0;
        int bestDiff = int.MaxValue;

        for (int g = 2; g <= maxGroups; g++)
        {
            int koTotal = g * topAdvance;
            int perGroup = participantCount / g;

            // Must be a power of 2 for the KO bracket
            if ((koTotal & (koTotal - 1)) != 0) continue;
            // Each group needs at least 3 participants
            if (perGroup < 3) continue;

            // Prefer groups closest to 4 per group
            int diff = Math.Abs(perGroup - 4);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                recommended = g;
            }
        }

        // Fallback: if no power-of-2 combo found, pick nearest valid bracket
        if (recommended == 0)
        {
            int idealGroups = Math.Max(2, participantCount / 4);
            recommended = Math.Min(idealGroups, maxGroups);
        }

        int recPerGroup = participantCount / recommended;
        int remainder = participantCount % recommended;
        int recKoTotal = recommended * topAdvance;
        bool koValid = recKoTotal >= 2 && (recKoTotal & (recKoTotal - 1)) == 0;

        var explanation = $"{participantCount} participants across {totalTables} tables\n" +
                          $"Recommended: {recommended} groups of ~{recPerGroup}" +
                          (remainder > 0 ? $" ({remainder} group(s) with {recPerGroup + 1})" : "") +
                          $"\n→ {recKoTotal} advance to knockout (top {topAdvance} per group)" +
                          (koValid ? " ✅" : " ⚠️ not a power of 2");

        return (recommended, totalTables, participantCount, explanation);
    }

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

    /// <summary>
    /// Persist the current competition state (e.g. after updating group selections).
    /// </summary>
    public async Task SaveCompetitionAsync()
    {
        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();
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
    private async Task GenerateManualBracketAsync()
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

            var rounds = CompetitionGenerator.GenerateManualKnockout(participantCount);

            _competition.Rounds = rounds;
            _competition.Status = CompetitionStatus.InProgress;

            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();

            HasRounds = _competition.Rounds.Count > 0;
            StatusMessage = $"Manual bracket created — {rounds[0].Matches.Count} first-round slots to fill";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating manual bracket: {ex.Message}";
        }
    }

    /// <summary>
    /// Assign a participant to a specific slot in a match.
    /// </summary>
    public async Task AssignParticipantToMatchAsync(Guid matchId, bool isSlot1, Guid participantId)
    {
        try
        {
            foreach (var round in _competition.Rounds)
            {
                var match = round.Matches.FirstOrDefault(m => m.Id == matchId);
                if (match != null)
                {
                    if (isSlot1)
                        match.Participant1Id = participantId;
                    else
                        match.Participant2Id = participantId;

                    await _competitionStore.UpdateCompetitionAsync(_competition);
                    await _competitionStore.SaveAsync();
                    StatusMessage = $"Assigned {GetParticipantName(participantId) ?? "participant"} to match";
                    return;
                }
            }
            StatusMessage = "Match not found";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error assigning participant: {ex.Message}";
        }
    }

    /// <summary>
    /// Get all participants that haven't been assigned to any first-round match yet.
    /// </summary>
    public List<ParticipantItem> GetUnassignedParticipants()
    {
        var firstRound = _competition.Rounds.FirstOrDefault();
        if (firstRound == null) return Participants.ToList();

        var assignedIds = new HashSet<Guid>();
        foreach (var match in firstRound.Matches)
        {
            if (match.Participant1Id.HasValue) assignedIds.Add(match.Participant1Id.Value);
            if (match.Participant2Id.HasValue) assignedIds.Add(match.Participant2Id.Value);
        }

        return Participants.Where(p => !assignedIds.Contains(p.Id)).ToList();
    }

    [RelayCommand]
    private async Task GenerateGroupsAsync()
    {
        if (_competition.GroupSettings == null)
        {
            StatusMessage = "No group settings configured";
            return;
        }

        if (_competition.GroupSettings.NumberOfGroups < 2)
        {
            StatusMessage = "Choose the number of groups first";
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
            int topAdvance = _competition.GroupSettings.TopPlayersAdvance;

            // Collect winners from the manually-set standings (Position 1..topAdvance)
            var knockoutParticipants = new List<Guid>();
            var plateParticipants = new List<Guid>();
            int lowerToPlate = _competition.GroupSettings.LowerPlayersToPlate;

            foreach (var group in _competition.Groups)
            {
                // Winners: standings with Position 1..topAdvance
                var winners = group.Standings
                    .Where(s => s.Position > 0 && s.Position <= topAdvance)
                    .OrderBy(s => s.Position)
                    .Select(s => s.ParticipantId)
                    .ToList();

                knockoutParticipants.AddRange(winners);

                // Plate: everyone else from the group (not selected as winners)
                if (lowerToPlate > 0)
                {
                    var losers = group.ParticipantIds
                        .Where(p => !winners.Contains(p))
                        .Take(lowerToPlate)
                        .ToList();
                    plateParticipants.AddRange(losers);
                }
            }

            if (knockoutParticipants.Count < 2)
            {
                StatusMessage = "Not enough winners selected to create a knockout bracket";
                return;
            }

            _competition.Rounds = CompetitionGenerator.GenerateSingleKnockout(
                knockoutParticipants,
                randomize: _competition.RandomDraw
            );

            // Handle plate competition for group-stage losers
            if (plateParticipants.Count >= 2 && _competition.GroupSettings.CreatePlateCompetition)
            {
                Competition? plateComp = null;

                // Try to find existing plate comp
                if (_competition.PlateCompetitionId.HasValue)
                {
                    var plateComps = await _competitionStore.GetCompetitionsAsync(_competition.SeasonId);
                    plateComp = plateComps
                        .FirstOrDefault(c => c.Id == _competition.PlateCompetitionId.Value);
                }

                // Create plate comp if it doesn't exist yet
                if (plateComp == null)
                {
                    var plateSuffix = _competition.GroupSettings.PlateNameSuffix ?? "Plate";
                    plateComp = new Competition
                    {
                        Name = $"{_competition.Name} {plateSuffix}",
                        Format = _competition.Format == CompetitionFormat.DoublesGroupStage
                            ? CompetitionFormat.DoublesKnockout
                            : CompetitionFormat.SinglesKnockout,
                        Status = CompetitionStatus.InProgress,
                        SeasonId = _competition.SeasonId,
                        StartDate = _competition.StartDate,
                        CreatedDate = DateTime.Now,
                        BestOf = _competition.BestOf,
                        RandomDraw = _competition.RandomDraw
                    };
                    await _competitionStore.AddCompetitionAsync(plateComp);
                    _competition.PlateCompetitionId = plateComp.Id;
                }

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
                    randomize: _competition.RandomDraw
                );
                plateComp.Status = CompetitionStatus.InProgress;

                await _competitionStore.UpdateCompetitionAsync(plateComp);
            }

            _competition.Status = CompetitionStatus.InProgress;
            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();

            HasRounds = _competition.Rounds.Count > 0;
            StatusMessage = $"Knockout created! {knockoutParticipants.Count} in main draw" +
                           (plateParticipants.Count > 0 ? $", {plateParticipants.Count} in plate" : "");
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

    /// <summary>
    /// Creates a new "Losers Cup" competition populated with the losers
    /// from the first round of this knockout bracket.
    /// </summary>
    [RelayCommand]
    private async Task CreateLosersCupAsync()
    {
        try
        {
            var firstRound = _competition.Rounds.FirstOrDefault();
            if (firstRound == null)
            {
                StatusMessage = "No bracket generated yet";
                return;
            }

            if (_competition.PlateCompetitionId.HasValue)
            {
                StatusMessage = "A Losers Cup has already been created for this competition";
                return;
            }

            // Collect losers from completed first-round matches
            var loserIds = new List<Guid>();
            foreach (var match in firstRound.Matches)
            {
                if (!match.IsComplete || !match.WinnerId.HasValue) continue;
                if (!match.Participant1Id.HasValue || !match.Participant2Id.HasValue) continue;

                // The loser is whichever participant is NOT the winner
                var loserId = match.WinnerId == match.Participant1Id
                    ? match.Participant2Id.Value
                    : match.Participant1Id.Value;
                loserIds.Add(loserId);
            }

            if (loserIds.Count < 2)
            {
                StatusMessage = $"Need at least 2 first-round losers to create a Losers Cup (found {loserIds.Count}). Complete more first-round matches first.";
                return;
            }

            // Create the losers cup competition with the same settings
            var losersCup = new Competition
            {
                Name = $"{_competition.Name} - Losers Cup",
                SeasonId = _competition.SeasonId,
                Format = _competition.Format,
                Status = CompetitionStatus.Draft,
                StartDate = _competition.StartDate,
                CreatedDate = DateTime.Now,
                BestOf = _competition.BestOf,
                RandomDraw = _competition.RandomDraw,
                Notes = $"Losers Cup for {_competition.Name}"
            };

            // Add participants based on format
            if (_competition.Format == CompetitionFormat.DoublesKnockout)
            {
                // For doubles, copy the DoublesTeam objects that lost
                var loserTeams = _competition.DoublesTeams
                    .Where(t => loserIds.Contains(t.Id))
                    .Select(t => new DoublesTeam
                    {
                        Id = t.Id,
                        Player1Id = t.Player1Id,
                        Player2Id = t.Player2Id,
                        TeamName = t.TeamName
                    })
                    .ToList();
                losersCup.DoublesTeams = loserTeams;
            }
            else
            {
                losersCup.ParticipantIds = loserIds;
            }

            // Save to data store
            await _competitionStore.AddCompetitionAsync(losersCup);

            // Link the losers cup back to the parent
            _competition.PlateCompetitionId = losersCup.Id;
            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();

            StatusMessage = $"Losers Cup created with {loserIds.Count} participants: \"{losersCup.Name}\"";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating Losers Cup: {ex.Message}";
        }
    }

    /// <summary>
    /// Returns true if first-round losers exist and no Losers Cup has been created yet.
    /// </summary>
    public bool CanCreateLosersCup
    {
        get
        {
            if (_competition.PlateCompetitionId.HasValue) return false;
            var firstRound = _competition.Rounds.FirstOrDefault();
            if (firstRound == null) return false;
            int loserCount = firstRound.Matches.Count(m =>
                m.IsComplete && m.WinnerId.HasValue &&
                m.Participant1Id.HasValue && m.Participant2Id.HasValue);
            return loserCount >= 2;
        }
    }

    /// <summary>
    /// Returns true if a Losers Cup already exists for this competition.
    /// </summary>
    public bool HasLosersCup => _competition.PlateCompetitionId.HasValue;

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
