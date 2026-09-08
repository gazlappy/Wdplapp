// Dependency-free behavioral tests for the exact script embedded in generated forms.
// Run: node --test wdpl2.Tests/Features/WebsiteBuilder/entry-forms.browser.test.mjs
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';

const source = readFileSync(fileURLToPath(new URL('../../../wdpl2/Features/WebsiteBuilder/Generation/WebsiteGenerator.EntryForms.cs', import.meta.url)), 'utf8');
const script = source.match(/html\.AppendLine\("""\s*<script>([\s\S]*?)<\/script>\s*"""\);/)[1];

function harness({ endpoint = '', preview = false, valid = true, fetcher } = {}) {
    const created = [];
    const requests = [];
    const timers = [];
    const node = () => ({ textContent: '', hidden: false, disabled: false, children: [],
        attributes: {}, dataset: {}, classList: { toggle() {} }, focus() { this.focused = true; },
        setAttribute(key, value) { this.attributes[key] = value; }, removeAttribute(key) { delete this.attributes[key]; },
        replaceChildren() { this.children = []; }, append(...items) { this.children.push(...items); },
        click() { this.clicked = true; }, remove() {} });
    const field = { name: '<script>name</script>', value: '  A & B  ', type: 'text', tagName: 'INPUT', required: true, validity: { valid: true }, focus() {} };
    const checkbox = { name: 'Consent', type: 'checkbox', checked: true, tagName: 'INPUT', required: true, validity: { valid: true } };
    const steps = [1, 2, 3].map(step => { const item = node(); item.dataset.entryStep = String(step); return item; });
    const nodes = Object.fromEntries(['.entry-form-feedback', 'fieldset', '.entry-form-review', '.entry-form-review dl', '[data-edit]', '[type=submit]', 'progress', '[data-completion-label]'].map(key => [key, node()]));
    nodes['[type=submit]'].textContent = 'Review entry';
    const form = {
        dataset: { formId: 'form-1', endpoint, preview: String(preview), closingDate: '' },
        querySelector: selector => selector === '[data-entry-field]' ? field : nodes[selector],
        querySelectorAll: selector => selector === '[data-entry-step]' ? steps : [field, checkbox],
        addEventListener() {},
        reportValidity: () => valid, setAttribute() {}, removeAttribute() {}
    };
    let sequence = 0;
    const context = vm.createContext({
        window: { entryFormConfig: { 'form-1': { submitText: 'Send team entry', confirmation: 'Thank you!' } } },
        document: { createElement() { const item = node(); created.push(item); return item; },
            querySelectorAll() { return [form]; }, body: { appendChild() {} } },
        crypto: { randomUUID: () => `submission-${++sequence}` },
        URL: { createObjectURL: () => 'blob:test', revokeObjectURL() {} }, Blob, AbortController,
        setTimeout: (callback, delay) => { timers.push({ callback, delay }); return timers.length; }, clearTimeout() {},
        fetch: async (url, options) => { requests.push({ url, options }); return fetcher ? fetcher(url, options) : { ok: true, json: async () => ({ accepted: true, id: JSON.parse(options.body).id }) }; }
        // Intentionally no localStorage: any automatic persistence fails the tests.
    });
    vm.runInContext(script, context);
    return { context, form, nodes, field, checkbox, steps, created, requests, timers,
        submit: () => context.handleEntrySubmit(form), edit: () => context.editEntryForm(form) };
}
const settle = () => new Promise(resolve => setImmediate(resolve));

test('completion counts only valid required fields including checked consent', () => {
    const h = harness();
    assert.equal(h.nodes.progress.value, 100);
    h.field.value = '   '; h.context.entryCompletion(h.form);
    assert.equal(h.nodes.progress.value, 50);
    h.checkbox.checked = false; h.context.entryCompletion(h.form);
    assert.equal(h.nodes.progress.value, 0);
    h.field.value = 'invalid email'; h.field.validity.valid = false;
    h.context.entryCompletion(h.form); assert.equal(h.nodes.progress.value, 0);
    h.field.required = h.checkbox.required = false;
    h.context.entryCompletion(h.form);
    assert.match(h.nodes['[data-completion-label]'].textContent, /optional/);
});

test('step state follows review and edit without changing another form', () => {
    const h = harness(); const other = harness();
    h.submit(); assert.equal(h.steps[1].attributes['aria-current'], 'step');
    h.edit(); assert.equal(h.steps[0].attributes['aria-current'], 'step');
    assert.equal(h.steps[1].attributes['aria-current'], undefined);
    assert.equal(other.form.dataset.reviewed, undefined);
});

test('download stage never claims confirmed delivery', () => {
    const h = harness(); h.submit(); h.submit();
    assert.equal(h.steps[2].attributes['aria-current'], 'step');
    assert.notEqual(h.form.dataset.sent, 'true');
});

test('invalid details never enter review or send', () => {
    const h = harness({ valid: false });
    assert.equal(h.submit(), false);
    assert.notEqual(h.form.dataset.reviewed, 'true');
    assert.equal(h.requests.length, 0);
});

