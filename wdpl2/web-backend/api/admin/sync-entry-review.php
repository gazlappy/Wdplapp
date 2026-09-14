<?php
// Version-checked resolution of review metadata. Original answers remain immutable.
require_once __DIR__ . '/../_admin.php';
require_once __DIR__ . '/../_admin_sync.php';
require_once __DIR__ . '/../_admin_entry_review_rules.php';
$actor = require_admin('admin');
require_post();
try {
	$body = read_json_body();
	$id = admin_sync_integer($body['id'] ?? null, 'submission id');
	$expected = admin_sync_integer($body['expected_revision'] ?? null, 'expected_revision');
	$requestId = admin_sync_request_id($body['request_id'] ?? '');
	$mode = $body['mode'] ?? '';
	if (!in_array($mode, array('server', 'local'), true) || ($body['applied'] ?? false) !== true)
		throw new InvalidArgumentException('Choose a reviewed resolution and confirm durable local application.');
	$review = admin_entry_review_values($body['status'] ?? null, $body['notes'] ?? null);
	admin_sync_ensure_schema();
	db()->beginTransaction();
	$backend = admin_sync_lock();
	if (($body['backend_id'] ?? '') !== $backend['backend_id']) {
		db()->rollBack(); json_response(array('error' => 'Backend changed. Review required.'), 409);
	}
	$prepared = admin_sync_prepare_change($actor, 'entry_review', (string)$id, $expected, $requestId,
		array('action' => 'resolve_review', 'mode' => $mode, 'review' => $review));
	if (!empty($prepared['conflict'])) { db()->rollBack(); json_response($prepared, 409); }
	if (!empty($prepared['replayed'])) {
		db()->commit();
		json_response(array('protocol' => 1, 'backendId' => $backend['backend_id'], 'requestId' => $requestId,
			'id' => (string)$id, 'revision' => $prepared['revision'], 'sequence' => $prepared['sequence'], 'accepted' => true));
	}
	$record = admin_sync_read_record('entry_review', (string)$id);
	if (!$record) throw new InvalidArgumentException('Download and review the server metadata first.');
	$query = db()->prepare('SELECT sequence_id, form_id, client_id FROM entry_form_submissions WHERE sequence_id = ?');
	$query->execute(array($id));
	$row = $query->fetch();
	if (!$row || $record['payload']['formId'] !== $row['form_id'] || $record['payload']['clientId'] !== $row['client_id'])
		throw new InvalidArgumentException('Submission identity mismatch.');
	if ($mode === 'server' && ($review['status'] !== $record['payload']['status'] || $review['notes'] !== $record['payload']['notes'])) {
		db()->rollBack(); json_response(array('error' => 'Reviewed values differ from the server revision.'), 409);
	}
	$query = db()->prepare('SELECT sequence_id, source FROM admin_sync_changes WHERE record_kind = ? AND record_id = ? AND revision = ?');
	$query->execute(array('entry_review', (string)$id, $expected));
	$last = $query->fetch();
	if ($mode === 'server' && $last && $last['source'] === 'desktop') {
		$receipt = admin_sync_acknowledge($actor, 'entry_review', (string)$id, $prepared);
	} else {
		$receipt = admin_sync_commit_change($actor, 'entry_review', (string)$id, null, 'desktop', admin_entry_review_payload($row, $review), $prepared);
	}
	db()->commit();
	json_response($receipt + array('requestId' => $requestId, 'id' => (string)$id, 'accepted' => true));
} catch (InvalidArgumentException $e) {
	if (db()->inTransaction()) db()->rollBack();
	json_response(array('error' => $e->getMessage()), 422);
} catch (Exception $e) {
	if (db()->inTransaction()) db()->rollBack();
	error_log('WDPL entry review resolution: ' . get_class($e));
	json_response(array('error' => 'Resolution unavailable. Keep the saved request for retry.'), 500);
}
