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

    [ObservableProperty]
    private bool _isLocked;

    [ObservableProperty]
    private bool _showOnWebsite = true;

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
        if (CheckSeasonLocked()) return;

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
        if (CheckSeasonLocked()) return;

        if (_competition.GroupSettings == null)
            _competition.GroupSettings = new GroupStageSettings();

        _competition.GroupSettings.GroupDate = date;
        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();
        StatusMessage = date.HasValue ? $"Group date set to {date.Value:dd MMM yyyy}" : "Group date cleared";
    }

    /// <summary>
    /// Save the competition-level default Best Of value (inherited by all rounds unless overridden).
    /// Pass 0 for "Unlimited" or a positive integer for a specific best-of value.
    /// </summary>
    public async Task SaveCompetitionBestOfAsync(int bestOf)
    {
        if (CheckSeasonLocked()) return;

        _competition.BestOf = bestOf;
        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();
        StatusMessage = bestOf > 0 ? $"Default set to Best of {bestOf}" : "Default set to Unlimited";
    }

    /// <summary>
    /// Save date and table selections for a specific KO round.
    /// Automatically assigns matches to the selected tables round-robin.
    /// </summary>
    public async Task SaveRoundDetailsAsync(Guid roundId, DateTime? date, List<CompetitionVenue>? venues, int? bestOf = null, bool clearBestOf = false)
    {
        if (CheckSeasonLocked()) return;

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
        if (clearBestOf) round.BestOf = null;
        else if (bestOf.HasValue) round.BestOf = bestOf.Value;

        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();

        // Auto-create / update / remove a calendar event for this round
        if (date.HasValue)
            SyncRoundCalendarEvent(round);

        var datePart = round.Date.HasValue ? round.Date.Value.ToString("dd MMM yyyy") : "no date";
        StatusMessage = $"{round.Name}: {datePart}, {round.TotalTables} table(s)";
    }

    /// <summary>
    /// Create or update a <see cref="CalendarEvent"/> linked to the given competition round.
    /// Also syncs a blackout / exclusion date on the season so league fixtures are not
    /// scheduled on the same date as a competition round.
    /// If the round has no date the existing event (if any) is removed.
    /// </summary>
    private void SyncRoundCalendarEvent(CompetitionRound round)
    {
        var events = DataStore.Data.CalendarEvents;
        var existing = events.FirstOrDefault(e =>
            e.CompetitionId == _competition.Id && e.RoundId == round.Id);

        var title = $"{_competition.Name} — {round.Name}";
        var previousDate = existing?.Date;

        if (!round.Date.HasValue)
        {
            if (existing != null)
                events.Remove(existing);
            SyncRoundBlackoutDate(round, previousDate, title);
            DataStore.SaveJsonOnly();
            return;
        }

        if (existing != null)
        {
            existing.Date = round.Date.Value.Date;
            existing.Title = title;
        }
        else
        {
            events.Add(new CalendarEvent
            {
                Date = round.Date.Value.Date,
                Title = title,
                Category = CalendarEventCategory.Competition,
                CompetitionId = _competition.Id,
                RoundId = round.Id
            });
        }

        SyncRoundBlackoutDate(round, previousDate, title);

        // Use SaveJsonOnly to avoid SyncEntitiesToDatabase overwriting
        // competition data that was just saved via the SQLite store.
        DataStore.SaveJsonOnly();
    }

    /// <summary>
    /// Add or update a blackout / exclusion date on the season for a competition round.
    /// Only removes the old blackout if its title matches (so user-created blackouts are preserved).
    /// </summary>
    private void SyncRoundBlackoutDate(CompetitionRound round, DateTime? previousDate, string title)
    {
        var seasonId = _competition.SeasonId ?? CurrentSeasonId;
        if (!seasonId.HasValue) return;

        var season = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == seasonId.Value);
        if (season == null) return;

        // Remove previous blackout for this round (only if the title matches ours)
        if (previousDate.HasValue)
        {
            var prevKey = previousDate.Value.Date.ToString("yyyy-MM-dd");
            if (season.BlackoutDateTitles.TryGetValue(prevKey, out var prevTitle) && prevTitle == title)
            {
                season.BlackoutDates.RemoveAll(d => d.Date == previousDate.Value.Date);
                season.BlackoutDateTitles.Remove(prevKey);
            }
        }

        // Add new blackout date for the round
        if (round.Date.HasValue)
        {
            var newDate = round.Date.Value.Date;
            var newKey = newDate.ToString("yyyy-MM-dd");
            if (!season.BlackoutDates.Any(d => d.Date == newDate))
                season.BlackoutDates.Add(newDate);
            season.BlackoutDateTitles[newKey] = title;
        }
    }

    /// <summary>
    /// Randomly reassign matches to tables within a round (shuffle).
    /// </summary>
    public async Task RandomiseVenueAssignmentsAsync(Guid roundId)
    {
        if (CheckSeasonLocked()) return;

        var round = _competition.Rounds.FirstOrDefault(r => r.Id == roundId);
        if (round == null)
        {
            StatusMessage = "Round not found";
            return;
        }

        if (round.SelectedVenues.Count == 0 || round.TotalTables == 0)
        {
            StatusMessage = "No tables selected to randomise";
            return;
        }

        CompetitionGenerator.ShuffleMatchVenueTables(round.Matches, round.SelectedVenues);

        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();

        StatusMessage = $"🎲 {round.Name}: venues randomised";
    }

    /// <summary>
    /// Apply pre-selected venues/tables (stored in GroupSettings before the draw)
    /// to the first knockout round after bracket generation.
    /// Also copies the pre-draw date to round 1.
    /// </summary>
    private void ApplyPreDrawVenuesToFirstRound()
    {
        if (_competition.GroupSettings == null) return;
        if (_competition.Rounds.Count == 0) return;

        // Only apply for knockout formats (group stage handles this differently)
        if (_competition.Format is CompetitionFormat.SinglesGroupStage or CompetitionFormat.DoublesGroupStage)
            return;

        var firstRound = _competition.Rounds.OrderBy(r => r.RoundNumber).First();
        var preDrawVenues = _competition.GroupSettings.SelectedVenues;
        var preDrawDate = _competition.GroupSettings.GroupDate;

        if (preDrawDate.HasValue)
            firstRound.Date = preDrawDate;

        if (preDrawVenues.Count > 0)
        {
            firstRound.SelectedVenues = preDrawVenues;
            CompetitionGenerator.AssignMatchVenueTables(firstRound.Matches, preDrawVenues);
        }
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

        return CollectTablesOnDate(parentComp, date);
    }

    /// <summary>
    /// Get tables that are in use by ANY other competition in the same season on a given date.
    /// Returns a dictionary mapping table ID → the name of the competition using it.
    /// </summary>
    public async Task<Dictionary<Guid, string>> GetTablesInUseByOtherCompsOnDateAsync(DateTime date)
    {
        var seasonId = _competition.SeasonId ?? CurrentSeasonId;
        var allComps = await _competitionStore.GetCompetitionsAsync(seasonId);

        var result = new Dictionary<Guid, string>();
        foreach (var comp in allComps)
        {
            if (comp.Id == _competition.Id) continue; // skip self

            var tableIds = CollectTablesOnDate(comp, date);
            foreach (var tableId in tableIds)
            {
                result.TryAdd(tableId, comp.Name);
            }
        }
        return result;
    }

    /// <summary>
    /// Collect all table IDs used by a competition on a specific date.
    /// </summary>
    private static List<Guid> CollectTablesOnDate(Competition comp, DateTime date)
    {
        var usedTableIds = new List<Guid>();

        // Check group stage tables (if groups match the date)
        if (comp.GroupSettings?.GroupDate?.Date == date.Date)
        {
            usedTableIds.AddRange(
                comp.GroupSettings.SelectedVenues
                    .SelectMany(v => v.SelectedTables)
                    .Select(t => t.TableId));
        }

        // Check KO round tables
        foreach (var round in comp.Rounds.Where(r => r.Date?.Date == date.Date))
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
        if (CheckSeasonLocked()) return;

        if (_competition.GroupSettings == null)
            _competition.GroupSettings = new GroupStageSettings();

        _competition.GroupSettings.NumberOfGroups = groupCount;
        await _competitionStore.UpdateCompetitionAsync(_competition);
        await _competitionStore.SaveAsync();
        StatusMessage = $"Group count set to {groupCount}";
    }

    /// <summary>
    /// Change the number of groups and regenerate. Only safe before knockout rounds exist.
    /// Clears existing groups (current round only — preserves PreviousGroups archive)
    /// and regenerates with the new count using the existing participants.
    /// </summary>
    public async Task ChangeGroupCountAndRegenerateAsync(int newGroupCount)
    {
        if (CheckSeasonLocked()) return;

        if (_competition.GroupSettings == null)
        {
            StatusMessage = "No group settings configured";
            return;
        }

        if (_competition.Rounds.Count > 0)
        {
            StatusMessage = "Cannot change group count once knockout has been created";
            return;
        }

        if (newGroupCount < 1)
        {
            StatusMessage = "Group count must be at least 1";
            return;
        }

        try
        {
            // Preserve current group round number so PreviousGroups stays consistent
            int currentRound = _competition.Groups.Count > 0
                ? _competition.Groups.Max(g => g.GroupRound)
                : 1;

            // Manual-draw scenario: existing groups have no participants assigned.
            // Just resize the empty group containers without randomizing anything.
            bool isManualDraw = _competition.Groups.Count > 0
                && _competition.Groups.All(g => g.ParticipantIds.Count == 0);

            if (isManualDraw)
            {
                var resized = new List<CompetitionGroup>();
                for (int i = 0; i < newGroupCount; i++)
                {
                    if (i < _competition.Groups.Count)
                    {
                        var existing = _competition.Groups[i];
                        existing.GroupNumber = i + 1;
                        existing.Name = $"Group {(char)('A' + i)}";
                        existing.GroupRound = currentRound;
                        resized.Add(existing);
                    }
                    else
                    {
                        resized.Add(new CompetitionGroup
                        {
                            Name = $"Group {(char)('A' + i)}",
                            GroupNumber = i + 1,
                            GroupRound = currentRound
                        });
                    }
                }

                CompetitionGenerator.AssignVenueTables(resized, _competition.GroupSettings.SelectedVenues);

                _competition.Groups = resized;
                _competition.GroupSettings.NumberOfGroups = newGroupCount;

                await _competitionStore.UpdateCompetitionAsync(_competition);
                await _competitionStore.SaveAsync();
                StatusMessage = $"Resized to {newGroupCount} empty groups";
                return;
            }

            // Use participants currently in the active groups (preserves any manual additions/removals)
            var participants = _competition.Groups.Count > 0
                ? _competition.Groups.SelectMany(g => g.ParticipantIds).Distinct().ToList()
                : (_competition.Format == CompetitionFormat.DoublesGroupStage
                    ? _competition.DoublesTeams.Select(t => t.Id).ToList()
                    : _competition.ParticipantIds.ToList());

            if (participants.Count < newGroupCount * 2)
            {
                StatusMessage = $"Need at least {newGroupCount * 2} participants for {newGroupCount} groups";
                return;
            }

            _competition.GroupSettings.NumberOfGroups = newGroupCount;

            var (groups, _) = CompetitionGenerator.GenerateGroupStage(
                participants,
                _competition.GroupSettings,
                _competition.Format,
                _competition.SeasonId,
                _competition.Name,
                randomize: true
            );

            // Preserve the round number on regenerated groups
            foreach (var g in groups)
                g.GroupRound = currentRound;

            _competition.Groups = groups;

            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();
            StatusMessage = $"Regenerated into {groups.Count} groups";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error regenerating groups: {ex.Message}";
        }
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

    /// <summary>
    /// Returns true and sets StatusMessage if the competition's season is locked
    /// OR if the competition itself has been locked by the user.
    /// </summary>
    private bool CheckSeasonLocked()
    {
        if (DataStore.Data.IsSeasonLocked(_competition.SeasonId))
        {
            StatusMessage = "Cannot modify — season is locked";
            return true;
        }
        if (_competition.IsLocked)
        {
            StatusMessage = "Cannot modify — competition is locked. Unlock it to make changes.";
            return true;
        }
        return false;
    }

    private void LoadCompetitionData()
    {
        Name = _competition.Name;
        Status = _competition.Status;
        StartDate = _competition.StartDate ?? DateTime.Today;
        Notes = _competition.Notes ?? "";
        IsLocked = _competition.IsLocked;
        ShowOnWebsite = _competition.ShowOnWebsite;
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
            // Allow toggling the lock + website-visibility flags even when the
            // competition is locked — these are the only two settings that can
            // change a locked competition (otherwise you could never unlock it).
            if (DataStore.Data.IsSeasonLocked(_competition.SeasonId))
            {
                StatusMessage = "Cannot modify — season is locked";
                return;
            }

            // Apply lock + website toggles first (these always allowed).
            _competition.IsLocked = IsLocked;
            _competition.ShowOnWebsite = ShowOnWebsite;

            // The remaining edits are only applied if the competition isn't locked.
            if (!_competition.IsLocked)
            {
                _competition.Name = Name;
                _competition.Status = Status;
                _competition.StartDate = StartDate;
                _competition.Notes = Notes;
            }

            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();

            StatusMessage = _competition.IsLocked ? "Competition saved (locked)" : "Competition saved";
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
        if (CheckSeasonLocked()) return;

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
        if (CheckSeasonLocked()) return;

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
        if (CheckSeasonLocked()) return;

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
        if (CheckSeasonLocked()) return;

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

            ApplyPreDrawVenuesToFirstRound();

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
        if (CheckSeasonLocked()) return;

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

            ApplyPreDrawVenuesToFirstRound();

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
        if (CheckSeasonLocked()) return;

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

            ApplyPreDrawVenuesToFirstRound();

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
        if (CheckSeasonLocked()) return;

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

    /// <summary>
    /// Get all participants in the competition (for manual reassignment dialogs
    /// where the user may pick any participant, not just unassigned ones).
    /// </summary>
    public List<ParticipantItem> GetAllParticipants() => Participants.ToList();

    /// <summary>
    /// Clear a participant slot in a match.
    /// </summary>
    public async Task ClearMatchSlotAsync(Guid matchId, bool isSlot1)
    {
        if (CheckSeasonLocked()) return;

        try
        {
            foreach (var round in _competition.Rounds)
            {
                var match = round.Matches.FirstOrDefault(m => m.Id == matchId);
                if (match != null)
                {
                    if (isSlot1) match.Participant1Id = null;
                    else match.Participant2Id = null;
                    await _competitionStore.UpdateCompetitionAsync(_competition);
                    await _competitionStore.SaveAsync();
                    StatusMessage = "Slot cleared";
                    return;
                }
            }
            StatusMessage = "Match not found";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error clearing slot: {ex.Message}";
        }
    }

    /// <summary>
    /// Swap home/away participants in a match.
    /// </summary>
    public async Task SwapMatchParticipantsAsync(Guid matchId)
    {
        if (CheckSeasonLocked()) return;

        try
        {
            foreach (var round in _competition.Rounds)
            {
                var match = round.Matches.FirstOrDefault(m => m.Id == matchId);
                if (match != null)
                {
                    (match.Participant1Id, match.Participant2Id) = (match.Participant2Id, match.Participant1Id);
                    (match.Participant1Score, match.Participant2Score) = (match.Participant2Score, match.Participant1Score);
                    await _competitionStore.UpdateCompetitionAsync(_competition);
                    await _competitionStore.SaveAsync();
                    StatusMessage = "Swapped home/away";
                    return;
                }
            }
            StatusMessage = "Match not found";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error swapping: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task GenerateGroupsAsync()
    {
        if (CheckSeasonLocked()) return;

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
        if (CheckSeasonLocked()) return;

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
    /// Clear all groups for the current group stage. Removes participant assignments,
    /// matches and standings so the user can start over. Knockout rounds (if any)
    /// must be removed first. Previous (archived) group rounds are preserved.
    /// </summary>
    public async Task ClearAllGroupsAsync()
    {
        if (CheckSeasonLocked()) return;

        if (_competition.Rounds.Count > 0)
        {
            StatusMessage = "Can't clear groups — knockout rounds already created";
            return;
        }

        if (_competition.Groups.Count == 0)
        {
            StatusMessage = "No groups to clear";
            return;
        }

        try
        {
            int cleared = _competition.Groups.Count;
            _competition.Groups.Clear();

            // Reset status to Draft if nothing else is in progress
            if (_competition.PreviousGroups.Count == 0)
                _competition.Status = CompetitionStatus.Draft;

            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();

            HasGroups = false;
            StatusMessage = $"Cleared {cleared} group(s)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error clearing groups: {ex.Message}";
        }
    }

    /// <summary>
    /// Move a participant from their current group into a different group.
    /// Regenerates the round-robin matches for both affected groups and clears
    /// any previous standings/winner selections (since the makeup changed).
    /// Only works in the current (latest) group round and before any KO rounds exist.
    /// </summary>
    public async Task MoveParticipantToGroupAsync(Guid participantId, Guid targetGroupId)
    {
        if (CheckSeasonLocked()) return;

        if (_competition.Groups.Count == 0)
        {
            StatusMessage = "No groups";
            return;
        }

        if (_competition.Rounds.Count > 0)
        {
            StatusMessage = "Can't move — knockout rounds already created";
            return;
        }

        try
        {
            int latestRound = _competition.Groups.Max(g => g.GroupRound);
            var sourceGroup = _competition.Groups
                .FirstOrDefault(g => g.GroupRound == latestRound && g.ParticipantIds.Contains(participantId));
            var targetGroup = _competition.Groups
                .FirstOrDefault(g => g.GroupRound == latestRound && g.Id == targetGroupId);

            if (sourceGroup == null || targetGroup == null)
            {
                StatusMessage = "Group not found";
                return;
            }
            if (sourceGroup.Id == targetGroup.Id)
            {
                StatusMessage = "Already in that group";
                return;
            }

            sourceGroup.ParticipantIds.Remove(participantId);
            targetGroup.ParticipantIds.Add(participantId);

            // Rebuild round-robin matches and clear standings for both groups
            RebuildGroupMatches(sourceGroup);
            RebuildGroupMatches(targetGroup);
            sourceGroup.Standings.Clear();
            targetGroup.Standings.Clear();

            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();
            StatusMessage = $"Moved to {targetGroup.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error moving participant: {ex.Message}";
        }
    }

    private static void RebuildGroupMatches(CompetitionGroup group)
    {
        var matches = new List<CompetitionMatch>();
        var ids = group.ParticipantIds;
        for (int i = 0; i < ids.Count; i++)
        {
            for (int j = i + 1; j < ids.Count; j++)
            {
                matches.Add(new CompetitionMatch
                {
                    Participant1Id = ids[i],
                    Participant2Id = ids[j],
                    GroupId = group.Id
                });
            }
        }
        group.Matches = matches;
    }

    /// <summary>
    /// Create empty groups for a manual draw. The user will then drag/assign
    /// each participant into the group of their choice via the groups view.
    /// </summary>
    public async Task GenerateEmptyGroupsAsync()
    {
        if (CheckSeasonLocked()) return;

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
            int n = _competition.GroupSettings.NumberOfGroups;
            var groups = new List<CompetitionGroup>();
            for (int i = 0; i < n; i++)
            {
                groups.Add(new CompetitionGroup
                {
                    Name = $"Group {(char)('A' + i)}",
                    GroupNumber = i + 1
                });
            }

            // Assign venue tables (groups are empty but venue mapping still applies)
            CompetitionGenerator.AssignVenueTables(groups, _competition.GroupSettings.SelectedVenues);

            _competition.Groups = groups;
            _competition.Status = CompetitionStatus.InProgress;

            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();

            HasGroups = true;
            StatusMessage = $"Created {n} empty groups — drag players in";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error creating empty groups: {ex.Message}";
        }
    }

    /// <summary>
    /// Add a participant to a specific group (for manual draw). Rebuilds that
    /// group's round-robin matches and clears its standings.
    /// </summary>
    public async Task AssignParticipantToGroupAsync(Guid participantId, Guid targetGroupId)
    {
        if (CheckSeasonLocked()) return;

        if (_competition.Rounds.Count > 0)
        {
            StatusMessage = "Can't assign — knockout rounds already created";
            return;
        }

        try
        {
            int latestRound = _competition.Groups.Count > 0
                ? _competition.Groups.Max(g => g.GroupRound)
                : 1;

            // Remove from any other group in the latest round first
            foreach (var g in _competition.Groups.Where(g => g.GroupRound == latestRound))
            {
                if (g.ParticipantIds.Remove(participantId))
                    RebuildGroupMatches(g);
            }

            var target = _competition.Groups.FirstOrDefault(g => g.Id == targetGroupId);
            if (target == null) { StatusMessage = "Group not found"; return; }

            target.ParticipantIds.Add(participantId);
            RebuildGroupMatches(target);
            target.Standings.Clear();

            await _competitionStore.UpdateCompetitionAsync(_competition);
            await _competitionStore.SaveAsync();
            StatusMessage = $"Assigned to {target.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error assigning: {ex.Message}";
        }
    }

    /// <summary>
    /// Remove a participant from whichever group currently holds them (manual draw undo).
    /// </summary>
    public async Task RemoveParticipantFromGroupsAsync(Guid participantId)
    {
        if (CheckSeasonLocked()) return;

        if (_competition.Rounds.Count > 0)
        {
            StatusMessage = "Can't unassign — knockout rounds already created";
            return;
        }

        try
        {
            int latestRound = _competition.Groups.Count > 0
                ? _competition.Groups.Max(g => g.GroupRound)
                : 1;

            bool changed = false;
            foreach (var g in _competition.Groups.Where(g => g.GroupRound == latestRound))
            {
                if (g.ParticipantIds.Remove(participantId))
                {
                    RebuildGroupMatches(g);
                    g.Standings.Clear();
                    changed = true;
                }
            }

            if (changed)
            {
                await _competitionStore.UpdateCompetitionAsync(_competition);
                await _competitionStore.SaveAsync();
                StatusMessage = "Returned to unassigned pool";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error removing: {ex.Message}";
        }
    }

    /// <summary>
    /// Get participant IDs that aren't yet placed in any group of the latest group round.
    /// Used by the manual-draw UI to show an "Unassigned" pool.
    /// </summary>
    public List<Guid> GetUnassignedGroupParticipants()
    {
        var allParticipants = _competition.Format == CompetitionFormat.DoublesGroupStage
            ? _competition.DoublesTeams.Select(t => t.Id).ToList()
            : _competition.ParticipantIds.ToList();

        if (_competition.Groups.Count == 0)
            return allParticipants;

        int latestRound = _competition.Groups.Max(g => g.GroupRound);
        var assigned = new HashSet<Guid>(_competition.Groups
            .Where(g => g.GroupRound == latestRound)
            .SelectMany(g => g.ParticipantIds));

        return allParticipants.Where(id => !assigned.Contains(id)).ToList();
    }

    /// <summary>
    /// Archive the current groups, take the selected winners, and create a new round of groups.
    /// </summary>
    public async Task AdvanceToNextGroupRoundAsync(int newGroupCount, int advancePerGroup)
    {
        if (CheckSeasonLocked()) return;

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
        if (CheckSeasonLocked()) return;

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
        if (CheckSeasonLocked()) return;

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
        if (CheckSeasonLocked()) return;

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
        if (CheckSeasonLocked()) return;

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
        if (CheckSeasonLocked()) return;

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
        if (CheckSeasonLocked()) return;

        bool anyUpdates = false;

        foreach (var round in _competition.Rounds)
        {
            int ftw = round.GetFramesToWin(_competition); // 0 = unlimited / not set

            foreach (var match in round.Matches)
            {
                if (!match.Participant1Id.HasValue || !match.Participant2Id.HasValue)
                    continue;

                Guid? winnerId = null;

                if (ftw > 0)
                {
                    // Best-of mode: only complete when a player reaches the winning score
                    if (match.Participant1Score >= ftw)
                        winnerId = match.Participant1Id;
                    else if (match.Participant2Score >= ftw)
                        winnerId = match.Participant2Id;
                }
                else
                {
                    // Unlimited mode: whoever is ahead wins (scores must differ)
                    if (match.Participant1Score > match.Participant2Score)
                        winnerId = match.Participant1Id;
                    else if (match.Participant2Score > match.Participant1Score)
                        winnerId = match.Participant2Id;
                }

                if (winnerId.HasValue)
                {
                    bool changed = !match.IsComplete || match.WinnerId != winnerId;
                    match.WinnerId = winnerId;
                    match.IsComplete = true;
                    if (changed)
                    {
                        anyUpdates = true;
                        AdvanceWinner(round, match);
                    }
                }
                else if (match.IsComplete)
                {
                    // Score was edited so there's no longer a clear winner — revert to incomplete
                    match.WinnerId = null;
                    match.IsComplete = false;
                    anyUpdates = true;
                    ClearAdvancement(round, match);
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

    private void ClearAdvancement(CompetitionRound round, CompetitionMatch match)
    {
        var nextRound = _competition.Rounds.FirstOrDefault(r => r.RoundNumber == round.RoundNumber + 1);
        if (nextRound == null) return;

        int matchIndex = round.Matches.IndexOf(match);
        if (matchIndex < 0) return;

        int nextMatchIndex = matchIndex / 2;
        if (nextMatchIndex >= nextRound.Matches.Count) return;

        var nextMatch = nextRound.Matches[nextMatchIndex];
        if (matchIndex % 2 == 0)
            nextMatch.Participant1Id = null;
        else
            nextMatch.Participant2Id = null;
    }

    /// <summary>
    /// Creates a new "Losers Cup" competition populated with the losers
    /// from the first round of this knockout bracket.
    /// </summary>
    [RelayCommand]
    private async Task CreateLosersCupAsync()
    {
        if (CheckSeasonLocked()) return;

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
        if (CheckSeasonLocked()) return;

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
