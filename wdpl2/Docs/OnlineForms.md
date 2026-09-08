# Website Builder online forms

## Design and review

Open **Website → Entry Forms**. On wide windows, design and entry review sit side by side; narrower windows offer **Design form** and **Review entries** tabs.

- Start with a team, competition or custom form. New forms are unpublished private drafts until saved.
- Edit labels, types, required fields, options, instructions, logo, delivery confirmation and closing date. **Save Form** validates the definition; labels must be nonempty and unique because existing imports use labels as keys.
- **Preview** uses the public form renderer and selected website theme. It never submits, downloads or stores entries. Saved website changes require regeneration and publishing separately.
- Preview offers **Desktop** (up to 1100px) and **Mobile (390px)** viewports, constrained by the available window width. Switching widths keeps entered preview details intact.
- The main **Website Builder → Preview → Entry Forms** page uses the same test-only form behavior. It shows saved, published forms when the Entry Forms page is enabled; use the form editor's **Preview** for unpublished or unsaved drafts. Public delivery endpoints and the legacy browser-entry recovery page are omitted from builder preview output, not from normal publishing.
- The builder offers **Desktop** and **Mobile (390px)** controls and **Settings/Preview** tabs in narrow windows. Changing viewport width does not reload the page. **Refresh**, or returning from a settings page, regenerates the preview from saved settings and clears test entries; if the selected page was removed, preview returns to Home. Generation for deployment remains separate and retains the configured public delivery behavior.
- The **Visual Layout Editor** also offers **Entry Forms** when published forms are enabled. Its generated forms stay test-only after opening, switching pages, following local page links and saving the layout. Use the editor's preview mode to interact with form controls.
- Both builder previews load generated player/team data locally, including cache-busted data URLs. Local generated-page links and in-page anchors remain available; external website, email and telephone navigation is blocked rather than replacing the preview with a live page. Test those external links on the separately generated/published website.
- The public forms page starts with a directory of published forms showing status, field count and deadline. Each card links directly to its form.
- Forms show required-field completion and active Complete/Review/Send steps. Completion is a usability aid, not delivery or registration confirmation. Only a matching server receipt completes the online delivery step; download mode remains explicitly download-and-send.
- Closing dates are inclusive: a form remains open throughout the selected date. An unchecked closing-date switch means no deadline. The Closed switch closes it immediately on the next generation.
- Switching forms warns about unsaved form changes. Entry records and imported IDs are retained when a definition is saved. Renaming/removing a field does not delete historical values.
- Importing a file or fetching a collection presents counts grouped by explicit form identity before adding pending records. Unmatched identities are excluded, never assigned to whichever form is selected. Review individual details afterward; no team/player records are created automatically.
- Search by entry name or field values and filter Pending/Confirmed/Rejected. Team cross-reference counts only explicitly linked season-team records, not matching names.

## Delivery modes

### Download and send (no public endpoint)

The visitor completes the form, reviews the details, and downloads a JSON entry. They must send the file to the league secretary, who imports it in the app. Configure website contact information so visitors know whom to contact.

**Downloading is not delivery.** The website says this explicitly and does not claim the entry was submitted. Entries are not automatically written to localStorage. Downloaded files contain personal data: store/share them appropriately and remove them when no longer needed.

`_submissions.html` remains a recovery page for entries saved by older websites in that browser on that device. It is not an admin inbox or shared collection and cannot recover entries stored on another device. No old browser entries are silently deleted.

### Our own hosting (bundled PHP/MySQL receiver)

FTP deploys the files; visitors submit over HTTPS. The host must execute a supported PHP version (8.2 or newer), provide PDO MySQL with InnoDB, and have a valid HTTPS certificate. Static-only FTP hosting is not sufficient. Never deploy PHP credentials to a host that serves PHP as plain text.

**Setup order:**

