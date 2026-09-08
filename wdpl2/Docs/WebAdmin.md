# WDPL browser administration

## First-phase contract

The browser dashboard and MAUI app share the existing PHP/MySQL backend. This is a responsive online interface, not an offline or installable application. First-phase editing is limited to explicitly supported scorecard and entry-review records. Seasons, divisions, venues, fixtures and competitions need separate synchronization contracts before full two-way editing is enabled.

### Existing behavior and required safeguards

- `publish-league.php` currently replaces season teams, players and fixtures. Settings and frame results are written separately. This is a snapshot upload, not a two-way merge.
- Live scorecards already have a version shared with captains. Browser edits must check that version under the same row lock; stale edits must return a conflict without writing.
- Desktop frame results and online live scorecards are separate representations. Initial reconciliation must show both; timestamps cannot establish which is authoritative.
- Entry submissions are immutable historical payloads. Review status and notes are separate versioned metadata. Identifiers must include the backend, form ID and client submission ID. Older local imports without that mapping must be linked explicitly, never by names or field similarity.
- Desktop collection/import is not server acknowledgement. A synchronization cursor can advance only after durable local application, with a retryable acknowledgement and conflict resolution checked against the current server version.
- Publishing must not replace records while unresolved web edits exist. Publication and web mutations must share a transaction lock; preflight-only checks are insufficient.
- Locked seasons and unknown player/fixture identities must stop automatic application. Review does not create teams or register players automatically.
- Preserve the public hosted submission and Basic-authenticated desktop collection contracts; browser reviews need separate session-authenticated endpoints.

## Authentication

Use an existing enabled backend admin account, not FTP credentials. Administration requires HTTPS. Browser sessions use Secure, HttpOnly, SameSite cookies. Browser mutations require same-origin JSON requests with an explicit admin-request header; cross-origin administrative CORS is not supported. Desktop Basic authentication remains supported over HTTPS.

New administrators must be provisioned by an existing superadmin or an explicit database setup operation. An anonymous visitor must never be able to claim the first administrator account. Keep private passwords, session tokens and submission data out of browser persistent storage and logs.

## Entry-review synchronization services

`admin/entry-reviews.php` lists immutable submissions with their current review metadata and accepts revision-checked status/notes edits. `admin/sync-entry-review.php` resolves a downloaded review using a saved server or local choice. Both use the existing synchronization journal and backend identity; original submission answers are never updated.

The desktop coordinator freezes the choice in its private review queue, then atomically saves the metadata and an application request marker in the local JSON data file. A retry must match the original snapshot, decision and resulting entry. Changes to answers, mappings or review values stop the retry rather than being overwritten. The applied cursor advances only after a matching server receipt and a local-state recheck.

The browser **Entry forms** tab provides a responsive submission list and read-only answer detail, paged within a fixed submission range. Admins can save status and notes; readonly accounts can inspect entries without editing. A stale revision preserves the draft and requires an explicit reload. An uncertain response freezes the request and values for retry while the page remains open. No submission data is saved to browser persistent storage, and session expiry clears the workspace.

Web Inbox now exposes **Review website changes**, a desktop page using the saved connection settings. **Download website changes** captures read-only local/website previews in a durable queue without applying data. The first queued record requires a comparison checkbox and a confirmation dialog before **Use website** or **Keep local** invokes the resolution coordinator. A saved choice disables the opposite action on retry. HTTPS certificate bypass is not permitted for this workflow.

The explicit submission-linking UI and stale-snapshot reconciliation controls are still pending. Entries without explicit backend/form/client/sequence mappings and unknown fixtures remain blocked; do not delete the review queue to bypass them. A browser confirmation updates review metadata only: it does not register a team/player or mean that desktop synchronization is complete.

## Deployment and validation

Back up the database before deploying backend changes. Deploy explicitly through Web Inbox; no live deployment is performed during implementation. Test with a disposable MySQL database, including authentication failures, read-only access, CSRF, stale revisions, concurrent publishing, retry recovery and desktop conflict resolution before enabling production web editing.

PHP/MySQL integration tests require a local PHP runtime with PDO MySQL and a disposable database. A missing runtime is an unvalidated environment, not a successful integration test.
