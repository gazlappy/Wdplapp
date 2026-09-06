# Copilot Instructions

## Project Guidelines
- WDPL stands for Wellington District Pool League. The project (wdpl2/Wdpl2) is a .NET 9 MAUI app for managing the Wellington District Pool League. Use MAUI, not Xamarin.Forms.
- WDPL has separate summer and winter seasons within a year, with division naming changing between numbered divisions (1st, 2nd) and colored divisions (red, green, yellow). Imports must not collapse these distinct seasonal division schemes into one season.
- Summer and winter can share a calendar year but remain separate seasons. Preserve season terms and year ranges, including pre-2000 years. Do not merge seasons solely by overlapping year, substring, or start/end date. Automatic links require unambiguous season identity.

## Fixture Scheduling
- WDPL fixture scheduling requires all matches for a given week on the same night, no team playing more than one match that night, and no more than one home match on a venue/table that night. Shared-table conflicts must not be solved by moving matches to another night.
- WDPL printable fixtures sheet should have one shared set of dates and numbered home/away pairings for all divisions, with each division's team-number key derived from actual fixtures rather than alphabetical ordering. Do not display separate division pairing grids or silently change saved matches to force alignment.

## Technology and Structure
- Solution: `wdpl2.sln`. App: `wdpl2/wdpl2.csproj`. Tests: `wdpl2.Tests/wdpl2.Tests.csproj` (xUnit).
- Persistence uses Entity Framework Core and SQLite.
- Core data includes seasons, divisions, venues, teams, players, fixtures, frame results, and competitions. A legitimate team may share a venue name; name equality alone does not justify deletion or merging.
- Use repository-relative paths, not developer-specific checkout paths.

## Season and Entity Identity
- Numbered divisions are winter evidence and colored divisions are summer evidence in the current archive importer. Conflicting evidence requires review.
- Alphabet navigation labels (A, B, C, etc.) and index/placeholder text are not players. Preserve legitimate names and initials such as J. Smith.
- Avoid speculative fuzzy identity merges that could collapse different players, teams, or seasons.

## Import Workflow and Safeguards
- The unified import entry uses a choose/review/import flow. Review detected data before saving.
- Import source flows include Access, SQL, Paradox database folders, Word, Excel, CSV, HTML, and PDF, with format and platform/provider limitations.
- Multiple HTML files use batch preview. Scan imports discover files, group by season, analyze contents, and present groups for review.
- Classify using explicit source evidence such as page titles, division names, and table headers, not arbitrary links or positional cells alone.
- Explain unresolved/conflicting season groups and exclude them from automatic import rather than guessing.
- Preserve private import workspaces, transactional commits, relationship/placement validation, and locked-season protection.
- Parser fixes do not automatically repair existing imported data. Cleanup or re-import is separate work; do not silently delete user data.
- Large archives are a real use case (the reported scan had approximately 43,000 files). Distinguish representative regression coverage from full-archive validation.

## Key Import Files
- `wdpl2/Views/Import/HistoricalImportPage.xaml` and code-behind: unified entry and step-based workflow.
- `wdpl2/Views/Import/SmartImportPage.xaml.cs`: scan, season review, and import orchestration.
- `wdpl2/Features/Import/Html/LeagueFileDiscoveryService.cs`: discovery, season detection/grouping, and analysis.
- `wdpl2/Features/Import/Html/HtmlLeagueParser.cs`: HTML classification and extraction of standings, results, ratings, profiles, player lists, doubles, and fixtures.
- `wdpl2/Views/Import/BatchImportPreviewPage.xaml.cs`: selected-file aggregation, preview, and batch import.
- `wdpl2/Helpers/DivisionHelper.cs`: division normalization and matching.
- `wdpl2.Tests/Features/Import/Html/HtmlLeagueParserTests.cs` and `ArchiveClassificationTests.cs`: parser and classification regressions.

