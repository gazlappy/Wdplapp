using Wdpl2.Services.Import;
using Wdpl2.Services;

namespace Wdpl2.Tests;

public class ReviewPaginationTests
{
    [Fact]
    public void LargeArchive_AllFilesRemainReachableWithoutDuplicates()
    {
        var source = Enumerable.Range(0, 6590).ToList();
        var visited = new List<int>();
        var pageCount = new ReviewPagination<int>(source, 0, 20).PageCount;
        for (var i = 0; i < pageCount; i++)
        {
            var page = new ReviewPagination<int>(source, i, 20);
            Assert.InRange(page.Items.Count, 1, 20);
            visited.AddRange(page.Items);
        }
        Assert.Equal(source, visited);
    }

    [Fact]
    public void Paging_PreservesOriginalSeasonSelections()
    {
        var source = Enumerable.Range(0, 90)
            .Select(_ => new LeagueFileDiscoveryService.SeasonGroup()).ToList();
        var page = new ReviewPagination<LeagueFileDiscoveryService.SeasonGroup>(source, 1, 5);
        Assert.Equal(5, page.Items.Count);
        Assert.Same(source[5], page.Items[0]);
        page.Items[0].IsSelected = false;
        Assert.False(source[5].IsSelected);
        Assert.Equal(90, source.Count);
        Assert.True(source[89].IsSelected);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(21, -1, 0, 20)]
    [InlineData(21, 99, 1, 1)]
    public void PageBounds_AreClamped(int count, int requested, int expectedPage, int expectedCount)
    {
        var page = new ReviewPagination<int>(Enumerable.Range(0, count).ToList(), requested, 20);
        Assert.Equal(expectedPage, page.PageIndex);
        Assert.Equal(expectedCount, page.Items.Count);
        Assert.Equal(expectedPage > 0, page.HasPrevious);
        Assert.Equal(expectedPage < page.PageCount - 1, page.HasNext);
    }

    [Fact]
    public void InvalidPageSize_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReviewPagination<int>(new List<int>(), 0, 0));
    }
}
