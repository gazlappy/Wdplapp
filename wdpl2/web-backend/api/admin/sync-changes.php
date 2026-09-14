<?php
// Private bounded change feed. Reading is NOT an acknowledgement or conflict resolution.
require_once __DIR__ . '/../_admin.php';
require_once __DIR__ . '/../_admin_sync.php';
require_admin('admin');
if ($_SERVER['REQUEST_METHOD'] !== 'GET') json_response(array('error' => 'GET required'), 405);
try {
	$after = admin_sync_integer(isset($_GET['after']) ? $_GET['after'] : 0, 'after');
	admin_sync_ensure_schema();
	db()->beginTransaction();
	$state = admin_sync_lock();
	$through = admin_sync_integer(isset($_GET['through']) ? $_GET['through'] : $state['sequence_id'], 'through');
	$backend = isset($_GET['backendId']) ? $_GET['backendId'] : '';
	if (($after > 0 && $backend === '') || ($backend !== '' && $backend !== $state['backend_id'])) {
		db()->rollBack();
		json_response(array('error' => 'Backend identity changed or missing. Reconciliation required.'), 409);
	}
	if ($after > $through || $through > (int)$state['sequence_id']) throw new InvalidArgumentException('Invalid synchronization range.');
	$query = db()->prepare('SELECT sequence_id, record_kind, record_id, revision, season_id, source, payload_json, created_utc
		FROM admin_sync_changes WHERE sequence_id > ? AND sequence_id <= ? ORDER BY sequence_id LIMIT 21');
	$query->execute(array($after, $through));
	$rows = $query->fetchAll();
	$more = count($rows) > 20;
	if ($more) array_pop($rows);
	$items = array();
	foreach ($rows as $row) {
		$items[] = array('sequence' => (int)$row['sequence_id'], 'kind' => $row['record_kind'], 'id' => $row['record_id'],
			'revision' => (int)$row['revision'], 'seasonId' => $row['season_id'], 'source' => $row['source'],
			'payload' => json_decode($row['payload_json'], true, 512, JSON_THROW_ON_ERROR), 'changedUtc' => $row['created_utc']);
	}
	db()->commit();
	json_response(array('protocol' => 1, 'backendId' => $state['backend_id'], 'through' => $through,
		'nextAfter' => $more ? (int)$rows[count($rows) - 1]['sequence_id'] : null, 'items' => $items));
} catch (InvalidArgumentException $e) {
	if (db()->inTransaction()) db()->rollBack();
	json_response(array('error' => $e->getMessage()), 422);
} catch (Exception $e) {
	if (db()->inTransaction()) db()->rollBack();
	error_log('WDPL synchronization feed: ' . get_class($e));
	json_response(array('error' => 'Synchronization unavailable. No changes acknowledged.'), 500);
}
