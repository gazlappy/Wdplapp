# ?? Quick Access Guide - SQL Import

## How to Access SQL Import

The SQL Import feature is now accessible in your app!

### Navigation Path

```
Import Tab ? SQL Import
```

### Step-by-Step

1. **Launch the app**

2. **Click on the "Import" tab** (in the main navigation bar)

3. **You'll see two options:**
   - **Historical Import** - Multi-format wizard (Word, HTML, etc.)
   - **SQL Import** ? Click this one!

4. **SQL Import page opens** with:
   - File selection button
   - Import options (merge, mark, create season)
   - Import button
   - Results display

---

## Quick Test

### Test with Sample SQL

1. **Create a test file** `test.sql`:
```sql
INSERT INTO teams (id, name) VALUES 
(1, 'Test Team A'),
(2, 'Test Team B');

INSERT INTO players (id, first_name, last_name, team_id) VALUES
(1, 'John', 'Smith', 1),
(2, 'Jane', 'Doe', 1);
```

2. **Import the file:**
   - Import Tab ? SQL Import
   - Select `test.sql`
   - Click Import
   - See results!

---

## Available Import Methods

From the **Import Tab**, you can choose:

### ??? SQL Import
- MySQL dumps
- PostgreSQL dumps
- SQLite exports
- SQL Server scripts
- **Best for:** Complete database migrations

### ?? Historical Import
- Access databases (.accdb, .mdb)
- Word documents (.docx, .doc)
- HTML files (.html)
- Batch imports
- **Best for:** Mixed format imports

---

## Navigation Structure

```
Main App
??? Import Tab
    ??? Historical Import (default)
    ?   ??? Access Database
    ?   ??? Word Document
    ?   ??? Excel/CSV
    ?   ??? HTML Files
    ?   ??? Images (OCR)
    ?
    ??? SQL Import (NEW!)
        ??? Select SQL File
        ??? Configure Options
        ??? Import
```

---

## Shortcut Tips

### For SQL Imports
**Quick Path:** Import ? SQL Import

### For Access Databases
**Quick Path:** Import ? Historical Import ? Access Database

### For Documents
**Quick Path:** Import ? Historical Import ? Word/HTML

---

## What You Should See

### Import Tab
```
???????????????????????????????????????
?  Import                             ?
???????????????????????????????????????
?  ? Historical Import                ?
?  ? SQL Import          ? NEW!       ?
???????????????????????????????????????
```

### SQL Import Page
```
???????????????????????????????????????
?  ??? Import SQL Dump File            ?
???????????????????????????????????????
?  Import historical league data from ?
?  SQL dump files (.sql)              ?
?                                     ?
?  ?? Supported SQL Formats:          ?
?  ? MySQL dump files (mysqldump)    ?
?  ? PostgreSQL dump files (pg_dump) ?
?  ? SQLite dump files (.sql)        ?
?  ? SQL Server export files (.sql)  ?
?                                     ?
?  [?? Select SQL File]               ?
?                                     ?
?  Import Options:                    ?
?  ? Merge with existing data         ?
?  ? Mark imported records            ?
?  ? Create new season                ?
?                                     ?
?  [?? Import SQL Data]               ?
???????????????????????????????????????
```

---

## Troubleshooting

### "I don't see SQL Import"

**Check:**
1. Make sure you're in the **Import** tab
2. Look for **two options**: Historical Import and SQL Import
3. Click on "SQL Import" (second option)

### "Import tab only shows one option"

**Solution:**
- Restart the app
- The build should have included both import pages
- Check app was deployed correctly

### "Can't find Import tab"

**Solution:**
The Import tab should be visible in the main tab bar:
- Seasons
- Divisions
- Teams
- Players
- Venues
- Fixtures
- Competitions
- Tables
- Analytics
- **Import** ? Look for this
- Website
- Settings

---

## Export from Your Database

### MySQL
```bash
mysqldump -u user -p database_name > export.sql
```

### PostgreSQL
```bash
pg_dump database_name > export.sql
```

### SQLite
```bash
sqlite3 database.db .dump > export.sql
```

### SQL Server
```
Right-click database ? Tasks ? Generate Scripts
? Choose data to export ? Save to file
```

---

## Quick Import Workflow

```
1. Export from old database
   ?? Create .sql file

2. Open app ? Import tab
   ?? Click "SQL Import"

3. Select your .sql file
   ?? Click "Select SQL File"

4. Configure options
   ?? ? Merge (avoid duplicates)
   ?? ? Mark as [IMPORTED]
   ?? ? Create new season

5. Import!
   ?? Click "Import SQL Data"

6. Review results
   ?? Check import summary
```

---

## Example Use Cases

### Case 1: Old MySQL Website
**Scenario:** Migrating from old PHP/MySQL site

**Steps:**
1. `mysqldump -u user -p league > export.sql`
2. Import ? SQL Import
3. Select `export.sql`
4. Import!

**Time:** 2 minutes for 10,000 records

---

### Case 2: PostgreSQL Server
**Scenario:** Moving from PostgreSQL to local database

**Steps:**
1. `pg_dump league_db > export.sql`
2. Import ? SQL Import
3. Select `export.sql`
4. Import!

**Time:** 1 minute for 5,000 records

---

### Case 3: Multiple Seasons
**Scenario:** Have SQL files for different years

**Steps:**
1. Import ? SQL Import
2. Import season2020.sql
3. Import season2021.sql
4. Import season2022.sql
5. Each creates a separate season!

**Time:** 5 minutes for 3 seasons

---

## Need More Help?

### Documentation
- `HISTORICAL_DATA_IMPORT_GUIDE.md` - Complete guide
- `SQL_IMPORT_QUICK_REFERENCE.md` - SQL commands
- `DATA_IMPORT_COMPARISON.md` - Choose import method

### In-App Help
- Look for ?? icons on import pages
- Tooltips explain each option
- Error messages are detailed

---

## Status

? **SQL Import is LIVE!**
- Accessible from Import tab
- Ready to use
- All SQL dialects supported
- Cross-platform compatible

**Start importing your historical data today!** ??

---

**Last Updated:** 2025
**Build:** Successful
**Feature Status:** Active
