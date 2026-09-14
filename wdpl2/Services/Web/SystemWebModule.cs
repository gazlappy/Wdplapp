namespace Wdpl2.Services.Web;

/// <summary>
/// The backend core: front controller, shared services and the system module.
/// Always deployed; every other module depends on it.
/// </summary>
public sealed class SystemWebModule : IWebModule
{
    public string Id => "system";
    public string Title => "Connection & Deploy";
    public string Description => "Backend address, administrator sign-in, deployment and health.";
    public string Icon => "\U0001F517"; // link
    public int SchemaVersion => 1;

    /// <summary>
    /// Core files plus the system module. Kept in sync with
    /// <c>wdpl2/web-backend/</c>; the csproj packs that folder as
    /// <c>backend/&lt;relative&gt;</c> assets.
    /// </summary>
    public IReadOnlyList<string> ServerFiles { get; } = new[]
    {
        "api/index.php",
        "api/.htaccess",
        "api/core/Http.php",
        "api/core/Config.php",
        "api/core/Db.php",
        "api/core/Auth.php",
        "api/core/Captain.php",
        "api/core/Module.php",
        "api/core/Registry.php",
        "api/core/Schema.php",
        "api/modules/system/Module.php",
    };
}
