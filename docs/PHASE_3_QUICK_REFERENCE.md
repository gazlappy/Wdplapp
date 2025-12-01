# ?? Phase 3 Quick Reference

## ?? User Settings

### Available Options

| Setting | Options | Default |
|---------|---------|---------|
| **Match Reminders** | ON/OFF | ON |
| **Reminder Time** | 1, 2, 4, 6, 12, 24 hours | 2 hours |
| **Result Notifications** | ON/OFF | OFF *(future)* |
| **Weekly Fixture List** | ON/OFF | OFF *(future)* |

---

## ?? Quick Actions

### Enable/Disable Reminders
```
Settings ? Notifications ? Match Reminders [Toggle]
```

### Change Reminder Time
```
Settings ? Notifications ? "Remind me:" [Dropdown] ? Select hours
```

### Test Notifications
```
Settings ? Notifications ? Send Test Notification
```

### View Pending
```
Fixtures ? [? Menu] ? ?? Manage Match Reminders
```

---

## ?? Where Settings are Saved

**File:** `league.json`
**Location:** App data folder
**Section:** `Settings` object

```json
{
  "Settings": {
    "MatchRemindersEnabled": true,
    "ReminderHoursBefore": 2,
    "ResultNotificationsEnabled": false,
    "WeeklyFixtureListEnabled": false
  }
}
```

---

## ?? How It Works

### 1. User Changes Setting
```
Settings Page ? Toggle/Picker change ? Immediate save
```

### 2. Applied to Fixtures
```
Generate/Save Fixture ? Check Settings ? Schedule with configured hours
```

### 3. Reminder Fires
```
Configured hours before ? OS notification ? User reminded
```

---

## ?? Troubleshooting

| Problem | Solution |
|---------|----------|
| No notifications | Request permissions first |
| Settings not saving | Check storage permissions |
| Wrong reminder time | Check Settings ? Notifications |
| Still using old time | Only affects NEW reminders |

---

## ?? Best Practices

### **Recommended Settings**

**For Regular Players:**
- Match Reminders: ON
- Reminder Time: 2 hours
- Result Notifications: ON *(when implemented)*

**For Team Captains:**
- Match Reminders: ON
- Reminder Time: 24 hours + 2 hours *(future: multiple reminders)*
- Result Notifications: ON
- Weekly Fixture List: ON

**For Casual Players:**
- Match Reminders: ON
- Reminder Time: 4 hours
- Result Notifications: OFF

---

## ?? Developer Quick Reference

### Access Settings
```csharp
var settings = DataStore.Data.Settings;
var enabled = settings.MatchRemindersEnabled;
var hours = settings.ReminderHoursBefore;
```

### Save Settings
```csharp
Settings.MatchRemindersEnabled = true;
Settings.ReminderHoursBefore = 4;
DataStore.Save();
```

### Use in Scheduling
```csharp
if (settings.MatchRemindersEnabled)
{
    await ScheduleReminderAsync(
        fixture, 
        hoursBeforeMatch: settings.ReminderHoursBefore
    );
}
```

---

## ?? Status Indicators

### Settings Page
```
? Notification settings saved (4h reminder)
```

### Fixtures Page
```
?? Reminder scheduled for Tue 17:30 (2h before)
?? Match reminders disabled in Settings
?? Match has passed - no reminder
```

---

## ?? Quick Test Procedure

1. **Enable Permissions**
   ```
   Settings ? Notifications ? Request Permissions ? Grant
   ```

2. **Configure**
   ```
   Toggle Match Reminders ON
   Select "4 hours" from dropdown
   ```

3. **Test**
   ```
   Click "Send Test Notification"
   Check notification appears
   ```

4. **Verify**
   ```
   Generate fixture
   Check reminder status shows "4h before"
   ```

5. **Confirm**
   ```
   Restart app
   Check Settings still shows 4 hours
   ```

---

## ?? Phase 3 Features at a Glance

- ? Enable/disable match reminders
- ? Choose reminder time (1-24 hours)
- ? Immediate save on change
- ? Settings persist across app restarts
- ? Visual status indicators
- ? Future-ready for more notification types

---

## ?? UI Layout

```
Settings ? Notifications Tab
?
?? ?? Notification Preferences
?  ?? ?? Match Reminders: [? ON]
?  ??    Remind me: [2 hours ?]
?  ?? ? Result Notifications: [OFF]
?  ?? ?? Weekly Fixture List: [OFF]
?
?? Setup & Testing
?  ?? [?? Request Permissions]
?  ?? [?? Send Test]
?  ?? Pending: 12
?  ?? [? Cancel All]
?
?? ?? Help & Information
```

---

## ?? Settings Flow

```
User Action ? Immediate Save ? Apply to Fixtures
     ?              ?                  ?
  Toggle        DataStore         Schedule with
  Picker         .Save()         configured hours
```

---

## ? Verification Checklist

- [ ] Settings UI visible
- [ ] Toggle works
- [ ] Picker shows correct options
- [ ] Changes save immediately
- [ ] Status messages appear
- [ ] Fixtures use configured hours
- [ ] Settings persist after restart

---

## ?? You're All Set!

**Phase 3 Complete** - Users have full control over notifications!

**Next:** Enjoy your customizable notification system! ??

---

**Quick Links:**
- Full Documentation: `PHASE_3_COMPLETE.md`
- Phase 2: `PHASE_2_COMPLETE.md`
- Phase 1: `PHASE_1_SUMMARY.md`
