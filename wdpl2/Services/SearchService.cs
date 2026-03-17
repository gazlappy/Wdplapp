using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Provides cross-entity search across all league data.
/// </summary>
public static class SearchService
{
    public sealed class SearchResult
    {
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public Guid Id { get; set; }
        public Guid? SeasonId { get; set; }
    }

    /// <summary>
    /// Search across all entity types for the given query string.
    /// </summary>
    public static List<SearchResult> Search(LeagueData data, string query, Guid? seasonFilter = null)
    {
        if (string.IsNullOrWhiteSpace(query) || data == null)
            return new List<SearchResult>();

        var results = new List<SearchResult>();
        var q = query.Trim().ToLowerInvariant();

        // Search players
        foreach (var p in data.Players)
        {
            if (seasonFilter.HasValue && p.SeasonId != seasonFilter) continue;

            var fullName = $"{p.FirstName} {p.LastName}".ToLowerInvariant();
            if (fullName.Contains(q) || (p.Notes ?? "").ToLowerInvariant().Contains(q))
            {
                var team = data.Teams.FirstOrDefault(t => t.Id == p.TeamId);
                results.Add(new SearchResult
                {
                    Type = "Player",
                    Title = $"{p.FirstName} {p.LastName}",
                    Subtitle = team?.Name ?? "Unassigned",
                    Id = p.Id,
                    SeasonId = p.SeasonId
                });
            }
        }

        // Search teams
        foreach (var t in data.Teams)
        {
            if (seasonFilter.HasValue && t.SeasonId != seasonFilter) continue;

            if ((t.Name ?? "").ToLowerInvariant().Contains(q) ||
                (t.Captain ?? "").ToLowerInvariant().Contains(q) ||
                (t.Notes ?? "").ToLowerInvariant().Contains(q))
            {
                var div = data.Divisions.FirstOrDefault(d => d.Id == t.DivisionId);
                results.Add(new SearchResult
                {
                    Type = "Team",
                    Title = t.Name ?? "",
                    Subtitle = div?.Name ?? "No Division",
                    Id = t.Id,
                    SeasonId = t.SeasonId
                });
            }
        }

        // Search venues
        foreach (var v in data.Venues)
        {
            if (seasonFilter.HasValue && v.SeasonId != seasonFilter) continue;

            if ((v.Name ?? "").ToLowerInvariant().Contains(q) ||
                (v.Address ?? "").ToLowerInvariant().Contains(q) ||
                (v.Notes ?? "").ToLowerInvariant().Contains(q))
            {
                results.Add(new SearchResult
                {
                    Type = "Venue",
                    Title = v.Name ?? "",
                    Subtitle = v.Address ?? "",
                    Id = v.Id,
                    SeasonId = v.SeasonId
                });
            }
        }

        // Search divisions
        foreach (var d in data.Divisions)
        {
            if (seasonFilter.HasValue && d.SeasonId != seasonFilter) continue;

            if ((d.Name ?? "").ToLowerInvariant().Contains(q) ||
                (d.Notes ?? "").ToLowerInvariant().Contains(q))
            {
                results.Add(new SearchResult
                {
                    Type = "Division",
                    Title = d.Name ?? "",
                    Subtitle = "",
                    Id = d.Id,
                    SeasonId = d.SeasonId
                });
            }
        }

        // Search seasons
        foreach (var s in data.Seasons)
        {
            if ((s.Name ?? "").ToLowerInvariant().Contains(q))
            {
                results.Add(new SearchResult
                {
                    Type = "Season",
                    Title = s.Name ?? "",
                    Subtitle = $"{s.StartDate:dd MMM yyyy} - {s.EndDate:dd MMM yyyy}",
                    Id = s.Id,
                    SeasonId = s.Id
                });
            }
        }

        return results;
    }
}
