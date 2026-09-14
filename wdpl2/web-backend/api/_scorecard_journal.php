<?php
require_once __DIR__ . '/_admin_sync.php';

// The caller holds the journal lock before reading or changing domain rows.
function scorecard_journal_prepare($actor, $fixtureId, $action) {
	admin_sync_lock();
	$current = admin_sync_read_record('scorecard', $fixtureId);
	return admin_sync_prepare_change($actor, 'scorecard', $fixtureId, $current ? $current['revision'] : 0,
		admin_sync_guid(), array('legacy_action' => $action));
}

function scorecard_journal_commit($actor, $fixtureId, $seasonId, $prepared, $deleted = false) {
	if (!is_string($seasonId) || $seasonId === '') throw new InvalidArgumentException('Scorecard season is required.');
	$query = db()->prepare('SELECT version, state_json, home_finalized_version, away_finalized_version FROM live_scorecards WHERE fixture_id = ? FOR UPDATE');
	$query->execute(array($fixtureId));
	$row = $query->fetch();
	if ($deleted) {
		if ($row) throw new LogicException('Deleted scorecard still exists.');
		$payload = array('deleted' => true);
	} else {
		if (!$row) throw new LogicException('Scorecard missing after write.');
		$payload = array('version' => (int)$row['version'], 'state' => json_decode($row['state_json'], true, 512, JSON_THROW_ON_ERROR),
			'home_finalized' => $row['home_finalized_version'] !== null, 'away_finalized' => $row['away_finalized_version'] !== null);
	}
	return admin_sync_commit_change($actor, 'scorecard', $fixtureId, $seasonId, 'web', $payload, $prepared);
}

function scorecard_journal_season($fixtureId) {
	$query = db()->prepare('SELECT season_id FROM league_fixtures WHERE fixture_id = ? FOR UPDATE');
	$query->execute(array($fixtureId));
	$season = $query->fetchColumn();
	if (!is_string($season) || $season === '') throw new InvalidArgumentException('Scorecard fixture needs an explicit season.');
	return $season;
}
