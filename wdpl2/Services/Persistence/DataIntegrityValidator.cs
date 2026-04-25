using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Scans league data for referential integrity issues, orphans, and inconsistencies.
/// </summary>
public static class DataIntegrityValidator
{
    public sealed class IntegrityReport
    {
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public int TotalIssues => Errors.Count + Warnings.Count;
        public bool IsClean => TotalIssues == 0;
    }

    /// <summary>
    /// Run a full integrity check on the league data.
    /// </summary>
    public static IntegrityReport Validate(LeagueData data)
    {
        var report = new IntegrityReport();

        var seasonIds = new HashSet<Guid>(data.Seasons.Select(s => s.Id));
        var teamIds = new HashSet<Guid>(data.Teams.Select(t => t.Id));
        var playerIds = new HashSet<Guid>(data.Players.Select(p => p.Id));
        var venueIds = new HashSet<Guid>(data.Venues.Select(v => v.Id));
        var divisionIds = new HashSet<Guid>(data.Divisions.Select(d => d.Id));

        // Fixtures referencing missing teams
        foreach (var f in data.Fixtures)
        {
            if (!teamIds.Contains(f.HomeTeamId))
                report.Errors.Add($"Fixture {f.Date:dd MMM} references missing home team {f.HomeTeamId}");
            if (!teamIds.Contains(f.AwayTeamId))
                report.Errors.Add($"Fixture {f.Date:dd MMM} references missing away team {f.AwayTeamId}");
            if (f.SeasonId.HasValue && !seasonIds.Contains(f.SeasonId.Value))
                report.Warnings.Add($"Fixture {f.Date:dd MMM} references missing season");
            if (f.VenueId.HasValue && !venueIds.Contains(f.VenueId.Value))
                report.Warnings.Add($"Fixture {f.Date:dd MMM} references missing venue");
        }

        // Players referencing missing teams
        foreach (var p in data.Players)
        {
            if (p.TeamId.HasValue && !teamIds.Contains(p.TeamId.Value))
                report.Errors.Add($"Player '{p.FullName}' assigned to missing team {p.TeamId}");
            if (p.SeasonId.HasValue && !seasonIds.Contains(p.SeasonId.Value))
                report.Warnings.Add($"Player '{p.FullName}' references missing season");
        }

        // Teams referencing missing divisions/venues
        foreach (var t in data.Teams)
        {
            if (t.DivisionId.HasValue && !divisionIds.Contains(t.DivisionId.Value))
                report.Warnings.Add($"Team '{t.Name}' assigned to missing division");
            if (t.VenueId.HasValue && !venueIds.Contains(t.VenueId.Value))
                report.Warnings.Add($"Team '{t.Name}' references missing venue");
            if (t.SeasonId.HasValue && !seasonIds.Contains(t.SeasonId.Value))
                report.Warnings.Add($"Team '{t.Name}' references missing season");
            if (t.CaptainPlayerId.HasValue && !playerIds.Contains(t.CaptainPlayerId.Value))
                report.Warnings.Add($"Team '{t.Name}' captain references missing player");
        }

        // Frame results referencing missing players
        foreach (var f in data.Fixtures)
        {
            foreach (var fr in f.Frames)
            {
                if (fr.HomePlayerId.HasValue && !FrameResult.IsVoidPlayer(fr.HomePlayerId) && !playerIds.Contains(fr.HomePlayerId.Value))
                    report.Errors.Add($"Fixture {f.Date:dd MMM} frame {fr.Number} references missing home player");
                if (fr.AwayPlayerId.HasValue && !FrameResult.IsVoidPlayer(fr.AwayPlayerId) && !playerIds.Contains(fr.AwayPlayerId.Value))
                    report.Errors.Add($"Fixture {f.Date:dd MMM} frame {fr.Number} references missing away player");
            }
        }

        // Duplicate GlobalPlayerId within same season
        var globalDupes = data.Players
            .Where(p => p.GlobalPlayerId.HasValue && p.SeasonId.HasValue)
            .GroupBy(p => (p.GlobalPlayerId!.Value, p.SeasonId!.Value))
            .Where(g => g.Count() > 1);
        foreach (var g in globalDupes)
            report.Warnings.Add($"Duplicate GlobalPlayerId in season: {string.Join(", ", g.Select(p => p.FullName))}");

        // ActiveSeasonId pointing to non-existent season
        if (data.ActiveSeasonId.HasValue && !seasonIds.Contains(data.ActiveSeasonId.Value))
            report.Errors.Add("ActiveSeasonId references a non-existent season");

        // Empty team names
        foreach (var t in data.Teams.Where(t => string.IsNullOrWhiteSpace(t.Name)))
            report.Warnings.Add($"Team with ID {t.Id} has no name");

        // Players with no name
        foreach (var p in data.Players.Where(p => string.IsNullOrWhiteSpace(p.FirstName) && string.IsNullOrWhiteSpace(p.LastName)))
            report.Warnings.Add($"Player with ID {p.Id} has no name");

        return report;
    }
}
