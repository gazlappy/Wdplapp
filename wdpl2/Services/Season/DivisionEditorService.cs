using Wdpl2.Models;
using Wdpl2.Services.Import;

namespace Wdpl2.Services;

public sealed class DivisionEditorService(IDataStore store)
{
    private static void ValidateSeason(LeagueData data, Guid seasonId)
    {
        var season = data.Seasons.SingleOrDefault(s => s.Id == seasonId)
            ?? throw new InvalidOperationException("Choose an existing season to configure.");
        if (season.IsLocked) throw new InvalidOperationException("This season is locked. Unlock it on the Seasons page before editing divisions.");
    }

    private static string ValidateName(string name)
    {
        name = name.Trim();
        if (name.Length is 0 or > 100) throw new InvalidOperationException("Enter a division name of 1 to 100 characters.");
        return name;
    }

    public async Task SaveAsync(Guid seasonId, Guid? divisionId, string name, string? notes)
    {
        var workspace = new ImportWorkspace(store);
        var data = workspace.GetData();
        ValidateSeason(data, seasonId);
        name = ValidateName(name);
        if (data.Divisions.Any(d => d.SeasonId == seasonId && d.Id != divisionId && string.Equals(d.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("A division with this name already exists in this season.");
        var division = divisionId.HasValue
            ? data.Divisions.SingleOrDefault(d => d.Id == divisionId && d.SeasonId == seasonId)
                ?? throw new InvalidOperationException("The selected division does not belong to the configured season. Refresh and select it again.")
            : new Division { SeasonId = seasonId };
        division.Name = name;
        division.Notes = notes?.Trim();
        if (divisionId.HasValue) division.ModifiedDate = DateTime.UtcNow;
        else data.Divisions.Add(division);
        await workspace.SaveAsync();
    }

    public async Task DeleteAsync(Guid seasonId, IReadOnlyCollection<Guid> ids)
    {
        var workspace = new ImportWorkspace(store);
        var data = workspace.GetData();
        ValidateSeason(data, seasonId);
        if (ids.Any(id => !data.Divisions.Any(d => d.Id == id && d.SeasonId == seasonId)))
            throw new InvalidOperationException("Select divisions only from the configured season.");
        if (data.Teams.Any(t => t.DivisionId.HasValue && ids.Contains(t.DivisionId.Value)) ||
            data.Fixtures.Any(f => f.DivisionId.HasValue && ids.Contains(f.DivisionId.Value)))
            throw new InvalidOperationException("Move the division's teams and fixtures before deleting it.");
        data.Divisions.RemoveAll(d => ids.Contains(d.Id));
        await workspace.SaveAsync();
    }

    public async Task ImportAsync(Guid seasonId, IEnumerable<(string Name, string? Notes)> rows)
    {
        var workspace = new ImportWorkspace(store);
        var data = workspace.GetData();
        ValidateSeason(data, seasonId);
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name)) continue;
            var name = ValidateName(row.Name);
            var matches = data.Divisions.Where(d => d.SeasonId == seasonId && string.Equals(d.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 1) throw new InvalidOperationException($"Multiple divisions named '{name}' exist. Resolve them before importing.");
            var division = matches.SingleOrDefault();
            if (division == null) data.Divisions.Add(new Division { SeasonId = seasonId, Name = name, Notes = row.Notes });
            else division.Notes = row.Notes;
        }
        await workspace.SaveAsync();
    }
}
