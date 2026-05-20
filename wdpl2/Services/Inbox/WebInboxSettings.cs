using Microsoft.Maui.Storage;

namespace Wdpl2.Services.Inbox;

/// <summary>
/// Persistent connection settings for the web inbox.
/// Base URL + admin user live in <see cref="Preferences"/>;
/// the password is kept in <see cref="SecureStorage"/>.
/// </summary>
public sealed class WebInboxSettings
{
    private const string KeyBaseUrl    = "WebInbox.BaseUrl";
    private const string KeyAdminUser  = "WebInbox.AdminUser";
    private const string KeyIgnoreSsl  = "WebInbox.IgnoreSslErrors";
    private const string SecureKeyPwd  = "WebInbox.AdminPassword";

    public const string DefaultBaseUrl = "https://wdpl.uk/api/";

    public string BaseUrl   { get; set; } = DefaultBaseUrl;
    public string AdminUser { get; set; } = "";
    public string AdminPassword { get; set; } = "";
    public bool   IgnoreSslErrors { get; set; }

    public static async Task<WebInboxSettings> LoadAsync()
    {
        var s = new WebInboxSettings
        {
            BaseUrl         = Preferences.Get(KeyBaseUrl,   DefaultBaseUrl),
            AdminUser       = Preferences.Get(KeyAdminUser, ""),
            IgnoreSslErrors = Preferences.Get(KeyIgnoreSsl, false),
        };
        try
        {
            s.AdminPassword = await SecureStorage.Default.GetAsync(SecureKeyPwd) ?? "";
        }
        catch
        {
            s.AdminPassword = "";
        }
        return s;
    }

    public async Task SaveAsync()
    {
        Preferences.Set(KeyBaseUrl,   string.IsNullOrWhiteSpace(BaseUrl) ? DefaultBaseUrl : BaseUrl.Trim());
        Preferences.Set(KeyAdminUser, AdminUser?.Trim() ?? "");
        Preferences.Set(KeyIgnoreSsl, IgnoreSslErrors);
        try
        {
            if (string.IsNullOrEmpty(AdminPassword))
                SecureStorage.Default.Remove(SecureKeyPwd);
            else
                await SecureStorage.Default.SetAsync(SecureKeyPwd, AdminPassword);
        }
        catch
        {
            // SecureStorage isn't available on every target; ignore so settings still save.
        }
    }
}
