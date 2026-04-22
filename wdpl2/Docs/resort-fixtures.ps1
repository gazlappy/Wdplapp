param(
    [string]$Source = "$PSScriptRoot\schedule-snapshot-Summer_2026-20260422-232407.json",
    [string]$Destination
)

$ErrorActionPreference = 'Stop'

if (-not $Destination) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $Destination = Join-Path $PSScriptRoot "schedule-snapshot-Summer_2026-resorted-$stamp.json"
}

# Compile a small C# solver – PowerShell recursion is too slow for the search.
$cs = @'
using System;
using System.Collections.Generic;

public class Fixture
{
    public string Id;
    public int HomeTeam;
    public int AwayTeam;
    public int Slot;
    public int AssignedNight = -1;
}

public class Solver
{
    public int Nights;
    public int[] Capacity;            // per night
    public int[] Used;                // per night
    public bool[,] TeamBusy;          // [night, team]
    public bool[,] SlotBusy;          // [night, slot]
    public List<Fixture> Pending;
    public bool[] Placed;
    public long Steps;

    public bool Solve()
    {
        int idx = PickNext();
        if (idx == -1) return true;
        var f = Pending[idx];

        // Build candidate nights ordered by least-loaded first (load balancing)
        var cands = new List<int>();
        for (int n = 0; n < Nights; n++)
        {
            if (Used[n] >= Capacity[n]) continue;
            if (TeamBusy[n, f.HomeTeam]) continue;
            if (TeamBusy[n, f.AwayTeam]) continue;
            if (SlotBusy[n, f.Slot]) continue;
            cands.Add(n);
        }
        cands.Sort((a, b) => Used[a].CompareTo(Used[b]));

        Placed[idx] = true;
        foreach (var n in cands)
        {
            Steps++;
            f.AssignedNight = n;
            Used[n]++;
            TeamBusy[n, f.HomeTeam] = true;
            TeamBusy[n, f.AwayTeam] = true;
            SlotBusy[n, f.Slot] = true;

            if (Solve()) return true;

            Used[n]--;
            TeamBusy[n, f.HomeTeam] = false;
            TeamBusy[n, f.AwayTeam] = false;
            SlotBusy[n, f.Slot] = false;
            f.AssignedNight = -1;
        }
        Placed[idx] = false;
        return false;
    }

    // MRV: choose unplaced fixture with fewest viable nights
    int PickNext()
    {
        int best = -1; int bestCount = int.MaxValue;
        for (int i = 0; i < Pending.Count; i++)
        {
            if (Placed[i]) continue;
            var f = Pending[i];
            int c = 0;
            for (int n = 0; n < Nights; n++)
            {
                if (Used[n] >= Capacity[n]) continue;
                if (TeamBusy[n, f.HomeTeam]) continue;
                if (TeamBusy[n, f.AwayTeam]) continue;
                if (SlotBusy[n, f.Slot]) continue;
                c++;
                if (c >= bestCount) break;
            }
            if (c == 0) return i;     // dead-end – return immediately to fail fast
            if (c < bestCount) { bestCount = c; best = i; if (c == 1) return i; }
        }
        return best;
    }
}
'@

Add-Type -TypeDefinition $cs -Language CSharp

$json = Get-Content $Source -Raw | ConvertFrom-Json

# Index teams and slots to ints
$teamHome = @{}
foreach ($t in $json.teams) {
    $teamHome[$t.id] = [pscustomobject]@{
        venueId    = $t.venueId
        venueName  = $t.venueName
        tableId    = $t.tableId
        tableLabel = $t.tableLabel
    }
}

$teamIdx = @{}
$i = 0
foreach ($t in $json.teams) { $teamIdx[$t.id] = $i; $i++ }
$teamCount = $json.teams.Count

$slotIdx = @{}
$si = 0
foreach ($t in $json.teams) {
    $key = "$($t.venueId)|$($t.tableId)"
    if (-not $slotIdx.ContainsKey($key)) { $slotIdx[$key] = $si; $si++ }
}
# Also add any slots used by played fixtures that aren't a team's home table
foreach ($p in $json.playedFixtures) {
    $key = "$($p.venueId)|$($p.tableId)"
    if (-not $slotIdx.ContainsKey($key)) { $slotIdx[$key] = $si; $si++ }
}
$slotCount = $si

$nights = @($json.matchNights)
$nightCount = $nights.Count
$nightIdxByDate = @{}
for ($k = 0; $k -lt $nightCount; $k++) { $nightIdxByDate[$nights[$k]] = $k }

$capacity = New-Object 'int[]' $nightCount
for ($k = 0; $k -lt $nightCount; $k++) { $capacity[$k] = 6 }
$used = New-Object 'int[]' $nightCount
$teamBusy = New-Object 'bool[,]' $nightCount, $teamCount
$slotBusy = New-Object 'bool[,]' $nightCount, $slotCount

foreach ($p in $json.playedFixtures) {
    $n = $nightIdxByDate[$p.date]
    $used[$n]++
    $teamBusy[$n, $teamIdx[$p.homeTeamId]] = $true
    $teamBusy[$n, $teamIdx[$p.awayTeamId]] = $true
    $slotBusy[$n, $slotIdx["$($p.venueId)|$($p.tableId)"]] = $true
}

