# ?? Import Diagnostics - Quick Check

## How to Verify Import Data

After importing from Access database, run this diagnostic:

### **Step 1: Check Diagnostics in Fixtures Page**

1. Go to **Fixtures** page
2. Click **? Menu** ? **Diagnostics**
3. Look for output showing:
   ```
   ACTIVE SEASON DIAGNOSTICS
   
   ActiveSeasonId Property: [GUID]
   
   Total Seasons: [number]
   
   Seasons:
   ACTIVE [Season Name] (matches ActiveSeasonId)
        ID: [GUID]
        IsActive: True
        
      [IMPORTED] Season Name
        ID: [GUID]
        IsActive: False
   
   Data for Active Season:
     Divisions: [number]
     Teams: [number]
   ```

### **Step 2: Check Imported Season Data**

After import, look at **each tab** and check the **season filter**:

#### **Divisions Page:**
- Are imported divisions showing up?
- Do they have `SeasonId` set?
- Are they in "[IMPORTED]" season?

#### **Teams Page:**
- Are imported teams showing up?
- Do they show in "[IMPORTED]" season?
- Are they assigned to divisions?

#### **Players Page:**
- Are imported players showing up?
- Are they assigned to teams?
- Do they show in "[IMPORTED]" season?

#### **Fixtures Page:**
- Toggle **"Active Season Only"** OFF
- Search for imported fixtures
- Are they showing up?

---

## ?? Common Import Issues

### **Issue 1: Data imports but doesn't show in current season**
**Cause:** Imported data is assigned to the "[IMPORTED] Season Name" season, not your current active season.

**Solution:** This is by design! Imported data gets its own season to avoid conflicts.

**How to use:**
1. Go to **Seasons** page
2. Find the `[IMPORTED] ...` season
3. Click "Set Active" to make it the active season
4. **OR** manually change divisions/teams/players to your current season

---

### **Issue 2: "No divisions/teams/players found"**
**Cause:** Season filter is ON and pointing to wrong season.

**Solution:**
1. **Divisions/Venues/Teams/Players pages:** These have **season filters**
2. Toggle to show all seasons
3. Or select the imported season

---

### **Issue 3: Duplicate names when checking**
**Cause:** Merge logic checks by name only - `d.Name.Equals(div.Name, StringComparison.OrdinalIgnoreCase)`

**What this means:**
- If you have a division named "Premier" in your current season
- And import a division named "Premier" from Access
- Only ONE "Premier" division will exist (the original)
- The imported "Premier" won't be added (duplicate name)

**Fix if needed:**
Import with modified `ActualDatabaseImporter.cs` to add suffix:
```csharp
// In ImportDivisions():
var division = new Division
{
    Id = Guid.NewGuid(),
    SeasonId = _seasonId,
    Name = $"{name} (Imported)", // ADD SUFFIX TO MAKE UNIQUE
    Notes = $"[IMPORTED] {bandColour}"
};
```

---

### **Issue 4: Imported season not showing as separate**
**Cause:** Season name might match existing season

**Check:**
```
Seasons page ? Look for "[IMPORTED] Season Name Year"
```

If you see your season name WITHOUT `[IMPORTED]` prefix, the importer might have found a match and not created a new season.

---

## ?? Quick Fix Steps

### **To see ALL imported data:**

#### **1. Set Imported Season as Active**
```
Seasons page
? Select "[IMPORTED] ..." season
? Click "Set Active"
? Now all pages will show imported data
```

#### **2. Merge Manually**
If you want imported data in your CURRENT season:
```
For each imported entity:
1. Open editor
2. Change SeasonId to current season
3. Save
```

This is tedious but ensures clean data.

#### **3. Re-import with Modified Code**
Modify `ActualDatabaseImporter.cs` to assign to current season instead of creating new:

```csharp
// Change this line in ActualDatabaseImporter constructor:
// OLD:
private Guid _seasonId = Guid.NewGuid();

// NEW (use current active season):
private Guid _seasonId = [YOUR_CURRENT_SEASON_ID];
```

---

## ?? Verification Queries

### **Check what was imported:**

#### **Check Divisions:**
```
Divisions page ? Search for "IMPORTED" or your division names
Check SeasonId column (if visible in diagnostics)
```

