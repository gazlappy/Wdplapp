import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { randomUUID } from 'node:crypto';
import vm from 'node:vm';

const root = new URL('../../../wdpl2/web-backend/', import.meta.url);
const html = readFileSync(new URL('api/admin/index.html', root), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
const code = script.slice(script.indexOf('let _edFid ='), script.indexOf('/* Fixtures */'));
const backend = randomUUID();
function harness(write, role = 'admin') {
  const nodes = new Map(), calls = [];
  const $ = id => {
    if (!nodes.has(id)) nodes.set(id, { textContent:'', innerHTML:'', hidden:false, disabled:false, classList:{add(){},remove(){}} });
    return nodes.get(id);
  };
  const context = vm.createContext({ $, session:{user:{role}}, crypto:{randomUUID}, confirm:()=>true, flash(){},
    document:{querySelectorAll:()=>[]}, window:{addEventListener(){}},
    api:async (path, options) => {
      calls.push({path, options});
      if (options) return write(JSON.parse(options.body));
      return {protocol:1, backendId:backend, revision:3, version:7, state:{frames:[]}};
    }
  });
  vm.runInContext(code + '\nrenderEditor = function() {};', context);
  return {context, calls, $};
}
const receipt = body => ({protocol:1, accepted:true, backendId:body.backend_id, id:body.fixture_id,
  requestId:body.request_id, revision:body.expected_revision+1, sequence:9});

test('editor sends loaded version and revision, then refreshes only after a matching receipt', async () => {
  const h = harness(body => receipt(body));
  await h.context.scEdit('fixture-1');
  await h.context.edWrite({action:'set_notes', value:'Reviewed'});
  const body = JSON.parse(h.calls[1].options.body);
  assert.equal(body.expected_version, 7);
  assert.equal(body.expected_revision, 3);
  assert.equal(body.backend_id, backend);
  assert.equal(body.fixture_id, 'fixture-1');
  assert.equal(h.calls.length, 3);
  assert.match(h.$('scEditStatus').textContent, /journaled/);
});

test('uncertain writes retry the exact serialized payload and reject a different edit', async () => {
  let attempts = 0;
  const h = harness(body => { if (++attempts === 1) throw new Error('Connection lost'); return receipt(body); });
  await h.context.scEdit('fixture-1');
  await assert.rejects(h.context.edWrite({action:'add_frame'}), /Connection lost/);
  assert.equal(h.$('scRetry').hidden, false);
  await assert.rejects(h.context.edWrite({action:'remove_frame'}), /original saved edit/);
  await h.context.edRetry();
  assert.equal(h.calls[1].options.body, h.calls[2].options.body);
});

test('409 preserves draft and blocks writes until explicit reload', async () => {
  const h = harness(() => { throw Object.assign(new Error('Stale'), {status:409}); });
  await h.context.scEdit('fixture-1');
  h.$('scModalBody').innerHTML = 'unsaved draft';
  await assert.rejects(h.context.edWrite({action:'set_notes', value:'draft'}));
  assert.equal(h.$('scModalBody').innerHTML, 'unsaved draft');
  await assert.rejects(h.context.edWrite({action:'add_frame'}), /Reload/);
  assert.equal(h.calls.length, 2);
  await h.context.edReloadReview();
  assert.equal(h.calls.length, 3);
});

test('mismatched receipt keeps request available and readonly sessions cannot write', async () => {
  const h = harness(body => ({...receipt(body), requestId:randomUUID()}));
  await h.context.scEdit('fixture-1');
  await assert.rejects(h.context.edWrite({action:'add_frame'}), /Receipt mismatch/);
  assert.equal(h.$('scRetry').hidden, false);
  const ro = harness(() => {throw new Error('Must not send');}, 'readonly');
  await ro.context.scEdit('fixture-1');
  await assert.rejects(ro.context.edWrite({action:'add_frame'}), /Read-only/);
  assert.equal(ro.calls.length, 1);
});

test('reset ignores a late write receipt and clears private draft', async () => {
  let finish;
  const h = harness(body => new Promise(resolve => {finish = () => resolve(receipt(body));}));
  await h.context.scEdit('fixture-1');
  const writing = h.context.edWrite({action:'set_notes', value:'private'});
  h.context.edReset(); finish(); await writing;
  assert.equal(h.$('scModalBody').innerHTML, '');
  assert.equal(h.$('scRetry').hidden, true);
  assert.equal(h.calls.length, 2);
});

test('endpoint structurally checks version before mutation and journals before commit', () => {
  const php = readFileSync(new URL('api/admin/scorecard-edit.php', root), 'utf8');
  const post = php.slice(php.indexOf('require_post();'));
  assert.ok(post.indexOf('admin_sync_prepare_change') < post.indexOf('FOR UPDATE'));
  assert.ok(post.indexOf('$ver !== $expectedVersion') < post.indexOf("$action === 'set_frame'"));
  assert.ok(post.indexOf("$prepared['replayed']") < post.indexOf('FOR UPDATE'));
  const mutation = post.slice(post.indexOf("if ($action === 'set_frame')"));
  assert.ok(mutation.indexOf('admin_sync_commit_change') < mutation.indexOf('$pdo->commit()'));
  assert.equal((mutation.match(/\$pdo->commit\(\)/g) || []).length, 1);
  assert.match(mutation, /home_finalized_at = NULL/);
});
