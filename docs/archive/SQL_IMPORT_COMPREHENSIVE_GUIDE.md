# ??? SQL Import System - Complete Guide

## Overview

The **SQL Import System** allows you to import historical WDPL (Wellington & District Pool League) data from phpMyAdmin/VBA Access SQL dump files into your .NET MAUI application. It intelligently detects seasons, maps VBA table structures, and merges data with your existing database.

---

## ?? Key Features

### Intelligent Import
- **Auto-detects season** from `tblleague` data
- **Preserves VBA IDs** in notes for reference tracking
- **Maps VBA tables** to modern data models
- **Handles relationships** automatically (divisions, teams, venues, players)
- **Merges or replaces** data based on dates

### Supported VBA/WDPL Tables

| VBA Table | Purpose | Imports To |
|-----------|---------|------------|
| **tblleague** | Season settings | Season + AppSettings |
| **tbldivisions** | League divisions | Division |
| **tblfixtures** | Match schedule | Fixture (schedule only) |
| **tblmatchheader** | Match metadata | Fixture (division, venue) |
| **tblmatchdetail** | Frame results | FrameResult |
| **tblmatchfooter** | Match scores | Fixture (calculated) |

### What Gets Imported

? **Season Information**
- Season name (e.g., "Wellington & District Pool League Winter 2025")
- Start/end dates
- Frames per match
- Rating settings (weighting, bias, factors)

? **Divisions**
- Division names
- Band colors (stored in notes)
- Linked to season

? **Teams** (auto-created)
- Team placeholders created from fixture data
- VBA Team ID preserved in notes
- Linked to season

? **Players** (auto-created)
- Player placeholders created from match details
- VBA Player ID preserved in notes
- Linked to season

? **Fixtures**
- Match dates and week numbers
- Home/away team assignments
- Division associations

? **Frame Results**
- Player matchups
- Winners (home/away/none)
- 8-ball clearances
- Frame order

---

## ?? How to Use

### Step 1: Export SQL from phpMyAdmin

1. **Open phpMyAdmin** and select your WDPL database
2. Click **Export** tab
3. Choose **Custom** export method
4. Select the following tables:
   - `tblleague` ? **REQUIRED**
   - `tbldivisions` ? **REQUIRED**
   - `tblfixtures` ? **REQUIRED**
   - `tblmatchheader` (optional - provides division/venue info)
   - `tblmatchdetail` (optional - provides frame results)
   - `tblmatchfooter` (optional - provides match scores)
5. Format: **SQL**
6. Click **Go** and save the `.sql` file

### Step 2: Import to MAUI App

#### Option A: Using SqlImportPage (Recommended)

1. Navigate to **Import ? SQL Import** in the app
2. Click **"?? Select SQL File (*.sql)"**
3. Choose your exported `.sql` file
4. Configure options:
   - ? **Replace existing season data** - Replaces existing data if newer
   - ? **Import as inactive season** - Don't auto-activate the imported season
5. Click **"?? Import SQL Data"**
6. Review the import log for warnings/errors
7. Navigate to **Seasons** page to view imported season

#### Option B: Using HistoricalImportPage (Wizard)

1. Navigate to **Import ? Historical Data** in the app
2. Select **"?? SQL Dump File (.sql)"**
3. Choose your `.sql` file
4. Follow the wizard steps
5. View import summary

---

## ?? Understanding the Import Process

### Phase 1: Season Detection

```csharp
// The importer reads tblleague to detect:
LeagueName:    "Wellington & District Pool League"
SeasonName:    "Winter"
SeasonYear:    2025
FirstMatchDate: 2025-09-18
Frames:        15
WinBonus:      2

// Creates season:
Season Name:   "Wellington & District Pool League Winter 2025"
Start Date:    2025-09-18
End Date:      2026-04-02 (calculated)
Active:        false (imported data is inactive by default)
```

### Phase 2: Data Mapping

**VBA ? MAUI Model Mapping:**

