# SQL Import Quick Fix Guide

## ?? If You Just Imported and See 100+ Players

### **ROLLBACK IMMEDIATELY!**

1. **Stay on the SQL Import page** (don't navigate away)
2. **Click the red "?? Rollback" button**
3. **Confirm rollback** when prompted
4. **All imported data will be removed** (season, teams, players, fixtures)
5. **You can now re-import correctly**

---

## ? How to Import Correctly

### Step 1: Import Structure Only
- Select your SQL file
- ?? Check "Import as inactive season"
- Click "Import SQL Data"

### Step 2: Verify Results
**Look for this:**
```
Season: Wellington & District Pool League Winter 2025
Divisions: 1
Teams: 14
Players: 0        ? MUST BE ZERO!
Fixtures: 182
Frames: 0
```

### Step 3: If Players > 0
- **STOP!**
- Click "?? Rollback"
- Something went wrong
- Contact support or check guide

### Step 4: Add Real Players
- Go to Seasons ? [Your Season] ? Teams
- For each team, click "Edit Team"
- Add real player names
- Save roster

---

## ?? What Should Happen

### ? Correct Import
- Season created
- 1 Division created (e.g., "United")
- 14 Teams created (Team 1, Team 2, etc.)
- 0 Players (you add these manually)
- 182 Fixtures scheduled
- 0 Frames (will be populated when you add results)

### ? Bad Import (OLD BEHAVIOR)
- Season created
- 1 Division created
- 14 Teams created
- **126 Players created** ? WRONG!
- 182 Fixtures scheduled
- 2730 Frames with wrong player data

---

## ?? What Was Fixed

**Before:**
- SQL import created "Player 1", "Player 2", etc. automatically
- Hundreds of unnamed placeholder players
- Frame results linked to wrong players

**After:**
- NO players auto-created
- You must add real player names manually
- Frame results will link when you add players

---

## ? Quick Commands

| If You See... | Do This |
|--------------|---------|
| Players: 0 | ? Perfect! Continue to add players |
| Players: 100+ | ? Click Rollback NOW! |
| Can't find Rollback button | Import didn't complete, try again |
| Rollback button is grayed | No recent import to rollback |

---

## ?? Still Having Issues?

1. **Check the warnings log** on SQL Import page
2. **Read the full guide:** `SQL_IMPORT_ROLLBACK_GUIDE.md`
3. **Verify your SQL file** contains correct tables
4. **Try the sample file** from documentation

---

## ?? Expected Workflow

```
1. Import SQL ? Get structure (teams, fixtures)
   ?? Verify: Players = 0 ?

2. Add Players ? Go to each team, add roster
   ?? Use real names ?

3. Enter Results ? Match results link to real players
   ?? Ratings calculate correctly ?
```

---

## ?? Important Notes

- **Players are NOT auto-created from SQL anymore**
- **This is intentional and correct**
- **Rollback removes ALL imported data** (not just players)
- **You can re-import the same SQL file multiple times**
- **Keep your original SQL file** for reference

---

## ?? Emergency Rollback

If something looks wrong:

1. **Don't panic**
2. **Don't edit anything**
3. **Click Rollback**
4. **Everything goes back to before import**
5. **Try again following this guide**

The rollback feature makes it safe to experiment!
