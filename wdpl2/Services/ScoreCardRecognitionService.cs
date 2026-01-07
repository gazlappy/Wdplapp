using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Plugin.Maui.OCR;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Service for recognizing and parsing pool league score cards from photos.
/// Uses Plugin.Maui.OCR for cross-platform OCR (ML Kit on Android, Vision on iOS/Mac, Windows OCR).
/// </summary>
public sealed class ScoreCardRecognitionService
{
    /// <summary>
    /// Result from score card recognition
    /// </summary>
    public sealed class RecognitionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<RecognizedFrame> Frames { get; set; } = new();
        public string? HomeTeamName { get; set; }
        public string? AwayTeamName { get; set; }
        public DateTime? MatchDate { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public double Confidence { get; set; }
        public byte[]? ProcessedImageData { get; set; }
        public string? RawOcrText { get; set; }
    }

    /// <summary>
    /// A recognized frame from the score card
    /// </summary>
    public sealed class RecognizedFrame
    {
        public int FrameNumber { get; set; }
        public string? HomePlayerName { get; set; }
        public string? AwayPlayerName { get; set; }
        public FrameWinner Winner { get; set; } = FrameWinner.None;
        public bool EightBall { get; set; }
        public double Confidence { get; set; }
        public Guid? MatchedHomePlayerId { get; set; }
        public Guid? MatchedAwayPlayerId { get; set; }
    }

    private readonly List<Player> _availablePlayers;
    private readonly List<Team> _availableTeams;
    private readonly IOcrService _ocrService;

    public ScoreCardRecognitionService()
    {
        _availablePlayers = DataStore.Data.Players.ToList();
        _availableTeams = DataStore.Data.Teams.ToList();
        _ocrService = OcrPlugin.Default;
    }

