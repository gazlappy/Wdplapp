# Isolated WDPL backend validation

This is test infrastructure, not a deployment or a live captain portal. Do not use production database credentials here.

## Run from the repository root

Install/start Docker Desktop with Linux containers, then run:

`docker compose -f tools/backend-tests/compose.yaml up --build --abort-on-container-exit --exit-code-from tests`

After recording the result, clean up this test environment:

`docker compose -f tools/backend-tests/compose.yaml down --volumes`

The MySQL database lives in temporary memory-backed storage. No database port is published; the test network is internal. The repository is mounted read-only. The fixed password is for this disposable, unexposed test database only. Image builds require internet access. No application configuration or hosted database is changed.

## Coverage and limitations

The runner requires PHP 8.2+ and PDO MySQL, lints deployed PHP, runs pure PHP rules, Node browser/source tests, and the disposable journal integration test (including a two-process competing-write case). Missing prerequisites, failed commands and skipped journal integration fail the runner.

This is NOT full endpoint validation. Captain/admin HTTP authentication, finalization retries, concurrent publication, all destructive admin actions, deployment behavior on real hosting, and started-decision recovery require separate staging acceptance tests. Hosted entry-form database tests use a different empty database prefix and are not run by this Compose setup.

The initial workspace validation could not execute Docker, PHP or MySQL because they were absent. Do not treat the supplied container configuration or the concurrency test as already validated.

## Required staging acceptance

1. Create a separate HTTPS backend directory and separate database. Back up production but do not copy live credentials into this test setup. Configure the app/test profile explicitly for staging, preserving any existing review queue.
2. Deploy all backend runtime files together, including `_scorecard_journal.php`. Confirm PHP 8.2+ and PDO MySQL on hosting. Use test teams/players/fixtures only.
3. Verify admin login/logout, readonly denial, expired sessions, CSRF denial, and captain access limited to their own fixtures.
4. Open one fixture as each captain. Test player nominations, frame winners, doubles, eight-ball agreement and stale-version rejection. Verify live state equals the current journal payload.
5. Finalize each side; verify submission and finalization journal atomically. Test reopening, clearing, reset/delete, fixture swap/delete and player merge on disposable records. Check invalidated versions/finalizations.
6. Test edits competing with publication, forced transaction failure and lost HTTP responses. No partial domain writes or sequence gaps should remain.
7. Compare/download changes in the desktop queue. Confirm locked seasons and unknown identities block application; no cursor advances from comparison alone.
8. Do NOT sign off full two-way synchronization until durable acknowledgement receipts and started-decision recovery are implemented and tested. Never delete a queue or change its request IDs to bypass a conflict.

## Production gate

Production requires a verified backup/restore route, passing isolated and staging tests, confirmed deployment target, and explicit deployment confirmation. Incomplete app packages now abort before upload, but FTP deployment itself is not atomic; schedule a maintenance window and retain the previous complete backend for rollback.
