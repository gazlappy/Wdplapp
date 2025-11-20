# Emoji Reference for WDPL2 Application

This document lists all emojis used throughout the application and their Unicode representations for consistency across platforms.

## Centralized Emoji Helper

All emojis are defined in `Wdpl2.Helpers.Emojis` class for consistency. Usage:

```csharp
using Wdpl2.Helpers;

await DisplayAlert($"{Emojis.Success} Success", "Operation completed!", "OK");
```

## Complete Emoji List

### Success & Status
- ? **Success**: `Emojis.Success` - `\u2705`
- ? **Error**: `Emojis.Error` - `\u274C`
- ?? **Warning**: `Emojis.Warning` - `\u26A0\uFE0F`
- ?? **Info**: `Emojis.Info` - `\u2139\uFE0F`
- ? **Check**: `Emojis.Check` - `\u2713`
- ? **Cross**: `Emojis.Cross` - `\u2717`
- ?? **Active**: `Emojis.Active` - `\U0001F7E2`
- ? **Inactive**: `Emojis.Inactive` - `\u26AA`
- ?? **In Progress**: `Emojis.InProgress` - `\U0001F7E1`

### Actions
- ? **Add**: `Emojis.Add` - `\u2795`
- ??? **Delete**: `Emojis.Delete` - `\U0001F5D1`
- ?? **Edit**: `Emojis.Edit` - `\u270F\uFE0F`
- ?? **Save**: `Emojis.Save` - `\U0001F4BE`
- ?? **Reload**: `Emojis.Reload` - `\U0001F504`
- ?? **Export**: `Emojis.Export` - `\U0001F4E4`
- ?? **Import**: `Emojis.Import` - `\U0001F4E5`
- ?? **Copy**: `Emojis.Copy` - `\U0001F4CB`
- ?? **Settings**: `Emojis.Settings` - `\u2699\uFE0F`

### Sports & Games
- 8? **Eight Ball**: `Emojis.EightBall` - `"8\u20E3"`
- ?? **Trophy**: `Emojis.Trophy` - `\U0001F3C6`
- ?? **Medal**: `Emojis.Medal` - `\U0001F3C5`
- ?? **Target**: `Emojis.Target` - `\U0001F3AF`
- ?? **Fire**: `Emojis.Fire` - `\U0001F525`
- ? **Star**: `Emojis.Star` - `\u2B50`

### People & Teams
- ?? **Player**: `Emojis.Player` - `\U0001F464`
- ?? **Team**: `Emojis.Team` - `\U0001F465`
- ?? **Crown** (Captain): `Emojis.Crown` - `\U0001F451`
- ?? **Person**: `Emojis.Person` - `\U0001F9D1`

### Locations
- ?? **Location**: `Emojis.Location` - `\U0001F4CD`
- ?? **Building**: `Emojis.Building` - `\U0001F3E2`
- ?? **Home**: `Emojis.Home` - `\U0001F3E0`
- ?? **Map Pin**: `Emojis.MapPin` - `\U0001F4CC`

### Time & Calendar
- ?? **Calendar**: `Emojis.Calendar` - `\U0001F4C5`
- ?? **Clock**: `Emojis.Clock` - `\U0001F550`
- ? **Hourglass**: `Emojis.Hourglass` - `\u231B`
- ?? **Stopwatch**: `Emojis.Stopwatch` - `\u23F1\uFE0F`

### Data & Stats
- ?? **Chart**: `Emojis.Chart` - `\U0001F4CA`
- ?? **Table**: `Emojis.Table` - `\U0001F4CB`
- ?? **Document**: `Emojis.Document` - `\U0001F4C4`
- ?? **Folder**: `Emojis.Folder` - `\U0001F4C1`
- ?? **Database**: `Emojis.Database` - `\U0001F4BE`