    /// <summary>
    /// Check if OCR is available on this platform
    /// </summary>
    public async Task<bool> IsOcrAvailableAsync()
    {
        try
        {
            await _ocrService.InitAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Recognize score card from image file path
    /// </summary>
    public async Task<RecognitionResult> RecognizeFromFileAsync(string filePath)
    {
        try
        {
            var imageData = await File.ReadAllBytesAsync(filePath);
            return await RecognizeFromImageAsync(imageData);
        }
        catch (Exception ex)
        {
            return new RecognitionResult
            {
                Success = false,
                Message = "Failed to read image file",
                Errors = { ex.Message }
            };
        }
    }

    /// <summary>
    /// Recognize score card from image bytes
    /// </summary>
    public async Task<RecognitionResult> RecognizeFromImageAsync(byte[] imageData)
    {
        var result = new RecognitionResult
        {
            ProcessedImageData = imageData
        };

        try
        {
            // Extract text from image using OCR
            var ocrText = await PerformOcrAsync(imageData);
            result.RawOcrText = ocrText;

            if (string.IsNullOrWhiteSpace(ocrText))
            {
                result.Success = false;
                result.Message = "No text could be extracted from the image";
                result.Errors.Add("OCR returned empty result - ensure the image is clear and properly lit");
                return result;
            }

            // Parse the extracted text
            result = ParseScoreCardText(ocrText, result);
            
            // Try to match player names to known players
            MatchPlayersToKnownPlayers(result);

            // Calculate overall confidence
            if (result.Frames.Any())
            {
                result.Confidence = result.Frames.Average(f => f.Confidence);
                result.Success = result.Confidence > 0.3;
                result.Message = result.Success 
                    ? $"Recognized {result.Frames.Count} frames with {result.Confidence:P0} confidence"
                    : "Low confidence - please verify results manually";
            }
            else
            {
                result.Success = false;
                result.Message = "Could not identify any frames in the score card";
                result.Warnings.Add("Try to ensure the entire score card is visible in the photo");
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = "Error processing score card";
            result.Errors.Add(ex.Message);
            return result;
        }
    }

    /// <summary>
    /// Recognize score card from pre-extracted OCR text (e.g. from Azure Vision)
    /// </summary>
    public RecognitionResult RecognizeFromOcrText(string ocrText, byte[]? imageData = null)
    {
        var result = new RecognitionResult
        {
            ProcessedImageData = imageData,
            RawOcrText = ocrText
        };

        try
        {
            if (string.IsNullOrWhiteSpace(ocrText))
            {
                result.Success = false;
                result.Message = "No OCR text provided";
                result.Errors.Add("OCR text is empty");
                return result;
            }

            // Log raw OCR text for debugging
            System.Diagnostics.Debug.WriteLine("=== RAW OCR TEXT FROM AZURE ===");
            System.Diagnostics.Debug.WriteLine(ocrText);
            System.Diagnostics.Debug.WriteLine("=== END RAW OCR TEXT ===");

            // Parse the extracted text using strict patterns first
            result = ParseScoreCardText(ocrText, result);
            
            // If no frames found with strict patterns, try flexible extraction
            // (Only matches against known players in database)
            if (!result.Frames.Any())
            {
                System.Diagnostics.Debug.WriteLine("No frames found with strict patterns, trying flexible extraction...");
                ExtractFramesFromKnownPlayers(ocrText, result);
            }
            
            // Try to match player names to known players
            MatchPlayersToKnownPlayers(result);

            // Calculate overall confidence
            if (result.Frames.Any())
            {
                result.Confidence = result.Frames.Average(f => f.Confidence);
                result.Success = result.Confidence > 0.3;
                result.Message = result.Success 
                    ? $"Recognized {result.Frames.Count} frames with {result.Confidence:P0} confidence"
                    : "Low confidence - please verify results manually";
            }
            else
            {
                result.Success = false;
                result.Message = "Could not identify any frames in the score card";
                result.Warnings.Add("No known players found in OCR text");
                result.Warnings.Add($"Make sure players exist in the database ({_availablePlayers.Count} players available)");
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = "Error processing score card";
            result.Errors.Add(ex.Message);
            return result;
        }
    }

    /// <summary>
    /// Perform OCR on the image using Plugin.Maui.OCR
    /// </summary>
    private async Task<string> PerformOcrAsync(byte[] imageData)
    {
        try
        {
            // Initialize OCR service
            await _ocrService.InitAsync();

            // Perform OCR recognition
            var ocrResult = await _ocrService.RecognizeTextAsync(imageData);

            if (ocrResult == null || !ocrResult.Success)
            {
                System.Diagnostics.Debug.WriteLine("OCR failed or returned null result");
                return "";
            }

            // Return the extracted text
            return ocrResult.AllText ?? "";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OCR error: {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// Parse OCR text to extract score card data
    /// </summary>
    private RecognitionResult ParseScoreCardText(string ocrText, RecognitionResult result)
    {
        var lines = ocrText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        // Try to find team names (often at the top of the card)
        ExtractTeamNames(lines, result);

        // Try to find date
        ExtractMatchDate(lines, result);

        // Try to extract frame rows
        ExtractFrames(lines, result);

        return result;
    }

    private void ExtractTeamNames(List<string> lines, RecognitionResult result)
    {
        // Look for patterns like "Home: Team Name" or "Away: Team Name"
        // Or team names as headers
        
        foreach (var line in lines.Take(15)) // Usually at the top
        {
            var lower = line.ToLower();
            
            if (lower.Contains("home") && !lower.Contains("player"))
            {
                var teamName = ExtractValueAfterColon(line);
                if (!string.IsNullOrWhiteSpace(teamName))
                {
                    result.HomeTeamName = teamName;
                }
            }
            else if (lower.Contains("away") && !lower.Contains("player"))
            {
                var teamName = ExtractValueAfterColon(line);
                if (!string.IsNullOrWhiteSpace(teamName))
                {
                    result.AwayTeamName = teamName;
                }
            }
            else if (lower.Contains(" v ") || lower.Contains(" vs ") || lower.Contains(" v. "))
            {
                // Pattern: "Team A v Team B" or "Team A vs Team B"
                var parts = Regex.Split(line, @"\s+v\.?\s+|\s+vs\.?\s+", RegexOptions.IgnoreCase);
                if (parts.Length == 2)
                {
                    result.HomeTeamName = CleanTeamName(parts[0]);
                    result.AwayTeamName = CleanTeamName(parts[1]);
                }
            }
        }

        // Try to match to known teams
        if (!string.IsNullOrWhiteSpace(result.HomeTeamName))
        {
            var matched = FindBestMatchingTeam(result.HomeTeamName);
            if (matched != null)
            {
                result.HomeTeamName = matched.Name;
            }
        }
        
        if (!string.IsNullOrWhiteSpace(result.AwayTeamName))
        {
            var matched = FindBestMatchingTeam(result.AwayTeamName);
            if (matched != null)
            {
                result.AwayTeamName = matched.Name;
            }
        }
    }

    private string CleanTeamName(string name)
    {
        // Remove common prefixes/suffixes
        name = Regex.Replace(name, @"^(home|away)\s*:?\s*", "", RegexOptions.IgnoreCase);
        return name.Trim();
    }

    private void ExtractMatchDate(List<string> lines, RecognitionResult result)
    {
        // Look for date patterns
        var datePatterns = new[]
        {
            @"\b(\d{1,2})[/\-\.](\d{1,2})[/\-\.](\d{2,4})\b", // dd/mm/yyyy, dd-mm-yyyy, dd.mm.yyyy
            @"\b(\d{1,2})\s+(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+(\d{2,4})\b", // dd Mon yyyy
            @"\b(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+(\d{1,2}),?\s+(\d{2,4})\b" // Mon dd, yyyy
        };

        foreach (var line in lines)
        {
            foreach (var pattern in datePatterns)
            {
                var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    if (DateTime.TryParse(match.Value, out var date))
                    {
                        result.MatchDate = date;
                        return;
                    }
                }
            }
        }
    }

    private void ExtractFrames(List<string> lines, RecognitionResult result)
    {
        // Score card frame patterns - multiple formats supported:
        // Format 1: "1. John Smith  1-0  Jane Doe"
        // Format 2: "1 | John Smith | 1 | 0 | Jane Doe"  
        // Format 3: "John Smith 1 0 Jane Doe" (with frame number on separate line or implied)
        // Format 4: "1 John Smith W L Jane Doe" (W/L markers)
        // Format 5: "1 John Smith ? ? Jane Doe" (checkmarks)
        
        var framePatterns = new[]
        {
            // Frame# Name Score-Score Name
            new Regex(@"^(\d{1,2})\s*[.\|)\s]+\s*(.+?)\s+(\d)\s*[-:]\s*(\d)\s+(.+)$", RegexOptions.IgnoreCase),
            
            // Frame# Name 1 0 Name or Frame# Name 0 1 Name
            new Regex(@"^(\d{1,2})\s+(.+?)\s+([01])\s+([01])\s+(.+)$", RegexOptions.IgnoreCase),
            
            // Frame# Name W/L markers Name  
            new Regex(@"^(\d{1,2})\s+(.+?)\s+([WwLl??xX1])\s+([WwLl??xX0])\s+(.+)$", RegexOptions.IgnoreCase),
            
            // Name vs Name with score (no frame number - we'll add it)
            new Regex(@"^(.+?)\s+(\d)\s*[-:]\s*(\d)\s+(.+)$", RegexOptions.IgnoreCase),
            
            // Pipe-separated: Frame | Home | Score | Score | Away
            new Regex(@"^\|?\s*(\d{1,2})\s*\|\s*(.+?)\s*\|\s*(\d)\s*\|\s*(\d)\s*\|\s*(.+?)\s*\|?$", RegexOptions.IgnoreCase)
        };

        int inferredFrameNumber = 1;
        var processedFrameNumbers = new HashSet<int>();

        foreach (var line in lines)
        {
            RecognizedFrame? frame = null;
            
            foreach (var pattern in framePatterns)
            {
                var match = pattern.Match(line);
                if (match.Success)
                {
                    // Determine frame number
                    int frameNum;
                    string homePlayer, awayPlayer;
                    string homeScore, awayScore;

                    if (match.Groups.Count == 6)
                    {
                        // Pattern with frame number
                        frameNum = int.Parse(match.Groups[1].Value);
                        homePlayer = match.Groups[2].Value;
                        homeScore = match.Groups[3].Value;
                        awayScore = match.Groups[4].Value;
                        awayPlayer = match.Groups[5].Value;
                    }
                    else if (match.Groups.Count == 5)
                    {
                        // Pattern without frame number
                        frameNum = inferredFrameNumber;
                        homePlayer = match.Groups[1].Value;
                        homeScore = match.Groups[2].Value;
                        awayScore = match.Groups[3].Value;
                        awayPlayer = match.Groups[4].Value;
                    }
                    else
                    {
                        continue;
                    }

                    // Skip if we've already processed this frame number
                    if (processedFrameNumbers.Contains(frameNum))
                        continue;

                    frame = new RecognizedFrame
                    {
                        FrameNumber = frameNum,
                        HomePlayerName = CleanPlayerName(homePlayer),
                        AwayPlayerName = CleanPlayerName(awayPlayer),
                        Winner = ParseWinnerFromScores(homeScore, awayScore),
                        Confidence = 0.7
                    };

                    processedFrameNumbers.Add(frameNum);
                    inferredFrameNumber = frameNum + 1;
                    break;
                }
            }

            // Look for 8-ball indicator in the line
            if (frame != null)
            {
                var lower = line.ToLower();
                if (lower.Contains("8") || lower.Contains("eight") || 
                    lower.Contains("dish") || lower.Contains("?") ||
                    lower.Contains("8ball") || lower.Contains("??"))
                {
                    frame.EightBall = true;
                    frame.Confidence *= 0.9; // Slightly less confident about 8-ball detection
                }

                result.Frames.Add(frame);
            }
        }

        // Sort frames by number
        result.Frames = result.Frames.OrderBy(f => f.FrameNumber).ToList();

        // Calculate scores
        result.HomeScore = result.Frames.Count(f => f.Winner == FrameWinner.Home);
        result.AwayScore = result.Frames.Count(f => f.Winner == FrameWinner.Away);
    }

    private string CleanPlayerName(string name)
    {
        // Remove common artifacts from OCR
        name = name.Trim();
        
        // Remove leading/trailing punctuation and numbers that aren't part of names
        name = Regex.Replace(name, @"^[\d\[\]\(\)\{\}\|\.]+\s*", "");
        name = Regex.Replace(name, @"\s*[\[\]\(\)\{\}\|]+$", "");
        
        // Remove score indicators that might be attached
        name = Regex.Replace(name, @"\s*[01WwLl??]\s*$", "");
        
        // Normalize whitespace
        name = Regex.Replace(name, @"\s+", " ");
        
        return name.Trim();
    }

    private FrameWinner ParseWinnerFromScores(string homeScore, string awayScore)
    {
        // Handle numeric scores
        if (int.TryParse(homeScore, out var h) && int.TryParse(awayScore, out var a))
        {
            if (h > a) return FrameWinner.Home;
            if (a > h) return FrameWinner.Away;
            return FrameWinner.None;
        }

        // Handle W/L or checkmark indicators
        var homeWin = IsWinIndicator(homeScore);
        var awayWin = IsWinIndicator(awayScore);

        if (homeWin && !awayWin) return FrameWinner.Home;
        if (awayWin && !homeWin) return FrameWinner.Away;
        
        return FrameWinner.None;
    }

    private bool IsWinIndicator(string mark)
    {
        mark = mark.Trim().ToUpper();
        return mark == "1" || mark == "W" || mark == "?" || mark == "?" || mark == "X";
    }

    private void MatchPlayersToKnownPlayers(RecognitionResult result)
    {
        foreach (var frame in result.Frames)
        {
            // Try to match home player
            if (!string.IsNullOrWhiteSpace(frame.HomePlayerName))
            {
                var matchedPlayer = FindBestMatchingPlayer(frame.HomePlayerName);
                if (matchedPlayer != null)
                {
                    frame.MatchedHomePlayerId = matchedPlayer.Id;
                    frame.HomePlayerName = matchedPlayer.FullName;
                    frame.Confidence += 0.2;
                }
            }

            // Try to match away player
            if (!string.IsNullOrWhiteSpace(frame.AwayPlayerName))
            {
                var matchedPlayer = FindBestMatchingPlayer(frame.AwayPlayerName);
                if (matchedPlayer != null)
                {
                    frame.MatchedAwayPlayerId = matchedPlayer.Id;
                    frame.AwayPlayerName = matchedPlayer.FullName;
                    frame.Confidence += 0.2;
                }
            }

            // Cap confidence at 1.0
            frame.Confidence = Math.Min(frame.Confidence, 1.0);
        }
    }

    private Player? FindBestMatchingPlayer(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var normalizedName = NormalizeName(name);

        // Exact match first
        var exactMatch = _availablePlayers.FirstOrDefault(p => 
            string.Equals(NormalizeName(p.FullName), normalizedName, StringComparison.OrdinalIgnoreCase));
        
        if (exactMatch != null)
            return exactMatch;

        // Fuzzy match using Levenshtein distance
        var bestMatch = _availablePlayers
            .Select(p => new 
            { 
                Player = p, 
                Score = CalculateNameSimilarity(normalizedName, NormalizeName(p.FullName)) 
            })
            .Where(x => x.Score > 0.6)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        return bestMatch?.Player;
    }

    private Team? FindBestMatchingTeam(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var normalizedName = NormalizeName(name);

        // Exact match first
        var exactMatch = _availableTeams.FirstOrDefault(t => 
            string.Equals(NormalizeName(t.Name ?? ""), normalizedName, StringComparison.OrdinalIgnoreCase));
        
        if (exactMatch != null)
            return exactMatch;

        // Fuzzy match
        var bestMatch = _availableTeams
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .Select(t => new 
            { 
                Team = t, 
                Score = CalculateNameSimilarity(normalizedName, NormalizeName(t.Name!)) 
            })
            .Where(x => x.Score > 0.5)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        return bestMatch?.Team;
    }

    private string NormalizeName(string name)
    {
        // Remove punctuation, extra spaces, and convert to lowercase
        name = Regex.Replace(name, @"[^\w\s]", "");
        name = Regex.Replace(name, @"\s+", " ");
        return name.Trim().ToLower();
    }

    /// <summary>
    /// Calculate similarity between two names using a combination of 
    /// Levenshtein distance and word matching
    /// </summary>
    private double CalculateNameSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0;

        // Method 1: Word-based matching (for names)
        var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        
        var commonWords = wordsA.Intersect(wordsB).Count();
        var totalWords = Math.Max(wordsA.Count, wordsB.Count);
        var wordSimilarity = totalWords > 0 ? (double)commonWords / totalWords : 0;

        // Method 2: Levenshtein distance (normalized)
        var levenshteinSimilarity = 1.0 - ((double)LevenshteinDistance(a, b) / Math.Max(a.Length, b.Length));

        // Method 3: Contains check (for partial matches)
        var containsSimilarity = 0.0;
        if (a.Contains(b) || b.Contains(a))
            containsSimilarity = 0.8;

        // Combine methods with weights
        return Math.Max(
            Math.Max(wordSimilarity * 0.8, levenshteinSimilarity * 0.9),
            containsSimilarity
        );
    }

    /// <summary>
    /// Calculate Levenshtein distance between two strings
    /// </summary>
    private int LevenshteinDistance(string s1, string s2)
    {
        var n = s1.Length;
        var m = s2.Length;
        var d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (var i = 0; i <= n; i++)
            d[i, 0] = i;

        for (var j = 0; j <= m; j++)
            d[0, j] = j;

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost
                );
            }
        }

        return d[n, m];
    }

    private string ExtractValueAfterColon(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < line.Length - 1)
        {
            return line.Substring(colonIndex + 1).Trim();
        }
        return "";
    }

    /// <summary>
    /// Create frame results from recognized data for applying to a fixture
    /// </summary>
    public List<FrameResult> CreateFrameResults(RecognitionResult recognition)
    {
        var frames = new List<FrameResult>();

        foreach (var recognized in recognition.Frames)
        {
            frames.Add(new FrameResult
            {
                Number = recognized.FrameNumber,
                HomePlayerId = recognized.MatchedHomePlayerId,
                AwayPlayerId = recognized.MatchedAwayPlayerId,
                Winner = recognized.Winner,
                EightBall = recognized.EightBall
            });
        }

        return frames;
    }

    /// <summary>
    /// Extract frames by matching OCR text against known players in the database.
    /// STRICT MODE: Only creates frames for players that exist in the database.
    /// Uses fuzzy matching to handle OCR misspellings.
    /// </summary>
    private void ExtractFramesFromKnownPlayers(string ocrText, RecognitionResult result)
    {
        var lines = ocrText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) && l.Length > 2)
            .ToList();

