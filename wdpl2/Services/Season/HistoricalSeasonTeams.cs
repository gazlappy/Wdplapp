using Wdpl2.Models;
using Wdpl2.Services.Import;

namespace Wdpl2.Services;

public sealed class HistoricalSeasonTeams
{
    private readonly IDataStore _store;
    private readonly ImportWorkspace _workspace;
    private readonly List<Team> _selected = new();
    private bool _saving;
    private bool _saved;
    public LeagueData Preview { get; }
    public Guid SeasonId { get; }
    public IReadOnlyList<Team> Selected => _selected.AsReadOnly();

    public HistoricalSeasonTeams(IDataStore store, Guid seasonId)
    {
        _store = store;
        _workspace = new ImportWorkspace(store);
        Preview = _workspace.GetData();
        SeasonId = seasonId;
        ValidateDestination(Preview.Seasons);
    }

    private void ValidateDestination(IEnumerable<Season> seasons)
    {
        var season = seasons.SingleOrDefault(s => s.Id == SeasonId)
            ?? throw new InvalidOperationException("Choose an existing destination season.");
        if (season.IsLocked) throw new InvalidOperationException("The destination season is locked.");
    }

    private static bool SameIdentity(Team left, Team right) =>
        (left.GlobalTeamId ?? left.Id) == (right.GlobalTeamId ?? right.Id) ||
        left.GlobalTeamId == right.Id || right.GlobalTeamId == left.Id;

    public string? UnavailableReason(Team source)
    {
        if (source.SeasonId == SeasonId || !Preview.Seasons.Any(s => s.Id == source.SeasonId))
            return "Choose a team from another season.";
        if (string.IsNullOrWhiteSpace(source.Name) || source.Name.Trim().Length > 100)
            return "The source team needs a name of 1 to 100 characters.";
        var matches = Preview.Teams.Count(t => t.SeasonId == SeasonId && SameIdentity(t, source));
        return matches > 1 ? "Multiple matching identities in destination — resolve them first." :
            matches == 1 ? "Already in this season" : null;
    }

    public List<Team> SourceTeams(Guid seasonId, string? search) => Preview.Teams
        .Where(t => t.SeasonId == seasonId && seasonId != SeasonId)
        .Where(t => string.IsNullOrWhiteSpace(search) || (t.Name ?? "").Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
        .OrderBy(t => t.Name).ThenBy(t => t.Id).ToList();

    public bool IsSelected(Team source) => _selected.Any(t => SameIdentity(t, source));

    public void Add(Guid sourceId)
    {
        EnsureEditable();
        var source = Preview.Teams.SingleOrDefault(t => t.Id == sourceId)
            ?? throw new InvalidOperationException("Source team not found.");
        if (UnavailableReason(source) is string reason) throw new InvalidOperationException(reason);
        if (!IsSelected(source)) _selected.Add(source);
    }

    public void Remove(Guid sourceId)
    {
        EnsureEditable();
        _selected.RemoveAll(t => t.Id == sourceId);
    }

    private void EnsureEditable()
    {
        if (_saving || _saved) throw new InvalidOperationException("This selection is being saved or has already been added.");
    }

    public async Task SaveAsync()
    {
        EnsureEditable();
        if (_selected.Count == 0) throw new InvalidOperationException("Select at least one team.");
        _saving = true;
        var before = Preview.Teams.ToList();
        try
        {
            ValidateDestination(await _store.GetSeasonsAsync());
            var currentTeams = await _store.GetTeamsAsync(SeasonId);
            var roster = new ManualSeasonRoster();
            foreach (var source in _selected)
            {
                if (UnavailableReason(source) is string reason) throw new InvalidOperationException(reason);
                if (currentTeams.Any(t => SameIdentity(t, source)))
                    throw new InvalidOperationException($"'{source.Name}' is now in the destination season. Reopen the selection before saving.");
                var copy = roster.AddHistoricalTeam(source);
                copy.SeasonId = SeasonId;
                Preview.Teams.Add(copy);
            }
            await _workspace.SaveAsync();
            _saved = true;
        }
        catch
        {
            Preview.Teams.Clear();
            Preview.Teams.AddRange(before);
            throw;
        }
        finally { _saving = false; }
    }
}
