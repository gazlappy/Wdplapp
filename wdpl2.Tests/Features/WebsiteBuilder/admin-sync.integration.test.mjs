// Uses only an empty disposable database. Never point this at a production database.
import test from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
const php = process.env.PHP_BINARY || 'php';
const available = !spawnSync(php, ['-v'], { stdio: 'ignore' }).error;
const configured = /^wdpl_admin_test_[a-zA-Z0-9_]+$/.test(process.env.WDPL_TEST_DB_NAME || '') && process.env.WDPL_TEST_DB_USER;
const root = fileURLToPath(new URL('../../../wdpl2/web-backend/api/', import.meta.url));
const quote = value => "'" + value.replaceAll('\\', '\\\\').replaceAll("'", "\\'") + "'";

test('journal rejects stale writes, replays identical requests, and rolls back data and sequence together', {
    skip: !available ? 'PHP runtime unavailable' : !configured ? 'Configure an empty wdpl_admin_test_* database' : false
}, () => {
    const directory = mkdtempSync(join(tmpdir(), 'wdpl-admin-sync-'));
    const source = `<?php
if (!preg_match('/^wdpl_admin_test_[a-zA-Z0-9_]+$/D', getenv('WDPL_TEST_DB_NAME'))) exit(2);
define('DB_HOST', getenv('WDPL_TEST_DB_HOST') ?: 'localhost');
define('DB_NAME', getenv('WDPL_TEST_DB_NAME'));
define('DB_USER', getenv('WDPL_TEST_DB_USER'));
define('DB_PASS', getenv('WDPL_TEST_DB_PASSWORD') ?: '');
require ${quote(join(root, '_admin_sync.php'))};
function verify($condition) { if (!$condition) throw new RuntimeException('Assertion failed'); }
verify(count(db()->query('SHOW TABLES')->fetchAll()) === 0);
try {
    admin_sync_ensure_schema();
    $actor = array('user_id' => '00000000-0000-0000-0000-000000000001');
    $request = '00000000-0000-0000-0000-000000000002';
    db()->beginTransaction();
    $prepared = admin_sync_prepare_change($actor, 'scorecard', 'fixture-1', 0, $request, array('notes' => 'reviewed'));
    $receipt = admin_sync_commit_change($actor, 'scorecard', 'fixture-1', 'season-1', 'web', array('notes' => 'reviewed'), $prepared);
    db()->commit();
    verify($receipt['revision'] === 1 && $receipt['sequence'] === 1);
    db()->beginTransaction();
    $replay = admin_sync_prepare_change($actor, 'scorecard', 'fixture-1', 0, $request, array('notes' => 'reviewed'));
    verify($replay['replayed'] && $replay['sequence'] === 1);
    $reuse = admin_sync_prepare_change($actor, 'scorecard', 'fixture-1', 0, $request, array('notes' => 'different'));
    verify($reuse['conflict']);
    $stale = admin_sync_prepare_change($actor, 'scorecard', 'fixture-1', 0, '00000000-0000-0000-0000-000000000003', array());
    verify($stale['conflict'] && $stale['current']['revision'] === 1);
    db()->rollBack();
    db()->beginTransaction();
    $prepared = admin_sync_prepare_change($actor, 'scorecard', 'fixture-1', 1, '00000000-0000-0000-0000-000000000004', array());
    admin_sync_commit_change($actor, 'scorecard', 'fixture-1', 'season-1', 'desktop', array('notes' => 'rollback'), $prepared);
    db()->rollBack();
    verify(admin_sync_read_record('scorecard', 'fixture-1')['revision'] === 1);
    verify((int)db()->query('SELECT sequence_id FROM admin_sync_state')->fetchColumn() === 1);
    echo 'PASS';
} finally {
    if (db()->inTransaction()) db()->rollBack();
    foreach (array('admin_sync_changes','admin_sync_records','admin_sync_state') as $table) db()->exec('DROP TABLE IF EXISTS ' . $table);
}
`;
    try {
        const file = join(directory, 'test.php');
        writeFileSync(file, source);
        const result = spawnSync(php, [file], { encoding: 'utf8' });
        assert.equal(result.status, 0, result.stderr || result.stdout);
        assert.equal(result.stdout, 'PASS', result.stderr || result.stdout);
    } finally { rmSync(directory, { recursive: true, force: true }); }
});
