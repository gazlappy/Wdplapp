# ?? Import from Previous Seasons Feature

## Overview

A powerful new feature that allows you to **copy teams, players, venues, and divisions from previous seasons** into a new season, with smart duplicate detection.

---

## ? Key Features

### 1. **Smart Duplicate Detection**
- Automatically groups entities by name across all seasons
- Shows which seasons each entity appeared in
- **Skips duplicates** - won't import if already exists in target season

### 2. **Filtered Historical List**
- **Teams:** Grouped by team name (case-insensitive)
- **Players:** Grouped by first name + last name combination
- **Venues:** Grouped by venue name (includes tables)
- **Divisions:** Grouped by division name

### 3. **Bulk Selection**
- Select/deselect individual items with checkboxes
- "Select All" and "Clear" buttons for quick selection
- Live counter showing selected items

### 4. **Relationship Preservation**
- **Venues** ? Tables are copied with the venue
- **Teams** ? Properties like `ProvidesFood` are preserved
- **Players** ? Names are preserved (but team assignment is reset)

---

## ?? How to Use

### Step 1: Navigate to Seasons Page
1. Go to **SeasonsPage**
2. Select the **target season** (where you want to import data)

### Step 2: Open Import Dialog
1. Click **?** burger menu
2. Click **"?? Import from Previous Seasons"** button
3. Import dialog opens

### Step 3: Select Items to Import

#### Divisions Tab
- View all unique divisions from previous seasons
- Shows which seasons each division was used in
- Select divisions you want to copy

#### Venues Tab
- View all unique venues from previous seasons
- Shows address and number of tables
- Tables are automatically included when venue is imported
- Shows which seasons venue was used in

#### Teams Tab
- View all unique teams from previous seasons
- Shows captain name (if available)
- Shows which seasons team played in
- Select teams you want to copy

#### Players Tab
- View all unique players from previous seasons
- Shows last team they played for
- Shows which seasons they played in
- Select players you want to copy

### Step 4: Import
1. Review selection counter at bottom
2. Click **"Import Selected"**
3. Confirm the import
4. Review import summary

---

## ?? How Duplicate Detection Works

### Teams
```csharp
// Grouped by name (case-insensitive, trimmed)
"Eagles" in Season 2023
"Eagles" in Season 2024
?
Shows as ONE entry: "Eagles"
Seasons: "2023, 2024"
```

### Players
```csharp
// Grouped by First Name + Last Name
"John Smith" in Season 2023
"John Smith" in Season 2024
"john smith" in Season 2025 // Case-insensitive
?
Shows as ONE entry: "John Smith"
Seasons: "2023, 2024, 2025"
```

### Venues
```csharp
// Grouped by name (case-insensitive, trimmed)
"Royal Oak" in Season 2023 (3 tables)
"Royal Oak" in Season 2024 (4 tables)
?
Shows as ONE entry: "Royal Oak"
Tables: From most recent season (4 tables)
Seasons: "2023, 2024"
```

### Divisions
```csharp
// Grouped by name (case-insensitive, trimmed)
"Premier Division" in Season 2023
"Premier Division" in Season 2024
?
Shows as ONE entry: "Premier Division"
Seasons: "2023, 2024"
```

---

## ?? Import Process

### What Gets Copied

#### Divisions ?
- ? Name
- ? Notes (from most recent)
- ? NOT copied: Associated teams (must be manually assigned)

#### Venues ?
- ? Name
- ? Address
- ? **All tables** (with labels and max teams)
- ? NOT copied: Table IDs (new IDs generated)

#### Teams ?
- ? Name
- ? Captain name (legacy field)
- ? ProvidesFood flag
- ? NOT copied: DivisionId, VenueId, TableId, CaptainPlayerId
- ?? **Note:** User must reassign division, venue, and captain after import

#### Players ?
- ? First Name
- ? Last Name
- ? NOT copied: TeamId, Notes
- ?? **Note:** User must assign players to teams after import

### What Does NOT Get Copied
- ? Fixtures
- ? Competitions
- ? Match results
- ? Team relationships (division, venue, table)
- ? Player-team assignments

---

## ?? Database Structure

### Historical Data Classes

```csharp
public class HistoricalTeam
{
    public string Name { get; set; }
    public List<Guid> SourceSeasonIds { get; set; }
    public string SeasonsPlayed { get; set; } // Display: "2023, 2024"
    public bool IsSelected { get; set; }
}
```

### Copy Process

```csharp
1. Load all entities from previous seasons
   ?
2. Group by unique identifier (name, first+last, etc.)
   ?
3. User selects items to import
   ?
4. Check for duplicates in target season
   ?
5. Create new entities with new GUIDs
   ?
6. Assign to target season
   ?
7. Save to database
```

---

## ?? UI Features

### Tabbed Interface
- **4 tabs:** Divisions, Venues, Teams, Players
- Active tab highlighted with `PrimaryButtonStyle`
- Smooth tab switching

### Selection Controls
- **Checkbox per item** - Individual selection
- **Select All** button - Select all visible items
- **Clear** button - Deselect all
- **Live counter** - Shows selected count

