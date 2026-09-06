using Wdpl2.Models;

namespace Wdpl2.Services;

public sealed class SeasonConfigurationSelection
{
    public Guid? SeasonId { get; private set; }
    private bool _explicitSelection;

    public void Select(Guid? seasonId)
    {
        SeasonId = seasonId;
        _explicitSelection = true;
    }

    public void Refresh(IEnumerable<Season> seasons, Guid? workingSeasonId)
    {
        var candidate = _explicitSelection ? SeasonId : workingSeasonId;
        SeasonId = seasons.FirstOrDefault(s => s.Id == candidate)?.Id;
    }

    public bool CanEdit(IEnumerable<Season> seasons, bool browseAll) => !browseAll &&
        seasons.Any(s => s.Id == SeasonId && !s.IsLocked);
}
