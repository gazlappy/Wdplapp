# SQL Import Manual Workflow Implementation Summary

## Overview
A complete two-step SQL import system has been implemented that allows manual review before importing VBA Access database SQL dumps.

## Key Features Implemented

### 1. Manual Preview/Review Step
- **Before Import**: The system now parses the SQL file and shows what will be imported
- **No Auto-Processing**: Import only happens after explicit user confirmation
- **Data Statistics**: Shows counts of tables, rows, and expected imports

### 2. Complete Data Import
The SQL importer now handles all data types:

#### Season Data ?
- Extracts from `tblleague`
- Creates season with name and year
- Sets start/end dates

#### Divisions ?
- Imports from `tbldivisions`
- Links to the imported season

#### Teams ?
- **Auto-detects** from fixture data (HomeTeam/AwayTeam IDs)
- Creates placeholder team names: "Team 1", "Team 2", etc.
- Maps VBA team IDs to new GUIDs for relationship preservation

#### Players ?
- **Auto-detects** from match detail data (Player1, Player2, PlayerID, Played columns)
- Creates placeholder player names: "Player 1", "Player 2", etc.
- Maps VBA player IDs to new GUIDs
- Checks for existing players to avoid duplicates

#### Fixtures ?
- Imports from `tblfixtures`
- Maps VBA team IDs to imported teams
- Preserves week numbers and match dates

#### Frame Results ?
- Imports from `tblmatchdetail` OR `tblplayerresult`
- Maps players to frames
- Determines winners from HomeScore/AwayScore
- Imports 8-ball markers (Achived8Ball or EightBall columns)
- Links frames to fixtures

### 3. VBA ID Mapping System
Preserves relationships from the VBA database:
```csharp
// Tracks VBA ID ? New GUID mappings
public Dictionary<int, Guid> VbaTeamIdToGuid { get; set; }
public Dictionary<int, Guid> VbaPlayerIdToGuid { get; set; }
```

This ensures that when importing results, player/team references are maintained correctly.

### 4. SQL Parser Enhancements
- **Handles MySQL/phpMyAdmin formats**
- **Bit field conversion**: `b'0'` ? `0`, `b'1'` ? `1`
- **Multi-value INSERTs**: Parses multiple rows per statement
- **Comment stripping**: Removes `--` comments and `/*!` MySQL directives
- **Value escaping**: Handles quotes, newlines, backslashes

### 5. Two-Step Workflow

#### Step 1: Select & Preview
```
1. Click "Select SQL File"
2. Choose your phpMyAdmin export
3. Click "Preview Data"
```

**Preview shows:**
- SQL Dialect detected
- Tables found with row counts
- Sample column names
- Expected import counts
- Warnings about placeholder names

#### Step 2: Confirm & Import
```
1. Review the preview data
2. Click "Confirm Import"
3. Review confirmation dialog
4. Data is imported
```

**After import:**
- Shows success summary
- Displays warnings/errors log
- Provides rollback button

### 6. Rollback Capability
If something goes wrong, click "Rollback" to remove all imported data:
- Removes seasons
- Removes divisions
- Removes teams
- Removes players
- Removes fixtures
- Removes frames

All tracked by GUID lists in `SqlImportResult`.

## UI Flow

### SQL Import Page Structure
```
?? Select SQL File
    ?
??? Preview Data (Parse only, no import)
    ?
    Preview Panel:
    - Tables detected
    - Row counts
    - Expected imports
    - Warnings
    ?
? Confirm Import (User decision)
    ?
    Import executes
    ?
    Results Panel:
    - Success summary
    - Warnings/Errors log
    - Rollback button
```

## Example SQL File Support

### Supported Tables
- `tblleague` ? Season
- `tbldivisions` ? Divisions
- `tblfixtures` ? Fixtures
- `tblmatchdetail` ? Frame results (preferred)
- `tblplayerresult` ? Alternative frame results
- `tblmatchheader` ? Match metadata (optional)
- `tblmatchfooter` ? Match scores (optional)

### Sample Data Flow
```
tblleague:
  ID=1, SeasonName="Winter", SeasonYear=2025
  ? Creates Season "Winter 2025"

tblfixtures:
  HomeTeam=1, AwayTeam=2, MatchDate='2025-09-18'
  ? Auto-creates "Team 1" and "Team 2"
  ? Creates fixture between them

tblmatchdetail:
  MatchNo=1, FrameNo=1, Player1=93, Player2=43, HomeScore=1, AwayScore=0
  ? Auto-creates "Player 93" and "Player 43"
  ? Creates frame result: Player 93 wins

Result:
  - 1 Season
  - 2 Teams (placeholder names)
  - 2 Players (placeholder names)
  - 1 Fixture with 1 frame
```

