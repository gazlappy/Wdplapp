using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Tests;

/// <summary>
/// Tests for ExportService — CSV and HTML export generation.
/// </summary>
public class ExportServiceTests
{
    private static (List<Fixture> fixtures, List<Team> teams, Division division, AppSettings settings) CreateSampleData()
    {
        var divId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        var teams = new List<Team>
        {
            new() { Id = teamA, Name = "Alpha", DivisionId = divId },
            new() { Id = teamB, Name = "Beta", DivisionId = divId }
        };

        var fixtures = new List<Fixture>
        {
            new()
            {
                HomeTeamId = teamA,
                AwayTeamId = teamB,
                DivisionId = divId,
                Date = DateTime.Today.AddDays(-7),
                Frames = new List<FrameResult>
                {
                    new() { Number = 1, HomePlayerId = Guid.NewGuid(), AwayPlayerId = Guid.NewGuid(), Winner = FrameWinner.Home },
                    new() { Number = 2, HomePlayerId = Guid.NewGuid(), AwayPlayerId = Guid.NewGuid(), Winner = FrameWinner.Home },
                    new() { Number = 3, HomePlayerId = Guid.NewGuid(), AwayPlayerId = Guid.NewGuid(), Winner = FrameWinner.Away }
                }
            }
        };

        var division = new Division { Id = divId, Name = "Premier" };
        var settings = new AppSettings();

        return (fixtures, teams, division, settings);
    }

    [Fact]
    public void LeagueTableCsv_ContainsHeader()
    {
        var (fixtures, teams, division, settings) = CreateSampleData();
        var csv = ExportService.GenerateLeagueTableCsv(fixtures, teams, division, settings);

        Assert.Contains("Position,Team,Played", csv);
    }

    [Fact]
    public void LeagueTableCsv_ContainsTeamNames()
    {
        var (fixtures, teams, division, settings) = CreateSampleData();
        var csv = ExportService.GenerateLeagueTableCsv(fixtures, teams, division, settings);

        Assert.Contains("Alpha", csv);
        Assert.Contains("Beta", csv);
    }

    [Fact]
    public void FixturesCsv_ContainsAllFixtures()
    {
        var (fixtures, teams, division, settings) = CreateSampleData();
        var venues = new List<Venue>();
        var divisions = new List<Division> { division };

        var csv = ExportService.GenerateFixturesCsv(fixtures, teams, venues, divisions);

        Assert.Contains("Alpha", csv);
        Assert.Contains("Beta", csv);
        Assert.Contains("Completed", csv);
    }

    [Fact]
    public void LeagueTableHtml_IsValidHtml()
    {
        var (fixtures, teams, division, settings) = CreateSampleData();
        var html = ExportService.GenerateLeagueTableHtml(fixtures, teams, division, settings);

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("<table>", html);
        Assert.Contains("</table>", html);
        Assert.Contains("Premier", html);
    }

    [Fact]
    public void CsvEscapes_CommasInTeamName()
    {
        var divId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        var teams = new List<Team>
        {
            new() { Id = teamA, Name = "Alpha, The Best", DivisionId = divId },
            new() { Id = teamB, Name = "Beta", DivisionId = divId }
        };

        var fixtures = new List<Fixture>
        {
            new()
            {
                HomeTeamId = teamA,
                AwayTeamId = teamB,
                DivisionId = divId,
                Date = DateTime.Today,
                Frames = new List<FrameResult>
                {
                    new() { Number = 1, HomePlayerId = Guid.NewGuid(), AwayPlayerId = Guid.NewGuid(), Winner = FrameWinner.Home }
                }
            }
        };

        var csv = ExportService.GenerateLeagueTableCsv(fixtures, teams, new Division { Id = divId }, new AppSettings());

        // Team name with comma should be quoted
        Assert.Contains("\"Alpha, The Best\"", csv);
    }
}
