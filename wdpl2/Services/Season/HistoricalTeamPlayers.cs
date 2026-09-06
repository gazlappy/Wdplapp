using Wdpl2.Models;
using Wdpl2.Services.Import;

namespace Wdpl2.Services;

public sealed class HistoricalTeamPlayers
{
    private readonly IDataStore _store;
    private readonly ImportWorkspace _workspace;
    private readonly List<Player> _selected = new();
    private bool _saving;
    private bool _saved;
    public LeagueData Preview { get; }
    public Team Destination { get; }
    public Guid SeasonId { get; }
    public IReadOnlyList<Player> Selected => _selected.AsReadOnly();

    public HistoricalTeamPlayers(IDataStore store, Guid teamId)
    {
        _store = store;
        _workspace = new ImportWorkspace(store);
        Preview = _workspace.GetData();
        Destination = Preview.Teams.SingleOrDefault(t => t.Id == teamId)
            ?? throw new InvalidOperationException("Select an existing destination team.");
        SeasonId = Destination.SeasonId ?? throw new InvalidOperationException("The destination team needs a season.");
        var season = Preview.Seasons.SingleOrDefault(s => s.Id == SeasonId)
            ?? throw new InvalidOperationException("The destination season was not found.");
        if (season.IsLocked) throw new InvalidOperationException("The destination season is locked.");
    }

    private static Guid Identity(Player player) => player.GlobalPlayerId ?? player.Id;

    private static bool SameIdentity(Player left, Player right) => Identity(left) == Identity(right) ||
        left.GlobalPlayerId == right.Id || right.GlobalPlayerId == left.Id;

    private List<Player> Matches(Player source, IEnumerable<Player> players) => players
        .Where(p => p.SeasonId == SeasonId && SameIdentity(source, p)).ToList();

    public string? UnavailableReason(Player source)
    {
        if (source.SeasonId == SeasonId || !Preview.Seasons.Any(s => s.Id == source.SeasonId))
            return "Choose a player from another season.";
        var matches = Matches(source, Preview.Players);
        if (matches.Count > 1) return "Multiple matching identities in this season — resolve them on Players first.";
        if (matches.SingleOrDefault() is { TeamId: Guid teamId })
            return teamId == Destination.Id ? "Already on this team" : "Already assigned in this season — use Players to transfer.";
        return null;
    }

    public bool IsSelected(Player source) => _selected.Any(p => SameIdentity(p, source));
    public bool WillAssignExisting(Player source) => Matches(source, Preview.Players).Count == 1;

    public List<Player> SourcePlayers(Guid seasonId, Guid? teamId, string? search) =>
        ManualSeasonRoster.SourceRoster(Preview, seasonId, teamId)
            .Where(p => string.IsNullOrWhiteSpace(search) || p.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

    public void Add(Guid sourceId)
    {
        EnsureEditable();
        var source = Preview.Players.SingleOrDefault(p => p.Id == sourceId)
            ?? throw new InvalidOperationException("Source player not found.");
        if (UnavailableReason(source) is string reason) throw new InvalidOperationException(reason);
        if (!IsSelected(source)) _selected.Add(source);
    }

    public void Remove(Guid sourceId)
    {
        EnsureEditable();
        _selected.RemoveAll(p => p.Id == sourceId);
    }

    private void EnsureEditable()
    {
        if (_saving || _saved) throw new InvalidOperationException("This selection is being saved or has already been added.");
    }

    public async Task SaveAsync()
    {
        EnsureEditable();
        if (_selected.Count == 0) throw new InvalidOperationException("Select at least one player.");
        _saving = true;
        var before = Preview.Players.ToList();
        try
        {
            var currentTeam = (await _store.GetTeamsAsync(SeasonId)).SingleOrDefault(t => t.Id == Destination.Id);
            if (currentTeam == null) throw new InvalidOperationException("The destination team changed or was deleted. Reopen the selection.");
            var currentPlayers = await _store.GetPlayersAsync(SeasonId);
            foreach (var source in _selected)
            {
                if (UnavailableReason(source) is string reason) throw new InvalidOperationException(reason);
                var matches = Matches(source, currentPlayers);
                var original = Matches(source, before).SingleOrDefault();
                if (matches.Count != (original == null ? 0 : 1) ||
                    (original != null && (matches[0].Id != original.Id || !ImportWorkspace.Equal(original, matches[0]))))
                    throw new InvalidOperationException($"'{source.Name}' changed in the destination season. Reopen the selection before saving.");
                var player = original == null ? new Player
                {
                    GlobalPlayerId = Identity(source), FirstName = source.FirstName,
                    LastName = source.LastName, Name = source.Name, IsActive = true,
                    SeasonId = SeasonId
                } : ImportWorkspace.Clone(original);
                player.TeamId = Destination.Id;
                if (original != null) Preview.Players.Remove(original);
                Preview.Players.Add(player);
            }
            await _workspace.SaveAsync();
            _saved = true;
        }
        catch
        {
            Preview.Players.Clear();
            Preview.Players.AddRange(before);
            throw;
        }
        finally { _saving = false; }
    }
}
