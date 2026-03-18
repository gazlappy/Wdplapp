using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Wdpl2.Helpers;

/// <summary>
/// Shared division name normalization and matching utilities.
/// Handles ordinal forms ("1st"↔"First"), color names, suffix stripping,
/// and fuzzy prefix matching used by all import paths.
/// </summary>
public static class DivisionHelper
{
    /// <summary>
    /// Maps ordinal variants to a canonical form.
    /// Covers written-out ordinals, numeric ordinals and abbreviations
    /// commonly used for winter league divisions.
    /// </summary>
    private static readonly Dictionary<string, string> OrdinalMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // First / 1st
        ["1st"] = "First",
        ["first"] = "First",
        ["1"] = "First",

        // Second / 2nd
        ["2nd"] = "Second",
        ["second"] = "Second",
        ["2"] = "Second",

        // Third / 3rd
        ["3rd"] = "Third",
        ["third"] = "Third",
        ["3"] = "Third",

        // Fourth / 4th
        ["4th"] = "Fourth",
        ["fourth"] = "Fourth",
        ["4"] = "Fourth",

        // Fifth / 5th
        ["5th"] = "Fifth",
        ["fifth"] = "Fifth",
        ["5"] = "Fifth",

        // Sixth / 6th
        ["6th"] = "Sixth",
        ["sixth"] = "Sixth",
        ["6"] = "Sixth",
    };

    /// <summary>
    /// Normalize a division name for display and storage.
    /// "Red", "Red Division", "RED DIVISION" → "Red Division".
    /// "1st", "First", "first division" → "First Division".
    /// </summary>
    public static string NormalizeDivisionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";

        var n = name.Trim();

        // Collapse multiple whitespace
        n = Regex.Replace(n, @"\s+", " ");

        // Strip "Table" suffix (from headings like "Red Division Table")
        n = Regex.Replace(n, @"\s*Table\s*$", "", RegexOptions.IgnoreCase).Trim();

        // Strip "Division" / "Div" / "Div." suffix
        n = Regex.Replace(n, @"\s*(Division|Div\.?)\s*$", "", RegexOptions.IgnoreCase).Trim();

        // Strip "Division" / "Div" prefix (for "Division Red")
        n = Regex.Replace(n, @"^(Division|Div\.?)\s+", "", RegexOptions.IgnoreCase).Trim();

        if (string.IsNullOrWhiteSpace(n)) return name.Trim();

        // Normalize ordinals: "1st" → "First", "second" → "Second", etc.
        n = NormalizeOrdinalWord(n);

        // Title case each word
        n = string.Join(' ', n.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length > 0 ? char.ToUpper(w[0]) + (w.Length > 1 ? w[1..].ToLower() : "") : w));

        return n + " Division";
    }

    /// <summary>
    /// Normalize for matching (lowercase). Used by import paths for comparison.
    /// "Red", "Red Division", "red div" all → "red division".
    /// </summary>
    public static string NormalizeDivisionNameForMatching(string name)
    {
        return NormalizeDivisionName(name).ToLowerInvariant();
    }

    /// <summary>
    /// Strip "Division"/"Div"/"Table" suffix from a division name, returning the core word.
    /// "Red Division" → "Red", "Premier Div." → "Premier".
    /// </summary>
    public static string StripDivisionSuffix(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var n = name.Trim();
        n = Regex.Replace(n, @"\s*Table\s*$", "", RegexOptions.IgnoreCase);
        n = Regex.Replace(n, @"\s*(Division|Div\.?)\s*$", "", RegexOptions.IgnoreCase);
        n = Regex.Replace(n, @"^(Division|Div\.?)\s+", "", RegexOptions.IgnoreCase);
        return n.Trim();
    }

    /// <summary>
    /// Find a fuzzy match for a normalized division name in the existing map.
    /// Handles abbreviations ("R" matches "Red") and ordinal equivalents
    /// ("1st" matches "First") via prefix matching.
    /// </summary>
    public static Guid? FindFuzzyDivisionMatch(string normalizedName, Dictionary<string, Guid> divisionMap)
    {
        var core = StripDivisionSuffix(normalizedName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(core)) return null;

        // Resolve ordinal so "1st" and "first" both map to "first"
        var coreCanonical = ResolveOrdinal(core);

        foreach (var (existingName, id) in divisionMap)
        {
            var existingCore = StripDivisionSuffix(existingName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(existingCore)) continue;

            var existingCanonical = ResolveOrdinal(existingCore);

            // Exact core match (after ordinal resolution)
            if (coreCanonical == existingCanonical) return id;

            // Prefix matching: "R" matches "Red", "Prem" matches "Premier"
            if (coreCanonical.Length >= 1 && existingCanonical.Length >= 1 &&
                (coreCanonical.StartsWith(existingCanonical) || existingCanonical.StartsWith(coreCanonical)))
                return id;
        }

        return null;
    }

    /// <summary>
    /// Resolve a raw division name to a GUID using normalization and fuzzy matching.
    /// Three-tier lookup: exact → normalized → fuzzy prefix.
    /// </summary>
    public static Guid? ResolveDivisionId(
        string? rawName, Dictionary<string, Guid> divisionMap, Guid? defaultId = null)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return defaultId;

        // Exact raw name
        if (divisionMap.TryGetValue(rawName, out var exactId)) return exactId;

        // Normalized name
        var normalized = NormalizeDivisionName(rawName);
        if (divisionMap.TryGetValue(normalized, out var normId)) return normId;

        // Fuzzy/prefix match
        var fuzzy = FindFuzzyDivisionMatch(normalized, divisionMap);
        return fuzzy ?? defaultId;
    }

    /// <summary>
    /// Check whether two division names refer to the same division after normalization
    /// and ordinal resolution.
    /// </summary>
    public static bool AreSameDivision(string name1, string name2)
    {
        if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2))
            return false;

        var n1 = NormalizeDivisionNameForMatching(name1);
        var n2 = NormalizeDivisionNameForMatching(name2);
        if (n1 == n2) return true;

        // Also compare ordinal-resolved cores
        var c1 = ResolveOrdinal(StripDivisionSuffix(name1).ToLowerInvariant());
        var c2 = ResolveOrdinal(StripDivisionSuffix(name2).ToLowerInvariant());
        return c1 == c2 && !string.IsNullOrWhiteSpace(c1);
    }

    /// <summary>
    /// If the input is an ordinal key ("1st", "first", "2nd", etc.)
    /// return its canonical lowercase form ("first", "second", …).
    /// Otherwise return the input unchanged (lowered).
    /// </summary>
    private static string ResolveOrdinal(string core)
    {
        if (string.IsNullOrWhiteSpace(core)) return core;
        var trimmed = core.Trim();
        return OrdinalMap.TryGetValue(trimmed, out var canonical)
            ? canonical.ToLowerInvariant()
            : trimmed.ToLowerInvariant();
    }

    /// <summary>
    /// If the whole word is an ordinal variant, replace it with the canonical
    /// title-cased form. "1st" → "First". Multi-word strings have each word checked.
    /// </summary>
    private static string NormalizeOrdinalWord(string input)
    {
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (OrdinalMap.TryGetValue(words[i], out var canonical))
                words[i] = canonical;
        }
        return string.Join(' ', words);
    }
}
