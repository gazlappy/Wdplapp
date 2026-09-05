using System.Text.RegularExpressions;

namespace Wdpl2.Services;

/// <summary>
/// Scans the filesystem for pool league files, detects which season they belong to,
/// groups them by season, and identifies duplicates for merging.
/// </summary>
public partial class LeagueFileDiscoveryService
{
    private static readonly string[] ImportExtensions =
    {
        ".mdb", ".accdb",
        ".sql",
        ".html", ".htm",
        ".xlsx", ".xls",
        ".csv",
        ".docx", ".doc",
        ".pdf"
    };

    private static readonly string[] LeagueKeywords =
    {
        "pool", "league", "wdpl", "division", "fixture", "result",
        "season", "snooker", "billiard", "table", "match", "rating",
        "player", "team", "venue", "standing", "singles", "doubles"
    };

    // Keywords that indicate a file is almost certainly NOT league data
    private static readonly string[] NegativeKeywords =
    {
        "invoice", "receipt", "statement", "payslip", "tax", "insurance",
        "cv", "resume", "recipe", "holiday", "booking", "ticket", "manual",
        "warranty", "newsletter"
    };

    // ── Discovered file ────────────────────────────────────────────

    public class DiscoveredFile
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FileType { get; set; } = "";
        public string FileTypeIcon { get; set; } = "📄";
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public string DetectedSeason { get; set; } = "Unknown";
        public string? DetectedDivision { get; set; }
        public string? SeasonReviewReason { get; set; }
        public double Confidence { get; set; }
        public bool IsFolder { get; set; }
        public string SeasonSortKey { get; set; } = "9999";
        public bool IsSelected { get; set; } = true;

        public string FileSizeDisplay => FileSize switch
        {
            < 1024 => $"{FileSize} B",
            < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
            _ => $"{FileSize / (1024.0 * 1024.0):F1} MB"
        };

        public string ConfidenceDisplay => Confidence switch
        {
            >= 0.7 => "High",
            >= 0.4 => "Medium",
            _ => "Low"
        };

