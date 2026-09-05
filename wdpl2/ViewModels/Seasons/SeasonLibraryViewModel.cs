using Wdpl2.Models;

namespace Wdpl2.ViewModels;

public enum SeasonLibraryFilter { All, Current, Locked }

public sealed class SeasonLibraryViewModel
{
    public IReadOnlyList<SeasonYearGroup> Groups { get; private set; } = [];
    public SeasonCard? CurrentSeason { get; private set; }
    public Season? PreviewedSeason { get; private set; }
    public int VisibleCount => Groups.Sum(g => g.Count);

    public void Preview(Season season) => PreviewedSeason = season;

    public void ClosePreview() => PreviewedSeason = null;

    public void Refresh(LeagueData data, Guid? currentSeasonId, string? search = null,
        SeasonLibraryFilter filter = SeasonLibraryFilter.All)
    {
        var teams = data.Teams.ToLookup(t => t.SeasonId);
        var players = data.Players.ToLookup(p => p.SeasonId);
        var fixtures = data.Fixtures.ToLookup(f => f.SeasonId);
        var cards = data.Seasons.Select(season => new SeasonCard(
            season, season.Id == currentSeasonId,
            teams[season.Id].Count(), players[season.Id].Count(), fixtures[season.Id].ToList(),
            data.GetSettingsForSeason(season.Id).DefaultFramesPerMatch)).ToList();

        CurrentSeason = cards.FirstOrDefault(c => c.IsCurrent);
        if (PreviewedSeason != null)
            PreviewedSeason = data.Seasons.FirstOrDefault(s => s.Id == PreviewedSeason.Id);

        var query = search?.Trim() ?? "";
        Groups = cards
            .Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || c.Season.StartDate.Year.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(c => filter switch
            {
                SeasonLibraryFilter.Current => c.IsCurrent,
                SeasonLibraryFilter.Locked => c.IsLocked,
                _ => true
            })
            .OrderByDescending(c => c.Season.StartDate)
            .ThenBy(c => c.Name)
            .GroupBy(c => c.Season.StartDate.Year)
            .Select(g => new SeasonYearGroup(g.Key, g))
            .ToList();
    }
}

public sealed class SeasonYearGroup : List<SeasonCard>
{
    public string Title { get; }

    public SeasonYearGroup(int year, IEnumerable<SeasonCard> cards) : base(cards)
    {
        Title = year.ToString();
    }
}

public sealed class SeasonCard
{
    public Season Season { get; }
    public string Name => Season.Name;
    public string Dates => $"{Season.StartDate:dd MMM yyyy} – {Season.EndDate:dd MMM yyyy}";
    public bool IsCurrent { get; }
    public bool IsLocked => Season.IsLocked;
    public string Summary { get; }
    public int FixtureCount { get; }
    public int CompletedFixtures { get; }
    public double Progress => FixtureCount == 0 ? 0 : (double)CompletedFixtures / FixtureCount;
    public string ProgressText => FixtureCount == 0
        ? "No fixtures scheduled"
        : $"{CompletedFixtures} of {FixtureCount} fixtures completed";

    public SeasonCard(Season season, bool isCurrent, int teamCount, int playerCount,
        IReadOnlyList<Fixture> fixtures, int defaultFramesPerMatch)
    {
        Season = season;
        IsCurrent = isCurrent;
        Summary = $"{teamCount} teams · {playerCount} players";
        FixtureCount = fixtures.Count;
        var expectedFrames = season.IncludeDoubles
            ? season.SinglesFrameCount + season.DoublesFrameCount
            : season.FramesPerMatch > 0 ? season.FramesPerMatch : defaultFramesPerMatch;
        CompletedFixtures = fixtures.Count(f => f.Frames.Count >= Math.Max(1, expectedFrames)
            && f.Frames.All(frame => frame.Winner != FrameWinner.None));
    }
}
