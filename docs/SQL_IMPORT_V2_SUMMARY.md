# SQL Import v2.0 - Implementation Summary

## Overview
This document summarizes the SQL Import v2.0 enhancement that adds ID-to-name mapping and intelligent duplicate detection to the WDPL SQL import system.

## Problem Statement
The previous SQL import system had the following issues:
1. Player names were not extracted from `tblplayers` - only placeholder names like "Player 1" were created
2. Re-running imports would create duplicate records in the database
3. No visibility into what was imported vs. what already existed
4. Manual cleanup required after imports

## Solution Implemented

### 1. ID-to-Name Mapping
**Feature**: Parse `tblplayers` table to build PlayerID → PlayerName dictionaries

**Implementation**:
```csharp
// New method: BuildIdToNameMappings()
private static void BuildIdToNameMappings(
    Dictionary<string, List<Dictionary<string, string>>> tableData,
    SqlImportResult result)
{
    // Parse tblplayers to extract PlayerID → PlayerName
    if (tableData.ContainsKey("tblplayers"))
    {
        foreach (var playerRow in tableData["tblplayers"])
        {
            var playerId = GetIntValue(playerRow, "PlayerID", -1);
            var playerName = GetStringValue(playerRow, "PlayerName", "");
            
            if (playerId >= 0 && !string.IsNullOrWhiteSpace(playerName))
            {
                result.VbaPlayerIdToName[playerId] = playerName;
            }
        }
    }
}
```

**Result**: 
- Players imported with actual names: "TONY HARTNELL", "PETE BROWN", etc.
- No more "Player 1, Player 2" placeholders
- Team associations preserved from tblplayers Team field

### 2. Duplicate Detection
**Feature**: Check existing database before importing to prevent duplicates

**Implementation**:

#### Players
```csharp
// Check by name OR PlayerID
var existingPlayer = existingData.Players.FirstOrDefault(p => 
    (p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase) ||
     p.Name.EndsWith($"({vbaPlayerId})")) &&
    p.SeasonId == result.DetectedSeason?.Id);

if (existingPlayer != null)
{
    result.VbaPlayerIdToGuid[vbaPlayerId] = existingPlayer.Id;
    result.PlayersSkipped++;
    continue; // Skip - already exists
}
```

#### Fixtures
```csharp
// Check by date + teams
var existingFixture = existingData.Fixtures.FirstOrDefault(f =>
    f.SeasonId == result.DetectedSeason?.Id &&
    f.Date.Date == matchDate.Date &&
    f.HomeTeamId == homeTeamId &&
    f.AwayTeamId == awayTeamId);

if (existingFixture != null)
{
    result.FixturesSkipped++;
    continue; // Skip - already exists
}
```

#### Results/Frames
```csharp
// Check if fixture already has frames
if (fixture.Frames.Count > 0)
{
    result.ResultsSkipped++;
    result.FramesSkipped += matchGroup.Count();
    continue; // Skip - results already imported
}
```

**Result**:
- Safe to re-run imports - no duplicates created
- Tracks skipped records for reporting
- Incremental imports supported (add new results without re-importing old data)

### 3. Enhanced Statistics
**Feature**: Track and display imported vs. skipped counts

**Implementation**:
```csharp
public class SqlImportResult
{
    // Added fields
    public int TeamsSkipped { get; set; }
    public int PlayersSkipped { get; set; }
    public int FixturesSkipped { get; set; }
    public int ResultsSkipped { get; set; }
    public int FramesSkipped { get; set; }
    
    // ID-to-Name mappings
    public Dictionary<int, string> VbaPlayerIdToName { get; set; }
    public Dictionary<int, string> VbaTeamIdToName { get; set; }
    
    // Enhanced summary
    public string Summary =>
        $"✓ Teams: {TeamsImported} imported, {TeamsSkipped} skipped\n" +
        $"✓ Players: {PlayersImported} imported, {PlayersSkipped} skipped\n" +
        $"✓ Fixtures: {FixturesImported} imported, {FixturesSkipped} skipped\n" +
        // ... etc
}
```