test('review uses text nodes, trimmed values and explicit checkbox values', () => {
    const h = harness(); h.submit();
    const children = h.nodes['.entry-form-review dl'].children;
    assert.equal(children[0].textContent, '<script>name</script>');
    assert.equal(children[1].textContent, 'A & B');
    assert.equal(children[3].textContent, 'Yes');
    assert.equal(h.nodes['fieldset'].disabled, true);
    assert.equal(h.nodes['[type=submit]'].textContent, 'Download entry');
    assert.equal(h.requests.length, 0);
});

test('back to edit restores fields without sending', () => {
    const h = harness(); h.submit(); h.edit();
    assert.equal(h.nodes['fieldset'].disabled, false);
    assert.equal(h.nodes['fieldset'].hidden, false);
    assert.equal(h.form.dataset.reviewed, 'false');
    assert.equal(h.requests.length, 0);
});

for (const endpoint of ['', 'https://example.test/entries']) {
    test(`preview never downloads or sends, including repeated tests and edits (${endpoint || 'download mode'})`, () => {
        const h = harness({ preview: true, endpoint });
        for (let attempt = 0; attempt < 2; attempt++) {
            h.submit();
            assert.equal(h.nodes['[type=submit]'].textContent, 'Test confirmation');
            const before = h.created.length;
            h.submit(); h.submit();
            assert.equal(h.created.length, before);
            assert.equal(h.requests.length, 0);
            assert.equal(h.timers.length, 0);
            assert.equal(h.form.dataset.submissionId, undefined);
            assert.notEqual(h.form.dataset.attempted, 'true');
            assert.notEqual(h.form.dataset.sent, 'true');
            assert.match(h.nodes['.entry-form-feedback'].textContent, /Nothing was sent or saved/);
            h.edit();
            assert.equal(h.nodes['fieldset'].disabled, false);
            assert.equal(h.form.dataset.reviewed, 'false');
            h.field.value = 'Edited preview';
        }
    });
}

test('offline download is explicitly not a submission and keeps unchanged entry identity', () => {
    const h = harness(); h.submit(); h.submit();
    const id = h.form.dataset.submissionId;
    assert.equal(h.requests.length, 0);
    assert.match(h.nodes['.entry-form-feedback'].textContent, /NOT been submitted/);
    h.submit(); assert.equal(h.form.dataset.submissionId, id);
    h.edit(); h.field.value = 'Different'; h.submit(); h.submit();
    assert.notEqual(h.form.dataset.submissionId, id);
});

test('successful POST requires matching receipt and prevents double submit', async () => {
    const h = harness({ endpoint: 'https://example.test/entries' });
    h.submit(); h.submit(); h.submit();
    assert.equal(h.requests.length, 1);
    await settle();
    assert.equal(h.form.dataset.sent, 'true');
    assert.match(h.nodes['.entry-form-feedback'].textContent, /Received for review/);
    assert.equal(h.requests[0].options.credentials, 'omit');
    assert.deepEqual(Object.keys(h.requests[0].options.headers).sort(), ['Accept', 'Content-Type']);
    h.submit(); assert.equal(h.requests.length, 1);
});

for (const [name, fetcher] of [
    ['HTTP failure', async () => ({ ok: false })],
    ['network failure', async () => { throw new Error('offline'); }],
    ['invalid JSON', async () => ({ ok: true, json: async () => { throw new Error('not JSON'); } })],
    ['missing acknowledgement', async () => ({ ok: true, json: async () => ({}) })],
    ['wrong receipt identity', async () => ({ ok: true, json: async () => ({ accepted: true, id: 'wrong' }) })]
]) {
    test(`${name} retains reviewed values and retries identical payload`, async () => {
        const h = harness({ endpoint: 'https://example.test/entries', fetcher });
        h.submit(); h.submit(); await settle();
        assert.notEqual(h.form.dataset.sent, 'true');
        assert.equal(h.nodes['[type=submit]'].disabled, false);
        assert.equal(h.nodes['[type=submit]'].textContent, 'Send team entry');
        assert.equal(h.nodes['[data-edit]'].disabled, true);
        assert.match(h.nodes['.entry-form-feedback'].textContent, /could not be confirmed/);
        h.edit(); assert.equal(h.form.dataset.reviewed, 'true');
        h.submit(); await settle();
        assert.equal(h.requests[0].options.body, h.requests[1].options.body);
    });
}

test('stale generated page enforces expired date in browser', () => {
    const h = harness(); h.form.dataset.closingDate = '2000-01-01'; h.submit();
    assert.match(h.nodes['.entry-form-feedback'].textContent, /now closed/);
    assert.equal(h.requests.length, 0);
});

test('request timeout aborts and permits retry without success', async () => {
    const h = harness({ endpoint: 'https://example.test/entries', fetcher: (_, options) => new Promise((_, reject) => options.signal.addEventListener('abort', () => reject(new Error('timeout')))) });
    h.submit(); h.submit(); h.timers.find(t => t.delay === 30000).callback(); await settle();
    assert.notEqual(h.form.dataset.sent, 'true');
    assert.equal(h.nodes['[type=submit]'].disabled, false);
});
