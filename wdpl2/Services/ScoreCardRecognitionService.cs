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
/// 
/// OCR Format from Azure Vision (line by line):
///   HOME_PLAYER_NAME
///   HOME_SCORE (0 = loss)
///   AWAY_PLAYER_NAME  
///   [AWAY_SCORE sometimes missing - if home=0, away wins]
/// 
/// Repeats for each frame. Same player can appear multiple times.
/// </summary>
public sealed class ScoreCardRecognitionService
{
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
        public string? ParsingStrategy { get; set; }
    }

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
    private readonly IOcrService _ocrService;

    public ScoreCardRecognitionService()
    {
        _availablePlayers = DataStore.Data.Players.ToList();
        _ocrService = OcrPlugin.Default;
    }

    public async Task<bool> IsOcrAvailableAsync()
    {
        try { await _ocrService.InitAsync(); return true; }
        catch { return false; }
    }

    public async Task<RecognitionResult> RecognizeFromFileAsync(string filePath)
    {
        try
        {
            var imageData = await File.ReadAllBytesAsync(filePath);
            return await RecognizeFromImageAsync(imageData);
        }
        catch (Exception ex)
        {
            return new RecognitionResult { Success = false, Message = "Failed to read image", Errors = { ex.Message } };
        }
    }

    public async Task<RecognitionResult> RecognizeFromImageAsync(byte[] imageData)
    {
        var result = new RecognitionResult { ProcessedImageData = imageData };
        try
        {
            var ocrText = await PerformOcrAsync(imageData);
            result.RawOcrText = ocrText;
            if (string.IsNullOrWhiteSpace(ocrText))
            {
                result.Success = false;
                result.Message = "No text extracted";
            }
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            return result;
        }
    }

    /// <summary>
    /// Main entry point for parsing OCR text
    /// </summary>
    public RecognitionResult RecognizeFromOcrText(string ocrText, byte[]? imageData = null, Guid? homeTeamId = null, Guid? awayTeamId = null, int expectedFrames = 15)
    {
        var result = new RecognitionResult 
        { 
            ProcessedImageData = imageData, 
            RawOcrText = ocrText,
            ParsingStrategy = "LineByLine"
        };

        try
        {
            if (string.IsNullOrWhiteSpace(ocrText))
            {
                result.Success = false;
                result.Message = "No OCR text provided";
                return result;
            }

            System.Diagnostics.Debug.WriteLine("=== SCORE CARD RECOGNITION ===");

            var homePlayers = homeTeamId.HasValue 
                ? _availablePlayers.Where(p => p.TeamId == homeTeamId.Value).ToList()
                : new List<Player>();
            var awayPlayers = awayTeamId.HasValue 
                ? _availablePlayers.Where(p => p.TeamId == awayTeamId.Value).ToList()
                : new List<Player>();

            System.Diagnostics.Debug.WriteLine($"Home Players ({homePlayers.Count}): {string.Join(", ", homePlayers.Select(p => p.FullName))}");
            System.Diagnostics.Debug.WriteLine($"Away Players ({awayPlayers.Count}): {string.Join(", ", awayPlayers.Select(p => p.FullName))}");

            // Parse the OCR text line by line
            var frames = ParseFramesLineByLine(ocrText, homePlayers, awayPlayers, expectedFrames);

            // Fill remaining empty frames
            while (frames.Count < expectedFrames)
            {
                frames.Add(new RecognizedFrame 
                { 
                    FrameNumber = frames.Count + 1, 
                    Winner = FrameWinner.None, 
                    Confidence = 0 
                });
            }

            result.Frames = frames.Take(expectedFrames).ToList();
            result.HomeScore = result.Frames.Count(f => f.Winner == FrameWinner.Home);
            result.AwayScore = result.Frames.Count(f => f.Winner == FrameWinner.Away);

            var complete = result.Frames.Count(f => f.MatchedHomePlayerId.HasValue && f.MatchedAwayPlayerId.HasValue);
            result.Confidence = complete > 0 
                ? result.Frames.Where(f => f.Confidence > 0).DefaultIfEmpty().Average(f => f?.Confidence ?? 0) 
                : 0;
            result.Success = complete > 0;
            result.Message = $"Recognized {complete} frames, score {result.HomeScore}-{result.AwayScore}";

            System.Diagnostics.Debug.WriteLine($"=== RESULT: {result.Message} ===");
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            return result;
        }
    }

    /// <summary>
    /// Parse frames from OCR text in line-by-line format:
    /// HOME_PLAYER, SCORE, AWAY_PLAYER, [SCORE]
    /// </summary>
    private List<RecognizedFrame> ParseFramesLineByLine(string ocrText, List<Player> homePlayers, List<Player> awayPlayers, int expectedFrames)
    {
        var frames = new List<RecognizedFrame>();

        // Split and clean lines
        var allLines = ocrText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        System.Diagnostics.Debug.WriteLine($"=== PARSING {allLines.Count} LINES ===");

        // Classify each line
        var classified = new List<(int index, string text, string type, Player? player, double conf, int? score)>();

        for (int i = 0; i < allLines.Count; i++)
        {
            var line = allLines[i];
            var lower = line.ToLower();

            // Skip header lines
            if (IsHeaderLine(lower))
            {
                System.Diagnostics.Debug.WriteLine($"  [{i:D2}] HEADER: '{line}'");
                continue;
            }

            // Check if this line is ONLY a score
            var scoreOnly = ParseScoreOnly(line);
            if (scoreOnly.HasValue)
            {
                classified.Add((i, line, "SCORE", null, 1.0, scoreOnly.Value));
                System.Diagnostics.Debug.WriteLine($"  [{i:D2}] SCORE={scoreOnly}: '{line}'");
                continue;
            }

            // Try to match as a player name (with possible embedded score)
            var (cleanName, embeddedScore) = ExtractEmbeddedScore(line);
            var (homeMatch, homeConf) = FindBestPlayerMatch(cleanName, homePlayers);
            var (awayMatch, awayConf) = FindBestPlayerMatch(cleanName, awayPlayers);

            if (homeMatch != null && homeConf >= awayConf && homeConf >= 0.4)
            {
                classified.Add((i, line, "HOME", homeMatch, homeConf, embeddedScore));
                System.Diagnostics.Debug.WriteLine($"  [{i:D2}] HOME ({homeConf:P0}): '{line}' -> {homeMatch.FullName}" + (embeddedScore.HasValue ? $" [score={embeddedScore}]" : ""));
            }
            else if (awayMatch != null && awayConf >= 0.4)
            {
                classified.Add((i, line, "AWAY", awayMatch, awayConf, embeddedScore));
                System.Diagnostics.Debug.WriteLine($"  [{i:D2}] AWAY ({awayConf:P0}): '{line}' -> {awayMatch.FullName}" + (embeddedScore.HasValue ? $" [score={embeddedScore}]" : ""));
            }
            else
            {
                classified.Add((i, line, "???", null, 0, embeddedScore));
                System.Diagnostics.Debug.WriteLine($"  [{i:D2}] ???: '{line}'" + (embeddedScore.HasValue ? $" [score={embeddedScore}]" : ""));
            }
        }

        System.Diagnostics.Debug.WriteLine($"=== BUILDING FRAMES from {classified.Count} classified lines ===");

        // Build frames: Pattern is HOME, SCORE, AWAY, [SCORE]
        int idx = 0;
        while (idx < classified.Count && frames.Count < expectedFrames)
        {
            var current = classified[idx];

            // Start frame when we find a HOME player
            if (current.type == "HOME")
            {
                var frame = BuildFrame(classified, ref idx, frames.Count + 1);
                if (frame != null)
                {
                    frames.Add(frame);
                    System.Diagnostics.Debug.WriteLine($"  Frame {frames.Count}: {frame.HomePlayerName} vs {frame.AwayPlayerName} [{frame.Winner}]");
                }
            }
            // Or start with AWAY if that's what we have
            else if (current.type == "AWAY")
            {
                var frame = BuildFrameStartingAway(classified, ref idx, frames.Count + 1);
                if (frame != null)
                {
                    frames.Add(frame);
                    System.Diagnostics.Debug.WriteLine($"  Frame {frames.Count} (A): {frame.HomePlayerName} vs {frame.AwayPlayerName} [{frame.Winner}]");
                }
            }
            else
            {
                idx++; // Skip scores and unknowns when looking for frame start
            }
        }

        System.Diagnostics.Debug.WriteLine($"=== EXTRACTED {frames.Count} FRAMES ===");
        return frames;
    }

    private RecognizedFrame? BuildFrame(List<(int index, string text, string type, Player? player, double conf, int? score)> lines, ref int idx, int frameNumber)
    {
        var homeLine = lines[idx];
        idx++; // Consume home line

        Player? homePlayer = homeLine.player;
        double homeConf = homeLine.conf;
        int? homeScore = homeLine.score;

        Player? awayPlayer = null;
        double awayConf = 0;
        int? awayScore = null;

        // Look for: [SCORE], AWAY, [SCORE]
        int lookAhead = 0;
        while (idx < lines.Count && lookAhead < 5)
        {
            var next = lines[idx];

            if (next.type == "SCORE")
            {
                // Assign score to whoever doesn't have one yet
                if (!homeScore.HasValue)
                {
                    homeScore = next.score;
                    idx++;
                }
                else if (awayPlayer != null && !awayScore.HasValue)
                {
                    awayScore = next.score;
                    idx++;
                }
                else
                {
                    idx++; // Skip extra score
                }
                lookAhead++;
                continue;
            }

            if (next.type == "AWAY")
            {
                awayPlayer = next.player;
                awayConf = next.conf;
                awayScore = next.score;
                idx++; // Consume away line
                
                // Look one more for away score if not embedded
                if (!awayScore.HasValue && idx < lines.Count && lines[idx].type == "SCORE")
                {
                    awayScore = lines[idx].score;
                    idx++;
                }
                break; // Found away player, frame complete
            }

            if (next.type == "HOME")
            {
                // Hit another home player - frame boundary
                break;
            }

            // Unknown line - skip it
            idx++;
            lookAhead++;
        }

        if (homePlayer != null && awayPlayer != null)
        {
            return new RecognizedFrame
            {
                FrameNumber = frameNumber,
                HomePlayerName = homePlayer.FullName,
                AwayPlayerName = awayPlayer.FullName,
                MatchedHomePlayerId = homePlayer.Id,
                MatchedAwayPlayerId = awayPlayer.Id,
                Winner = DetermineWinner(homeScore, awayScore),
                Confidence = (homeConf + awayConf) / 2
            };
        }

        return null;
    }

    private RecognizedFrame? BuildFrameStartingAway(List<(int index, string text, string type, Player? player, double conf, int? score)> lines, ref int idx, int frameNumber)
    {
        // This handles cases where OCR returned away player first
        var awayLine = lines[idx];
        idx++;

        Player? awayPlayer = awayLine.player;
        double awayConf = awayLine.conf;
        int? awayScore = awayLine.score;

        Player? homePlayer = null;
        double homeConf = 0;
        int? homeScore = null;

        int lookAhead = 0;
        while (idx < lines.Count && lookAhead < 5)
        {
            var next = lines[idx];

            if (next.type == "SCORE")
            {
                if (!awayScore.HasValue)
                {
                    awayScore = next.score;
                    idx++;
                }
                else if (homePlayer != null && !homeScore.HasValue)
                {
                    homeScore = next.score;
                    idx++;
                }
                else
                {
                    idx++;
                }
                lookAhead++;
                continue;
            }

            if (next.type == "HOME")
            {
                homePlayer = next.player;
                homeConf = next.conf;
                homeScore = next.score;
                idx++;
                break;
            }

            if (next.type == "AWAY")
            {
                break;
            }

            idx++;
            lookAhead++;
        }

        if (homePlayer != null && awayPlayer != null)
        {
            return new RecognizedFrame
            {
                FrameNumber = frameNumber,
                HomePlayerName = homePlayer.FullName,
                AwayPlayerName = awayPlayer.FullName,
                MatchedHomePlayerId = homePlayer.Id,
                MatchedAwayPlayerId = awayPlayer.Id,
                Winner = DetermineWinner(homeScore, awayScore),
                Confidence = (homeConf + awayConf) / 2
            };
        }

        return null;
    }

    private FrameWinner DetermineWinner(int? homeScore, int? awayScore)
    {
        // Score of 1 = win, 0 = loss
        if (homeScore == 1 && awayScore == 0) return FrameWinner.Home;
        if (homeScore == 0 && awayScore == 1) return FrameWinner.Away;
        if (homeScore == 1) return FrameWinner.Home;
        if (awayScore == 1) return FrameWinner.Away;
        // If home has 0 and away score unknown, away wins
        if (homeScore == 0 && !awayScore.HasValue) return FrameWinner.Away;
        if (awayScore == 0 && !homeScore.HasValue) return FrameWinner.Home;
        return FrameWinner.None;
    }

    /// <summary>
    /// Check if line is ONLY a score value (0, 1, O, etc.)
    /// </summary>
    private int? ParseScoreOnly(string line)
    {
        var t = line.Trim();
        // Loss (0)
        if (t == "0" || t == "O" || t == "o" || t == "D" || t == "Q") return 0;
        // Win (1)  
        if (t == "1" || t == "l" || t == "I" || t == "!" || t == "|") return 1;
        // Also match "0-0", "00-0", "5" at end etc as unknown
        return null;
    }

    /// <summary>
    /// Extract embedded score from end of line: "PLAYER NAME 0" or "0 PLAYER NAME"
    /// </summary>
    private (string cleanedLine, int? score) ExtractEmbeddedScore(string line)
    {
        // Pattern: "NAME 0" or "NAME O" at end
        var endMatch = Regex.Match(line, @"^(.+?)\s+([0OoDQ1lI!|])$");
        if (endMatch.Success)
        {
            var scoreChar = endMatch.Groups[2].Value;
            int? score = (scoreChar == "0" || scoreChar == "O" || scoreChar == "o" || scoreChar == "D" || scoreChar == "Q") ? 0 : 1;
            return (endMatch.Groups[1].Value.Trim(), score);
        }

        // Pattern: "0 NAME" at start
        var startMatch = Regex.Match(line, @"^([0OoDQ1lI!|])\s+(.+)$");
        if (startMatch.Success)
        {
            var scoreChar = startMatch.Groups[1].Value;
            int? score = (scoreChar == "0" || scoreChar == "O" || scoreChar == "o" || scoreChar == "D" || scoreChar == "Q") ? 0 : 1;
            return (startMatch.Groups[2].Value.Trim(), score);
        }

        return (line, null);
    }

    /// <summary>
    /// Find best matching player using fuzzy matching to handle OCR errors
    /// </summary>
    private (Player? player, double confidence) FindBestPlayerMatch(string text, List<Player> players)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
            return (null, 0);

        Player? best = null;
        double bestConf = 0;

        var normalizedText = NormalizeForMatching(text);

        foreach (var player in players)
        {
            var (isMatch, confidence) = CalculateMatchConfidence(normalizedText, player);
            if (isMatch && confidence > bestConf)
            {
                best = player;
                bestConf = confidence;
            }
        }

        return (best, bestConf);
    }

    private (bool isMatch, double confidence) CalculateMatchConfidence(string ocrText, Player player)
    {
        var fullName = NormalizeForMatching(player.FullName ?? "");
        var firstName = NormalizeForMatching(player.FirstName ?? "");
        var lastName = NormalizeForMatching(player.LastName ?? "");

        if (string.IsNullOrEmpty(fullName)) return (false, 0);

        // Exact match
        if (ocrText == fullName)
            return (true, 1.0);

        // OCR text contains full name
        if (ocrText.Contains(fullName))
            return (true, 0.95);

        // Full name contains OCR text (truncated name)
        if (fullName.Contains(ocrText) && ocrText.Length >= 5)
            return (true, 0.85);

        // Both first and last name present (in any order)
        if (firstName.Length >= 2 && lastName.Length >= 2)
        {
            if (ocrText.Contains(firstName) && ocrText.Contains(lastName))
                return (true, 0.9);
        }

        // Last name only (4+ chars for reliability)
        if (lastName.Length >= 4 && ocrText.Contains(lastName))
            return (true, 0.75);

        // First name + partial last name (for OCR errors like "LENINDONO" -> "LENINDON")
        if (firstName.Length >= 3 && ocrText.Contains(firstName) && lastName.Length >= 4)
        {
            // Check if OCR contains at least first 4 chars of last name
            var lastPrefix = lastName.Substring(0, Math.Min(4, lastName.Length));
            if (ocrText.Contains(lastPrefix))
                return (true, 0.7);

            // Or any 4+ char substring of last name
            for (int len = lastName.Length; len >= 4; len--)
            {
                for (int start = 0; start <= lastName.Length - len; start++)
                {
                    var sub = lastName.Substring(start, len);
                    if (ocrText.Contains(sub))
                        return (true, 0.5 + 0.25 * len / lastName.Length);
                }
            }
        }

        // Levenshtein distance for fuzzy matching
        int distance = LevenshteinDistance(ocrText, fullName);
        int maxLen = Math.Max(ocrText.Length, fullName.Length);
        double similarity = 1.0 - (double)distance / maxLen;

        // Accept if similarity >= 55% (handles OCR errors like LONIS->LEWIS, STOVE->STEVE)
        if (similarity >= 0.55)
            return (true, similarity * 0.7);

        return (false, 0);
    }

    private string NormalizeForMatching(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        // Remove all non-alphanumeric, collapse spaces
        s = Regex.Replace(s, @"[^\w\s]", "");
        s = Regex.Replace(s, @"\s+", " ");
        return s.Trim().ToLower();
    }

    private int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;
        
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        
        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[a.Length, b.Length];
    }

    private bool IsHeaderLine(string lower)
    {
        // Skip header/label lines
        var skipPatterns = new[] { 
            "home", "away", "player", "team", "score", "frame", "date", "division",
            "wellington", "district", "pool", "league", "match", "result", "full name",
            "captain", "before start", "games 11", "completed", "total", "signature",
            "referee", "venue", "div.", "ensure", "repeat", "pairing", "only", 
            "old blues", "mylons", "minion", "new player", "8 ball", "capt", 
            "town", "18/", "19/", "20/", "21/", "22/", "23/", "24/", "25/" // dates
        };
        
        return skipPatterns.Any(p => lower.Contains(p)) || lower.Length < 3;
    }

    private async Task<string> PerformOcrAsync(byte[] imageData)
    {
        try
        {
            await _ocrService.InitAsync();
            var result = await _ocrService.RecognizeTextAsync(imageData);
            return result?.AllText ?? "";
        }
        catch { return ""; }
    }
}