### Status Display
```
Selected: 2 div, 5 venues, 12 teams, 45 players (64 total)
```

### Empty States
- Friendly message when no historical data found
- Guidance text explaining what to do

---

## ?? Service: SeasonCopyService

### Methods

#### Get Historical Data
```csharp
GetHistoricalTeams(LeagueData data, Guid targetSeasonId)
GetHistoricalPlayers(LeagueData data, Guid targetSeasonId)
GetHistoricalVenues(LeagueData data, Guid targetSeasonId)
GetHistoricalDivisions(LeagueData data, Guid targetSeasonId)
```

#### Copy to Season
```csharp
CopyTeamsToSeason(LeagueData data, List<HistoricalTeam> teams, Guid targetSeasonId)
CopyPlayersToSeason(LeagueData data, List<HistoricalPlayer> players, Guid targetSeasonId)
CopyVenuesToSeason(LeagueData data, List<HistoricalVenue> venues, Guid targetSeasonId)
CopyDivisionsToSeason(LeagueData data, List<HistoricalDivision> divisions, Guid targetSeasonId)
```

#### Bulk Copy
```csharp
CopyAllToSeason(
    LeagueData data,
    List<HistoricalDivision> divisions,
    List<HistoricalVenue> venues,
    List<HistoricalTeam> teams,
    List<HistoricalPlayer> players,
    Guid targetSeasonId)
? Returns: (int divisions, int venues, int teams, int players)
```

---

## ?? Example Workflow

### Scenario: Starting Season 2025

**Previous Seasons:**
- Season 2023: 10 teams, 50 players
- Season 2024: 12 teams, 60 players

**Goal:** Import teams and players for Season 2025

### Steps:

1. **Create Season 2025**
   ```
   SeasonsPage ? New Season ? "2025"
   ```

2. **Import Historical Data**
   ```
   Select "2025" season
   ? ? Import from Previous Seasons
   ```

3. **Select Items**
   ```
   Divisions: ? Premier, ? Division One, ? Division Two
   Venues: ? Royal Oak, ? Star Inn, ? Bull's Head
   Teams: ? All teams (Select All button)
   Players: ? All players (Select All button)
   ```

4. **Import**
   ```
   Click "Import Selected"
   Confirm import
   ```

5. **Result**
   ```
   Imported:
   • 3 Divisions
   • 8 Venues (5 skipped - already exist)
   • 15 Teams (unique names)
   • 72 Players (unique names)
   
   12 items skipped (duplicates)
   ```

6. **After Import - Manual Steps**
   ```
   TeamsPage: Assign divisions to teams
   TeamsPage: Assign venues/tables to teams
   PlayersPage: Assign players to teams
   TeamsPage: Assign captains
   ```

---

## ?? Important Notes

### 1. **Relationships Must Be Reassigned**
After importing:
- Teams have NO division assigned ? Assign in TeamsPage
- Teams have NO venue assigned ? Assign in TeamsPage
- Players have NO team assigned ? Assign in PlayersPage
- Teams have NO captain ? Assign in TeamsPage

### 2. **Duplicate Skipping is Automatic**
- If "Eagles" already exists in target season ? Skip
- If "John Smith" already exists ? Skip
- No error, just skipped silently
- Import summary shows how many were skipped

### 3. **IDs are New**
- All imported entities get **new GUIDs**
- No relationship to previous season entities
- Completely fresh records

### 4. **Tables Come With Venues**
- When you import a venue, its tables are included
- Table IDs are regenerated
- Table labels and max teams are preserved

### 5. **Most Recent Data Used**
- If an entity exists in multiple seasons
- Data from the **most recent season** is used
- Example: Venue has 3 tables in 2023, 4 tables in 2024
  ? Import will use 4 tables from 2024

---

## ?? Benefits

### Time Saving
- ? No manual re-entry of teams
- ? No manual re-entry of players
- ? No manual re-entry of venues
- ? Bulk import with checkboxes

### Data Integrity
- ? Smart duplicate detection
- ? Automatic duplicate skipping
- ? Clear import summary

### Flexibility
- ? Choose what to import (selective)
- ? Import from multiple previous seasons
- ? See historical context (which seasons used)

### User Experience
- ? Tabbed interface (organized)
- ? Select All / Clear buttons (quick)
- ? Live counter (feedback)
- ? Clear status messages

---

## ?? Future Enhancements (Possible)

- **Smart Assignment:** Automatically assign teams to divisions based on previous season
- **Player-Team Preservation:** Option to keep player-team relationships
- **Bulk Edit:** Edit multiple items before import
- **Import Templates:** Save selection as template for next season
- **Import History:** Track what was imported when
- **Undo Import:** Revert an import operation

---

## ? Summary

**Feature:** Import from Previous Seasons  
**Location:** SeasonsPage ? ? ? "?? Import from Previous Seasons"  
**What it does:** Copies teams, players, venues, divisions from old seasons to new season  
**Smart Features:** Duplicate detection, grouping, bulk selection, relationship preservation  
**Manual Steps After:** Assign divisions, venues, teams, captains  

**Result:** Quickly populate a new season with historical data while avoiding duplicates! ??
