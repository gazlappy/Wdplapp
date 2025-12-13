# SQL Import Rollback Guide

## Issue Fixed
The SQL import was auto-creating hundreds of placeholder players based on VBA player IDs found in frame results data. This has been fixed.

## Changes Made

### 1. Player Auto-Creation Disabled
- **Before**: Import created "Player 1", "Player 2", etc. for every VBA player ID
- **After**: Players are NOT auto-created from frame data
- Frame results will have `null` player IDs if players don't exist
- You must create/import actual players separately

### 2. Rollback Feature Added
New rollback functionality allows you to undo an import:

**Tracks:**
- Season IDs created
- Division IDs created
- Team IDs created  
- Player IDs created (should be 0 now)
- Fixture IDs created

**Rollback Button:**
- Appears after successful import
- Removes ALL imported data
- Cannot be undone (but you can re-import)

### 3. Import Warnings
The import now warns you:
- "? Players must be created separately (not auto-created)"
- "?? Note: X players were imported" - should be 0
- "You can use the Rollback button to undo this import"

## How to Use SQL Import Properly

### Step 1: Review the SQL File
Make sure it contains:
- `tblleague` - Season definition
- `tbldivisions` - Division configuration
- `tblfixtures` - Fixture schedule
- Optional: `tblmatchheader`, `tblmatchdetail`, `tblmatchfooter`

### Step 2: Import Without Players
1. Go to SQL Import page
2. Select your SQL file
3. Check "Import as inactive season"
4. Click "Import SQL Data"
5. **Verify: "Players: 0" in results**
6. If players > 0, click "Rollback" immediately

### Step 3: Create Players Manually
After importing fixtures:
1. Go to the imported season
2. Select each team
3. Add players manually with correct names
4. Save team rosters

### Step 4: Re-Import with Player Mapping (Future)
Once players exist:
- Re-import the same SQL file
- Frame results will now map to real players
- Or manually enter results

## Quick Fix: Rollback Bad Import

If you accidentally imported and created hundreds of players:

```
1. Stay on SQL Import page
2. Click "?? Rollback" button
3. Confirm rollback
4. All imported data is removed
5. Re-import following proper steps
```

## Sample SQL Import Result

**Correct Import (No Players):**
```
Season: Wellington & District Pool League Winter 2025
Divisions: 1
Venues: 0
Teams: 14
Players: 0        ? Should be 0!
Fixtures: 182
Frames: 0         ? Will be populated when you add results
```

**Bad Import (Auto-Created Players):**
```
Season: Wellington & District Pool League Winter 2025
Divisions: 1
Venues: 0
Teams: 14
Players: 126      ? Wrong! Use rollback!
Fixtures: 182
Frames: 2730
```

## Rollback Confirmation

When you click Rollback, you'll see:
```
This will remove all data imported in the last operation:

• 1 Seasons
• 1 Divisions
• 14 Teams
• 126 Players    ? These shouldn't exist
• 182 Fixtures

This cannot be undone!
```

## Technical Details

### What Was Changed

**SqlFileImporter.cs:**
- Added `ImportedSeasonIds`, `ImportedDivisionIds`, etc. to track imports
- Added `RollbackImport()` method
- Modified `ImportFrames()` to NOT create players
- Players only mapped if they already exist in database

**SqlImportPage.xaml.cs:**
- Added rollback button (initially hidden)
- Shows rollback button after import
- Tracks `_lastImportResult` for rollback
- Added `OnRollbackClicked()` handler

### Frame Results Without Players

Frame results are still imported but with `null` player IDs:
```csharp
var frame = new FrameResult
{
    Number = frameNo,
    HomePlayerId = null,  // No player auto-created
    AwayPlayerId = null,  // No player auto-created
    Winner = FrameWinner.Home,
    EightBall = false
};
```

This means:
- Fixtures are created correctly
- Frame counts are tracked
- Winners are recorded
- But player stats are not calculated until players exist

## Best Practice Workflow

1. **Import Structure First**
   - Run SQL import
   - Get season, divisions, teams, fixtures
   - Verify 0 players created

2. **Add Players**
   - Go to each team
   - Add real player names
   - Save rosters

3. **Enter Results**
   - Either re-import SQL with player mapping
   - Or manually enter match results
   - Player stats will calculate correctly

4. **Keep SQL File**
   - Save original SQL export
   - Can re-import if needed
   - Rollback removes everything cleanly

## Troubleshooting

**Q: Import shows 100+ players?**
A: Click Rollback immediately! Re-import after reading this guide.

**Q: Can I keep the auto-created players?**
A: No, they're just numbered placeholders (Player 1, Player 2, etc.) with no real names. Rollback and add proper player names.

**Q: Will rollback delete my real players?**
A: Only if they were created in the same import. Existing players before import are safe.

**Q: What happens to frame results after rollback?**
A: Everything is removed - frames, fixtures, teams, divisions, season. You can re-import the SQL file.

**Q: Can I rollback twice?**
A: Only the most recent import. Once you rollback, that import is forgotten.

## Future Improvements

Planned enhancements:
- Import players from separate VBA player table
- Map VBA player IDs to existing players by name
- Preview import before committing
- Multi-step import wizard
- Player name extraction from VBA database
