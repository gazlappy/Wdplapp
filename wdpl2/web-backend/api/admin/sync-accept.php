<?php
// Explicitly accept an unchanged server scorecard after durable desktop application.
// This endpoint does not merge or replace captain data. Local-wins requires a separate reviewed edit.
require_once __DIR__ . '/../_admin_sync.php';
$actor = require_admin('admin');
require_post();
try {
	$body = read_json_body();
	$id = isset($body['id']) ? $body['id'] : '';
	admin_sync_identity('scorecard', $id);
	$expected = admin_sync_integer(isset($body['expected_revision']) ? $body['expected_revision'] : null, 'expected_revision');
	$version = admin_sync_integer(isset($body['expected_version']) ? $body['expected_version'] : null, 'expected_version');
	$requestId = admin_sync_request_id(isset($body['request_id']) ? $body['request_id'] : '');
	if (!isset($body['applied']) || $body['applied'] !== true) throw new InvalidArgumentException('Durable local application must be confirmed explicitly.');
	admin_sync_ensure_schema();
	db()->beginTransaction();
	$backend = admin_sync_lock();
	if (!isset($body['backend_id']) || $body['backend_id'] !== $backend['backend_id']) {
		db()->rollBack();
		json_response(array('error' => 'Backend changed. Review required.'), 409);
	}
	$prepared = admin_sync_prepare_change($actor, 'scorecard', $id, $expected, $requestId,
		array('action' => 'accept_server', 'expected_version' => $version));
	if (!empty($prepared['conflict'])) {
		db()->rollBack();
		json_response($prepared, 409);
	}
	if (!empty($prepared['replayed'])) {
		db()->commit();
		json_response(array('protocol' => 1, 'backendId' => $backend['backend_id'], 'requestId' => $requestId,
			'id' => $id, 'revision' => $prepared['revision'], 'sequence' => $prepared['sequence'], 'accepted' => true));
	}
	$record = admin_sync_read_record('scorecard', $id);
	if (!$record || !isset($record['payload']['version']) || (int)$record['payload']['version'] !== $version) {
		db()->rollBack();
		json_response(array('error' => 'Scorecard snapshot changed. Review required.'), 409);
	}
	$query = db()->prepare('SELECT version, state_json, home_finalized_version, away_finalized_version FROM live_scorecards WHERE fixture_id = ? FOR UPDATE');
	$query->execute(array($id));
	$live = $query->fetch();
	if (!$live || (int)$live['version'] !== $version ||
		json_decode($live['state_json'], true, 512, JSON_THROW_ON_ERROR) != $record['payload']['state'] ||
		($live['home_finalized_version'] !== null) !== $record['payload']['home_finalized'] ||
		($live['away_finalized_version'] !== null) !== $record['payload']['away_finalized']) {
		db()->rollBack();
		json_response(array('error' => 'A captain or administrator changed the live card. Refresh before accepting.'), 409);
	}
	$fixture = db()->prepare('SELECT season_id FROM league_fixtures WHERE fixture_id = ?');
	$fixture->execute(array($id));
	$season = $fixture->fetchColumn();
	if (!$season) throw new InvalidArgumentException('The fixture has no explicit server season.');
	$last = db()->prepare('SELECT sequence_id, source FROM admin_sync_changes WHERE record_kind = ? AND record_id = ? AND revision = ?');
	$last->execute(array('scorecard', $id, $expected));
	$previous = $last->fetch();
	if ($previous && $previous['source'] === 'desktop') {
		$receipt = array('protocol' => 1, 'backendId' => $backend['backend_id'], 'sequence' => (int)$previous['sequence_id'], 'revision' => $expected);
	} else {
		$receipt = admin_sync_commit_change($actor, 'scorecard', $id, $season, 'desktop', $record['payload'], $prepared);
	}
	db()->commit();
	json_response($receipt + array('requestId' => $requestId, 'id' => $id, 'accepted' => true));
} catch (InvalidArgumentException $e) {
	if (db()->inTransaction()) db()->rollBack();
	json_response(array('error' => $e->getMessage()), 422);
} catch (Exception $e) {
	if (db()->inTransaction()) db()->rollBack();
	error_log('WDPL synchronization acceptance: ' . get_class($e));
	json_response(array('error' => 'Acceptance failed. Keep the local review record and retry with the same request identity.'), 500);
}
