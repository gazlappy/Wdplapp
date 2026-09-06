using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Tests;

public class SeasonConfigurationSelectionTests
{
    [Fact]
    public void ExplicitInactiveSelection_DoesNotActivateOrFollowWorkingSeason()
    {
        var active = new Season { IsActive = true };
        var inactive = new Season { IsActive = false };
        var seasons = new[] { active, inactive };
        var selection = new SeasonConfigurationSelection();
        selection.Refresh(seasons, active.Id);
        Assert.Equal(active.Id, selection.SeasonId);
        selection.Select(inactive.Id);
        selection.Refresh(seasons, active.Id);
        Assert.Equal(inactive.Id, selection.SeasonId);
        Assert.True(selection.CanEdit(seasons, false));
        Assert.False(inactive.IsActive);
        Assert.True(active.IsActive);
        Assert.False(selection.CanEdit(seasons, true));
        inactive.IsLocked = true;
        Assert.False(selection.CanEdit(seasons, false));
    }

    [Fact]
    public void MissingOrClearedExplicitTarget_DoesNotFallBackToAnotherSeason()
    {
        var active = new Season();
        var removed = new Season();
        var selection = new SeasonConfigurationSelection();
        selection.Select(removed.Id);
        selection.Refresh(new[] { active }, active.Id);
        Assert.Null(selection.SeasonId);
        Assert.False(selection.CanEdit(new[] { active }, false));
        selection.Select(null);
        selection.Refresh(new[] { active }, active.Id);
        Assert.Null(selection.SeasonId);
    }

    [Fact]
    public void WithoutExplicitSelection_FollowsExistingWorkingSeasonOnly()
    {
        var first = new Season();
        var second = new Season();
        var seasons = new[] { first, second };
        var selection = new SeasonConfigurationSelection();
        selection.Refresh(seasons, first.Id);
        selection.Refresh(seasons, second.Id);
        Assert.Equal(second.Id, selection.SeasonId);
        selection.Refresh(seasons, Guid.NewGuid());
        Assert.Null(selection.SeasonId);
    }
}
