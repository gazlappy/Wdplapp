using Wdpl2.Models;

namespace Wdpl2.Services.Import;

public static class ImportPlacementValidator
{
    public static IReadOnlyDictionary<string, string> Validate(LeagueData data)
    {
        var issues = new Dictionary<string, string>();
        var seasons = data.Seasons.ToDictionary(s => s.Id);
        var divisions = data.Divisions.ToDictionary(s => s.Id, s => s.SeasonId);
        var venues = data.Venues.ToDictionary(s => s.Id, s => s.SeasonId);
        var teams = data.Teams.ToDictionary(s => s.Id, s => s.SeasonId);
        var players = data.Players.ToDictionary(s => s.Id, s => s.SeasonId);

        void Season(string key, string label, Guid? id)
        {
            if (!id.HasValue || !seasons.ContainsKey(id.Value))
                issues[$"{key}:season:{id}"] = $"{label}: no valid target season.";
        }
        void Link(string key, string label, string field, Guid? id, Guid? season, Dictionary<Guid, Guid?> targets, bool required = false)
        {
            if (!id.HasValue && !required) return;
            if (!id.HasValue || !targets.TryGetValue(id.Value, out var targetSeason))
                issues[$"{key}:{field}:missing:{id}"] = $"{label}: {field} does not exist ({id}).";
            else if (targetSeason != season)
                issues[$"{key}:{field}:season:{id}:{season}:{targetSeason}"] = $"{label}: {field} belongs to another season ({id}).";
        }
        foreach (var d in data.Divisions) Season($"division:{d.Id}", $"Division '{d.Name}'", d.SeasonId);
        foreach (var v in data.Venues) Season($"venue:{v.Id}", $"Venue '{v.Name}'", v.SeasonId);
        foreach (var t in data.Teams)
        {
            var key = $"team:{t.Id}";
            var label = $"Team '{t.Name}'";
            Season(key, label, t.SeasonId);
            Link(key, label, "division", t.DivisionId, t.SeasonId, divisions);
            Link(key, label, "venue", t.VenueId, t.SeasonId, venues);
            Link(key, label, "captain", t.CaptainPlayerId, t.SeasonId, players);
        }
        foreach (var p in data.Players)
        {
            var key = $"player:{p.Id}";
            Season(key, $"Player '{p.FullName}'", p.SeasonId);
            Link(key, $"Player '{p.FullName}'", "team", p.TeamId, p.SeasonId, teams);
        }
        foreach (var f in data.Fixtures)
        {
            var key = $"fixture:{f.Id}";
            var label = $"Fixture {f.Date:dd MMM yyyy} ({f.Id})";
            Season(key, label, f.SeasonId);
            Link(key, label, "home team", f.HomeTeamId, f.SeasonId, teams, true);
            Link(key, label, "away team", f.AwayTeamId, f.SeasonId, teams, true);
            Link(key, label, "division", f.DivisionId, f.SeasonId, divisions);
            Link(key, label, "venue", f.VenueId, f.SeasonId, venues);
            if (f.HomeTeamId == f.AwayTeamId)
                issues[$"{key}:same-team"] = $"{label}: home and away teams are the same.";
            if (f.TableId.HasValue && !data.Venues.Any(v => v.Id == f.VenueId && v.Tables.Any(t => t.Id == f.TableId)))
                issues[$"{key}:table:{f.TableId}:{f.VenueId}"] = $"{label}: selected table does not belong to its venue.";
            foreach (var frame in f.Frames)
            {
                var frameKey = $"{key}:frame:{frame.Number}";
                var frameLabel = $"{label}, frame {frame.Number}";
                foreach (var (field, id) in new[] { ("home player", frame.HomePlayerId), ("away player", frame.AwayPlayerId),
                    ("second home player", frame.HomePlayer2Id), ("second away player", frame.AwayPlayer2Id) })
                    if (!FrameResult.IsVoidPlayer(id)) Link(frameKey, frameLabel, field, id, f.SeasonId, players);
                if (frame.Number < 1 || f.Frames.Count(x => x.Number == frame.Number) != 1)
                    issues[$"{frameKey}:number"] = $"{frameLabel}: frame numbers must be positive and unique.";
                var ids = new[] { frame.HomePlayerId, frame.HomePlayer2Id, frame.AwayPlayerId, frame.AwayPlayer2Id }
                    .Where(id => id.HasValue && !FrameResult.IsVoidPlayer(id)).ToList();
                if (ids.Distinct().Count() != ids.Count)
                    issues[$"{frameKey}:duplicate-player"] = $"{frameLabel}: the same player occupies more than one slot.";
            }
        }
        foreach (var c in data.Competitions) Season($"competition:{c.Id}", $"Competition '{c.Name}'", c.SeasonId);
        return issues;
    }

    public static void ThrowIfNewIssues(LeagueData before, LeagueData after)
    {
        var existing = Validate(before);
        var introduced = Validate(after).Where(p => !existing.ContainsKey(p.Key)).Select(p => p.Value).ToList();
        if (introduced.Count > 0)
            throw new InvalidDataException($"Import placement checks found {introduced.Count} issue(s). Nothing was saved.\n" +
                string.Join("\n", introduced.Take(20)));
    }
}
