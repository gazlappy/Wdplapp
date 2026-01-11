using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Wdpl2.Services;

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

        /// <summary>Website settings for HTML generation and FTP upload.</summary>
        public WebsiteSettings WebsiteSettings { get; set; } = new();
        
        /// <summary>Fixtures sheet settings for printable fixture sheet generation.</summary>
        public FixturesSheetSettings FixturesSheetSettings { get; set; } = new();

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
        
        /// <summary>Global team identity - links same team across multiple seasons for career tracking.</summary>
        public Guid? GlobalTeamId { get; set; }
        
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

        /// <summary>Global player identity - links same person across multiple seasons for career tracking.</summary>
        public Guid? GlobalPlayerId { get; set; }

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

        /// <summary>
        /// Whether the player is active and can be selected for matches.
        /// Inactive players keep their historical results but cannot play new frames.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Date the player was deactivated (if applicable).
        /// </summary>
        public DateTime? DeactivatedDate { get; set; }

        /// <summary>
        /// Reason for deactivation (optional).
        /// </summary>
        public string? DeactivationReason { get; set; }

        /// <summary>
        /// Transfer history for this player within the season.
        /// </summary>
        public List<PlayerTransfer> TransferHistory { get; set; } = new();

        public string? Notes { get; set; }

        public override string ToString() => FullName;
    }

    // ---------- PlayerTransfer ----------
    /// <summary>
    /// Records a player transfer from one team to another within a season.
    /// This preserves the player's historical performance at each team.
    /// </summary>
    public sealed class PlayerTransfer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>The team the player transferred FROM.</summary>
        public Guid FromTeamId { get; set; }
        
        /// <summary>Name of the team at time of transfer (for historical reference).</summary>
        public string FromTeamName { get; set; } = "";
        
        /// <summary>The team the player transferred TO.</summary>
        public Guid ToTeamId { get; set; }
        
        /// <summary>Name of the team at time of transfer (for historical reference).</summary>
        public string ToTeamName { get; set; } = "";
        
        /// <summary>Date of the transfer.</summary>
        public DateTime TransferDate { get; set; } = DateTime.Now;
        
        /// <summary>
        /// Player's rating at the time of transfer.
        /// This allows carrying over their rating to the new team.
        /// </summary>
        public int RatingAtTransfer { get; set; }
        
        /// <summary>
        /// Frames played at the time of transfer.
        /// </summary>
        public int FramesPlayedAtTransfer { get; set; }
        
        /// <summary>
        /// Wins at the time of transfer.
        /// </summary>
        public int WinsAtTransfer { get; set; }
        
        /// <summary>
        /// Losses at the time of transfer.
        /// </summary>
        public int LossesAtTransfer { get; set; }
        
        /// <summary>Optional notes about the transfer.</summary>
        public string? Notes { get; set; }
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
