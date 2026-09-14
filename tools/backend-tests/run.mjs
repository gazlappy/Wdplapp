import { readdirSync } from 'node:fs';
import { join } from 'node:path';
import { spawnSync } from 'node:child_process';

function run(command, args, capture = false) {
    const result = spawnSync(command, args, { encoding: 'utf8', stdio: capture ? 'pipe' : 'inherit' });
    if (result.error || result.status !== 0) {
        if (capture) process.stderr.write((result.stdout || '') + (result.stderr || ''));
        throw new Error(`${command} failed: ${result.error?.message || result.status}`);
    }
    return result.stdout || '';
}
function files(directory) {
    return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
        const path = join(directory, entry.name);
        return entry.isDirectory() ? files(path) : [path];
    });
}
try {
    if (!/^wdpl_admin_test_[a-zA-Z0-9_]+$/.test(process.env.WDPL_TEST_DB_NAME || '') || !process.env.WDPL_TEST_DB_USER)
        throw new Error('An empty disposable wdpl_admin_test_* database is required. No tests were run.');
    const php = process.env.PHP_BINARY || 'php';
    run(php, ['-r', 'if (PHP_VERSION_ID < 80200 || !extension_loaded("pdo_mysql")) exit(2);']);
    for (const file of files('wdpl2/web-backend').filter(f => f.endsWith('.php'))) run(php, ['-l', file]);
    const testRoot = 'wdpl2.Tests/Features/WebsiteBuilder';
    for (const file of files(testRoot).filter(f => f.endsWith('.rules.test.php'))) run(php, [file]);
    const browserTests = files(testRoot).filter(f => f.endsWith('.browser.test.mjs') || f.endsWith('legacy-scorecard-journal.test.mjs'));
    run('node', ['--test', ...browserTests]);
    const output = run('node', ['--test', '--test-reporter=tap', join(testRoot, 'admin-sync.integration.test.mjs')], true);
    process.stdout.write(output);
    if (/\bSKIP\b/i.test(output)) throw new Error('Integration tests skipped; backend validation is incomplete.');
    console.log('Backend automated checks passed. Endpoint/concurrency smoke tests and conflict recovery remain separate release gates.');
} catch (error) {
    console.error(error.message);
    process.exitCode = 1;
}
