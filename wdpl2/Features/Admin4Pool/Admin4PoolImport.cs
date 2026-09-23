using Wdpl2.Models;
using Wdpl2.Services.Import;

namespace Wdpl2.Services.Admin4Pool;

/// <summary>
/// Brings seasons exported from Admin4Pool in as new, inactive seasons, all
/// together or not at all.
/// </summary>
/// <remarks>
/// Works in a private <see cref="ImportWorkspace"/>, so nothing reaches the
/// league until <see cref="SaveAsync"/>, and then only through the same
/// transactional, placement-checked commit every other editor uses. Existing
/// seasons are never touched: a name already in use is refused, not merged.
/// </remarks>
public sealed class Admin4PoolImport
{
    private readonly ImportWorkspace _workspace;
    private readonly List<Admin4PoolSeasonPlan> _plans = [];
    private bool _saved;

    public Admin4PoolImport(IDataStore store) => _workspace = new ImportWorkspace(store);

    public IReadOnlyList<Admin4PoolSeasonPlan> Plans => _plans;

    public Admin4PoolSeasonPlan Add(Admin4PoolSqlFile file)
    {
        var data = _workspace.GetData();
        var plan = Admin4PoolSeasonPlan.Build(file, data.Settings);
        _plans.Add(plan);
        return plan;
    }

    public void Remove(Admin4PoolSeasonPlan plan) => _plans.Remove(plan);

    /// <summary>Why <paramref name="name"/> can't be used for <paramref name="plan"/>, or null if it can.</summary>
    public string? NameProblem(Admin4PoolSeasonPlan plan, string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name)) return "Give the season a name.";
        if (_workspace.GetData().Seasons.Any(s => string.Equals(s.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            return $"There's already a season called \"{name}\". Delete or rename it first, or pick another name.";
        if (_plans.Any(p => p != plan && string.Equals(p.Season.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            return $"Another file here is also called \"{name}\".";
        return null;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (_saved) throw new InvalidOperationException("These seasons have already been imported.");
        if (_plans.Count == 0) throw new InvalidOperationException("Choose at least one season to import.");
        foreach (var plan in _plans)
            if (NameProblem(plan, plan.Season.Name) is { } problem)
                throw new InvalidOperationException(problem);

        var data = _workspace.GetData();
        foreach (var plan in _plans)
        {
            plan.Season.Name = plan.Season.Name.Trim();
            data.Seasons.Add(ImportWorkspace.Clone(plan.Season));
            data.Divisions.AddRange(plan.Divisions.Select(ImportWorkspace.Clone));
            data.Venues.AddRange(plan.Venues.Select(ImportWorkspace.Clone));
            data.Teams.AddRange(plan.Teams.Select(ImportWorkspace.Clone));
            data.Players.AddRange(plan.Players.Select(ImportWorkspace.Clone));
            data.Fixtures.AddRange(plan.Fixtures.Select(ImportWorkspace.Clone));
        }
        try
        {
            await _workspace.SaveAsync(ct);
            _saved = true;
        }
        catch
        {
            _workspace.Reset();
            throw;
        }
    }
}
