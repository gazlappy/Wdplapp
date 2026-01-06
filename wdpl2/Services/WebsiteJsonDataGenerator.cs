using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Generates JSON data files for the website (players-data.json, teams-data.json)
/// This allows a single template HTML page to load data dynamically via JavaScript.
/// </summary>
public sealed class WebsiteJsonDataGenerator
{
    private readonly LeagueData _league;
    private readonly WebsiteSettings _settings;

    public WebsiteJsonDataGenerator(LeagueData league, WebsiteSettings settings)
    {
        _league = league;
        _settings = settings;
    }

    /// <summary>
    /// Generate JSON data for all players with stats and match history
    /// </summary>
    public string GeneratePlayersJson(List<Player> players, List<Team> teams, List<Fixture> fixtures)
    {
        var playerStats = CalculatePlayerStats(players, teams, fixtures);
        var statsById = playerStats.ToDictionary(s => s.PlayerId, s => s);
        var teamById = teams.ToDictionary(t => t.Id, t => t);

        var playersData = new List<object>();

        foreach (var player in players)
        {
            if (!statsById.TryGetValue(player.Id, out var stats) || stats.Played == 0)
                continue;

            var team = player.TeamId.HasValue ? teamById.GetValueOrDefault(player.TeamId.Value) : null;
            var matchHistory = GetPlayerMatchHistory(player.Id, fixtures, teams, players);

            var historyData = matchHistory
                .OrderByDescending(r => r.Date)
                .Select(r => new
                {
                    date = r.Date.ToString("yyyy-MM-dd"),
                    dateDisplay = r.Date.ToString("dd MMM yyyy"),
                    opponentId = r.OpponentId.ToString("N"),
                    opponentName = r.OpponentName,
                    opponentTeam = r.OpponentTeamName,
                    won = r.Won,
                    eightBall = r.EightBall
                })
                .ToList();

            playersData.Add(new
            {
                id = player.Id.ToString("N"),
                name = stats.PlayerName,
                team = team?.Name ?? "",
                played = stats.Played,
                won = stats.Won,
                lost = stats.Lost,
                eightBalls = stats.EightBalls,
                winPct = Math.Round(stats.WinPercentage, 1),
                rating = stats.Rating,
                history = historyData
            });
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(new { players = playersData, generated = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") }, options);
    }

    /// <summary>
    /// Generate JSON data for all teams with stats, roster, and match history
    /// </summary>
    public string GenerateTeamsJson(List<Team> teams, List<Division> divisions, List<Venue> venues, List<Player> players, List<Fixture> fixtures)
    {
        var divisionById = divisions.ToDictionary(d => d.Id, d => d);
        var venueById = venues.ToDictionary(v => v.Id, v => v);
        var settings = _league.Settings;

        var teamsData = new List<object>();

        foreach (var team in teams.OrderBy(t => t.Name))
        {
            var division = team.DivisionId.HasValue ? divisionById.GetValueOrDefault(team.DivisionId.Value) : null;
            var venue = team.VenueId.HasValue ? venueById.GetValueOrDefault(team.VenueId.Value) : null;

            // Get team roster
            var roster = players
                .Where(p => p.TeamId == team.Id)
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .Select(p => new
                {
                    id = p.Id.ToString("N"),
                    name = p.FullName ?? $"{p.FirstName} {p.LastName}".Trim()
                })
                .ToList();

            // Calculate team stats
            var teamFixtures = fixtures
                .Where(f => f.Frames.Any() && (f.HomeTeamId == team.Id || f.AwayTeamId == team.Id))
                .OrderByDescending(f => f.Date)
                .ToList();

            int played = 0, won = 0, drawn = 0, lost = 0, framesFor = 0, framesAgainst = 0, points = 0;
            var recentForm = new List<string>();
            var matchHistory = new List<object>();

            foreach (var fixture in teamFixtures)
            {
                bool isHome = fixture.HomeTeamId == team.Id;
                int teamScore = isHome ? fixture.HomeScore : fixture.AwayScore;
                int oppScore = isHome ? fixture.AwayScore : fixture.HomeScore;
                var opponentId = isHome ? fixture.AwayTeamId : fixture.HomeTeamId;
                var opponent = teams.FirstOrDefault(t => t.Id == opponentId);

                played++;
                framesFor += teamScore;
                framesAgainst += oppScore;

                string result;
                if (teamScore > oppScore)
                {
                    won++;
                    points += teamScore + settings.MatchWinBonus;
                    result = "W";
                }
                else if (teamScore < oppScore)
                {
                    lost++;
                    points += teamScore;
                    result = "L";
                }
                else
                {
                    drawn++;
                    points += teamScore + settings.MatchDrawBonus;
                    result = "D";
                }

                if (recentForm.Count < 5) recentForm.Add(result);

                matchHistory.Add(new
                {
                    date = fixture.Date.ToString("yyyy-MM-dd"),
                    dateDisplay = fixture.Date.ToString("dd MMM yyyy"),
                    opponentId = opponentId.ToString("N"),
                    opponentName = opponent?.Name ?? "Unknown",
                    isHome,
                    teamScore,
                    oppScore,
                    result
                });
            }

            teamsData.Add(new
            {
                id = team.Id.ToString("N"),
                name = team.Name ?? "Unknown",
                division = division?.Name ?? "",
                divisionId = division?.Id.ToString("N") ?? "",
                venue = venue?.Name ?? "",
                providesFood = team.ProvidesFood,
                played,
                won,
                drawn,
                lost,
                framesFor,
                framesAgainst,
                framesDiff = framesFor - framesAgainst,
                points,
                winPct = played > 0 ? Math.Round((double)won / played * 100, 1) : 0,
                form = recentForm,
                roster,
                history = matchHistory.Take(20).ToList() // Last 20 matches
            });
        }

        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(new { teams = teamsData, generated = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") }, options);
    }

    #region Private Helpers

    private List<PlayerStat> CalculatePlayerStats(List<Player> players, List<Team> teams, List<Fixture> fixtures)
    {
        var stats = new List<PlayerStat>();
        var settings = _league.Settings;

        // Get season start date for rating calculation
        var seasonId = _settings.SelectedSeasonId;
        var season = seasonId.HasValue
            ? _league.Seasons.FirstOrDefault(s => s.Id == seasonId.Value)
            : _league.Seasons.FirstOrDefault(s => s.IsActive);
        var seasonStartDate = season?.StartDate ?? DateTime.Now.AddMonths(-6);

        // Use the shared RatingCalculator to get all player ratings
        var allRatings = RatingCalculator.CalculateAllRatings(
            fixtures,
            players,
            teams,
            settings,
            seasonStartDate);

        // Convert to PlayerStat format
        foreach (var kvp in allRatings)
        {
            var ratingStats = kvp.Value;
            stats.Add(new PlayerStat
            {
                PlayerId = ratingStats.PlayerId,
                PlayerName = ratingStats.PlayerName,
                TeamName = ratingStats.TeamName,
                Played = ratingStats.Played,
                Won = ratingStats.Wins,
                Lost = ratingStats.Losses,
                EightBalls = ratingStats.EightBalls,
                Rating = ratingStats.Rating
            });
        }

        return stats;
    }

    private List<PlayerMatchRecord> GetPlayerMatchHistory(Guid playerId, List<Fixture> fixtures, List<Team> teams, List<Player> allPlayers)
    {
        var records = new List<PlayerMatchRecord>();
        var teamById = teams.ToDictionary(t => t.Id, t => t);
        var playerById = allPlayers.ToDictionary(p => p.Id, p => p);

        foreach (var fixture in fixtures.Where(f => f.Frames.Any() && f.Frames.Any(fr => fr.Winner != FrameWinner.None)))
        {
            foreach (var frame in fixture.Frames.Where(f => f.Winner != FrameWinner.None))
            {
                Guid? opponentId = null;
                bool isPlayer = false;
                bool won = false;
                bool eightBall = false;

                if (frame.HomePlayerId == playerId)
                {
                    isPlayer = true;
                    opponentId = frame.AwayPlayerId;
                    won = frame.Winner == FrameWinner.Home;
                    eightBall = frame.EightBall && won;
                }
                else if (frame.AwayPlayerId == playerId)
                {
                    isPlayer = true;
                    opponentId = frame.HomePlayerId;
                    won = frame.Winner == FrameWinner.Away;
                    eightBall = frame.EightBall && won;
                }

                if (isPlayer && opponentId.HasValue)
                {
                    var opponent = playerById.GetValueOrDefault(opponentId.Value);
                    var opponentTeam = opponent?.TeamId.HasValue == true
                        ? teamById.GetValueOrDefault(opponent.TeamId.Value)
                        : null;

                    records.Add(new PlayerMatchRecord
                    {
                        Date = fixture.Date,
                        OpponentId = opponentId.Value,
                        OpponentName = opponent != null
                            ? (opponent.FullName ?? $"{opponent.FirstName} {opponent.LastName}".Trim())
                            : "Unknown",
                        OpponentTeamName = opponentTeam?.Name ?? "",
                        Won = won,
                        EightBall = eightBall
                    });
                }
            }
        }

        return records;
    }

    private sealed class PlayerStat
    {
        public Guid PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public string TeamName { get; set; } = "";
        public int Played { get; set; }
        public int Won { get; set; }
        public int Lost { get; set; }
        public int EightBalls { get; set; }
        public int Rating { get; set; } = 1000;
        public double WinPercentage => Played > 0 ? (Won * 100.0 / Played) : 0;
    }

    private sealed class PlayerMatchRecord
    {
        public DateTime Date { get; set; }
        public Guid OpponentId { get; set; }
        public string OpponentName { get; set; } = "";
        public string OpponentTeamName { get; set; } = "";
        public bool Won { get; set; }
        public bool EightBall { get; set; }
    }

    #endregion
}