1. Configure database credentials and the backend destination under **Website → Deployment**, then explicitly deploy the bundled backend. The receiver uses the existing `api/_db.config.php` sidecar. Backend and generated-site folders can differ; the Web Inbox API URL must point at the backend's actual public location.
2. Provision an existing enabled backend admin account with a strong password. Configure its username/password and HTTPS API base URL in **Web Inbox settings**. These are backend admin credentials, not FTP credentials or cPanel Directory Privacy credentials. The new endpoints do not bootstrap accounts or accept cookie-only sessions. Leave **Ignore SSL errors** off.
3. Open **Entry Forms → Delivery and collection settings → Use our own hosting**. The switch saves immediately, even before setup is complete. Enter the public **Website URL** here and choose **Save hosting settings** (the shared website setting is updated). This is the address visitors open, not the Web Inbox `/api/` URL. Its HTTPS origin (scheme, host and optional port) becomes the receiver's exact CORS allowlist. If both `www` and non-`www` names exist, redirect visitors to the configured one before they open forms.
4. Save the form definitions, publication states and deadlines in the app. Open **Entry Forms → Delivery and collection settings → Use our own hosting → Publish saved forms to our hosting**. Review the displayed backend, website origin and form count before confirming.
5. Publishing creates the four separate `entry_form_*` tables if needed and atomically replaces the active definition set. Only saved, published forms are included. Disabling the website forms page publishes an empty set. Omitted forms stop accepting new entries; existing entries are retained. No unsaved draft is published.
6. The hosting preference and website address are saved locally independently of publishing; **Publish saved forms** also saves the entered website address. Only a matching backend acknowledgement marks hosted delivery confirmed and saves the public/private endpoint URLs. Until confirmed, generated forms remain in download-and-send mode and hosted fetching is blocked. Changing the public website address here requires publishing again. Earlier successfully published hosting configurations remain usable. Regenerate and deploy the website separately, then test one submission and fetch it back before opening registration.
7. Use **Fetch Submissions** to collect all pages, review detected form identities, then import pending entries. Entries still require individual review; no teams or players are created automatically.

**Repeat definition publishing after changing fields, closing dates, publication states or the website origin**, including when closing or deleting forms. Saving local settings or uploading PHP files alone does not update authoritative server definitions. A field change can invalidate an older browser page; visitors should reload. Publish the backend definitions and generated website close together.

Endpoints relative to the saved Web Inbox API base URL:

- `entry-forms/submit.php`: public JSON POST; no credentials or cookies. A matching receipt confirms durable storage, not acceptance into a competition or league.
- `admin/entry-form-definitions.php`: authenticated JSON POST of the complete form set. It uses the existing enabled admin account, not public access or bootstrap credentials.
- `admin/entry-form-submissions.php`: authenticated GET, 100 entries per page, with `after`/`through` cursors. The app keeps a fixed upper snapshot and finishes fetching before offering import review. It refuses partial/oversized collections rather than silently importing a subset.

Server deadlines are inclusive through the closing date in **Europe/London**, including daylight-saving changes. Server receipt time is used for imported dates; client timestamps are retained only as metadata. Definition publishing and acceptance share a transaction lock, and `(form ID, client submission ID)` is unique. Identical stored retries receive the original acknowledgement even after closure; a changed payload with that identity returns HTTP 409. Database/validation failures never receive an accepted receipt.

Limits: 100 published forms; 100 fields per form; 200-character titles/labels; 100 distinct dropdown options of up to 500 characters; 500-character single-line values and 4,000-character text areas; 64 KiB total public request body; 1 MiB definition document. Default throttling is 30 POST attempts per client IP per hour and 300 globally per minute (HTTP 429). IP addresses are HMAC-hashed in short-lived rate counters; forwarded-IP headers are not trusted. Shared networks may hit these limits. Add host-level abuse protection and request-size limits for production; CORS alone is not authentication or bot protection.

The app requires valid HTTPS certificates, refuses redirects with credentials, and sends its admin password only to endpoints derived from the saved Web Inbox API URL. Changing that base URL requires publishing to the intended destination again before fetching. A reverse proxy must configure the web server's HTTPS state correctly; the PHP receiver does not blindly trust forwarded HTTPS headers.

Submissions remain private in MySQL. Fetching or deleting a local record does not delete server history. Back up the database securely, restrict administrative access, and establish a retention/deletion process appropriate for personal data. Each app collection is limited to 50,000 entries / 64 MiB; larger collections need a smaller private export. Private collection tokens used by external services are ignored in own-hosting mode and are never sent to the public receiver.

### Online delivery (another compatible public HTTPS endpoint)

With **Use our own hosting** off, supply a public HTTPS POST endpoint and save the external-service settings. No API token is included in generated HTML or browser requests. Do not put secrets in the public URL. The bundled entry-form receiver above implements this contract; the older captain/results APIs are separate and must not be used as entry-form receivers.

The browser POSTs JSON:

```json
{
  "id": "stable-client-submission-id",
  "formId": "form-0123456789abcdef0123456789abcdef",
  "name": "Example Team",
  "values": { "Team Name": "Example Team", "Consent": "Yes" },
  "submittedAt": "2026-01-20T19:30:00.000Z"
}
```

A successful server response must be a 2xx JSON response:

```json
{ "accepted": true, "id": "stable-client-submission-id" }
```

The browser only confirms delivery if the response explicitly acknowledges the same ID. The configured confirmation message is then displayed with a reference. Sending is still not acceptance into the league/competition.

**Endpoint requirements:**

