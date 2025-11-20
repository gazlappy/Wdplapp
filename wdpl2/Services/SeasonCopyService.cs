using System;
using System.Collections.Generic;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Service for copying entities from previous seasons to a new season.
/// Handles smart duplicate detection and relationship preservation.
/// </summary>
public static class SeasonCopyService
{
    #region Historical Data Classes

    public class HistoricalTeam
    {
        public string Name { get; set; } = "";
        public string? Captain { get; set; }
        public bool ProvidesFood { get; set; }
        public List<Guid> SourceSeasonIds { get; set; } = new();
        public string SeasonsPlayed { get; set; } = "";
        public bool IsSelected { get; set; }
    }

    public class HistoricalPlayer
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string FullName { get; set; } = "";
        public string? LastTeamName { get; set; }
        public List<Guid> SourceSeasonIds { get; set; } = new();
        public string SeasonsPlayed { get; set; } = "";
        public bool IsSelected { get; set; }
    }

    public class HistoricalVenue
    {
        public string Name { get; set; } = "";
        public string? Address { get; set; }
        public int TableCount { get; set; }
        public List<VenueTable> Tables { get; set; } = new();
        public List<Guid> SourceSeasonIds { get; set; } = new();
        public string SeasonsUsed { get; set; } = "";
        public bool IsSelected { get; set; }
    }

    public class HistoricalDivision
    {
        public string Name { get; set; } = "";
        public List<Guid> SourceSeasonIds { get; set; } = new();
        public string SeasonsUsed { get; set; } = "";
        public bool IsSelected { get; set; }
    }

    #endregion

    #region Get Historical Data

    /// <summary>
    /// Gets unique teams from all previous seasons (excluding target season)
    /// </summary>
    public static List<HistoricalTeam> GetHistoricalTeams(LeagueData data, Guid targetSeasonId)
    {
        var seasons = data.Seasons
            .Where(s => s.Id != targetSeasonId)
            .OrderByDescending(s => s.StartDate)
            .ToList();

        var teamGroups = data.Teams
            .Where(t => t.SeasonId != targetSeasonId && t.SeasonId.HasValue)
            .GroupBy(t => t.Name?.Trim() ?? "")
            .Where(g => !string.IsNullOrWhiteSpace(g.Key));

        var historicalTeams = new List<HistoricalTeam>();

        foreach (var group in teamGroups)
        {
            var teams = group.ToList();
            var sourceSeasonIds = teams.Select(t => t.SeasonId!.Value).Distinct().ToList();
            var seasonNames = sourceSeasonIds
                .Select(sid => seasons.FirstOrDefault(s => s.Id == sid)?.Name ?? "Unknown")
                .ToList();

            historicalTeams.Add(new HistoricalTeam
            {
                Name = group.Key,
                Captain = teams.FirstOrDefault()?.Captain,
                ProvidesFood = teams.Any(t => t.ProvidesFood),
                SourceSeasonIds = sourceSeasonIds,
                SeasonsPlayed = string.Join(", ", seasonNames),
                IsSelected = false
            });
        }

        return historicalTeams.OrderBy(t => t.Name).ToList();
    }

    /// <summary>
    /// Gets unique players from all previous seasons (excluding target season)
    /// </summary>
    public static List<HistoricalPlayer> GetHistoricalPlayers(LeagueData data, Guid targetSeasonId)
    {
        var seasons = data.Seasons
            .Where(s => s.Id != targetSeasonId)
            .OrderByDescending(s => s.StartDate)
            .ToList();

        var playerGroups = data.Players
            .Where(p => p.SeasonId != targetSeasonId && p.SeasonId.HasValue)
            .GroupBy(p => new { 
                FirstName = p.FirstName?.Trim() ?? "", 
                LastName = p.LastName?.Trim() ?? "" 
            })
            .Where(g => !string.IsNullOrWhiteSpace(g.Key.FirstName) || !string.IsNullOrWhiteSpace(g.Key.LastName));

        var historicalPlayers = new List<HistoricalPlayer>();

        foreach (var group in playerGroups)
        {
            var players = group.ToList();
            var sourceSeasonIds = players.Select(p => p.SeasonId!.Value).Distinct().ToList();
            var seasonNames = sourceSeasonIds
                .Select(sid => seasons.FirstOrDefault(s => s.Id == sid)?.Name ?? "Unknown")
                .ToList();

            // Get last team name
            var lastPlayer = players.OrderByDescending(p => 
            {
                var season = seasons.FirstOrDefault(s => s.Id == p.SeasonId);
                return season?.StartDate ?? DateTime.MinValue;
            }).FirstOrDefault();

            string? lastTeamName = null;
            if (lastPlayer?.TeamId.HasValue == true)
            {
                var team = data.Teams.FirstOrDefault(t => t.Id == lastPlayer.TeamId.Value);
                lastTeamName = team?.Name;
            }

            historicalPlayers.Add(new HistoricalPlayer
            {
                FirstName = group.Key.FirstName,
                LastName = group.Key.LastName,
                FullName = $"{group.Key.FirstName} {group.Key.LastName}".Trim(),
                LastTeamName = lastTeamName,
                SourceSeasonIds = sourceSeasonIds,
                SeasonsPlayed = string.Join(", ", seasonNames),
                IsSelected = false
            });
        }

        return historicalPlayers.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToList();
    }

    /// <summary>
    /// Gets unique venues from all previous seasons (excluding target season)
    /// </summary>
    public static List<HistoricalVenue> GetHistoricalVenues(LeagueData data, Guid targetSeasonId)
    {
        var seasons = data.Seasons
            .Where(s => s.Id != targetSeasonId)
            .OrderByDescending(s => s.StartDate)
            .ToList();

        var venueGroups = data.Venues
            .Where(v => v.SeasonId != targetSeasonId && v.SeasonId.HasValue)
            .GroupBy(v => v.Name?.Trim() ?? "")
            .Where(g => !string.IsNullOrWhiteSpace(g.Key));

        var historicalVenues = new List<HistoricalVenue>();

        foreach (var group in venueGroups)
        {
            var venues = group.ToList();
            var sourceSeasonIds = venues.Select(v => v.SeasonId!.Value).Distinct().ToList();
            var seasonNames = sourceSeasonIds
                .Select(sid => seasons.FirstOrDefault(s => s.Id == sid)?.Name ?? "Unknown")
                .ToList();

            // Get most recent venue with tables
            var mostRecent = venues
                .OrderByDescending(v => 
                {
                    var season = seasons.FirstOrDefault(s => s.Id == v.SeasonId);
                    return season?.StartDate ?? DateTime.MinValue;
                })
                .FirstOrDefault();

            historicalVenues.Add(new HistoricalVenue
            {
                Name = group.Key,
                Address = mostRecent?.Address,
                TableCount = mostRecent?.Tables?.Count ?? 0,
                Tables = mostRecent?.Tables?.Select(t => new VenueTable 
                { 
                    Label = t.Label, 
                    MaxTeams = t.MaxTeams 
                }).ToList() ?? new List<VenueTable>(),
                SourceSeasonIds = sourceSeasonIds,
                SeasonsUsed = string.Join(", ", seasonNames),
                IsSelected = false
            });
        }

        return historicalVenues.OrderBy(v => v.Name).ToList();
    }

    /// <summary>
    /// Gets unique divisions from all previous seasons (excluding target season)
    /// </summary>
    public static List<HistoricalDivision> GetHistoricalDivisions(LeagueData data, Guid targetSeasonId)
    {
        var seasons = data.Seasons
            .Where(s => s.Id != targetSeasonId)
            .OrderByDescending(s => s.StartDate)
            .ToList();

        var divisionGroups = data.Divisions
            .Where(d => d.SeasonId != targetSeasonId && d.SeasonId.HasValue)
            .GroupBy(d => d.Name?.Trim() ?? "")
            .Where(g => !string.IsNullOrWhiteSpace(g.Key));

        var historicalDivisions = new List<HistoricalDivision>();

        foreach (var group in divisionGroups)
        {
            var divisions = group.ToList();
            var sourceSeasonIds = divisions.Select(d => d.SeasonId!.Value).Distinct().ToList();
            var seasonNames = sourceSeasonIds
                .Select(sid => seasons.FirstOrDefault(s => s.Id == sid)?.Name ?? "Unknown")
                .ToList();

            historicalDivisions.Add(new HistoricalDivision
            {
                Name = group.Key,
                SourceSeasonIds = sourceSeasonIds,
                SeasonsUsed = string.Join(", ", seasonNames),
                IsSelected = false
            });
        }

        return historicalDivisions.OrderBy(d => d.Name).ToList();
    }

    #endregion

    #region Copy Operations

    /// <summary>
    /// Copies selected teams to target season
    /// </summary>
    public static int CopyTeamsToSeason(LeagueData data, List<HistoricalTeam> selectedTeams, Guid targetSeasonId)
    {
        int copied = 0;
        var existingTeamNames = data.Teams
            .Where(t => t.SeasonId == targetSeasonId)
            .Select(t => t.Name?.Trim()?.ToLower() ?? "")
            .ToHashSet();

        foreach (var historical in selectedTeams.Where(t => t.IsSelected))
        {
            // Skip if already exists in target season
            if (existingTeamNames.Contains(historical.Name.Trim().ToLower()))
                continue;

            var newTeam = new Team
            {
                Id = Guid.NewGuid(),
                SeasonId = targetSeasonId,
                Name = historical.Name,
                Captain = historical.Captain,
                ProvidesFood = historical.ProvidesFood
                // Note: DivisionId, VenueId, TableId, CaptainPlayerId will be null
                // User needs to reassign these after import
            };

            data.Teams.Add(newTeam);
            copied++;
        }

        return copied;
    }

    /// <summary>
    /// Copies selected players to target season
    /// </summary>
    public static int CopyPlayersToSeason(LeagueData data, List<HistoricalPlayer> selectedPlayers, Guid targetSeasonId)
    {
        int copied = 0;
        var existingPlayers = data.Players
            .Where(p => p.SeasonId == targetSeasonId)
            .Select(p => new { 
                FirstName = p.FirstName?.Trim()?.ToLower() ?? "", 
                LastName = p.LastName?.Trim()?.ToLower() ?? "" 
            })
            .ToHashSet();

        foreach (var historical in selectedPlayers.Where(p => p.IsSelected))
        {
            var key = new { 
                FirstName = historical.FirstName.Trim().ToLower(), 
                LastName = historical.LastName.Trim().ToLower() 
            };

            // Skip if already exists in target season
            if (existingPlayers.Contains(key))
                continue;

            var newPlayer = new Player
            {
                Id = Guid.NewGuid(),
                SeasonId = targetSeasonId,
                FirstName = historical.FirstName,
                LastName = historical.LastName
                // Note: TeamId will be null - user needs to assign players to teams
            };

            data.Players.Add(newPlayer);
            copied++;
        }

        return copied;
    }

    /// <summary>
    /// Copies selected venues to target season (including tables)
    /// </summary>
    public static int CopyVenuesToSeason(LeagueData data, List<HistoricalVenue> selectedVenues, Guid targetSeasonId)
    {
        int copied = 0;
        var existingVenueNames = data.Venues
            .Where(v => v.SeasonId == targetSeasonId)
            .Select(v => v.Name?.Trim()?.ToLower() ?? "")
            .ToHashSet();

        foreach (var historical in selectedVenues.Where(v => v.IsSelected))
        {
            // Skip if already exists in target season
            if (existingVenueNames.Contains(historical.Name.Trim().ToLower()))
                continue;

            var newVenue = new Venue
            {
                Id = Guid.NewGuid(),
                SeasonId = targetSeasonId,
                Name = historical.Name,
                Address = historical.Address,
                Tables = historical.Tables.Select(t => new VenueTable
                {
                    Id = Guid.NewGuid(), // New ID for the table
                    Label = t.Label,
                    MaxTeams = t.MaxTeams
                }).ToList()
            };

            data.Venues.Add(newVenue);
            copied++;
        }

        return copied;
    }

    /// <summary>
    /// Copies selected divisions to target season
    /// </summary>
    public static int CopyDivisionsToSeason(LeagueData data, List<HistoricalDivision> selectedDivisions, Guid targetSeasonId)
    {
        int copied = 0;
        var existingDivisionNames = data.Divisions
            .Where(d => d.SeasonId == targetSeasonId)
            .Select(d => d.Name?.Trim()?.ToLower() ?? "")
            .ToHashSet();

        foreach (var historical in selectedDivisions.Where(d => d.IsSelected))
        {
            // Skip if already exists in target season
            if (existingDivisionNames.Contains(historical.Name.Trim().ToLower()))
                continue;

            var newDivision = new Division
            {
                Id = Guid.NewGuid(),
                SeasonId = targetSeasonId,
                Name = historical.Name
            };

            data.Divisions.Add(newDivision);
            copied++;
        }

        return copied;
    }

    /// <summary>
    /// Copies all selected historical data to target season
    /// </summary>
    public static (int divisions, int venues, int teams, int players) CopyAllToSeason(
        LeagueData data,
        List<HistoricalDivision> divisions,
        List<HistoricalVenue> venues,
        List<HistoricalTeam> teams,
        List<HistoricalPlayer> players,
        Guid targetSeasonId)
    {
        var divCount = CopyDivisionsToSeason(data, divisions, targetSeasonId);
        var venCount = CopyVenuesToSeason(data, venues, targetSeasonId);
        var teamCount = CopyTeamsToSeason(data, teams, targetSeasonId);
        var playerCount = CopyPlayersToSeason(data, players, targetSeasonId);

        return (divCount, venCount, teamCount, playerCount);
    }

    #endregion
}
