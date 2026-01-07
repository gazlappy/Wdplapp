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
}
