# ?? Team Data Storage Structure

## How Teams Data is Stored

### Storage Architecture

Teams data is stored in **two ways** in your application:

1. **JSON File (Legacy/Backup)** - `data.json`
2. **SQLite Database (Current)** - `league.db`

---

## ? Teams ARE Separated by Season

### Team Model Structure

```csharp
public sealed class Team
{
    public Guid Id { get; set; }
    
    // ?? THIS IS THE KEY - Each team belongs to a season
    public Guid? SeasonId { get; set; }
    
    public string? Name { get; set; }
    public Guid? DivisionId { get; set; }
    public Guid? VenueId { get; set; }
    public Guid? TableId { get; set; }
    public Guid? CaptainPlayerId { get; set; }
    public bool ProvidesFood { get; set; }
}
```

### Key Points:

1. **Every team has a `SeasonId`** - Links the team to a specific season
2. **Same team name can exist in multiple seasons** - Different `Id` for each
3. **Teams are isolated by season** - You can have "Team A" in Season 2023 and "Team A" in Season 2024

---

## ?? Storage Details

### 1. In-Memory (LeagueData)

```csharp
public sealed class LeagueData
{
    public List<Team> Teams { get; set; } = new();
    public List<Season> Seasons { get; set; } = new();
    public Guid? ActiveSeasonId { get; set; }
}
```

- All teams from all seasons are in one list: `Teams`
- Filtered by `SeasonId` when needed
- Helper method: `GetSeasonData(seasonId)` returns teams for specific season

### 2. SQLite Database

**Table:** `Teams`

| Column | Type | Notes |
|--------|------|-------|
| Id | GUID | Primary Key |
| **SeasonId** | GUID | **Foreign Key to Seasons table** |
| Name | VARCHAR(100) | Team name |
| DivisionId | GUID | Foreign Key (nullable) |
| VenueId | GUID | Foreign Key (nullable) |
| TableId | GUID | Stored in Venue's JSON |
| CaptainPlayerId | GUID | Foreign Key (nullable) |
| ProvidesFood | BOOLEAN | |

**Indexes:**
- `SeasonId` - Fast filtering by season
- `DivisionId` - Fast filtering by division

**Relationships:**
```csharp
// Team belongs to Season (CASCADE DELETE)
Team -> Season (SeasonId)
  OnDelete: Cascade // Delete season ? deletes all teams

// Team belongs to Division (SET NULL)
Team -> Division (DivisionId)
  OnDelete: SetNull // Delete division ? sets DivisionId to null

// Team has home Venue (SET NULL)
Team -> Venue (VenueId)
  OnDelete: SetNull // Delete venue ? sets VenueId to null
```

---

## ?? How Teams are Filtered by Season

### In Pages (e.g., TeamsPage.xaml.cs)

```csharp
private void RefreshTeamList(string? search)
{
    _teamItems.Clear();

    if (!_currentSeasonId.HasValue)
    {
        SetStatus("No season selected");
        return;
    }

    // ?? Filter by current season
    var teams = DataStore.Data.Teams
        .Where(t => t.SeasonId == _currentSeasonId.Value)
        .OrderBy(t => t.Name)
        .ToList();

    // Search filter if needed
    if (!string.IsNullOrWhiteSpace(search))
    {
        var lower = search.ToLower();
        teams = teams.Where(t => t.Name?.ToLower().Contains(lower) ?? false)
            .OrderBy(t => t.Name)
            .ToList();
    }

    foreach (var t in teams)
        _teamItems.Add(new TeamListItem { Id = t.Id, Name = t.Name });
}
```

### In SQLite (SqliteDataStore.cs)

```csharp
public async Task<List<Team>> GetTeamsBySeasonAsync(Guid seasonId)
{
    using var context = new LeagueContext();
    
    return await context.Teams
        .Where(t => t.SeasonId == seasonId)
        .OrderBy(t => t.Name)
        .ToListAsync();
}
```

---

## ??? Cascade Delete Behavior

When you **delete a season**, all associated teams are automatically deleted:

