namespace Wdpl2.Services.Web;

/// <summary>
/// Publishes league data to the website: seasons, divisions, venues, teams,
/// players, fixtures and standings.
/// </summary>
/// <remarks>
/// Everything this module publishes is desktop-owned. The app pushes a complete
/// snapshot and the server never edits it, so there is no merge and no conflict
/// to resolve - see <c>wdpl2/Docs/WebPlatform.md</c>.
/// </remarks>
public sealed class LeagueWebModule : IWebModule
{
    public string Id => "league";
    public string Title => "League data";
    public string Description => "Publish teams, fixtures, results and tables to the website.";
    public string Icon => "\U0001F3C6"; // trophy
    public int SchemaVersion => 5;

    public IReadOnlyList<string> ServerFiles { get; } = new[]
    {
        "api/modules/league/Module.php",
    };
}
