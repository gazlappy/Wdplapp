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
    const worker = join(directory, 'concurrent.php');
    writeFileSync(worker, `<?php
define('DB_HOST', getenv('WDPL_TEST_DB_HOST') ?: 'localhost');
define('DB_NAME', getenv('WDPL_TEST_DB_NAME'));
define('DB_USER', getenv('WDPL_TEST_DB_USER'));
define('DB_PASS', getenv('WDPL_TEST_DB_PASSWORD') ?: '');
if (!preg_match('/^wdpl_admin_test_[a-zA-Z0-9_]+$/D', DB_NAME)) exit(2);
require ${quote(join(root, '_admin_sync.php'))};
db()->beginTransaction();
$actor = array('user_id' => '00000000-0000-0000-0000-000000000001');
$prepared = admin_sync_prepare_change($actor, 'scorecard', 'race-card', 0, admin_sync_guid(), array('action'=>'race'));
if (!empty($prepared['conflict'])) { db()->rollBack(); echo 'CONFLICT'; exit; }
admin_sync_commit_change($actor, 'scorecard', 'race-card', 'season-1', 'web', array('version'=>1), $prepared);
db()->commit(); echo 'COMMITTED';
`);
    const source = `<?php
if (!preg_match('/^wdpl_admin_test_[a-zA-Z0-9_]+$/D', getenv('WDPL_TEST_DB_NAME'))) exit(2);
define('DB_HOST', getenv('WDPL_TEST_DB_HOST') ?: 'localhost');
define('DB_NAME', getenv('WDPL_TEST_DB_NAME'));
define('DB_USER', getenv('WDPL_TEST_DB_USER'));
define('DB_PASS', getenv('WDPL_TEST_DB_PASSWORD') ?: '');
require ${quote(join(root, '_admin_sync.php'))};
require ${quote(join(root, '_scorecard_journal.php'))};
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
    db()->exec('CREATE TABLE live_scorecards (fixture_id VARCHAR(64) PRIMARY KEY, version INT NOT NULL, state_json MEDIUMTEXT NOT NULL, home_finalized_version INT NULL, away_finalized_version INT NULL) ENGINE=InnoDB');
    db()->beginTransaction();
    admin_sync_lock();
    $prepared = scorecard_journal_prepare($actor, 'fixture-2', 'captain.initialize');
    db()->prepare('INSERT INTO live_scorecards VALUES (?, 0, ?, NULL, NULL)')->execute(array('fixture-2', '{"frames":[]}'));
    scorecard_journal_commit($actor, 'fixture-2', 'season-1', $prepared);
    db()->commit();
    verify(admin_sync_read_record('scorecard', 'fixture-2')['revision'] === 1);
    db()->beginTransaction();
    admin_sync_lock();
    $prepared = scorecard_journal_prepare($actor, 'fixture-2', 'captain.finalize');
    db()->exec("UPDATE live_scorecards SET home_finalized_version = 0 WHERE fixture_id = 'fixture-2'");
    scorecard_journal_commit($actor, 'fixture-2', 'season-1', $prepared);
    db()->commit();
    verify(admin_sync_read_record('scorecard', 'fixture-2')['payload']['home_finalized'] === true);
    db()->beginTransaction();
    admin_sync_lock();
    $prepared = scorecard_journal_prepare($actor, 'fixture-2', 'admin.delete');
    db()->exec("DELETE FROM live_scorecards WHERE fixture_id = 'fixture-2'");
    scorecard_journal_commit($actor, 'fixture-2', 'season-1', $prepared, true);
    db()->rollBack();
    verify(admin_sync_read_record('scorecard', 'fixture-2')['revision'] === 2);
    verify((int)db()->query('SELECT COUNT(*) FROM live_scorecards')->fetchColumn() === 1);
    db()->beginTransaction();
    admin_sync_lock();
    $prepared = scorecard_journal_prepare($actor, 'fixture-2', 'admin.delete');
    db()->exec("DELETE FROM live_scorecards WHERE fixture_id = 'fixture-2'");
    scorecard_journal_commit($actor, 'fixture-2', 'season-1', $prepared, true);
    db()->commit();
    verify(admin_sync_read_record('scorecard', 'fixture-2')['payload'] === array('deleted' => true));
    verify(admin_sync_read_record('scorecard', 'fixture-2')['revision'] === 3);
    // No-op acknowledgements survive lost responses without extending the feed.
    foreach (array('scorecard', 'entry_review') as $kind) {
        $id = $kind === 'scorecard' ? 'ack-card' : '123';
        $accept = array('action' => 'accept_unchanged');
        db()->beginTransaction();
        $prepared = admin_sync_prepare_change($actor, $kind, $id, 0, admin_sync_guid(), array('seed'=>true));
        $original = admin_sync_commit_change($actor, $kind, $id, null, 'desktop', array('notes'=>'original'), $prepared);
        db()->commit();
        $ackRequest = admin_sync_guid();
        $through = (int)db()->query('SELECT sequence_id FROM admin_sync_state')->fetchColumn();
        db()->beginTransaction();
        $prepared = admin_sync_prepare_change($actor, $kind, $id, 1, $ackRequest, $accept);
        $ack = admin_sync_acknowledge($actor, $kind, $id, $prepared);
        verify($ack === $original);
        db()->rollBack();
        $query = db()->prepare('SELECT COUNT(*) FROM admin_sync_acknowledgements WHERE request_id = ?');
        $query->execute(array($ackRequest)); verify((int)$query->fetchColumn() === 0);
        db()->beginTransaction();
        $prepared = admin_sync_prepare_change($actor, $kind, $id, 1, $ackRequest, $accept);
        verify(empty($prepared['replayed']));
        admin_sync_acknowledge($actor, $kind, $id, $prepared);
        db()->commit();
        verify((int)db()->query('SELECT sequence_id FROM admin_sync_state')->fetchColumn() === $through);
        db()->beginTransaction();
        $prepared = admin_sync_prepare_change($actor, $kind, $id, 1, admin_sync_guid(), array('edit'=>true));
        admin_sync_commit_change($actor, $kind, $id, null, 'web', array('notes'=>'later edit'), $prepared);
        db()->commit();
        db()->beginTransaction();
        $replayed = admin_sync_prepare_change($actor, $kind, $id, 1, $ackRequest, $accept);
        verify($replayed['replayed'] && $replayed['revision'] === 1 && $replayed['sequence'] === $original['sequence']);
        verify(admin_sync_prepare_change($actor, $kind, $id, 1, $ackRequest, array('action'=>'changed'))['conflict']);
        verify(admin_sync_prepare_change(array('user_id'=>'00000000-0000-0000-0000-000000000099'), $kind, $id, 1, $ackRequest, $accept)['conflict']);
        verify(admin_sync_prepare_change($actor, $kind, 'different-record', 1, $ackRequest, $accept)['conflict']);
        verify(admin_sync_prepare_change($actor, $kind, $id, 2, $ackRequest, $accept)['conflict']);
        verify(admin_sync_prepare_change($actor, $kind, $id, 1, admin_sync_guid(), $accept)['conflict']);
        db()->commit();
        verify(admin_sync_read_record($kind, $id)['payload']['notes'] === 'later edit');
        verify((int)db()->query('SELECT sequence_id FROM admin_sync_state')->fetchColumn() === $through + 1);
    }
    // Both processes contend on the same expected revision. Only one may commit.
    $workers = array();
    db()->beginTransaction();
    admin_sync_lock();
    for ($i = 0; $i < 2; $i++) {
        $pipes = array();
        $process = proc_open(array(PHP_BINARY, ${quote(worker)}), array(0=>array('pipe','r'),1=>array('pipe','w'),2=>array('pipe','w')), $pipes);
        verify(is_resource($process));
        fclose($pipes[0]);
        $workers[] = array($process, $pipes);
    }
    db()->commit();
    $outcomes = array();
    foreach ($workers as $worker) {
        $outcomes[] = stream_get_contents($worker[1][1]); fclose($worker[1][1]);
        $errors = stream_get_contents($worker[1][2]); fclose($worker[1][2]);
        verify(proc_close($worker[0]) === 0 && $errors === '');
    }
    sort($outcomes);
    verify($outcomes === array('COMMITTED', 'CONFLICT'));
    verify(admin_sync_read_record('scorecard', 'race-card')['revision'] === 1);
    echo 'PASS';
} finally {
    if (db()->inTransaction()) db()->rollBack();
    foreach (array('live_scorecards','admin_sync_acknowledgements','admin_sync_changes','admin_sync_records','admin_sync_state') as $table) db()->exec('DROP TABLE IF EXISTS ' . $table);
}
`;
    try {
        const file = join(directory, 'test.php');
        writeFileSync(file, source);
        const result = spawnSync(php, [file], { encoding: 'utf8', timeout: 120000 });
        assert.equal(result.status, 0, result.stderr || result.stdout);
        assert.equal(result.stdout, 'PASS', result.stderr || result.stdout);
    } finally { rmSync(directory, { recursive: true, force: true }); }
});
