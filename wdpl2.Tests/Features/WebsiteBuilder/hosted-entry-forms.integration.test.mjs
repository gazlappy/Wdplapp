// Opt-in endpoint/database tests. Use ONLY an empty disposable database named wdpl_entryforms_test_*.
// See Docs/OnlineForms.md. No production config or database is used.
import test from 'node:test';
import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomBytes } from 'node:crypto';
import { createServer } from 'node:net';

const php = process.env.PHP_BINARY || 'php';
const available = !spawnSync(php, ['-v'], { stdio: 'ignore' }).error;
const database = process.env.WDPL_TEST_DB_NAME || '';
const configured = /^wdpl_entryforms_test_[a-zA-Z0-9_]+$/.test(database) && process.env.WDPL_TEST_DB_USER;
const root = fileURLToPath(new URL('../../../wdpl2/web-backend/api/', import.meta.url));
const quote = value => "'" + value.replaceAll('\\', '\\\\').replaceAll("'", "\\'") + "'";

async function freePort() {
    const server = createServer();
    await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
    const port = server.address().port;
    await new Promise(resolve => server.close(resolve));
    return port;
}

test('hosted endpoint auth, validation, idempotence, collection and throttling', {
    skip: !available ? 'PHP runtime unavailable' : !configured ? 'Set a disposable WDPL_TEST_DB_NAME and WDPL_TEST_DB_USER' : false,
    timeout: 60000
}, async () => {
    const directory = mkdtempSync(join(tmpdir(), 'wdpl-entry-forms-test-'));
    const password = randomBytes(24).toString('hex');
    const env = { ...process.env, WDPL_TEST_ADMIN_PASSWORD: password };
    const bootstrap = `<?php
$database = getenv('WDPL_TEST_DB_NAME');
if (!preg_match('/^wdpl_entryforms_test_[a-zA-Z0-9_]+$/D', $database)) exit(2);
define('DB_HOST', getenv('WDPL_TEST_DB_HOST') ?: 'localhost');
define('DB_NAME', $database);
define('DB_USER', getenv('WDPL_TEST_DB_USER'));
define('DB_PASS', getenv('WDPL_TEST_DB_PASSWORD') ?: '');
`;
    writeFileSync(join(directory, 'bootstrap.php'), bootstrap);
    const preamble = `<?php require ${quote(join(directory, 'bootstrap.php'))}; require ${quote(join(root, '_entry_forms.php'))}; `;
    writeFileSync(join(directory, 'setup.php'), preamble + `
if (count(db()->query('SHOW TABLES')->fetchAll()) !== 0) throw new RuntimeException('Test database must be empty.');
ef_ensure_schema(); require ${quote(join(root, '_admin.php'))};
admin_create_user('test_admin', getenv('WDPL_TEST_ADMIN_PASSWORD'), 'Test admin');
`);
    writeFileSync(join(directory, 'cleanup.php'), preamble + `foreach (array('entry_form_rate_limits','entry_form_submissions','entry_form_definitions','entry_form_config','admin_sessions','admin_users') as $table) db()->exec('DROP TABLE IF EXISTS ' . $table);`);
    writeFileSync(join(directory, 'seed.php'), preamble + `
$insert = db()->prepare('INSERT INTO entry_form_submissions (form_id,client_id,payload_hash,payload_json,received_utc) VALUES (?,?,?,?,UTC_TIMESTAMP())');
for ($i = 0; $i < 105; $i++) {
  $item = (object)array('id'=>'seed-'.$i,'formId'=>'form-0123456789abcdef0123456789abcdef','name'=>'Seed','values'=>(object)array('Team'=>'Seed'),'submittedAt'=>'2026-01-01T00:00:00Z');
  $json = json_encode($item); $insert->execute(array($item->formId,$item->id,hash('sha256',$json),$json));
}
`);
    writeFileSync(join(directory, 'router.php'), `<?php
require ${quote(join(directory, 'bootstrap.php'))};
// Local test transport only. Production endpoints still require the server's real HTTPS flag.
$_SERVER['HTTPS'] = 'on';
$path = parse_url($_SERVER['REQUEST_URI'], PHP_URL_PATH);
if ($path === '/ready') { echo 'ready'; return; }
$routes = array('/submit'=>${quote(join(root, 'entry-forms/submit.php'))}, '/definitions'=>${quote(join(root, 'admin/entry-form-definitions.php'))}, '/collection'=>${quote(join(root, 'admin/entry-form-submissions.php'))});
if (!isset($routes[$path])) { http_response_code(404); return; }
require $routes[$path];
`);
    let initialized = false;
    let server;
    const run = file => {
        const result = spawnSync(php, [join(directory, file)], { env, encoding: 'utf8' });
        assert.equal(result.status, 0, `PHP ${file} failed: ${result.stderr || result.stdout}`);
        assert.equal(result.stdout.trim(), '', `PHP ${file} returned an error response: ${result.stdout}`);
    };
    try {
        run('setup.php'); initialized = true;
        const port = await freePort();
        server = spawn(php, ['-S', `127.0.0.1:${port}`, join(directory, 'router.php')], { env, stdio: 'ignore' });
        const base = `http://127.0.0.1:${port}`;
        let ready = false;
        for (let attempt = 0; attempt < 50; attempt++) {
            try { ready = (await fetch(base + '/ready')).ok; } catch { }
            if (ready) break;
            await new Promise(resolve => setTimeout(resolve, 100));
        }
        assert.ok(ready, 'Local PHP test server did not start');
        const origin = 'https://league.example.test';
        const auth = 'Basic ' + Buffer.from('test_admin:' + password).toString('base64');
        const form = { id: 'form-0123456789abcdef0123456789abcdef', title: 'Team entry', closed: false, closingDate: null,
            fields: [{ label: 'Team', type: 'text', required: true, options: [] }] };
        const definitions = { protocol: 1, origin, timeZone: 'Europe/London', forms: [form] };
        const post = (path, body, headers = {}) => fetch(base + path, { method: 'POST', headers: { 'Content-Type': 'application/json', ...headers }, body: JSON.stringify(body) });
        const submit = (body, websiteOrigin = origin) => post('/submit', body, { Origin: websiteOrigin });
        const entry = { id: 'stable-1', formId: form.id, name: 'Aces', values: { Team: 'Aces' }, submittedAt: '2026-01-01T00:00:00.000Z' };
        assert.equal((await post('/definitions', definitions)).status, 401);
        assert.equal((await post('/definitions', definitions, { Authorization: 'Basic ' + Buffer.from('test_admin:wrong').toString('base64') })).status, 401);
        assert.equal((await post('/definitions', definitions, { Authorization: auth })).status, 200);
        assert.equal((await fetch(base + '/collection')).status, 401);
        assert.equal((await submit(entry, 'https://other.example.test')).status, 403);
        const preflight = await fetch(base + '/submit', { method: 'OPTIONS', headers: { Origin: origin } });
        assert.equal(preflight.status, 204);
        assert.equal(preflight.headers.get('access-control-allow-origin'), origin);
        assert.equal((await submit({ ...entry, values: { Team: '' } })).status, 422);
        assert.equal((await submit({ ...entry, values: { Team: 'Aces', Extra: 'x' } })).status, 422);
        assert.equal((await submit({ ...entry, formId: 'form-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' })).status, 422);
        assert.equal((await submit({ ...entry, values: { Team: 'x'.repeat(70000) } })).status, 413);
        const simultaneous = await Promise.all([submit(entry), submit(entry)]);
        for (const response of simultaneous) assert.deepEqual(await response.json(), { accepted: true, id: entry.id });
        assert.equal((await submit({ ...entry, values: { Team: 'Different' } })).status, 409);
        let collection = await (await fetch(base + '/collection', { headers: { Authorization: auth } })).json();
        assert.equal(collection.submissions.length, 1);
        assert.equal(collection.submissions[0].values.Team, 'Aces');
        form.closed = true;
        assert.equal((await post('/definitions', definitions, { Authorization: auth })).status, 200);
        assert.equal((await submit({ ...entry, id: 'closed-new' })).status, 422);
        assert.equal((await submit(entry)).status, 200);
        definitions.forms = [];
        assert.equal((await post('/definitions', definitions, { Authorization: auth })).status, 200);
        assert.equal((await submit({ ...entry, id: 'unpublished-new' })).status, 422);
        assert.equal((await submit(entry)).status, 200);
        run('seed.php');
        collection = await (await fetch(base + '/collection', { headers: { Authorization: auth } })).json();
        assert.equal(collection.submissions.length, 100);
        assert.ok(collection.nextAfter > 0);
        const rest = await (await fetch(base + `/collection?after=${collection.nextAfter}&through=${collection.through}`, { headers: { Authorization: auth } })).json();
        assert.equal(rest.submissions.length, 6);
        assert.equal(rest.nextAfter, null);
        let limited = false;
        for (let attempt = 0; attempt < 31; attempt++) {
            if ((await submit(entry)).status === 429) { limited = true; break; }
        }
        assert.ok(limited, 'Expected server-side throttling');
    } finally {
        if (server) {
            const stopped = new Promise(resolve => server.once('exit', resolve));
            if (server.exitCode === null) { server.kill(); await stopped; }
        }
        if (initialized) run('cleanup.php');
        rmSync(directory, { recursive: true, force: true });
    }
});
