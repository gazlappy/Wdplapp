<?php
// Read-only comparison. Never acknowledges or replaces a queued revision.
require_once __DIR__ . '/../_admin.php';
require_once __DIR__ . '/../_admin_sync.php';
require_admin('admin');
if ($_SERVER['REQUEST_METHOD'] !== 'GET') json_response(array('error' => 'GET required'), 405);
try {
	$kind = $_GET['kind'] ?? '';
	$id = $_GET['id'] ?? '';
	admin_sync_identity($kind, $id);
	admin_sync_ensure_schema();
	db()->beginTransaction();
	$backend = admin_sync_lock();
	if (($_GET['backendId'] ?? '') !== $backend['backend_id']) {
		db()->rollBack(); json_response(array('error' => 'Backend changed. Keep the saved review queue.'), 409);
	}
	$record = admin_sync_read_record($kind, $id);
	if (!$record) { db()->rollBack(); json_response(array('error' => 'No journal record exists for this identity.'), 404); }
	$query = db()->prepare('SELECT sequence_id, season_id, source, payload_json FROM admin_sync_changes WHERE record_kind = ? AND record_id = ? AND revision = ?');
	$query->execute(array($kind, $id, $record['revision']));
	$row = $query->fetch();
	if (!$row || json_decode($row['payload_json'], true, 512, JSON_THROW_ON_ERROR) != $record['payload'])
		throw new RuntimeException('Journal record mismatch.');
	$live = null;
	$matches = true;
	if ($kind === 'scorecard') {
		$query = db()->prepare('SELECT version, state_json, home_finalized_version, away_finalized_version FROM live_scorecards WHERE fixture_id = ? FOR UPDATE');
		$query->execute(array($id));
		$card = $query->fetch();
		if ($card) {
			$live = array('version' => (int)$card['version'], 'state' => json_decode($card['state_json'], true, 512, JSON_THROW_ON_ERROR),
				'home_finalized' => $card['home_finalized_version'] !== null, 'away_finalized' => $card['away_finalized_version'] !== null);
		}
		$payload = $record['payload'];
		$matches = ($payload['deleted'] ?? false) === true ? $live === null :
			($live !== null && isset($payload['version'], $payload['state'], $payload['home_finalized'], $payload['away_finalized']) &&
			$live['version'] === $payload['version'] && $live['state'] == $payload['state'] &&
			$live['home_finalized'] === $payload['home_finalized'] && $live['away_finalized'] === $payload['away_finalized']);
	}
	db()->commit();
	json_response(array('protocol' => 1, 'backendId' => $backend['backend_id'], 'liveMatchesJournal' => $matches, 'live' => $live,
		'item' => array('sequence' => (int)$row['sequence_id'], 'kind' => $kind, 'id' => $id, 'revision' => $record['revision'],
			'seasonId' => $row['season_id'], 'source' => $row['source'], 'payload' => $record['payload'])));
} catch (InvalidArgumentException $e) {
	if (db()->inTransaction()) db()->rollBack();
	json_response(array('error' => $e->getMessage()), 422);
} catch (Exception $e) {
	if (db()->inTransaction()) db()->rollBack();
	error_log('WDPL current review comparison: ' . get_class($e));
	json_response(array('error' => 'Comparison unavailable. No changes acknowledged.'), 500);
}
