using Microsoft.Maui.Storage;

namespace Wdpl2.Services.Web;

/// <summary>
/// Where the backend lives and how to authenticate to it.
/// Base URL and username live in <see cref="Preferences"/>; the password is
/// kept in <see cref="SecureStorage"/> and never written to the JSON data file.
/// </summary>
/// <remarks>
/// There is deliberately no "ignore SSL errors" option. Credentials are sent on
/// every admin call, so a bypassed certificate check would hand them to whoever
/// is in the middle.
/// </remarks>
public sealed class WebConnection
{
    private const string KeyBaseUrl   = "Web.BaseUrl";
    private const string KeyAdminUser = "Web.AdminUser";
    private const string SecureKeyPwd = "Web.AdminPassword";

    public const string DefaultBaseUrl = "https://wdpl.uk/api/";

    public string BaseUrl { get; set; } = DefaultBaseUrl;
    public string AdminUser { get; set; } = "";
    public string AdminPassword { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(AdminUser) &&
        !string.IsNullOrEmpty(AdminPassword);

    /// <summary>
    /// The validated <c>index.php</c> endpoint. Throws rather than returning a
    /// usable-looking URI, so a misconfiguration cannot silently send the admin
    /// password somewhere unintended.
    /// </summary>
    public Uri ResolveEndpoint()
    {
        var text = (BaseUrl ?? "").Trim();
        if (text.Length == 0)
            throw new InvalidOperationException("Set the backend API URL first.");

        if (!Uri.TryCreate(text.EndsWith('/') ? text : text + "/", UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"'{text}' is not a valid URL.");

        if (uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("The backend API URL must use HTTPS.");

        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Do not put credentials in the API URL.");

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("The API URL must not include a query or fragment.");

        return new Uri(uri, "index.php");
    }

    public static async Task<WebConnection> LoadAsync()
    {
        var connection = new WebConnection
        {
            BaseUrl   = Preferences.Get(KeyBaseUrl, DefaultBaseUrl),
            AdminUser = Preferences.Get(KeyAdminUser, ""),
        };
        try
        {
            connection.AdminPassword = await SecureStorage.Default.GetAsync(SecureKeyPwd) ?? "";
        }
        catch
        {
            // SecureStorage is unavailable on some targets; treat as "no password saved".
            connection.AdminPassword = "";
        }
        return connection;
    }

    public async Task SaveAsync()
    {
        Preferences.Set(KeyBaseUrl, string.IsNullOrWhiteSpace(BaseUrl) ? DefaultBaseUrl : BaseUrl.Trim());
        Preferences.Set(KeyAdminUser, AdminUser?.Trim() ?? "");
        try
        {
            if (string.IsNullOrEmpty(AdminPassword))
                SecureStorage.Default.Remove(SecureKeyPwd);
            else
                await SecureStorage.Default.SetAsync(SecureKeyPwd, AdminPassword);
        }
        catch
        {
            // Saving the rest still succeeds if the secure store is unavailable.
        }
    }
}
