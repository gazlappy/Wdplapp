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
/// OCR Format (lines split by Azure Vision):
///   HOME_PLAYER
///   SCORE (0 or 1 - home player's score)
///   AWAY_PLAYER
/// Repeats for each frame.
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
    /// Main entry point for Azure Vision OCR results
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

            System.Diagnostics.Debug.WriteLine("=== SCORE CARD RECOGNITION ===");

            var homePlayers = homeTeamId.HasValue 
                ? _availablePlayers.Where(p => p.TeamId == homeTeamId.Value).ToList()
                : new List<Player>();
            var awayPlayers = awayTeamId.HasValue 
                ? _availablePlayers.Where(p => p.TeamId == awayTeamId.Value).ToList()
                : new List<Player>();

            System.Diagnostics.Debug.WriteLine($"Home team: {homePlayers.Count} players: {string.Join(", ", homePlayers.Select(p => p.FullName))}");
            System.Diagnostics.Debug.WriteLine($"Away team: {awayPlayers.Count} players: {string.Join(", ", awayPlayers.Select(p => p.FullName))}");

            // Parse lines and classify each
            var lines = ocrText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            // Classify each line
            var classified = ClassifyLines(lines, homePlayers, awayPlayers);

            // Extract frames using the triplet pattern: HOME, SCORE, AWAY
            var frames = ExtractFramesTripletPattern(classified, expectedFrames);

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
            result.Confidence = complete > 0 ? result.Frames.Where(f => f.Confidence > 0).Average(f => f.Confidence) : 0;
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

    private enum LineType { Header, Score, HomePlayer, AwayPlayer, Unknown }
    
    private class ClassifiedLine
    {
        public int Index;
        public string Text = "";
        public LineType Type;
        public Player? Player;
        public double Confidence;
        public int? Score; // 0 or 1
    }

    /// <summary>
    /// Classify each line as Header, Score, HomePlayer, AwayPlayer, or Unknown
    /// </summary>
    private List<ClassifiedLine> ClassifyLines(List<string> lines, List<Player> homePlayers, List<Player> awayPlayers)
    {
        var result = new List<ClassifiedLine>();

        System.Diagnostics.Debug.WriteLine("=== CLASSIFYING LINES ===");

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var cl = new ClassifiedLine { Index = i, Text = line };

            // Skip headers
            if (IsHeaderLine(line.ToLower()))
            {
                cl.Type = LineType.Header;
                result.Add(cl);
                continue;
            }

            // Check if it's just a score (0, 1, O, etc.)
            var score = ParseScoreOnly(line);
            if (score.HasValue)
            {
                cl.Type = LineType.Score;
                cl.Score = score.Value;
                cl.Confidence = 1.0;
                result.Add(cl);
                System.Diagnostics.Debug.WriteLine($"  [{i}] SCORE: '{line}' = {score}");
                continue;
            }

            // Try to match as player - check if line has embedded score first
            var (cleanLine, embeddedScore) = ExtractEmbeddedScore(line);
            
            // Find best match in each team
            var (homeMatch, homeConf) = FindBestMatch(cleanLine, homePlayers);
            var (awayMatch, awayConf) = FindBestMatch(cleanLine, awayPlayers);

            if (homeMatch != null && homeConf >= awayConf && homeConf >= 0.5)
            {
                cl.Type = LineType.HomePlayer;
                cl.Player = homeMatch;
                cl.Confidence = homeConf;
                cl.Score = embeddedScore;
                result.Add(cl);
                System.Diagnostics.Debug.WriteLine($"  [{i}] HOME: '{line}' -> {homeMatch.FullName} ({homeConf:P0})" + (embeddedScore.HasValue ? $" score={embeddedScore}" : ""));
            }
            else if (awayMatch != null && awayConf >= 0.5)
            {
                cl.Type = LineType.AwayPlayer;
                cl.Player = awayMatch;
                cl.Confidence = awayConf;
                cl.Score = embeddedScore;
                result.Add(cl);
                System.Diagnostics.Debug.WriteLine($"  [{i}] AWAY: '{line}' -> {awayMatch.FullName} ({awayConf:P0})" + (embeddedScore.HasValue ? $" score={embeddedScore}" : ""));
            }
            else
            {
                cl.Type = LineType.Unknown;
                cl.Score = embeddedScore;
                result.Add(cl);
                System.Diagnostics.Debug.WriteLine($"  [{i}] ???: '{line}'" + (embeddedScore.HasValue ? $" score={embeddedScore}" : ""));
            }
        }

        return result;
    }

    /// <summary>
    /// Extract frames using triplet pattern: HOME, SCORE, AWAY
    /// The score on its own line is the HOME player's score:
    ///   - 1 = home won the frame
    ///   - 0 = home lost (away won)
    /// </summary>
    private List<RecognizedFrame> ExtractFramesTripletPattern(List<ClassifiedLine> lines, int expectedFrames)
    {
        var frames = new List<RecognizedFrame>();
        var used = new HashSet<int>();

        System.Diagnostics.Debug.WriteLine("=== EXTRACTING FRAMES (Triplet Pattern) ===");

        // Get only player and score lines (skip headers and unknowns for initial matching)
        var playerAndScoreLines = lines.Where(l => 
            l.Type == LineType.HomePlayer || 
            l.Type == LineType.AwayPlayer || 
            l.Type == LineType.Score).ToList();

        int i = 0;
        while (i < playerAndScoreLines.Count && frames.Count < expectedFrames)
        {
            var current = playerAndScoreLines[i];

            // Look for pattern: HOME, [SCORE], AWAY
            if (current.Type == LineType.HomePlayer)
            {
                var homePlayer = current.Player;
                var homeConf = current.Confidence;
                int? score = current.Score;
                Player? awayPlayer = null;
                double awayConf = 0;

                // Look ahead for score and away player
                int j = i + 1;
                while (j < playerAndScoreLines.Count && j <= i + 3)
                {
                    var next = playerAndScoreLines[j];

                    if (next.Type == LineType.Score && !score.HasValue)
                    {
                        score = next.Score;
                        j++;
                        continue;
                    }

                    if (next.Type == LineType.AwayPlayer)
                    {
                        awayPlayer = next.Player;
                        awayConf = next.Confidence;
                        if (next.Score.HasValue && !score.HasValue)
                            score = next.Score;
                        i = j; // Move past this away player
                        break;
                    }

                    if (next.Type == LineType.HomePlayer)
                    {
                        // Hit another home player without finding away - skip
                        break;
                    }

                    j++;
                }

                if (homePlayer != null && awayPlayer != null)
                {
                    // Score interpretation: the score line shows HOME player's result
                    // 1 = home won, 0 = home lost (away won)
                    var winner = FrameWinner.None;
                    if (score.HasValue)
                    {
                        winner = score.Value == 1 ? FrameWinner.Home : FrameWinner.Away;
                    }

                    frames.Add(new RecognizedFrame
                    {
                        FrameNumber = frames.Count + 1,
                        HomePlayerName = homePlayer.FullName,
                        AwayPlayerName = awayPlayer.FullName,
                        MatchedHomePlayerId = homePlayer.Id,
                        MatchedAwayPlayerId = awayPlayer.Id,
                        Winner = winner,
                        Confidence = (homeConf + awayConf) / 2
                    });

                    System.Diagnostics.Debug.WriteLine($"  Frame {frames.Count}: {homePlayer.FullName} vs {awayPlayer.FullName} [{winner}] (score={score})");
                }
            }

            i++;
        }

        System.Diagnostics.Debug.WriteLine($"Extracted {frames.Count} frames from triplet pattern");

        // If we didn't get enough, try a more relaxed approach
        if (frames.Count < expectedFrames)
        {
            System.Diagnostics.Debug.WriteLine("=== FALLBACK: Pairing remaining players ===");
            
            // Get all home and away player occurrences we haven't used yet
            var allHome = lines.Where(l => l.Type == LineType.HomePlayer).ToList();
            var allAway = lines.Where(l => l.Type == LineType.AwayPlayer).ToList();
            var allScores = lines.Where(l => l.Type == LineType.Score).ToList();

            // Track which line indices we've used
            var usedIndices = new HashSet<int>();
            foreach (var f in frames)
            {
                // Mark indices as used based on matched players
                var homeIdx = allHome.FindIndex(h => h.Player?.Id == f.MatchedHomePlayerId);
                var awayIdx = allAway.FindIndex(a => a.Player?.Id == f.MatchedAwayPlayerId);
                if (homeIdx >= 0) usedIndices.Add(allHome[homeIdx].Index);
                if (awayIdx >= 0) usedIndices.Add(allAway[awayIdx].Index);
            }

            // Pair remaining
            var unusedHome = allHome.Where(h => !usedIndices.Contains(h.Index)).OrderBy(h => h.Index).ToList();
            var unusedAway = allAway.Where(a => !usedIndices.Contains(a.Index)).OrderBy(a => a.Index).ToList();

            for (int h = 0; h < unusedHome.Count && frames.Count < expectedFrames; h++)
            {
                var home = unusedHome[h];
                
                // Find nearest away player
                var nearestAway = unusedAway
                    .Where(a => !usedIndices.Contains(a.Index))
                    .OrderBy(a => Math.Abs(a.Index - home.Index))
                    .FirstOrDefault();

                if (nearestAway != null)
                {
                    // Look for score between them
                    int? score = home.Score ?? nearestAway.Score;
                    var minIdx = Math.Min(home.Index, nearestAway.Index);
                    var maxIdx = Math.Max(home.Index, nearestAway.Index);
                    var scoreBetween = allScores.FirstOrDefault(s => s.Index > minIdx && s.Index < maxIdx);
                    if (scoreBetween != null) score = scoreBetween.Score;

                    var winner = score.HasValue ? (score.Value == 1 ? FrameWinner.Home : FrameWinner.Away) : FrameWinner.None;

                    frames.Add(new RecognizedFrame
                    {
                        FrameNumber = frames.Count + 1,
                        HomePlayerName = home.Player?.FullName,
                        AwayPlayerName = nearestAway.Player?.FullName,
                        MatchedHomePlayerId = home.Player?.Id,
                        MatchedAwayPlayerId = nearestAway.Player?.Id,
                        Winner = winner,
                        Confidence = (home.Confidence + nearestAway.Confidence) / 2 * 0.8
                    });

                    usedIndices.Add(home.Index);
                    usedIndices.Add(nearestAway.Index);

                    System.Diagnostics.Debug.WriteLine($"  Frame {frames.Count} (fallback): {home.Player?.FullName} vs {nearestAway.Player?.FullName} [{winner}]");
                }
            }
        }

        return frames;
    }

    /// <summary>
    /// Parse a line that is ONLY a score (0, 1, O, etc.)
    /// </summary>
    private int? ParseScoreOnly(string line)
    {
        var t = line.Trim();
        if (t == "0" || t == "O" || t == "o" || t == "0.") return 0;
        if (t == "1" || t == "l" || t == "I" || t == "1.") return 1;
        return null;
    }

    /// <summary>
    /// Extract embedded score from "0 NAME" or "NAME 1" patterns
    /// </summary>
    private (string cleanedLine, int? score) ExtractEmbeddedScore(string line)
    {
        // Score at start
        var startMatch = Regex.Match(line, @"^([01OoIl])\s+(.+)$");
        if (startMatch.Success)
        {
            var c = startMatch.Groups[1].Value[0];
            return (startMatch.Groups[2].Value.Trim(), (c == '1' || c == 'l' || c == 'I') ? 1 : 0);
        }

        // Score at end
        var endMatch = Regex.Match(line, @"^(.+)\s+([01OoIl])$");
        if (endMatch.Success)
        {
            var c = endMatch.Groups[2].Value[0];
            return (endMatch.Groups[1].Value.Trim(), (c == '1' || c == 'l' || c == 'I') ? 1 : 0);
        }

        return (line, null);
    }

    private (Player? player, double confidence) FindBestMatch(string text, List<Player> players)
    {
        Player? best = null;
        double bestConf = 0;

        foreach (var p in players)
        {
            var (match, conf) = MatchPlayer(p, text);
            if (match && conf > bestConf)
            {
                best = p;
                bestConf = conf;
            }
        }

        return (best, bestConf);
    }

    private (bool match, double confidence) MatchPlayer(Player player, string text)
    {
        var line = Normalize(text);
        var full = Normalize(player.FullName ?? "");
        var first = Normalize(player.FirstName ?? "");
        var last = Normalize(player.LastName ?? "");

        if (string.IsNullOrWhiteSpace(full) || line.Length < 3) return (false, 0);

        // Exact match
        if (line == full) return (true, 1.0);

        // Line contains full name
        if (line.Contains(full)) return (true, 0.95);

        // Full name contains line (truncated OCR)
        if (full.Contains(line) && line.Length >= 5) return (true, 0.85);

        // Both first and last name present
        if (first.Length >= 2 && last.Length >= 2 && line.Contains(first) && line.Contains(last))
            return (true, 0.9);

        // Last name only (4+ chars)
        if (last.Length >= 4 && line.Contains(last)) return (true, 0.75);

        // First name + partial last
        if (first.Length >= 3 && line.Contains(first) && last.Length >= 3)
        {
            for (int len = last.Length; len >= 3; len--)
            {
                for (int start = 0; start <= last.Length - len; start++)
                {
                    if (line.Contains(last.Substring(start, len)))
                        return (true, 0.6 + 0.15 * len / last.Length);
                }
            }
        }

        // Levenshtein
        var dist = Levenshtein(line, full);
        var maxLen = Math.Max(line.Length, full.Length);
        var sim = 1.0 - (double)dist / maxLen;
        if (sim >= 0.7) return (true, sim * 0.75);

        return (false, 0);
    }

    private string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = Regex.Replace(s, @"[^\w\s]", "");
        s = Regex.Replace(s, @"\s+", " ");
        return s.Trim().ToLower();
    }

    private int Levenshtein(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }

        return d[a.Length, b.Length];
    }

    private bool IsHeaderLine(string lower)
    {
        var skip = new[] { "home", "away", "player", "team", "score", "frame", "date", "division",
            "wellington", "district", "pool", "league", "match", "result", "full name",
            "captain", "before start", "games 11", "completed", "total", "signature",
            "referee", "venue", "div.", "ensure", "repeat", "pairing", "only", "town", "minion" };
        return skip.Any(lower.Contains) || lower.Length < 3;
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
