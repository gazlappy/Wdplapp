# SQL Import Fix Summary

## Problem
The SQL import was creating hundreds of placeholder players (Player 1, Player 2, etc.) from the `tblmatchdetail` frame results data, resulting in incorrect season data with 100+ unnamed players.

## Root Cause
The `ImportFrames()` method in `SqlFileImporter.cs` was auto-creating a new player for every unique VBA player ID it encountered in match frame data. Since frame results contain every player who ever played a frame, this created massive numbers of placeholder players.

## Solution Implemented

### 1. Disabled Player Auto-Creation
**File:** `wdpl2/Services/SqlFileImporter.cs`

**Changes:**
- Modified `ImportFrames()` to only MAP existing players, not create new ones
- Changed logic: `if (existing != null) { map it } else { warn, don't create }`
- Frame results now imported with `null` player IDs if players don't exist
- Added warning when player VBA IDs can't be mapped

**Code Before:**
```csharp
if (existing != null)
{
    playerIdMap[vbaId] = existing.Id;
}
else
{
    // Created new placeholder player - WRONG!
    var player = new Player { ... };
    data.Players.Add(player);
}
```

**Code After:**
```csharp
if (existing != null)
{
    playerIdMap[vbaId] = existing.Id;
}
else
{
    // Player not found - DON'T create, just warn
    result.Warnings.Add($"Player VBA ID {vbaId} not found");
}
```

### 2. Added Rollback Functionality
**File:** `wdpl2/Services/SqlFileImporter.cs`

**New Tracking Properties:**
```csharp
public List<Guid> ImportedSeasonIds { get; set; } = new();
public List<Guid> ImportedDivisionIds { get; set; } = new();
public List<Guid> ImportedTeamIds { get; set; } = new();
public List<Guid> ImportedPlayerIds { get; set; } = new();
public List<Guid> ImportedFixtureIds { get; set; } = new();
```

**New Method:**
```csharp
public static void RollbackImport(LeagueData data, SqlImportResult importResult)
{
    // Remove in reverse order of dependencies
    data.Fixtures.RemoveAll(f => importResult.ImportedFixtureIds.Contains(f.Id));
    data.Players.RemoveAll(p => importResult.ImportedPlayerIds.Contains(p.Id));
    data.Teams.RemoveAll(t => importResult.ImportedTeamIds.Contains(t.Id));
    data.Divisions.RemoveAll(d => importResult.ImportedDivisionIds.Contains(d.Id));
    data.Seasons.RemoveAll(s => importResult.ImportedSeasonIds.Contains(s.Id));
}
```

### 3. Updated UI for Rollback
**File:** `wdpl2/Views/SqlImportPage.xaml.cs`

**New Features:**
- Added `_lastImportResult` field to track last import
- Added "?? Rollback" button (hidden by default)
- Shows rollback button after successful import
- Rollback button removes all imported data
- Updated warning text to clarify player behavior

**New UI Elements:**
```csharp
var rollbackBtn = new Button
{
    Text = "?? Rollback",
    BackgroundColor = Color.FromArgb("#F44336"),
    TextColor = Colors.White,
    IsVisible = false,  // Only shown after import
    StyleId = "RollbackButton"
};
rollbackBtn.Clicked += OnRollbackClicked;
```

### 4. Updated Info Text
**File:** `wdpl2/Views/SqlImportPage.xaml.cs`

**Changed:**
```csharp
// Old:
"? Auto-creates teams and players"

// New:
"? Players must be created separately (not auto-created)"
```

## Expected Import Results

### Correct Import (After Fix)
```
Season: Wellington & District Pool League Winter 2025
Divisions: 1
Teams: 14
Players: 0        ? No players auto-created
Fixtures: 182     ? Fixtures imported
Frames: 0         ? Frames structure only (no player data)
```

### Previous Bad Import (Before Fix)
```
Season: Wellington & District Pool League Winter 2025
Divisions: 1
Teams: 14
Players: 126      ? Wrong! Auto-created from frame data
Fixtures: 182
Frames: 2730      ? All frames with wrong player mappings
```