## Important Notes

### Placeholder Names ??
After import, you MUST update:
1. **Team names**: "Team 1" ? "Dog & Partridge A"
2. **Player names**: "Player 93" ? "John Smith"

These are created as placeholders because the VBA database uses numeric IDs without storing actual names in the fixture/result tables.

### Manual Steps After Import
1. Go to Seasons page
2. Select the imported season
3. Edit each team to set the correct name
4. Edit each player to set the correct name
5. Activate the season when ready

### Data Validation
The importer validates:
- Season exists before importing other data
- Teams exist before importing fixtures
- Players exist before importing frames
- Referential integrity (VBA IDs ? GUIDs)

### Error Handling
- **Parse errors**: Shown in warnings log
- **Missing data**: Skipped with warning
- **Invalid references**: Skipped with warning
- **All errors logged**: Visible in UI

## Code Structure

### Services/SqlFileImporter.cs
```csharp
// Parse only (for preview)
ParseSqlFileAsync(string sqlFilePath) ? ParsedSqlData

// Full import
ImportFromSqlFileAsync(string sqlFilePath, LeagueData existingData, bool replaceExisting) 
    ? (LeagueData, SqlImportResult)

// Rollback
RollbackImport(LeagueData data, SqlImportResult result)
```

### Import Methods
- `ImportSeasonData()` - Seasons from tblleague
- `ImportDivisions()` - Divisions from tbldivisions
- `ImportTeams()` - Teams from fixture data
- `ImportPlayers()` - Players from match detail data
- `ImportFixtures()` - Fixtures from tblfixtures
- `ImportResults()` - Frame results from tblmatchdetail/tblplayerresult

### Result Tracking
```csharp
public class SqlImportResult
{
    public List<Guid> ImportedSeasonIds
    public List<Guid> ImportedDivisionIds
    public List<Guid> ImportedTeamIds
    public List<Guid> ImportedPlayerIds
    public List<Guid> ImportedFixtureIds
    
    public Dictionary<int, Guid> VbaTeamIdToGuid
    public Dictionary<int, Guid> VbaPlayerIdToGuid
    
    public List<string> Warnings
    public List<string> Errors
}
```

## Testing the Import

### Sample Test Flow
1. Export your VBA Access database using phpMyAdmin
2. Open the Wdpl2 app
3. Navigate to SQL Import page
4. Select your .sql file
5. Click "Preview Data"
6. Verify the preview looks correct
7. Click "Confirm Import"
8. Check the results summary
9. Go to Seasons page ? Edit teams/players
10. Activate season
11. Test fixtures and results display

### Rollback Test
1. After import, click "Rollback"
2. Confirm the rollback
3. Verify all imported data is removed
4. Try importing again

## Known Limitations

1. **Team/Player Names**: Must be updated manually post-import
2. **Match Numbers**: Approximate fixture matching (assumes sequential)
3. **Venue Data**: Not imported (VBA tables don't include venue info)
4. **Historical Ratings**: Not imported (would require complex calculation)
5. **Competition Data**: Not imported (separate table, not commonly used)

## Future Enhancements

Possible improvements:
- [ ] Import player names from a separate mapping file
- [ ] Import team names from a separate mapping file
- [ ] Better fixture matching using tblmatchheader
- [ ] Import historical ratings from VBA calculations
- [ ] Support for other SQL dialects (PostgreSQL, SQL Server)
- [ ] Batch import multiple seasons
- [ ] CSV import as alternative to SQL

## Success Criteria

? SQL file parsing works
? Preview shows accurate data before import
? Manual confirmation required
? All data types imported correctly
? VBA ID relationships preserved
? Rollback functionality works
? Warnings and errors logged
? Build compiles successfully

## Support

If you encounter issues:
1. Check the warnings/errors log in the import results
2. Verify your SQL file format matches the expected structure
3. Try the preview first to see what will be imported
4. Use rollback if the import doesn't look correct
5. Check the sample_import.sql file for format reference

---

**Implementation Complete** ?
All SQL import features are now operational with manual review workflow.
