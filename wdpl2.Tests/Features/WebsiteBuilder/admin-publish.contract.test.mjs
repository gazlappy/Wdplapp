import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const root = new URL('../../../wdpl2/web-backend/api/', import.meta.url);
const source = readFileSync(new URL('admin/publish-league.php', root), 'utf8');
// Structural checks supplement, not replace, disposable MySQL concurrency tests.
test('publication holds one transaction across core data, settings and results', () => {
    const start = source.indexOf('$pdo->beginTransaction()');
    const commit = source.indexOf('$pdo->commit()');
    assert.equal((source.match(/\$pdo->commit\(\)/g) || []).length, 1);
    assert.ok(source.indexOf('admin_sync_lock()', start) < source.indexOf('DELETE FROM league_fixtures', start));
    assert.ok(source.indexOf('publish_pending_conflicts', start) < source.indexOf('DELETE FROM league_fixtures', start));
    assert.ok(commit > source.indexOf('INSERT INTO league_frame_results'));
    assert.doesNotMatch(source.slice(start, commit), /CREATE TABLE|ALTER TABLE/);
    assert.match(source.slice(commit), /rollBack\(\)/);
});
test('publication preserves separate player identities and requires an explicit season', () => {
    assert.doesNotMatch(source, /DELETE cp FROM/);
    assert.match(source, /explicit season_id is required/);
});
test('publication checks backend identity and each reviewed revision, not a client timestamp', () => {
    const guard = readFileSync(new URL('_publish_schema.php', root), 'utf8');
    assert.match(guard, /sync_backend_id/);
    assert.match(guard, /sync_revisions/);
    assert.match(guard, /\$row\['source'\] === 'web'/);
    assert.match(guard, /\$revision !== \(int\)\$row\['revision'\]/);
});
