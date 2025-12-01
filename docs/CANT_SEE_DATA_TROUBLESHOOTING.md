# ?? Can't See Data - Troubleshooting Guide

## ? **What I Just Added:**

### **1. Enhanced Debug Logging**
- Now shows EXACTLY what's in the database at each step
- Shows which divisions are being filtered
- Shows why divisions aren't appearing

### **2. "Check Database" Button**
- Added **?? Check Database** button in the burger menu
- Click it to see a complete diagnostic report
- Shows ALL data in your database with Season assignments

---

## ?? **How to Diagnose the Issue:**

### **Step 1: Run the App**
1. Start the app in **Debug mode** (F5)
2. Go to **Divisions** page

### **Step 2: Open Debug Output**
1. In Visual Studio: **View ? Output**
2. Select **"Debug"** from the dropdown at the top
3. You should immediately see `=== DIVISIONS DEBUG ===` output

### **Step 3: Check the Database**
1. Click **?** (burger menu) on Divisions page
2. Scroll down and click **?? Check Database** button
3. Read the popup - it will tell you:
   - ? How many divisions are in the database
   - ? Which season each division belongs to
   - ? What your current season is
   - ? Why divisions aren't showing

---

## ?? **What the Output Means:**

### **Example Debug Output:**

```
=== DIVISIONS DEBUG ===
Current Season ID: a1b2c3d4-e5f6-7890-abcd-ef1234567890
Show All Seasons: False
Total Divisions in DB: 8
  Division: 'Premier' (SeasonId: a1b2c3d4-e5f6-7890-abcd-ef1234567890)
  Division: 'Division One' (SeasonId: a1b2c3d4-e5f6-7890-abcd-ef1234567890)
  Division: 'Premier' (SeasonId: 12345678-90ab-cdef-1234-567890abcdef)
  Division: 'Division One' (SeasonId: 12345678-90ab-cdef-1234-567890abcdef)
  Division: 'Division Two' (SeasonId: 12345678-90ab-cdef-1234-567890abcdef)
Filtered Divisions: 2
  Will display: 'Premier' (SeasonId: a1b2c3d4...)
  Will display: 'Division One' (SeasonId: a1b2c3d4...)
Added 2 items to ObservableCollection
=== END DIVISIONS DEBUG ===
```

**This means:**
- ? 8 divisions exist in database
- ? 2 match your current season ? these will display
- ? 6 are in a different season (the imported season)

### **If You See This:**
```
Total Divisions in DB: 0
```
**Problem:** No divisions were imported at all!  
**Solution:** Re-run the import and check the import logs

### **If You See This:**
```
Total Divisions in DB: 15
Filtered Divisions: 0
Found divisions in other seasons: 15 in season 12345678-90ab-cdef-1234-567890abcdef
```
**Problem:** All divisions are in a different season!  
**Solution:** Check **"Show all seasons"** checkbox OR go to Seasons page and set the imported season as active

---

## ?? **Common Issues and Fixes:**

### **Issue 1: Database Check Shows 0 Divisions**
```
Total Divisions: 0
Total Teams: 0
Total Players: 0
```

**Cause:** Import didn't work OR data file is wrong  
**Fix:**
1. Check import logs in Output window for `=== IMPORT MERGE DEBUG ===`
2. Look for "? Added" messages - if you see "? Skipped (duplicate name)", that's the issue
3. Re-import after deleting existing data with same names

---

### **Issue 2: Database Check Shows Divisions But None Display**
```
Total Divisions: 20
Divisions by Season:
  [IMPORTED] Season 2024: 20 division(s)
  Current Season: 0 division(s) ? CURRENT
```

**Cause:** All divisions are in imported season, none in current season  
**Fix:**

**Option A: Switch to Imported Season**
1. Go to **Seasons** tab
2. Find `[IMPORTED] Season 2024`
3. Click it ? Click **"Set Active"**
4. ? Done! Now you'll see all 20 divisions

**Option B: Use Show All Seasons Toggle**
1. On Divisions page, check **"Show all seasons"** checkbox
2. ? Done! Now you'll see all 20 divisions

---

### **Issue 3: Divisions Show in Debug but Not in UI**
```
Added 15 items to ObservableCollection
Items in ObservableCollection: 15
```

**Cause:** UI binding issue  
**Fix:**
1. Check if `DivisionsList.ItemsSource` is set to `_divisions`
2. Try restarting the app
3. Check for XAML errors in Output window

---

## ?? **The 3-Step Test:**

### **Test 1: Check the Database**
1. Click **?? Check Database** button
2. Look at "Total Divisions" line
3. **If 0:** Import didn't work - check import logs
4. **If > 0:** Data is there, move to Test 2

### **Test 2: Check Season Assignment**
1. Look at "Divisions by Season" section in database check
2. Find the line with "? CURRENT" marker
3. **If count is 0:** All divisions are in other seasons - check "Show all seasons" OR switch active season
4. **If count > 0:** Move to Test 3

### **Test 3: Check UI Binding**
1. Look at "Items in ObservableCollection" line
2. **If matches division count:** UI binding issue - restart app
3. **If 0:** Filtering issue - check debug output

---

## ?? **Next Steps:**

After running the diagnostic:

1. **Run the app**
2. **Go to Divisions page**
3. **Click ?? Check Database**
4. **Share the popup text** with me so I can tell you exactly what's wrong!

The diagnostic will tell us:
- ? Is data in the database?
- ? Which season is it assigned to?
- ? Why isn't it displaying?
- ? What's the fix?

---

**Last Updated:** 2025  
**Status:** Diagnostic tools added - ready to troubleshoot!
