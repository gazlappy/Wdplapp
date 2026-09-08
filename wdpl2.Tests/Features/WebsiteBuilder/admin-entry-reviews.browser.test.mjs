import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { randomUUID } from 'node:crypto';

const source = readFileSync(new URL('../../../wdpl2/web-backend/api/admin/entry-reviews.js', import.meta.url), 'utf8');
const backend = '00000000-0000-0000-0000-000000000001';
function item(id = 1) {
  return { id: String(id), revision: 0, receivedUtc: '2026-01-01 12:00:00',
    review: { submissionSequence: id, formId: 'form-1', clientId: 'client-' + id, status: 'pending', notes: '' },
    submission: { id: 'client-' + id, formId: 'form-1', name: '<img src=x onerror=alert(1)>', values: { '<script>': '<b>Original answer</b>' } } };
}
function page(items = [item()], through = 1, nextAfter = null) {
  return { protocol: 1, backendId: backend, through, nextAfter, items };
}
function element() {
  return { value: '', textContent: '', hidden: false, disabled: false, children: [], attributes: {},
    replaceChildren(...children) { this.children = children; this.textContent = ''; },
    append(...children) { this.children.push(...children); },
    setAttribute(key, value) { this.attributes[key] = value; },
    querySelectorAll() { return this.children; },
    set innerHTML(_) { throw new Error('Untrusted content must use textContent'); }
  };
}
function harness(send, role = 'admin') {
  const nodes = new Map(), calls = [], listeners = {};
  const get = id => { if (!nodes.has(id)) nodes.set(id, element()); return nodes.get(id); };
  const context = vm.createContext({
    document: { getElementById: get, createElement: element },
    session: { user: { role } }, TextEncoder, crypto: { randomUUID }, confirm: () => true,
    window: { addEventListener: (event, handler) => { listeners[event] = handler; } },
    api: async (path, options) => { calls.push({ path, options }); return send(path, options); }
  });
  vm.runInContext(source + '\nthis.reviews = EntryReviews;', context);
  const select = () => get('entryList').children[0].onclick();
  const save = () => get('entryReviewForm').onsubmit({ preventDefault() {} });
  return { context, get, calls, select, save, listeners, reviews: context.reviews };
}
function receipt(body) {
  return { protocol: 1, accepted: true, backendId: body.backend_id, id: body.id,
    requestId: body.request_id, revision: body.expected_revision + 1, sequence: 1 };
}

test('original answers render as text and saves only review metadata', async () => {
  const h = harness(async (_, options) => options ? receipt(JSON.parse(options.body)) : page());
  await h.reviews.open(); h.select();
  assert.equal(h.get('entryTitle').textContent, '<img src=x onerror=alert(1)>');
  assert.equal(h.get('entryAnswers').children[0].textContent, '<script>');
  assert.equal(h.get('entryAnswers').children[1].textContent, '<b>Original answer</b>');
  h.get('entryStatus').value = 'confirmed'; h.get('entryNotes').value = 'Checked';
  await h.save();
  const body = JSON.parse(h.calls[1].options.body);
  assert.deepEqual(Object.keys(body).sort(), ['backend_id', 'expected_revision', 'id', 'notes', 'request_id', 'status']);
  assert.equal(body.expected_revision, 0);
  assert.equal(h.get('entrySave').disabled, false);
  assert.match(h.get('entryMessage').textContent, /Desktop reconciliation/);
});

test('uncertain save freezes the payload and retries with the same request ID', async () => {
  let writes = 0;
  const h = harness(async (_, options) => {
    if (!options) return page();
    if (++writes === 1) throw new Error('Connection lost');
    return receipt(JSON.parse(options.body));
  });
  await h.reviews.open(); h.select(); h.get('entryNotes').value = 'Frozen';
  await h.save();
  assert.equal(h.get('entryNotes').disabled, true);
  assert.equal(h.get('entryRefresh').disabled, true);
  h.get('entryNotes').value = 'Must not be sent';
  await h.save();
  assert.equal(h.calls[1].options.body, h.calls[2].options.body);
  assert.equal(h.get('entryRefresh').disabled, false);
});

test('conflicts preserve drafts and require explicit reload, never automatic overwrite', async () => {
  const h = harness(async (_, options) => {
    if (!options) return page();
    throw Object.assign(new Error('Stale revision'), { status: 409 });
  });
  await h.reviews.open(); h.select(); h.get('entryNotes').value = 'My draft';
  await h.save();
  assert.equal(h.get('entryNotes').value, 'My draft');
  assert.equal(h.get('entrySave').disabled, true);
  await h.save(); assert.equal(h.calls.length, 2);
  await h.get('entryRefresh').onclick();
  assert.equal(h.get('entryDetail').hidden, true);
  h.select(); assert.equal(h.get('entrySave').disabled, false);
});

test('read-only users can inspect answers but cannot submit', async () => {
  const h = harness(async () => page(), 'readonly');
  await h.reviews.open(); h.select();
  assert.equal(h.get('entryStatus').disabled, true);
  assert.equal(h.get('entryNotes').disabled, true);
  await h.save(); assert.equal(h.calls.length, 1);
});

test('paging carries backend identity and fixed range and rejects a changed backend', async () => {
  let calls = 0;
  const h = harness(async () => ++calls === 1 ? page([item()], 2, 1) :
    { ...page([item(2)], 2), backendId: randomUUID() });
  await h.reviews.open();
  await h.get('entryNext').onclick();
  assert.match(h.calls[1].path, new RegExp('after=1&backendId=' + backend + '&through=2'));
  assert.equal(h.get('entryDetail').hidden, true);
  assert.match(h.get('entryMessage').textContent, /changed review page/);
});

test('mismatched receipts keep the original request available for retry', async () => {
  const h = harness(async (_, options) => options ? { ...receipt(JSON.parse(options.body)), requestId: randomUUID() } : page());
  await h.reviews.open(); h.select(); await h.save();
  assert.equal(h.get('entryNotes').disabled, true);
  assert.match(h.get('entrySave').textContent, /Retry/);
  assert.match(h.get('entryMessage').textContent, /Receipt does not match/);
});

test('session reset clears answers and ignores an in-flight page response', async () => {
  let resolve;
  const h = harness(() => new Promise(done => { resolve = done; }));
  const loading = h.reviews.open();
  h.reviews.reset(); resolve(page()); await loading;
  assert.equal(h.get('entryList').children.length, 0);
  assert.equal(h.get('entryAnswers').children.length, 0);
  assert.equal(h.get('entryNotes').value, '');
  assert.equal(h.get('entryDetail').hidden, true);
});

test('UTF-8 note limit is checked before any POST', async () => {
  const h = harness(async () => page());
  await h.reviews.open(); h.select(); h.get('entryNotes').value = 'é'.repeat(8001);
  await h.save();
  assert.equal(h.calls.length, 1);
  assert.match(h.get('entryMessage').textContent, /16,000/);
});
