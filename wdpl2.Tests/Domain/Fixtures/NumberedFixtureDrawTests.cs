using Wdpl2.Services;

namespace wdpl2.Tests;

public class NumberedFixtureDrawTests
{
    [Fact]
    public void EightSlots_MatchesExampleWithCorrectedThirdWeek()
    {
        var draw = NumberedFixtureDraw.Create(8, 2);
        Assert.Equal(new[] { (1, 2), (3, 4), (5, 6), (7, 8) }, draw.Where(p => p.Round == 0).Select(p => (p.Home, p.Away)));
        Assert.Equal(new[] { (4, 1), (2, 5), (8, 3), (6, 7) }, draw.Where(p => p.Round == 1).Select(p => (p.Home, p.Away)));
        Assert.Equal(new[] { (1, 5), (4, 8), (7, 2), (6, 3) }, draw.Where(p => p.Round == 2).Select(p => (p.Home, p.Away)));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(16)]
    public void EveryRound_CompleteOppositeTablePartnersAndAlternatingNumberOne(int slots)
    {
        var draw = NumberedFixtureDraw.Create(slots, 4);
        foreach (var round in draw.GroupBy(p => p.Round))
        {
            Assert.Equal(slots, round.SelectMany(p => new[] { p.Home, p.Away }).Distinct().Count());
            Assert.Equal(slots / 2, round.Count());
            var first = round.First();
            Assert.Equal(1, round.Key % 2 == 0 ? first.Home : first.Away);
            var home = round.Select(p => p.Home).ToHashSet();
            for (int odd = 1; odd <= slots; odd += 2)
                Assert.NotEqual(home.Contains(odd), home.Contains(odd + 1));
        }
        for (int a = 1; a <= slots; a++)
            for (int b = a + 1; b <= slots; b++)
            {
                Assert.Equal(2, draw.Count(p => p.Home == a && p.Away == b));
                Assert.Equal(2, draw.Count(p => p.Home == b && p.Away == a));
            }
        Assert.False(NumberedFixtureDraw.AreTablePartners(4, 5));
    }
}
