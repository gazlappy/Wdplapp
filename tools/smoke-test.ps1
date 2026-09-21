<#
.SYNOPSIS
    Opens every tab of the running app and fails any that crash, freeze or stick.

.DESCRIPTION
    MAUI pages cannot be built inside the unit-test runner, so page bugs have
    only ever shown up when someone clicked. This drives the real Windows app
    through UI Automation the way a person would:

      1. Cuts short the first visit to every tab - leaving before the page
         has finished opening, then coming straight back. This is what left
         the Website tab on a spinner for good, and it can only be tried on a
         fresh launch, before each page has been built once.
      2. Opens every tab and waits for it to settle. A tab fails if the app
         stops responding, exits, or is still showing a loading message.
      3. Flips out of and back into every tab quickly. The tab must still
         settle.

    It only reads: nothing is clicked except the tabs themselves. It uses the
    league on this machine, so run it against a build you are happy to open
    your own data with.

.PARAMETER Build
    Build the Windows target first.

.PARAMETER KeepOpen
    Leave the app running afterwards.

.PARAMETER Screenshots
    Folder to save a screenshot of each tab into. Omit to skip screenshots.

.EXAMPLE
    .\tools\smoke-test.ps1 -Build
    .\tools\smoke-test.ps1 -Screenshots .\smoke-shots -KeepOpen
