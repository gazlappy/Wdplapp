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
/// Score card format: HOME_PLAYER | 0 | AWAY_PLAYER | ? (or vice versa)
/// The player with ? (tick/1) wins, the player with 0 loses.
/// Players can play multiple consecutive frames.
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

    private enum LineType { Header, Score, HomePlayer, AwayPlayer, Unknown }
    
    private class ClassifiedLine
    {
        public int Index;
        public string Text = "";
        public LineType Type;
        public Player? Player;
        public double Confidence;
        public int? Score; // 0 = loss, 1 = win (tick mark)
        public bool Used; // Track if this line has been consumed
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

            System.Diagnostics.Debug.WriteLine($"Home: {string.Join(", ", homePlayers.Select(p => p.FullName))}");
            System.Diagnostics.Debug.WriteLine($"Away: {string.Join(", ", awayPlayers.Select(p => p.FullName))}");

            var lines = ocrText.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            var classified = ClassifyLines(lines, homePlayers, awayPlayers);
            var frames = ExtractFrames(classified, expectedFrames);

            while (frames.Count < expectedFrames)
                frames.Add(new RecognizedFrame { FrameNumber = frames.Count + 1, Winner = FrameWinner.None, Confidence = 0 });

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

    private List<ClassifiedLine> ClassifyLines(List<string> lines, List<Player> homePlayers, List<Player> awayPlayers)
    {
        var result = new List<ClassifiedLine>();
        System.Diagnostics.Debug.WriteLine("=== CLASSIFYING LINES ===");

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var cl = new ClassifiedLine { Index = i, Text = line, Used = false };

            if (IsHeaderLine(line.ToLower())) { cl.Type = LineType.Header; result.Add(cl); continue; }

            // Check if line is ONLY a score (0, 1, tick, etc.)
            var scoreOnly = ParseScoreOnly(line);
            if (scoreOnly.HasValue)
            {
                cl.Type = LineType.Score; cl.Score = scoreOnly.Value; cl.Confidence = 1.0;
                result.Add(cl);
                System.Diagnostics.Debug.WriteLine($"  [{i}] SCORE: '{line}' = {scoreOnly}");
                continue;
            }

            // Extract embedded score from line
            var (cleanLine, embeddedScore) = ExtractEmbeddedScore(line);
            var (homeMatch, homeConf) = FindBestMatch(cleanLine, homePlayers);
            var (awayMatch, awayConf) = FindBestMatch(cleanLine, awayPlayers);

            if (homeMatch != null && homeConf >= awayConf && homeConf >= 0.5)
            {
                cl.Type = LineType.HomePlayer; cl.Player = homeMatch; cl.Confidence = homeConf; cl.Score = embeddedScore;
                System.Diagnostics.Debug.WriteLine($"  [{i}] HOME: '{line}' -> {homeMatch.FullName} ({homeConf:P0})" + (embeddedScore.HasValue ? $" score={embeddedScore}" : ""));
            }
            else if (awayMatch != null && awayConf >= 0.5)
            {
                cl.Type = LineType.AwayPlayer; cl.Player = awayMatch; cl.Confidence = awayConf; cl.Score = embeddedScore;
                System.Diagnostics.Debug.WriteLine($"  [{i}] AWAY: '{line}' -> {awayMatch.FullName} ({awayConf:P0})" + (embeddedScore.HasValue ? $" score={embeddedScore}" : ""));
            }
            else
            {
                cl.Type = LineType.Unknown; cl.Score = embeddedScore;
                System.Diagnostics.Debug.WriteLine($"  [{i}] ???: '{line}'" + (embeddedScore.HasValue ? $" score={embeddedScore}" : ""));
            }
            result.Add(cl);
        }
        return result;
    }

    /// <summary>
    /// Extract frames sequentially. Each frame consists of:
    /// HOME_PLAYER [HOME_SCORE] AWAY_PLAYER [AWAY_SCORE]
    /// 
    /// Important: Same player can appear in consecutive frames!
    /// We process line by line and pair HOME with the NEXT AWAY (or vice versa).
    /// </summary>
    private List<RecognizedFrame> ExtractFrames(List<ClassifiedLine> allLines, int expectedFrames)
    {
        var frames = new List<RecognizedFrame>();

        System.Diagnostics.Debug.WriteLine("=== EXTRACTING FRAMES ===");

        // Get relevant lines (players and scores) in order
        var relevant = allLines
            .Where(l => l.Type == LineType.HomePlayer || l.Type == LineType.AwayPlayer || l.Type == LineType.Score)
            .OrderBy(l => l.Index)
            .ToList();

        foreach (var l in relevant)
            System.Diagnostics.Debug.WriteLine($"  [{l.Index}] {l.Type}: '{l.Text}'" + (l.Score.HasValue ? $" score={l.Score}" : ""));

        int i = 0;
        while (i < relevant.Count && frames.Count < expectedFrames)
        {
            var cur = relevant[i];
            if (cur.Used) { i++; continue; }

            // Try to build a frame starting from current position
            var frame = TryBuildFrame(relevant, i, frames.Count + 1);
            if (frame != null)
            {
                frames.Add(frame);
                System.Diagnostics.Debug.WriteLine($"  Frame {frames.Count}: {frame.HomePlayerName}({GetScoreForLog(relevant, i, true)}) vs {frame.AwayPlayerName}({GetScoreForLog(relevant, i, false)}) [{frame.Winner}]");
            }
            
            i++;
        }

        System.Diagnostics.Debug.WriteLine($"Extracted {frames.Count} frames");
        return frames;
    }

    private string GetScoreForLog(List<ClassifiedLine> lines, int startIdx, bool home)
    {
        // Helper for logging - not critical
        return "?";
    }

    /// <summary>
    /// Try to build a frame starting from position i.
    /// Returns null if can't build a valid frame.
    /// Marks used lines.
    /// </summary>
    private RecognizedFrame? TryBuildFrame(List<ClassifiedLine> relevant, int startIdx, int frameNumber)
    {
        var cur = relevant[startIdx];
        
        if (cur.Type == LineType.HomePlayer)
        {
            return BuildFrameFromHome(relevant, startIdx, frameNumber);
        }
        else if (cur.Type == LineType.AwayPlayer)
        {
            return BuildFrameFromAway(relevant, startIdx, frameNumber);
        }
        else if (cur.Type == LineType.Score)
        {
            // Orphan score - try to find context
            // Look ahead for a player
            for (int j = startIdx + 1; j < relevant.Count && j <= startIdx + 2; j++)
            {
                if (!relevant[j].Used && (relevant[j].Type == LineType.HomePlayer || relevant[j].Type == LineType.AwayPlayer))
                {
                    // This score belongs to the upcoming player
                    return null; // Let the next iteration handle it
                }
            }
            cur.Used = true; // Orphan score, skip it
        }
        
        return null;
    }

    private RecognizedFrame? BuildFrameFromHome(List<ClassifiedLine> relevant, int startIdx, int frameNumber)
    {
        var homeLine = relevant[startIdx];
        var home = homeLine.Player;
        var homeConf = homeLine.Confidence;
        int? homeScore = homeLine.Score;
        
        homeLine.Used = true;

        Player? away = null;
        double awayConf = 0;
        int? awayScore = null;

        // Look ahead for: [SCORE], AWAY_PLAYER, [SCORE]
        for (int j = startIdx + 1; j < relevant.Count && j <= startIdx + 6; j++)
        {
            var next = relevant[j];
            if (next.Used) continue;

            if (next.Type == LineType.Score)
            {
                if (!homeScore.HasValue)
                {
                    // This is home's score
                    homeScore = next.Score;
                    next.Used = true;
                }
                else if (away != null && !awayScore.HasValue)
                {
                    // This is away's score (after we found away player)
                    awayScore = next.Score;
                    next.Used = true;
                }
                // Don't break - continue looking
                continue;
            }
            
            if (next.Type == LineType.AwayPlayer)
            {
                away = next.Player;
                awayConf = next.Confidence;
                awayScore = next.Score; // Might have embedded score
                next.Used = true;
                
                // Look one more line for away score if not embedded
                if (!awayScore.HasValue && j + 1 < relevant.Count)
                {
                    var afterAway = relevant[j + 1];
                    if (!afterAway.Used && afterAway.Type == LineType.Score)
                    {
                        awayScore = afterAway.Score;
                        afterAway.Used = true;
                    }
                }
                break; // Found away player, stop looking
            }
            
            if (next.Type == LineType.HomePlayer)
            {
                // Hit another home player without finding away
                // This is the START of the NEXT frame - don't consume it!
                break;
            }
        }

        if (home != null && away != null)
        {
            return new RecognizedFrame
            {
                FrameNumber = frameNumber,
                HomePlayerName = home.FullName,
                AwayPlayerName = away.FullName,
                MatchedHomePlayerId = home.Id,
                MatchedAwayPlayerId = away.Id,
                Winner = DetermineWinner(homeScore, awayScore),
                Confidence = (homeConf + awayConf) / 2
            };
        }

        return null;
    }

    private RecognizedFrame? BuildFrameFromAway(List<ClassifiedLine> relevant, int startIdx, int frameNumber)
    {
        var awayLine = relevant[startIdx];
        var away = awayLine.Player;
        var awayConf = awayLine.Confidence;
        int? awayScore = awayLine.Score;
        
        awayLine.Used = true;

        Player? home = null;
        double homeConf = 0;
        int? homeScore = null;

        // Look ahead for: [SCORE], HOME_PLAYER, [SCORE]
        for (int j = startIdx + 1; j < relevant.Count && j <= startIdx + 6; j++)
        {
            var next = relevant[j];
            if (next.Used) continue;

            if (next.Type == LineType.Score)
            {
                if (!awayScore.HasValue)
                {
                    awayScore = next.Score;
                    next.Used = true;
                }
                else if (home != null && !homeScore.HasValue)
                {
                    homeScore = next.Score;
                    next.Used = true;
                }
                continue;
            }
            
            if (next.Type == LineType.HomePlayer)
            {
                home = next.Player;
                homeConf = next.Confidence;
                homeScore = next.Score;
                next.Used = true;
                break;
            }
            
            if (next.Type == LineType.AwayPlayer)
            {
                // Hit another away player - start of next frame
                break;
            }
        }

        if (home != null && away != null)
        {
            return new RecognizedFrame
            {
                FrameNumber = frameNumber,
                HomePlayerName = home.FullName,
                AwayPlayerName = away.FullName,
                MatchedHomePlayerId = home.Id,
                MatchedAwayPlayerId = away.Id,
                Winner = DetermineWinner(homeScore, awayScore),
                Confidence = (homeConf + awayConf) / 2
            };
        }

        return null;
    }

    private FrameWinner DetermineWinner(int? homeScore, int? awayScore)
    {
        // 1 (or tick) = win, 0 = loss
        if (homeScore == 1 && awayScore == 0) return FrameWinner.Home;
        if (homeScore == 0 && awayScore == 1) return FrameWinner.Away;
        if (homeScore == 1) return FrameWinner.Home;
        if (awayScore == 1) return FrameWinner.Away;
        if (homeScore == 0 && !awayScore.HasValue) return FrameWinner.Away;
        if (awayScore == 0 && !homeScore.HasValue) return FrameWinner.Home;
        return FrameWinner.None;
    }

    private int? ParseScoreOnly(string line)
    {
        var t = line.Trim();
        if (t == "0" || t == "O" || t == "o" || t == "()") return 0;
        if (t == "1" || t == "l" || t == "I" || t == "?" || t == "?" || 
            t == "/" || t == "\\" || t == "V" || t == "v" || t == "Y" || t == "y" ||
            t == ">" || t == "J" || t == "j" || t == ")" || t == "]") return 1;
        return null;
    }

    private (string cleanedLine, int? score) ExtractEmbeddedScore(string line)
    {
        // Loss at end: "NAME 0"
        var endLossMatch = Regex.Match(line, @"^(.+?)\s+([0Oo])$");
        if (endLossMatch.Success)
            return (endLossMatch.Groups[1].Value.Trim(), 0);

        // Win at end: "NAME ?"
        var endWinMatch = Regex.Match(line, @"^(.+?)\s+([1lI??/\\VvYy>\]Jj])$");
        if (endWinMatch.Success)
            return (endWinMatch.Groups[1].Value.Trim(), 1);

        // Loss at start: "0 NAME"
        var startLossMatch = Regex.Match(line, @"^([0Oo])\s+(.+)$");
        if (startLossMatch.Success)
            return (startLossMatch.Groups[2].Value.Trim(), 0);

        // Win at start: "? NAME"
        var startWinMatch = Regex.Match(line, @"^([1lI??/\\VvYy>\]Jj])\s+(.+)$");
        if (startWinMatch.Success)
            return (startWinMatch.Groups[2].Value.Trim(), 1);

        return (line, null);
    }

    private (Player? player, double confidence) FindBestMatch(string text, List<Player> players)
    {
        Player? best = null;
        double bestConf = 0;
        foreach (var p in players)
        {
            var (match, conf) = MatchPlayer(p, text);
            if (match && conf > bestConf) { best = p; bestConf = conf; }
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
        if (line == full) return (true, 1.0);
        if (line.Contains(full)) return (true, 0.95);
        if (full.Contains(line) && line.Length >= 5) return (true, 0.85);
        if (first.Length >= 2 && last.Length >= 2 && line.Contains(first) && line.Contains(last)) return (true, 0.9);
        if (last.Length >= 4 && line.Contains(last)) return (true, 0.75);

        // First name + partial last name (handles OCR errors like "LENINDONO" vs "LEN,NOON")
        if (first.Length >= 3 && line.Contains(first) && last.Length >= 3)
        {
            for (int len = last.Length; len >= 3; len--)
                for (int start = 0; start <= last.Length - len; start++)
                    if (line.Contains(last.Substring(start, len)))
                        return (true, 0.6 + 0.15 * len / last.Length);
        }

        // Levenshtein for OCR errors
        var dist = Levenshtein(line, full);
        var maxLen = Math.Max(line.Length, full.Length);
        var sim = 1.0 - (double)dist / maxLen;
        if (sim >= 0.65) return (true, sim * 0.75); // Lowered threshold for OCR variants

        return (false, 0);
    }

    private string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = Regex.Replace(s, @"[^\w\s]", ""); // Remove punctuation (handles "LEN,NOON" -> "LENNOON")
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
            "referee", "venue", "div.", "ensure", "repeat", "pairing", "only", "town", "minion",
            "new player", "8 ball", "capt" };
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
