# Copilot Instructions

## Project Guidelines
- WDPL stands for Wellington District Pool League. The project (wdpl2/Wdpl2) is a .NET 9 MAUI app built to manage that league and now being made into a product any pool league can use. Use MAUI, not Xamarin.Forms. The scheduling and season rules below are WDPL's and the app implements them.
- WDPL has separate summer and winter seasons within a year, with division naming changing between numbered divisions (1st, 2nd) and colored divisions (red, green, yellow). Imports must not collapse these distinct seasonal division schemes into one season.
- Summer and winter can share a calendar year but remain separate seasons. Preserve season terms and year ranges, including pre-2000 years. Do not merge seasons solely by overlapping year, substring, or start/end date. Automatic links require unambiguous season identity.

## Product Name and App Identity
- The name people see comes from `wdpl2/Product.cs` (`Product.Name`), or from the league's own name in website settings where the text is about the league. Do not hardcode "WDPL" or "Wellington" in anything a user sees.
- The app's internal identity is separate and must not be renamed without a data migration: `ApplicationId` (`com.wdpl2.app`) and the `wdpl2` folder under `FileSystem.AppDataDirectory` decide where a league is stored, so changing either opens the app empty with the league left behind. The same applies to the backend's `wdpl_*` table names and the `backend: wdpl` ping identifier. The `Wdpl2` namespace and assembly name are internal and not worth the churn to rename.
- There is no default server address (`WebConnection.DefaultBaseUrl` is empty). A default pointing at one league's server would send every new install's admin password there.

## Fixture Scheduling
- WDPL fixture scheduling requires all matches for a given week on the same night, no team playing more than one match that night, and no more than one home match on a venue/table that night. Shared-table conflicts must not be solved by moving matches to another night.
- WDPL shared fixture draw must assign teams sharing a home venue/table to consecutive odd/even slots (1/2, 3/4, 5/6, 7/8), never pairs such as 4/5. Number 1 remains in the first fixture row and alternates home/away. Preserve these slot identities when rendering the fixture sheet.
- WDPL printable fixtures sheet should have one shared set of dates and numbered home/away pairings for all divisions, with each division's team-number key derived from actual fixtures rather than alphabetical ordering. Do not display separate division pairing grids or silently change saved matches to force alignment.
- WDPL fixtures sheets should display competition dates as dated cards alongside league-night date cards, using the user-visible setting label 'Show competition / event date cards', not the internal ShowSpecialEvents name. Fixtures sheets should include only dates within the selected active season's start/end inclusive and show competition/event dates only in the main chronological date-card grid, not a duplicate key-dates section at the bottom.
- WDPL team-number customization swaps paired odd/even blocks within the selected division only, with table-sharing partners following. Fixture-number customization must also permit within-pair reversals (1↔2) and opposite-parity paired-block exchanges when all fixture/table rules pass. When a division-local paired-block swap conflicts with table partners in another division, offer an explicit combined cross-division swap, preview all affected teams/fixtures, and validate/apply atomically rather than forcing sequential swaps that cannot pass individually. Other divisions remain unchanged; reject local moves violating cross-division table-partner or fixture rules. Offer preview/confirmation both during generation and afterward only for a completely unplayed season. This includes offering explicit confirmation of a validated combined paired-block swap when cross-division table partners would otherwise block a local move. Follow cascading linked divisions, apply accepted changes together, preserve unrelated divisions, and leave drafts untouched on decline. Prefer compact number controls and a responsive split before/after preview, with Preview, Apply to draft, and Save as separate actions rather than stretched full-width controls.

## Technology and Structure
- Solution: `wdpl2.sln`. App: `wdpl2/wdpl2.csproj`. Tests: `wdpl2.Tests/wdpl2.Tests.csproj` (xUnit).
- Persistence uses Entity Framework Core and SQLite.
- Core data includes seasons, divisions, venues, teams, players, fixtures, frame results, and competitions. A legitimate team may share a venue name; name equality alone does not justify deletion or merging.
- Use repository-relative paths, not developer-specific checkout paths.

## Season and Entity Identity
- Avoid speculative fuzzy identity merges that could collapse different players, teams, or seasons. Preserve legitimate names and initials such as J. Smith.
- `wdpl2/Helpers/DivisionHelper.cs` handles division normalization and matching.

## Editing Safeguards
- The legacy importers (Access, SQL, Paradox, HTML archives, Word) and the Import tab were removed; the league's historical data they brought in is still in the data file. Bulk entry is now the CSV boxes on the Teams, Players, Venues and Divisions pages, and new seasons are started with Import from Previous Seasons on the Seasons page (`Views/Import/ImportHistoricalDataPage`, backed by `SeasonCopyService`).
- `wdpl2/Features/Import/` keeps only shared plumbing, despite the folder name: `ImportWorkspace` (the private workspace and all-or-nothing commit every editing screen and season copy goes through), `ImportPlacementValidator` (relationship/placement checks on save), `ReviewPagination` (paged review lists) and `Csv`/`CsvRows`.
- Preserve private workspaces, transactional commits, relationship/placement validation, and locked-season protection. Do not silently delete user data.

