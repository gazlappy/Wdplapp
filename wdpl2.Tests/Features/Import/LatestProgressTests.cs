using Wdpl2.Services.Import;

namespace Wdpl2.Tests;

public class LatestProgressTests
{
    [Fact]
    public void Burst_RetainsOnlyLatestReport()
    {
        using var progress = new LatestProgress<string>();
        for (var i = 0; i < 43000; i++)
            progress.Report(i.ToString());

        Assert.Equal("42999", progress.TakeLatest());
        Assert.Null(progress.TakeLatest());
    }

    [Fact]
    public void Dispose_ClearsPendingAndIgnoresLateReports()
    {
        var progress = new LatestProgress<string>();
        progress.Report("pending");
        progress.Dispose();
        progress.Report("late");
        Assert.Null(progress.TakeLatest());
        progress.Dispose();
    }

    [Fact]
    public void SubsequentPhase_DoesNotReceiveOldPhaseReports()
    {
        using var scan = new LatestProgress<string>();
        scan.Dispose();
        using var analysis = new LatestProgress<string>();
        analysis.Report("analysis");
        scan.Report("late scan");
        Assert.Null(scan.TakeLatest());
        Assert.Equal("analysis", analysis.TakeLatest());
    }

    [Fact]
    public async Task ConcurrentReports_CannotRestoreProgressAfterDispose()
    {
        var progress = new LatestProgress<string>();
        var reporting = Task.Run(() => Parallel.For(0, 10000, i => progress.Report(i.ToString())));
        progress.Dispose();
        await reporting;
        Assert.Null(progress.TakeLatest());
    }
}
