# WDPL Web Backend (cPanel edition)

This folder contains everything you need to add a small **web inbox** to
your existing wdpl.uk website so captains/players can submit data
(match results, availability, entry forms) through the website.

The MAUI desktop app stays the source of truth — it pulls pending
submissions from the website and applies them with a click.

---

## What you’ll set up (one-off, ~15 minutes)

1. A small **MySQL database** that stores submissions until you process them.
2. A few small **PHP scripts** (in `api/`) that:
   - Accept form posts from the public website.
   - Let the MAUI app list and mark them processed (over basic auth).
3. A **password-protected admin folder** so only you (and the MAUI app)
   can read the inbox.

You do **not** need to know PHP or MySQL — just follow the steps below.

---

## Step 1 — Create the database (cPanel)

1. Log in to cPanel for wdpl.uk.
2. Open **MySQL® Databases**.
3. Under **Create New Database** enter a name, e.g. `inbox` → click *Create Database*.
   - cPanel will prefix it with your account name, so the real name will look
     like `youracct_inbox`. **Write that name down.**
4. Under **MySQL Users → Add New User**, create a user (e.g. `wdpl`) with
   a strong password. **Write the full username (`youracct_wdpl`) and password down.**
5. Under **Add User To Database**, pick the user + database you just made,
   tick **ALL PRIVILEGES**, click *Make Changes*.

## Step 2 — Create the tables (phpMyAdmin)

1. cPanel → **phpMyAdmin**.
2. Left sidebar: click your new database (e.g. `youracct_inbox`).
3. Click the **SQL** tab at the top.
4. Open `sql/schema.sql` from this folder, copy **all** of its contents,
   paste into the SQL box, click **Go**.
5. You should see “2 tables created”. Optional: add a test captain token —
   see the bottom of `schema.sql`.

## Step 3 — Upload the API files

1. cPanel → **File Manager**.
2. Navigate to `public_html`.
3. Upload the entire `api/` folder from this repo into `public_html/`
   (so the path on the server becomes `public_html/api/`).
   - You can drag-drop using File Manager, or use an SFTP client like
     WinSCP / FileZilla.

## Step 4 — Put your DB password into `_db.php`

1. In File Manager, open `public_html/api/_db.php` for editing.
2. Replace the three values near the top:
   ```php
   const DB_NAME = 'youracct_inbox';
   const DB_USER = 'youracct_wdpl';
   const DB_PASS = 'the-strong-password-you-chose';
   ```
3. Save.

## Step 5 — Password-protect the admin folder

1. cPanel → **Directory Privacy** (sometimes called *Password Protected Directories*).
2. Navigate into `public_html/api/` and click `admin`.
3. Tick **Password protect this directory**, enter label `WDPL Admin`, *Save*.
4. Click *Go Back*, then under **Create User** add a username (e.g. `wdpladmin`)
   and a strong password. **Write these down — the MAUI app will use them.**

## Step 6 — Quick smoke test

In any browser, open:

```
https://wdpl.uk/api/admin/pending.php
```

- You should be prompted for the username/password from step 5.
- After logging in you should see `{"items":[]}` — empty list, but it works.

To test a submission, open `examples/result-form.html` in a browser
(or upload it temporarily to your site) and submit it. Refresh
`pending.php` and you should see your test row.

You can also check the table directly in phpMyAdmin → `submissions`.

---

## What gets stored

Each submission is one row in the `submissions` table:

| column         | meaning                                                |
|----------------|--------------------------------------------------------|
| `id`           | auto number                                            |
| `type`         | what kind of submission (`match_result`, `availability`, `entry`, `generic`) |
| `season_id`    | Guid of the WDPL season (optional)                     |
| `reference_id` | Guid of the fixture/match/competition (optional)       |
| `payload_json` | the actual form data (free-form JSON)                  |
| `submitter`    | who sent it (captain name or email)                    |
| `received_utc` | when                                                   |
| `processed`    | 0 until you apply it in the MAUI app, then 1           |

---

## Next: MAUI side

Once the above works, the MAUI app will get a new **Web Inbox** page
that lists pending submissions and lets you apply each one with a tap.
That part lives in the main `wdpl2/` project — ask Copilot to add it
when you're ready.

---

## Security notes

- Always use **https://** when calling the API.
- Keep your DB password and admin password different.
- Enable cPanel **Two-Step Authentication** on your own account.
- Optional: schedule a nightly MySQL backup under **Cron Jobs**:
  ```
  30 2 * * * mysqldump --single-transaction youracct_inbox > ~/backups/inbox_$(date +\%F).sql
  ```
