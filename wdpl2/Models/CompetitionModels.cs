using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Wdpl2.Models
{
    /// <summary>
    /// Type of competition format
    /// </summary>
    public enum CompetitionFormat
    {
        SinglesKnockout,
        DoublesKnockout,
        TeamKnockout,
        RoundRobin,
        Swiss,
        SinglesGroupStage,
        DoublesGroupStage
    }

    /// <summary>
    /// Status of a competition
    /// </summary>
    public enum CompetitionStatus
    {
        Draft,
        InProgress,
        Completed
    }

    /// <summary>
    /// Represents a competition/tournament with INotifyPropertyChanged for UI updates
    /// </summary>
    public sealed class Competition : INotifyPropertyChanged
    {
        private string _name = "";
        private CompetitionFormat _format = CompetitionFormat.SinglesKnockout;
        private CompetitionStatus _status = CompetitionStatus.Draft;
        private DateTime? _startDate;
        private string? _notes;

        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? SeasonId { get; set; }
        
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public CompetitionFormat Format
        {
            get => _format;
            set
            {
                if (_format != value)
                {
                    _format = value;
                    OnPropertyChanged();
                }
            }
        }

        public CompetitionStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        
        public DateTime? StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate != value)
                {
                    _startDate = value;
                    OnPropertyChanged();
                }
            }
        }

        public string? Notes
        {
            get => _notes;
            set
            {
                if (_notes != value)
                {
                    _notes = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>List of participant IDs (Players for Singles, Teams for Team KO)</summary>
        public List<Guid> ParticipantIds { get; set; } = new();

        /// <summary>For doubles competitions - pairs of player IDs</summary>
        public List<DoublesTeam> DoublesTeams { get; set; } = new();

        /// <summary>Competition rounds/brackets</summary>
        public List<CompetitionRound> Rounds { get; set; } = new();

        /// <summary>Group stage configuration (for group stage formats)</summary>
        public GroupStageSettings? GroupSettings { get; set; }

        /// <summary>Groups for group stage competitions</summary>
        public List<CompetitionGroup> Groups { get; set; } = new();

        /// <summary>Linked plate competition ID (for group stage lower-ranked players)</summary>
        public Guid? PlateCompetitionId { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString() => Name ?? "Unnamed Competition";
    }

    /// <summary>
    /// Settings for group stage competitions
    /// </summary>
    public sealed class GroupStageSettings
    {
        /// <summary>Number of groups to create (e.g., 8 groups of 16 = 128 players)</summary>
        public int NumberOfGroups { get; set; } = 4;

        /// <summary>Number of top players from each group advancing to knockout</summary>
        public int TopPlayersAdvance { get; set; } = 2;

        /// <summary>Number of lower players from each group going to plate competition</summary>
        public int LowerPlayersToPlate { get; set; } = 2;

        /// <summary>Whether to create a plate competition automatically</summary>
        public bool CreatePlateCompetition { get; set; } = true;

        /// <summary>Name suffix for plate competition (e.g., "Plate")</summary>
        public string PlateNameSuffix { get; set; } = "Plate";
    }

    /// <summary>
    /// A group in a group stage competition
    /// </summary>
    public sealed class CompetitionGroup
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public int GroupNumber { get; set; }
        
        /// <summary>Participants in this group</summary>
        public List<Guid> ParticipantIds { get; set; } = new();
        
        /// <summary>Group stage matches (round robin within group)</summary>
        public List<CompetitionMatch> Matches { get; set; } = new();
        
        /// <summary>Group standings (calculated from matches)</summary>
        public List<GroupStanding> Standings { get; set; } = new();

        public override string ToString() => Name;
    }

    /// <summary>
    /// Standing of a participant within a group
    /// </summary>
    public sealed class GroupStanding
    {
        public Guid ParticipantId { get; set; }
        public int Position { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int FramesFor { get; set; }
        public int FramesAgainst { get; set; }
        public int FrameDifference => FramesFor - FramesAgainst;
        public int Points { get; set; }
    }

    /// <summary>
    /// A doubles team (pair of players)
    /// </summary>
    public sealed class DoublesTeam
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid Player1Id { get; set; }
        public Guid Player2Id { get; set; }
        public string TeamName { get; set; } = "";

        public override string ToString() => TeamName;
    }

    /// <summary>
    /// A round in a knockout competition (e.g., Quarter-Finals, Semi-Finals, Final)
    /// </summary>
    public sealed class CompetitionRound
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public int RoundNumber { get; set; }
        public List<CompetitionMatch> Matches { get; set; } = new();

        /// <summary>Whether this round is part of a group stage</summary>
        public bool IsGroupStage { get; set; }

        /// <summary>Group ID if this round belongs to a specific group</summary>
        public Guid? GroupId { get; set; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// A single match in a competition
    /// </summary>
    public sealed class CompetitionMatch
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? Participant1Id { get; set; }
        public Guid? Participant2Id { get; set; }
        public Guid? WinnerId { get; set; }
        public int Participant1Score { get; set; }
        public int Participant2Score { get; set; }
        public DateTime? ScheduledDate { get; set; }
        public bool IsComplete { get; set; }
        public string? Notes { get; set; }

        /// <summary>For losers bracket in double elimination</summary>
        public bool IsLosersBracket { get; set; }

        /// <summary>Group ID if this match belongs to a group stage</summary>
        public Guid? GroupId { get; set; }

        public override string ToString() => $"Match {Id}";
    }

    /// <summary>
    /// Helper class for competition generation
    /// </summary>
    public static class CompetitionGenerator
    {
        /// <summary>
        /// Generate a group stage competition with knockout progression
        /// </summary>
        public static (List<CompetitionGroup> groups, Competition? plateCompetition) GenerateGroupStage(
            List<Guid> participants, 
            GroupStageSettings settings,
            CompetitionFormat format,
            Guid? seasonId,
            string competitionName,
            bool randomize = true)
        {
            if (participants == null || participants.Count < settings.NumberOfGroups * 2)
                return (new List<CompetitionGroup>(), null);

            // Randomize if requested
            var orderedParticipants = randomize
                ? participants.OrderBy(_ => Random.Shared.Next()).ToList()
                : new List<Guid>(participants);

            var groups = new List<CompetitionGroup>();
            int playersPerGroup = orderedParticipants.Count / settings.NumberOfGroups;
            int remainder = orderedParticipants.Count % settings.NumberOfGroups;

            int participantIndex = 0;

            // Create groups
            for (int i = 0; i < settings.NumberOfGroups; i++)
            {
                int groupSize = playersPerGroup + (i < remainder ? 1 : 0);
                
                var group = new CompetitionGroup
                {
                    Name = $"Group {(char)('A' + i)}",
                    GroupNumber = i + 1
                };

                // Assign participants to group
                for (int j = 0; j < groupSize && participantIndex < orderedParticipants.Count; j++)
                {
                    group.ParticipantIds.Add(orderedParticipants[participantIndex++]);
                }

                // Generate round-robin matches within group
                group.Matches = GenerateGroupMatches(group.ParticipantIds, group.Id);

                groups.Add(group);
            }

            // Create plate competition if requested
            Competition? plateCompetition = null;
            if (settings.CreatePlateCompetition && settings.LowerPlayersToPlate > 0)
            {
                plateCompetition = new Competition
                {
                    Name = $"{competitionName} {settings.PlateNameSuffix}",
                    Format = format,
                    Status = CompetitionStatus.Draft,
                    SeasonId = seasonId
                };
            }

            return (groups, plateCompetition);
        }

        /// <summary>
        /// Generate round-robin matches within a group
        /// </summary>
        private static List<CompetitionMatch> GenerateGroupMatches(List<Guid> participants, Guid groupId)
        {
            var matches = new List<CompetitionMatch>();

            for (int i = 0; i < participants.Count; i++)
            {
                for (int j = i + 1; j < participants.Count; j++)
                {
                    matches.Add(new CompetitionMatch
                    {
                        Participant1Id = participants[i],
                        Participant2Id = participants[j],
                        GroupId = groupId
                    });
                }
            }

            return matches;
        }

        /// <summary>
        /// Calculate group standings from match results
        /// </summary>
        public static List<GroupStanding> CalculateGroupStandings(CompetitionGroup group)
        {
            var standings = new Dictionary<Guid, GroupStanding>();

            // Initialize standings for all participants
            foreach (var participantId in group.ParticipantIds)
            {
                standings[participantId] = new GroupStanding
                {
                    ParticipantId = participantId
                };
            }

            // Process match results
            foreach (var match in group.Matches.Where(m => m.IsComplete))
            {
                if (!match.Participant1Id.HasValue || !match.Participant2Id.HasValue)
                    continue;

                var p1Standing = standings[match.Participant1Id.Value];
                var p2Standing = standings[match.Participant2Id.Value];

                p1Standing.Played++;
                p2Standing.Played++;

                p1Standing.FramesFor += match.Participant1Score;
                p1Standing.FramesAgainst += match.Participant2Score;
                p2Standing.FramesFor += match.Participant2Score;
                p2Standing.FramesAgainst += match.Participant1Score;

                if (match.Participant1Score > match.Participant2Score)
                {
                    // Player 1 wins
                    p1Standing.Won++;
                    p1Standing.Points += match.Participant1Score + 2; // Frames + win bonus
                    p2Standing.Lost++;
                    p2Standing.Points += match.Participant2Score; // Just frames
                }
                else if (match.Participant2Score > match.Participant1Score)
                {
                    // Player 2 wins
                    p2Standing.Won++;
                    p2Standing.Points += match.Participant2Score + 2; // Frames + win bonus
                    p1Standing.Lost++;
                    p1Standing.Points += match.Participant1Score; // Just frames
                }
                else
                {
                    // Draw
                    p1Standing.Drawn++;
                    p2Standing.Drawn++;
                    p1Standing.Points += match.Participant1Score + 1; // Frames + draw bonus
                    p2Standing.Points += match.Participant2Score + 1; // Frames + draw bonus
                }
            }

            // Sort by points, then frame difference, then frames for
            var sortedStandings = standings.Values
                .OrderByDescending(s => s.Points)
                .ThenByDescending(s => s.FrameDifference)
                .ThenByDescending(s => s.FramesFor)
                .ToList();

            // Assign positions
            for (int i = 0; i < sortedStandings.Count; i++)
            {
                sortedStandings[i].Position = i + 1;
            }

            return sortedStandings;
        }

        /// <summary>
        /// Advance participants from group stage to knockout rounds
        /// </summary>
        public static (List<Guid> knockoutParticipants, List<Guid> plateParticipants) AdvanceFromGroups(
            List<CompetitionGroup> groups,
            int topPlayersAdvance,
            int lowerPlayersToPlate)
        {
            var knockoutParticipants = new List<Guid>();
            var plateParticipants = new List<Guid>();

            foreach (var group in groups)
            {
                // Calculate standings
                var standings = CalculateGroupStandings(group);
                group.Standings = standings;

                // Take top players for knockout
                for (int i = 0; i < Math.Min(topPlayersAdvance, standings.Count); i++)
                {
                    knockoutParticipants.Add(standings[i].ParticipantId);
                }

                // Take lower players for plate (if specified)
                if (lowerPlayersToPlate > 0)
                {
                    int startIndex = topPlayersAdvance;
                    int endIndex = Math.Min(startIndex + lowerPlayersToPlate, standings.Count);
                    
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        plateParticipants.Add(standings[i].ParticipantId);
                    }
                }
            }

            return (knockoutParticipants, plateParticipants);
        }

        /// <summary>
        /// Generate a single elimination knockout bracket with random seeding
        /// </summary>
        public static List<CompetitionRound> GenerateSingleKnockout(List<Guid> participants, bool randomize = true)
        {
            if (participants == null || participants.Count < 2)
                return new List<CompetitionRound>();

            var rounds = new List<CompetitionRound>();
            var participantCount = participants.Count;
            
            // Find next power of 2
            int bracketSize = 1;
            while (bracketSize < participantCount)
                bracketSize *= 2;

            // Randomize participants if requested
            var seededParticipants = randomize 
                ? new List<Guid?>(participants.OrderBy(_ => Random.Shared.Next()).Cast<Guid?>())
                : new List<Guid?>(participants.Cast<Guid?>());
            
            // Add byes if needed
            while (seededParticipants.Count < bracketSize)
                seededParticipants.Add(null);

            var currentRound = seededParticipants;
            int roundNum = 1;
            int totalRounds = (int)Math.Log2(bracketSize);

            while (currentRound.Count > 1)
            {
                var roundName = GetRoundName(totalRounds, roundNum, currentRound.Count / 2);
                var round = new CompetitionRound
                {
                    Name = roundName,
                    RoundNumber = roundNum
                };

                var nextRound = new List<Guid?>();

                for (int i = 0; i < currentRound.Count; i += 2)
                {
                    var match = new CompetitionMatch
                    {
                        Participant1Id = currentRound[i],
                        Participant2Id = currentRound[i + 1]
                    };

                    // Handle byes
                    if (match.Participant1Id == null && match.Participant2Id != null)
                    {
                        match.WinnerId = match.Participant2Id;
                        match.IsComplete = true;
                        nextRound.Add(match.Participant2Id);
                    }
                    else if (match.Participant2Id == null && match.Participant1Id != null)
                    {
                        match.WinnerId = match.Participant1Id;
                        match.IsComplete = true;
                        nextRound.Add(match.Participant1Id);
                    }
                    else
                    {
                        nextRound.Add(null); // Winner TBD
                    }

                    round.Matches.Add(match);
                }

                rounds.Add(round);
                currentRound = nextRound;
                roundNum++;
            }

            return rounds;
        }

        /// <summary>
        /// Generate a double elimination bracket (winners + losers brackets) with random seeding
        /// </summary>
        public static List<CompetitionRound> GenerateDoubleKnockout(List<Guid> participants, bool randomize = true)
        {
            if (participants == null || participants.Count < 2)
                return new List<CompetitionRound>();

            var rounds = new List<CompetitionRound>();
            
            // Generate winners bracket (same as single elimination)
            var winnersRounds = GenerateSingleKnockout(participants, randomize);
            
            // Add winners bracket rounds
            foreach (var round in winnersRounds)
            {
                round.Name = "Winners: " + round.Name;
                rounds.Add(round);
            }

            // Generate losers bracket (more complex - not fully implemented here)
            // This would require tracking losers from each winners bracket round
            // and creating a separate losers bracket structure

            // Add grand final
            var grandFinal = new CompetitionRound
            {
                Name = "Grand Final",
                RoundNumber = rounds.Count + 1
            };
            grandFinal.Matches.Add(new CompetitionMatch());
            rounds.Add(grandFinal);

            return rounds;
        }

        /// <summary>
        /// Get human-readable round name
        /// </summary>
        private static string GetRoundName(int totalRounds, int currentRound, int matchesInRound)
        {
            int roundsRemaining = totalRounds - currentRound + 1;

            return roundsRemaining switch
            {
                1 => "Final",
                2 => "Semi-Finals",
                3 => "Quarter-Finals",
                4 => "Round of 16",
                5 => "Round of 32",
                _ => $"Round {currentRound}"
            };
        }

        /// <summary>
        /// Generate round robin schedule with random ordering
        /// </summary>
        public static List<CompetitionRound> GenerateRoundRobin(List<Guid> participants, bool randomize = true)
        {
            if (participants == null || participants.Count < 2)
                return new List<CompetitionRound>();

            var rounds = new List<CompetitionRound>();
            
            // Randomize order if requested
            var orderedParticipants = randomize
                ? participants.OrderBy(_ => Random.Shared.Next()).ToList()
                : new List<Guid>(participants);
            
            var n = orderedParticipants.Count;
            
            // If odd number, add a "bye"
            if (n % 2 != 0)
            {
                orderedParticipants.Add(Guid.Empty);
                n++;
            }

            int totalRounds = n - 1;

            for (int round = 0; round < totalRounds; round++)
            {
                var competitionRound = new CompetitionRound
                {
                    Name = $"Round {round + 1}",
                    RoundNumber = round + 1
                };

                for (int i = 0; i < n / 2; i++)
                {
                    int home = i;
                    int away = n - 1 - i;

                    var p1 = orderedParticipants[home];
                    var p2 = orderedParticipants[away];

                    // Skip if bye
                    if (p1 == Guid.Empty || p2 == Guid.Empty)
                        continue;

                    var match = new CompetitionMatch
                    {
                        Participant1Id = p1,
                        Participant2Id = p2
                    };

                    competitionRound.Matches.Add(match);
                }

                rounds.Add(competitionRound);

                // Rotate participants (keep first fixed)
                var last = orderedParticipants[n - 1];
                orderedParticipants.RemoveAt(n - 1);
                orderedParticipants.Insert(1, last);
            }

            return rounds;
        }
    }
}
