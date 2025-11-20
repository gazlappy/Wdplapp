using System;
using System.Collections.Generic;

namespace Wdpl2.Services
{
    /// <summary>Shared helpers for CSV row dictionaries.</summary>
    public static class CsvRows
    {
        /// <summary>Returns trimmed value for key (case-insensitive) or empty string.</summary>
        public static string Get(this Dictionary<string, string> row, string key)
            => row.TryGetValue(key, out var v) ? v?.Trim() ?? "" : "";

        /// <summary>Returns the first non-empty value among the provided keys.</summary>
        public static string GetAny(this Dictionary<string, string> row, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (row.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }
            return "";
        }

        /// <summary>Returns bool parsed from value (true/false/1/0/yes/no), default false.</summary>
        public static bool GetBool(this Dictionary<string, string> row, string key)
        {
            var s = row.Get(key);
            if (bool.TryParse(s, out var b)) return b;
            if (string.Equals(s, "1", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Split a full name into (first, last) best-effort.</summary>
        public static (string first, string last) SplitName(string fullName)
        {
            fullName = (fullName ?? "").Trim();
            if (fullName.Length == 0) return ("", "");
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return (parts[0], "");
            return (parts[0], string.Join(' ', parts, 1, parts.Length - 1));
        }
    }
}
