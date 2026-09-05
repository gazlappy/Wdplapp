using Wdpl2.Models;
using Wdpl2.Services.Import;

namespace Wdpl2.Services;

public sealed class ManualSeasonRoster
{
    private readonly List<Venue> _venues = new();
    private readonly Dictionary<Guid, Guid> _venueIdentities = new();
    private readonly List<Team> _teams = new();
    private readonly List<Player> _players = new();
    private readonly Dictionary<Guid, Guid> _teamIdentities = new();
    private readonly Dictionary<Guid, Guid> _playerIdentities = new();

    public IReadOnlyList<Venue> Venues => _venues.AsReadOnly();
    public IReadOnlyList<Team> Teams => _teams.AsReadOnly();
    public IReadOnlyList<Player> Players => _players.AsReadOnly();

    public Venue? FindVenue(Venue source) => _venueIdentities.TryGetValue(source.Id, out var id)
        ? _venues.Single(v => v.Id == id) : null;

    public Venue AddHistoricalVenue(Venue source)
    {
        if (FindVenue(source) is { } existing) return existing;
        var venue = new Venue
        {
            Name = source.Name,
            Address = source.Address,
            Notes = source.Notes,
            Tables = source.Tables.Select(t => new VenueTable { Label = t.Label, MaxTeams = t.MaxTeams }).ToList()
        };
        _venues.Add(venue);
        _venueIdentities.Add(source.Id, venue.Id);
        return venue;
    }

    public void RemoveVenue(Guid venueId)
    {
        _venues.RemoveAll(v => v.Id == venueId);
        foreach (var key in _venueIdentities.Where(x => x.Value == venueId).Select(x => x.Key).ToList())
            _venueIdentities.Remove(key);
    }

    public Team? FindTeam(Team source) => _teamIdentities.TryGetValue(source.GlobalTeamId ?? source.Id, out var id)
        ? _teams.Single(t => t.Id == id) : null;

    public Player? FindPlayer(Player source) => _playerIdentities.TryGetValue(source.GlobalPlayerId ?? source.Id, out var id)
        ? _players.Single(p => p.Id == id) : null;

    public Team AddHistoricalTeam(Team source)
    {
        if (FindTeam(source) is { } existing) return existing;
        var team = AddTeam(source.Name ?? "");
        team.GlobalTeamId = source.GlobalTeamId ?? source.Id;
        team.ProvidesFood = source.ProvidesFood;
        team.LogoCatalogId = source.LogoCatalogId;
        _teamIdentities.Add(team.GlobalTeamId.Value, team.Id);
        return team;
    }

    public Team AddTeam(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ArgumentException("Enter a team name of 1 to 100 characters.", nameof(name));
        var team = new Team { Name = name.Trim(), GlobalTeamId = Guid.NewGuid() };
        _teams.Add(team);
        return team;
    }

    public void RemoveTeam(Guid teamId)
    {
        if (_players.Any(p => p.TeamId == teamId))
            throw new InvalidOperationException("Move or remove this team's drafted players before removing the team.");
        _teams.RemoveAll(t => t.Id == teamId);
        foreach (var key in _teamIdentities.Where(x => x.Value == teamId).Select(x => x.Key).ToList())
            _teamIdentities.Remove(key);
    }

    public Player AssignPlayer(Player source, Guid destinationTeamId)
    {
        if (!_teams.Any(t => t.Id == destinationTeamId))
            throw new InvalidOperationException("Choose a team in the new season first.");
        var player = FindPlayer(source);
        if (player == null)
        {
            player = new Player
            {
                GlobalPlayerId = source.GlobalPlayerId ?? source.Id,
                FirstName = source.FirstName,
                LastName = source.LastName,
                Name = source.Name,
                IsActive = true
            };
            _players.Add(player);
            _playerIdentities.Add(player.GlobalPlayerId.Value, player.Id);
        }
        player.TeamId = destinationTeamId;
        return player;
    }

    public void RemovePlayer(Guid playerId)
    {
        _players.RemoveAll(p => p.Id == playerId);
        foreach (var key in _playerIdentities.Where(x => x.Value == playerId).Select(x => x.Key).ToList())
            _playerIdentities.Remove(key);
    }

    public void MovePlayer(Guid playerId, Guid destinationTeamId)
    {
        if (!_teams.Any(t => t.Id == destinationTeamId))
            throw new InvalidOperationException("Choose a team in the new season first.");
        _players.Single(p => p.Id == playerId).TeamId = destinationTeamId;
    }

    public static List<Player> SourceRoster(LeagueData data, Guid seasonId, Guid? teamId) => data.Players
        .Where(p => p.SeasonId == seasonId && p.TeamId == teamId)
        .OrderBy(p => p.Name).ThenBy(p => p.Id).ToList();

    public async Task SaveAsync(IDataStore store, Season season, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(season.Name) || season.Name.Length > 100)
            throw new InvalidOperationException("Enter a season name of 1 to 100 characters.");
        if (season.EndDate.Date < season.StartDate.Date)
            throw new InvalidOperationException("The end date must be on or after the start date.");
        if (season.FramesPerMatch < 0)
            throw new InvalidOperationException("Frames per match cannot be negative.");
        if (_players.Any(p => !_teams.Any(t => t.Id == p.TeamId)))
            throw new InvalidOperationException("Every selected player must have a team in the new season.");

        var workspace = new ImportWorkspace(store);
        var data = workspace.GetData();
        if (data.Seasons.Any(s => s.Id == season.Id))
            throw new InvalidOperationException("This season has already been created.");
        var created = ImportWorkspace.Clone(season);
        created.IsActive = false;
        data.Seasons.Add(created);
        foreach (var draft in _venues)
        {
            var venue = ImportWorkspace.Clone(draft);
            venue.SeasonId = created.Id;
            data.Venues.Add(venue);
        }
        foreach (var draft in _teams)
        {
            var team = ImportWorkspace.Clone(draft);
            team.SeasonId = created.Id;
            data.Teams.Add(team);
        }
        foreach (var draft in _players)
        {
            var player = ImportWorkspace.Clone(draft);
            player.SeasonId = created.Id;
            data.Players.Add(player);
        }
        await workspace.SaveAsync(ct);
    }
}
