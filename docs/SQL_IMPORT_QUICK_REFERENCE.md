# ??? SQL Import - Quick Reference

## Fast Import (TL;DR)

1. **Export from phpMyAdmin:**
   - Select all `tbl*` tables
   - Format: SQL
   - Save as `.sql`

2. **Import to MAUI App:**
   - Navigate: Import ? SQL Import
   - Click: "?? Select SQL File"
   - Click: "?? Import SQL Data"
   - Done! ?

---

## ?? What Gets Imported

| VBA Table | Creates | Count (sample) |
|-----------|---------|----------------|
| **tblleague** | Season + Settings | 1 |
| **tbldivisions** | Divisions | 1 |
| **tblfixtures** | Fixtures | 182 |
| **tblmatchdetail** | Frame Results | 2730 |
| *Auto-created* | Teams | 14 |
| *Auto-created* | Players | 127 |

---

## ?? Import Options

**? Replace existing season data**
- OFF (default) = Merge new data
- ON = Replace older data

**? Import as inactive season** (recommended)
- OFF = Activate imported season
- ON = Keep as inactive (safer)

---

## ?? Example Results

```
? Import Successful!

Season: Wellington & District Pool League Winter 2025
Divisions: 1
Teams: 14
Players: 127
Fixtures: 182
Frames: 2730

?? Warnings: 3
- Division 'United' already exists
- Player names need updating
- Team names are placeholders
```

---

## ?? Post-Import Checklist

**1. Update Team Names** (Teams page)
- "Team 1" ? Real name
- Check notes for VBA ID

**2. Update Player Names** (Players page)
- "Player 93" ? Real name
- Assign to teams

**3. Verify Data** (Fixtures page)
- Check match dates
- Verify scores

**4. Calculate Ratings** (League Tables page)
- Click "Recalculate Ratings"
- Verify accuracy

---

## ?? Common Issues

**"No tblleague found"**
? Re-export with tblleague included

**"Team X not found"**
? Normal - teams auto-created as placeholders

**"Import shows warnings"**
? Review log - usually non-critical

---

## ?? Quick Tips

? **Always include tblleague** in export  
? **Test with small data first**  
? **Backup before import**  
? **Review import log**  
? **Update placeholders after import**  

---

## ?? Full Documentation

For detailed guide, see:
- [SQL Import Comprehensive Guide](SQL_IMPORT_COMPREHENSIVE_GUIDE.md)
- [SQL Import Implementation](SQL_IMPORT_IMPLEMENTATION.md)
- [Data Import Comparison](DATA_IMPORT_COMPARISON.md)

---

## ?? VBA Table Structure

**Core Tables (Required):**
```sql
tblleague       -- Season info + settings
tbldivisions    -- Division list
tblfixtures     -- Match schedule
```

**Data Tables (Optional):**
```sql
tblmatchheader  -- Match metadata
tblmatchdetail  -- Frame results (? important!)
tblmatchfooter  -- Match scores
tblmatchdates   -- Match dates
tblcompdates    -- Competition dates
```

---

## ?? VBA ID Mapping

All VBA IDs preserved in `Notes` field:

```
Division ? "Imported from VBA (ID: 1)"
Team     ? "Imported from VBA (ID: 5)"
Player   ? "Imported from VBA (ID: 93)"
```

Use these to map back to real names!

---

## ? Performance

| Records | Time |
|---------|------|
| 100 fixtures | ~2 sec |
| 200 fixtures | ~5 sec |
| 500 fixtures | ~15 sec |

---

## ?? Need Help?

1. Check import log for errors
2. Review SQL file has all tables
3. See comprehensive guide
4. Verify VBA IDs in notes

---

*Last Updated: January 2025*
*WDPL2 .NET MAUI Application*
