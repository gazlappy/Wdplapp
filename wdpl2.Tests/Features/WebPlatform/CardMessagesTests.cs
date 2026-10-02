using System.Text.Json;
using Wdpl2.Services.Web;
using Xunit;

namespace wdpl2.Tests.Features.WebPlatform;

/// <summary>
/// The captains' card notes, read as messages by the league in the app and on
/// the admin page.
/// </summary>
public class CardMessagesTests
{
    [Fact]
    public void Messages_are_read_from_the_website_reply()
    {
        using var doc = JsonDocument.Parse("""
            [{"fixture_id":"54ef93eb-d4ff-4d9d-9e32-b82be3d7e436","is_cup":true,"state":"live",
              "match_date":"2026-10-01","home_team_name":"LEGENDS","away_team_name":"CHANGING LANES",
              "text":"Started late.","written_at":"2026-10-01 19:15:00","read_at":null,"unread":true},
             {"fixture_id":"11111111-1111-1111-1111-111111111111","is_cup":false,"state":"claimed",
              "match_date":"2026-09-24","home_team_name":"X","away_team_name":"Y",
              "text":"Old","written_at":"2026-09-24 20:00:00","read_at":"2026-09-25 09:00:00","unread":false}]
            """);

        var messages = ScorecardService.ReadMessages(doc.RootElement);

        Assert.Equal(2, messages.Count);
        var first = messages[0];
        Assert.Equal(Guid.Parse("54ef93eb-d4ff-4d9d-9e32-b82be3d7e436"), first.FixtureId);
        Assert.True(first.IsCup);
        Assert.True(first.Unread);
        Assert.Equal("Started late.", first.Text);
        Assert.Equal("LEGENDS", first.HomeTeam);
        Assert.Equal(new DateTime(2026, 10, 1, 19, 15, 0, DateTimeKind.Utc).ToLocalTime(), first.WrittenAt);
        Assert.False(messages[1].Unread);
        Assert.False(messages[1].IsCup);
    }

    [Fact]
    public void An_unexpected_reply_is_no_messages()
    {
        using var doc = JsonDocument.Parse("{}");
        Assert.Empty(ScorecardService.ReadMessages(doc.RootElement));
    }

    [Fact]
    public void The_admin_page_and_the_module_agree_on_messages()
    {
        var root = RepoRoot();
        var module = File.ReadAllText(Path.Combine(root, "wdpl2", "web-backend", "api", "modules", "scorecards", "Module.php"));
        var admin = File.ReadAllText(Path.Combine(root, "wdpl2", "web-backend", "admin", "index.html"));

        Assert.Contains("'messages'    => ['role' => Role::Admin", module);
        Assert.Contains("'messageRead' => ['role' => Role::Admin", module);
        Assert.Contains("ADD COLUMN IF NOT EXISTS notes_read_at", module);
        // A changed note shows as new again, so the time it was written has to move.
        Assert.Contains("notes_at = IF(? = 1, UTC_TIMESTAMP(), notes_at)", module);

        Assert.Contains("data-tab=\"messages\"", admin);
        Assert.Contains("id=\"tab-messages\"", admin);
        Assert.Contains("call('scorecards','messages',{})", admin);
        Assert.Contains("call('scorecards','messageRead',", admin);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "wdpl2.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
