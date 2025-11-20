using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Wdpl2.Models
{
    // ---------- Root container ----------
    public sealed class LeagueData
    {
        public List<Division> Divisions { get; set; } = new();
        public List<Team> Teams { get; set; } = new();
        public List<Player> Players { get; set; } = new();
        public List<Venue> Venues { get; set; } = new();

        // Fixtures + Seasons are in their own files (Models/Fixture.cs, Models/Season.cs)
        public List<Fixture> Fixtures { get; set; } = new();
        public List<Season> Seasons { get; set; } = new();
        public Guid? ActiveSeasonId { get; set; }

        /// <summary>Competitions/tournaments.</summary>
        public List<Competition> Competitions { get; set; } = new();

        /// <summary>Application settings for league behavior.</summary>
        public AppSettings Settings { get; set; } = new();

        /// <summary>
        /// Get all entities for a specific season (divisions, venues, teams, players, fixtures).
        /// </summary>
        public (List<Division> divisions, List<Venue> venues, List<Team> teams, List<Player> players, List<Fixture> fixtures) 
            GetSeasonData(Guid seasonId)
        {
            return (
                Divisions.Where(d => d.SeasonId == seasonId).ToList(),
                Venues.Where(v => v.SeasonId == seasonId).ToList(),
                Teams.Where(t => t.SeasonId == seasonId).ToList(),
                Players.Where(p => p.SeasonId == seasonId).ToList(),
                Fixtures.Where(f => f.SeasonId == seasonId).ToList()
            );
        }

        /// <summary>
        /// Delete a season and ALL associated data (cascading delete).
        /// </summary>
        public void DeleteSeasonCascade(Guid seasonId)
        {
            // Remove all fixtures for this season
            Fixtures.RemoveAll(f => f.SeasonId == seasonId);
            
            // Remove all players for this season
            Players.RemoveAll(p => p.SeasonId == seasonId);
            
            // Remove all teams for this season
            Teams.RemoveAll(t => t.SeasonId == seasonId);
            
            // Remove all venues for this season
            Venues.RemoveAll(v => v.SeasonId == seasonId);
            
            // Remove all divisions for this season
            Divisions.RemoveAll(d => d.SeasonId == seasonId);
            
            // Finally remove the season itself
            Seasons.RemoveAll(s => s.Id == seasonId);
            
            // If this was the active season, clear it
            if (ActiveSeasonId == seasonId)
                ActiveSeasonId = null;
        }
    }

    // ---------- Division ----------
    public sealed class Division
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>Link to the season this division belongs to.</summary>
        public Guid? SeasonId { get; set; }
        
        public string Name { get; set; } = string.Empty;

        /// <summary>Free-text notes used by DivisionsPage.</summary>
        public string? Notes { get; set; }

        public override string ToString() => Name;
    }

    // ---------- Team ----------
    public sealed class Team
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>Link to the season this team belongs to.</summary>
        public Guid? SeasonId { get; set; }
        
        public string? Name { get; set; }

        /// <summary>Division this team plays in.</summary>
        public Guid? DivisionId { get; set; }

        /// <summary>Home venue + (optional) preferred table.</summary>
        public Guid? VenueId { get; set; }
        public Guid? TableId { get; set; }

        /// <summary>TeamsPage uses this toggle.</summary>
        public bool ProvidesFood { get; set; }

        /// <summary>TeamsPage uses this to pick the captain (player).</summary>
        public Guid? CaptainPlayerId { get; set; }

        /// <summary>Legacy/simple captain name (keep for compatibility if any old UI binds to it).</summary>
        public string? Captain { get; set; }

        /// <summary>Some UIs track if the captain played.</summary>
        public bool CaptainPlayed { get; set; }

        public string? Notes { get; set; }

        public override string ToString() => Name ?? "";
    }

    // ---------- Player ----------
    public sealed class Player
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>Link to the season this player belongs to.</summary>
        public Guid? SeasonId { get; set; }

        // Maintain both single Name and split First/Last for compatibility
        private string _name = string.Empty;
        public string Name
        {
            get => string.IsNullOrWhiteSpace(_name) ? FullName : _name;
            set => _name = value ?? "";
        }

        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";

        [JsonIgnore]
        public string FullName => string.Join(" ", new[] { FirstName, LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

        /// <summary>Team this player belongs to.</summary>
        public Guid? TeamId { get; set; }

        public string? Notes { get; set; }

        public override string ToString() => FullName;
    }

    // ---------- Venue ----------
    public sealed class Venue
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>Link to the season this venue belongs to.</summary>
        public Guid? SeasonId { get; set; }
        
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? Notes { get; set; }

        public List<VenueTable> Tables { get; set; } = new();

        public override string ToString() => Name;
    }

    // ---------- VenueTable (unchanged) ----------
    public sealed class VenueTable
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Label { get; set; } = "";
        public int MaxTeams { get; set; } = 2;
        public override string ToString() => Label;
    }
}