```csharp
// Division
VBA: tbldivisions.ID = 1, DivisionName = "United"
? Division: { 
    Id = new Guid(), 
    SeasonId = season.Id,
    Name = "United",
    Notes = "Imported from VBA (ID: 1)"
  }

// Team (auto-created from fixtures)
VBA: tblfixtures.HomeTeam = 1
? Team: {
    Id = new Guid(),
    SeasonId = season.Id,
    Name = "Team 1", // Placeholder until mapped
    Notes = "Imported from VBA (ID: 1)"
  }

// Player (auto-created from match details)
VBA: tblmatchdetail.Player1 = 93
? Player: {
    Id = new Guid(),
    SeasonId = season.Id,
    FirstName = "Player",
    LastName = "93", // Placeholder until mapped
    Notes = "Imported from VBA (ID: 93)"
  }

// Fixture
VBA: tblfixtures.MatchNo = 1, MatchDate = "2025-09-18"
? Fixture: {
    Id = new Guid(),
    SeasonId = season.Id,
    Date = 2025-09-18 19:30:00,
    HomeTeamId = mappedHomeTeamGuid,
    AwayTeamId = mappedAwayTeamGuid,
    Frames = [ /* frame results */ ]
  }

// Frame Result
VBA: tblmatchdetail.FrameNo = 1, Player1 = 93, Player2 = 43, HomeScore = 1
? FrameResult: {
    Number = 1,
    HomePlayerId = mappedPlayer93Guid,
    AwayPlayerId = mappedPlayer43Guid,
    Winner = FrameWinner.Home,
    EightBall = false
  }
```

### Phase 3: Relationship Building

```
Season
 ??? Division (SeasonId)
 ??? Team (SeasonId)
 ?    ??? Players (TeamId, SeasonId)
 ??? Fixture (SeasonId)
      ??? HomeTeamId ? Team
      ??? AwayTeamId ? Team
      ??? DivisionId ? Division
      ??? Frames
           ??? HomePlayerId ? Player
           ??? AwayPlayerId ? Player
```

---

## ?? Import Settings

### AppSettings Updated During Import

The importer automatically updates these settings from VBA `tblleague`:

| Setting | VBA Field | Description |
|---------|-----------|-------------|
| **RatingWeighting** | RatingWeighting | Base weighting for newest frame (220) |
| **RatingsBias** | RatingsBias | Decrement per older frame (4) |
| **RatingStartValue** | RatingStartValue | Starting rating (1000) |
| **MatchWinBonus** | WinBonus | Points for match win (2) |
| **WinFactor** | WinFactor | Win multiplier (1.25) |
| **LossFactor** | LoseFactor | Loss multiplier (0.75) |
| **EightBallFactor** | EightBallFactor | 8-ball multiplier (1.35) |
| **MinFramesPercentage** | MinFrame | Min frames % for ratings (60%) |

---

## ?? Common Issues & Solutions

### Issue: "No tblleague found in SQL file"

**Cause:** The SQL export didn't include the `tblleague` table.

**Solution:**
1. Re-export from phpMyAdmin
2. Make sure **tblleague** is selected in the export
3. Try again

### Issue: "Team X not found"

**Cause:** The fixture references a team ID that doesn't exist in the data.

**Solution:**
- The importer auto-creates placeholder teams
- Update team names after import:
  - Go to **Teams** page
  - Find teams named "Team 1", "Team 2", etc.
  - Edit their names to match real team names

### Issue: "Player duplicates after import"

**Cause:** Players were auto-created with placeholder names.

**Solution:**
- Use **Player Mapping** feature (if available) to merge duplicates
- Or manually update player names:
  - Go to **Players** page
  - Find players named "Player 93", etc.
  - Update to real names
  - Assign to correct teams

### Issue: "Import shows warnings but completes"

**Cause:** Non-critical issues like missing optional data.

**Solution:**
- Review the warnings log
- Most warnings are informational (e.g., "Player X not in any team")
- Data should still be usable

---

## ?? Import Results Example

```
SQL Dialect: MySQL

Season: Wellington & District Pool League Winter 2025
Divisions: 1
Venues: 0
Teams: 14 (auto-created)
Players: 127 (auto-created)
Fixtures: 182
Frames: 2730

?? Warnings: 3
- Division 'United' already exists for season - skipping
- Player 93 not assigned to team - added as free agent
- Match 8 missing some frames - check tblmatchdetail
```

---

## ?? Merge vs Replace

### Merge Mode (Default)
- Keeps existing data
- Only adds new records
- Prevents duplicates
- Safe for incremental imports

### Replace Mode
- Updates existing records if SQL data is newer
- Based on match dates
- Use when you want to overwrite with latest data
- ?? **Warning:** Can lose manual edits

**Recommendation:** Use **Merge** mode unless you're certain you want to replace existing data.

---

## ??? After Import Checklist

? **1. Verify Season**
- Go to **Seasons** page
- Check imported season appears
- Verify start/end dates
- Set as active if desired