#>
param(
    [switch]$Build,
    [switch]$KeepOpen,
    [string]$Screenshots = "",
    [int]$SettleSeconds = 6
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$exe  = Join-Path $repo 'wdpl2\bin\Debug\net9.0-windows10.0.19041.0\win10-x64\Wdpl2.exe'

if ($Build) {
    if (Get-Process Wdpl2 -ErrorAction SilentlyContinue) {
        throw "The app is running and would lock the build. Close it first."
    }
    Write-Host "Building..."
    & dotnet build (Join-Path $repo 'wdpl2\wdpl2.csproj') -f net9.0-windows10.0.19041.0 -v minimal | Out-Null
    if (-not $?) { throw "Build failed." }
}
if (-not (Test-Path $exe)) { throw "No build at $exe. Run with -Build." }
if (Get-Process Wdpl2 -ErrorAction SilentlyContinue) {
    throw "The app is already running. Close it so the test starts from a clean launch."
}

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class SmokeWin {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
if ($Screenshots) {
    Add-Type -AssemblyName System.Drawing
    New-Item -ItemType Directory -Force -Path $Screenshots | Out-Null
}

$A = [System.Windows.Automation.AutomationElement]
$Tree = [System.Windows.Automation.TreeScope]::Descendants
$Any = [System.Windows.Automation.Condition]::TrueCondition

function Get-All($root) { $root.FindAll($Tree, $Any) }

function Get-Tabs($root) {
    Get-All $root | Where-Object { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.TabItem' }
}

function Select-Tab($root, [string]$name) {
    $tab = Get-Tabs $root | Where-Object { $_.Current.Name -eq $name } | Select-Object -First 1
    if ($null -eq $tab) { return $false }
    $tab.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    return $true
}

# Words that mean a page has not finished opening. Checked only after the
# settle time, so a spinner that clears on its own does not count.
$stuckPattern = '^(Loading|Opening your league)|Loading Website Builder'

function Test-Settled($proc, $root) {
    $proc.Refresh()
    if ($proc.HasExited) { return "the app exited (code $($proc.ExitCode))" }
    if (-not $proc.Responding) { return "the app stopped responding" }
    $stuck = Get-All $root | Where-Object { $_.Current.Name -match $stuckPattern } | Select-Object -First 1
    if ($stuck) { return "still showing '$($stuck.Current.Name)'" }
    return ""
}

function Save-Shot($proc, [string]$name) {
    if (-not $Screenshots) { return }
    $r = New-Object SmokeWin+RECT
    [void][SmokeWin]::GetWindowRect($proc.MainWindowHandle, [ref]$r)
    $bmp = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
    $safe = ($name -replace '[^A-Za-z0-9]+', '-')
    $bmp.Save((Join-Path $Screenshots "$safe.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}

# ---------------------------------------------------------------- launch

Write-Host "Launching $exe"
$proc = Start-Process $exe -PassThru
$deadline = (Get-Date).AddSeconds(60)
do { Start-Sleep -Milliseconds 500; $proc.Refresh() }
until ($proc.HasExited -or $proc.MainWindowHandle -ne [IntPtr]::Zero -or (Get-Date) -gt $deadline)
if ($proc.HasExited) { throw "The app exited during start-up (code $($proc.ExitCode))." }
if ($proc.MainWindowHandle -eq [IntPtr]::Zero) { throw "No window appeared within 60 seconds." }

# Maximised, so no tab is hidden in the overflow menu.
[void][SmokeWin]::ShowWindow($proc.MainWindowHandle, 3)
[void][SmokeWin]::SetForegroundWindow($proc.MainWindowHandle)
$root = $A::FromHandle($proc.MainWindowHandle)

# The league has to finish opening before there are tabs to click.
$deadline = (Get-Date).AddSeconds(60)
do { Start-Sleep -Milliseconds 500; $names = @(Get-Tabs $root | ForEach-Object { $_.Current.Name }) }
until ($names.Count -gt 0 -or (Get-Date) -gt $deadline)
if ($names.Count -eq 0) { throw "No tabs appeared within 60 seconds of launch." }
Write-Host ("Found {0} tabs: {1}" -f $names.Count, ($names -join ', '))

$results = @()
$firstTab = $names[0]

# ---------------------------------------------------------------- interrupt each first visit
#
# First, because it only works once. Shell builds a tab's page the first time
# the tab is opened and keeps it; a page that sets itself up in OnAppearing and
# is left mid-way through has only that one chance to go wrong. The Website
# hang was exactly this: leave during the first visit, come back, and the page
# thought it had already finished. Opening every tab properly first - as the
# checks below do - would build every page cleanly and hide it.

foreach ($name in $names) {
    if ($name -eq $firstTab) { continue }
    [void](Select-Tab $root $name)
    [void](Select-Tab $root $firstTab)
    [void](Select-Tab $root $name)
    Start-Sleep -Seconds $SettleSeconds
    $problem = Test-Settled $proc $root
    $results += [pscustomobject]@{ Tab = $name; Check = 'first visit cut short'; Result = $(if ($problem) { 'FAIL' } else { 'ok' }); Detail = $problem }
    if ($proc.HasExited) { break }
}

# ---------------------------------------------------------------- open each tab

if (-not $proc.HasExited) { foreach ($name in $names) {
    [void](Select-Tab $root $name)
    Start-Sleep -Seconds $SettleSeconds
    $problem = Test-Settled $proc $root
    if (-not $problem) { Save-Shot $proc $name }
    $results += [pscustomobject]@{ Tab = $name; Check = 'opens'; Result = $(if ($problem) { 'FAIL' } else { 'ok' }); Detail = $problem }
    if ($proc.HasExited) { break }
} }

# ---------------------------------------------------------------- flip in and out fast

if (-not $proc.HasExited) {
    foreach ($name in $names) {
        if ($name -eq $firstTab) { continue }
        foreach ($pause in 250, 60, 0) {
            [void](Select-Tab $root $firstTab); if ($pause) { Start-Sleep -Milliseconds $pause }
            [void](Select-Tab $root $name); if ($pause) { Start-Sleep -Milliseconds $pause }
        }
        Start-Sleep -Seconds $SettleSeconds
        $problem = Test-Settled $proc $root
        $results += [pscustomobject]@{ Tab = $name; Check = 'fast flip'; Result = $(if ($problem) { 'FAIL' } else { 'ok' }); Detail = $problem }
        if ($proc.HasExited) { break }
    }
}

# ---------------------------------------------------------------- report

$results | Format-Table -AutoSize | Out-String -Width 200 | Write-Host
$failed = @($results | Where-Object { $_.Result -eq 'FAIL' })

if (-not $KeepOpen -and -not $proc.HasExited) {
    [void]$proc.CloseMainWindow()
    Start-Sleep -Seconds 3
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
}

if ($failed.Count -gt 0) {
    Write-Host ("{0} check(s) failed." -f $failed.Count) -ForegroundColor Red
    exit 1
}
Write-Host ("All {0} checks passed." -f $results.Count) -ForegroundColor Green
exit 0
