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
    private readonly List<Team> _availableTeams;
    private readonly IOcrService _ocrService;

    public ScoreCardRecognitionService()
    {
        _availablePlayers = DataStore.Data.Players.ToList();
        _availableTeams = DataStore.Data.Teams.ToList();
        _ocrService = OcrPlugin.Default;
    }

    public async Task<bool> IsOcrAvailableAsync()
    {
        try
        {
            await _ocrService.InitAsync();
            return true;
        }
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
            return new RecognitionResult { Success = false, Message = "Failed to read image file", Errors = { ex.Message } };
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
                result.Message = "No text could be extracted from the image";
                return result;
            }

            result = ParseScoreCardText(ocrText, result);
            result.Success = result.Frames.Any();
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
    /// Recognize score card from pre-extracted OCR text (e.g. from Azure Vision)
    /// </summary>
    public RecognitionResult RecognizeFromOcrText(string ocrText, byte[]? imageData = null, Guid? homeTeamId = null, Guid? awayTeamId = null, int expectedFrames = 15)
    {
        var result = new RecognitionResult { ProcessedImageData = imageData, RawOcrText = ocrText };

        try
        {
            if (string.IsNullOrWhiteSpace(ocrText))
            {
                result.Success = false;
                result.Message = "No OCR text provided";
                return result;
            }

            System.Diagnostics.Debug.WriteLine("=== RAW OCR TEXT FROM AZURE ===");
            System.Diagnostics.Debug.WriteLine(ocrText);
            System.Diagnostics.Debug.WriteLine("=== END RAW OCR TEXT ===");

            var homeTeamPlayers = homeTeamId.HasValue 
                ? _availablePlayers.Where(p => p.TeamId == homeTeamId.Value).ToList()
                : new List<Player>();
            var awayTeamPlayers = awayTeamId.HasValue 
                ? _availablePlayers.Where(p => p.TeamId == awayTeamId.Value).ToList()
                : new List<Player>();

            System.Diagnostics.Debug.WriteLine($"Home team has {homeTeamPlayers.Count} players, Away team has {awayTeamPlayers.Count} players");
            System.Diagnostics.Debug.WriteLine($"Expected frames: {expectedFrames}");

            if (homeTeamPlayers.Any() && awayTeamPlayers.Any())
            {
                ExtractFramesWithTeamContext(ocrText, result, homeTeamPlayers, awayTeamPlayers, expectedFrames);
            }

            if (result.Frames.Any())
            {
                result.Confidence = result.Frames.Where(f => f.Confidence > 0).DefaultIfEmpty().Average(f => f?.Confidence ?? 0);
                result.Success = true;
                result.Message = $"Recognized {result.Frames.Count(f => f.MatchedHomePlayerId.HasValue)} frames with players";
            }
            else
            {
                result.Success = false;
                result.Message = "Could not identify any frames";
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

    private async Task<string> PerformOcrAsync(byte[] imageData)
    {
        try
        {
            await _ocrService.InitAsync();
            var ocrResult = await _ocrService.RecognizeTextAsync(imageData);
            return ocrResult?.AllText ?? "";
        }
        catch { return ""; }
    }

    private RecognitionResult ParseScoreCardText(string ocrText, RecognitionResult result)
    {
        var lines = ocrText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        ExtractTeamNames(lines, result);
        return result;
    }

    private void ExtractTeamNames(List<string> lines, RecognitionResult result)
    {
        foreach (var line in lines.Take(15))
        {
            if (line.ToLower().Contains(" v ") || line.ToLower().Contains(" vs "))
            {
                var parts = Regex.Split(line, @"\s+v\.?\s+|\s+vs\.?\s+", RegexOptions.IgnoreCase);
                if (parts.Length >= 2)
                {
                    result.HomeTeamName = parts[0].Trim();
                    result.AwayTeamName = parts[1].Trim();
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Extract frames using team context - reads the score card layout properly
    /// </summary>
    private void ExtractFramesWithTeamContext(string ocrText, RecognitionResult result, List<Player> homePlayers, List<Player> awayPlayers, int expectedFrames)
    {
        var lines = ocrText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) && l.Length > 1)
            .ToList();

        System.Diagnostics.Debug.WriteLine($"=== TEAM-AWARE EXTRACTION ===");
        System.Diagnostics.Debug.WriteLine($"Processing {lines.Count} lines for {expectedFrames} expected frames");

        // Build a list of all player mentions with their line index
        var allMatches = new List<(int lineIndex, Player player, bool isHome, double confidence, string originalText)>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lower = line.ToLower();

            if (IsHeaderLine(lower))
                continue;

            // Check home players
            foreach (var player in homePlayers)
            {
                var (match, confidence) = MatchPlayerToLine(player, line);
                if (match && confidence >= 0.5)
                {
                    allMatches.Add((i, player, true, confidence, line));
                    System.Diagnostics.Debug.WriteLine($"  [{i}] HOME: '{line}' -> '{player.FullName}' ({confidence:P0})");
                    break;
                }
            }

            // Check away players
            foreach (var player in awayPlayers)
            {
                var (match, confidence) = MatchPlayerToLine(player, line);
                if (match && confidence >= 0.5)
                {
                    allMatches.Add((i, player, false, confidence, line));
                    System.Diagnostics.Debug.WriteLine($"  [{i}] AWAY: '{line}' -> '{player.FullName}' ({confidence:P0})");
                    break;
                }
            }
        }

        System.Diagnostics.Debug.WriteLine($"Found {allMatches.Count(m => m.isHome)} home and {allMatches.Count(m => !m.isHome)} away player matches");

        // Extract scores - look for standalone 0, 1, O patterns
        var scoreIndicators = ExtractScoreIndicators(lines);
        System.Diagnostics.Debug.WriteLine($"Found {scoreIndicators.Count} score indicators");

        // Build frames by pairing consecutive home-away or away-home players
        var frames = new List<RecognizedFrame>();
        var sortedMatches = allMatches.OrderBy(m => m.lineIndex).ToList();
        var usedIndices = new HashSet<int>();

        for (int i = 0; i < sortedMatches.Count && frames.Count < expectedFrames; i++)
        {
            if (usedIndices.Contains(i))
                continue;

            var current = sortedMatches[i];
            
            // Look for a player from the opposite team within 3 lines
            for (int j = i + 1; j < sortedMatches.Count && j <= i + 5; j++)
            {
                if (usedIndices.Contains(j))
                    continue;

                var next = sortedMatches[j];
                
                // Must be different teams
                if (current.isHome == next.isHome)
                    continue;

                // Found a pair!
                var homeMatch = current.isHome ? current : next;
                var awayMatch = current.isHome ? next : current;

                // Determine winner from score indicators
                var winner = DetermineWinner(homeMatch.lineIndex, awayMatch.lineIndex, scoreIndicators, lines);

                var frame = new RecognizedFrame
                {
                    FrameNumber = frames.Count + 1,
                    HomePlayerName = homeMatch.player.FullName,
                    AwayPlayerName = awayMatch.player.FullName,
                    MatchedHomePlayerId = homeMatch.player.Id,
                    MatchedAwayPlayerId = awayMatch.player.Id,
                    Winner = winner,
                    Confidence = (homeMatch.confidence + awayMatch.confidence) / 2
                };

                frames.Add(frame);
                usedIndices.Add(i);
                usedIndices.Add(j);

                System.Diagnostics.Debug.WriteLine($"  Frame {frame.FrameNumber}: {frame.HomePlayerName} vs {frame.AwayPlayerName} [{winner}]");
                break;
            }
        }

        // Fill empty frames to reach expected count
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

        System.Diagnostics.Debug.WriteLine($"=== END: {result.Frames.Count} frames, score {result.HomeScore}-{result.AwayScore} ===");
    }

    /// <summary>
    /// Extract score indicators from lines - handles standalone 0, 1, O patterns
    /// </summary>
    private List<(int lineIndex, bool isHomeWin)> ExtractScoreIndicators(List<string> lines)
    {
        var scores = new List<(int lineIndex, bool isHomeWin)>();

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            
            // Pattern: Line is just "0" or "1" or "O" (OCR often reads 0 as O)
            if (line == "0" || line == "O" || line == "o")
            {
                // 0 usually means loss - need to determine which side
                // Look at position relative to nearby player names
                scores.Add((i, false)); // Placeholder - will determine context later
                System.Diagnostics.Debug.WriteLine($"  Score [{i}]: '0' (loss indicator)");
            }
            else if (line == "1")
            {
                scores.Add((i, true)); // 1 = win
                System.Diagnostics.Debug.WriteLine($"  Score [{i}]: '1' (win indicator)");
            }
            // Pattern: "1 0" or "0 1" on same line
            else if (Regex.IsMatch(line, @"^\s*1\s+0\s*$") || Regex.IsMatch(line, @"^\s*1\s*-\s*0\s*$"))
            {
                scores.Add((i, true)); // Home wins
                System.Diagnostics.Debug.WriteLine($"  Score [{i}]: '1-0' -> Home wins");
            }
            else if (Regex.IsMatch(line, @"^\s*0\s+1\s*$") || Regex.IsMatch(line, @"^\s*0\s*-\s*1\s*$"))
            {
                scores.Add((i, false)); // Away wins
                System.Diagnostics.Debug.WriteLine($"  Score [{i}]: '0-1' -> Away wins");
            }
            // Pattern: Line starts with 0 or 1 followed by player name (e.g., "0 MARTIN ROBERTS")
            else if (Regex.IsMatch(line, @"^[01O]\s+[A-Za-z]"))
            {
                var firstChar = line[0];
                bool isWin = firstChar == '1';
                scores.Add((i, isWin));
                System.Diagnostics.Debug.WriteLine($"  Score [{i}]: '{firstChar}' prefix -> {(isWin ? "win" : "loss")}");
            }
            // Pattern: Line ends with 0 or 1 after player name
            else if (Regex.IsMatch(line, @"[A-Za-z]\s+[01O]\s*$"))
            {
                var lastChar = line.TrimEnd().Last();
                bool isWin = lastChar == '1';
                scores.Add((i, isWin));
                System.Diagnostics.Debug.WriteLine($"  Score [{i}]: '{lastChar}' suffix -> {(isWin ? "win" : "loss")}");
            }
        }

        return scores;
    }

    /// <summary>
    /// Determine winner based on nearby score indicators
    /// </summary>
    private FrameWinner DetermineWinner(int homeLineIdx, int awayLineIdx, List<(int lineIndex, bool isHomeWin)> scores, List<string> lines)
    {
        // Check the lines themselves for score patterns
        if (homeLineIdx < lines.Count)
        {
            var homeLine = lines[homeLineIdx];
            // Check if line starts or ends with 1 (win) or 0 (loss)
            if (Regex.IsMatch(homeLine, @"^1\s+") || Regex.IsMatch(homeLine, @"\s+1\s*$"))
                return FrameWinner.Home;
            if (Regex.IsMatch(homeLine, @"^0\s+") || Regex.IsMatch(homeLine, @"\s+0\s*$") ||
                Regex.IsMatch(homeLine, @"^O\s+") || Regex.IsMatch(homeLine, @"\s+O\s*$"))
                return FrameWinner.Away; // Home got 0, so away wins
        }

        if (awayLineIdx < lines.Count)
        {
            var awayLine = lines[awayLineIdx];
            if (Regex.IsMatch(awayLine, @"^1\s+") || Regex.IsMatch(awayLine, @"\s+1\s*$"))
                return FrameWinner.Away;
            if (Regex.IsMatch(awayLine, @"^0\s+") || Regex.IsMatch(awayLine, @"\s+0\s*$") ||
                Regex.IsMatch(awayLine, @"^O\s+") || Regex.IsMatch(awayLine, @"\s+O\s*$"))
                return FrameWinner.Home; // Away got 0, so home wins
        }

        // Look for score indicators between or near the player lines
        var minLine = Math.Min(homeLineIdx, awayLineIdx);
        var maxLine = Math.Max(homeLineIdx, awayLineIdx);

        foreach (var (lineIdx, isHomeWin) in scores)
        {
            // Score between the two player lines or immediately after
            if (lineIdx >= minLine - 1 && lineIdx <= maxLine + 1)
            {
                return isHomeWin ? FrameWinner.Home : FrameWinner.Away;
            }
        }

        return FrameWinner.None;
    }

    private (bool match, double confidence) MatchPlayerToLine(Player player, string line)
    {
        var normalizedLine = NormalizeName(line);
        var normalizedFull = NormalizeName(player.FullName ?? "");
        var normalizedFirst = NormalizeName(player.FirstName ?? "");
        var normalizedLast = NormalizeName(player.LastName ?? "");

        // Exact full name match
        if (normalizedLine == normalizedFull || normalizedLine.Contains(normalizedFull))
            return (true, 1.0);

        // Contains both first and last name
        if (normalizedFirst.Length >= 2 && normalizedLast.Length >= 2)
        {
            if (normalizedLine.Contains(normalizedFirst) && normalizedLine.Contains(normalizedLast))
                return (true, 0.95);
        }

        // Last name only (if distinctive enough)
        if (normalizedLast.Length >= 4 && normalizedLine.Contains(normalizedLast))
            return (true, 0.7);

        // Fuzzy match for OCR errors (e.g., LÓNIS -> LEWIS, STOVE -> STEVE)
        var similarity = CalculateNameSimilarity(normalizedLine, normalizedFull);
        if (similarity >= 0.65)
            return (true, similarity);

        // Check if line (minus score prefix/suffix) matches
        var cleanedLine = Regex.Replace(line, @"^[01O]\s+", "").Trim();
        cleanedLine = Regex.Replace(cleanedLine, @"\s+[01O]$", "").Trim();
        var normalizedCleaned = NormalizeName(cleanedLine);
        
        if (normalizedCleaned == normalizedFull || normalizedCleaned.Contains(normalizedFull))
            return (true, 0.9);

        if (normalizedFirst.Length >= 2 && normalizedLast.Length >= 2)
        {
            if (normalizedCleaned.Contains(normalizedFirst) && normalizedCleaned.Contains(normalizedLast))
                return (true, 0.85);
        }

        return (false, 0);
    }

    private string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        name = Regex.Replace(name, @"[^\w\s]", "");
        name = Regex.Replace(name, @"\s+", " ");
        return name.Trim().ToLower();
    }

    private double CalculateNameSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;

        var wordsA = a.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var wordsB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        
        var commonWords = wordsA.Intersect(wordsB).Count();
        var totalWords = Math.Max(wordsA.Count, wordsB.Count);
        var wordSimilarity = totalWords > 0 ? (double)commonWords / totalWords : 0;

        if (a.Contains(b) || b.Contains(a))
            return Math.Max(wordSimilarity, 0.8);

        return wordSimilarity;
    }

    private bool IsHeaderLine(string lowerLine)
    {
        var skipPatterns = new[] { "home", "away", "player", "team", "score", "frame", "date", "division",
            "wellington", "district", "pool", "league", "match", "result", "full name", "captain",
            "before start", "games 11", "completed", "total", "signature", "referee", "venue", "div.", 
            "ensure", "repeat", "pairing" };
        return skipPatterns.Any(p => lowerLine.Contains(p)) || lowerLine.Length < 3;
    }
}
