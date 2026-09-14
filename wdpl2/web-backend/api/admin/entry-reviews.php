<?php
// Browser and desktop review metadata; never mutates original submission payloads.
require_once __DIR__ . '/../_entry_forms.php';
require_once __DIR__ . '/../_admin.php';
require_once __DIR__ . '/../_admin_sync.php';
require_once __DIR__ . '/../_admin_entry_review_rules.php';
$actor = require_admin();
try {
	ef_ensure_schema();
	admin_sync_ensure_schema();
	if ($_SERVER['REQUEST_METHOD'] === 'GET') {
		$after = admin_sync_integer($_GET['after'] ?? 0, 'after');
		db()->beginTransaction();
		$backend = admin_sync_lock();
		$maximum = (int)db()->query('SELECT COALESCE(MAX(sequence_id),0) FROM entry_form_submissions')->fetchColumn();
		$through = admin_sync_integer($_GET['through'] ?? $maximum, 'through');
		if ($after > $through || $through > $maximum) throw new InvalidArgumentException('Invalid submission range.');
		if (($after > 0 && !isset($_GET['backendId'])) || (isset($_GET['backendId']) && $_GET['backendId'] !== $backend['backend_id'])) {
			db()->rollBack(); ef_reply(array('error' => 'Backend changed. Reload entry review.'), 409);
		}
		$query = db()->prepare("SELECT s.sequence_id, s.form_id, s.client_id, s.payload_json, s.received_utc,
			r.revision, r.payload_json AS review_json FROM entry_form_submissions s
			LEFT JOIN admin_sync_records r ON r.record_kind = 'entry_review' AND r.record_id = CAST(s.sequence_id AS CHAR)
			WHERE s.sequence_id > ? AND s.sequence_id <= ? ORDER BY s.sequence_id LIMIT 51");
		$query->execute(array($after, $through));
		$rows = $query->fetchAll();
		$more = count($rows) > 50;
		if ($more) array_pop($rows);
		$items = array();
		foreach ($rows as $row) {
			$review = $row['review_json'] ? json_decode($row['review_json'], true, 512, JSON_THROW_ON_ERROR) : array('status' => 'pending', 'notes' => '');
			$items[] = array('id' => (string)$row['sequence_id'], 'revision' => (int)($row['revision'] ?? 0),
				'review' => admin_entry_review_payload($row, $review),
				'submission' => json_decode($row['payload_json'], true, 512, JSON_THROW_ON_ERROR), 'receivedUtc' => $row['received_utc']);
		}
		db()->commit();
		ef_reply(array('protocol' => 1, 'backendId' => $backend['backend_id'], 'through' => $through,
			'nextAfter' => $more ? (int)$rows[count($rows) - 1]['sequence_id'] : null, 'items' => $items));
	}
	ef_method('POST');
	$body = ef_body(32768);
	$id = admin_sync_integer($body->id ?? null, 'submission id');
	$expected = admin_sync_integer($body->expected_revision ?? null, 'expected_revision');
	$requestId = admin_sync_request_id($body->request_id ?? '');
	$review = admin_entry_review_values($body->status ?? null, $body->notes ?? null);
	db()->beginTransaction();
	$backend = admin_sync_lock();
	if (($body->backend_id ?? '') !== $backend['backend_id']) {
		db()->rollBack(); ef_reply(array('error' => 'Backend changed. Review required.'), 409);
	}
	$prepared = admin_sync_prepare_change($actor, 'entry_review', (string)$id, $expected, $requestId, $review);
	if (!empty($prepared['conflict'])) { db()->rollBack(); ef_reply($prepared, 409); }
	if (!empty($prepared['replayed'])) {
		db()->commit();
		ef_reply(array('protocol' => 1, 'backendId' => $backend['backend_id'], 'id' => (string)$id,
			'requestId' => $requestId, 'accepted' => true, 'revision' => $prepared['revision'], 'sequence' => $prepared['sequence']));
	}
	$query = db()->prepare('SELECT sequence_id, form_id, client_id FROM entry_form_submissions WHERE sequence_id = ?');
	$query->execute(array($id));
	$row = $query->fetch();
	if (!$row) { db()->rollBack(); ef_reply(array('error' => 'Submission not found.'), 404); }
	// Review writes remain web-origin until explicitly applied by the desktop;
	// callers cannot label their own writes as synchronized.
	$receipt = admin_sync_commit_change($actor, 'entry_review', (string)$id, null, 'web', admin_entry_review_payload($row, $review), $prepared);
	db()->commit();
	ef_reply($receipt + array('id' => (string)$id, 'requestId' => $requestId, 'accepted' => true));
} catch (InvalidArgumentException $e) {
	if (db()->inTransaction()) db()->rollBack();
	ef_reply(array('error' => $e->getMessage()), 422);
} catch (Exception $e) {
	if (db()->inTransaction()) db()->rollBack();
	error_log('WDPL entry review: ' . get_class($e));
	ef_reply(array('error' => 'Entry review unavailable. No review changes committed.'), 500);
}
