# WDPL (Wdpl2)

.NET 9 MAUI app for managing the Wellington District Pool League.

The durable architecture and domain rules for this project live in
`.github/copilot-instructions.md` — read as part of this file:

@.github/copilot-instructions.md

Keep that file as the single source of truth for domain rules; add to it rather
than duplicating rules here.

## Layout
- Solution `wdpl2.sln` → app `wdpl2/wdpl2.csproj`, tests `wdpl2.Tests/wdpl2.Tests.csproj` (xUnit).
- SDK pinned by `global.json` to 9.0.100 (`rollForward: latestMajor`).
- Targets: `net9.0-android;net9.0-ios;net9.0-maccatalyst;net9.0-windows10.0.19041.0`.
  Tests target `net9.0-windows10.0.19041.0` only.

## Build & test
Windows is the only target that builds and runs locally — iOS builds via the
`.github/workflows/ios-build.yml` runner.

```powershell
# fastest inner loop: Windows target only
dotnet build wdpl2\wdpl2.csproj -f net9.0-windows10.0.19041.0 -v minimal

# tests
dotnet test wdpl2.Tests\wdpl2.Tests.csproj -v minimal

# full restore + all targets (slow; after dependency or workload changes)
.\restore-and-build.ps1
```

- Always pass `-f net9.0-windows10.0.19041.0` for a quick check. Building all four
  target frameworks takes many minutes and needs the Android/iOS workloads.
- The csproj sets a wide `NoWarn` list deliberately (MVVMTK*, CS0618, CS8618,
  CA1416, XC0022/XC0025, XA0141, XA4301, CA1860, SYSLIB1045). Don't "fix" these by
  editing the suppression list without asking.
- Documentation-only changes don't need a build.

## Stack notes
- MVVM via `CommunityToolkit.Mvvm` 8.4, UI via `CommunityToolkit.Maui` 9.1.
  The codebase mixes XAML code-behind with view models — follow the local pattern
  per page rather than assuming full MVVM.
- EF Core 9 + SQLite (`wdpl2/Data/LeagueContext.cs`), alongside the static
  `DataStore` JSON snapshot bridge. Persistence is hybrid — read both paths before
  changing save/load behaviour.
- Also in play: SkiaSharp (Logo Studio), Plugin.Maui.OCR (scorecard scanning),
  Plugin.LocalNotification, FluentFTP + `wdpl2/web-backend` (PHP/MySQL) for publishing.

## Housekeeping
- Repo root has ~20 empty `_*.ps1` / `patch-*.ps1` stubs and a 10 MB
  `wdpl2/build_warnings.txt` left over from past sessions. They're dead weight —
  ignore them, and don't treat them as part of the build.
- Use repo-relative paths in code and docs, never `C:\Users\bobgc\...`.

## Running it (VS Code / CLI)
The Windows build is unpackaged, so the exe runs straight from `bin` — no
deployment or packaging step:

```powershell
dotnet build wdpl2\wdpl2.csproj -f net9.0-windows10.0.19041.0 -v minimal
.\wdpl2\bin\Debug\net9.0-windows10.0.19041.0\win10-x64\Wdpl2.exe
```

Verified working from VS Code (no Visual Studio needed): build ~1m20s,
0 errors / 46 pre-existing warnings.

- Required VS Code extensions (already installed): `ms-dotnettools.csdevkit`,
  `ms-dotnettools.csharp`, `ms-dotnettools.dotnet-maui`. F5 debugging works via
  the MAUI extension's target picker.
- The app reports an empty `MainWindowTitle` (WinUI 3 quirk) — check
  `MainWindowHandle` instead when scripting against the running process.