        System.Diagnostics.Debug.WriteLine($"=== STRICT PLAYER MATCHING ===");
        System.Diagnostics.Debug.WriteLine($"Processing {lines.Count} lines against {_availablePlayers.Count} known players");

        var matchedPlayers = new List<(int lineIndex, string ocrText, Player player, double confidence)>();
        
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lower = line.ToLower();
            
            // Skip obvious header/label lines
            if (IsHeaderLine(lower))
            {
                System.Diagnostics.Debug.WriteLine($"  [{i}] SKIP (header): '{line}'");
                continue;
            }

            // Skip if this matches a team name
            var matchedTeam = FindBestMatchingTeam(line);
            if (matchedTeam != null)
            {
                System.Diagnostics.Debug.WriteLine($"  [{i}] SKIP (team): '{line}' -> '{matchedTeam.Name}'");
                if (string.IsNullOrEmpty(result.HomeTeamName))
                    result.HomeTeamName = matchedTeam.Name;
                else if (string.IsNullOrEmpty(result.AwayTeamName) && matchedTeam.Name != result.HomeTeamName)
                    result.AwayTeamName = matchedTeam.Name;
                continue;
            }

            // Try to match the line (or parts of it) to a known player
            var playerMatches = FindPlayersInLine(line);
            
            foreach (var match in playerMatches)
            {
                // Don't add the same player twice
                if (!matchedPlayers.Any(m => m.player.Id == match.player.Id))
                {
                    matchedPlayers.Add((i, match.ocrText, match.player, match.confidence));
                    System.Diagnostics.Debug.WriteLine($"  [{i}] MATCHED: '{match.ocrText}' -> '{match.player.FullName}' ({match.confidence:P0})");
                }
            }

