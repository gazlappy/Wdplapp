<?php
// Explicit local-frame resolution. Reopens the card; never silently finalizes a match.
require_once __DIR__ . '/../_admin.php';
require_once __DIR__ . '/../_admin_sync.php';
require_once __DIR__ . '/../_admin_scorecard_rules.php';
$actor = require_admin('admin');
require_post();
try {
	$body = read_json_body();
	$id = $body['id'] ?? '';
	admin_sync_identity('scorecard', $id);
	$expected = admin_sync_integer($body['expected_revision'] ?? null, 'expected_revision');
	$version = admin_sync_integer($body['expected_version'] ?? null, 'expected_version');
	$requestId = admin_sync_request_id($body['request_id'] ?? '');
	$frames = admin_scorecard_frames($body['frames'] ?? null);
	$seasonId = $body['season_id'] ?? null;
	if (!is_string($seasonId) || $seasonId === '') throw new InvalidArgumentException('Explicit season identity required.');
	admin_sync_ensure_schema();
	db()->beginTransaction();
	$backend = admin_sync_lock();
	if (($body['backend_id'] ?? '') !== $backend['backend_id']) {
		db()->rollBack(); json_response(array('error' => 'Backend changed. Review required.'), 409);
	}
	$prepared = admin_sync_prepare_change($actor, 'scorecard', $id, $expected, $requestId,
		array('action' => 'keep_local', 'version' => $version, 'season_id' => $seasonId, 'frames' => $frames));
	if (!empty($prepared['conflict'])) { db()->rollBack(); json_response($prepared, 409); }
	if (!empty($prepared['replayed'])) {
		db()->commit();
		json_response(array('protocol' => 1, 'backendId' => $backend['backend_id'], 'requestId' => $requestId,
			'id' => $id, 'revision' => $prepared['revision'], 'sequence' => $prepared['sequence'], 'accepted' => true));
	}
	$record = admin_sync_read_record('scorecard', $id);
	$query = db()->prepare('SELECT version, state_json, home_finalized_version, away_finalized_version FROM live_scorecards WHERE fixture_id = ? FOR UPDATE');
	$query->execute(array($id));
	$live = $query->fetch();
	$state = $live ? json_decode($live['state_json'], true, 512, JSON_THROW_ON_ERROR) : null;
	if (!$record || !$live || (int)$live['version'] !== $version || ($record['payload']['version'] ?? null) !== $version ||
		$state != $record['payload']['state'] ||
		($live['home_finalized_version'] !== null) !== $record['payload']['home_finalized'] ||
		($live['away_finalized_version'] !== null) !== $record['payload']['away_finalized']) {
		db()->rollBack(); json_response(array('error' => 'Live card changed. Compare the latest server card before replacing frames.'), 409);
	}
	$query = db()->prepare('SELECT season_id, home_team_id, away_team_id FROM league_fixtures WHERE fixture_id = ?');
	$query->execute(array($id));
	$fixture = $query->fetch();
	if (!$fixture || $fixture['season_id'] !== $seasonId ||
		($state['home_team_id'] ?? null) !== $fixture['home_team_id'] || ($state['away_team_id'] ?? null) !== $fixture['away_team_id'])
		throw new InvalidArgumentException('Fixture season or team identities do not match.');
	$state['frames'] = admin_scorecard_resolve_players(db(), $frames, $fixture);
	$state['last_edit'] = array('by' => 'admin:' . $actor['username'], 'at' => gmdate('c'));
	$newVersion = $version + 1;
	db()->prepare('UPDATE live_scorecards SET version = ?, state_json = ?, updated_utc = UTC_TIMESTAMP(),
		home_finalized_version = NULL, away_finalized_version = NULL, home_finalized_at = NULL, away_finalized_at = NULL WHERE fixture_id = ?')
		->execute(array($newVersion, json_encode($state, JSON_THROW_ON_ERROR), $id));
	$payload = array('state' => $state, 'version' => $newVersion, 'home_finalized' => false, 'away_finalized' => false);
	$receipt = admin_sync_commit_change($actor, 'scorecard', $id, $seasonId, 'desktop', $payload, $prepared);
	db()->commit();
	json_response($receipt + array('requestId' => $requestId, 'id' => $id, 'accepted' => true));
} catch (InvalidArgumentException $e) {
	if (db()->inTransaction()) db()->rollBack();
	json_response(array('error' => $e->getMessage()), 422);
} catch (Exception $e) {
	if (db()->inTransaction()) db()->rollBack();
	error_log('WDPL local scorecard resolution: ' . get_class($e));
	json_response(array('error' => 'Resolution failed. Keep the review request and retry without changing its contents.'), 500);
}