#### **Check Teams:**
```
Teams page ? Look for teams from Access
Check if they have DivisionId assigned
```

#### **Check Players:**
```
Players page ? Look for players from Access
Check if they have TeamId assigned
```

#### **Check Fixtures:**
```
Fixtures page ? Turn OFF "Active Season Only"
Look for fixtures from Access database
```

---

## ? Success Indicators

After successful import, you should see:

1. **New season** named `[IMPORTED] Season Name Year`
2. **Divisions** with SeasonId = imported season
3. **Venues** with SeasonId = imported season
4. **Teams** with SeasonId = imported season + DivisionId set
5. **Players** with SeasonId = imported season + TeamId set
6. **Fixtures** with SeasonId = imported season

**All imported entities have Notes or Name fields marked with `[IMPORTED]`**

---

## ?? Next Steps

Once you verify data is imported:

### **Option A: Use Imported Season**
```
1. Seasons page ? Set imported season as active
2. All imported data now available
3. Generate new fixtures if needed
```

### **Option B: Merge into Current Season**
```
1. Manually update SeasonId for each imported entity
2. OR write a migration script
3. OR modify importer to use current season ID
```

### **Option C: Keep Separate**
```
1. Keep imported season as historical data
2. Switch between seasons using "Set Active"
3. Compare historical vs current
```

---

## ?? If Still Not Working

### **Enable Debug Output:**

Add this to `MergeImportedDataAsync` in `SettingsPage.xaml.cs`:

```csharp
private async Task MergeImportedDataAsync(LeagueData importedData)
{
    await Task.Run(() =>
    {
        // ADD THIS DEBUG OUTPUT:
        System.Diagnostics.Debug.WriteLine($"=== IMPORT MERGE DEBUG ===");
        System.Diagnostics.Debug.WriteLine($"Divisions to import: {importedData.Divisions.Count}");
        System.Diagnostics.Debug.WriteLine($"Venues to import: {importedData.Venues.Count}");
        System.Diagnostics.Debug.WriteLine($"Teams to import: {importedData.Teams.Count}");
        System.Diagnostics.Debug.WriteLine($"Players to import: {importedData.Players.Count}");
        System.Diagnostics.Debug.WriteLine($"Seasons to import: {importedData.Seasons.Count}");
        System.Diagnostics.Debug.WriteLine($"Fixtures to import: {importedData.Fixtures.Count}");
        
        var beforeCounts = new
        {
            Divisions = DataStore.Data.Divisions.Count,
            Venues = DataStore.Data.Venues.Count,
            Teams = DataStore.Data.Teams.Count,
            Players = DataStore.Data.Players.Count,
            Seasons = DataStore.Data.Seasons.Count,
            Fixtures = DataStore.Data.Fixtures.Count
        };

        // Merge divisions
        int divsAdded = 0;
        foreach (var div in importedData.Divisions)
        {
            var exists = DataStore.Data.Divisions.Any(d => 
                d.Name.Equals(div.Name, StringComparison.OrdinalIgnoreCase));
                
            if (!exists)
            {
                DataStore.Data.Divisions.Add(div);
                divsAdded++;
                System.Diagnostics.Debug.WriteLine($"  Added Division: {div.Name} (SeasonId: {div.SeasonId})");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"  Skipped Division (duplicate): {div.Name}");
            }
        }

        // ... similar for other entities ...
        
        System.Diagnostics.Debug.WriteLine($"Import Complete:");
        System.Diagnostics.Debug.WriteLine($"  Divisions: {beforeCounts.Divisions} ? {DataStore.Data.Divisions.Count} (added: {divsAdded})");
        // ... etc
    });
}
```

Then check **Output window** in Visual Studio after import to see exactly what's happening!

---

## ?? Quick Summary

**Your imported data IS there**, but it's in a **separate season** called `[IMPORTED] Season Name`.

**To see it:**
1. Go to Seasons page
2. Find `[IMPORTED] ...` season
3. Click "Set Active"
4. Now all pages show imported data

**OR**

Turn off "Active Season Only" filters on each page to see ALL seasons' data.

---

**Last Updated:** 2025  
**Status:** Import works, data is in separate season by design  
**Quick Fix:** Set imported season as active to see data
