namespace Wdpl2.Services.Web;

/// <summary>
/// One online feature, as the app sees it.
/// </summary>
/// <remarks>
/// Mirrors the PHP <c>Module</c> interface. Registering an implementation in DI
/// makes the feature appear on the Web Control tab and includes its server files
/// in the next deploy - nothing else needs editing.
/// </remarks>
public interface IWebModule
{
    /// <summary>Must match the PHP module's <c>id()</c> and its folder name.</summary>
    string Id { get; }

    /// <summary>Tile heading on the Web Control tab.</summary>
    string Title { get; }

    /// <summary>One line explaining what the module does.</summary>
    string Description { get; }

    /// <summary>Emoji shown on the tile, matching the style of the Website tab.</summary>
    string Icon { get; }

    /// <summary>
    /// Must match the PHP module's <c>schemaVersion()</c>. A mismatch against the
    /// server's applied version is what the status view reports as "needs install".
    /// </summary>
    int SchemaVersion { get; }

    /// <summary>
    /// Backend files this module needs, relative to the backend root
    /// (e.g. <c>api/modules/teams/Module.php</c>). Bundled as MAUI assets under
    /// <c>backend/</c> and uploaded by <see cref="WebDeployService"/>.
    /// </summary>
    IReadOnlyList<string> ServerFiles { get; }
}