## App Architecture and Shared State
- `wdpl2/MauiProgram.cs` configures MAUI Community Toolkit, local notifications, OCR, SkiaSharp, fonts, and DI. Registration is split into `AddPersistence`, `AddCoreAppServices`, `AddNotifications`, `AddViewModels`, and `AddPages` extension methods.
- `wdpl2/App.xaml.cs` initializes the database, bridges the static datastore to DI, loads data, applies the saved theme, initializes season selection, then creates `AppShell`.
- `wdpl2/AppShell.xaml` defines tab navigation for Dashboard, Seasons, Divisions, Teams, Players, Venues, Fixtures, Calendar, Competitions, Tables, Analytics, Import, Logos, Website, Web Inbox, Settings, and Pool.
- The UI mixes XAML/code-behind with CommunityToolkit.Mvvm view models. Follow the local pattern rather than assuming every page is fully MVVM.
- `wdpl2/ViewModels/BaseViewModel.cs` provides observable loading/status/season state, cancellation on season changes, and subscription cleanup. Preserve stale-load cancellation and event cleanup.
- `ISeasonService`/`SeasonService` is the shared singleton for current season selection and `SeasonChanged` notifications; `SeasonService.Current` supports non-DI callers. Do not invent independent current-season state in individual pages.
- Seasons uses `SeasonLibraryViewModel` for preview selection. Previewing a season and activating it are separate actions; preserve that distinction.
- Manual new-season setup uses `Views/Seasons/SeasonSetupPage.ManualRoster.cs` and `Services/Season/ManualSeasonRoster.cs`: optionally add historical or new teams, browse players by explicit source season/team, assign them to any drafted destination team, and review before saving. Selection changes do not save or modify historical records. The season and roster commit together through the existing workspace transaction and remain inactive. Reuse explicit record/global identities, not name-based merges. Manual roster copying does not carry divisions, venue/table links, captain credentials, results, availability, or transfers; configure new-season placements separately.
  - Optional historical venue selection copies names, addresses, notes and tables with fresh venue/table IDs in the same transaction. Deduplicate only repeated selections of the same source venue record (venues have no global identity), not matching names across seasons. Review/remove selections before saving; teams are not automatically assigned to copied venues or tables.
  - Historical venue selection must also be available after season creation, not only during manual setup. Align the Venues tab with the newer Seasons page appearance and explicit configuration-season workflow.
- `IThemeService`/`ThemeService` handles shared theme state; shared XAML resources live in `wdpl2/Resources/Styles/`.
- Folder layout and namespaces are not identical: domain models commonly use `Wdpl2.Models`, pages use `Wdpl2.Views`, and many feature classes use `Wdpl2.Services`. Check declarations and `GlobalUsings.cs` before adding imports.

## Persistence Details
- Persistence is hybrid, not SQLite-only: `wdpl2/Data/LeagueContext.cs` is the EF Core context; `IDataStore`/`SqliteDataStore` expose data access; static partial `Wdpl2.DataStore` maintains the shared `LeagueData` snapshot and JSON persistence bridge.
- Startup loads entities from EF Core and settings from JSON. `DataStore.Save()` writes JSON, synchronizes entity tables, and can trigger backups and optional cloud sync. `SaveJsonOnly()` avoids entity synchronization and cloud pushes for non-entity changes.
- `LeagueContext`, `IDataStore`/`SqliteDataStore`, `DataMigrationService`, and `BackupService` are registered as transient services. Verify lifetimes and shared state before changing persistence behavior.
- `wdpl2/Services/Persistence/` contains migration, backup, integrity validation, schema configuration, and partial import persistence code. Inspect both static and injected datastore paths when changing saving/loading; do not bypass existing safeguards.