```csharp
public void DeleteSeasonCascade(Guid seasonId)
{
    // Remove all teams for this season
    Teams.RemoveAll(t => t.SeasonId == seasonId);
    
    // Also removes:
    // - Players (SeasonId)
    // - Fixtures (SeasonId)
    // - Venues (SeasonId)
    // - Divisions (SeasonId)
    
    // Finally remove the season itself
    Seasons.RemoveAll(s => s.Id == seasonId);
}
```

**Database:** `OnDelete(DeleteBehavior.Cascade)` ensures SQLite does the same.

---

## ?? Other Entities Also Separated by Season

All these entities have `SeasonId`:

| Entity | SeasonId | Cascade Delete |
|--------|----------|----------------|
| **Division** | ? Yes | ? Yes |
| **Team** | ? Yes | ? Yes |
| **Player** | ? Yes | ? Yes |
| **Venue** | ? Yes | ? Yes |
| **Fixture** | ? Yes | ? Yes |
| **Competition** | ? Yes | ? Yes |

**This means:**
- Each season has its own complete set of data
- You can have multiple seasons with different teams, players, divisions
- Deleting a season removes all its associated data
- No data is shared between seasons (except AppSettings)

---

## ?? Active Season Concept

```csharp
public sealed class LeagueData
{
    public List<Season> Seasons { get; set; } = new();
    public Guid? ActiveSeasonId { get; set; }  // ?? Current/active season
}
```

- **Only ONE season can be active** at a time
- Pages typically filter data by: `SeasonService.CurrentSeasonId` or `ActiveSeasonId`
- Users switch seasons via the **SeasonsPage** ? "Set Active" button

---

## ?? Data Flow

```
User Creates Team
    ?
Team.SeasonId = CurrentSeasonId
    ?
Added to LeagueData.Teams list
    ?
Saved to:
    1. JSON file (data.json)
    2. SQLite database (league.db)
    ?
Filtered by SeasonId when displaying
```

---

## ?? Key Takeaways

1. ? **Teams ARE separated by season** - Each team has a `SeasonId`
2. ? **Same team name can exist in multiple seasons** - Different records
3. ? **One Teams list contains all seasons** - Filtered by `SeasonId` when queried
4. ? **Cascade delete** - Delete season ? deletes all teams in that season
5. ? **Indexed for performance** - Fast queries by `SeasonId`
6. ? **Consistent with other entities** - Players, Venues, Divisions all work the same way

---

## ?? Example Scenario

### Season 2023
- Team A (Id: guid1, SeasonId: season2023)
- Team B (Id: guid2, SeasonId: season2023)

### Season 2024
- Team A (Id: guid3, SeasonId: season2024)  ? Different ID!
- Team C (Id: guid4, SeasonId: season2024)

**Both "Team A" records exist in the database**, but they:
- Have different IDs
- Have different SeasonIds
- Are completely independent
- Can have different venues, players, divisions

---

## ??? Database Query Examples

```csharp
// Get all teams for a specific season
var teams = await context.Teams
    .Where(t => t.SeasonId == seasonId)
    .ToListAsync();

// Get team with its division
var team = await context.Teams
    .Include(t => t.Division)  // Navigation property would need to be added
    .FirstOrDefaultAsync(t => t.Id == teamId);

// Count teams per season
var teamCounts = await context.Teams
    .GroupBy(t => t.SeasonId)
    .Select(g => new { SeasonId = g.Key, Count = g.Count() })
    .ToListAsync();
```

---

## ? Summary

**Question:** Are teams separate for each season?  
**Answer:** **YES - completely separate!**

- Each team record has a `SeasonId` linking it to a season
- You can have the same team name across multiple seasons
- Teams are filtered by season in all UI pages
- Deleting a season removes all its teams
- SQLite database enforces this with foreign keys and cascade delete

**This design allows you to:**
- Track teams across multiple seasons
- Keep historical data
- Start fresh each season with new teams
- Maintain data integrity with automatic cleanup
