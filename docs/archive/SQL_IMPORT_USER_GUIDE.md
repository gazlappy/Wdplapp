# Quick Start: SQL Import Guide

## What This Does
Import your existing VBA Access league data (exported via phpMyAdmin) into Wdpl2 app.

## Before You Start

### Export from VBA Access
1. Open phpMyAdmin
2. Select your WDPL database
3. Click "Export"
4. Choose "SQL" format
5. Select these tables minimum:
   - `tblleague`
   - `tbldivisions`
   - `tblfixtures`
   - `tblmatchdetail` (or `tblplayerresult`)
6. Click "Go" to download the `.sql` file

## Import Steps

### 1. Open SQL Import
- Launch Wdpl2 app
- Navigate to: **Menu ? SQL Import**

### 2. Select File
- Click "?? Select SQL File"
- Choose your exported `.sql` file
- File info appears below button

### 3. Preview Data
- Click "??? Preview Data"
- **Review the preview carefully:**
  - SQL Dialect
  - Tables found
  - Row counts
  - Expected imports

**IMPORTANT**: Preview shows placeholder names like "Team 1" and "Player 93" - these will need manual updating after import!

### 4. Import
- Click "? Confirm Import"
- Read confirmation dialog
- Click "Import Now"
- Wait for completion

### 5. Review Results
**Success Panel shows:**
- Teams imported
- Players imported
- Fixtures imported
- Frames imported

**Warnings/Errors Panel shows:**
- Any issues encountered
- Skipped data
- Reference problems

### 6. Update Names (CRITICAL)
After import, immediately:

1. **Go to Seasons Page**
2. **Select your imported season**
3. **Edit each team:**
   - Change "Team 1" ? Real name
   - Repeat for all teams
4. **Edit each player:**
   - Change "Player 93" ? Real name
   - Repeat for all players
5. **Activate the season** when names are correct

## If Something Goes Wrong

### Use Rollback
- Click "?? Rollback" button
- Confirms removal of ALL imported data
- Returns to clean state
- You can try importing again

### Common Issues

#### "No season detected"
- Your SQL file doesn't have `tblleague` table
- Export again with `tblleague` included

#### "No match detail data found"
- Missing `tblmatchdetail` AND `tblplayerresult`
- Export again including match result tables

#### "Skipping fixture with unknown team IDs"
- Team IDs in fixtures don't match any teams
- Check your SQL export includes all data

#### High warning count
- Usually just informational
- Check the warnings panel for details
- Most warnings are safe to ignore

## Example Timeline

### Typical Import
1. **Select file**: 5 seconds
2. **Preview**: 10 seconds (parses file)
3. **Review preview**: 30 seconds (read carefully!)
4. **Import**: 10-30 seconds (depends on data size)
5. **Update names**: 10-30 minutes (manual work)

### For a 14-team, 26-week season:
- **182 fixtures** = ~3 minutes to import
- **28 players** × 2 teams = ~15 minutes to rename
- **Total time**: ~20 minutes

## What Gets Imported

### ? Automatically
- Season info
- Divisions
- Fixtures schedule
- Match results
- Frame-by-frame scores
- 8-ball markers
- Dates and times

### ?? Needs Manual Work
- Team names (created as "Team 1", "Team 2")
- Player names (created as "Player 93", "Player 44")

### ? Not Imported
- Player phone numbers
- Team contact info
- Venue details
- Historical ratings (must recalculate)
- Competition results

## Tips for Success

### Before Export
- ? Backup your VBA database
- ? Verify data is complete
- ? Note down team/player names

### During Preview
- ? Check table counts match expected
- ? Verify dates look correct
- ? Note any warnings

### After Import
- ? Update ALL team names first
- ? Update ALL player names next
- ? Test one fixture to verify data
- ? Activate season last

### If Unsure
- ? Use Preview (doesn't change data)
- ? Try with test data first
- ? Keep your SQL file as backup
- ? Use Rollback if needed

## Support Data Format

### Minimum Required SQL
```sql
-- Season
INSERT INTO `tblleague` (...) VALUES (...);

-- Fixtures
INSERT INTO `tblfixtures` (...) VALUES (...);

-- Results
INSERT INTO `tblmatchdetail` (...) VALUES (...);
```

### Complete Export (Recommended)
```sql
-- All these tables for full import:
INSERT INTO `tblleague` ...;
INSERT INTO `tbldivisions` ...;
INSERT INTO `tblfixtures` ...;
INSERT INTO `tblmatchheader` ...;
INSERT INTO `tblmatchdetail` ...;
INSERT INTO `tblmatchfooter` ...;
```

## Troubleshooting Checklist

- [ ] Exported ALL relevant tables?
- [ ] File size > 0KB?
- [ ] File ends with `.sql`?
- [ ] Preview shows expected table counts?
- [ ] Reviewed all warnings?
- [ ] Updated ALL team names after import?
- [ ] Updated ALL player names after import?
- [ ] Activated season?

## Quick Reference Commands

| Action | Button | When to Use |
|--------|--------|-------------|
| Select File | ?? | Start of process |
| Preview | ??? | Before importing |
| Import | ? | After reviewing preview |
| Rollback | ?? | If import wrong |

## Success Indicators

? **Good Import:**
- Preview shows expected counts
- No error messages (only warnings OK)
- Results summary shows positive numbers
- Team/player lists appear in app

? **Problem Import:**
- Error messages in red
- Zero imports shown
- Preview shows empty tables
- App crashes/freezes

## Need Help?

1. Check preview carefully
2. Read warning messages
3. Try with smaller test export
4. Use rollback and retry
5. Check your SQL file in text editor

---

**Remember**: The import creates placeholder names. You MUST update team and player names manually after import!
