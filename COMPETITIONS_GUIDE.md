# Competitions Feature Documentation

## Overview

The Competitions feature allows you to organize and run various tournament formats including knockout competitions, round-robin events, and more. This complements the regular league fixtures with special events.

---

## Competition Formats

### 1. **Singles Knockout** (Single Elimination)
- **Description:** Traditional bracket where players are eliminated after one loss
- **Use Case:** Quick tournaments, championship playoffs
- **Features:**
  - Automatic seeding
  - Bye management for non-power-of-2 participant counts
  - Progressive rounds (Quarter-Finals ? Semi-Finals ? Final)

### 2. **Doubles Knockout**
- **Description:** Knockout tournament for pairs of players
- **Use Case:** Doubles championships, team-building events
- **Features:**
  - Pair management
  - Same bracket structure as Singles Knockout
  - Team naming options

### 3. **Team Knockout**
- **Description:** Knockout tournament for teams
- **Use Case:** Inter-division playoffs, cup competitions
- **Features:**
  - Uses existing league teams
  - Team-based bracket generation
  - Suitable for divisional champions tournaments

### 4. **Round Robin** (Coming Soon)
- **Description:** Every participant plays every other participant
- **Use Case:** League-style mini tournaments, qualification rounds
- **Features:**
  - Fair schedule generation
  - Automatic rotation algorithm
  - Points-based standings

### 5. **Swiss System** (Coming Soon)
- **Description:** Pairing system where players with similar records face each other
- **Use Case:** Large tournaments, chess-style events
- **Features:**
  - Dynamic pairing based on performance
  - No elimination
  - Suitable for many participants

---

## Using the Competitions Page

### Layout

The Competitions page follows the same two-column layout as Settings:

- **Left Panel:** Competition list and management buttons
- **Right Panel:** Competition details, bracket view, and editing

### Creating a New Competition

1. Click **"New"** button in the left panel
2. A new competition is created with default settings
3. Edit the competition details in the right panel

### Competition Details

#### Basic Information
- **Name:** Competition title (e.g., "Winter Championship 2025")
- **Format:** Choose from available competition formats
- **Status:**
  - `Draft` - Planning stage, bracket not generated
  - `InProgress` - Bracket generated, matches ongoing
  - `Completed` - All matches finished
- **Start Date:** When the competition begins
- **Notes:** Additional information

#### Managing Participants

**Adding Participants:**
1. Click **"Add Participant"** button
2. Select from available players/teams based on format:
   - **Singles:** Choose from season players
   - **Doubles:** Create pairs (coming soon)
   - **Team:** Choose from season teams

**Removing Participants:**
- Click **"Remove"** next to any participant
- Or use **"Clear All"** to start fresh

**Requirements:**
- Minimum 2 participants required
- No maximum limit (bracket automatically adjusts)

### Generating the Bracket

1. Add all participants
2. Click **"Generate Bracket"**
3. The system creates:
   - Appropriate number of rounds
   - Balanced bracket structure
   - Byes if needed (non-power-of-2 participants)
   - Match pairings

**Auto-generated Round Names:**
- Round of 32, Round of 16
- Quarter-Finals (8 participants)
- Semi-Finals (4 participants)
- Final (2 participants)

### Viewing and Managing the Bracket

Click **"View Bracket"** to see:
- All rounds and matches
- Participant matchups
- Score entry fields
- Match progression

**Score Entry:**
- Enter scores directly in the bracket view
- Winner automatically advances
- Completed matches update bracket flow

---

## Technical Details

### Data Models

#### **Competition**
```csharp
public sealed class Competition
{
    public Guid Id { get; set; }
    public Guid? SeasonId { get; set; }
    public string Name { get; set; }
    public CompetitionFormat Format { get; set; }
    public CompetitionStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? StartDate { get; set; }
    public List<Guid> ParticipantIds { get; set; }
    public List<DoublesTeam> DoublesTeams { get; set; }
    public List<CompetitionRound> Rounds { get; set; }
}
```

#### **CompetitionRound**
```csharp
public sealed class CompetitionRound
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int RoundNumber { get; set; }
    public List<CompetitionMatch> Matches { get; set; }
}
```

#### **CompetitionMatch**
```csharp
public sealed class CompetitionMatch
{
    public Guid Id { get; set; }
    public Guid? Participant1Id { get; set; }
    public Guid? Participant2Id { get; set; }
    public Guid? WinnerId { get; set; }
    public int Participant1Score { get; set; }
    public int Participant2Score { get; set; }
    public bool IsComplete { get; set; }
}
```

### Bracket Generation Algorithms

#### Single Elimination
```
- Finds next power of 2 ? participant count
- Adds byes for balance
- Creates logarithmic rounds (log? n)
- Auto-advances byes
```

#### Double Elimination (Planned)
```
- Winners Bracket (single elimination)
- Losers Bracket (for eliminated players)
- Grand Final (winners bracket vs losers bracket champion)
- Bracket reset if losers bracket wins grand final
```

#### Round Robin
```
- (n-1) rounds for n participants
- n/2 matches per round
- Circle rotation algorithm
- Every participant plays every other once
```

---

## Best Practices

### Planning a Competition

1. **Choose Format Carefully**
   - Singles Knockout: Best for 4-32 players, quick completion
   - Doubles: Social events, variety
   - Team Knockout: Inter-division competitions
   - Round Robin: When you want everyone to play multiple matches

