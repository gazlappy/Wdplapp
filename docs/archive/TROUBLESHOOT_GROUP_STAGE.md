# Troubleshooting Group Stage Generation

## ?? Debugging Steps

I've added enhanced debugging to the `OnGenerateGroups()` method. When you click "Generate Groups", you'll now see detailed status messages that will help us identify the problem.

---

## ? Pre-Flight Checklist

Before clicking "Generate Groups", verify:

### **1. Competition Format**
- [ ] Competition format is set to **`SinglesGroupStage`** or **`DoublesGroupStage`**
- [ ] **NOT** `SinglesKnockout`, `RoundRobin`, etc.

### **2. Participants Added**
- [ ] You've added participants via the "Add" button
- [ ] Participant count ? (Number of Groups × 2)
- [ ] Example: 4 groups needs minimum 8 participants

### **3. Group Settings Configured**
- [ ] Number of Groups: (e.g., 4, 8)
- [ ] Top Players Advance: (e.g., 2)
- [ ] Lower to Plate: (e.g., 2)
- [ ] All fields show valid numbers

### **4. Season Selected**
- [ ] A season is active/selected
- [ ] Check status bar shows current season

---

## ?? Status Messages You'll See

After adding the enhanced debugging, the status bar will show:

### **? Success Flow:**
```
DEBUG: Format = SinglesGroupStage
DEBUG: Singles - 32 players, need 8
DEBUG: Calling GenerateGroupStage with 32 participants...
DEBUG: GenerateGroupStage returned 8 groups
DEBUG: Assigned 8 groups to competition
DEBUG: Created plate competition: Winter Championship Plate
? Generated 8 groups with 120 matches
```

### **? Common Error Patterns:**

#### **Error 1: Wrong Format**
```
DEBUG: Format = SinglesKnockout
ERROR: This competition is not a group stage format (Format = SinglesKnockout)
```
**Solution:** Change Format to `SinglesGroupStage`

#### **Error 2: Not Enough Participants**
```
DEBUG: Singles - 6 players, need 8
ERROR: Need at least 8 players, but only have 6
```
**Solution:** Add more participants OR reduce number of groups

#### **Error 3: No Competition Selected**
```
ERROR: No competition selected
```
**Solution:** Click on a competition in the left panel first

#### **Error 4: Participants Not Saved**
```
DEBUG: Singles - 0 players, need 8
```
**Solution:** Make sure to click "Save Changes" after adding participants

---

## ?? Testing Procedure

### **Test 1: Minimal Setup (4 Groups, 8 Players)**
1. Create new competition
2. Set Name: "Test Group Stage"
3. Set Format: **`SinglesGroupStage`**
4. Click "Save Changes"
5. Click "Add" participants
6. Select 8 players using multi-select
7. Click "Add Selected"
8. Verify participant list shows 8 players
9. Check Group Settings:
   - Number of Groups: **4**
   - Top Players Advance: **2**
   - Lower to Plate: **2**
10. Click **"Generate Groups"**
11. Watch status bar for messages

**Expected Result:**
- Status shows: "? Generated 4 groups with 12 matches"
- "View Groups (4)" button appears
- Success dialog shown

---

### **Test 2: Standard Setup (8 Groups, 128 Players)**
1. Create new competition
2. Set Format: **`SinglesGroupStage`**
3. Add 128 players via multi-select
4. Set Number of Groups: **8**
5. Click **"Generate Groups"**

**Expected Result:**
- 8 groups created (Groups A-H)
- 960 total matches
- Plate competition created

---

## ?? Common Issues & Solutions

### **Issue 1: Button Doesn't Appear**
**Problem:** "Generate Groups" button not visible

**Possible Causes:**
1. Format not set to Group Stage
2. Page not refreshed after changing format

**Solution:**
1. Change format to `SinglesGroupStage`
2. Click "Save Changes"
3. Select another competition, then reselect yours
4. Button should now appear

---

### **Issue 2: Nothing Happens When Clicking**
**Problem:** Click "Generate Groups" but no feedback

**Check:**
1. Look at status bar at bottom
2. Any error messages?
3. Is an exception dialog showing?

**Debug:**
- Check Visual Studio Output window for exceptions
- Look for any console errors

---

### **Issue 3: Groups Generated But Not Visible**
**Problem:** Status says generated but can't see them

