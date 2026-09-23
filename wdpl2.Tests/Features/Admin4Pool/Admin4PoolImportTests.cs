using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wdpl2.Data;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Admin4Pool;

namespace Wdpl2.Tests;

[Collection(SharedDataStoreCollection.Name)]
public class Admin4PoolImportTests : SharedDataStoreTest
{
    // A small season in the shape Admin4Pool's File > Export Season to SQL writes.
    // Two 3-frame singles matches and one doubles frame; team 3 plays itself once.
    private const string Export = """
        -- Admin4Pool season export
        -- League: Test League
        /*!40101 SET NAMES latin1 */;
        /*!40101 SET sql_mode = 'NO_BACKSLASH_ESCAPES' */;
        BEGIN;
        DROP TABLE IF EXISTS `League`;
        CREATE TABLE `League` (
          `LeagueName` VARCHAR(40),
          `Season` VARCHAR(20)
        );
        INSERT INTO `League` (`LeagueName`, `Season`, `NoSingles`, `NoDoubles`, `SinglesBonus`, `DoublesBonus`, `WinBonus`, `DrawBonus`, `LossBonus`, `WinFactor`, `LossFactor`, `EightBallFactor`, `UpdateRequired`, `MaxSingles`, `Explanation`) VALUES ('Test League', 'Winter 2000/2001', 3, 1, 1, 1, 2, 0, 0, 1.25, 0.75, 1.35, 0, 1, 'Line one
        it''s line two');
        INSERT INTO `Division` (`Item_id`, `Abbreviated`, `FullDivisionName`) VALUES (1, 'Prem', 'Premier');
        INSERT INTO `Venue` (`Item_id`, `Venue`, `AddressLine1`, `AddressLine2`, `AddressLine3`, `AddressLine4`) VALUES (1, 'The Dolphin', 'Waterloo Road', 'Wellington', NULL, NULL);
        INSERT INTO `Team` (`Item_id`, `TeamName`, `Venue`, `Division`, `Contact`, `ContactAddress1`, `Wins`, `Loses`, `Draws`, `SWins`, `SLosses`, `DWins`, `DLosses`, `Points`, `Played`, `Withdrawn`, `RemoveResults`) VALUES (1, 'Pot Blacks ', 1, 1, 'Ann Able', NULL, 1, 1, 0, 3, 3, 1, 0, 6, 2, 0, 0);
        INSERT INTO `Team` (`Item_id`, `TeamName`, `Venue`, `Division`, `Contact`, `ContactAddress1`, `Wins`, `Loses`, `Draws`, `SWins`, `SLosses`, `DWins`, `DLosses`, `Points`, `Played`, `Withdrawn`, `RemoveResults`) VALUES (2, 'Rockers', 1, 1, NULL, '1 High St', 1, 1, 0, 3, 3, 0, 1, 99, 2, 1, 0);
        INSERT INTO `Team` (`Item_id`, `TeamName`, `Venue`, `Division`, `Contact`, `ContactAddress1`, `Wins`, `Loses`, `Draws`, `SWins`, `SLosses`, `DWins`, `DLosses`, `Points`, `Played`, `Withdrawn`, `RemoveResults`) VALUES (3, 'Ghosts', 1, 1, NULL, NULL, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        INSERT INTO `Team_1` (`Item_id`, `Deduction`, `AmtFined`, `FinesPaid`, `FinesDue`) VALUES (1, 2, 5.00, 5.00, 0.00);
        INSERT INTO `Player` (`PlayerNo`, `PlayerName`, `PlayerTeam`) VALUES (1, 'Void Frame', 1);
        INSERT INTO `Player` (`PlayerNo`, `PlayerName`, `PlayerTeam`) VALUES (2, 'Ann Able', 1);
        INSERT INTO `Player` (`PlayerNo`, `PlayerName`, `PlayerTeam`) VALUES (3, 'Bob Van Der Berg', 1);
        INSERT INTO `Player` (`PlayerNo`, `PlayerName`, `PlayerTeam`) VALUES (4, 'Cat Cole', 2);
        INSERT INTO `Player` (`PlayerNo`, `PlayerName`, `PlayerTeam`) VALUES (5, 'Dan Dee', 2);
        INSERT INTO `Match` (`MatchNo`, `HomeTeam`, `AwayTeam`, `MatchDate`, `HSWins`, `ASWins`, `HDWins`, `ADWins`) VALUES (1, 1, 2, '2000-09-21', 2, 1, 1, 0);
        INSERT INTO `Match` (`MatchNo`, `HomeTeam`, `AwayTeam`, `MatchDate`, `HSWins`, `ASWins`, `HDWins`, `ADWins`) VALUES (2, 2, 1, '2000-09-28', 2, 1, 0, 0);
        INSERT INTO `Match` (`MatchNo`, `HomeTeam`, `AwayTeam`, `MatchDate`, `HSWins`, `ASWins`, `HDWins`, `ADWins`) VALUES (3, 3, 3, '2000-10-05', 0, 0, 0, 0);
        INSERT INTO `Single` (`MatchNo`, `SingleNo`, `HomePlayerNo`, `AwayPlayerNo`, `Winner`, `EightBall`) VALUES (1, 1, 2, 4, 'Home', 1);
        INSERT INTO `Single` (`MatchNo`, `SingleNo`, `HomePlayerNo`, `AwayPlayerNo`, `Winner`, `EightBall`) VALUES (1, 2, 3, 5, 'Away', 0);
        INSERT INTO `Single` (`MatchNo`, `SingleNo`, `HomePlayerNo`, `AwayPlayerNo`, `Winner`, `EightBall`) VALUES (1, 3, 1, 5, 'Home', 0);
        INSERT INTO `Single` (`MatchNo`, `SingleNo`, `HomePlayerNo`, `AwayPlayerNo`, `Winner`, `EightBall`) VALUES (2, 1, 4, 2, 'Home', 0);
        INSERT INTO `Single` (`MatchNo`, `SingleNo`, `HomePlayerNo`, `AwayPlayerNo`, `Winner`, `EightBall`) VALUES (2, 2, 5, 3, 'Home', 0);
        INSERT INTO `Single` (`MatchNo`, `SingleNo`, `HomePlayerNo`, `AwayPlayerNo`, `Winner`, `EightBall`) VALUES (2, 2, 5, 2, 'Away', 0);
        INSERT INTO `Dbls` (`MatchNo`, `DoubleNo`, `HomePlayerNo1`, `HomePlayerNo2`, `AwayPlayerNo1`, `AwayPlayerNo2`, `Winner`, `EightBall1`, `EightBall2`) VALUES (1, 1, 2, 2, 4, 5, 'Home', 0, 1);
        COMMIT;
        """;

