using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Calculates player/team statistics across all competitions in a season.
/// </summary>
public static class CompetitionStatsService
{
    /// <summary>
    /// Per-player competition record.
    /// </summary>
    public sealed class PlayerCompRecord
    {
        public Guid PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public int MatchesPlayed { get; set; }
        public int MatchesWon { get; set; }
        public int MatchesLost { get; set; }
        public int FramesFor { get; set; }
        public int FramesAgainst { get; set; }
        public int FrameDiff => FramesFor - FramesAgainst;
        public double WinPercentage => MatchesPlayed == 0 ? 0 : (double)MatchesWon / MatchesPlayed * 100;
        public int CompetitionsEntered { get; set; }
        public int CompetitionsWon { get; set; }
        public string FormString { get; set; } = "";
    }

    /// <summary>
    /// Build competition records for all players in the given season.
    /// Covers knockout rounds and group stage matches.
    /// </summary>
    public static List<PlayerCompRecord> GetPlayerRecords(
        List<Competition> competitions, List<Player> players)
    {
        var lookup = players.ToDictionary(p => p.Id);
        var records = new Dictionary<Guid, PlayerCompRecord>();

        PlayerCompRecord GetOrCreate(Guid id)
        {
            if (!records.TryGetValue(id, out var rec))
            {
                rec = new PlayerCompRecord
                {
                    PlayerId = id,
                    PlayerName = lookup.TryGetValue(id, out var p) ? p.FullName : "?"
                };
                records[id] = rec;
            }
            return rec;
        }

        foreach (var comp in competitions)
        {
            // Track which players entered this competition
            var entrants = new HashSet<Guid>(comp.ParticipantIds);

            // Determine the overall winner (last round, last match winner)
            Guid? compWinnerId = null;
            if (comp.Status == CompetitionStatus.Completed && comp.Rounds.Count > 0)
            {
                var finalRound = comp.Rounds.OrderByDescending(r => r.RoundNumber).First();
                var finalMatch = finalRound.Matches.FirstOrDefault(m => m.IsComplete);
                compWinnerId = finalMatch?.WinnerId;
            }

            // Process knockout/round-robin matches
            foreach (var round in comp.Rounds)
            {
                foreach (var match in round.Matches.Where(m => m.IsComplete))
                {
                    ApplyMatch(match, GetOrCreate);
                }
            }

            // Process group stage matches
            foreach (var group in comp.Groups.Concat(comp.PreviousGroups))
            {
                foreach (var match in group.Matches.Where(m => m.IsComplete))
                {
                    ApplyMatch(match, GetOrCreate);
                }
            }

            // Count competitions entered/won
            foreach (var pid in entrants)
            {
                var rec = GetOrCreate(pid);
                rec.CompetitionsEntered++;
                if (pid == compWinnerId)
                    rec.CompetitionsWon++;
            }
        }

        return records.Values
            .OrderByDescending(r => r.MatchesWon)
            .ThenByDescending(r => r.WinPercentage)
            .ToList();
    }

    private static void ApplyMatch(
        CompetitionMatch match,
        Func<Guid, PlayerCompRecord> getOrCreate)
    {
        if (!match.Participant1Id.HasValue || !match.Participant2Id.HasValue)
            return;

        var p1 = getOrCreate(match.Participant1Id.Value);
        var p2 = getOrCreate(match.Participant2Id.Value);

        p1.MatchesPlayed++;
        p2.MatchesPlayed++;

        p1.FramesFor += match.Participant1Score;
        p1.FramesAgainst += match.Participant2Score;
        p2.FramesFor += match.Participant2Score;
        p2.FramesAgainst += match.Participant1Score;

        if (match.WinnerId == match.Participant1Id)
        {
            p1.MatchesWon++;
            p2.MatchesLost++;
        }
        else if (match.WinnerId == match.Participant2Id)
        {
            p2.MatchesWon++;
            p1.MatchesLost++;
        }
    }

    /// <summary>
    /// Build a form string (last <paramref name="count"/> results) for a single player
    /// across all completed competition matches in order.
    /// W = win, L = loss.
    /// </summary>
    public static string GetPlayerForm(
        Guid playerId, List<Competition> competitions, int count = 5)
    {
        var results = new List<char>();

        foreach (var comp in competitions)
        {
            foreach (var round in comp.Rounds)
            {
                foreach (var m in round.Matches.Where(m => m.IsComplete))
                {
                    if (m.Participant1Id == playerId)
                        results.Add(m.WinnerId == playerId ? 'W' : 'L');
                    else if (m.Participant2Id == playerId)
                        results.Add(m.WinnerId == playerId ? 'W' : 'L');
                }
            }

            foreach (var g in comp.Groups.Concat(comp.PreviousGroups))
            {
                foreach (var m in g.Matches.Where(m => m.IsComplete))
                {
                    if (m.Participant1Id == playerId)
                        results.Add(m.WinnerId == playerId ? 'W' : 'L');
                    else if (m.Participant2Id == playerId)
                        results.Add(m.WinnerId == playerId ? 'W' : 'L');
                }
            }
        }

        // Take the last N
        var recent = results.TakeLast(count);
        return string.Join("", recent);
    }
}
