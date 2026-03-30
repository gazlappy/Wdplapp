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
    public Task<List<Venue>> GetAvailableVenuesAsync()
    {
        var seasonId = _competition.SeasonId ?? CurrentSeasonId;
        var venues = DataStore.Data?.Venues?
            .Where(v => v != null && v.SeasonId == seasonId)
            .OrderBy(v => v.Name)
            .ToList() ?? new List<Venue>();
        return Task.FromResult(venues);
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
        StatusMessage = $"Saved {venues.Count} venue(s) with {venues.Sum(v => v.TableCount)} table(s)";
    }

    /// <summary>
    /// Save the group round date and persist.
    /// </summary>
    public async Task SaveGroupDateAsync(DateTime? date)
    {
        if (_competition.GroupSettings == null)
            _competition.GroupSettings = new GroupStageSettings();

        _competition.GroupSettings.GroupDate = date;
        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();
        StatusMessage = date.HasValue ? $"Group date set to {date.Value:dd MMM yyyy}" : "Group date cleared";
    }

    /// <summary>
    /// Save date and table selections for a specific KO round.
    /// Automatically assigns matches to the selected tables round-robin.
    /// </summary>
    public async Task SaveRoundDetailsAsync(Guid roundId, DateTime? date, List<CompetitionVenue>? venues)
    {
        var round = _competition.Rounds.FirstOrDefault(r => r.Id == roundId);
        if (round == null)
        {
            StatusMessage = "Round not found";
            return;
        }

        if (date.HasValue) round.Date = date;
        if (venues != null)
        {
            round.SelectedVenues = venues;
            CompetitionGenerator.AssignMatchVenueTables(round.Matches, venues);
        }

        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();

        var datePart = round.Date.HasValue ? round.Date.Value.ToString("dd MMM yyyy") : "no date";
        StatusMessage = $"{round.Name}: {datePart}, {round.TotalTables} table(s)";
    }

    /// <summary>
    /// Get tables that are in use by the parent competition on a given date.
    /// Used to restrict plate competitions from using the same tables.
    /// </summary>
    public async Task<List<Guid>> GetTablesInUseByParentOnDateAsync(DateTime date)
    {
        if (!_competition.ParentCompetitionId.HasValue) return new();

        var seasonId = _competition.SeasonId ?? CurrentSeasonId;
        var allComps = await _competitionStore.GetCompetitionsAsync(seasonId);
        var parentComp = allComps.FirstOrDefault(c => c.Id == _competition.ParentCompetitionId.Value);
        if (parentComp == null) return new();

        var usedTableIds = new List<Guid>();

        // Check group stage tables (if groups match the date)
        if (parentComp.GroupSettings?.GroupDate?.Date == date.Date)
        {
            usedTableIds.AddRange(
                parentComp.GroupSettings.SelectedVenues
                    .SelectMany(v => v.SelectedTables)
                    .Select(t => t.TableId));
        }

        // Check KO round tables
        foreach (var round in parentComp.Rounds.Where(r => r.Date?.Date == date.Date))
        {
            usedTableIds.AddRange(
                round.SelectedVenues
                    .SelectMany(v => v.SelectedTables)
                    .Select(t => t.TableId));
        }

        return usedTableIds.Distinct().ToList();
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

    public CompetitionEditorViewModel(IDataStore competitionStore, Competition competition, Guid? currentSeasonId)
    {
        _competitionStore = competitionStore;
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
    private Task LoadParticipantsAsync()
    {
        Participants.Clear();

        var format = _competition.Format;
        var seasonId = _competition.SeasonId ?? CurrentSeasonId;

        if (format == CompetitionFormat.SinglesKnockout || format == CompetitionFormat.RoundRobin ||
            format == CompetitionFormat.Swiss || format == CompetitionFormat.SinglesGroupStage)
        {
            // Singles - use players from JSON store
            _cachedPlayers = DataStore.Data?.Players?
                .Where(p => p != null && p.SeasonId == seasonId)
                .ToList() ?? new List<Player>();
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
            // Doubles - use players from JSON store
            _cachedPlayers = DataStore.Data?.Players?
                .Where(p => p != null && p.SeasonId == seasonId)
                .ToList() ?? new List<Player>();
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
            // Team knockout - use teams from JSON store
            _cachedTeams = DataStore.Data?.Teams?
                .Where(t => t != null && t.SeasonId == seasonId)
                .ToList() ?? new List<Team>();
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

        return Task.CompletedTask;
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

    /// <summary>
    /// Seed participants by their current league rating (highest first) then generate bracket.
    /// Higher-rated players are placed to meet lower-rated ones in early rounds.
    /// </summary>
    [RelayCommand]
    private async Task GenerateSeededBracketAsync()
    {
        try
        {
            var seasonId = _competition.SeasonId ?? CurrentSeasonId;
            var participants = _competition.Format == CompetitionFormat.DoublesKnockout ||
                               _competition.Format == CompetitionFormat.DoublesGroupStage
                ? _competition.DoublesTeams.Select(t => t.Id).ToList()
                : _competition.ParticipantIds;

            if (participants.Count < 2)
            {
                StatusMessage = "Need at least 2 participants";
                return;
            }

            // Get ratings for seeding
            var data = DataStore.Data;
            var season = seasonId.HasValue ? data.Seasons.FirstOrDefault(s => s.Id == seasonId) : null;
            var fixtures = data.Fixtures.Where(f => f.SeasonId == seasonId && f.Frames.Count > 0).ToList();
            var players = data.Players.Where(p => p.SeasonId == seasonId).ToList();
            var teams = data.Teams.Where(t => t.SeasonId == seasonId).ToList();

            var seasonSettings = data.GetSettingsForSeason(seasonId);
            var ratings = RatingCalculator.CalculateAllRatings(
                fixtures, players, teams, seasonSettings, season?.StartDate ?? DateTime.Today);

            // Sort participants by rating (highest first)
            var seeded = participants
                .OrderByDescending(id => ratings.TryGetValue(id, out var r) ? r.Rating : seasonSettings.RatingStartValue)
                .ToList();

            // Update participant order
            _competition.ParticipantIds = seeded;

            // Generate bracket with seeded order (not randomized)
            var rounds = CompetitionGenerator.GenerateSingleKnockout(seeded, randomize: false);
            _competition.Rounds = rounds;
            _competition.Status = CompetitionStatus.InProgress;

            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();

            HasRounds = _competition.Rounds.Count > 0;
            StatusMessage = $"Seeded bracket: {rounds.Count} rounds (top seed: {GetParticipantName(seeded[0]) ?? "?"})";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating seeded bracket: {ex.Message}";
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

        if (_competition.GroupSettings.NumberOfGroups < 1)
        {
            StatusMessage = "Choose the number of groups first";
            return;
        }

        try
        {
            var participants = _competition.Format == CompetitionFormat.DoublesGroupStage
                ? _competition.DoublesTeams.Select(t => t.Id).ToList()
                : _competition.ParticipantIds;

            if (participants.Count < Math.Max(2, _competition.GroupSettings.NumberOfGroups * 2))
            {
                StatusMessage = $"Need at least {Math.Max(2, _competition.GroupSettings.NumberOfGroups * 2)} participants";
                return;
            }

            var (groups, _) = CompetitionGenerator.GenerateGroupStage(
                participants,
                _competition.GroupSettings,
                _competition.Format,
                _competition.SeasonId,
                _competition.Name,
                randomize: true
            );

            _competition.Groups = groups;

            _competition.Status = CompetitionStatus.InProgress;
            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();

            HasGroups = _competition.Groups.Count > 0;
            StatusMessage = $"Generated {groups.Count} groups";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating groups: {ex.Message}";
        }
    }

    /// <summary>
    /// Re-shuffle all participants across the existing groups randomly.
    /// Clears any previous winner selections.
    /// </summary>
    public async Task RandomiseGroupsAsync()
    {
        if (_competition.Groups.Count == 0 || _competition.GroupSettings == null)
        {
            StatusMessage = "No groups to randomise";
            return;
        }

        try
        {
            // Collect all participant IDs from every group
            var allParticipants = _competition.Groups
                .SelectMany(g => g.ParticipantIds)
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            int numberOfGroups = _competition.Groups.Count;
            int perGroup = allParticipants.Count / numberOfGroups;
            int remainder = allParticipants.Count % numberOfGroups;
            int idx = 0;

            for (int i = 0; i < numberOfGroups; i++)
            {
                int size = perGroup + (i < remainder ? 1 : 0);
                _competition.Groups[i].ParticipantIds = allParticipants.GetRange(idx, size);
                _competition.Groups[i].Standings.Clear();
                _competition.Groups[i].Matches.Clear();
                idx += size;
            }

            // Reassign venue tables randomly
            if (_competition.GroupSettings?.SelectedVenues.Count > 0)
                CompetitionGenerator.AssignVenueTables(_competition.Groups, _competition.GroupSettings.SelectedVenues);

            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();
            StatusMessage = "Groups randomised";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error randomising groups: {ex.Message}";
        }
    }

    /// <summary>
    /// Archive the current groups, take the selected winners, and create a new round of groups.
    /// </summary>
    public async Task AdvanceToNextGroupRoundAsync(int newGroupCount, int advancePerGroup)
    {
        if (_competition.GroupSettings == null || _competition.Groups.Count == 0)
        {
            StatusMessage = "No groups to advance from";
            return;
        }

        try
        {
            int topAdvance = _competition.GroupSettings.TopPlayersAdvance;

            // Collect winners from the current groups
            var winners = new List<Guid>();
            foreach (var group in _competition.Groups)
            {
                var groupWinners = group.Standings
                    .Where(s => s.Position > 0 && s.Position <= topAdvance)
                    .OrderBy(s => s.Position)
                    .Select(s => s.ParticipantId)
                    .ToList();
                winners.AddRange(groupWinners);
            }

            if (winners.Count < newGroupCount * 2)
            {
                StatusMessage = $"Not enough winners ({winners.Count}) for {newGroupCount} groups";
                return;
            }

            // Work out the next group round number
            int currentRound = _competition.Groups.Max(g => g.GroupRound);
            int nextRound = currentRound + 1;

            // Archive current groups into PreviousGroups
            _competition.PreviousGroups.AddRange(_competition.Groups);

            // Randomise winners
            var shuffled = winners.OrderBy(_ => Random.Shared.Next()).ToList();

            // Create new groups
            var newGroups = new List<CompetitionGroup>();
            int perGroup = shuffled.Count / newGroupCount;
            int remainder = shuffled.Count % newGroupCount;
            int idx = 0;

            for (int i = 0; i < newGroupCount; i++)
            {
                int size = perGroup + (i < remainder ? 1 : 0);
                var group = new CompetitionGroup
                {
                    Name = $"Group {(char)('A' + i)} (R{nextRound})",
                    GroupNumber = i + 1,
                    GroupRound = nextRound,
                    ParticipantIds = shuffled.GetRange(idx, size)
                };
                newGroups.Add(group);
                idx += size;
            }

            // Assign venue tables randomly to the new groups
            if (_competition.GroupSettings.SelectedVenues.Count > 0)
                CompetitionGenerator.AssignVenueTables(newGroups, _competition.GroupSettings.SelectedVenues);

            _competition.Groups = newGroups;
            _competition.GroupSettings.NumberOfGroups = newGroupCount;
            _competition.GroupSettings.TopPlayersAdvance = advancePerGroup;

            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();

            HasGroups = true;
            StatusMessage = $"Round {nextRound}: {newGroups.Count} groups created with {winners.Count} players";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating next group round: {ex.Message}";
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

            foreach (var group in _competition.Groups)
            {
                var winners = group.Standings
                    .Where(s => s.Position > 0 && s.Position <= topAdvance)
                    .OrderBy(s => s.Position)
                    .Select(s => s.ParticipantId)
                    .ToList();

                knockoutParticipants.AddRange(winners);
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

            _competition.Status = CompetitionStatus.InProgress;
            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();

            HasRounds = _competition.Rounds.Count > 0;
            StatusMessage = $"Knockout created with {knockoutParticipants.Count} players";
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

    /// <summary>
    /// Toggle a participant's no-show status. No-shows are excluded from the plate.
    /// </summary>
    public async Task ToggleNoShowAsync(Guid participantId)
    {
        if (_competition.NoShowIds.Contains(participantId))
            _competition.NoShowIds.Remove(participantId);
        else
            _competition.NoShowIds.Add(participantId);

        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();

        var name = GetParticipantName(participantId) ?? "Player";
        bool isNoShow = _competition.NoShowIds.Contains(participantId);
        StatusMessage = isNoShow ? $"{name} marked as No Show" : $"{name} unmarked as No Show";
    }

    /// <summary>
    /// Check if a participant is marked as a no-show.
    /// </summary>
    public bool IsNoShow(Guid participantId) => _competition.NoShowIds.Contains(participantId);

    /// <summary>
    /// Manually create a plate competition from the group stage losers.
    /// Collects all non-winners from every group round (current + previous).
    /// </summary>
    public async Task CreatePlateFromGroupsAsync()
    {
        if (_competition.PlateCompetitionId.HasValue)
        {
            StatusMessage = "A plate competition already exists for this competition";
            return;
        }

        if (_competition.ParentCompetitionId.HasValue)
        {
            StatusMessage = "This is already a plate — cannot create a sub-plate";
            return;
        }

        try
        {
            // Collect all winners across all group rounds (current + previous)
            var allWinnerIds = new HashSet<Guid>();

            foreach (var group in _competition.Groups.Concat(_competition.PreviousGroups))
            {
                foreach (var standing in group.Standings.Where(s => s.Position > 0))
                    allWinnerIds.Add(standing.ParticipantId);
            }

            // All participants from the initial round who are NOT winners
            // Use the full participant list from the competition
            List<Guid> allParticipantIds;
            if (_competition.Format == CompetitionFormat.DoublesGroupStage)
                allParticipantIds = _competition.DoublesTeams.Select(t => t.Id).ToList();
            else
                allParticipantIds = _competition.ParticipantIds;

            var plateParticipants = allParticipantIds
                .Where(p => !allWinnerIds.Contains(p) && !_competition.NoShowIds.Contains(p))
                .ToList();

            if (plateParticipants.Count < 2)
            {
                StatusMessage = $"Not enough losers for a plate ({plateParticipants.Count} found, need at least 2)";
                return;
            }

            var plateSuffix = _competition.GroupSettings?.PlateNameSuffix ?? "Plate";
            var plateComp = new Competition
            {
                Name = $"{_competition.Name} {plateSuffix}",
                Format = _competition.Format,
                Status = CompetitionStatus.Draft,
                SeasonId = _competition.SeasonId,
                StartDate = _competition.StartDate,
                CreatedDate = DateTime.Now,
                BestOf = _competition.BestOf,
                RandomDraw = _competition.RandomDraw,
                ParentCompetitionId = _competition.Id,
                GroupSettings = new GroupStageSettings
                {
                    NumberOfGroups = 0,
                    TopPlayersAdvance = _competition.GroupSettings?.TopPlayersAdvance ?? 2,
                    LowerPlayersToPlate = 0,
                    AllLosersToPlate = false,
                    CreatePlateCompetition = false,
                    PlateNameSuffix = "Plate"
                }
            };

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

            await _competitionStore.AddCompetitionAsync(plateComp);
            _competition.PlateCompetitionId = plateComp.Id;
            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();

            StatusMessage = $"Plate created with {plateParticipants.Count} players: \"{plateComp.Name}\"";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating plate: {ex.Message}";
        }
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

    public Task<List<Player>> GetAvailablePlayersAsync()
    {
        var seasonId = _competition.SeasonId ?? CurrentSeasonId;
        var players = DataStore.Data?.Players?
            .Where(p => p != null && p.SeasonId == seasonId && !_competition.ParticipantIds.Contains(p.Id))
            .OrderBy(p => p.FullName)
            .ToList() ?? new List<Player>();
        return Task.FromResult(players);
    }

    public Task<List<Player>> GetAvailableDoublesPlayersAsync()
    {
        var usedPlayerIds = new HashSet<Guid>();
        foreach (var team in _competition.DoublesTeams)
        {
            usedPlayerIds.Add(team.Player1Id);
            usedPlayerIds.Add(team.Player2Id);
        }
        var seasonId = _competition.SeasonId ?? CurrentSeasonId;
        var players = DataStore.Data?.Players?
            .Where(p => p != null && p.SeasonId == seasonId && !usedPlayerIds.Contains(p.Id))
            .OrderBy(p => p.FullName)
            .ToList() ?? new List<Player>();
        return Task.FromResult(players);
    }

    public Task<List<Team>> GetAvailableTeamsAsync()
    {
        var seasonId = _competition.SeasonId ?? CurrentSeasonId;
        var teams = DataStore.Data?.Teams?
            .Where(t => t != null && t.SeasonId == seasonId && !_competition.ParticipantIds.Contains(t.Id))
            .OrderBy(t => t.Name)
            .ToList() ?? new List<Team>();
        return Task.FromResult(teams);
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

            // Don't allow plates to create their own sub-plates
            if (_competition.ParentCompetitionId.HasValue)
            {
                StatusMessage = "This competition is already a plate/losers cup — cannot create a sub-plate";
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
                Notes = $"Losers Cup for {_competition.Name}",
                ParentCompetitionId = _competition.Id
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