## App Architecture and Shared State
- `wdpl2/MauiProgram.cs` configures MAUI Community Toolkit, local notifications, OCR, SkiaSharp, fonts, and DI. Registration is split into `AddPersistence`, `AddCoreAppServices`, `AddNotifications`, `AddViewModels`, and `AddPages` extension methods.
- `wdpl2/App.xaml.cs` initializes the database, bridges the static datastore to DI, loads data, applies the saved theme, initializes season selection, then creates `AppShell`.
- `wdpl2/AppShell.xaml` defines the pages as `FlyoutItem`s, each with a `Route`; navigation is a sidebar (locked open on desktop, a flyout on phones) drawn by `AppShell.xaml.cs` from its `Sections` list under the headings League, Matches, Stats and Online, with Dashboard first and Settings last. A new page needs a FlyoutItem with a Route *and* an entry in `Sections`; `AppWiringTests` fails if the two disagree, and if any `"//Route"` in the code is not a page. Point a page's `ContentTemplate` at the real page: Shell reuses a page instance, so a redirect page that navigates once gets stuck on its loading screen when revisited.
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

## Website and Publishing
- `wdpl2/Features/WebsiteBuilder/` contains settings views, generated HTML/CSS/components, JSON data generation, template pages, fixture sheets, and live-score output. Website generation is split across partial `WebsiteGenerator` files.
- Website settings cover branding, layout, colors, league data pages, history, galleries, rules/contact content, entry forms, captain access, SEO, and deployment.
- Entry forms use private editor drafts and shared rendering in `WebsiteGenerator.EntryForms.cs` for public pages and non-submitting previews. `EntryFormRules` centralizes validation and inclusive closing dates. Delivery is download-and-send, or a credential-free HTTPS POST to an external endpoint that returns an explicit matching acknowledgement; private collection tokens never enter generated HTML. Forms never claim browser storage is a submission. Imports require explicit form identity and review, preserve historical field values, and link season teams only by explicit records.
- Logos are uploaded images in the website settings' logo catalog (Branding). The logo designer was removed; logos it made remain as images. `Team.LogoCatalogId` is kept for data compatibility but nothing displays team logos.
- `wdpl2/Services/Cloud/` contains GitHub Pages publishing, optional GitHub data sync, and FTP upload. Do not assume credentials are configured or publishing is enabled, and never store credentials in instructions.

## Web platform (online backend)

The previous PHP backend, Web Inbox, browser admin and two-way sync were removed and are being rebuilt modularly. **`wdpl2/Docs/WebPlatform.md` is the contract — read it before changing `wdpl2/web-backend/` or `wdpl2/Services/Web/`.**

- **Single-writer ownership.** A scorecard is owned by the desktop or by the server, never both, and ownership moves by explicit handoff. This is what makes conflict resolution unnecessary. Code that reconciles two versions of a record means an ownership boundary is wrong — do not add it.
- **One front controller.** `api/index.php` is the only entry point and does routing, auth, CORS, body limits and error shaping. Module handlers hold feature logic only. Every response is `{ok:true,data}` or `{ok:false,error:{code,message}}`.
- **Two auth realms that never overlap.** Admin (PBKDF2 hash in the `config.php` sidecar) and captain (`Team.CaptainPin`, server-side, scoped to one team). Captain-scoped queries filter on the session's team id, never on a team id taken from the request body.
- **A module is one folder plus one DI registration**: `api/modules/<id>/Module.php` implementing `Module`, an `IWebModule` in `Services/Web/`, registered in `AddWebPlatform`. The Web Control tab, deploy file set and schema installer all read from the registry, so none of them need editing.
- **Code and configuration deploy separately**, so redeploying code cannot clobber working server credentials. `api/config.php` is never committed and never bundled as an app asset.
- Modules built: `system`, `league` (published data and the `live` board), `captains`, `scorecards` (live scoring, cup ties, solo mode) and `comps` (competition nights). The public live page reads `league.live`; `WebsiteGeneratorLiveScoresTests` parses the PHP to keep the page reading only fields the endpoint sends.
- **The live host reports PHP 8.4** (`system.ping`, September 2026; it was 7.4.33 before the host moved to a version selector). A buyer's host may be older, so keep backend PHP conservative and check `wdpl2/Docs/WebPlatform.md` before relying on newer syntax. Lint every backend change and run the rules suites in `wdpl2.Tests/Features/WebPlatform/`.

## Resources and Validation
- The games (Pool, Breakout, Memory, Snake, RetroFps) were removed. `wdpl2/Resources/` holds styles, fonts, raw web assets, and the rules templates in `Resources/Rules/` (neutral starting templates plus the EPA rules; a league's own rules are saved in website settings, and the Rules editor loads those first). `Helpers/` contains shared responsive UI, panels, WebView, emoji, and division helpers.
- The app project declares Android, iOS, Mac Catalyst, and Windows targets. Declared targets are not proof that every platform has been tested; honor platform-specific code and dependencies.
- `wdpl2.Tests/` mirrors domain, services, view models, features, and views using xUnit. Run relevant regressions and build for code changes; documentation-only changes do not require compiling the app.
- Legacy archive/sample folders are test/reference data, not the application's architecture. `wdpl2/Docs/` includes Paradox format documentation and scheduling references; inspect project exclusions before treating files as compiled code.

## Maintaining Context
- These are durable project notes, not a substitute for inspecting current code. Update them when architecture or domain requirements change.
- Do not store transient plans, build/test counts, or debugging progress here. Keep persistent instructions focused on durable architecture/domain rules, not transient build results or session progress.

## General Familiarization
- Retain broad, verified app familiarization context across all major features and architecture to avoid repeating orientation in future chats.

## UI Diagnostics
- When diagnosing WDPL UI issues, verify the actual navigation/runtime data path rather than relying on passing generator or source-text tests; the user reported those validations did not resolve missing controls and dates.