## App-Wide Feature Map
- `wdpl2/Domain/Models/` and `Domain/Settings/`: league entities and app/calendar/website configuration, including doubles, player availability, and transfers.
- `wdpl2/Domain/Fixtures/`: fixture generation, validation, season scheduling, clash resolution, and schedule snapshots. Calendar UI includes exclusions and season scenarios.
- `wdpl2/Domain/Standings/`: standings calculation/sorting and player ratings. Reuse these calculations rather than creating inconsistent UI-specific formulas.
- `wdpl2/Domain/Competitions/` and `Views/Competitions/`: competition generation, setup wizard, participants, groups, brackets, rounds, and venue assignment; the page is split across partial files.
- `wdpl2/Views/Players/`, `Views/Analytics/`, `Views/Achievements/`, and `Services/Stats/`: player profiles, results, frame/career statistics, team analytics, what-if simulation, achievements, and season awards.
- `wdpl2/Services/Season/`: shared season selection and season copying. `Views/Seasons/` includes the season library, setup, and comparison.
- `wdpl2/Services/Search/`, `Services/Export/`, `Services/Media/`, and `Services/Notifications/`: search, local/SQL exports, image optimization and scorecard OCR (including an Azure Vision implementation), and match reminders. Service presence does not imply external services are configured.

## Website, Publishing, and Web Inbox
- `wdpl2/Features/WebsiteBuilder/` contains settings views, generated HTML/CSS/components, JSON data generation, template pages, fixture sheets, and live-score output. Website generation is split across partial `WebsiteGenerator` files.
- Website settings cover branding, layout, colors, league data pages, history, galleries, rules/contact content, entry forms, captain access, SEO, and deployment.
- Logo Studio uses `Views/Logos/` and `Features/WebsiteBuilder/Logo/`; SkiaSharp supports logo rendering, design recipes, layers, and shape/icon catalogs.
- `wdpl2/Services/Cloud/` contains GitHub Pages publishing, optional GitHub data sync, FTP upload, and backend deployment. Do not assume credentials are configured or publishing is enabled, and never store credentials in instructions.
- `wdpl2/web-backend/` is a separate PHP/MySQL web backend with admin, captain, and public APIs, a captain web interface, and SQL setup scripts. PHP/HTML/JS backend assets are bundled by the MAUI project for deployment.
- `wdpl2/Services/Inbox/`, `ViewModels/Inbox/`, and `Views/Inbox/` connect the app to web submissions/publishing, including match results and roster changes. The documented workflow keeps the MAUI app authoritative and reviews/applies pending submissions.
- When changing web contracts, inspect both C# clients/models and PHP endpoints/schema. The backend README contains older setup/future-work text; verify current implementation rather than assuming the inbox is unimplemented.

## Games, Resources, and Validation
- `wdpl2/Features/Games/` contains a games library, Pool, Breakout, Memory, Snake, and RetroFps. Pool generates embedded HTML/JavaScript from C# modules under `Pool/Engine/`, covering physics, rendering, input, AI, audio, replay, spin, and shot controls.
- `wdpl2/Resources/` holds styles, fonts, rules, raw web assets, and 3D models. `Helpers/` contains shared responsive UI, panels, WebView, emoji, and division helpers.
- The app project declares Android, iOS, Mac Catalyst, and Windows targets. Declared targets are not proof that every platform has been tested; honor platform-specific code and dependencies.
- `wdpl2.Tests/` mirrors domain, services, view models, features, and views using xUnit. Run relevant regressions and build for code changes; documentation-only changes do not require compiling the app.
- Legacy archive/sample folders are test/reference data, not the application's architecture. `wdpl2/Docs/` includes Paradox format documentation and scheduling references; inspect project exclusions before treating files as compiled code.

## Maintaining Context
- These are durable project notes, not a substitute for inspecting current code. Update them when architecture or domain requirements change.
- Do not store transient plans, build/test counts, or debugging progress here. Keep persistent instructions focused on durable architecture/domain rules, not transient build results or session progress.

## General Familiarization
- Retain broad, verified app familiarization context across all major features and architecture to avoid repeating orientation in future chats.