        public Color ConfidenceColor => Confidence switch
        {
            >= 0.7 => Colors.Green,
            >= 0.4 => Colors.Orange,
            _ => Colors.Gray
        };
    }

    // ── Season group ───────────────────────────────────────────────

    public class SeasonGroup
    {
        public string SeasonKey { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public List<DiscoveredFile> Files { get; set; } = new();
        public Guid? ExistingSeasonId { get; set; }
        public string? ExistingSeasonName { get; set; }
        public bool HasDuplicateTypes { get; set; }
        public bool IsSelected { get; set; } = true;

        // Pre-analysis entity counts (populated by AnalyzeGroupsAsync)
        public string? ReviewReason { get; set; }
        public bool RequiresReview => !string.IsNullOrEmpty(ReviewReason);
        public int AnalyzedTeams { get; set; }
        public int AnalyzedPlayers { get; set; }
        public int AnalyzedResults { get; set; }
        public int AnalyzedCompetitions { get; set; }
        public int AnalyzedDoublesPairings { get; set; }
        public int AnalyzedDivisions { get; set; }
        public bool IsAnalyzed { get; set; }
        public bool HasData => AnalyzedTeams + AnalyzedPlayers + AnalyzedResults + AnalyzedCompetitions + AnalyzedDoublesPairings > 0;
        public int TotalEntities => AnalyzedTeams + AnalyzedPlayers + AnalyzedResults + AnalyzedCompetitions + AnalyzedDoublesPairings;

        public string DataSummary
        {
            get
            {
                if (!IsAnalyzed) return "Not analyzed";
                if (!HasData) return "⚠️ No data detected";
                var parts = new List<string>();
                if (AnalyzedDivisions > 0) parts.Add($"{AnalyzedDivisions} div");
                if (AnalyzedTeams > 0) parts.Add($"{AnalyzedTeams} teams");
                if (AnalyzedPlayers > 0) parts.Add($"{AnalyzedPlayers} players");
                if (AnalyzedResults > 0) parts.Add($"{AnalyzedResults} results");
                if (AnalyzedCompetitions > 0) parts.Add($"{AnalyzedCompetitions} comps");
                if (AnalyzedDoublesPairings > 0) parts.Add($"{AnalyzedDoublesPairings} doubles pairs");
                return string.Join(", ", parts);
            }
        }

        public int HtmlCount => Files.Count(f => f.FileType == "HTML");
        public int DatabaseCount => Files.Count(f => f.FileType is "Access" or "SQL" or "Paradox");
        public int OtherCount => Files.Count(f => f.FileType is "Word" or "Excel" or "CSV" or "PDF");
        public bool IsExistingSeason => ExistingSeasonId.HasValue;

        public string Summary
        {
            get
            {
                var parts = new List<string>();
                if (HtmlCount > 0) parts.Add($"{HtmlCount} HTML");
                if (DatabaseCount > 0) parts.Add($"{DatabaseCount} Database");
                if (OtherCount > 0) parts.Add($"{OtherCount} Other");
                return string.Join(", ", parts);
            }
        }

        public string DuplicateInfo
        {
            get
            {
                if (!HasDuplicateTypes) return "";
                var dupes = Files.GroupBy(f => f.FileType).Where(g => g.Count() > 1)
                    .Select(g => $"{g.Count()}× {g.Key}");
                return $"⚠️ Duplicates: {string.Join(", ", dupes)}";
            }
        }
    }

    // ── Progress ───────────────────────────────────────────────────

    public class ScanProgress
    {
        public string CurrentPath { get; set; } = "";
        public int FilesScanned { get; set; }
        public int FilesFound { get; set; }
    }

    // ── Default scan locations ─────────────────────────────────────

    public static List<(string Path, string Label, bool DefaultChecked)> GetDefaultScanLocations()
    {
        var locations = new List<(string, string, bool)>();
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var downloads = Path.Combine(userProfile, "Downloads");

            if (!string.IsNullOrEmpty(downloads) && Directory.Exists(downloads))
                locations.Add((downloads, "📥 Downloads", true));

            if (!string.IsNullOrEmpty(documents) && Directory.Exists(documents))
                locations.Add((documents, "📁 Documents", true));

            if (!string.IsNullOrEmpty(desktop) && Directory.Exists(desktop))
                locations.Add((desktop, "🖥️ Desktop", true));

            // Check for common WDPL-specific paths
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            var wdplPaths = new[]
            {
                Path.Combine(programFiles, "WDPL"),
                Path.Combine(programFilesX86, "WDPL"),
                Path.Combine(userProfile, "WDPL"),
                @"C:\WDPL",
                @"C:\PoolLeague",
            };

            foreach (var path in wdplPaths)
            {
                if (Directory.Exists(path))
                    locations.Add((path, $"🎱 WDPL ({Path.GetFileName(path)})", true));
            }
        }
        catch { /* platform differences */ }

        return locations;
    }

    // ── Main scan ──────────────────────────────────────────────────

    public async Task<List<DiscoveredFile>> ScanAsync(
        IEnumerable<string> directories,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var discovered = new List<DiscoveredFile>();
        var scanProgress = new ScanProgress();

        foreach (var dir in directories)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (!Directory.Exists(dir)) continue;

            await Task.Run(() =>
                ScanDirectory(dir, discovered, scanProgress, progress, cancellationToken, maxDepth: 5),
                cancellationToken);
        }

        // Sort by confidence descending
        discovered.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));
        return discovered;
    }

    private void ScanDirectory(
        string directory,
        List<DiscoveredFile> discovered,
        ScanProgress scanProgress,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken,
        int maxDepth,
        int currentDepth = 0)
    {
        if (currentDepth > maxDepth || cancellationToken.IsCancellationRequested) return;

        try
        {
            scanProgress.CurrentPath = directory;
            progress?.Report(new ScanProgress
            {
                CurrentPath = directory,
                FilesScanned = scanProgress.FilesScanned,
                FilesFound = scanProgress.FilesFound
            });

            // Check if this is a Paradox database folder
            try
            {
                var dbFiles = Directory.GetFiles(directory, "*.DB");
                if (dbFiles.Length >= 3)
                {
                    var hasLeagueFiles = dbFiles.Any(f =>
                    {
                        var name = Path.GetFileNameWithoutExtension(f).ToLower();
                        return name.Contains("team") || name.Contains("player") ||
                               name.Contains("fixture") || name.Contains("match") ||
                               name.Contains("division") || name.Contains("venue");
                    });

                    if (hasLeagueFiles)
                    {
                        var folderInfo = new DirectoryInfo(directory);
                        var file = new DiscoveredFile
                        {
                            FilePath = directory,
                            FileName = folderInfo.Name,
                            FileType = "Paradox",
                            FileTypeIcon = "📁",
                            FileSize = dbFiles.Sum(f => new FileInfo(f).Length),
                            LastModified = folderInfo.LastWriteTime,
                            IsFolder = true,
                            Confidence = 0.9
                        };
                        DetectSeason(file);
                        lock (discovered) { discovered.Add(file); scanProgress.FilesFound++; }
                    }
                }
            }
            catch { /* skip */ }

            // Scan individual files
            foreach (var filePath in Directory.EnumerateFiles(directory))
            {
                if (cancellationToken.IsCancellationRequested) break;
                scanProgress.FilesScanned++;

                var ext = Path.GetExtension(filePath).ToLower();
                if (!ImportExtensions.Contains(ext)) continue;

                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length > 500_000_000 || fileInfo.Length < 10) continue;

                    var file = new DiscoveredFile
                    {
                        FilePath = filePath,
                        FileName = fileInfo.Name,
                        FileType = GetFileType(ext),
                        FileTypeIcon = GetFileTypeIcon(ext),
                        FileSize = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTime
                    };

                    file.Confidence = CalculateConfidence(file);

                    // For text-based files near the threshold, sniff actual content
                    // to confirm/deny league data (reduces false positives/negatives)
                    if (file.FileType is "HTML" or "CSV" && file.Confidence < 0.7)
                        file.Confidence = AdjustConfidenceByContent(file);

                    if (file.Confidence >= 0.2)
                    {
                        DetectSeason(file);
                        lock (discovered) { discovered.Add(file); scanProgress.FilesFound++; }
                    }
                }
                catch { /* skip inaccessible */ }

                if (scanProgress.FilesScanned % 200 == 0)
                {
                    progress?.Report(new ScanProgress
                    {
                        CurrentPath = directory,
                        FilesScanned = scanProgress.FilesScanned,
                        FilesFound = scanProgress.FilesFound
                    });
                }
            }

            // Recurse subdirectories
            foreach (var subDir in Directory.EnumerateDirectories(directory))
            {
                if (cancellationToken.IsCancellationRequested) break;
                try
                {
                    var dirInfo = new DirectoryInfo(subDir);
                    if ((dirInfo.Attributes & FileAttributes.System) != 0 ||
                        (dirInfo.Attributes & FileAttributes.Hidden) != 0)
                        continue;

                    var dirName = dirInfo.Name.ToLower();
                    if (dirName.StartsWith('.') || dirName is "node_modules" or "bin" or "obj"
                        or "packages" or "$recycle.bin" or "windows" or "program files"
                        or "programdata" or "appdata")
                        continue;

                    ScanDirectory(subDir, discovered, scanProgress, progress, cancellationToken,
                        maxDepth, currentDepth + 1);
                }
                catch { /* skip */ }
            }
        }
        catch { /* skip inaccessible directories */ }
    }

    // ── Grouping ───────────────────────────────────────────────────

    /// <summary>
    /// Normalize a detected season string to a canonical form so that
    /// "2023-24", "2023/24", "2023-2024", "2023/2024" all become "2023-24".
    /// </summary>
    public static string NormalizeSeasonKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "Unknown";
        key = Regex.Replace(key.Trim(), @"\s+", " ");
        if (key.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)) return key;
        var rangeMatch = Regex.Match(key, @"\b((?:19|20)\d{2})\s*[-_/\\]\s*((?:19|20)?\d{2})\b");
        string year;
        if (rangeMatch.Success)
        {
            year = $"{rangeMatch.Groups[1].Value}-{rangeMatch.Groups[2].Value[^2..]}";
        }
        else
        {
            var match = Regex.Match(key, @"\b(?:19|20)\d{2}\b");
            if (!match.Success) return key;
            year = match.Value;
        }
        var term = Regex.Match(key, @"\b(winter|summer|spring|autumn|fall)\b", RegexOptions.IgnoreCase);
        return term.Success ? $"{char.ToUpperInvariant(term.Value[0])}{term.Value[1..].ToLowerInvariant()} {year}" : year;
    }

    public List<SeasonGroup> GroupBySeason(List<DiscoveredFile> files)
    {
        // First, normalize all detected seasons so duplicates converge
        foreach (var file in files)
        {
            file.DetectedSeason = NormalizeSeasonKey(file.DetectedSeason);
        }

        var groups = files
            .Where(f => f.IsSelected)
            .GroupBy(f => f.DetectedSeason, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var group = new SeasonGroup
                {
                    SeasonKey = g.Key,
                    DisplayName = g.Key,
                    Files = g.OrderByDescending(f => f.Confidence).ToList(),
                    ReviewReason = g.Select(f => f.SeasonReviewReason).FirstOrDefault(r => r != null)
                        ?? (!HasSeasonTerm(g.Key) ? "Season is uncertain. Separate summer and winter source folders before importing." : null)
                };
                if (group.RequiresReview) group.IsSelected = false;

                var typeCounts = group.Files.GroupBy(f => f.FileType).Where(t => t.Count() > 1);
                group.HasDuplicateTypes = typeCounts.Any();

                FindMatchingSeason(g.Key, group);
                return group;
            })
            .ToList();

        return groups.OrderBy(g => g.Files.FirstOrDefault()?.SeasonSortKey ?? "9999").ToList();
    }

    /// <summary>
    /// Merge groups that clearly refer to the same season:
    /// - Groups that match the same existing season
    /// - Groups whose normalized keys are equivalent (e.g. "2023" and "2023-24" when start year matches)
    /// </summary>
    private static List<SeasonGroup> MergeDuplicateGroups(List<SeasonGroup> groups)
    {
        var merged = new List<SeasonGroup>();
        var consumed = new HashSet<int>();

        for (int i = 0; i < groups.Count; i++)
        {
            if (consumed.Contains(i)) continue;

            var primary = groups[i];

            for (int j = i + 1; j < groups.Count; j++)
            {
                if (consumed.Contains(j)) continue;

                var candidate = groups[j];
                bool shouldMerge = false;

                // Same existing season ID
                if (primary.ExistingSeasonId.HasValue && candidate.ExistingSeasonId.HasValue
                    && primary.ExistingSeasonId == candidate.ExistingSeasonId)
                {
                    shouldMerge = true;
                }

                // Same normalized key
                if (!shouldMerge && primary.SeasonKey == candidate.SeasonKey)
                    shouldMerge = true;

                // One is bare year, other is range starting with that year ("2023" + "2023-24")
                if (!shouldMerge)
                {
                    var pk = primary.SeasonKey;
                    var ck = candidate.SeasonKey;
                    if (pk.Length == 4 && ck.StartsWith(pk + "-")) shouldMerge = true;
                    if (ck.Length == 4 && pk.StartsWith(ck + "-")) shouldMerge = true;
                }

                if (shouldMerge)
                {
                    // Prefer the longer/more descriptive key as display name
                    if (candidate.SeasonKey.Length > primary.SeasonKey.Length)
                    {
                        primary.SeasonKey = candidate.SeasonKey;
                        primary.DisplayName = candidate.DisplayName;
                    }
                    if (candidate.ExistingSeasonId.HasValue && !primary.ExistingSeasonId.HasValue)
                    {
                        primary.ExistingSeasonId = candidate.ExistingSeasonId;
                        primary.ExistingSeasonName = candidate.ExistingSeasonName;
                    }

                    primary.Files.AddRange(candidate.Files);
                    var typeCounts = primary.Files.GroupBy(f => f.FileType).Where(t => t.Count() > 1);
                    primary.HasDuplicateTypes = typeCounts.Any();
                    consumed.Add(j);
                }
            }

            merged.Add(primary);
        }

        return merged;
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static string GetFileType(string extension) => extension.ToLower() switch
    {
        ".mdb" or ".accdb" => "Access",
        ".sql" => "SQL",
        ".html" or ".htm" => "HTML",
        ".xlsx" or ".xls" => "Excel",
        ".csv" => "CSV",
        ".docx" or ".doc" => "Word",
        ".pdf" => "PDF",
        _ => "Unknown"
    };

    private static string GetFileTypeIcon(string extension) => extension.ToLower() switch
    {
        ".mdb" or ".accdb" => "🗄️",
        ".sql" => "🗃️",
        ".html" or ".htm" => "🌐",
        ".xlsx" or ".xls" => "📊",
        ".csv" => "📊",
        ".docx" or ".doc" => "📄",
        ".pdf" => "📕",
        _ => "📄"
    };

    private static double CalculateConfidence(DiscoveredFile file)
    {
        double confidence = 0.0;
        var lowerPath = file.FilePath.ToLower();
        var lowerName = file.FileName.ToLower();

        foreach (var keyword in LeagueKeywords)
        {
            if (lowerName.Contains(keyword))
                confidence += 0.3;
            else if (lowerPath.Contains(keyword))
                confidence += 0.15;
        }

        // Database files are high-value imports
        if (file.FileType is "Access" or "SQL")
            confidence += 0.2;

        // HTML files from WDPL websites have known naming patterns
        if (file.FileType == "HTML")
        {
            if (lowerName.StartsWith("table") || lowerName.StartsWith("single") ||
                lowerName.StartsWith("double") || lowerName == "results.htm" ||
                lowerName.StartsWith("player") || lowerName.StartsWith("fixture"))
                confidence += 0.5;
        }

        if (SeasonYearRangePattern().IsMatch(lowerPath))
            confidence += 0.2;

        if (lowerPath.Contains("wdpl") || lowerPath.Contains("pool league") ||
            lowerPath.Contains("poolleague"))
            confidence += 0.3;

        // Penalize names that suggest non-league documents
        foreach (var negative in NegativeKeywords)
        {
            if (lowerName.Contains(negative))
            {
                confidence -= 0.4;
                break;
            }
        }

        return Math.Clamp(confidence, 0.0, 1.0);
    }

    /// <summary>
    /// Read the first few KB of a text-based file and adjust confidence based on
    /// whether real league keywords appear in the actual content.
    /// </summary>
    private static double AdjustConfidenceByContent(DiscoveredFile file)
    {
        try
        {
            using var stream = new FileStream(file.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var buffer = new char[8192];
            var read = reader.Read(buffer, 0, buffer.Length);
            if (read <= 0) return file.Confidence;

            var content = new string(buffer, 0, read).ToLowerInvariant();

            int hits = 0;
            foreach (var keyword in LeagueKeywords)
            {
                if (content.Contains(keyword)) hits++;
            }

            // Strong signals from typical WDPL page content
            if (content.Contains("pld") || content.Contains("played") ||
                content.Contains("pts") || content.Contains("points"))
                hits++;

            if (hits >= 4) return Math.Min(file.Confidence + 0.4, 1.0);
            if (hits >= 2) return Math.Min(file.Confidence + 0.2, 1.0);
            if (hits == 0) return Math.Max(file.Confidence - 0.3, 0.0);
        }
        catch { /* unreadable — keep name-based confidence */ }

        return file.Confidence;
    }

    /// <summary>
    /// Build a short path from filename + nearest 2 parent folder names
    /// so that random years deep in user paths (C:\Users\2024\...) are ignored.
    /// </summary>
    private static string GetRelevantPathSegments(string filePath)
    {
        var segments = new List<string>();
        segments.Add(Path.GetFileName(filePath));

        var dir = Path.GetDirectoryName(filePath);
        for (int i = 0; i < 2 && !string.IsNullOrEmpty(dir); i++)
        {
            var folderName = Path.GetFileName(dir);
            if (!string.IsNullOrEmpty(folderName))
                segments.Add(folderName);
            dir = Path.GetDirectoryName(dir);
        }

        return string.Join(" ", segments);
    }

    private static void DetectSeason(DiscoveredFile file)
    {
        // Only check filename + nearest 2 parent folders — not the entire path
        var pathToCheck = GetRelevantPathSegments(file.FilePath);
        var normalized = NormalizeSeasonKey(pathToCheck.Replace('_', ' '));
        if (Regex.IsMatch(normalized, @"^(Winter|Summer|Spring|Autumn|Fall) ", RegexOptions.IgnoreCase))
        {
            file.DetectedSeason = normalized;
            file.SeasonSortKey = Regex.Match(normalized, @"(?:19|20)\d{2}").Value;
            return;
        }

        // Season word patterns (e.g., "Winter 2023")
        var seasonWordMatch = SeasonWordPattern().Match(pathToCheck);
        if (seasonWordMatch.Success)
        {
            var word = seasonWordMatch.Groups[1].Value;
            var year = seasonWordMatch.Groups[2].Value;
            file.DetectedSeason = $"{char.ToUpper(word[0])}{word[1..].ToLower()} {year}";
            file.SeasonSortKey = year;
            return;
        }

        // Year-first season word patterns (e.g., "2023 Winter")
        var yearFirstMatch = YearFirstSeasonWordPattern().Match(pathToCheck);
        if (yearFirstMatch.Success)
        {
            var year = yearFirstMatch.Groups[1].Value;
            var word = yearFirstMatch.Groups[2].Value;
            file.DetectedSeason = $"{char.ToUpper(word[0])}{word[1..].ToLower()} {year}";
            file.SeasonSortKey = year;
            return;
        }

        // Year range patterns (e.g., "2023-24", "2023_2024")
        var yearRangeMatch = SeasonYearRangePattern().Match(pathToCheck);
        if (yearRangeMatch.Success)
        {
            var startYear = yearRangeMatch.Groups[1].Value;
            var endYearShort = yearRangeMatch.Groups[3].Value;
            var endYear = endYearShort.Length == 2
                ? startYear[..2] + endYearShort
                : endYearShort;
            file.DetectedSeason = $"{startYear}-{endYear[2..]}";
            file.SeasonSortKey = startYear;
            return;
        }

        // Single year patterns
        var yearMatches = SingleYearPattern().Matches(pathToCheck);
        if (yearMatches.Count > 0)
        {
            var years = yearMatches.Select(m => m.Groups[1].Value)
                .Distinct().OrderByDescending(y => y).ToList();

            if (years.Count >= 2)
            {
                var y1 = int.Parse(years[0]);
                var y2 = int.Parse(years[1]);
                if (Math.Abs(y1 - y2) == 1)
                {
                    var start = Math.Min(y1, y2);
                    file.DetectedSeason = $"{start}-{(start + 1) % 100:D2}";
                    file.SeasonSortKey = start.ToString();
                    return;
                }
            }
            file.DetectedSeason = years[0];
            file.SeasonSortKey = years[0];
            return;
        }

        // Apostrophe year patterns (e.g., "Results '23")
        var apostropheMatch = ApostropheYearPattern().Match(pathToCheck);
        if (apostropheMatch.Success)
        {
            var year = "20" + apostropheMatch.Groups[1].Value;
            file.DetectedSeason = year;
            file.SeasonSortKey = year;
            return;
        }

        // Fallback: modification year
        file.DetectedSeason = $"Unknown ({file.LastModified.Year})";
        file.SeasonSortKey = file.LastModified.Year.ToString();
    }

    private static void FindMatchingSeason(string seasonKey, SeasonGroup group)
    {
        group.ExistingSeasonId = null;
        group.ExistingSeasonName = null;
        if (!HasSeasonTerm(seasonKey)) return;
        var matches = DataStore.Data.Seasons.Where(s =>
            string.Equals(NormalizeSeasonKey(s.Name), NormalizeSeasonKey(seasonKey), StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count != 1) return;
        group.ExistingSeasonId = matches[0].Id;
        group.ExistingSeasonName = matches[0].Name;
    }

    public static bool HasSeasonTerm(string key) => Regex.IsMatch(key,
        @"\b(winter|summer|spring|autumn|fall)\b.*\b(?:19|20)\d{2}\b", RegexOptions.IgnoreCase);

    // ── Regex patterns ─────────────────────────────────────────────

    [GeneratedRegex(@"((?:19|20)\d{2})[-_/\\](19|20)?(\d{2})\b", RegexOptions.Compiled)]
    private static partial Regex SeasonYearRangePattern();

    [GeneratedRegex(@"\b((?:19|20)\d{2})\b", RegexOptions.Compiled)]
    private static partial Regex SingleYearPattern();

    [GeneratedRegex(@"\b(winter|summer|spring|autumn|fall)\s*((?:19|20)\d{2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex SeasonWordPattern();

    [GeneratedRegex(@"\b((?:19|20)\d{2})\s*(winter|summer|spring|autumn|fall)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex YearFirstSeasonWordPattern();

    [GeneratedRegex(@"'(\d{2})\b", RegexOptions.Compiled)]
    private static partial Regex ApostropheYearPattern();

    // ── Pre-analysis ───────────────────────────────────────────────

    /// <summary>
    /// Pre-parse all HTML files in each group to count actual data entities.
    /// This lets us show the user what data each season contains BEFORE creating any seasons.
    /// Groups with no data are auto-deselected.
    /// </summary>
    public static async Task AnalyzeGroupsAsync(
        List<SeasonGroup> groups,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var files = groups.SelectMany(g => g.Files).ToList();
        var parsedFiles = new Dictionary<string, HtmlLeagueParser.HtmlParseResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files.Where(f => f.FileType == "HTML"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = await HtmlLeagueParser.ParseHtmlFileAsync(file.FilePath);
            parsedFiles[file.FilePath] = parsed;
            if (parsed.Success) RefineSeason(file, parsed);
            progress?.Report(new ScanProgress { CurrentPath = file.FileName, FilesScanned = parsedFiles.Count, FilesFound = files.Count(f => f.FileType == "HTML") });
        }
        var refinedGroups = new LeagueFileDiscoveryService().GroupBySeason(files);
        groups.Clear();
        groups.AddRange(refinedGroups);
        int totalFiles = groups.Sum(g => g.Files.Count(f => f.FileType == "HTML"));
        int processed = 0;

        foreach (var group in groups)
        {
            if (cancellationToken.IsCancellationRequested) break;

            // Track unique names across all files in this season group
            var teamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var playerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var divisionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int resultCount = 0;
            int competitionCount = 0;
            int doublesCount = 0;

            foreach (var file in group.Files.Where(f => f.FileType == "HTML"))
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    processed++;
                    progress?.Report(new ScanProgress
                    {
                        CurrentPath = file.FileName,
                        FilesScanned = processed,
                        FilesFound = totalFiles
                    });

                    var result = parsedFiles[file.FilePath];
                    if (!result.Success) continue;

                    foreach (var team in result.Teams)
                    {
                        teamNames.Add(team.Name);
                        if (!string.IsNullOrWhiteSpace(team.Division))
                            divisionNames.Add(team.Division);
                    }

                    foreach (var player in result.Players)
                    {
                        playerNames.Add(player.Name);
                        if (!string.IsNullOrWhiteSpace(player.Division))
                            divisionNames.Add(player.Division);
                    }

                    resultCount += result.Results.Count;
                    competitionCount += result.DetectedCompetitions.Count;
                    doublesCount += result.DoublesEntries.Count;

                    // Pick up divisions from doubles entries
                    foreach (var d in result.DoublesEntries)
                    {
                        if (!string.IsNullOrWhiteSpace(d.Division))
                            divisionNames.Add(d.Division);
                    }

                    // Also pick up division from page heading
                    if (!string.IsNullOrWhiteSpace(result.DetectedDivision))
                        divisionNames.Add(result.DetectedDivision);
                }
                catch
                {
                    // Skip files that fail to parse
                }
            }

            group.AnalyzedTeams = teamNames.Count;
            group.AnalyzedPlayers = playerNames.Count;
            group.AnalyzedResults = resultCount;
            group.AnalyzedCompetitions = competitionCount;
            group.AnalyzedDoublesPairings = doublesCount;
            group.AnalyzedDivisions = divisionNames.Count;
            group.IsAnalyzed = true;
            var schemes = divisionNames.Select(DivisionSeasonTerm).Where(t => t != null).Distinct().ToList();
            if (schemes.Count > 1)
            {
                group.ReviewReason = "Numbered winter divisions and colored summer divisions occur together. Separate the source seasons before importing.";
                group.IsSelected = false;
                group.ExistingSeasonId = null;
                group.ExistingSeasonName = null;
            }

            // Auto-deselect groups with no detectable data
            if (!group.HasData && group.Files.All(f => f.FileType == "HTML"))
                group.IsSelected = false;
        }
    }

    public static void RefineSeason(DiscoveredFile file, HtmlLeagueParser.HtmlParseResult parsed)
    {
        var key = NormalizeSeasonKey(file.DetectedSeason);
        var year = Regex.Match(key, @"\b(?:19|20)\d{2}(?:-\d{2})?\b");
        if (!year.Success || key.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)) return;
        var terms = Regex.Matches(parsed.PageTitle + " " + parsed.PageHeading, @"\b(winter|summer|spring|autumn|fall)\b", RegexOptions.IgnoreCase)
            .Select(m => m.Value.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var division in parsed.Teams.Select(t => t.Division)
            .Concat(parsed.Players.Select(p => p.Division))
            .Concat(parsed.Results.Select(r => r.Division))
            .Append(parsed.DetectedDivision ?? ""))
        {
            var term = DivisionSeasonTerm(division);
            if (term != null) terms.Add(term);
        }
        if (HasSeasonTerm(key)) terms.Add(key.Split(' ')[0].ToLowerInvariant());
        if (terms.Count > 1)
        {
            file.SeasonReviewReason = "Conflicting season or division evidence in source files. Separate summer and winter before importing.";
            return;
        }
        if (terms.Count == 1)
            file.DetectedSeason = NormalizeSeasonKey($"{terms.Single()} {year.Value}");
    }

    private static string? DivisionSeasonTerm(string division)
    {
        var core = Wdpl2.Helpers.DivisionHelper.StripDivisionSuffix(division).Trim();
        if (Regex.IsMatch(core, @"^(red|green|yellow|blue|white|black|orange|purple)$", RegexOptions.IgnoreCase)) return "summer";
        if (Regex.IsMatch(core, @"^(first|second|third|fourth|fifth|sixth|[1-6](?:st|nd|rd|th)?)$", RegexOptions.IgnoreCase)) return "winter";
        return null;
    }
}