## Usage Instructions

### To Fix Existing Bad Import:
1. Go to SQL Import page
2. Click "?? Rollback" button
3. Confirm rollback
4. All imported data removed cleanly

### To Import Correctly:
1. Select SQL file
2. Check "Import as inactive season"
3. Click "Import SQL Data"
4. **Verify "Players: 0" in results**
5. If not 0, click Rollback immediately
6. Manually add players to teams afterward

## Benefits of This Approach

### 1. Clean Imports
- No placeholder "Player 1", "Player 2" entries
- Season structure imported correctly
- Teams created properly

### 2. Correct Player Management
- Players added manually with real names
- Team rosters managed explicitly
- Player ratings calculated on real players only

### 3. Easy Recovery
- One-click rollback removes everything
- Can re-import same file multiple times
- No manual cleanup needed

### 4. Clear Workflow
1. Import structure (season, divisions, teams, fixtures)
2. Add real players to teams
3. Enter or re-import match results
4. Calculate ratings on real data

## Files Modified

| File | Changes |
|------|---------|
| `wdpl2/Services/SqlFileImporter.cs` | • Disabled player auto-creation<br>• Added rollback tracking<br>• Added `RollbackImport()` method<br>• Track all imported entity IDs |
| `wdpl2/Views/SqlImportPage.xaml.cs` | • Added rollback button<br>• Added `OnRollbackClicked()` handler<br>• Store last import result<br>• Updated warning messages |
| `docs/SQL_IMPORT_ROLLBACK_GUIDE.md` | • New user guide<br>• Usage instructions<br>• Troubleshooting |

## Testing

### To Test Rollback:
1. Import SQL file
2. Note imported counts (seasons, teams, etc.)
3. Click Rollback
4. Verify all counts return to previous state
5. Check Seasons page - imported season gone

### To Test No Player Creation:
1. Import SQL file
2. Check results: "Players: 0"
3. Check Seasons page - no unnamed players
4. Teams exist but have no roster entries

## Known Limitations

### Frame Results Without Players
- Frames imported but player IDs are null
- Match winners still recorded
- Player statistics not calculated until players exist
- Can re-import later to link players

### VBA Player ID Mapping
- Currently no automatic player name import
- Must manually match VBA IDs to player names
- Future: Could import from VBA player table

### Rollback Scope
- Only rolls back most recent import
- Can't rollback multiple imports
- Can't partial rollback (all or nothing)

## Future Enhancements

### Player Import Options:
1. Import players from VBA player table separately
2. Map VBA player IDs by name matching
3. Show player mapping preview before frame import
4. Multi-step wizard: structure ? players ? results

### Rollback Improvements:
1. Multiple rollback history
2. Selective rollback (e.g., just fixtures)
3. Rollback preview before committing
4. Undo/redo stack

## Migration Path for Existing Bad Imports

If you already have a bad import with 100+ players:

### Option 1: Rollback and Re-Import (Recommended)
```
1. SQL Import page ? Rollback
2. Re-import SQL file
3. Verify Players: 0
4. Manually add real players
```

### Option 2: Manual Cleanup (Not Recommended)
```
1. Go to each team
2. Remove all "Player N" entries
3. Add real player names
4. May still have orphaned frame data
```

### Option 3: Start Fresh
```
1. Create new season manually
2. Add divisions, teams, players
3. Don't re-import SQL (or import structure only)
```

## Conclusion

The SQL import now works correctly by:
- ? Importing season structure (divisions, teams, fixtures)
- ? NOT auto-creating placeholder players
- ? Providing easy rollback for bad imports
- ? Clear warnings and usage instructions
- ? Clean workflow: structure ? players ? results

Users must now explicitly add players to teams, which ensures:
- Real player names are used
- Team rosters are accurate
- Player ratings calculate correctly
- No cleanup of placeholder players needed
