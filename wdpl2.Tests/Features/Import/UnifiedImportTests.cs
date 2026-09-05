using System.Text;
using Wdpl2.Services;
using Wdpl2.Services.Import;

namespace Wdpl2.Tests;

public class UnifiedImportTests
{
    [Fact]
    public void Workspace_RollbackPreservesPreparedSeason_WithoutMutatingSource()
    {
        var source = new Wdpl2.Models.LeagueData();
        var store = new Moq.Mock<IDataStore>();
        store.Setup(s => s.GetData()).Returns(source);
        var workspace = new ImportWorkspace(store.Object);
        var season = new Wdpl2.Models.Season { Name = "Prepared" };
        workspace.GetData().Seasons.Add(season);
        workspace.CreatePreImportSnapshot();
        workspace.GetData().Teams.Add(new Wdpl2.Models.Team { Name = "Pending", SeasonId = season.Id });

        workspace.RestorePreImportSnapshot();

        Assert.Single(workspace.GetData().Seasons);
        Assert.Empty(workspace.GetData().Teams);
        Assert.Empty(source.Seasons);
        workspace.Reset();
        Assert.Empty(workspace.GetData().Seasons);
    }

    [Theory]
    [InlineData("LEAGUE.SQL", ImportFileKind.Sql)]
    [InlineData("teams.CsV", ImportFileKind.Spreadsheet)]
    [InlineData("results.htm", ImportFileKind.Html)]
    [InlineData("league.accdb", ImportFileKind.Access)]
    [InlineData("history.doc", ImportFileKind.Word)]
    [InlineData("Team.DB", ImportFileKind.Paradox)]
    [InlineData("report.pdf", ImportFileKind.Pdf)]
    public void DetectKind_RoutesSupportedFormats(string name, ImportFileKind expected) =>
        Assert.Equal(expected, ImportFileIntake.DetectKind(name));

    [Theory]
    [InlineData("file.exe")]
    [InlineData("legacy.xls")]
    [InlineData("noextension")]
    public void DetectKind_UnsupportedFormat_Throws(string name) =>
        Assert.Throws<InvalidDataException>(() => ImportFileIntake.DetectKind(name));

    [Fact]
    public async Task Intake_RejectsEmptyDuplicateAndRenamedPdf_AndCleansUp()
    {
        string path;
        using (var intake = new ImportFileIntake(Path.GetTempPath()))
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => intake.AddAsync("empty.csv", new MemoryStream()));
            var file = await intake.AddAsync("teams.csv", new MemoryStream(Encoding.UTF8.GetBytes("Team,Points\nA,1")));
            path = file.FilePath;
            Assert.True(File.Exists(path));
            await Assert.ThrowsAsync<InvalidDataException>(() => intake.AddAsync("copy.csv", new MemoryStream(Encoding.UTF8.GetBytes("Team,Points\nA,1"))));
            await Assert.ThrowsAsync<InvalidDataException>(() => intake.AddAsync("fake.pdf", new MemoryStream(Encoding.UTF8.GetBytes("not a PDF"))));
            Assert.Single(intake.Files);
        }
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Intake_CancelledCopy_DoesNotRetainFile()
    {
        using var intake = new ImportFileIntake(Path.GetTempPath());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => intake.AddAsync("test.sql", new MemoryStream([1, 2, 3]), cts.Token));
        Assert.Empty(intake.Files);
    }

    [Fact]
    public async Task Csv_ParsesQuotedMultilineAndEscapedFields()
    {
        var result = await ParseCsv("Player;Team\r\n\"Pat \"\"Ace\"\" Smith\";\"The\nClub\"\r\n");
        Assert.True(result.Success, string.Join("\n", result.Errors));
        var table = Assert.Single(result.Tables);
        Assert.Equal("Pat \"Ace\" Smith", table.Rows[1][0]);
        Assert.Equal("The\nClub", table.Rows[1][1]);
    }

    [Theory]
    [InlineData("Team,Team\nA,B")]
    [InlineData("Team,Points\nA")]
    [InlineData("Team,Points\n\"A,2")]
    [InlineData("Team,Points")]
    public async Task Csv_RejectsMalformedData(string text)
    {
        var result = await ParseCsv(text);
        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    private static async Task<DocumentParser.ParsedDocument> ParseCsv(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
        try
        {
            await File.WriteAllTextAsync(path, text);
            return await DocumentParser.ParseDocumentAsync(path);
        }
        finally { File.Delete(path); }
    }
}