            if (!playerMatches.Any())
            {
                System.Diagnostics.Debug.WriteLine($"  [{i}] NO MATCH: '{line}'");
            }
        }

        System.Diagnostics.Debug.WriteLine($"Found {matchedPlayers.Count} matched players");

        // Extract score indicators from all lines
        var scoreInfo = ExtractAllScoreInfo(lines);
        
        // Pair players into frames (alternating home/away)
        int frameNum = 1;
        for (int i = 0; i < matchedPlayers.Count - 1; i += 2)
        {
            var home = matchedPlayers[i];
            var away = matchedPlayers[i + 1];

            // Try to determine winner from nearby score info
            var winner = FindWinnerForFrame(home.lineIndex, away.lineIndex, scoreInfo, lines);

            var frame = new RecognizedFrame
            {
                FrameNumber = frameNum++,
                HomePlayerName = home.player.FullName,
                AwayPlayerName = away.player.FullName,
                MatchedHomePlayerId = home.player.Id,
                MatchedAwayPlayerId = away.player.Id,
                Winner = winner,
                Confidence = (home.confidence + away.confidence) / 2
            };

            result.Frames.Add(frame);
            System.Diagnostics.Debug.WriteLine($"  Frame {frame.FrameNumber}: {frame.HomePlayerName} vs {frame.AwayPlayerName} [{winner}]");
        }

        // Calculate scores from frames
        result.HomeScore = result.Frames.Count(f => f.Winner == FrameWinner.Home);
        result.AwayScore = result.Frames.Count(f => f.Winner == FrameWinner.Away);
        
        System.Diagnostics.Debug.WriteLine($"=== END STRICT MATCHING: {result.Frames.Count} frames, score {result.HomeScore}-{result.AwayScore} ===");
    }

    /// <summary>
    /// Find all players mentioned in a line of text.
    /// Handles lines like "John Smith  Jane Doe" (two names on one line)
    /// </summary>
    private List<(string ocrText, Player player, double confidence)> FindPlayersInLine(string line)
    {
        var results = new List<(string ocrText, Player player, double confidence)>();
        
        // Clean the line first
        var cleaned = CleanPlayerName(line);
        
        // Try to match the whole line first
        var (player, confidence) = FindPlayerWithConfidence(cleaned);
        if (player != null && confidence >= 0.5)
        {
            results.Add((cleaned, player, confidence));
            return results;
        }

        // Try splitting at common separators and check each part
        var words = cleaned.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (words.Length >= 4) // Could be two names
        {
            // Try different split points
            for (int split = 2; split <= words.Length - 2; split++)
            {
                var part1 = string.Join(" ", words.Take(split));
                var part2 = string.Join(" ", words.Skip(split));
                
                var (player1, conf1) = FindPlayerWithConfidence(part1);
                var (player2, conf2) = FindPlayerWithConfidence(part2);
                
                if (player1 != null && conf1 >= 0.5 && player2 != null && conf2 >= 0.5 && player1.Id != player2.Id)
                {
                    results.Add((part1, player1, conf1));
                    results.Add((part2, player2, conf2));
                    return results;
                }
            }
        }

        // Try matching just first+last name combinations (2 words)
        if (words.Length >= 2)
        {
            var twoWordName = words[0] + " " + words[1];
            var (p, c) = FindPlayerWithConfidence(twoWordName);
            if (p != null && c >= 0.5)
            {
                results.Add((twoWordName, p, c));
            }
        }

        return results;
    }

    /// <summary>
    /// Find the best matching player with confidence score.
    /// Uses multiple matching strategies to handle OCR misspellings.
    /// </summary>
    private (Player? player, double confidence) FindPlayerWithConfidence(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length < 3)
            return (null, 0);

        // Skip numbers and obvious non-names
        if (Regex.IsMatch(name, @"^\d+$") || Regex.IsMatch(name, @"^[0-9\s\-:]+$"))
            return (null, 0);

        var normalizedInput = NormalizeName(name);
        if (normalizedInput.Length < 3)
            return (null, 0);

        // 1. Exact match (100% confidence)
        var exactMatch = _availablePlayers.FirstOrDefault(p => 
            string.Equals(NormalizeName(p.FullName), normalizedInput, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
            return (exactMatch, 1.0);

        // 2. Check if input contains both first and last name
        foreach (var player in _availablePlayers)
        {
            var firstName = NormalizeName(player.FirstName ?? "");
            var lastName = NormalizeName(player.LastName ?? "");
            
            if (firstName.Length >= 2 && lastName.Length >= 2 &&
                normalizedInput.Contains(firstName) && normalizedInput.Contains(lastName))
            {
                return (player, 0.95);
            }
        }

        // 3. Fuzzy match with strict threshold
        var candidates = _availablePlayers
            .Select(p => new 
            { 
                Player = p, 
                FullNameScore = CalculateNameSimilarity(normalizedInput, NormalizeName(p.FullName)),
                FirstNameScore = CalculateNameSimilarity(normalizedInput, NormalizeName(p.FirstName ?? "")),
                LastNameScore = CalculateNameSimilarity(normalizedInput, NormalizeName(p.LastName ?? ""))
            })
            .Select(x => new
            {
                x.Player,
                BestScore = Math.Max(
                    x.FullNameScore,
                    Math.Max(x.LastNameScore * 0.8, (x.FirstNameScore + x.LastNameScore) / 2)
                )
            })
            .Where(x => x.BestScore >= 0.5)
            .OrderByDescending(x => x.BestScore)
            .ToList();

        if (candidates.Any())
        {
            var best = candidates.First();
            return (best.Player, best.BestScore);
        }

        return (null, 0);
    }

    private bool IsHeaderLine(string lowerLine)
    {
        var skipPatterns = new[]
        {
            "home", "away", "player", "team", "score", "frame", "date", "division",
            "wellington", "district", "pool", "league", "match", "result",
            "full name", "captain", "before start", "games 11", "completed",
            "total", "signature", "referee", "venue", "div."
        };
        return skipPatterns.Any(p => lowerLine.Contains(p)) || lowerLine.Length < 4;
    }

    private List<(int lineIndex, FrameWinner winner)> ExtractAllScoreInfo(List<string> lines)
    {
        var scores = new List<(int lineIndex, FrameWinner winner)>();
        
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            
            // Look for "1 0" or "0 1" patterns
            var match = Regex.Match(line, @"\b([01])\s+([01])\b");
            if (match.Success)
            {
                var home = match.Groups[1].Value;
                var away = match.Groups[2].Value;
                if (home == "1" && away == "0")
                    scores.Add((i, FrameWinner.Home));
                else if (home == "0" && away == "1")
                    scores.Add((i, FrameWinner.Away));
            }
            
            // Look for tick marks
            if (line.Contains("?") || line.Contains("?") || line.Contains("?"))
            {
                // This line has a tick - need context to know which side
                scores.Add((i, FrameWinner.None)); // Placeholder
            }
        }
        
        return scores;
    }

    private FrameWinner FindWinnerForFrame(int homeLineIdx, int awayLineIdx, List<(int lineIndex, FrameWinner winner)> scores, List<string> lines)
    {
        // Look for score indicators near the player lines
        foreach (var (lineIdx, winner) in scores)
        {
            if (Math.Abs(lineIdx - homeLineIdx) <= 1 || Math.Abs(lineIdx - awayLineIdx) <= 1)
            {
                if (winner != FrameWinner.None)
                    return winner;
            }
        }
        
        // Check the actual player lines for embedded scores
        if (homeLineIdx < lines.Count)
        {
            var homeLine = lines[homeLineIdx];
            if (Regex.IsMatch(homeLine, @"\b1\s+0\b") || homeLine.EndsWith(" 1"))
                return FrameWinner.Home;
            if (Regex.IsMatch(homeLine, @"\b0\s+1\b"))
                return FrameWinner.Away;
        }
        
        return FrameWinner.None;
    }
}