# Build pending fixtures (using home team's home venue/table)
$pendingList = New-Object 'System.Collections.Generic.List[Fixture]'
$pendingMeta = New-Object 'System.Collections.Generic.List[object]'
foreach ($u in $json.unplayedFixtures) {
    $hi = $teamHome[$u.homeTeamId]
    $slotKey = "$($hi.venueId)|$($hi.tableId)"
    if (-not $slotIdx.ContainsKey($slotKey)) { $slotIdx[$slotKey] = $slotCount; $slotCount++ }
    $f = New-Object Fixture
    $f.Id = $u.id
    $f.HomeTeam = $teamIdx[$u.homeTeamId]
    $f.AwayTeam = $teamIdx[$u.awayTeamId]
    $f.Slot = $slotIdx[$slotKey]
    [void]$pendingList.Add($f)
    [void]$pendingMeta.Add([pscustomobject]@{
        id           = $u.id
        homeTeamId   = $u.homeTeamId
        homeTeam     = $u.homeTeam
        awayTeamId   = $u.awayTeamId
        awayTeam     = $u.awayTeam
        venueId      = $hi.venueId
        venueName    = $hi.venueName
        tableId      = $hi.tableId
        tableLabel   = $hi.tableLabel
    })
}

$solver = New-Object Solver
$solver.Nights   = $nightCount
$solver.Capacity = $capacity
$solver.Used     = $used
$solver.TeamBusy = $teamBusy
$solver.SlotBusy = $slotBusy
$solver.Pending  = $pendingList
$solver.Placed   = New-Object 'bool[]' $pendingList.Count

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$ok = $solver.Solve()
$sw.Stop()
Write-Host ("Solve: ok={0}  steps={1}  ms={2}" -f $ok, $solver.Steps, $sw.ElapsedMilliseconds)

if (-not $ok) { throw "No clash-free assignment found." }

# Build output
$newUnplayed = @()
for ($k = 0; $k -lt $pendingList.Count; $k++) {
    $m = $pendingMeta[$k]
    $f = $pendingList[$k]
    $newUnplayed += [pscustomobject]@{
        id              = $m.id
        currentDate     = $nights[$f.AssignedNight]
        homeTeamId      = $m.homeTeamId
        homeTeam        = $m.homeTeam
        awayTeamId      = $m.awayTeamId
        awayTeam        = $m.awayTeam
        currentVenueId  = $m.venueId
        currentVenue    = $m.venueName
        currentTableId  = $m.tableId
        currentTable    = $m.tableLabel
    }
}
$newUnplayed = $newUnplayed | Sort-Object currentDate, currentVenue, currentTable | ForEach-Object {
    [ordered]@{
        id              = $_.id
        currentDate     = $_.currentDate
        homeTeamId      = $_.homeTeamId
        homeTeam        = $_.homeTeam
        awayTeamId      = $_.awayTeamId
        awayTeam        = $_.awayTeam
        currentVenueId  = $_.currentVenueId
        currentVenue    = $_.currentVenue
        currentTableId  = $_.currentTableId
        currentTable    = $_.currentTable
    }
}

$out = [ordered]@{
    season                  = $json.season
    venues                  = $json.venues
    teams                   = $json.teams
    matchNights             = $json.matchNights
    blackoutDates           = $json.blackoutDates
    playedFixtures          = $json.playedFixtures
    unplayedFixtures        = $newUnplayed
    sharedHomeTableWarnings = $json.sharedHomeTableWarnings
}

$out | ConvertTo-Json -Depth 12 | Set-Content -Path $Destination -Encoding UTF8

# Also emit a Plan file (the shape ScheduleSnapshotService.Apply expects).
$planAssignments = @()
for ($k = 0; $k -lt $pendingList.Count; $k++) {
    $m = $pendingMeta[$k]
    $f = $pendingList[$k]
    $planAssignments += [ordered]@{
        fixtureId = $m.id
        date      = $nights[$f.AssignedNight]
        venueId   = $m.venueId
        tableId   = $m.tableId
    }
}
$plan = [ordered]@{ assignments = $planAssignments }
$planPath = $Destination -replace '\.json$', '-plan.json'
$plan | ConvertTo-Json -Depth 6 | Set-Content -Path $planPath -Encoding UTF8

Write-Host ""
Write-Host "Wrote snapshot: $Destination" -ForegroundColor Green
Write-Host "Wrote plan    : $planPath  (import THIS one)" -ForegroundColor Green
Write-Host ""
Write-Host "Fixtures per night:"
foreach ($n in $nights) {
    $c = ($json.playedFixtures | Where-Object { $_.date -eq $n }).Count + ($newUnplayed | Where-Object { $_.currentDate -eq $n }).Count
    Write-Host ("  {0} : {1}" -f $n, $c)
}

# Verify
$problems = @()
foreach ($n in $nights) {
    $all = @()
    $all += $json.playedFixtures   | Where-Object { $_.date -eq $n }        | ForEach-Object { [pscustomobject]@{ home=$_.homeTeam; away=$_.awayTeam; slot="$($_.venue)/$($_.table)" } }
    $all += $newUnplayed           | Where-Object { $_.currentDate -eq $n } | ForEach-Object { [pscustomobject]@{ home=$_.homeTeam; away=$_.awayTeam; slot="$($_.currentVenue)/$($_.currentTable)" } }
    $tc = @{}
    foreach ($x in $all) { foreach ($t in @($x.home, $x.away)) { if (-not $tc.ContainsKey($t)) { $tc[$t] = 0 }; $tc[$t]++ } }
    foreach ($k in $tc.Keys) { if ($tc[$k] -gt 1) { $problems += "[$n] team double-booked: $k" } }
    $sc = @{}
    foreach ($x in $all) { if (-not $sc.ContainsKey($x.slot)) { $sc[$x.slot] = 0 }; $sc[$x.slot]++ }
    foreach ($k in $sc.Keys) { if ($sc[$k] -gt 1) { $problems += "[$n] table double-booked: $k" } }
}
if ($problems.Count -gt 0) {
    Write-Host ""
    Write-Host "PROBLEMS:" -ForegroundColor Red
    $problems | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
} else {
    Write-Host ""
    Write-Host "Verification passed: no team or table clashes." -ForegroundColor Green
}
