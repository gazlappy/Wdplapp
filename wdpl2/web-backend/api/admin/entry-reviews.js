/* Private, in-memory review workspace. Original submission values are display-only. */
const EntryReviews = (() => {
  let items = [], backend = null, through = null, next = null, selected = null;
  let pending = null, conflict = false, busy = false, loaded = false, generation = 0;
  const el = id => document.getElementById(id);
  const canEdit = () => session.user && ['admin', 'superadmin'].includes(session.user.role);
  const validStatus = status => ['pending', 'confirmed', 'rejected'].includes(status);
  const uuid = value => typeof value === 'string' && /^[a-f0-9]{8}-(?:[a-f0-9]{4}-){3}[a-f0-9]{12}$/i.test(value);
  function message(text, error = false) {
    el('entryMessage').textContent = text;
    el('entryMessage').className = 'status ' + (error ? 'err' : 'info');
  }
  function dirty() {
    return selected && (el('entryStatus').value !== selected.review.status || el('entryNotes').value !== selected.review.notes);
  }
  function controls() {
    el('entryRefresh').disabled = busy || !!pending;
    el('entryNext').disabled = busy || !!pending || next === null;
    el('entryFilter').disabled = busy || !!pending;
    el('entryStatus').disabled = busy || !!pending || conflict || !canEdit();
    el('entryNotes').disabled = busy || !!pending || conflict || !canEdit();
    el('entrySave').disabled = busy || conflict || !selected || !canEdit();
    el('entrySave').textContent = pending ? 'Retry same saved request' : 'Save review';
    el('entryDiscard').hidden = !pending && !conflict;
    el('entryDiscard').disabled = busy;
    el('entryList').querySelectorAll('button').forEach(button => { button.disabled = busy || !!pending; });
  }
  function renderList() {
    el('entryList').replaceChildren();
    const filter = el('entryFilter').value;
    const visible = items.filter(item => !filter || item.review.status === filter);
    for (const item of visible) {
      const button = document.createElement('button');
      button.type = 'button';
      button.textContent = (item.submission.name || 'Unnamed entry') + '\n' + item.review.status + ' · #' + item.id;
      button.setAttribute('aria-pressed', String(selected === item));
      button.onclick = () => select(item);
      el('entryList').append(button);
    }
    if (!visible.length) el('entryList').textContent = 'No submissions match on this page.';
    el('entryCount').textContent = visible.length + ' shown / ' + items.length + ' on page';
    controls();
  }
  function select(item) {
    if (busy || pending || (dirty() && !confirm('Discard the unsaved review draft?'))) return;
    selected = item; conflict = false;
    el('entryTitle').textContent = item.submission.name || 'Unnamed entry';
    el('entryIdentity').textContent = 'Submission #' + item.id + ' · Revision ' + item.revision + '\nBackend: ' + backend +
      '\nForm: ' + item.review.formId + '\nClient: ' + item.review.clientId + '\nReceived (UTC): ' + item.receivedUtc;
    el('entryAnswers').replaceChildren();
    for (const [label, value] of Object.entries(item.submission.values)) {
      const term = document.createElement('dt'), answer = document.createElement('dd');
      term.textContent = label; answer.textContent = value;
      el('entryAnswers').append(term, answer);
    }
    el('entryStatus').value = item.review.status;
    el('entryNotes').value = item.review.notes;
    el('entryDetail').hidden = false;
    message(canEdit() ? 'Only status and review notes can be changed.' : 'Read-only access: review editing is disabled.');
    renderList();
  }
  function validatePage(page, after) {
    if (!page || page.protocol !== 1 || !uuid(page.backendId) || (backend && page.backendId !== backend) ||
        !Number.isSafeInteger(page.through) || page.through < after || (through !== null && through !== page.through) ||
        !Array.isArray(page.items) || page.items.length > 50) throw new Error('Invalid or changed review page. Reload before reviewing.');
    let cursor = after;
    for (const item of page.items) {
      const id = Number(item.id), review = item.review, submission = item.submission;
      if (typeof item.id !== 'string' || String(id) !== item.id || !Number.isSafeInteger(id) || id <= cursor || id > page.through ||
          !Number.isSafeInteger(item.revision) || item.revision < 0 || !review || review.submissionSequence !== id ||
          typeof review.formId !== 'string' || typeof review.clientId !== 'string' || !validStatus(review.status) || typeof review.notes !== 'string' ||
          !submission || submission.formId !== review.formId || submission.id !== review.clientId || typeof submission.name !== 'string' ||
          !submission.values || Array.isArray(submission.values) || typeof submission.values !== 'object' ||
          Object.values(submission.values).some(value => typeof value !== 'string')) throw new Error('Invalid submission identity or review data.');
      cursor = id;
    }
    if (page.nextAfter !== null && (!Number.isSafeInteger(page.nextAfter) || page.nextAfter !== cursor || cursor <= after || cursor >= page.through))
      throw new Error('Review pagination did not advance safely.');
  }
  async function load(restart = false) {
    if (busy || pending || (dirty() && !confirm('Discard the unsaved review draft?'))) return;
    const epoch = generation;
    const after = restart ? 0 : (next || 0);
    if (restart) { backend = null; through = null; }
    busy = true; controls(); message('Loading submissions…');
    try {
      let path = '/admin/entry-reviews.php?after=' + after;
      if (backend) path += '&backendId=' + encodeURIComponent(backend) + '&through=' + through;
      const page = await api(path);
      if (epoch !== generation) return;
      validatePage(page, after);
      items = page.items; backend = page.backendId; through = page.through; next = page.nextAfter;
      selected = null; conflict = false; loaded = true;
      el('entryDetail').hidden = true;
      renderList(); message(items.length ? 'Choose a submission to review.' : 'No submissions in this range.');
    } catch (error) {
      if (epoch === generation) {
        // A failed reload must not leave an old selection writable against a new backend.
        selected = null; items = []; loaded = false; next = null;
        el('entryDetail').hidden = true; renderList(); message(error.message, true);
      }
    } finally { if (epoch === generation) { busy = false; controls(); } }
  }
  async function save(event) {
    event.preventDefault();
    if (busy || conflict || !selected || !canEdit()) return;
    const epoch = generation;
    try {
      if (!pending) {
        const status = el('entryStatus').value, notes = el('entryNotes').value;
        if (!validStatus(status) || new TextEncoder().encode(notes).length > 16000) throw new Error('Choose a valid status and keep notes within 16,000 UTF-8 bytes.');
        pending = Object.freeze({ backend_id: backend, id: selected.id, expected_revision: selected.revision,
          request_id: crypto.randomUUID(), status, notes });
      }
      busy = true; controls(); message('Saving review…');
      const receipt = await api('/admin/entry-reviews.php', { method: 'POST', body: JSON.stringify(pending) });
      if (epoch !== generation) return;
      if (!receipt || receipt.protocol !== 1 || receipt.accepted !== true || receipt.backendId !== pending.backend_id ||
          receipt.id !== pending.id || receipt.requestId !== pending.request_id || receipt.revision !== pending.expected_revision + 1 ||
          !Number.isSafeInteger(receipt.sequence) || receipt.sequence < 1) throw new Error('Receipt does not match. Retry the same saved request; do not assume the review was saved.');
      selected.review.status = pending.status; selected.review.notes = pending.notes; selected.revision = receipt.revision;
      pending = null;
      el('entryIdentity').textContent += '\nSaved review revision: ' + receipt.revision;
      renderList(); message('Review saved on the website. Desktop reconciliation is still required.');
    } catch (error) {
      if (epoch !== generation) return;
      if (error.status === 409) {
        conflict = true; pending = null;
        message('Another change or backend mismatch prevented this save. Your draft remains below. Discard it and reload to compare the latest review before saving again.', true);
      } else if (error.status === 422 || error.status === 403) {
        pending = null; message(error.message, true);
      } else {
        message(error.message + (pending ? ' The outcome is uncertain. Retry uses the same request and values; keep this page open.' : ''), true);
      }
    } finally { if (epoch === generation) { busy = false; controls(); } }
  }
  function reset() {
    generation++; items = []; backend = null; through = null; next = null; selected = null;
    pending = null; conflict = false; busy = false; loaded = false;
    for (const id of ['entryList', 'entryAnswers', 'entryTitle', 'entryIdentity', 'entryCount']) el(id).replaceChildren();
    el('entryNotes').value = ''; el('entryStatus').value = 'pending'; el('entryFilter').value = '';
    el('entryDetail').hidden = true; message('Open this tab to load submissions.'); controls();
  }
  function open() {
    el('entryRefresh').onclick = () => load(true);
    el('entryNext').onclick = () => load();
    el('entryFilter').onchange = renderList;
    el('entryReviewForm').onsubmit = save;
    el('entryDiscard').onclick = () => {
      if (busy || !confirm('Discard this draft and reload? An uncertain request may already have been saved on the server.')) return;
      pending = null; selected = null; conflict = false; load(true);
    };
    if (!loaded) return load(true);
    controls();
  }
  window.addEventListener('beforeunload', event => {
    if (pending || dirty()) { event.preventDefault(); event.returnValue = ''; }
  });
  return { open, reset };
})();