### Navigation
- ?? **Back**: `Emojis.Back` - `\u2B05\uFE0F`
- ?? **Forward**: `Emojis.Forward` - `\u27A1\uFE0F`
- ?? **Up**: `Emojis.Up` - `\u2B06\uFE0F`
- ?? **Down**: `Emojis.Down` - `\u2B07\uFE0F`

### Miscellaneous
- ?? **Food**: `Emojis.Food` - `\U0001F374`
- ?? **Note**: `Emojis.Note` - `\U0001F4DD`
- ?? **Lock**: `Emojis.Lock` - `\U0001F512`
- ?? **Unlock**: `Emojis.Unlock` - `\U0001F513`
- ? **Lightning**: `Emojis.Lightning` - `\u26A1`
- ?? **Wrench**: `Emojis.Wrench` - `\U0001F527`
- ?? **Rocket**: `Emojis.Rocket` - `\U0001F680`
- ? **Sparkles**: `Emojis.Sparkles` - `\u2728`
- • **Bullet**: `Emojis.Bullet` - `\u2022`
- ?? **Thumbs Up**: `Emojis.ThumbsUp` - `\U0001F44D`
- ?? **Thumbs Down**: `Emojis.ThumbsDown` - `\U0001F44E`

## Font Family Requirements

For emojis to render correctly across platforms in XAML:

```xml
<Label 
    Text="&#x2705;"
    FontFamily="Segoe UI Emoji" />
```

In C#, the `Emojis` helper class already provides the correct Unicode strings, no font family needed for DisplayAlert.

## Platform-Specific Notes

### Windows
- Uses **Segoe UI Emoji** font
- All emojis render in full color
- Variation selectors (FE0F) ensure emoji-style rendering

### iOS
- Uses **Apple Color Emoji** font
- Emojis render in full color by default
- FontFamily not required for labels

### Android
- Uses **Noto Color Emoji** font
- May need Android API 23+ for best emoji support
- FontFamily not required

### macOS (Catalyst)
- Uses **Apple Color Emoji** font
- Similar to iOS behavior

## Usage Examples

### DisplayAlert Messages
```csharp
// Success
await DisplayAlert($"{Emojis.Success} Saved", "All changes saved!", "OK");

// Error
await DisplayAlert($"{Emojis.Error} Error", $"Operation failed: {ex.Message}", "OK");

// Warning
await DisplayAlert($"{Emojis.Warning} Delete", "Are you sure?", "Yes", "No");

// Info
await DisplayAlert($"{Emojis.Info} No Data", "Please select an item first.", "OK");
```

### Status Messages
```csharp
SetStatus($"{Emojis.Success} Operation completed");
SetStatus($"{Emojis.Error} Failed to load data");
SetStatus($"{Emojis.Reload} Reloading...");
```

### Dynamic Labels
```csharp
new Label 
{ 
    Text = $"{Emojis.Player} Players: {count}",
    FontFamily = Emojis.FontFamily  // For XAML-created labels on Windows
}
```

## Pages Using Emojis

### ? Updated Pages
- **SeasonSetupPage**: ? Full emoji support
- **PlayersPage**: ? All alerts and status messages
- **FixturesPage**: ? All alerts and 8-ball display

### ?? To Be Updated
- TeamsPage
- DivisionsPage
- VenuesPage
- CompetitionsPage
- SeasonsPage
- SettingsPage
- LeagueTablesPage

## Troubleshooting

If emojis don't display correctly:

1. **In DisplayAlert**: Should work automatically, no font family needed
2. **In XAML Labels**: Add `FontFamily="Segoe UI Emoji"` on Windows
3. **Check Unicode**: Ensure you're using the `Emojis` helper class
4. **Platform Version**: Some older OS versions may not support all emojis

## Contributing

When adding new emojis:
1. Add to `Wdpl2/Helpers/Emojis.cs`
2. Update this documentation
3. Use proper Unicode escape sequences
4. Test on Windows, iOS, and Android if possible

