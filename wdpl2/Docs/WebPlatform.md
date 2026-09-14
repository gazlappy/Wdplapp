# WDPL web platform

How the online backend is put together, and the rules that keep it simple.
Read this before changing anything under `wdpl2/web-backend/` or `wdpl2/Services/Web/`.

## Why it looks like this

The previous backend was 70 loose PHP files, a 19-tab browser admin and a two-way
sync journal. It was abandoned with a documented release blocker: desktop and
server could both edit the same scorecard, so it needed an after-the-fact
reconciliation engine, and a lost HTTP response could leave a card unrecoverable.

This rebuild does not try to solve that problem. It removes it.

## Rule 1: single-writer ownership

Every scorecard has exactly one owner at any instant. Ownership moves by explicit
handoff — never inferred, never decided by comparing timestamps.

```
 desktop-owned ──[secretary opens fixture for live scoring]──▶ server-owned (live)
      ▲                                                              │
      │                                                   [captain finalises]
      │                                                              ▼
      └──────────[desktop claims finalised card]────────────  server-owned (finalised)
                                                                     │
                                                            [claimed, frozen forever]
```

- `desktop-owned` — the server copy is read-only. The app pushes; the server never edits.
- `server-owned (live)` — only captains write. The app will not write this fixture.
- `finalised` — captain writes stop. The desktop claims it in one atomic call.
- `claimed` — read-only forever. Corrections happen in the app and go out as a fresh push.