? **2. Update Team Names**
- Go to **Teams** page
- Replace "Team 1", "Team 2" with real names
- Assign divisions
- Set home venues

? **3. Update Player Names**
- Go to **Players** page
- Replace "Player 93", etc. with real names
- Assign players to teams
- Merge any duplicates

? **4. Verify Fixtures**
- Go to **Fixtures** page
- Check match dates are correct
- Verify frame results imported
- Check scores calculate correctly

? **5. Calculate Ratings**
- Go to **League Tables** page
- Click **"Recalculate Ratings"**
- Verify player ratings are reasonable
- Compare with VBA if available

---

## ??? Technical Details

### File Format Support

**Supported SQL Dialects:**
- ? MySQL (phpMyAdmin)
- ? MariaDB
- ? PostgreSQL
- ? SQLite
- ? SQL Server

**SQL Statement Support:**
- ? `INSERT INTO table VALUES (...)`
- ? `INSERT INTO table (cols) VALUES (...)`
- ? Multi-row inserts: `VALUES (...), (...), (...)`
- ? Comments: `-- comment` and `/* comment */`
- ? String escaping: `'O\'Brien'`, `"value"`
- ? NULL values
- ? Date/time formats

### VBA ID Preservation

All VBA IDs are preserved in the **Notes** field:

```csharp
// Division
Notes: "Imported from VBA (ID: 1)"

// Team
Notes: "Imported from VBA (ID: 5)"

// Player  
Notes: "Imported from VBA (ID: 93)"
```

This allows you to:
- Reference original VBA data
- Debug import issues
- Cross-reference with VBA system

---

## ?? Related Documentation

- [Data Import Comparison](DATA_IMPORT_COMPARISON.md) - Compare SQL vs other import methods
- [Import System README](IMPORT_SYSTEM_README.md) - Overview of all import features
- [Historical Data Import Guide](HISTORICAL_DATA_IMPORT_GUIDE.md) - Import wizard walkthrough
- [SQL Import Quick Reference](SQL_IMPORT_QUICK_REFERENCE.md) - Quick tips and tricks

---

## ?? Support

If you encounter issues:

1. **Check the import log** for specific error messages
2. **Verify SQL file** contains expected tables
3. **Review warnings** to understand what was skipped
4. **Test with sample data** first before importing production data
5. **Backup your data** before large imports

For technical support, please provide:
- SQL file size and dialect
- Error messages from import log
- Number of records in each VBA table
- .NET MAUI app version

---

## ?? Advanced Usage

### Batch Import Multiple Seasons

1. Export each season separately from phpMyAdmin
2. Import them one at a time through the app
3. Each will create a separate season
4. Use **Seasons** page to manage multiple seasons

### Partial Imports

You can import just specific tables:

**Fixtures Only:**
- Export only `tblfixtures`, `tblleague`, `tbldivisions`
- Import will create schedule without results

**Results Only:**
- Export `tblmatchdetail`, `tblmatchfooter` along with core tables
- Import will add frame results to existing fixtures

### Re-importing Updated Data

1. Enable **"Replace existing season data"** option
2. Import the updated SQL file
3. Newer records will replace older ones
4. Based on `MatchDate` comparison

---

## ? Best Practices

1. **Always export `tblleague`** - It's required for season detection
2. **Include all VBA tables** if possible - More complete data
3. **Test with small data first** - Verify import works before full season
4. **Review import log** - Check for warnings before using data
5. **Backup before import** - Save existing data in case of issues
6. **Update placeholders** - Replace auto-generated team/player names
7. **Recalculate ratings** - After import to ensure accuracy
8. **Keep SQL files** - For reference and re-imports if needed

---

## ?? Performance

**Import Speed (approximate):**

| Records | Time |
|---------|------|
| 100 fixtures, 1500 frames | ~2-3 seconds |
| 200 fixtures, 3000 frames | ~5-7 seconds |
| 500 fixtures, 7500 frames | ~15-20 seconds |

*Performance varies based on device and SQL file complexity*

---

## ?? Future Enhancements

Planned features:

- [ ] **Auto-map team names** from historical data
- [ ] **Player name matching** using fuzzy logic
- [ ] **Batch season import** from multiple files
- [ ] **Import validation** before committing
- [ ] **Rollback capability** to undo imports
- [ ] **Progress bar** for large imports
- [ ] **Export back to SQL** for backups

---

*Last Updated: January 2025*
*WDPL2 .NET MAUI Application*
