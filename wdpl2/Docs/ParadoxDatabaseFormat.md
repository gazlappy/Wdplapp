# Paradox Database Format Analysis

## Overview
This document summarizes the deep analysis of Paradox 7.x .DB files from the legacy Delphi-based pool league application.

**Important:** This is a separate system from the VBA/Access database. The Paradox files are from an older Delphi application.

## File Location
Example database files: `wdpl2/another databsae example/`

## Key Findings from Deep Dive Analysis

### File Structure
- **Block Size**: 2048 bytes (0x800)
- **Header**: Block 0 (first 2048 bytes)
- **Data**: Starts at block 1 (offset 2048)
- **Block Header**: 6 bytes at start of each data block

### Header Layout (Block 0)
| Offset | Size | Field | Example Value |
|--------|------|-------|---------------|
| 0-1 | 2 | Record Size | Division: 41, Team: 230, Player: 70 |
| 2-3 | 2 | Header Size | Usually 8 (8 blocks for header) |
| 4 | 1 | File Type | 0 or 2 |
| 5 | 1 | Max Table Size | Usually 2 |
| 6-9 | 4 | Record Count | Varies |
| 33 | 1 | Field Count | Division: 3, Team: 20, Player: 10 |
| 78+ | N | Field Types | 1 byte per field |
| 78+N | N | Field Sizes | 1 byte per field |
| ~200+ | Var | Field Names | Null-terminated strings |

### Field Names (Found in Headers)

**Division.DB** (3 fields, 41-byte records, 2 records):
- `Item_id` - AutoInc/Long
- `Abbreviated` - Alpha (short name like "RED")  
- `FullDivisionName` - Alpha (full name like "RED DIVISION")

**Team.DB** (20 fields, 230-byte records, 16 teams):
- `Item_id` - AutoInc
- `TeamName` - Alpha ("POT BLACKS", "MOTLEY CREW")
- `Venue` - Long (FK to Venue.DB)
- `Division` - Long (FK to Division.DB)
- `Contact` - Alpha
- `ContactAddress1-4` - Alpha
- `Wins`, `Loses`, `Draws` - Short
- `SWins`, `SLosses` - Short (Singles)
- `DWins`, `DLosses` - Short (Doubles)
- `Points`, `Played` - Short
- `Withdrawn`, `RemoveResults` - Logical

**Player.DB** (10 fields, 70-byte records, 157 players):
- `PlayerNo` - AutoInc
- `PlayerName` - Alpha ("James May", "Matt Howell", "Chris Patten")
- `PlayerTeam` - Long (FK to Team.DB)
- `Played`, `Wins`, `Losses` - Short
- `CurrentRating`, `BestRating` - Short
- `BestRatingDate` - Date
- `EightBalls` - Short
- Note: "Void Frame" is a placeholder for empty player slots

**Match.DB** (9 fields, 66-byte records, 111 matches):
- `MatchNo` - AutoInc
- `HomeTeam`, `AwayTeam` - Long (FK to Team.DB)
- `MatchDate` - Date
- `HSWins`, `ASWins` - Short (Home/Away Singles Wins)
- `HDWins`, `ADWins` - Short (Home/Away Doubles Wins)
- `DivName` - Alpha

**Single.DB** (6 fields, 37-byte records, 1665 frames):
- `MatchNo` - Long (FK to Match.DB)
- `SingleNo` - Short (Frame number, 1-10)
- `HomePlayerNo`, `AwayPlayerNo` - Long (FK to Player.DB)
- `Winner` - Alpha ("Home" or "Away")
- `EightBall` - Logical

**Venue.DB** (6 fields, 154-byte records, 10 venues):
- `Item_id` - AutoInc
- `Venue` - Alpha ("THE WEAVERS ARMS", "THE BARLEY MOW")
- `AddressLine1-4` - Alpha

## Data Encoding

### Numeric Values (Short, Long, AutoInc)
- **Big-endian** byte order
- **High bit** indicates sign: 0x80 = positive, 0x00-0x7F = negative
- Positive: `((b[0] & 0x7F) << 24) | (b[1] << 16) | (b[2] << 8) | b[3]`
- Negative: XOR each byte with 0x7F/0xFF, negate result

### Date Values
- 4 bytes, big-endian
- High bit 0x80 indicates valid date
- Value is days since January 1, year 1
- Convert: `new DateTime(1, 1, 1).AddDays(days - 1)`

### String (Alpha) Values
- Null-terminated ASCII
- Padded with zeros to field size

### Logical (Boolean) Values
- 0x00 = null
- 0x80 = false
- 0x81 = true

## Sample Data Found

### Divisions
- RED DIVISION
- YELLOW DIVISION

### Teams (16 teams)
- POT BLACKS
- MOTLEY CREW
- (and 14 more)

### Players (157 players - excluding Void Frame placeholders)
- James May
- Matt Howell
- Chris Patten
- Liam Moore
- Ashley Pearce
- (and more)

### Venues (10 venues)
- THE WEAVERS ARMS
- R.G.W.M.C.
- THE BARLEY MOW
- (and more)

### Matches (111 fixtures)
- With frame-by-frame results in Single.DB

### Frame Results (1665 singles frames)
- Winner recorded as "Home" or "Away"
- EightBall flag for 8-ball finishes

## Parser Implementation

Location: `wdpl2/Services/ParadoxDatabaseParser.cs`

### Key Changes Based on Analysis
1. Data starts at offset 2048 (not calculated from header)
2. Block header is 6 bytes (not variable)
3. Field names are after offset 200 in header
4. "Void Frame" players are filtered out
5. Winner field contains "Home"/"Away" strings

### Usage
```csharp
var result = ParadoxDatabaseParser.ParseFolder(folderPath);
// result.Divisions, result.Teams, result.Players, etc.
```

## Troubleshooting

### No records found?
- Check if data block starts at offset 2048
- Verify record size from header matches actual data
- Use Deep Dive Analysis button to examine binary structure

### Missing player names?
- "Void Frame" entries are filtered as placeholders
- Check if player names are at expected offset in records

### Wrong dates?
- Dates use Paradox epoch (year 1), not Unix epoch
- Check for high bit 0x80 indicating valid date