There is no instant when both sides may write the same row, so there is nothing to
merge. The remaining failure cases are refusals with clear messages ("card not
finalised yet", "card already claimed"), not data loss.

**If you are about to add code that reconciles two versions of a record, stop.**
That means an ownership boundary is wrong somewhere.

Concurrency *within* the live window (two captains, one card) is a single-row
optimistic `version` compare-and-swap under `SELECT ... FOR UPDATE`. That is the
only concurrency mechanism in this backend.

## Rule 2: one front controller

`api/index.php` is the only entry point. It does routing, auth, CORS, body limits
and error shaping, once. Module handlers contain feature logic and nothing else.

```
api/
  index.php            the only public URL
  config.php           sidecar: credentials. NOT in git, NOT a bundled asset.
  config.sample.php    template for hand-configuration
  .htaccess            denies direct access to everything but index.php
  core/
    Http.php     request/response, one error envelope, HTTPS detection
    Config.php   reads the sidecar
    Db.php       PDO, transactions, row locks
    Auth.php     admin realm + rate limiting
    Captain.php  captain realm, scoped to one team
    Module.php   the module interface
    Registry.php discovers modules/*/Module.php
    Schema.php   installs tables, tracks applied versions
  modules/
    system/Module.php
```

Every response is `{"ok":true,"data":…}` or `{"ok":false,"error":{"code","message"}}`.
Anything else means the front controller was not reached.

### Runtime constraint: PHP 7.4

**The host now runs PHP 8.4.17.** It previously served 5.6.40; the PHP Selector
is under *Plugins -> PHP Selector* in the x10hosting panel, and the version must
be set for the **domain** (wdpl.uk), not the account default.

Getting there cost several deploy cycles because the version reported by
phpMyAdmin (7.4.33) was the version phpMyAdmin itself ran under, not the one
serving the domain. Two lessons are now baked in:

- Boot failures report `PHP_VERSION`, so the running version comes from the
  backend itself rather than from a panel that can disagree with it.
- The code is kept portable and linted against **7.2, 7.4 and 8.4**. The floor
  is PHP 7.1. Nothing needs that portability today, but it costs nothing and it
  means a host or plan change cannot silently break the deployment again.

Extensions confirmed enabled on the host: `pdo`, `pdo_mysql`, `json`, `session`,
`hash`, `mbstring`, `filter`. Leave `opcache` **off** while iterating - it caches
compiled PHP and can serve stale code after an FTP deploy.

Do not use, however natural it looks:

| Feature | Introduced | Use instead |
|---|---|---|
| Constructor property promotion | 8.0 | declare properties, assign in the constructor |
| `readonly` properties | 8.1 | a plain typed property |
| `never` return type | 8.1 | `void` |
| `mixed` type hint | 8.0 | omit the hint |
| `catch (SomeType)` with no variable | 8.0 | `catch (SomeType $ignored)` |
| `$object::class` | 8.0 | `get_class($object)` |
| `str_contains` / `str_starts_with` | 8.0 | `strpos(...) !== false` |
| `match`, enums, nullsafe `?->` | 8.0/8.1 | `switch`, class constants, explicit checks |
| Typed properties (`public int $x;`) | 7.4 | untyped property + `@var` docblock |
| Arrow functions (`fn() => …`) | 7.4 | `function () { return …; }` |
| `??=` | 7.4 | `$x = $x ?? …` |

Nullable types (`?array`), return types and `??` are 7.1 and are fine.

Linting against 7.4 is the only reliable guard - PHP 8 accepts all of the above
silently, so testing on 8.x alone proves nothing about the host.

**PHP 7.4 reached end of life in November 2022 and receives no security
updates.** That is a hosting risk, not a code style preference. Moving to a host
with a supported PHP is the real fix; until then this constraint stands.

## Rule 3: two auth realms that never overlap

| Realm | Who | How |
|---|---|---|
| `Role::Admin` | League secretary | Basic credentials (desktop) or session cookie (browser), against a PBKDF2 hash in `config.php` |
| `Role::Captain` | One team captain | `Team.CaptainPin`, hashed by the app, verified server-side, rate-limited per caller **and per team**, scoped to that team |
| `Role::Public` | Anyone | No credentials |

A captain session must never grant admin access. Captain-scoped queries filter on
`Captain::requireTeamId()` — **never** on a team id taken from the request body.

The admin password hash is `pbkdf2-sha256$<iterations>$<b64 salt>$<b64 hash>`,
written by `PasswordHash.Create` in the app and verified by `Auth::verifyHash()`.
PBKDF2 rather than PHP's `password_hash()` because both .NET and PHP implement it
natively; bcrypt would mean adding a package to the app purely to produce a string.
`PasswordHashTests` pins the format — if it fails, a deployed backend is about to
stop accepting its own administrator.

## Adding a module

1. `api/modules/<id>/Module.php` implementing `Module`. Class name is the
   PascalCase of the folder plus `Module` (`teams` → `TeamsModule`).
2. An `IWebModule` in `wdpl2/Services/Web/`, listing its server files.
3. Register it: `services.AddSingleton<IWebModule, YourWebModule>()` in
   `CoreServiceCollectionExtensions.AddWebPlatform`.

That is the whole checklist. The Web Control tab, the deploy file set and the
schema installer all read from the registry, so none of them need editing.

Keep `schemaVersion()` and `IWebModule.SchemaVersion` equal — the Web Control
status view compares the app's expected version against the server's applied one
and reports the difference.

## Deploying

Web Control → Connection & Deploy.

- **Deploy backend** uploads code only, leaving server credentials untouched.
- **Deploy with configuration** also overwrites `api/config.php`.

They are separate because a code redeploy must never be able to clobber working
credentials. `config.php` is deliberately excluded from the bundled assets.

Then **Install tables** (`system/install`), which is safe to run repeatedly.

FTP upload is **not atomic** — a half-uploaded backend is a broken backend. Do not
deploy during a match.

## Testing

What runs without a server:

```powershell
dotnet test wdpl2.Tests\wdpl2.Tests.csproj --filter FullyQualifiedName~Web
```

Covers endpoint validation (`WebConnectionTests`), the response envelope and auth
handling (`WebApiClientTests`), and the hash contract (`PasswordHashTests`).

**The host runs PHP 7.4.33**, so that is the target. See "Runtime constraint"
below before writing any PHP.

Two runtimes are installed locally:

| Version | Where | Why |
|---|---|---|
| 7.2.34 | downloaded zip, session scratchpad | the floor; if it passes here it runs on anything the host is likely to have |
| 7.4.33 | downloaded zip, session scratchpad | what the panel advertises |
| 8.2.33 | winget `PHP.PHP.8.2` | confirms the code is not accidentally old-only |

```powershell
# lint against the host's version - this is the one that matters
Get-ChildItem wdpl2\web-backend -Recurse -Filter *.php | ForEach-Object { & <php74>\php.exe -l $_.FullName }

# rules tests - run under BOTH runtimes, no MySQL needed
& <php74>\php.exe wdpl2.Tests/Features/WebPlatform/backend.rules.test.php
php wdpl2.Tests/Features/WebPlatform/backend.rules.test.php
```

`backend.rules.test.php` covers the admin password format, module discovery, the
auth gate, HTTPS detection, the config guard and the SQL identifier guard.

To exercise the router for real, copy `web-backend/api` to a scratch folder, add a
`config.php` with `allow_insecure => true`, and run `php -S 127.0.0.1:8099 -t <that
folder>`. Everything except database-backed actions works without MySQL.

### Testing against a real database

MariaDB **10.6.22** (matching the host's 10.6) runs from a zip in the session
scratchpad - no installer, no service, no admin rights, bound to 127.0.0.1:3307
so it cannot collide with anything else:

```
mariadb\mariadb-10.6.22-winx64in\mariadbd.exe --datadir=<scratch>\mariadb\data --port=3307 --bind-address=127.0.0.1
```

Database `wdpl_test`, user `wdpl` / `wdpltest`. The backend reaches it by setting
`db_host` to `127.0.0.1;port=3307` in a scratch `config.php` (the DSN is built by
string concatenation, so the extra `port=` clause rides along).

Local PHP has no `php.ini`, so `pdo_mysql` must be enabled explicitly with
`php -c <scratch>\phpini\php.ini`, whose `extension_dir` points at the winget
package's `ext` folder.

This is what makes the database paths testable at all. It has already caught a
deadlock (authentication consulting a table that only the authenticated install
action creates) that was invisible without a database, because every local test
stopped at `db_unavailable` first.

The old Docker MySQL harness is deliberately not reinstated. It was never runnable
on this machine and proved nothing.

## Status

| Milestone | State |
|---|---|
| M1 — spine (core, routing, auth, deploy, Web Control tab) | **Done and live.** Deployed to wdpl.uk, admin auth verified, schema installed. |
| M2 — league data + public read | **Done**, tested against a local MariaDB. Not yet pushed to wdpl.uk. |
| M3 — captains (server-side PIN) | **Done**, tested against a local MariaDB. Not yet pushed to wdpl.uk. |
| M4 — live scorecards | Not started |

Two known loose ends carried from the removal of the old backend, both closed by
later milestones:

- ~~`WebsiteJsonDataGenerator.cs` published hashed captain PINs into public JSON~~
  — removed in M3. It turned out to be dead code (`GenerateCaptainsJson` had no
  caller outside tests, and `captains.json` 404s on the live site), so nothing
  was ever exposed, but an unsalted single-round SHA-256 of a 4-digit PIN sat in
  the codebase waiting to be re-enabled. Deleted along with its tests.
- ~~`WebsiteGenerator.LiveScores.cs` pointed at the deleted `api/public/live.php`~~
  — repointed in M2 to `api/index.php?m=league&a=live`, which answers with an
  empty `items` list until M4 fills it.
