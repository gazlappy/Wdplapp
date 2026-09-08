import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';

const root = new URL('../../../wdpl2/web-backend/', import.meta.url);
const html = readFileSync(new URL('api/admin/index.html', root), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
const apiCode = script.slice(script.indexOf('async function api('), script.indexOf('function isSuper('));

function harness(response) {
    const calls = [];
    const context = vm.createContext({
        API: '..', session: { user: { username: 'admin' } },
        fetch: async (url, opts) => { calls.push({ url, opts }); return response; },
        showLogin: () => calls.push('login')
    });
    vm.runInContext(apiCode, context);
    return { context, calls };
}

test('admin API uses cookies, no-store and protected request header without bearer credentials', async () => {
    const { context, calls } = harness({ ok: true, json: async () => ({ ok: true }) });
    await context.api('/admin/logout.php', { method: 'POST' });
    assert.equal(calls[0].opts.credentials, 'include');
    assert.equal(calls[0].opts.cache, 'no-store');
    assert.equal(calls[0].opts.headers['X-WDPL-Admin'], '1');
    assert.equal(calls[0].opts.headers.Authorization, undefined);
});

test('expired sessions return to login and propagate the error', async () => {
    const { context, calls } = harness({ ok: false, status: 401, json: async () => ({ error: 'unauthenticated' }) });
    await assert.rejects(context.api('/admin/scorecards.php'), /unauthenticated/);
    assert.equal(context.session.user, null);
    assert.equal(calls[1], 'login');
});

test('admin JavaScript parses and never persists session tokens', () => {
    new vm.Script(script);
    assert.doesNotMatch(script, /localStorage\.(getItem|setItem)/);
    assert.match(script, /localStorage\.removeItem\('wdpl_admin_token'\)/);
    const captains = readFileSync(new URL('api/admin/captains.html', root), 'utf8');
    new vm.Script(captains.match(/<script>([\s\S]*?)<\/script>/)[1]);
    assert.equal((captains.match(/'X-WDPL-Admin': '1'/g) || []).length, 2);
});
