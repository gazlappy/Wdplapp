namespace Wdpl2.Services.Web;

/// <summary>
/// The modules this build knows about.
/// </summary>
/// <remarks>
/// The Web Control tab renders from this, and <see cref="WebDeployService"/>
/// deploys from it, so adding a module is one DI registration rather than edits
/// spread across the UI and the deploy path.
/// </remarks>
public sealed class WebModuleRegistry
{
    private readonly List<IWebModule> _modules;

    public WebModuleRegistry(IEnumerable<IWebModule> modules)
    {
        _modules = modules.OrderBy(m => m.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public IReadOnlyList<IWebModule> Modules => _modules;

    public IWebModule? Find(string id) =>
        _modules.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Every module's server files, de-duplicated.</summary>
    public IReadOnlyList<string> AllServerFiles() =>
        _modules.SelectMany(m => m.ServerFiles)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
}
