# SQL Import Quick Start Guide

## Overview
The SQL Import feature now properly handles phpMyAdmin and other SQL dump files, including all MySQL-specific syntax, comments, and data types.

## How to Use

### Step 1: Prepare Your SQL File
1. Export your data from phpMyAdmin, MySQL, PostgreSQL, or SQLite
2. Save the `.sql` file to a location you can access from the app
3. The file should contain your league data tables (tblleague, tbldivisions, etc.)

### Step 2: Import via SQL Import Page

1. **Open SQL Import**
   - Navigate to the "SQL Import" page from the main menu

2. **Select Your File**
   - Click "?? Select SQL File (*.sql)"
   - Choose your SQL dump file
   - You'll see: "File selected. Click 'Import Data' to proceed."

3. **Review Options** (Optional)
   - ? Replace existing season data (if newer)
   - ? Import as inactive season (recommended for first import)

4. **Start Import**
   - Click "?? Import SQL Data"
   - Confirm the import
   - Watch the progress

5. **Review Results**
   - ? Success message with import summary
   - Season information detected
   - Teams, Players, Fixtures imported
   - Warnings (if any) shown in scrollable list

6. **Rollback if Needed**
   - Click "?? Rollback" button if something went wrong
   - All imported data will be removed

## What Gets Imported

### ? Fully Supported
- **Season Information** (from tblleague)
  - Season name and year
  - Start/end dates
  - League settings

- **Player Names** (from tblplayers)
  - PlayerID → PlayerName mapping
  - Actual player names used instead of placeholders
  - Team associations preserved

- **Duplicate Detection**
  - Automatically skips existing players (by name or ID)
  - Skips duplicate fixtures (same date + teams)
  - Skips duplicate results and frames
  - Shows detailed import vs. skipped statistics
  
### ?? Implemented
- **Divisions** (from tbldivisions)
- **Teams** (extracted from fixtures and players)
- **Players** (with actual names from tblplayers)
- **Fixtures** (from tblfixtures)
- **Results** (from tblmatchdetail/tblplayerresult)

## SQL File Requirements

### Required Tables
```sql
-- Minimum required table
tblleague -- Contains season and league information
```

### Recommended Tables
```sql
tblleague      -- Season/league configuration
tbldivisions   -- League divisions
tblteams       -- Teams (if exported with teams table name)
tblplayers     -- Players (if exported)
tblfixtures    -- Fixture schedule
tblmatchheader -- Match metadata
tblmatchdetail -- Frame results
tblmatchfooter -- Match scores
```

## Supported SQL Dialects

? **MySQL/phpMyAdmin**
- Full support for phpMyAdmin exports
- Handles `ENGINE=`, `CHARSET=`, `COLLATE=`
- Handles `/*!40101 ... */` commands
- Converts `b'0'` and `b'1'` bit values

? **PostgreSQL**
- Standard INSERT statements
- NULL value handling

? **SQLite**
- Native support

? **SQL Server**
- Standard INSERT statements

## Common Issues & Solutions

### Issue: "No INSERT statements found"
**Solution:** Make sure your SQL file contains actual data (INSERT INTO statements), not just schema (CREATE TABLE)

### Issue: "Hundreds of warnings"
**Solution:** This is normal for large imports. The first 10 warnings are shown in the UI. Most warnings are informational (e.g., "Column/value count mismatch" for optional fields)

### Issue: "Auto-imports when I select file"
**Solution:** This has been fixed. The file now only imports when you click "Import Data"

### Issue: "Season detected but no teams/players"
**Solution:** Full team/player import is still being implemented. Currently only season metadata is imported.

## Example SQL File

Your SQL file should look like this:

```sql
-- phpMyAdmin SQL Dump
-- version 5.2.1

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;

INSERT INTO `tblleague` (`ID`, `SeasonName`, `SeasonYear`, `FirstMatchDate`) VALUES
(1, 'Winter', 2025, '2025-09-18 00:00:00');

INSERT INTO `tblfixtures` (`FixtureID`, `WeekNo`, `HomeTeam`, `AwayTeam`) VALUES
(1, 1, 1, 2),
(2, 1, 3, 4);

-- ... more data ...
```

## Testing Your Import

1. **Start with small test file** (~50 lines)
2. **Check import summary** - verify detected season
3. **Review warnings** - note any issues
4. **Test rollback** - make sure you can undo
5. **Import full file** once testing is successful

## Need Help?

Check the following documentation:
- `SQL_IMPORT_PARSER_FIX_SUMMARY.md` - Technical details
- `SQL_IMPORT_IMPLEMENTATION.md` - Implementation guide
- `SQL_IMPORT_ACCESS_GUIDE.md` - Access database conversion

## New Features (v2.0)

? **ID-to-Name Mapping**
- Parses `tblplayers` table to extract PlayerID → PlayerName mappings
- Uses actual player names (e.g., "TONY HARTNELL") instead of "Player 1"
- Team associations maintained from tblplayers Team field

? **Smart Duplicate Detection**
- Checks existing database before importing
- Skips players already present (by name or PlayerID)
- Skips duplicate fixtures (same date + teams)
- Skips duplicate results/frames
- Detailed statistics: "X imported, Y skipped"

? **Comprehensive Import Summary**
- Shows records imported vs. skipped for each type
- Teams: imported/skipped counts
- Players: imported/skipped with name resolution status
- Fixtures: imported/skipped counts
- Results & Frames: imported/skipped counts

## Current Limitations

?? **Important Notes:**
1. Team names are still placeholders ("Team 12") - update manually after import
2. Rollback removes ALL data from the import session
3. VBA system doesn't have explicit team names table - extracted from player data

## Future Enhancements

Possible improvements:
- ? Team name extraction from venue or other sources
- ? Progress bar for large files
- ? Batch import of multiple SQL files
- ? Update existing records option (currently only adds new)
