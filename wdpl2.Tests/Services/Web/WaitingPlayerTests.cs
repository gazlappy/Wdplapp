using System.Net;
using System.Text;
using Wdpl2.Services.Web;

namespace wdpl2.Tests;

/// <summary>
/// Reading back the players captains added and the app has not taken in.
/// </summary>
/// <remarks>
/// These sit on the website until somebody collects them, so the list is the
/// only sign they exist. Getting a row wrong here means a player quietly never
/// arrives - which is the failure the Players waiting page exists to prevent.
/// </remarks>
public class WaitingPlayerTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _body;
        public FakeHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
    }

    private static WebConnection Connection() => new()
    {
        BaseUrl = "https://wdpl.uk/api/",
        AdminUser = "admin",
        AdminPassword = "pw",
    };

    private static readonly Guid Ryan = Guid.NewGuid();
    private static readonly Guid Crown = Guid.NewGuid();
    private static readonly Guid Season = Guid.NewGuid();

    /// <summary>The shape PDO actually returns: numbers as strings, UTC dates.</summary>
    private static string Body(string extra = "") => $$"""
    {
      "ok": true,
      "data": [
        {
          "id": "{{Ryan}}",
          "name": "Ryan Foulkes",
          "is_active": "1",
          "updated_at": "2026-02-04 20:14:03",
          "team_id": "{{Crown}}",
          "season_id": "{{Season}}",
          "team_name": "The Crown A"
        }{{extra}}
      ]
    }
    """;

    [Fact]
    public async Task Uncollected_ReadsTheRowTheWebsiteSends()
    {
        using var client = new WebApiClient(Connection(), new FakeHandler(Body()));

        var waiting = await CaptainRosterService.GetUncollectedAsync(client);

        var player = Assert.Single(waiting);
        Assert.Equal(Ryan, player.Id);
        Assert.Equal("Ryan Foulkes", player.Name);
        Assert.Equal(Crown, player.TeamId);
        Assert.Equal(Season, player.SeasonId);
        Assert.Equal("The Crown A", player.TeamName);
        Assert.True(player.IsActive);
    }

    [Fact]
    public async Task Uncollected_KeepsWhenTheCaptainAddedThem()
    {
        using var client = new WebApiClient(Connection(), new FakeHandler(Body()));

        var player = (await CaptainRosterService.GetUncollectedAsync(client)).Single();

        // The server speaks UTC and says so nowhere, so it has to be assumed -
        // otherwise a night's players read as the following morning's.
        Assert.NotNull(player.Added);
        Assert.Equal(DateTimeKind.Utc, player.Added!.Value.Kind);
        Assert.Equal(new DateTime(2026, 2, 4, 20, 14, 3, DateTimeKind.Utc), player.Added.Value);

        Assert.Contains("Ryan Foulkes", player.Describe());
        Assert.Contains("added", player.Describe());
    }

    [Fact]
    public async Task Uncollected_StillNamesAPlayerWithNoDate()
    {
        var body = Body().Replace("\"updated_at\": \"2026-02-04 20:14:03\",", "\"updated_at\": null,");
        using var client = new WebApiClient(Connection(), new FakeHandler(body));

        var player = (await CaptainRosterService.GetUncollectedAsync(client)).Single();

        Assert.Null(player.Added);
        Assert.Equal("Ryan Foulkes", player.Describe());
    }

    /// <summary>
    /// A player with no team cannot be placed, so it is left for a human.
    /// </summary>
    /// <remarks>
    /// Guessing a team would put somebody in the wrong squad, which is worse
    /// than leaving the row where it is - it stays on the website and shows up
    /// again next time.
    /// </remarks>
    [Fact]
    public async Task Uncollected_SkipsARowItCannotPlace()
    {
        var orphan = $$"""
        ,{
          "id": "{{Guid.NewGuid()}}",
          "name": "Nobody's Player",
          "is_active": "1",
          "updated_at": null,
          "team_id": null,
          "season_id": "{{Season}}",
          "team_name": null
        }
        """;

        using var client = new WebApiClient(Connection(), new FakeHandler(Body(orphan)));

        var waiting = await CaptainRosterService.GetUncollectedAsync(client);

        Assert.Single(waiting);
        Assert.Equal("Ryan Foulkes", waiting[0].Name);
    }
}
