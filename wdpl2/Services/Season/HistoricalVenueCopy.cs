using Wdpl2.Models;
using Wdpl2.Services.Import;

namespace Wdpl2.Services;

public sealed class HistoricalVenueCopy
{
    private readonly ImportWorkspace _workspace;
    private readonly ManualSeasonRoster _draft = new();
    private readonly Dictionary<Guid, Venue> _sources = new();
    private bool _saving;
    private bool _saved;
    public Guid DestinationId { get; }
    public LeagueData Preview { get; }
    public IReadOnlyList<Venue> Selected => _draft.Venues;

    public HistoricalVenueCopy(IDataStore store, Guid destinationId)
    {
        _workspace = new ImportWorkspace(store);
        Preview = _workspace.GetData();
        DestinationId = destinationId;
        ValidateDestination();
    }

    private void ValidateDestination()
    {
        var destination = Preview.Seasons.SingleOrDefault(s => s.Id == DestinationId)
            ?? throw new InvalidOperationException("Choose an existing destination season.");
        if (destination.IsLocked) throw new InvalidOperationException("The destination season is locked.");
    }

    public bool IsSelected(Guid sourceId) => _sources.Values.Any(v => v.Id == sourceId);
    public Venue SourceFor(Guid draftId) => _sources[draftId];

    public void Add(Guid sourceId)
    {
        EnsureEditable();
        var source = Preview.Venues.SingleOrDefault(v => v.Id == sourceId)
            ?? throw new InvalidOperationException("The source venue was not found.");
        if (source.SeasonId == DestinationId || !Preview.Seasons.Any(s => s.Id == source.SeasonId))
            throw new InvalidOperationException("Choose a venue from another season.");
        var copied = _draft.AddHistoricalVenue(source);
        _sources[copied.Id] = source;
    }

    public void Remove(Guid draftId)
    {
        EnsureEditable();
        _draft.RemoveVenue(draftId);
        _sources.Remove(draftId);
    }

    private void EnsureEditable()
    {
        if (_saving || _saved) throw new InvalidOperationException("This venue selection is already being saved or has been imported.");
    }

    public async Task SaveAsync()
    {
        EnsureEditable();
        ValidateDestination();
        if (Selected.Count == 0) throw new InvalidOperationException("Select at least one venue to import.");
        _saving = true;
        var copies = Selected.Select(v => ImportWorkspace.Clone(v)).ToList();
        foreach (var venue in copies) venue.SeasonId = DestinationId;
        Preview.Venues.AddRange(copies);
        try
        {
            await _workspace.SaveAsync();
            _saved = true;
        }
        catch
        {
            var ids = copies.Select(v => v.Id).ToHashSet();
            Preview.Venues.RemoveAll(v => ids.Contains(v.Id));
            throw;
        }
        finally { _saving = false; }
    }
}