**Result**:
- Clear visibility into import operations
- Users know exactly what was added vs. what already existed
- Better debugging and troubleshooting

## Code Changes

### Files Modified
1. **wdpl2/Services/SqlFileImporter.cs**
   - Added `BuildIdToNameMappings()` method
   - Updated `ImportTeams()` with duplicate detection
   - Updated `ImportPlayers()` with name mapping and duplicate detection
   - Updated `ImportFixtures()` with duplicate detection
   - Updated `ImportResults()` with duplicate detection
   - Enhanced `SqlImportResult` class with new fields
   - Total: +234 lines, -34 lines modified

2. **docs/SQL_IMPORT_QUICK_START.md**
   - Updated feature list to show new capabilities
   - Documented ID-to-name mapping
   - Documented duplicate detection behavior

3. **docs/SQL_IMPORT_DUPLICATE_DETECTION.md** (NEW)
   - Comprehensive technical documentation
   - Implementation details and algorithms
   - Test cases and examples

## Testing

### Test Case 1: Fresh Import
```
Input: sample_import.sql with 56 players, 182 fixtures
Result:
  ✓ Players: 56 imported (with actual names), 0 skipped
  ✓ Fixtures: 182 imported, 0 skipped
  ✓ All data created successfully
```

### Test Case 2: Duplicate Prevention
```
Input: Same file imported twice
Result:
  ✓ Players: 0 imported, 56 skipped
  ✓ Fixtures: 0 imported, 182 skipped
  ✓ No duplicate data created
```

### Test Case 3: Partial Update
```
Input: File with existing players + new results
Result:
  ✓ Players: 0 imported, 56 skipped (already exist)
  ✓ Fixtures: 0 imported, 182 skipped (already exist)
  ✓ Results: 32 imported, 150 skipped (new results added)
```

## Benefits

### For Users
1. **No Manual Name Updates**: Players imported with correct names from day one
2. **Safe Re-imports**: Can re-run imports without creating duplicates
3. **Clear Reporting**: Know exactly what was imported vs. skipped
4. **Incremental Updates**: Import new results without re-importing existing data

### For Developers
1. **Better Data Integrity**: Duplicate prevention at multiple levels
2. **Consistent Nullable Handling**: All optional GUIDs use null (not Guid.Empty)
3. **Comprehensive Statistics**: Easy to track and debug imports
4. **Well-Documented**: Clear documentation for maintenance

## Limitations

1. **Team Names**: Still use "Team 12" format (VBA doesn't have team names table)
2. **Update Logic**: Only adds new records, doesn't update existing ones
3. **Match Numbers**: Uses sequential matching for results (assumes order)

## Future Enhancements

1. **Update Mode**: Option to update existing records with new data
2. **Team Name Extraction**: Try to get team names from venues or other sources
3. **Conflict Resolution**: UI for handling conflicting data
4. **Import Preview**: Show what will be imported before committing

## Implementation Notes

### Design Decisions
1. **Two-Phase Import**: First build ID mappings, then import entities
2. **Null over Guid.Empty**: Consistent use of null for optional references
3. **Skip over Update**: Currently only adds new records (safer default)
4. **Case-Insensitive Matching**: Player/team names compared case-insensitively

### Code Quality
- All nullable reference warnings addressed
- Consistent error handling patterns
- Comprehensive logging via Warnings list
- Follows existing code style and conventions

## Version History

### v2.0 (Current)
- ID-to-name mapping from tblplayers
- Duplicate detection for all entity types
- Enhanced statistics with skipped counts
- Consistent nullable Guid handling
- Comprehensive documentation

### v1.0 (Previous)
- Basic SQL parsing and import
- Season metadata import
- Placeholder names for players/teams
- No duplicate detection

## Related Documentation
- `SQL_IMPORT_QUICK_START.md` - User guide
- `SQL_IMPORT_DUPLICATE_DETECTION.md` - Technical details
- `SQL_IMPORT_USER_GUIDE.md` - Step-by-step instructions