    [Fact]
    public void Parse_ReadsRowsQuotesLineBreaksAndNulls()
    {
        var file = Admin4PoolSqlFile.Parse(Export);

        var league = Assert.Single(file.Rows("League"));
        Assert.Equal("Line one\nit's line two", ((string)league["Explanation"]!).Replace("\r\n", "\n"));
        Assert.Equal(1.25, league["WinFactor"]);
        Assert.Equal(3L, league["NoSingles"]);
        Assert.Null(file.Rows("Venue")[0]["AddressLine3"]);
        Assert.Equal(6, file.Rows("Single").Count);
        Assert.Empty(file.Rows("Pair"));
    }

    [Fact]
    public void Parse_RejectsFilesThatAreNotAdmin4PoolExports()
    {
        Assert.Throws<InvalidDataException>(() => Admin4PoolSqlFile.Parse("INSERT INTO `League` (`A`) VALUES (1);"));
        Assert.Throws<InvalidDataException>(() => Admin4PoolSqlFile.Parse(Admin4PoolSqlFile.Header + "\nINSERT INTO `League` (`A`) VALUES (1);"));
    }

    [Fact]
    public void Build_MapsTheSeasonAndRepairsWhatWouldFailPlacementChecks()
    {
        var plan = Admin4PoolSeasonPlan.Build(Admin4PoolSqlFile.Parse(Export, "test.sql"), new AppSettings { MatchDrawBonus = 1 });

        Assert.Equal("Winter 2000-01", plan.Season.Name);
        Assert.Equal(new DateTime(2000, 9, 21), plan.Season.StartDate);
        Assert.Equal(new DateTime(2000, 9, 28), plan.Season.EndDate);
        Assert.Equal(DayOfWeek.Thursday, plan.Season.MatchDayOfWeek);
        Assert.Equal(4, plan.Season.FramesPerMatch);
        Assert.True(plan.Season.IncludeDoubles);
        Assert.Equal(0, plan.Season.Settings!.MatchDrawBonus);
        Assert.Equal(2, plan.Season.Settings.MatchWinBonus);

        Assert.Equal("Premier", Assert.Single(plan.Divisions).Name);
        Assert.Equal("Waterloo Road, Wellington", Assert.Single(plan.Venues).Address);
        Assert.Equal(4, plan.Players.Count);
        Assert.DoesNotContain(plan.Players, p => p.FullName.StartsWith("VOID"));
        var bob = plan.Players.Single(p => p.FirstName == "BOB");
        Assert.Equal("VAN DER BERG", bob.LastName);

        var pots = plan.Teams.Single(t => t.Name == "Pot Blacks");
        Assert.Equal("Ann Able", pots.Captain);
        Assert.Equal(plan.Players.Single(p => p.FullName == "ANN ABLE").Id, pots.CaptainPlayerId);
        Assert.Contains("Points deducted: 2.", pots.Notes);
        Assert.Contains("£5.00 fined", pots.Notes);
        var rockers = plan.Teams.Single(t => t.Name == "Rockers");
        Assert.Contains("Contact address: 1 High St", rockers.Notes);
        Assert.Contains("Withdrew", rockers.Notes);

        // The team playing itself is dropped; both real matches arrive in date order.
        Assert.Equal(2, plan.Fixtures.Count);
        var first = plan.Fixtures[0];
        Assert.Equal(new[] { 1, 2, 3, 4 }, first.Frames.Select(f => f.Number));
        Assert.Equal(FrameResult.VoidPlayerId, first.Frames[2].HomePlayerId);
        Assert.True(first.Frames[0].EightBall);
        var doubles = first.Frames[3];
        Assert.True(doubles.IsDoubles);
        Assert.True(doubles.EightBall);
        Assert.Null(doubles.HomePlayer2Id);            // Ann was entered twice in one pair
        Assert.Equal(3, first.HomeScore);
        Assert.Equal(1, first.AwayScore);

        var second = plan.Fixtures[1];
        Assert.Equal(new[] { 1, 2, 3 }, second.Frames.Select(f => f.Number)); // repeated frame 2 renumbered

        Assert.Equal(6, plan.SinglesFrames);
        Assert.Equal(1, plan.DoublesFrames);
        Assert.Equal(2, plan.EightBalls);
        Assert.Contains(plan.Warnings, w => w.Contains("team played itself"));
        Assert.Contains(plan.Warnings, w => w.Contains("repeated a player"));
        Assert.Contains(plan.Warnings, w => w.Contains("numbered 1, 2, 3"));

        // Pot Blacks: won 3-1 (3 + 2 bonus) and lost 1-2 (1) = the 6 Admin4Pool saved. Rockers' 99 is wrong.
        Assert.Equal(3, plan.TeamsChecked);
        Assert.Single(plan.TableDifferences);
        Assert.StartsWith("Rockers:", plan.TableDifferences[0]);
    }

