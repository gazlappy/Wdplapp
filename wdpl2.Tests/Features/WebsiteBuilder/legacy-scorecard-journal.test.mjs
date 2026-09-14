import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const read = file => readFileSync(new URL('../../../wdpl2/web-backend/api/' + file, import.meta.url), 'utf8');

test('unchanged acceptances persist acknowledgements and retries check receipts before revisions', () => {
    for (const endpoint of ['sync-accept', 'sync-entry-review']) {
        const code = read('admin/' + endpoint + '.php');
        assert.match(code, /admin_sync_acknowledge\(/);
        assert.ok(code.indexOf("$prepared['replayed']") < code.indexOf('admin_sync_acknowledge('));
    }
    const core = read('_admin_sync.php');
    const prepare = core.slice(core.indexOf('function admin_sync_prepare_change'), core.indexOf('function admin_sync_acknowledge'));
    assert.ok(prepare.indexOf('FROM admin_sync_acknowledgements') < prepare.indexOf('$current = admin_sync_read_record'));
    const ack = core.slice(core.indexOf('function admin_sync_acknowledge'), core.indexOf('function admin_sync_commit_change'));
    assert.match(ack, /INSERT INTO admin_sync_acknowledgements/);
    assert.doesNotMatch(ack, /UPDATE admin_sync_state|INSERT INTO admin_sync_changes/);
});

test('journal helpers do not impose admin authentication on captains', () => {
    const core = read('_admin_sync.php');
    assert.doesNotMatch(core, /require.*\/_admin\.php/);
    for (const endpoint of ['sync-accept', 'sync-changes', 'sync-current', 'sync-entry-review', 'sync-keep-local', 'scorecard-edit', 'entry-reviews']) {
        const code = read('admin/' + endpoint + '.php');
        assert.match(code, /require_once.*\/_admin\.php/);
        assert.match(code, /require_admin\(/);
    }
    for (const endpoint of ['scorecard', 'finalize']) {
        const code = read('captain/' + endpoint + '.php');
        assert.match(code, /require_captain\(/);
        assert.match(code, /require_post\(/);
        assert.match(code, /admin_sync_lock\(\)/);
        assert.match(code, /scorecard_journal_commit\(/);
        assert.match(code, /rollBack\(\)/);
    }
});

test('all legacy admin live-card mutation routes participate in journal transactions', () => {
    for (const endpoint of ['reopen-fixture', 'scorecards', 'fixtures', 'players']) {
        const code = read('admin/' + endpoint + '.php');
        assert.match(code, /_scorecard_journal\.php/);
        assert.match(code, /admin_sync_lock\(\)/);
        assert.match(code, /scorecard_journal_prepare\(/);
        assert.match(code, /scorecard_journal_commit\(/);
        assert.match(code, /beginTransaction\(\)/);
        assert.match(code, /rollBack\(\)/);
    }
    assert.match(read('admin/players.php'), /version=version\+1/);
    assert.match(read('admin/players.php'), /home_finalized_version=NULL/);
});

test('current comparison remains a read with no acknowledgement or recovery write', () => {
    const code = read('admin/sync-current.php');
    assert.doesNotMatch(code, /admin_sync_(prepare|commit)_change\(/);
    assert.match(code, /\$payload\['deleted'\]/);
    assert.match(code, /FOR UPDATE/);
    const submissions = read('captain/submit-result.php');
    assert.doesNotMatch(submissions, /scorecard_journal_commit|UPDATE live_scorecards/);
});
