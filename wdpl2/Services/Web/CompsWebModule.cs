namespace Wdpl2.Services.Web;

/// <summary>
/// Competition nights: the groups and rounds players run at the venue.
/// </summary>
public sealed class CompsWebModule : IWebModule
{
    public string Id => "comps";
    public string Title => "Competition nights";
    public string Description => "Give the player running a group a PIN, and collect their results back.";
    public string Icon => "\U0001F3C6"; // trophy
    public int SchemaVersion => 1;

    public IReadOnlyList<string> ServerFiles { get; } = new[]
    {
        "api/modules/comps/Module.php",
        "api/modules/comps/Standings.php",
        "comp/index.html",
    };
}
