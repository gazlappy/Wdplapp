using Wdpl2.Models;
using Wdpl2.ViewModels;

namespace Wdpl2.Tests;

public class SeasonLibraryViewModelTests
{
    [Fact]
    public void Preview_AnotherSeason_DoesNotChangeCurrentOrActiveSeason()
    {
        var current = new Season { Name = "Current", IsActive = true };
        var historical = new Season { Name = "Historical", IsActive = false, IsLocked = true };
        var data = new LeagueData { Seasons = [current, historical], ActiveSeasonId = current.Id };
        var viewModel = new SeasonLibraryViewModel();
        viewModel.Refresh(data, current.Id);

        viewModel.Preview(historical);

        Assert.Same(historical, viewModel.PreviewedSeason);
        Assert.Same(current, viewModel.CurrentSeason!.Season);
        Assert.Equal(current.Id, data.ActiveSeasonId);
        Assert.True(current.IsActive);
        Assert.False(historical.IsActive);
        Assert.True(historical.IsLocked);

        viewModel.ClosePreview();

        Assert.Null(viewModel.PreviewedSeason);
        Assert.Same(current, viewModel.CurrentSeason.Season);
        Assert.Equal(current.Id, data.ActiveSeasonId);
    }

    [Fact]
    public void Refresh_NewCurrentSeason_UpdatesCardsWithoutChangingPreview()
    {
        var first = new Season { Name = "First", IsActive = true };
        var second = new Season { Name = "Second", IsActive = false };
        var data = new LeagueData { Seasons = [first, second] };
        var viewModel = new SeasonLibraryViewModel();
        viewModel.Refresh(data, first.Id);
        viewModel.Preview(first);

        viewModel.Refresh(data, second.Id);

        Assert.Same(first, viewModel.PreviewedSeason);
        Assert.Same(second, viewModel.CurrentSeason!.Season);
        Assert.Equal(second.Id, Assert.Single(viewModel.Groups.SelectMany(g => g).Where(c => c.IsCurrent)).Season.Id);
        Assert.True(first.IsActive);
        Assert.False(second.IsActive);
    }

    [Fact]
    public void Refresh_RemovedPreview_ClearsPreview()
    {
        var season = new Season();
        var data = new LeagueData { Seasons = [season] };
        var viewModel = new SeasonLibraryViewModel();
        viewModel.Preview(season);
        data.Seasons.Clear();

        viewModel.Refresh(data, null);

        Assert.Null(viewModel.PreviewedSeason);
        Assert.Null(viewModel.CurrentSeason);
        Assert.Empty(viewModel.Groups);
        Assert.Equal(0, viewModel.VisibleCount);
    }

    [Theory]
    [InlineData(" winter ", SeasonLibraryFilter.All, 2)]
    [InlineData("2024", SeasonLibraryFilter.All, 1)]
    [InlineData("", SeasonLibraryFilter.Current, 1)]
    [InlineData("", SeasonLibraryFilter.Locked, 1)]
    [InlineData("missing", SeasonLibraryFilter.All, 0)]
    public void Refresh_SearchAndFilter_PreserveCurrentAndPreview(string search, SeasonLibraryFilter filter, int expectedCount)
    {
        var current = new Season { Name = "Winter 2025", StartDate = new DateTime(2025, 10, 1) };
        var historical = new Season { Name = "Winter 2024", StartDate = new DateTime(2024, 10, 1), IsLocked = true };
        var data = new LeagueData { Seasons = [historical, current] };
        var viewModel = new SeasonLibraryViewModel();
        viewModel.Preview(historical);

        viewModel.Refresh(data, current.Id, search, filter);

        Assert.Equal(expectedCount, viewModel.VisibleCount);
        Assert.Same(current, viewModel.CurrentSeason!.Season);
        Assert.Same(historical, viewModel.PreviewedSeason);
    }

    [Fact]
    public void Refresh_GroupsNewestFirst_AndScopesCardCountsToSeason()
    {
        var old = new Season { Name = "Old", StartDate = new DateTime(2024, 1, 1) };
        var current = new Season { Name = "Current", StartDate = new DateTime(2025, 1, 1) };
        var data = new LeagueData
        {
            Seasons = [old, current],
            Teams = [new Team { SeasonId = old.Id }, new Team { SeasonId = current.Id }],
            Players = [new Player { SeasonId = old.Id }],
            Fixtures = [new Fixture { SeasonId = old.Id }]
        };
        var viewModel = new SeasonLibraryViewModel();

        viewModel.Refresh(data, current.Id);

        Assert.Equal(new[] { "2025", "2024" }, viewModel.Groups.Select(g => g.Title));
        Assert.Equal("1 teams · 0 players", viewModel.CurrentSeason!.Summary);
        Assert.Equal(0, viewModel.CurrentSeason.FixtureCount);
        Assert.Equal(0d, viewModel.CurrentSeason.Progress);
        var oldCard = Assert.Single(viewModel.Groups[1]);
        Assert.Equal("1 teams · 1 players", oldCard.Summary);
        Assert.Equal(1, oldCard.FixtureCount);
        Assert.Equal(0, oldCard.CompletedFixtures);
    }
}