**Check:**
1. Does "View Groups" button appear after generation?
2. Is `Groups.Count > 0`?

**Solution:**
1. Close and reopen the competition
2. Check if groups persist after save
3. Look at status: "DEBUG: Assigned X groups"

---

### **Issue 4: Participants List Shows 0**
**Problem:** Added participants but count shows 0

**Possible Causes:**
1. Didn't click "Save Changes" after adding
2. Wrong format selected when adding
3. Season mismatch

**Solution:**
1. Verify Format is correct
2. Add participants again
3. Click "Save Changes"
4. Check participant count in competition details

---

## ?? What to Report

If the issue persists, provide:

1. **Status Bar Messages**
   - Copy all DEBUG and ERROR messages
   
2. **Competition Settings**
   ```
   Name: [Competition Name]
   Format: [Selected Format]
   Participant Count: [Number]
   Number of Groups: [Number]
   ```

3. **Steps Taken**
   - What you clicked
   - In what order

4. **Screenshot**
   - Show the Competitions page with the issue

5. **Any Error Dialogs**
   - Copy full error message if shown

---

## ?? Manual Verification

You can manually check the data:

### **Check 1: Participant IDs**
```csharp
// In CompetitionsPage.xaml.cs, add temporary debug:
SetStatus($"Participant IDs: {string.Join(", ", _selectedCompetition.ParticipantIds)}");
```

### **Check 2: Group Settings**
```csharp
SetStatus($"Groups: {_selectedCompetition.GroupSettings?.NumberOfGroups}, " +
          $"Advance: {_selectedCompetition.GroupSettings?.TopPlayersAdvance}");
```

### **Check 3: Groups After Generation**
```csharp
SetStatus($"Groups Count: {_selectedCompetition.Groups.Count}");
foreach (var g in _selectedCompetition.Groups)
{
    SetStatus($"  {g.Name}: {g.ParticipantIds.Count} players, {g.Matches.Count} matches");
}
```

---

## ?? Quick Fix Attempts

### **Attempt 1: Reset and Retry**
1. Delete the competition
2. Create fresh competition
3. Set format to `SinglesGroupStage` FIRST
4. Then add participants
5. Then generate groups

### **Attempt 2: Reduce Complexity**
1. Start with minimum: 4 groups, 8 players
2. Don't enable plate competition
3. Generate and verify it works
4. Then scale up

### **Attempt 3: Check Data Persistence**
```csharp
// After generation, before refresh:
DataStore.Save(); // Force save
var saved = DataStore.Data.Competitions.First(c => c.Id == _selectedCompetition.Id);
SetStatus($"Saved groups count: {saved.Groups.Count}");
```

---

## ? Expected Behavior

When working correctly:

1. **Click "Generate Groups"**
2. Status shows progress messages
3. Success dialog appears
4. "View Groups (X)" button appears
5. Can click to see tabbed group view
6. Each group shows participants and matches
7. Standings table visible (all 0s initially)

---

## ?? Emergency Debug Mode

Add this temporary method to CompetitionsPage.xaml.cs:

```csharp
private async void DebugGroupGeneration()
{
    var report = new System.Text.StringBuilder();
    
    report.AppendLine($"Competition: {_selectedCompetition?.Name}");
    report.AppendLine($"Format: {_selectedCompetition?.Format}");
    report.AppendLine($"Participants: {_selectedCompetition?.ParticipantIds.Count}");
    report.AppendLine($"Groups Settings: {_selectedCompetition?.GroupSettings != null}");
    
    if (_selectedCompetition?.GroupSettings != null)
    {
        report.AppendLine($"  Number of Groups: {_selectedCompetition.GroupSettings.NumberOfGroups}");
        report.AppendLine($"  Top Advance: {_selectedCompetition.GroupSettings.TopPlayersAdvance}");
        report.AppendLine($"  Lower Plate: {_selectedCompetition.GroupSettings.LowerPlayersToPlate}");
    }
    
    report.AppendLine($"Current Groups Count: {_selectedCompetition?.Groups.Count}");
    
    await DisplayAlert("Debug Report", report.ToString(), "OK");
}
```

Call this before and after generating to compare.

---

## ?? Next Steps

1. **Run the app with the enhanced debugging**
2. **Try to generate groups**
3. **Note the status messages**
4. **Report back what you see**

The debug messages will tell us exactly where the process is failing!
