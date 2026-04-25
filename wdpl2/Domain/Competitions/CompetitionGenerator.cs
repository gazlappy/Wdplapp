namespace Wdpl2.Models
{
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
            if (participants == null || participants.Count < 2)
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

            // Assign groups to venue tables randomly
            AssignVenueTables(groups, settings.SelectedVenues);

            return (groups, null);
        }

        /// <summary>
        /// Randomly assign groups to available venue tables.
        /// Uses the specific tables selected at each venue. Groups are shuffled
        /// and dealt round-robin across the available table slots.
        /// </summary>
        public static void AssignVenueTables(List<CompetitionGroup> groups, List<CompetitionVenue> venues)
        {
            if (groups.Count == 0) return;

            // Build a flat list of table slots from the selected tables
            var tableSlots = new List<(Guid VenueId, string VenueName, Guid TableId, string TableLabel)>();
            foreach (var venue in venues)
            {
                foreach (var table in venue.SelectedTables)
                    tableSlots.Add((venue.VenueId, venue.VenueName, table.TableId, table.Label));
            }

            if (tableSlots.Count == 0)
            {
                // No tables selected — clear any existing assignments
                foreach (var g in groups)
                {
                    g.VenueId = null;
                    g.VenueName = null;
                    g.TableId = null;
                    g.TableLabel = null;
                    g.TableNumber = 0;
                }
                return;
            }

            // Shuffle the table slots so assignment is random
            var shuffledSlots = tableSlots.OrderBy(_ => Random.Shared.Next()).ToList();

            // Assign groups to slots (round-robin if more groups than tables)
            for (int i = 0; i < groups.Count; i++)
            {
                var slot = shuffledSlots[i % shuffledSlots.Count];
                groups[i].VenueId = slot.VenueId;
                groups[i].VenueName = slot.VenueName;
                groups[i].TableId = slot.TableId;
                groups[i].TableLabel = slot.TableLabel;
                groups[i].TableNumber = (i % shuffledSlots.Count) + 1;
            }
        }

        /// <summary>
        /// Assign matches in a round to available venue tables (round-robin).
        /// </summary>
        public static void AssignMatchVenueTables(List<CompetitionMatch> matches, List<CompetitionVenue> venues)
        {
            if (matches.Count == 0) return;

            var tableSlots = new List<(Guid VenueId, string VenueName, Guid TableId, string TableLabel)>();
            foreach (var venue in venues)
            {
                foreach (var table in venue.SelectedTables)
                    tableSlots.Add((venue.VenueId, venue.VenueName, table.TableId, table.Label));
            }

            // Separate playable matches from BYEs (where a participant is missing)
            var playable = matches.Where(m => m.Participant1Id.HasValue && m.Participant2Id.HasValue).ToList();
            var byes = matches.Where(m => !m.Participant1Id.HasValue || !m.Participant2Id.HasValue).ToList();

            // Clear venue on BYE matches
            foreach (var m in byes)
            {
                m.VenueId = null;
                m.VenueName = null;
                m.TableId = null;
                m.TableLabel = null;
            }

            if (tableSlots.Count == 0)
            {
                foreach (var m in playable)
                {
                    m.VenueId = null;
                    m.VenueName = null;
                    m.TableId = null;
                    m.TableLabel = null;
                }
                return;
            }

            for (int i = 0; i < playable.Count; i++)
            {
                var slot = tableSlots[i % tableSlots.Count];
                playable[i].VenueId = slot.VenueId;
                playable[i].VenueName = slot.VenueName;
                playable[i].TableId = slot.TableId;
                playable[i].TableLabel = slot.TableLabel;
            }
        }

        /// <summary>
        /// Randomly assign matches in a round to available venue tables (shuffled).
        /// Same as AssignMatchVenueTables but randomises the table order.
        /// </summary>
        public static void ShuffleMatchVenueTables(List<CompetitionMatch> matches, List<CompetitionVenue> venues)
        {
            if (matches.Count == 0) return;

            var tableSlots = new List<(Guid VenueId, string VenueName, Guid TableId, string TableLabel)>();
            foreach (var venue in venues)
            {
                foreach (var table in venue.SelectedTables)
                    tableSlots.Add((venue.VenueId, venue.VenueName, table.TableId, table.Label));
            }

            // Separate playable matches from BYEs (where a participant is missing)
            var playable = matches.Where(m => m.Participant1Id.HasValue && m.Participant2Id.HasValue).ToList();
            var byes = matches.Where(m => !m.Participant1Id.HasValue || !m.Participant2Id.HasValue).ToList();

            // Clear venue on BYE matches
            foreach (var m in byes)
            {
                m.VenueId = null;
                m.VenueName = null;
                m.TableId = null;
                m.TableLabel = null;
            }

            if (tableSlots.Count == 0)
            {
                foreach (var m in playable)
                {
                    m.VenueId = null;
                    m.VenueName = null;
                    m.TableId = null;
                    m.TableLabel = null;
                }
                return;
            }

            // Shuffle table slots randomly
            var rng = Random.Shared;
            for (int i = tableSlots.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (tableSlots[i], tableSlots[j]) = (tableSlots[j], tableSlots[i]);
            }

            for (int i = 0; i < playable.Count; i++)
            {
                var slot = tableSlots[i % tableSlots.Count];
                playable[i].VenueId = slot.VenueId;
                playable[i].VenueName = slot.VenueName;
                playable[i].TableId = slot.TableId;
                playable[i].TableLabel = slot.TableLabel;
            }
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
        /// Generate a single elimination knockout bracket with random seeding.
        /// Byes are distributed using standard tournament seeding positions so
        /// they spread evenly across the bracket instead of clustering at the bottom.
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
            var seeded = randomize
                ? participants.OrderBy(_ => Random.Shared.Next()).ToList()
                : new List<Guid>(participants);

            // Build the bracket slots using proper seeding order so byes are
            // distributed evenly. Standard seeding places seed 1 vs last seed,
            // seed 2 vs second-to-last, etc.  We generate the seed positions
            // for the bracket size and then map our participants into those slots.
            var seedOrder = GetBracketSeedOrder(bracketSize);

            var slots = new Guid?[bracketSize];
            for (int i = 0; i < bracketSize; i++)
            {
                int seedIndex = seedOrder[i]; // 0-based seed position
                slots[i] = seedIndex < seeded.Count ? seeded[seedIndex] : null;
            }

            var currentRound = slots.ToList();
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
                bool isFirstRound = roundNum == 1;

                for (int i = 0; i < currentRound.Count; i += 2)
                {
                    var p1 = currentRound[i];
                    var p2 = currentRound[i + 1];

                    // In Round 1 only: skip completely empty slots (null vs null)
                    // where no real participant exists on either side.
                    // In later rounds null means "winner not yet determined" so we
                    // must still create the match as a placeholder.
                    if (isFirstRound && p1 == null && p2 == null)
                    {
                        nextRound.Add(null);
                        continue;
                    }

                    var match = new CompetitionMatch
                    {
                        Participant1Id = p1,
                        Participant2Id = p2
                    };

                    // Byes only apply in the first round. A null in Round 1 means
                    // "no opponent" (bye). In later rounds null means "winner not
                    // yet determined" — the match should wait for scores.
                    if (isFirstRound && p1 == null)
                    {
                        match.WinnerId = p2;
                        match.IsComplete = true;
                        nextRound.Add(p2);
                    }
                    else if (isFirstRound && p2 == null)
                    {
                        match.WinnerId = p1;
                        match.IsComplete = true;
                        nextRound.Add(p1);
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
        /// Generates the standard bracket seed ordering for a given bracket size.
        /// This ensures seed 1 plays seed N, seed 2 plays seed N-1, etc.,
        /// and byes (high seed numbers with no real participant) are distributed
        /// evenly across both halves of the bracket.
        /// </summary>
        private static int[] GetBracketSeedOrder(int bracketSize)
        {
            // Start with seed positions [0] (just the top seed)
            var order = new List<int> { 0 };

            // Iteratively build the bracket ordering by interleaving
            // complementary seeds at each level
            while (order.Count < bracketSize)
            {
                var nextOrder = new List<int>(order.Count * 2);
                int currentSize = order.Count * 2;
                foreach (var seed in order)
                {
                    nextOrder.Add(seed);
                    nextOrder.Add(currentSize - 1 - seed);
                }
                order = nextOrder;
            }

            return order.ToArray();
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
        /// Generate an empty knockout bracket for manual participant assignment.
        /// Creates the correct number of rounds and match placeholders with all
        /// participant slots set to null (TBD).
        /// </summary>
        public static List<CompetitionRound> GenerateManualKnockout(int participantCount)
        {
            if (participantCount < 2)
                return new List<CompetitionRound>();

            var rounds = new List<CompetitionRound>();

            // Find next power of 2
            int bracketSize = 1;
            while (bracketSize < participantCount)
                bracketSize *= 2;

            int totalRounds = (int)Math.Log2(bracketSize);
            int matchesInRound = bracketSize / 2;

            for (int r = 1; r <= totalRounds; r++)
            {
                var round = new CompetitionRound
                {
                    Name = GetRoundName(totalRounds, r, matchesInRound),
                    RoundNumber = r
                };

                for (int m = 0; m < matchesInRound; m++)
                {
                    round.Matches.Add(new CompetitionMatch());
                }

                rounds.Add(round);
                matchesInRound /= 2;
            }

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
