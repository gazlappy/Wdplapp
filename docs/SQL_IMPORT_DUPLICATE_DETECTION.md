# SQL Import: ID-to-Name Mapping & Duplicate Detection

## Overview

The SQL importer now intelligently maps VBA database IDs to actual player/team names and detects duplicate records to prevent re-importing existing data.

## Features Implemented

### 1. ID-to-Name Mapping

**What it does:**
- Parses `tblplayers` table to build PlayerID → PlayerName dictionary
- Uses actual names like "TONY HARTNELL" instead of "Player 1"
- Preserves team associations from the `Team` field in tblplayers

**How it works:**
```
Step 1: Parse tblplayers table first
   PlayerID=1 → "TONY HARTNELL"
   PlayerID=2 → "PETE BROWN"
   ...

Step 2: Use mappings when importing other tables
   When processing tblmatchdetail:
   - Player1=1 → Look up "TONY HARTNELL" → Create/Link player
   - Player2=2 → Look up "PETE BROWN" → Create/Link player
```

**Benefits:**
- No more manual name updates for players
- Proper player identification across imports
- Team associations maintained automatically

### 2. Duplicate Detection

**What it does:**
- Checks existing database before importing each record
- Skips records that already exist
- Tracks detailed statistics (imported vs. skipped)

**Detection Logic:**

#### Players
```csharp
// Check by name OR PlayerID
var existing = existingData.Players.FirstOrDefault(p => 
    (p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase) ||
     p.Name.EndsWith($"({vbaPlayerId})")) &&
    p.SeasonId == seasonId);

if (existing != null) {
    // Skip - player already exists
    result.PlayersSkipped++;
}
```

#### Fixtures
```csharp
// Check by date + teams
var existing = existingData.Fixtures.FirstOrDefault(f =>
    f.SeasonId == seasonId &&
    f.Date.Date == matchDate.Date &&
    f.HomeTeamId == homeTeamId &&
    f.AwayTeamId == awayTeamId);

if (existing != null) {
    // Skip - fixture already exists
    result.FixturesSkipped++;
}
```

#### Results/Frames
```csharp
// Check if fixture already has frames
if (fixture.Frames.Count > 0) {
    // Skip - results already imported
    result.ResultsSkipped++;
}
```

### 3. Import Statistics

**Summary Output:**
```
✓ Season: Winter 2025
✓ Teams: 14 imported, 0 skipped
✓ Players: 56 imported, 0 skipped
✓ Fixtures: 182 imported, 0 skipped
✓ Results: 150 matches imported, 32 skipped
✓ Frames: 900 imported, 192 skipped
⚠ Warnings: 3
❌ Errors: 0
```

**Detailed Warnings:**
```
⚠️ Loaded 56 player names from tblplayers
⚠️ Players: 56 imported with names, 0 already exist
⚠️ Teams: 14 created, 0 already exist
⚠️ Fixtures: 182 imported, 0 already exist
⚠️ Results: 150 matches imported, 32 skipped; 900 frames imported, 192 skipped
```

## Usage Example

### First Import (Clean Database)
```
Input: sample_import.sql with 56 players
Result:
  ✓ Players: 56 imported, 0 skipped
  ✓ All players have actual names from tblplayers
```

### Second Import (Same File)
```
Input: sample_import.sql (same file again)
Result:
  ✓ Players: 0 imported, 56 skipped
  ✓ Fixtures: 0 imported, 182 skipped
  ✓ No duplicate data created!
```

### Partial Re-Import
```
Input: updated_results.sql (new match results only)
Result:
  ✓ Players: 0 imported, 56 skipped (already exist)
  ✓ Fixtures: 0 imported, 182 skipped (already exist)
  ✓ Results: 32 imported, 150 skipped (new results added)
```

## Technical Implementation

### Code Flow

1. **ParseSqlFileAsync()**
   - Reads SQL file
   - Parses INSERT statements
   - Returns table data as dictionaries

2. **BuildIdToNameMappings()**
   - Processes `tblplayers` table
   - Builds PlayerID → PlayerName mapping
   - Stores in SqlImportResult

3. **ImportPlayers()**
   - Iterates through player IDs
   - Looks up names from mapping
   - Checks for duplicates in existing data
   - Creates only new players
   - Tracks skipped count

4. **ImportFixtures()**
   - Checks each fixture against existing
   - Compares date + teams
   - Skips if already exists
   - Tracks skipped count

5. **ImportResults()**
   - Checks if fixture already has frames
   - Skips entire match if frames exist
   - Individual frame duplicate detection
   - Tracks skipped counts

### Data Structures

```csharp
public class SqlImportResult
{
    // ID-to-Name mappings
    public Dictionary<int, string> VbaPlayerIdToName { get; set; }
    public Dictionary<int, string> VbaTeamIdToName { get; set; }
    
    // Import statistics
    public int PlayersImported { get; set; }
    public int PlayersSkipped { get; set; }
    public int FixturesImported { get; set; }
    public int FixturesSkipped { get; set; }
    public int ResultsImported { get; set; }
    public int ResultsSkipped { get; set; }
    public int FramesImported { get; set; }
    public int FramesSkipped { get; set; }
}
```

## Testing

### Test Case 1: Fresh Import
```
1. Start with empty database
2. Import sample_import.sql
3. Verify:
   - All 56 players have actual names
   - Team associations correct
   - No skipped records (all new)
```

### Test Case 2: Duplicate Prevention
```
1. Import sample_import.sql
2. Import same file again
3. Verify:
   - 0 new records created
   - All records skipped
   - No duplicate data in database
```

### Test Case 3: Partial Update
```
1. Import sample_import.sql (base data)
2. Add new results to SQL file
3. Import updated file
4. Verify:
   - Existing players/fixtures skipped
   - Only new results imported
```

## Benefits

1. **Prevents Data Duplication**
   - Safe to re-run imports
   - No duplicate players/fixtures/results
   - Database stays clean

2. **Better User Experience**
   - Clear import statistics
   - Know exactly what was added vs. skipped
   - No manual cleanup needed

3. **Proper Name Resolution**
   - Real player names from day one
   - No "Player 1, Player 2" placeholders
   - Team associations maintained

4. **Incremental Updates**
   - Import new results without re-importing old data
   - Update in stages (players first, then fixtures, then results)
   - Flexible import workflow

## Limitations

1. **Team Names**
   - Still use "Team 12" format (VBA doesn't have team names table)
   - Must be updated manually after import

2. **Update Logic**
   - Currently only adds new records
   - Doesn't update existing records with new data
   - Future enhancement: optional update mode

3. **Matching Logic**
   - Players matched by name (case-insensitive) or ID
   - Fixtures matched by date + teams (date comparison by day)
   - May need adjustment for specific use cases

## Future Enhancements

1. **Update Mode**
   - Option to update existing records with new data
   - Merge changes instead of skipping

2. **Conflict Resolution**
   - UI to handle conflicting data
   - Choose which version to keep

3. **Import Preview**
   - Show what will be imported/skipped before committing
   - Review changes before applying

4. **Team Name Extraction**
   - Try to extract team names from other sources
   - Venue names, player home teams, etc.