    [Theory]
    [InlineData("2011-05-05", "Summer 2011")]
    [InlineData("2011-09-22", "Winter 2011-12")]
    [InlineData("2000-01-13", "Winter 1999-00")]
    [InlineData("1999-09-09", "Winter 1999-00")]
    public void SuggestName_FollowsTheAppsSeasonNames(string firstMatch, string expected) =>
        Assert.Equal(expected, Admin4PoolSeasonPlan.SuggestName(DateTime.Parse(firstMatch)));

    [Fact]
    public async Task Save_AddsAnInactiveSeasonAndRefusesNamesAlreadyInUse()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var context = new LeagueContext(new DbContextOptionsBuilder<LeagueContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var current = new Season { Name = "Winter 2026-27", IsActive = true };
        var clash = new Season { Name = "winter 2000-01 ", IsActive = false };
        context.AddRange(current, clash);
        await context.SaveChangesAsync();
        var store = new SqliteDataStore(context);

        var import = new Admin4PoolImport(store);
        var plan = import.Add(Admin4PoolSqlFile.Parse(Export));
        Assert.NotNull(import.NameProblem(plan, plan.Season.Name));
        await Assert.ThrowsAsync<InvalidOperationException>(() => import.SaveAsync());
        Assert.Equal(2, await context.Seasons.CountAsync());

        plan.Season.Name = "Winter 2000-01 (Admin4Pool)";
        Assert.Null(import.NameProblem(plan, plan.Season.Name));
        await import.SaveAsync();

        context.ChangeTracker.Clear();
        var saved = await context.Seasons.AsNoTracking().SingleAsync(s => s.Id == plan.Season.Id);
        Assert.False(saved.IsActive);
        Assert.True((await context.Seasons.AsNoTracking().SingleAsync(s => s.Id == current.Id)).IsActive);
        Assert.Equal(3, await context.Teams.CountAsync(t => t.SeasonId == saved.Id));
        Assert.Equal(4, await context.Players.CountAsync(p => p.SeasonId == saved.Id));
        var fixtures = await context.Fixtures.AsNoTracking().Where(f => f.SeasonId == saved.Id).ToListAsync();
        Assert.Equal(2, fixtures.Count);
        Assert.Equal(7, fixtures.Sum(f => f.Frames.Count));
        await Assert.ThrowsAsync<InvalidOperationException>(() => import.SaveAsync());
    }
}
