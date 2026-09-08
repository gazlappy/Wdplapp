using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Wdpl2.Services;

public static class EntryFormImportIdentity
{
    /// <summary>External IDs take precedence. Older ID-less exports use exact payload identity, not fuzzy names.</summary>
    public static string GetKey(string? externalId, string? formId, string? name, DateTime? submittedAt, IDictionary<string, string>? values)
    {
        if (!string.IsNullOrWhiteSpace(externalId)) return externalId;
        var identity = JsonSerializer.Serialize(new
        {
            formId, name, submittedAt,
            values = values?.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray()
        });
        return "legacy-sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
}
