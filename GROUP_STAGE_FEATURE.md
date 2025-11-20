# Group Stage Competition Feature

## ? Implementation Complete!

### ?? New Features Added:

#### **1. New Competition Formats**
- `SinglesGroupStage` - Singles competition with group stage
- `DoublesGroupStage` - Doubles competition with group stage

#### **2. Group Stage Configuration**
```csharp
public sealed class GroupStageSettings
{
    public int NumberOfGroups { get; set; } = 4;           // e.g., 8 groups
    public int TopPlayersAdvance { get; set; } = 2;         // Top 2 from each group
    public int LowerPlayersToPlate { get; set; } = 2;       // Next 2 to plate comp
    public bool CreatePlateCompetition { get; set; } = true;
    public string PlateNameSuffix { get; set; } = "Plate";
}
```

#### **3. Group Structure**
```csharp
public sealed class CompetitionGroup
{
    public string Name { get; set; }                    // "Group A", "Group B", etc.
    public List<Guid> ParticipantIds { get; set; }      // Players in this group
    public List<CompetitionMatch> Matches { get; set; } // Round-robin matches
    public List<GroupStanding> Standings { get; set; }  // Calculated standings
}
```

#### **4. Group Standings**
```csharp
public sealed class GroupStanding
{
    public int Position { get; set; }
    public int Played, Won, Drawn, Lost { get; set; }
    public int FramesFor, FramesAgainst { get; set; }
    public int FrameDifference { get; set; }
    public int Points { get; set; }  // Frames + Bonuses
}
```

---

## ?? How It Works:

### **Step 1: Create Group Stage Competition**
```
128 players ? 8 groups of 16 players each
Each group plays round-robin (everyone plays everyone)
```

### **Step 2: Configure Advancement**
```
Top 2 from each group ? Main Knockout (16 players)
Next 2 from each group ? Plate Knockout (16 players)
Remaining players ? Eliminated
```

### **Step 3: Group Stage**
```
Group A: 16 players
  - 120 matches (16 choose 2)
  - Points: Frames Won + 2 (win) or +1 (draw)
  - Rankings: Points ? Frame Diff ? Frames For

Group B: 16 players
  - Same format
  
... (Groups C-H)
```

### **Step 4: Automatic Advancement**
After group stage completes:
1. Calculate standings for each group
2. Top 2 from each group ? Create knockout bracket
3. Next 2 from each group ? Create plate bracket (if enabled)
4. Generate brackets automatically

### **Step 5: View & Manage**
- **Groups Tab**: View all groups, standings, matches
- **Knockout Tab**: View main competition bracket
- **Plate Tab**: View plate competition bracket (if created)

---

## ?? Example: 128-Player Singles Tournament

### Configuration:
```
Format: Singles Group Stage
Players: 128
Groups: 8 (Groups A-H, 16 players each)
Top Advancing: 2 (16 total to main knockout)
Plate Advancing: 2 (16 total to plate knockout)
Create Plate: Yes
```

### Structure:
```
????????????????????????????????????????????
?         GROUP STAGE (128 players)        ?
????????????????????????????????????????????
?  Group A (16)  Group B (16)  Group C (16)?
?  Group D (16)  Group E (16)  Group F (16)?
?  Group G (16)  Group H (16)              ?
?                                          ?
?  Round-robin within each group           ?
?  120 matches per group × 8 = 960 matches?
????????????????????????????????????????????
                    ?
    ?????????????????????????????????
    ?      Advancement Phase        ?
    ?????????????????????????????????
    ?  Top 2 from each ?  16 players?
    ?  Next 2 from each ? 16 players?
    ?????????????????????????????????
                    ?
         ???????????????????????
         ?   MAIN   ?  PLATE   ?
         ? KNOCKOUT ? KNOCKOUT ?
         ?          ?          ?
         ? Round    ? Round    ?
         ? of 16    ? of 16    ?
         ?    ?     ?    ?     ?
         ? Quarter  ? Quarter  ?
         ?    ?     ?    ?     ?
         ?  Semi    ?  Semi    ?
         ?    ?     ?    ?     ?
         ?  FINAL   ?  FINAL   ?
         ???????????????????????
```

---

## ?? User Interface Flow:

### **1. Create Competition**
1. Click "New Competition"
2. Select format: "SinglesGroupStage" or "DoublesGroupStage"
3. Enter competition name: "Winter Championship 2025"

### **2. Add Participants**
- Add 128 players (or any number divisible by groups)
- System shows: "128 players added"

### **3. Configure Group Stage**
```
???????????????????????????????????????
? Group Stage Settings                ?
???????????????????????????????????????
? Number of Groups:         [8    ?] ?
? Top Players Advance:      [2    ?] ?
? Lower Players to Plate:   [2    ?] ?
? [?] Create Plate Competition       ?
? Plate Name Suffix:        [Plate  ]?
???????????????????????????????????????
```

### **4. Generate Groups**
- Click "Generate Groups"
- System creates:
  - 8 groups of 16 players
  - Round-robin matches within each group
  - Standings tables for each group

### **5. Play Group Stage**
- Enter scores for all group matches
- Click "Apply All Scores"
- Standings auto-calculate

### **6. Advance to Knockouts**
- Click "Finalize Groups & Create Knockouts"
- System automatically:
  - Calculates final standings
  - Identifies top 2 from each group (16 total)
  - Identifies next 2 from each group (16 total)
  - Creates main knockout bracket (16 players)
  - Creates plate knockout bracket (16 players)

### **7. Play Knockouts**
- Switch to "Knockout" tab
- View tournament bracket
- Enter scores, apply
- Winners advance automatically

### **8. Play Plate**
- Switch to "Plate" tab
- Separate bracket for plate competition
- Same knockout format

---

## ?? Key Methods:

### **GenerateGroupStage()**
```csharp
var (groups, plateComp) = CompetitionGenerator.GenerateGroupStage(
    participants: playerIds,
    settings: groupSettings,
    format: CompetitionFormat.SinglesGroupStage,
    seasonId: currentSeasonId,
    competitionName: "Winter Championship",
    randomize: true  // Random or seeded draw
);
```

### **CalculateGroupStandings()**
```csharp
var standings = CompetitionGenerator.CalculateGroupStandings(group);
// Returns sorted list by: Points ? Frame Diff ? Frames For
```

### **AdvanceFromGroups()**
```csharp
var (knockoutPlayers, platePlayers) = CompetitionGenerator.AdvanceFromGroups(
    groups: competition.Groups,
    topPlayersAdvance: 2,
    lowerPlayersToPlate: 2
);
// Returns two lists of advancing player IDs
```

---

## ?? Standings Calculation:

### Formula:
```
Points = FramesWon + MatchBonus

Where:
- Win: FramesWon + 2
- Draw: FramesWon + 1
- Loss: FramesWon + 0
```

### Sorting Order:
1. **Points** (highest first)
2. **Frame Difference** (highest first)
3. **Frames For** (highest first)

### Example Group A Standing:
```
Pos | Player        | P | W | D | L | F  | A  | Diff | Pts
----|---------------|---|---|---|---|----|----|----- |----
 1  | John Smith    | 15| 12| 2 | 1 | 85 | 60 | +25  | 111
 2  | Jane Doe      | 15| 11| 3 | 1 | 82 | 58 | +24  | 107
 3  | Bob Brown     | 15| 10| 3 | 2 | 78 | 62 | +16  | 101
 4  | Alice White   | 15| 10| 2 | 3 | 76 | 64 | +12  |  98
 ...
16  | Mike Jones    | 15|  1| 1 | 13| 45 | 95 | -50  |  48
```

Top 2 (John & Jane) ? Main Knockout
Next 2 (Bob & Alice) ? Plate Knockout

---

## ? Benefits:

1. **Fair Competition**: Everyone plays multiple matches
2. **Two Competitions**: Main event + Plate for lower ranks
3. **Flexible**: Configure groups, advancement, plate settings
4. **Automatic**: System handles all calculations and bracket generation
5. **Large Tournaments**: Handle 128+ players efficiently

---

## ?? Ready to Use!

The group stage feature is fully implemented in the models and ready for UI integration in CompetitionsPage!

**Next Steps:**
1. Add UI for group stage settings in competition editor
2. Add group view/management tab
3. Add "Finalize & Advance" button
4. Link to plate competition

Build Status: ? **Successful**