- Allow the published website origin through CORS; handle OPTIONS and `Content-Type: application/json`. The client uses no cookies or authorization header.
- Validate form identity, published/open status, required fields, value types, length and option limits server-side. Publish authoritative form definitions to the receiving service through its own trusted administration flow. Browser checks are only usability checks and can be bypassed.
- Enforce authoritative closing dates/time zone, body-size limits, rate limits and abuse protection. Never trust client timestamps or names as identity.
- Store entries atomically and idempotently by form ID and submission ID; acknowledge only durable storage. A retry of an already-stored identical entry must return the original acknowledgement without creating a second record. Reject conflicting payloads for an existing ID.
- Protect collection reads and administration separately. Never expose other visitors' entries to anonymous clients. Define retention/access rules for personal data.
- Return non-2xx on rejected entries. Do not redirect to an HTML thank-you/login page; HTML, missing acknowledgements and mismatched IDs are treated as unconfirmed delivery.

The browser times out after 30 seconds and allows retries with the same payload and ID. After a send attempt, the reviewed data remains fixed because a network error might occur after the server stored it. Reloading starts a different entry: contact the secretary using the displayed reference before submitting a replacement. Before sending, Back to edit remains available.

### Private collection in the app

In own-hosting mode, collection uses the Web Inbox admin credentials and built-in pagination described above. The following settings apply to other services only.

Set a separate private HTTPS GET collection URL and optional private bearer token. JSONBin hosts use the private `X-Master-Key` header instead. Redirects are not followed with credentials. Fetch uses saved settings.

Accepted collection/file shapes include a submission array, `{ "submissions": [...] }`, `{ "data": [...] }`, `{ "data": { "submissions": [...] } }`, and JSONBin's `{ "record": [...] }`. Use an unpaginated complete export; automatic pagination is not implemented. IDs prevent reimport. Legacy ID-less exports use a deterministic exact-payload fingerprint; identical legacy payloads cannot be distinguished without source IDs. Existing pre-fingerprint imports may need manual duplicate review on their first reimport.

### Existing JSONBin installations

Older generated pages placed the JSONBin master key in public JavaScript and performed a shared read/append/write operation. That exposes credentials and other entries and can lose concurrent submissions. New generation deliberately stops this public-delivery path.

1. Recover existing submissions using the app's private collection URL (or its legacy URL fallback).
2. Rotate any key previously published in a website. Deleting the source from a newly generated site does not revoke an exposed key.
3. Move the JSONBin URL into **Private collection URL**; leave the public endpoint blank for download-and-send, or configure a compatible server-side endpoint.
4. Regenerate and republish the website, replacing old public pages. No publishing or credential rotation is performed automatically.

## Validation

- C# regressions: `dotnet test wdpl2.Tests/wdpl2.Tests.csproj --filter FullyQualifiedName~EntryFormsTests`
- Dependency-free browser-script behavior: `node --test wdpl2.Tests/Features/WebsiteBuilder/entry-forms.browser.test.mjs`
- Hosted client regressions: `dotnet test wdpl2.Tests/wdpl2.Tests.csproj --filter FullyQualifiedName~HostedEntryFormsServiceTests`
- PHP validation regressions: `php wdpl2.Tests/Features/WebsiteBuilder/hosted-entry-forms.rules.test.php`; lint each of the five bundled entry-form PHP files with `php -l`.
- Opt-in HTTP/MySQL integration: `node --test wdpl2.Tests/Features/WebsiteBuilder/hosted-entry-forms.integration.test.mjs`. Requires PHP on PATH (or `PHP_BINARY`) with PDO MySQL, and an **empty disposable** database named `wdpl_entryforms_test_*`. Set `WDPL_TEST_DB_NAME`, `WDPL_TEST_DB_USER`, `WDPL_TEST_DB_PASSWORD` and optionally `WDPL_TEST_DB_HOST` in the test process environment. Never use a production database. The suite creates and removes its test tables and binds its PHP server only to loopback; HTTPS is simulated only inside that test router. Missing runtime/configuration produces a skip, not a pass.
- Manually check the MAUI editor and main builder in wide/narrow windows and a published/generated page at mobile and desktop widths, with keyboard navigation and the selected website theme. In the builder, select **Entry Forms**, complete/review/test an entry, switch widths without losing details, then save a form change and verify the refreshed preview. Disable the Entry Forms page and verify the preview falls back to Home.
- With a configured staging endpoint, verify CORS, server validation/deadlines, persistence, duplicate retries, collection permissions and actual app import before enabling production delivery. Local script tests do not prove an external service is configured correctly.
- In staging, also verify that unauthenticated collection/publishing is denied, a disallowed Origin is rejected, two retries with one reference produce one stored entry, changed payloads with that reference conflict, and closing/unpublishing immediately stops new entries without removing prior ones. Confirm actual HTTPS handling and host-level abuse controls; the loopback integration suite does not establish those.