2. **Timing**
   - Singles KO with 16 players = 15 matches total
   - Singles KO with 32 players = 31 matches total
   - Round Robin with 8 players = 28 matches total

3. **Seeding**
   - Currently random
   - Manual reordering coming soon
   - Consider importing seeded participant lists

### Managing During Competition

1. **Update Status** as competition progresses:
   - Draft ? InProgress (when bracket generated)
   - InProgress ? Completed (when finished)

2. **Score Entry**
   - Enter scores promptly
   - Double-check before marking complete
   - Winners auto-advance to next round

3. **Bracket Regeneration**
   - Can regenerate bracket in Draft status
   - Regenerating loses all previous scores
   - Use with caution!

---

## Future Enhancements

### Planned Features

- [ ] **Seeding Options**
  - Manual seed ordering
  - Automatic seeding based on ratings
  - Import seed lists

- [ ] **Advanced Doubles Management**
  - Doubles team builder
  - Partnership history
  - Team naming and colors

- [ ] **Double Elimination Full Implementation**
  - Complete losers bracket generation
  - Grand final with bracket reset logic
  - Automatic progression tracking

- [ ] **Swiss System**
  - Dynamic pairing algorithm
  - Tiebreaker systems
  - Buchholz and Sonneborn-Berger scores

- [ ] **Match Scheduling**
  - Date/time assignment
  - Table/venue assignment
  - Schedule conflict detection

- [ ] **Reporting & Export**
  - Bracket PDF export
  - Results summary
  - Statistics and analytics

- [ ] **Visual Bracket Display**
  - Graphical bracket tree view
  - Drag-and-drop interface
  - Print-friendly layouts

- [ ] **Live Updates**
  - Real-time score updates
  - Mobile-friendly bracket view
  - Spectator mode

---

## Troubleshooting

### "Need at least 2 participants to generate bracket"
- **Cause:** Fewer than 2 participants added
- **Solution:** Add more participants before generating

### "All players have been added"
- **Cause:** All available players already in competition
- **Solution:** Check participant list, remove duplicates, or adjust format

### Bracket Not Showing
- **Cause:** Bracket not generated yet
- **Solution:** Click "Generate Bracket" first

### Scores Not Saving
- **Cause:** Changes not saved to data store
- **Solution:** Click "Save Changes" button after entering scores

### Competition Not Appearing
- **Cause:** Wrong season selected
- **Solution:** Check current season in Seasons tab

---

## Examples

### Example 1: Quick Singles Tournament (8 Players)
1. Create competition: "Friday Night Singles"
2. Format: Singles Knockout
3. Add 8 players
4. Generate bracket ? Creates 3 rounds (Quarter-Finals, Semi-Finals, Final)
5. Total matches: 7

### Example 2: Team Cup (16 Teams)
1. Create competition: "Division Champions Cup"
2. Format: Team Knockout
3. Add 16 teams
4. Generate bracket ? Creates 4 rounds
5. Total matches: 15

### Example 3: Round Robin (6 Players)
1. Create competition: "Mini League"
2. Format: Round Robin
3. Add 6 players
4. Generate bracket ? Creates 5 rounds
5. Total matches: 15 (everyone plays everyone)

---

## Integration with League System

### Season-Specific
- Competitions are tied to seasons
- Only participants from current season available
- Archived with season data

### Player/Team Records
- Competition results tracked separately from league
- Can be used for seeding in future competitions
- Historical competition data preserved

### Data Persistence
- Saved in `leaguedata.json`
- Backed up with regular data backups
- Exported with season exports

---

## Technical Notes

### Performance
- Bracket generation: O(n log n)
- Round robin generation: O(n²)
- Efficient for up to 128 participants
- Lazy loading for large brackets

### Data Storage
- Competitions stored in `LeagueData.Competitions`
- JSON serialization/deserialization
- Backward compatible with existing data

### UI Framework
- .NET MAUI ContentPage
- Responsive two-column layout
- Dynamic content switching
- CollectionView for efficient lists

---

## Support & Feedback

For issues, suggestions, or feature requests related to the Competitions feature, please note:
- Feature is in active development
- Some advanced features planned for future releases
- Feedback welcome for prioritization

---

## Version History

**v1.0** - Initial Release
- Singles Knockout
- Doubles Knockout  
- Team Knockout
- Round Robin
- Basic bracket generation
- Score entry interface

**Coming in v1.1**
- Double elimination full implementation
- Visual bracket tree view
- Advanced seeding options
- PDF export

---

## Quick Reference

| Action | Location | Shortcut |
|--------|----------|----------|
| Create Competition | Left Panel ? New | - |
| Add Participant | Right Panel ? Add Participant | - |
| Generate Bracket | Right Panel ? Generate Bracket | - |
| View Bracket | Right Panel ? View Bracket | - |
| Save Changes | Right Panel ? Save Changes | - |
| Delete Competition | Left Panel ? Delete | - |

| Format | Min Players | Rounds Formula | Best For |
|--------|------------|----------------|----------|
| Singles KO | 2 | log?(n) | Quick tournaments |
| Doubles KO | 2 pairs | log?(n) | Social events |
| Team KO | 2 | log?(n) | Cup competitions |
| Round Robin | 2 | n-1 | Everyone plays all |
| Swiss | 4 | Configurable | Large fields |
